using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class GameDvrTweak : ITweak
{
    public string Id => "gaming-disable-gamedvr";
    public string Name => "Desactivar GameDVR y Captura en Segundo Plano";
    public string Description => "Desactiva la grabación en segundo plano de GameDVR para liberar memoria, GPU y reducir latencia de entrada.";
    public string Category => "Services";
    public RiskLevel Risk => RiskLevel.Safe;
    public bool RequiresRestart => false;

    public bool Apply()
    {
        return SetGameDvrState(enabled: false);
    }

    public bool Revert()
    {
        return SetGameDvrState(enabled: true);
    }

    private static bool SetGameDvrState(bool enabled)
    {
        int value = enabled ? 1 : 0;
        bool hkcuSuccess = false;
        bool hklmSuccess = false;

        try
        {
            // HKCU\System\GameConfigStore -> GameDVR_Enabled
            using var key = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore", true);
            if (key != null)
            {
                key.SetValue("GameDVR_Enabled", value, RegistryValueKind.DWord);
                hkcuSuccess = true;
            }
        }
        catch
        {
            hkcuSuccess = false;
        }

        try
        {
            // HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR -> AllowGameDVR
            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", true);
            if (key != null)
            {
                key.SetValue("AllowGameDVR", value, RegistryValueKind.DWord);
                hklmSuccess = true;
            }
        }
        catch
        {
            hklmSuccess = false;
        }

        return hkcuSuccess || hklmSuccess;
    }
}
