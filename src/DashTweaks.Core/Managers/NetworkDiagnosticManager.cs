using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DashTweaks.Core.Managers;

public enum MetricSeverity
{
    Optimal,   // Verde
    Moderate,  // Amarillo
    Critical   // Rojo
}

public class DiagnosticMetric
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public MetricSeverity Severity { get; set; }

    public string ColorHex => Severity switch
    {
        MetricSeverity.Optimal => "#6CCB76",
        MetricSeverity.Moderate => "#F59E0B",
        MetricSeverity.Critical => "#FF6B5E",
        _ => "#CCCCCC"
    };

    public string BackgroundHex => Severity switch
    {
        MetricSeverity.Optimal => "#17291A",
        MetricSeverity.Moderate => "#2B2211",
        MetricSeverity.Critical => "#3D1814",
        _ => "#242428"
    };

    public string BorderHex => Severity switch
    {
        MetricSeverity.Optimal => "#244D29",
        MetricSeverity.Moderate => "#594419",
        MetricSeverity.Critical => "#6B2820",
        _ => "#383842"
    };
}

public class NetworkDiagnosticResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool UsedTcpFallback { get; set; }

    public double PacketLossPercent { get; set; }
    public double BaseLatencyMs { get; set; }
    public double JitterMs { get; set; }
    public double LoadedLatencyMs { get; set; }
    public double BufferbloatMs { get; set; }

    public DiagnosticMetric PacketLossMetric { get; set; } = new();
    public DiagnosticMetric JitterMetric { get; set; } = new();
    public DiagnosticMetric BufferbloatMetric { get; set; } = new();
    public DiagnosticMetric BaseLatencyMetric { get; set; } = new();

    public List<string> Recommendations { get; set; } = new();
}

public class NetworkDiagnosticManager
{
    private const string TargetHost = "1.1.1.1";
    private const string FallbackUrl = "http://1.1.1.1";
    private const string BufferbloatTestUrl = "http://speed.cloudflare.com/__down?bytes=10485760"; // 10MB test payload

