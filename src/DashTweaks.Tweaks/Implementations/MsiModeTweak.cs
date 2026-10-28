using System.Security;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class MsiModeTweak : ITweak
{
    private const string PciEnumPath = @"SYSTEM\CurrentControlSet\Enum\PCI";
    private const string MsiSubKeyRelative = @"Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";

    // GUIDs de clase de dispositivos objetivo
    private const string DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string NetClassGuid = "{4d36e972-e325-11ce-bfc1-08002be10318}";

    private static readonly string BackupFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DashTweaks",
        "msi_mode_backup.json"
    );

    private static readonly string DesktopRescueScriptPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "RESCATE_MSI.bat"
    );

    public string Id => "system-msi-mode-gpu-network";
    public string Name => "Modo MSI en GPU y Red (Message Signaled Interrupts)";
    public string Description => "Acelera la comunicación entre tu tarjeta gráfica, tarjeta de red y procesador. Suaviza la tasa de cuadros y reduce la latencia de entrada.";
    public string Category => "GPU / Network";
    public RiskLevel Risk => RiskLevel.Aggressive;
    public bool RequiresRestart => true;

    public string StatusMessage { get; private set; } = string.Empty;
    public bool IsApplied { get; private set; }
    public string RescueScriptPath => DesktopRescueScriptPath;

    public MsiModeTweak()
    {
        IsApplied = CheckIsApplied();
    }

    public bool Apply()
    {
        try
        {
            var targets = DiscoverTargetDevices();
            if (!targets.Any())
            {
                StatusMessage = "[ERROR] No se detectaron dispositivos PCI compatibles de GPU o Red en el sistema.";
                return false;
            }

            // 1. Guardar backup en JSON de los valores originales
            SaveBackup(targets);

            // 2. SALVAGUARDA EXTREMA: Generar script de rescate en el Escritorio
            GenerateRescueScript(targets);

            // 3. Modificar el registro para activar MSISupported = 1
            int modifiedCount = 0;
            using var pciBaseKey = Registry.LocalMachine.OpenSubKey(PciEnumPath, true);
            if (pciBaseKey == null)
            {
                StatusMessage = "[ERROR] No se pudo acceder a la clave de registro de dispositivos PCI.";
                return false;
            }

            foreach (var target in targets)
            {
                try
                {
                    using var devKey = pciBaseKey.OpenSubKey(target.DevicePath, true);
                    if (devKey == null) continue;

                    using var msiKey = devKey.CreateSubKey(MsiSubKeyRelative, true);
                    if (msiKey != null)
                    {
                        msiKey.SetValue("MSISupported", 1, RegistryValueKind.DWord);
                        modifiedCount++;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException)
                {
                    StatusMessage = "[ERROR] Acceso denegado al modificar el modo MSI. Se requieren permisos elevados de Administrador.";
                    return false;
                }
            }

            if (modifiedCount > 0)
            {
                IsApplied = true;
                var names = targets.Select(t => $"{t.DeviceType}: {t.CleanName}");
                StatusMessage = $"[MSI MODE] Modo MSI activado en {modifiedCount} dispositivo(s) ({string.Join(" | ", names)}).";
                return true;
            }

            StatusMessage = "[ERROR] No se pudo aplicar el modo MSI en los dispositivos seleccionados.";
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"[ERROR] Fallo general al activar el modo MSI: {ex.Message}";
            return false;
        }
    }

    public bool Revert()
    {
        try
        {
            var backup = LoadBackup();
            var targets = backup?.Devices ?? DiscoverTargetDevices();

            using var pciBaseKey = Registry.LocalMachine.OpenSubKey(PciEnumPath, true);
            if (pciBaseKey == null)
            {
                StatusMessage = "[ERROR] No se pudo acceder a la clave de registro de dispositivos PCI.";
                return false;
            }

            foreach (var dev in targets)
            {
                try
                {
                    using var devKey = pciBaseKey.OpenSubKey(dev.DevicePath, true);
                    if (devKey == null) continue;

                    using var msiKey = devKey.OpenSubKey(MsiSubKeyRelative, true);
                    if (msiKey != null)
                    {
                        if (dev.OriginalMsiSupported.HasValue)
                        {
                            msiKey.SetValue("MSISupported", dev.OriginalMsiSupported.Value, RegistryValueKind.DWord);
                        }
                        else
                        {
                            // Si no existía originalmente, eliminar el valor para dejar el registro en su estado original idéntico
                            msiKey.DeleteValue("MSISupported", false);
                        }
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException)
                {
                    StatusMessage = "[ERROR] Acceso denegado al revertir el modo MSI.";
                    return false;
                }
            }

            DeleteBackup();
            CleanRescueScript();

            IsApplied = false;
            StatusMessage = "[MSI MODE] Modo MSI revertido a los valores de fábrica.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"[ERROR] Fallo al revertir el modo MSI: {ex.Message}";
            return false;
        }
    }

    public bool CheckIsApplied()
    {
        try
        {
            var targets = DiscoverTargetDevices();
            if (!targets.Any()) return false;

            using var pciBaseKey = Registry.LocalMachine.OpenSubKey(PciEnumPath, false);
            if (pciBaseKey == null) return false;

            foreach (var dev in targets)
            {
                using var devKey = pciBaseKey.OpenSubKey(dev.DevicePath, false);
                using var msiKey = devKey?.OpenSubKey(MsiSubKeyRelative, false);
                object? val = msiKey?.GetValue("MSISupported");

                if (val is int intVal && intVal == 1)
                {
                    return true;
                }
                if (val != null && int.TryParse(val.ToString(), out int parsed) && parsed == 1)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static List<MsiDeviceItem> DiscoverTargetDevices()
    {
        var result = new List<MsiDeviceItem>();

        try
        {
            using var pciBaseKey = Registry.LocalMachine.OpenSubKey(PciEnumPath, false);
            if (pciBaseKey == null) return result;

            foreach (string hardwareId in pciBaseKey.GetSubKeyNames())
            {
                using var hwKey = pciBaseKey.OpenSubKey(hardwareId, false);
                if (hwKey == null) continue;

                foreach (string instanceId in hwKey.GetSubKeyNames())
                {
                    using var instKey = hwKey.OpenSubKey(instanceId, false);
                    if (instKey == null) continue;

                    string? classGuid = instKey.GetValue("ClassGUID")?.ToString();
                    string? className = instKey.GetValue("Class")?.ToString();
                    string? deviceDesc = instKey.GetValue("DeviceDesc")?.ToString();
                    string? friendlyName = instKey.GetValue("FriendlyName")?.ToString();

                    string type = string.Empty;
                    if (string.Equals(classGuid, DisplayClassGuid, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(className, "Display", StringComparison.OrdinalIgnoreCase))
                    {
                        type = "GPU";
                    }
                    else if (string.Equals(classGuid, NetClassGuid, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(className, "Net", StringComparison.OrdinalIgnoreCase))
                    {
                        type = "Red";
                    }

                    if (!string.IsNullOrEmpty(type))
                    {
                        string name = friendlyName ?? deviceDesc ?? hardwareId;
                        if (name.Contains(";"))
                        {
                            name = name.Split(';').Last();
                        }

                        // Consultar valor original de MSISupported
                        int? originalVal = null;
                        using var msiKey = instKey.OpenSubKey(MsiSubKeyRelative, false);
                        if (msiKey?.GetValue("MSISupported") is int v)
                        {
                            originalVal = v;
                        }

                        result.Add(new MsiDeviceItem
                        {
                            DevicePath = $@"{hardwareId}\{instanceId}",
                            CleanName = name.Trim(),
                            DeviceType = type,
                            OriginalMsiSupported = originalVal
                        });
                    }
                }
            }
        }
        catch { }

        return result;
    }

    private static void GenerateRescueScript(List<MsiDeviceItem> targets)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("echo Revirtiendo MSI Mode para evitar fallos de arranque...");
            sb.AppendLine("echo.");

            foreach (var dev in targets)
            {
                string keyPath = $@"HKLM\SYSTEM\CurrentControlSet\Enum\PCI\{dev.DevicePath}\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";
                sb.AppendLine($@"reg add ""{keyPath}"" /v MSISupported /t REG_DWORD /d 0 /f");
            }

            sb.AppendLine("echo.");
            sb.AppendLine("echo Hecho. Reinicia tu PC.");
            sb.AppendLine("pause");

            File.WriteAllText(DesktopRescueScriptPath, sb.ToString(), Encoding.ASCII);
        }
        catch { }
    }

    private static void CleanRescueScript()
    {
        try
        {
            if (File.Exists(DesktopRescueScriptPath))
            {
                File.Delete(DesktopRescueScriptPath);
            }
        }
        catch { }
    }

    private static void SaveBackup(List<MsiDeviceItem> targets)
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

            var backup = new MsiBackupContainer { Devices = targets };
            string json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BackupFilePath, json);
        }
        catch { }
    }

    private static MsiBackupContainer? LoadBackup()
    {
        try
        {
            if (File.Exists(BackupFilePath))
            {
                string json = File.ReadAllText(BackupFilePath);
                return JsonSerializer.Deserialize<MsiBackupContainer>(json);
            }
        }
        catch { }
        return null;
    }

    private static void DeleteBackup()
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

    public class MsiDeviceItem
    {
        public string DevicePath { get; set; } = string.Empty;
        public string CleanName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public int? OriginalMsiSupported { get; set; }
    }

    private class MsiBackupContainer
    {
        public List<MsiDeviceItem> Devices { get; set; } = new();
    }
}
