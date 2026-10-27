using System.Diagnostics;
using System.Text.RegularExpressions;
using DashTweaks.Core.Models;

namespace DashTweaks.Core.Managers;

public class RollbackManager
{
    public RollbackResult RestoreFromBackup(string backupPath)
    {
        var result = new RollbackResult();

        if (string.IsNullOrWhiteSpace(backupPath) || !Directory.Exists(backupPath))
        {
            result.Success = false;
            result.Message = $"La ruta del backup no existe o no es válida: {backupPath}";
            result.Errors.Add("Directorio no encontrado.");
            return result;
        }

        try
        {
            // 1. Restaurar Plan de Energía
            result.PowerPlanRestored = RestorePowerPlan(backupPath, out string powerPlanError);
            if (!string.IsNullOrEmpty(powerPlanError))
            {
                result.Errors.Add(powerPlanError);
            }

            // 2. Restaurar claves del registro a partir de los archivos .reg
            result.RestoredRegistryFilesCount = RestoreRegistryFiles(backupPath, out List<string> regErrors);
            if (regErrors.Any())
            {
                result.Errors.AddRange(regErrors);
            }

            result.Success = result.RestoredRegistryFilesCount > 0 || result.PowerPlanRestored;
            result.Message = result.Success
                ? $"Restauración completada. Archivos de registro restaurados: {result.RestoredRegistryFilesCount}. Plan de energía restaurado: {(result.PowerPlanRestored ? "Sí" : "No")}."
                : "No se pudieron restaurar los elementos del backup seleccionado.";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Fallo general durante la restauración: {ex.Message}";
            result.Errors.Add(ex.ToString());
        }

        return result;
    }

    private bool RestorePowerPlan(string backupPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        string guidFilePath = Path.Combine(backupPath, "power_plan_guid.txt");
        string rawPlanFilePath = Path.Combine(backupPath, "power_plan.txt");

        string? schemeGuid = null;

        if (File.Exists(guidFilePath))
        {
            schemeGuid = File.ReadAllText(guidFilePath).Trim();
        }
        else if (File.Exists(rawPlanFilePath))
        {
            string content = File.ReadAllText(rawPlanFilePath);
            var match = Regex.Match(content, @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
            if (match.Success)
            {
                schemeGuid = match.Groups[1].Value.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(schemeGuid))
        {
            errorMessage = "No se encontró un GUID de plan de energía válido en el backup.";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = $"/setactive {schemeGuid}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var run = ProcessRunner.Run(startInfo, 5000);
            if (run.ExitCode == 0)
            {
                return true;
            }

            errorMessage = $"Error al restaurar plan de energía: {run.StdErr.Trim()}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Excepción al restaurar plan de energía: {ex.Message}";
            return false;
        }
    }

    private int RestoreRegistryFiles(string backupPath, out List<string> errors)
    {
        errors = new List<string>();
        int restoredCount = 0;

        string[] regFiles = Directory.GetFiles(backupPath, "*.reg");
        foreach (var regFile in regFiles)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"import \"{regFile}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var run = ProcessRunner.Run(startInfo, 10000);
                if (run.ExitCode == 0)
                {
                    restoredCount++;
                }
                else
                {
                    errors.Add($"Error al importar '{Path.GetFileName(regFile)}': {run.StdErr.Trim()}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Excepción importando '{Path.GetFileName(regFile)}': {ex.Message}");
            }
        }

        return restoredCount;
    }
}
