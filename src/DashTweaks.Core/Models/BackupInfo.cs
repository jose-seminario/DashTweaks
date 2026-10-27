namespace DashTweaks.Core.Models;

public class BackupInfo
{
    public string Id { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool RestorePointCreated { get; set; }
    public bool HasPowerPlan { get; set; }
    public List<string> RegistryFiles { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}

public class BackupResult
{
    public bool Success { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public bool RestorePointCreated { get; set; }
    public string RestorePointMessage { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class RollbackResult
{
    public bool Success { get; set; }
    public int RestoredRegistryFilesCount { get; set; }
    public bool PowerPlanRestored { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}
