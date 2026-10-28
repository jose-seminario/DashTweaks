using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class GameDvrTweak : ITweak
{
    private const string HkcuPath = @"System\GameConfigStore";
    private const string HkcuValue = "GameDVR_Enabled";
    private const string PolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\GameDVR";
    private const string PolicyValue = "AllowGameDVR";

    // Estado original capturado una sola vez (idempotencia)
    private bool _applied;
    private object? _origHkcu;
    private RegistryValueKind _origHkcuKind = RegistryValueKind.DWord;
    private object? _origPolicy;
    private RegistryValueKind _origPolicyKind = RegistryValueKind.DWord;
    private bool _policyKeyExisted;

    public string Id => "gaming-disable-gamedvr";
    public string Name => "Desactivar GameDVR y Captura en Segundo Plano";
    public string Description => "Apaga la grabadora de fondo de Windows. Libera memoria, GPU y hace que el ratón responda más rápido.";
    public string Category => "Services";
    public RiskLevel Risk => RiskLevel.Safe;
    public bool RequiresRestart => false;

    public bool Apply()
    {
        if (_applied) return true;

        bool hkcuSuccess = false;
        bool hklmSuccess = false;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(HkcuPath, true);
            if (key != null)
            {
                _origHkcu = key.GetValue(HkcuValue, null);
                if (_origHkcu != null) _origHkcuKind = key.GetValueKind(HkcuValue);
                key.SetValue(HkcuValue, 0, RegistryValueKind.DWord);
                hkcuSuccess = true;
            }
        }
        catch { hkcuSuccess = false; }

        try
        {
            using (var existing = Registry.LocalMachine.OpenSubKey(PolicyPath, false))
            {
                _policyKeyExisted = existing != null;
            }

            using var key = Registry.LocalMachine.CreateSubKey(PolicyPath, true);
            if (key != null)
            {
                _origPolicy = key.GetValue(PolicyValue, null);
                if (_origPolicy != null) _origPolicyKind = key.GetValueKind(PolicyValue);
                key.SetValue(PolicyValue, 0, RegistryValueKind.DWord);
                hklmSuccess = true;
            }
        }
        catch { hklmSuccess = false; }

        _applied = hkcuSuccess || hklmSuccess;
        return _applied;
    }

    public bool Revert()
    {
        if (!_applied) return true;

        bool ok = true;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(HkcuPath, true);
            if (key != null)
            {
                if (_origHkcu != null) key.SetValue(HkcuValue, _origHkcu, _origHkcuKind);
                else key.DeleteValue(HkcuValue, false);
            }
        }
        catch { ok = false; }

        try
        {
            if (_origPolicy != null)
            {
                using var key = Registry.LocalMachine.OpenSubKey(PolicyPath, true);
                key?.SetValue(PolicyValue, _origPolicy, _origPolicyKind);
            }
            else
            {
                using (var key = Registry.LocalMachine.OpenSubKey(PolicyPath, true))
                {
                    key?.DeleteValue(PolicyValue, false);
                }
                if (!_policyKeyExisted)
                {
                    // La clave no existía antes de DashTweaks: se elimina solo si quedó vacía
                    using var parent = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows", true);
                    using var child = parent?.OpenSubKey("GameDVR", false);
                    if (child != null && child.ValueCount == 0 && child.SubKeyCount == 0)
                    {
                        child.Dispose();
                        parent?.DeleteSubKey("GameDVR", false);
                    }
                }
            }
        }
        catch { ok = false; }

        if (ok) _applied = false;
        return ok;
    }
    public bool CheckIsApplied()
    {
        try
        {
            using var hkcu = Registry.CurrentUser.OpenSubKey(HkcuPath, false);
            if (hkcu?.GetValue(HkcuValue) is int hkcuVal && hkcuVal == 0) return true;

            using var policy = Registry.LocalMachine.OpenSubKey(PolicyPath, false);
            if (policy?.GetValue(PolicyValue) is int policyVal && policyVal == 0) return true;

            return false;
        }
        catch
        {
            return false;
        }
    }
}
