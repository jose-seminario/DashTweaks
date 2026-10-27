using System.Diagnostics;
using System.Text.Json;

namespace DashTweaks.Core.Managers;

public class BloatwareAppInfo
{
    public string DisplayName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string PackageFamilyName { get; set; } = string.Empty;
    public string PackageFullName { get; set; } = string.Empty;
    public string WingetId { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class BloatwareManager
{
    // Catálogo seguro de bloatware común no crítico de Windows
    private static readonly List<BloatwareAppInfo> KnownBloatware = new()
    {
        new BloatwareAppInfo
        {
            DisplayName = "Xbox Console Companion",
            PackageName = "Microsoft.XboxApp",
            PackageFamilyName = "Microsoft.XboxApp_8wekyb3d8bbwe",
            WingetId = "9MV0B5HZVK9Z",
            Description = "Aplicación heredada de Xbox. (Nota: puede ser requerida para ciertas funciones de Game Pass)."
        },
        new BloatwareAppInfo
        {
            DisplayName = "Microsoft Noticias",
            PackageName = "Microsoft.BingNews",
            PackageFamilyName = "Microsoft.BingNews_8wekyb3d8bbwe",
            WingetId = "9WZDNCRFHVFW",
            Description = "Noticias de MSN y widgets de información en segundo plano."
        },
        new BloatwareAppInfo
        {
            DisplayName = "El Tiempo (MSN Weather)",
            PackageName = "Microsoft.BingWeather",
            PackageFamilyName = "Microsoft.BingWeather_8wekyb3d8bbwe",
            WingetId = "9WZDNCRFJ3Q2",
            Description = "Servicio meteorológico de Microsoft con actualizaciones periódicas en segundo plano."
        },
        new BloatwareAppInfo
        {
            DisplayName = "Skype",
            PackageName = "Microsoft.SkypeApp",
            PackageFamilyName = "Microsoft.SkypeApp_kzf8qxf38zg5c",
            WingetId = "9WZDNCRFJ364",
            Description = "Cliente de mensajería y llamadas preinstalado en Windows."
        },
        new BloatwareAppInfo
        {
            DisplayName = "Obtener Ayuda (Get Help)",
            PackageName = "Microsoft.GetHelp",
            PackageFamilyName = "Microsoft.GetHelp_8wekyb3d8bbwe",
            WingetId = "9PKDZBMV1H3T",
            Description = "Herramienta de asistencia automatizada y soporte de Windows."
        },
        new BloatwareAppInfo
        {
            DisplayName = "Sugerencias e Introducción",
            PackageName = "Microsoft.Getstarted",
            PackageFamilyName = "Microsoft.Getstarted_8wekyb3d8bbwe",
            WingetId = "9WZDNCRFJ4PV",
            Description = "Consejos publicitarios y tutoriales de funciones de Windows."
        },
        new BloatwareAppInfo
        {
            DisplayName = "Centro de Comentarios (Feedback Hub)",
            PackageName = "Microsoft.WindowsFeedbackHub",
            PackageFamilyName = "Microsoft.WindowsFeedbackHub_8wekyb3d8bbwe",
            WingetId = "9NBLGGH4R32N",
            Description = "Envío de diagnósticos de telemetría y sugerencias a Microsoft."
        },
        new BloatwareAppInfo
        {
            DisplayName = "Reproductor Multimedia (Groove)",
            PackageName = "Microsoft.ZuneMusic",
            PackageFamilyName = "Microsoft.ZuneMusic_8wekyb3d8bbwe",
            WingetId = "9WZDNCRFJ3PT",
            Description = "Reproductor de audio integrado de Windows."
        },
        new BloatwareAppInfo
        {
            DisplayName = "Películas y TV (Zune Video)",
            PackageName = "Microsoft.ZuneVideo",
            PackageFamilyName = "Microsoft.ZuneVideo_8wekyb3d8bbwe",
            WingetId = "9WZDNCRFJ3P2",
            Description = "Reproductor de vídeo y tienda de películas de Microsoft."
        }
    };

    /// <summary>
    /// Consulta paquetes UWP instalados y cruza con la lista de bloatware conocido.
    /// </summary>
    public List<BloatwareAppInfo> GetInstalledBloatware()
    {
        var resultList = new List<BloatwareAppInfo>();

        try
        {
            // Ejecutar PowerShell silencioso para listar paquetes UWP
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -AllUsers | Select-Object Name, PackageFullName, PackageFamilyName | ConvertTo-Json -Compress\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var run = ProcessRunner.Run(startInfo, 10000);
            {
                string jsonOutput = run.StdOut;

                var installedPackages = ParseInstalledPackages(jsonOutput);

                foreach (var target in KnownBloatware)
                {
                    var item = new BloatwareAppInfo
                    {
                        DisplayName = target.DisplayName,
                        PackageName = target.PackageName,
                        PackageFamilyName = target.PackageFamilyName,
                        WingetId = target.WingetId,
                        Description = target.Description
                    };

                    // Comprobar si está instalado en el sistema
                    if (installedPackages.TryGetValue(target.PackageName, out var foundPackage))
                    {
                        item.IsInstalled = true;
                        item.PackageFullName = foundPackage.PackageFullName;
                        if (!string.IsNullOrEmpty(foundPackage.PackageFamilyName))
                        {
                            item.PackageFamilyName = foundPackage.PackageFamilyName;
                        }
                    }
                    else
                    {
                        item.IsInstalled = false;
                        item.PackageFullName = string.Empty;
                    }

                    resultList.Add(item);
                }

                return resultList;
            }
        }
        catch
        {
            // Retornar lista base con estado no instalado en caso de error
        }

        return KnownBloatware;
    }

    /// <summary>
    /// Desinstala un paquete UWP mediante Remove-AppxPackage de forma segura.
    /// </summary>
    public bool UninstallApp(BloatwareAppInfo app, out string message)
    {
        if (string.IsNullOrWhiteSpace(app.PackageFullName))
        {
            message = $"[ERROR] No se pudo desinstalar {app.DisplayName}. No se detectó un PackageFullName válido.";
            return false;
        }

        // Validación estricta: evita inyección de comandos en PowerShell
        if (!System.Text.RegularExpressions.Regex.IsMatch(app.PackageFullName, @"^[A-Za-z0-9._~\-]+$"))
        {
            message = $"[ERROR] PackageFullName inválido para {app.DisplayName}.";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Remove-AppxPackage -Package '{app.PackageFullName}' -AllUsers\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var run = ProcessRunner.Run(startInfo, 15000);
            string errorOutput = run.TimedOut ? "tiempo de espera agotado" : run.StdErr;

            if (!run.TimedOut && run.ExitCode == 0)
            {
                app.IsInstalled = false;
                app.PackageFullName = string.Empty;
                message = $"[ÉXITO] {app.DisplayName} desinstalado correctamente.";
                return true;
            }

            // Salvaguarda 2: Manejo de error claro y seguro sin romper la app
            message = $"[ERROR] No se pudo desinstalar {app.DisplayName}. Puede requerir reinicio o permisos especiales. ({errorOutput.Trim()})";
            return false;
        }
        catch (Exception ex)
        {
            message = $"[ERROR] No se pudo desinstalar {app.DisplayName}. Puede requerir reinicio o permisos especiales: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Reinstala la aplicación mediante winget con fallback automático hacia la Microsoft Store.
    /// </summary>
    public bool RestoreApp(BloatwareAppInfo app, out string message)
    {
        // 1. Intentar instalación desatendida mediante winget
        if (!string.IsNullOrWhiteSpace(app.WingetId))
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "winget.exe",
                    Arguments = $"install --id \"{app.WingetId}\" --silent --accept-package-agreements --accept-source-agreements",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var run = ProcessRunner.Run(startInfo, 30000); // 30 segundos límite para descarga
                if (!run.TimedOut && run.ExitCode == 0)
                {
                    app.IsInstalled = true;
                    message = $"[ÉXITO] {app.DisplayName} reinstalado exitosamente vía winget.";
                    return true;
                }
            }
            catch
            {
                // Si winget no está instalado o falla, proceder al Plan B de Microsoft Store
            }
        }

        // 2. Salvaguarda 1 (Plan B): Abrir directamente la ficha en Microsoft Store
        try
        {
            string storeUri = !string.IsNullOrWhiteSpace(app.PackageFamilyName)
                ? $"ms-windows-store://pdp/?PFN={app.PackageFamilyName}"
                : $"ms-windows-store://search/?query={Uri.EscapeDataString(app.DisplayName)}";

            var storeProcess = new ProcessStartInfo
            {
                FileName = storeUri,
                UseShellExecute = true
            };

            Process.Start(storeProcess);
            message = $"[TIENDA] Abriendo Microsoft Store para '{app.DisplayName}'. Haz clic en 'Instalar' para restaurarla.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"[ERROR] No se pudo abrir la tienda para restaurar {app.DisplayName}: {ex.Message}";
            return false;
        }
    }

    private Dictionary<string, (string PackageFullName, string PackageFamilyName)> ParseInstalledPackages(string json)
    {
        var dict = new Dictionary<string, (string PackageFullName, string PackageFamilyName)>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json)) return dict;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    AddPackageFromElement(element, dict);
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                AddPackageFromElement(doc.RootElement, dict);
            }
        }
        catch
        {
            // Error de parseo JSON ignorado de forma segura
        }

        return dict;
    }

    private void AddPackageFromElement(JsonElement element, Dictionary<string, (string PackageFullName, string PackageFamilyName)> dict)
    {
        string name = element.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
        string fullName = element.TryGetProperty("PackageFullName", out var fnProp) ? fnProp.GetString() ?? string.Empty : string.Empty;
        string familyName = element.TryGetProperty("PackageFamilyName", out var fmProp) ? fmProp.GetString() ?? string.Empty : string.Empty;

        if (!string.IsNullOrWhiteSpace(name) && !dict.ContainsKey(name))
        {
            dict[name] = (fullName, familyName);
        }
    }
}
