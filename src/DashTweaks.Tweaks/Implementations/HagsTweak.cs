using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class HagsTweak : ITweak
{
    private const string GraphicsDriversPath = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    // Respaldo en memoria de HwSchMode
    private object? _originalHwSchMode;
    private RegistryValueKind? _originalHwSchModeKind;
    private bool _hadHwSchMode;
    private bool _applied;

    public bool IsApplied => _applied;

    public string Id => "gpu-hardware-accelerated-scheduling";
    public string Name => "Programación de GPU Acelerada por Hardware (HAGS)";
    public string Description => "Permite que la tarjeta gráfica administre su memoria de video directamente, logrando mayor fluidez y tiempos de cuadro más estables.";
    public string Category => "GPU";
    public RiskLevel Risk => RiskLevel.Safe;
    public bool RequiresRestart => true;

    public HagsTweak()
    {
        _applied = CheckIsApplied();
    }

    public bool Apply()
    {
        if (_applied) return true;

        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(GraphicsDriversPath, true);
            if (key == null) return false;

            _originalHwSchMode = key.GetValue("HwSchMode");
            _hadHwSchMode = _originalHwSchMode != null;
            if (_hadHwSchMode) _originalHwSchModeKind = key.GetValueKind("HwSchMode");

            // 2 = Activado
            key.SetValue("HwSchMode", 2, RegistryValueKind.DWord);
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
            using var key = Registry.LocalMachine.OpenSubKey(GraphicsDriversPath, true);
            if (key == null) return false;

            if (_hadHwSchMode && _originalHwSchMode != null && _originalHwSchModeKind.HasValue)
            {
                key.SetValue("HwSchMode", _originalHwSchMode, _originalHwSchModeKind.Value);
            }
            else
            {
                // Si antes no existía, el valor estándar de desactivado suele ser 1
                key.SetValue("HwSchMode", 1, RegistryValueKind.DWord);
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
            using var key = Registry.LocalMachine.OpenSubKey(GraphicsDriversPath, false);
            return key?.GetValue("HwSchMode") is int val && val == 2;
        }
        catch
        {
            return false;
        }
    }
}