    /// <summary>
    /// Ejecuta el diagnóstico completo de red: 20 mediciones de latencia/jitter y prueba de bufferbloat bajo carga.
    /// </summary>
    public async Task<NetworkDiagnosticResult> RunFullDiagnosticAsync(IProgress<string>? logger = null)
    {
        var result = new NetworkDiagnosticResult();

        try
        {
            logger?.Report("[DIAGNÓSTICO] Iniciando prueba de estabilidad y latencia base (20 muestras a 1.1.1.1)...");

            // Salvaguarda 1: Detección y Plan B si ICMP (Ping) está bloqueado por el firewall/ISP
            bool useTcpFallback = await CheckIfIcmpBlockedAsync();
            result.UsedTcpFallback = useTcpFallback;

            if (useTcpFallback)
            {
                logger?.Report("[DIAGNÓSTICO] ICMP bloqueado por la red. Usando medición vía TCP.");
            }

            // 1. Ejecutar las 20 mediciones base
            List<double> latencies = new();
            int failedAttempts = 0;

            for (int i = 0; i < 20; i++)
            {
                double? pingMs = useTcpFallback
                    ? await MeasureTcpLatencyAsync()
                    : await MeasureIcmpLatencyAsync();

                if (pingMs.HasValue)
                {
                    latencies.Add(pingMs.Value);
                }
                else
                {
                    failedAttempts++;
                }

                await Task.Delay(40); // Pequeña pausa para no saturar colas
            }

            // Si falló el 100% de las peticiones
            if (latencies.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No se pudo obtener respuesta del host de prueba. Verifica tu conexión a internet.";
                return result;
            }

            result.PacketLossPercent = (failedAttempts / 20.0) * 100.0;
            result.BaseLatencyMs = latencies.Average();
            result.JitterMs = CalculateStandardDeviation(latencies);

            logger?.Report($"[DIAGNÓSTICO] Latencia Base: {result.BaseLatencyMs:F1}ms • Jitter: {result.JitterMs:F1}ms • Packet Loss: {result.PacketLossPercent:F1}%");

            // 2. Ejecutar prueba de Bufferbloat (latencia bajo carga)
            logger?.Report("[DIAGNÓSTICO] Iniciando prueba de Bufferbloat (descarga activa de prueba)...");
            double loadedLatency = await RunBufferbloatTestInternalAsync(useTcpFallback, logger);
            result.LoadedLatencyMs = loadedLatency;
            result.BufferbloatMs = Math.Max(0, loadedLatency - result.BaseLatencyMs);

            logger?.Report($"[DIAGNÓSTICO] Latencia bajo carga: {result.LoadedLatencyMs:F1}ms • Bufferbloat: +{result.BufferbloatMs:F1}ms");

            // 3. Evaluar métricas para los semáforos (Verde / Amarillo / Rojo)
            BuildMetrics(result);

            // 4. Generar recomendaciones dirigidas
            BuildRecommendations(result);

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error durante el diagnóstico: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Salvaguarda 1: Verifica si los primeros 3 pings ICMP fallan para conmutar a TCP.
    /// </summary>
    private static async Task<bool> CheckIfIcmpBlockedAsync()
    {
        using var ping = new Ping();
        int failed = 0;

        for (int i = 0; i < 3; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(TargetHost, 800);
                if (reply.Status != IPStatus.Success)
                {
                    failed++;
                }
            }
            catch
            {
                failed++;
            }
        }

        // Si fallaron los 3 pings ICMP, probar si TCP responde
        if (failed == 3)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var resp = await client.GetAsync(FallbackUrl);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static async Task<double?> MeasureIcmpLatencyAsync()
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(TargetHost, 1000);
            if (reply.Status == IPStatus.Success)
            {
                return reply.RoundtripTime;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<double?> MeasureTcpLatencyAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(TargetHost, 80);
            var completed = await Task.WhenAny(connectTask, Task.Delay(1000));

            if (completed == connectTask && client.Connected)
            {
                sw.Stop();
                return sw.Elapsed.TotalMilliseconds;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Salvaguarda 2: Mide la latencia mientras se realiza una descarga, asegurando la cancelación y liberación inmediata del HttpClient.
    /// </summary>
    private static async Task<double> RunBufferbloatTestInternalAsync(bool useTcpFallback, IProgress<string>? logger)
    {
        using var cts = new CancellationTokenSource();
        var downloadTask = Task.Run(async () =>
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                using var response = await httpClient.GetAsync(BufferbloatTestUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                byte[] buffer = new byte[65536];

                while (!cts.Token.IsCancellationRequested && await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token) > 0)
                {
                    // Consumir datos para saturar el ancho de banda temporalmente
                }
            }
            catch { /* Cancelación esperada al finalizar el test */ }
        }, cts.Token);

        // Esperar brevemente a que la descarga gane tracción
        await Task.Delay(400);

        List<double> loadedLatencies = new();

        try
        {
            for (int i = 0; i < 10; i++)
            {
                double? pingMs = useTcpFallback
                    ? await MeasureTcpLatencyAsync()
                    : await MeasureIcmpLatencyAsync();

                if (pingMs.HasValue)
                {
                    loadedLatencies.Add(pingMs.Value);
                }

                await Task.Delay(60);
            }
        }
        finally
        {
            // SALVAGUARDA 2: Cancelar y liberar la descarga inmediatamente para no consumir ancho de banda de fondo
            cts.Cancel();
            try { await Task.WhenAny(downloadTask, Task.Delay(500)); } catch { }
            logger?.Report("[DIAGNÓSTICO] Flujo de prueba de Bufferbloat detenido y liberado.");
        }

        return loadedLatencies.Any() ? loadedLatencies.Average() : 0;
    }

    private static double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count < 2) return 0;
        double avg = values.Average();
        double sumSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }

