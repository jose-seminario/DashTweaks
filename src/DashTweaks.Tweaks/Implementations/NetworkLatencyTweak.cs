using System.Net.NetworkInformation;
using System.Security;
using System.Text.Json;
using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class NetworkLatencyTweak : ITweak
{
    private const string NetworkClassKeyPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    private static readonly string BackupFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DashTweaks",
        "nic_backup.json"
    );

    public string Id => "network-adapter-latency-tuning";
    public string Name => "Optimizador de Latencia de Red (NIC / DPC Tuning)";
    public string Description => "Convierte tu tarjeta Ethernet en modo competitivo: procesa los paquetes sin esperas ni ahorros de energía para anular picos de lag.";
    public string Category => "Network";
    public RiskLevel Risk => RiskLevel.Aggressive;
    public bool RequiresRestart => true;

    public string StatusMessage { get; private set; } = string.Empty;
    public string ActiveAdapterName { get; private set; } = string.Empty;
    public bool IsApplied { get; private set; }

    public NetworkLatencyTweak()
    {
        IsApplied = CheckIsApplied();
    }

    public bool Apply()
    {
        try
        {
            // 1. Identificar la tarjeta de red activa conectada a internet
            var activeNic = GetActiveNetworkInterface();
            if (activeNic == null)
            {
                StatusMessage = "[ERROR] No se detectó ninguna tarjeta de red activa con conexión a internet.";
                return false;
            }

            ActiveAdapterName = activeNic.Description;

            // Salvaguarda 1 (CRÍTICA): Detección y Bloqueo de Wi-Fi
            if (activeNic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                activeNic.Description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                activeNic.Description.Contains("Wireless", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "[ERROR] Este tweak solo es compatible con conexiones por cable (Ethernet). Desactivar estas opciones en Wi-Fi causará pérdida de paquetes.";
                return false;
            }

            // 2. Localizar la subclave del adaptador en el Registro de Windows
            string? subKeyName = FindAdapterSubKey(activeNic.Id, activeNic.Description);
            if (string.IsNullOrEmpty(subKeyName))
            {
                StatusMessage = $"[ERROR] No se pudo localizar la configuración del registro para el adaptador: {activeNic.Description}";
                return false;
            }

            // Salvaguarda 2: Manejo de Excepciones de Registro
            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(NetworkClassKeyPath, true);
                if (baseKey == null)
                {
                    StatusMessage = "[ERROR] No se pudo acceder a la clave de configuración de adaptadores en HKLM.";
                    return false;
                }

                using var adapterKey = baseKey.OpenSubKey(subKeyName, true);
                if (adapterKey == null)
                {
                    StatusMessage = $"[ERROR] No se pudo abrir la subclave del adaptador ({subKeyName}) para escritura.";
                    return false;
                }

                // Guardar valores originales para Revert()
                SaveOriginalValues(subKeyName, activeNic.Description, adapterKey);

                // Aplicar optimizaciones de latencia (DPC / NIC Tuning)
                adapterKey.SetValue("*InterruptModeration", "0", RegistryValueKind.String);
                adapterKey.SetValue("*EEE", "0", RegistryValueKind.String);
                adapterKey.SetValue("*GreenEthernet", "0", RegistryValueKind.String);
                adapterKey.SetValue("*ReceiveBuffers", "2048", RegistryValueKind.String);

                IsApplied = true;
                StatusMessage = $"[RED] Interrupt Moderation y EEE desactivados en {ActiveAdapterName}.";
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException)
            {
                StatusMessage = "[ERROR] No se pudo modificar el registro de la tarjeta de red. Asegúrate de tener permisos de administrador o actualiza los controladores.";
                return false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"[ERROR] No se pudo modificar el registro de la tarjeta de red: {ex.Message}";
                return false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"[ERROR] Fallo general al optimizar adaptador de red: {ex.Message}";
            return false;
        }
    }

    public bool Revert()
    {
        try
        {
            NicBackupData? backup = LoadBackupData();
            string? subKeyName = backup?.SubKey;

            if (string.IsNullOrEmpty(subKeyName))
            {
                var activeNic = GetActiveNetworkInterface();
                if (activeNic != null)
                {
                    subKeyName = FindAdapterSubKey(activeNic.Id, activeNic.Description);
                    ActiveAdapterName = activeNic.Description;
                }
            }
            else
            {
                ActiveAdapterName = backup?.AdapterName ?? "Adaptador Ethernet";
            }

            if (string.IsNullOrEmpty(subKeyName))
            {
                StatusMessage = "[ERROR] No se encontró información previa del adaptador para revertir.";
                return false;
            }

            // Salvaguarda 2: Manejo de Excepciones de Registro en Reversión
            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(NetworkClassKeyPath, true);
                if (baseKey == null)
                {
                    StatusMessage = "[ERROR] No se pudo acceder a la clave de adaptadores en HKLM.";
                    return false;
                }

                using var adapterKey = baseKey.OpenSubKey(subKeyName, true);
                if (adapterKey == null)
                {
                    StatusMessage = $"[ERROR] No se pudo abrir la subclave del adaptador ({subKeyName}) para revertir.";
                    return false;
                }

                if (backup != null && backup.Values.Count > 0)
                {
                    foreach (var kvp in backup.Values)
                    {
                        if (kvp.Value == null)
                        {
                            adapterKey.DeleteValue(kvp.Key, false);
                        }
                        else
                        {
                            adapterKey.SetValue(kvp.Key, kvp.Value, RegistryValueKind.String);
                        }
                    }
                }
                else
                {
                    // Si no había backup previo, devolver a valores seguros por defecto de Windows
                    adapterKey.SetValue("*InterruptModeration", "1", RegistryValueKind.String);
                    adapterKey.SetValue("*EEE", "1", RegistryValueKind.String);
                    adapterKey.DeleteValue("*GreenEthernet", false);
                    adapterKey.SetValue("*ReceiveBuffers", "256", RegistryValueKind.String);
                }

                DeleteBackupFile();

                IsApplied = false;
                StatusMessage = $"[RED] Valores originales de red restaurados en {ActiveAdapterName}.";
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException)
            {
                StatusMessage = "[ERROR] No se pudo modificar el registro de la tarjeta de red. Asegúrate de tener permisos de administrador o actualiza los controladores.";
                return false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"[ERROR] Error al revertir la configuración de red: {ex.Message}";
                return false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"[ERROR] Fallo general al revertir optimización de red: {ex.Message}";
            return false;
        }
    }

    public bool CheckIsApplied()
    {
        try
        {
            var activeNic = GetActiveNetworkInterface();
            if (activeNic == null) return false;

            string? subKeyName = FindAdapterSubKey(activeNic.Id, activeNic.Description);
            if (string.IsNullOrEmpty(subKeyName)) return false;

            using var baseKey = Registry.LocalMachine.OpenSubKey(NetworkClassKeyPath, false);
            using var adapterKey = baseKey?.OpenSubKey(subKeyName, false);
            if (adapterKey == null) return false;

            string? im = adapterKey.GetValue("*InterruptModeration")?.ToString();
            string? eee = adapterKey.GetValue("*EEE")?.ToString();

            return im == "0" && eee == "0";
        }
        catch
        {
            return false;
        }
    }

    private static NetworkInterface? GetActiveNetworkInterface()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();

        // Priorizar interfaz operativa con puerta de enlace válida
        var withGateway = interfaces.FirstOrDefault(nic =>
            nic.OperationalStatus == OperationalStatus.Up &&
            nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
            nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
            nic.GetIPProperties().GatewayAddresses.Any(g => g.Address != null));

        if (withGateway != null) return withGateway;

        // Fallback a cualquier interfaz activa que no sea loopback o tunnel
        return interfaces.FirstOrDefault(nic =>
            nic.OperationalStatus == OperationalStatus.Up &&
            nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
            nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel);
    }

    private static string? FindAdapterSubKey(string nicId, string nicDesc)
    {
        using var baseKey = Registry.LocalMachine.OpenSubKey(NetworkClassKeyPath, false);
        if (baseKey == null) return null;

        string cleanId = nicId.Trim('{', '}');

        foreach (var subKeyName in baseKey.GetSubKeyNames())
        {
            if (subKeyName.Length != 4 || !char.IsDigit(subKeyName[0]))
                continue;

            using var subKey = baseKey.OpenSubKey(subKeyName, false);
            if (subKey == null) continue;

            string? netCfg = subKey.GetValue("NetCfgInstanceId")?.ToString();
            if (!string.IsNullOrEmpty(netCfg) && netCfg.Trim('{', '}').Equals(cleanId, StringComparison.OrdinalIgnoreCase))
            {
                return subKeyName;
            }

            string? driverDesc = subKey.GetValue("DriverDesc")?.ToString();
            if (!string.IsNullOrEmpty(driverDesc) && !string.IsNullOrEmpty(nicDesc) &&
                (driverDesc.Equals(nicDesc, StringComparison.OrdinalIgnoreCase) || nicDesc.Contains(driverDesc, StringComparison.OrdinalIgnoreCase)))
            {
                return subKeyName;
            }
        }

        return null;
    }

    private static void SaveOriginalValues(string subKeyName, string adapterName, RegistryKey adapterKey)
    {
        try
        {
            // Idempotencia: no sobrescribir el backup de fábrica si ya existe
            if (File.Exists(BackupFilePath)) return;

            var dir = Path.GetDirectoryName(BackupFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var backup = new NicBackupData
            {
                SubKey = subKeyName,
                AdapterName = adapterName,
                Values = new Dictionary<string, string?>
                {
                    { "*InterruptModeration", adapterKey.GetValue("*InterruptModeration")?.ToString() },
                    { "*EEE", adapterKey.GetValue("*EEE")?.ToString() },
                    { "*GreenEthernet", adapterKey.GetValue("*GreenEthernet")?.ToString() },
                    { "*ReceiveBuffers", adapterKey.GetValue("*ReceiveBuffers")?.ToString() }
                }
            };

            string json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BackupFilePath, json);
        }
        catch { }
    }

    private static NicBackupData? LoadBackupData()
    {
        try
        {
            if (File.Exists(BackupFilePath))
            {
                string json = File.ReadAllText(BackupFilePath);
                return JsonSerializer.Deserialize<NicBackupData>(json);
            }
        }
        catch { }
        return null;
    }

    private static void DeleteBackupFile()
    {
        try
        {
            if (File.Exists(BackupFilePath))
            {
                File.Delete(BackupFilePath);
            }
        }
        catch { }
    }

    private class NicBackupData
    {
        public string SubKey { get; set; } = string.Empty;
        public string AdapterName { get; set; } = string.Empty;
        public Dictionary<string, string?> Values { get; set; } = new();
    }
}
