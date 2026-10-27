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

    public string Id => "gpu-fullscreen-exclusive-sfio";
    public string Name => "Optimizaciones de Pantalla Completa y SFIO";
    public string Description => "Fuerza el comportamiento de pantalla completa exclusiva real (FSE Behavior=2) y asigna prioridad Alta a operaciones I/O de juegos (SFIO Priority='High').";
    public string Category => "GPU";
    public RiskLevel Risk => RiskLevel.Safe;
    public bool RequiresRestart => true;

    public bool Apply()
    {
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

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Revert()
    {
        try
        {
            using (var configKey = Registry.CurrentUser.OpenSubKey(GameConfigStorePath, true))
            {
                if (configKey != null)
                {
                    if (_hadFseMode && _originalFseMode != null)
                        configKey.SetValue("GameDVR_FSEBehaviorMode", _originalFseMode, RegistryValueKind.DWord);
                    else
                        configKey.DeleteValue("GameDVR_FSEBehaviorMode", false);

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
                        taskKey.DeleteValue("SFIO Priority", false);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
