using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashTweaks.Core.Models;

namespace DashTweaks.Core.Managers;

public class BackupManager
{
    private static readonly string BaseBackupDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DashTweaks",
        "Backups"
    );

    // Claves del registro que suelen ser modificadas por optimizadores gaming
    private static readonly Dictionary<string, string> KeyRegistryBranches = new()
    {
        { "multimedia_systemprofile.reg", @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" },
        { "hkcu_desktop.reg", @"HKCU\Control Panel\Desktop" },
        { "priority_control.reg", @"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl" },
        { "tcpip_parameters.reg", @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" }
    };

    public BackupResult CreateCheckpoint()
    {
        var result = new BackupResult();
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupFolder = Path.Combine(BaseBackupDirectory, timestamp);

        try
        {
            Directory.CreateDirectory(backupFolder);
            result.BackupPath = backupFolder;

            // 1. Crear punto de restauración del sistema (Checkpoint-Computer)
            result.RestorePointCreated = TryCreateSystemRestorePoint(out string restoreMessage);
            result.RestorePointMessage = restoreMessage;

            // 2. Guardar el plan de energía activo actual
            SaveCurrentPowerPlan(backupFolder);

            // 3. Exportar ramas del registro a archivos .reg
            List<string> exportedRegFiles = ExportRegistryBranches(backupFolder);

            // 4. Guardar metadatos en formato JSON para trazabilidad
            var backupInfo = new BackupInfo
            {
                Id = timestamp,
                FolderPath = backupFolder,
                CreatedAt = DateTime.Now,
                RestorePointCreated = result.RestorePointCreated,
                HasPowerPlan = File.Exists(Path.Combine(backupFolder, "power_plan.txt")),
                RegistryFiles = exportedRegFiles,
                Notes = result.RestorePointCreated
                    ? "Checkpoint completo generado exitosamente."
                    : $"Backup completado con advertencia: {restoreMessage}"
            };

            string metadataPath = Path.Combine(backupFolder, "backup_info.json");
            try
            {
                File.WriteAllText(metadataPath, JsonSerializer.Serialize(backupInfo, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (IOException) { /* archivo bloqueado: el backup .reg sigue siendo válido */ }
            catch (UnauthorizedAccessException) { }

            result.Success = true;
            result.Message = result.RestorePointCreated
                ? $"Backup creado correctamente en: {backupFolder}"
                : $"Backup parcial creado en: {backupFolder} (Advertencia: Punto de restauración no creado: {restoreMessage})";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error al crear el backup: {ex.Message}";
        }

        return result;
    }

    public List<BackupInfo> GetAvailableBackups()
    {
        var backups = new List<BackupInfo>();

        if (!Directory.Exists(BaseBackupDirectory))
        {
            return backups;
        }

        var directories = Directory.GetDirectories(BaseBackupDirectory)
                                   .OrderByDescending(d => Directory.GetCreationTime(d));

        foreach (var dir in directories)
        {
            string metadataPath = Path.Combine(dir, "backup_info.json");
            if (File.Exists(metadataPath))
            {
                try
                {
                    string json = File.ReadAllText(metadataPath);
                    var info = JsonSerializer.Deserialize<BackupInfo>(json);
                    if (info != null)
                    {
                        backups.Add(info);
                        continue;
                    }
                }
                catch
                {
                    // Si el archivo JSON no se puede parsear, inferir datos de la carpeta
                }
            }

            var dirInfo = new DirectoryInfo(dir);
            backups.Add(new BackupInfo
            {
                Id = dirInfo.Name,
                FolderPath = dir,
                CreatedAt = dirInfo.CreationTime,
                RestorePointCreated = false,
                HasPowerPlan = File.Exists(Path.Combine(dir, "power_plan.txt")),
                RegistryFiles = Directory.GetFiles(dir, "*.reg").Select(Path.GetFileName).Where(f => f != null).Cast<string>().ToList(),
                Notes = "Backup detectado en disco."
            });
        }

        return backups;
    }

    private bool TryCreateSystemRestorePoint(out string message)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'DashTweaks_Backup' -RestorePointType 'MODIFY_SETTINGS'\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var run = ProcessRunner.Run(startInfo, 30000); // Límite de 30 segundos
            string output = run.StdOut;
            string error = run.StdErr;

            if (run.TimedOut)
            {
                message = "Tiempo de espera agotado al crear el punto de restauración.";
                return false;
            }

            if (run.ExitCode == 0)
            {
                message = "Punto de restauración del sistema creado con éxito.";
                return true;
            }

            message = !string.IsNullOrWhiteSpace(error)
                ? error.Trim()
                : (!string.IsNullOrWhiteSpace(output) ? output.Trim() : $"Código de salida: {run.ExitCode}");
            return false;
        }
        catch (Exception ex)
        {
            message = $"Excepción al crear punto de restauración: {ex.Message}";
            return false;
        }
    }

    private void SaveCurrentPowerPlan(string targetFolder)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/getactivescheme",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var run = ProcessRunner.Run(startInfo, 5000);
            {
                string output = run.StdOut;

                string filePath = Path.Combine(targetFolder, "power_plan.txt");
                File.WriteAllText(filePath, output.Trim(), Encoding.UTF8);

                // Extraer y almacenar únicamente el GUID para facilitar el rollback
                var match = Regex.Match(output, @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
                if (match.Success)
                {
                    File.WriteAllText(Path.Combine(targetFolder, "power_plan_guid.txt"), match.Groups[1].Value.Trim(), Encoding.UTF8);
                }
            }
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(Path.Combine(targetFolder, "power_plan_error.txt"), ex.ToString(), Encoding.UTF8); }
            catch { /* archivo bloqueado (antivirus/OneDrive): se ignora */ }
        }
    }

    private List<string> ExportRegistryBranches(string targetFolder)
    {
        var exportedFiles = new List<string>();

        foreach (var (fileName, regPath) in KeyRegistryBranches)
        {
            try
            {
                string targetFilePath = Path.Combine(targetFolder, fileName);
                var startInfo = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"export \"{regPath}\" \"{targetFilePath}\" /y",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0 && File.Exists(targetFilePath))
                    {
                        exportedFiles.Add(fileName);
                    }
                }
            }
            catch
            {
                // Si una clave no existe en esta edición de Windows, continuamos con las demás
            }
        }

        return exportedFiles;
    }
}
