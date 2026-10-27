using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class MouseAccelerationTweak : ITweak
{
    private const string MousePath = @"Control Panel\Mouse";

    private object? _originalSpeed;
    private object? _originalThreshold1;
    private object? _originalThreshold2;
    private bool _hadValues;

    public string Id => "peripherals-disable-mouse-acceleration";
    public string Name => "Desactivar Aceleración del Ratón (Precisión 1:1)";
    public string Description => "Desactiva la curva de aceleración angular de Windows (MouseSpeed=0, Thresholds=0) para asegurar puntería consistente 1:1. (Aviso: Requiere cerrar sesión o reiniciar para aplicarse en la mayoría de juegos).";
    public string Category => "Peripherals";
    public RiskLevel Risk => RiskLevel.Safe;
    public bool RequiresRestart => false;

    public bool Apply()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(MousePath, true);
            if (key == null) return false;

            _originalSpeed = key.GetValue("MouseSpeed");
            _originalThreshold1 = key.GetValue("MouseThreshold1");
            _originalThreshold2 = key.GetValue("MouseThreshold2");
            _hadValues = _originalSpeed != null && _originalThreshold1 != null && _originalThreshold2 != null;

            key.SetValue("MouseSpeed", "0", RegistryValueKind.String);
            key.SetValue("MouseThreshold1", "0", RegistryValueKind.String);
            key.SetValue("MouseThreshold2", "0", RegistryValueKind.String);

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
            using var key = Registry.CurrentUser.OpenSubKey(MousePath, true);
            if (key == null) return false;

            if (_hadValues)
            {
                key.SetValue("MouseSpeed", _originalSpeed ?? "1", RegistryValueKind.String);
                key.SetValue("MouseThreshold1", _originalThreshold1 ?? "6", RegistryValueKind.String);
                key.SetValue("MouseThreshold2", _originalThreshold2 ?? "10", RegistryValueKind.String);
            }
            else
            {
                // Valores estándar de Windows con aceleración activa
                key.SetValue("MouseSpeed", "1", RegistryValueKind.String);
                key.SetValue("MouseThreshold1", "6", RegistryValueKind.String);
                key.SetValue("MouseThreshold2", "10", RegistryValueKind.String);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
