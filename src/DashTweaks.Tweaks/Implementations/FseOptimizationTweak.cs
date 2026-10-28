using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class FseOptimizationTweak : ITweak
{
    private const string GameConfigStorePath = @"System\GameConfigStore";
    private const string GamesTaskPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

    private object? _originalFseMode;
    private bool _hadFseMode;

    private object? _originalHonorFse;
    private bool _hadHonorFse;

    private object? _originalSfioPriority;
    private bool _hadSfioPriority;

    private bool _applied;
    public bool IsApplied => _applied;

    public string Id => "gpu-fullscreen-exclusive-sfio";
    public string Name => "Optimizaciones de Pantalla Completa y SFIO";
    public string Description => "Elimina el retraso visual de la pantalla completa y acelera la carga de texturas dándole prioridad máxima a los archivos del juego en tu disco.";
    public string Category => "GPU";
    public RiskLevel Risk => RiskLevel.Safe;
    public bool RequiresRestart => true;

    public FseOptimizationTweak()
    {
        _applied = CheckIsApplied();
    }

    public bool Apply()
    {
        if (_applied) return true;

        try
        {
            // 1. HKCU\System\GameConfigStore
            using (var configKey = Registry.CurrentUser.CreateSubKey(GameConfigStorePath, true))
            {
                if (configKey != null)
                {
                    _originalFseMode = configKey.GetValue("GameDVR_FSEBehaviorMode");
                    _hadFseMode = _originalFseMode != null;

                    _originalHonorFse = configKey.GetValue("GameDVR_HonorUserFSEBehaviorMode");
                    _hadHonorFse = _originalHonorFse != null;

                    configKey.SetValue("GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);
                    configKey.SetValue("GameDVR_HonorUserFSEBehaviorMode", 1, RegistryValueKind.DWord);
                }
            }

            // 2. HKLM Tasks\Games -> SFIO Priority
            using (var taskKey = Registry.LocalMachine.CreateSubKey(GamesTaskPath, true))
            {
                if (taskKey != null)
                {
                    _originalSfioPriority = taskKey.GetValue("SFIO Priority");
                    _hadSfioPriority = _originalSfioPriority != null;

                    taskKey.SetValue("SFIO Priority", "High", RegistryValueKind.String);
                }
            }

            _applied = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Revert()
    {
        if (!_applied) return true;

        try
        {
            using (var configKey = Registry.CurrentUser.OpenSubKey(GameConfigStorePath, true))
            {
                if (configKey != null)
                {
                    if (_hadFseMode && _originalFseMode != null)
                        configKey.SetValue("GameDVR_FSEBehaviorMode", _originalFseMode, RegistryValueKind.DWord);
                    else
                        configKey.SetValue("GameDVR_FSEBehaviorMode", 0, RegistryValueKind.DWord);

                    if (_hadHonorFse && _originalHonorFse != null)
                        configKey.SetValue("GameDVR_HonorUserFSEBehaviorMode", _originalHonorFse, RegistryValueKind.DWord);
                    else
                        configKey.DeleteValue("GameDVR_HonorUserFSEBehaviorMode", false);
                }
            }

            using (var taskKey = Registry.LocalMachine.OpenSubKey(GamesTaskPath, true))
            {
                if (taskKey != null)
                {
                    if (_hadSfioPriority && _originalSfioPriority != null)
                        taskKey.SetValue("SFIO Priority", _originalSfioPriority, RegistryValueKind.String);
                    else
                        taskKey.SetValue("SFIO Priority", "Normal", RegistryValueKind.String);
                }
            }

            _applied = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool CheckIsApplied()
    {
        try
        {
            using var configKey = Registry.CurrentUser.OpenSubKey(GameConfigStorePath, false);
            using var taskKey = Registry.LocalMachine.OpenSubKey(GamesTaskPath, false);

            bool fseOk = configKey?.GetValue("GameDVR_FSEBehaviorMode") is int fse && fse == 2;
            bool sfioOk = string.Equals(taskKey?.GetValue("SFIO Priority")?.ToString(), "High", StringComparison.OrdinalIgnoreCase);

            return fseOk && sfioOk;
        }
        catch
        {
            return false;
        }
    }
}