    private static void BuildMetrics(NetworkDiagnosticResult result)
    {
        // 1. Packet Loss
        MetricSeverity lossSev = result.PacketLossPercent switch
        {
            0 => MetricSeverity.Optimal,
            <= 1.0 => MetricSeverity.Moderate,
            _ => MetricSeverity.Critical
        };

        result.PacketLossMetric = new DiagnosticMetric
        {
            Name = "Pérdida de Paquetes",
            Value = result.PacketLossPercent,
            FormattedValue = $"{result.PacketLossPercent:F1} %",
            Severity = lossSev,
            StatusText = lossSev switch
            {
                MetricSeverity.Optimal => "0% (Excelente)",
                MetricSeverity.Moderate => "Moderada",
                _ => "Crítica (Inestable)"
            }
        };

        // 2. Jitter
        MetricSeverity jitterSev = result.JitterMs switch
        {
            < 5.0 => MetricSeverity.Optimal,
            <= 30.0 => MetricSeverity.Moderate,
            _ => MetricSeverity.Critical
        };

        result.JitterMetric = new DiagnosticMetric
        {
            Name = "Jitter (Fluctuación)",
            Value = result.JitterMs,
            FormattedValue = $"{result.JitterMs:F1} ms",
            Severity = jitterSev,
            StatusText = jitterSev switch
            {
                MetricSeverity.Optimal => "Estable (< 5ms)",
                MetricSeverity.Moderate => "Aceptable (< 30ms)",
                _ => "Elevado (Lag Spikes)"
            }
        };

        // 3. Bufferbloat
        MetricSeverity bloatSev = result.BufferbloatMs switch
        {
            < 15.0 => MetricSeverity.Optimal,
            <= 50.0 => MetricSeverity.Moderate,
            _ => MetricSeverity.Critical
        };

        result.BufferbloatMetric = new DiagnosticMetric
        {
            Name = "Bufferbloat (+ms)",
            Value = result.BufferbloatMs,
            FormattedValue = $"+{result.BufferbloatMs:F1} ms",
            Severity = bloatSev,
            StatusText = bloatSev switch
            {
                MetricSeverity.Optimal => "Excelente (< 15ms)",
                MetricSeverity.Moderate => "Aceptable (< 50ms)",
                _ => "Crítico (> 50ms)"
            }
        };

        // 4. Latencia Base
        result.BaseLatencyMetric = new DiagnosticMetric
        {
            Name = "Latencia Base (Ping)",
            Value = result.BaseLatencyMs,
            FormattedValue = $"{result.BaseLatencyMs:F1} ms",
            Severity = result.BaseLatencyMs < 40 ? MetricSeverity.Optimal : (result.BaseLatencyMs < 80 ? MetricSeverity.Moderate : MetricSeverity.Critical),
            StatusText = result.BaseLatencyMs < 40 ? "Excelente" : (result.BaseLatencyMs < 80 ? "Moderada" : "Alta")
        };
    }

    private static void BuildRecommendations(NetworkDiagnosticResult result)
    {
        result.Recommendations.Clear();

        bool hasInstability = result.PacketLossPercent > 1.0 || result.JitterMs > 30.0;
        bool hasBufferbloat = result.BufferbloatMs > 50.0;

        if (hasInstability)
        {
            result.Recommendations.Add("⚠️ Se detecta inestabilidad en la conexión (Pérdida de paquetes o Jitter elevado). Si estás conectado por Wi-Fi, cambia a cable Ethernet. Si ya usas cable, revisa el cable de red y el router/módem.");
        }

        if (hasBufferbloat)
        {
            result.Recommendations.Add($"⚠️ Tu latencia se dispara bajo carga (Bufferbloat: +{result.BufferbloatMs:F1}ms). Esto se produce en el router por saturación de colas. Para corregirlo, activa SQM/QoS en la configuración de tu router o limita el ancho de banda de subida. Mientras tanto, evita realizar descargas durante el juego.");
        }

        if (!hasInstability && !hasBufferbloat)
        {
            result.Recommendations.Add("✅ Tu conexión es óptima para gaming (0% pérdida de paquetes, jitter mínimo y colas bajo control). Aplica el tweak de 'Latencia de Red' (si usas Ethernet) para una optimización competitiva extra.");
        }
        else if (result.BufferbloatMs > 15.0 && result.BufferbloatMs <= 50.0)
        {
            result.Recommendations.Add($"ℹ️ Tu conexión presenta un incremento moderado de latencia bajo carga (+{result.BufferbloatMs:F1}ms). Se recomienda configurar QoS en tu router para priorizar los paquetes de tu PC.");
        }

        if (hasInstability || hasBufferbloat)
        {
            result.Recommendations.Add("🔧 Acción Recomendada: Haz clic en 'LIMPIAR RUTA DE RED' para restablecer las rutas de paquetes y la caché DNS de Windows.");
        }
    }
}
