using System.Security;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class HidPriorityTweak : ITweak
{
    private const string AffinityPolicySubKey = @"Device Parameters\Interrupt Management\Affinity Policy";

    private const string KeyboardClassGuid = "{4d36e96b-e325-11ce-bfc1-08002be10318}";
    private const string MouseClassGuid = "{4d36e96f-e325-11ce-bfc1-08002be10318}";

    private static readonly string BackupFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DashTweaks",
        "hid_priority_backup.json"
    );

    private static readonly string DesktopRescueScriptPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "RESCATE_HID.bat"
    );

    public string Id => "input-hid-mouse-keyboard-priority";
    public string Name => "Prioridad IRQ para Ratón y Teclado (Affinity Policy High)";
    public string Description => "Configura la prioridad de interrupción en 'Alta' (DevicePriority = 3) y política segura (DevicePolicy = 0) para tus periféricos HID, garantizando que el hardware procese movimientos y clics con mínima latencia.";
    public string Category => "Input / Latency";
    public RiskLevel Risk => RiskLevel.Aggressive;
    public bool RequiresRestart => true;

    public string StatusMessage { get; private set; } = string.Empty;
    public bool IsApplied { get; private set; }

    public HidPriorityTweak()
    {
        IsApplied = CheckIsApplied();
    }

    public bool Apply()
    {
        try
        {
            var targets = DiscoverInputDevices();
            if (!targets.Any())
            {
                StatusMessage = "[ERROR] No se detectaron periféricos HID compatibles de ratón o teclado en el registro.";
                return false;
            }

            // 1. Guardar backup en JSON de los valores originales
            SaveBackup(targets);

            // 2. SALVAGUARDA CRÍTICA: Generar script de rescate RESCATE_HID.bat en el Escritorio
            GenerateRescueScript(targets);

            // 3. Aplicar DevicePriority = 3 y DevicePolicy = 0 (Seguro sin máscaras binarias)
            int modifiedCount = 0;
            using var enumBaseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum", true);
            if (enumBaseKey == null)
            {
                StatusMessage = "[ERROR] No se pudo acceder a la rama Enum de HKLM.";
                return false;
            }

            foreach (var target in targets)
            {
                try
                {
                    using var devKey = enumBaseKey.OpenSubKey(target.DevicePath, true);
                    if (devKey == null) continue;

                    using var affinityKey = devKey.CreateSubKey(AffinityPolicySubKey, true);
                    if (affinityKey != null)
                    {
                        // DevicePriority = 3 (Alta)
                        affinityKey.SetValue("DevicePriority", 3, RegistryValueKind.DWord);
                        // DevicePolicy = 0 (IrqPolicyMachineDefault) para máxima compatibilidad y estabilidad
                        affinityKey.SetValue("DevicePolicy", 0, RegistryValueKind.DWord);
                        modifiedCount++;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException)
                {
                    StatusMessage = "[ERROR] Acceso denegado al configurar prioridad IRQ en periféricos. Se requieren permisos de Administrador.";
                    return false;
                }
            }

            if (modifiedCount > 0)
            {
                IsApplied = true;
                StatusMessage = $"[HID] Prioridad IRQ Alta (3) configurada en {modifiedCount} periféricos de ratón y teclado.";
                return true;
            }

            StatusMessage = "[ERROR] No se pudo modificar la prioridad de interrupción en los periféricos detectados.";
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"[ERROR] Fallo general al optimizar prioridad HID: {ex.Message}";
            return false;
        }
    }

    public bool Revert()
    {
        try
        {
            var backup = LoadBackup();
            var targets = backup?.Devices ?? DiscoverInputDevices();

            using var enumBaseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum", true);
            if (enumBaseKey == null)
            {
                StatusMessage = "[ERROR] No se pudo acceder a la rama Enum de HKLM.";
                return false;
            }

            foreach (var dev in targets)
            {
                try
                {
                    using var devKey = enumBaseKey.OpenSubKey(dev.DevicePath, true);
                    if (devKey == null) continue;

                    // Si originalmente no tenía clave, borrar la subclave Affinity Policy creada
                    if (!dev.HadAffinityPolicy)
                    {
                        using var intMgmtKey = devKey.OpenSubKey(@"Device Parameters\Interrupt Management", true);
                        intMgmtKey?.DeleteSubKeyTree("Affinity Policy", false);
                    }
                    else
                    {
                        using var affinityKey = devKey.OpenSubKey(AffinityPolicySubKey, true);
                        if (affinityKey != null)
                        {
                            if (dev.OriginalPriority.HasValue)
                            {
                                affinityKey.SetValue("DevicePriority", dev.OriginalPriority.Value, RegistryValueKind.DWord);
                            }
                            else
                            {
                                affinityKey.DeleteValue("DevicePriority", false);
                            }

                            if (dev.OriginalPolicy.HasValue)
                            {
                                affinityKey.SetValue("DevicePolicy", dev.OriginalPolicy.Value, RegistryValueKind.DWord);
                            }
                            else
                            {
                                affinityKey.DeleteValue("DevicePolicy", false);
                            }
                        }
                    }
                }
                catch { }
            }

            DeleteBackup();
            CleanRescueScript();

            IsApplied = false;
            StatusMessage = "[HID] Prioridad IRQ restaurada a los valores de fábrica.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"[ERROR] Fallo al revertir prioridad HID: {ex.Message}";
            return false;
        }
    }

    public bool CheckIsApplied()
    {
        try
        {
            var targets = DiscoverInputDevices();
            if (!targets.Any()) return false;

            using var enumBaseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum", false);
            if (enumBaseKey == null) return false;

            foreach (var dev in targets)
            {
                using var devKey = enumBaseKey.OpenSubKey(dev.DevicePath, false);
                using var affinityKey = devKey?.OpenSubKey(AffinityPolicySubKey, false);
                object? prio = affinityKey?.GetValue("DevicePriority");

                if (prio is not int intPrio || intPrio != 3)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<HidDeviceItem> DiscoverInputDevices()
    {
        var result = new List<HidDeviceItem>();
        string[] rootBuses = { @"SYSTEM\CurrentControlSet\Enum\HID", @"SYSTEM\CurrentControlSet\Enum\USB" };

        foreach (var busPath in rootBuses)
        {
            try
            {
                using var busKey = Registry.LocalMachine.OpenSubKey(busPath, false);
                if (busKey == null) continue;

                string busName = Path.GetFileName(busPath);

                foreach (string deviceId in busKey.GetSubKeyNames())
                {
                    using var devSubKey = busKey.OpenSubKey(deviceId, false);
                    if (devSubKey == null) continue;

                    foreach (string instanceId in devSubKey.GetSubKeyNames())
                    {
                        using var instKey = devSubKey.OpenSubKey(instanceId, false);
                        if (instKey == null) continue;

                        string? classGuid = instKey.GetValue("ClassGUID")?.ToString();
                        string? className = instKey.GetValue("Class")?.ToString();
                        string? deviceDesc = instKey.GetValue("DeviceDesc")?.ToString();
                        string? friendlyName = instKey.GetValue("FriendlyName")?.ToString();

                        bool isMouse = string.Equals(classGuid, MouseClassGuid, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(className, "Mouse", StringComparison.OrdinalIgnoreCase) ||
                                       (deviceDesc != null && deviceDesc.Contains("Mouse", StringComparison.OrdinalIgnoreCase));

                        bool isKeyboard = string.Equals(classGuid, KeyboardClassGuid, StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(className, "Keyboard", StringComparison.OrdinalIgnoreCase) ||
                                          (deviceDesc != null && deviceDesc.Contains("Keyboard", StringComparison.OrdinalIgnoreCase));

                        if (isMouse || isKeyboard)
                        {
                            string relPath = $@"{busName}\{deviceId}\{instanceId}";
                            string name = friendlyName ?? deviceDesc ?? deviceId;
                            if (name.Contains(";")) name = name.Split(';').Last();

                            int? origPrio = null;
                            int? origPolicy = null;
                            bool hadAffinity = false;

                            using var affinityKey = instKey.OpenSubKey(AffinityPolicySubKey, false);
                            if (affinityKey != null)
                            {
                                hadAffinity = true;
                                if (affinityKey.GetValue("DevicePriority") is int p) origPrio = p;
                                if (affinityKey.GetValue("DevicePolicy") is int pol) origPolicy = pol;
                            }

                            result.Add(new HidDeviceItem
                            {
                                DevicePath = relPath,
                                DeviceName = name.Trim(),
                                DeviceType = isMouse ? "Ratón" : "Teclado",
                                HadAffinityPolicy = hadAffinity,
                                OriginalPriority = origPrio,
                                OriginalPolicy = origPolicy
                            });
                        }
                    }
                }
            }
            catch { }
        }

        return result;
    }

    private static void GenerateRescueScript(List<HidDeviceItem> targets)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("title DashTweaks - Rescate de Raton y Teclado (HID)");
            sb.AppendLine("echo ==============================================================");
            sb.AppendLine("echo  [DASHTWEAKS] SCRIPT DE RESCATE DE EMERGENCIA PARA RATON Y TECLADO");
            sb.AppendLine("echo ==============================================================");
            sb.AppendLine("echo Si tu raton o teclado dejaron de responder despues de reiniciar,");
            sb.AppendLine("echo este script restablecera las politicas de afinidad por defecto.");
            sb.AppendLine("echo.");
            sb.AppendLine("echo Restaurando valores seguros...");
            sb.AppendLine("echo.");

            foreach (var dev in targets)
            {
                string keyPath = $@"HKLM\SYSTEM\CurrentControlSet\Enum\{dev.DevicePath}\Device Parameters\Interrupt Management\Affinity Policy";
                // Restaurar DevicePriority a 0 y eliminar la clave si fue creada
                sb.AppendLine($@"reg add ""{keyPath}"" /v DevicePriority /t REG_DWORD /d 0 /f >nul 2>&1");
                sb.AppendLine($@"reg add ""{keyPath}"" /v DevicePolicy /t REG_DWORD /d 0 /f >nul 2>&1");
                sb.AppendLine($@"reg delete ""{keyPath}"" /f >nul 2>&1");
            }

            sb.AppendLine("echo.");
            sb.AppendLine("echo ==============================================================");
            sb.AppendLine("echo  Restauracion completada exitosamente.");
            sb.AppendLine("echo  Por favor reinicia tu equipo para recuperar el control.");
            sb.AppendLine("echo ==============================================================");
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

    private static void SaveBackup(List<HidDeviceItem> targets)
    {
        try
        {
            var dir = Path.GetDirectoryName(BackupFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var backup = new HidBackupContainer { Devices = targets };
            string json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BackupFilePath, json);
        }
        catch { }
    }

    private static HidBackupContainer? LoadBackup()
    {
        try
        {
            if (File.Exists(BackupFilePath))
            {
                string json = File.ReadAllText(BackupFilePath);
                return JsonSerializer.Deserialize<HidBackupContainer>(json);
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

    public class HidDeviceItem
    {
        public string DevicePath { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public bool HadAffinityPolicy { get; set; }
        public int? OriginalPriority { get; set; }
        public int? OriginalPolicy { get; set; }
    }

    private class HidBackupContainer
    {
        public List<HidDeviceItem> Devices { get; set; } = new();
    }
}
