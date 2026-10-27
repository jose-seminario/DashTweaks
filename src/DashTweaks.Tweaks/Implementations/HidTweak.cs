using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class HidTweak : ITweak
{
    private const string MouclassPath = @"SYSTEM\CurrentControlSet\Services\mouclass\Parameters";
    private const string KbdclassPath = @"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters";

    // Respaldo en memoria de valores previos
    private object? _originalMouseQueueSize;
    private RegistryValueKind? _originalMouseQueueSizeKind;
    private bool _hadMouseQueueSize;

    private object? _originalKeyboardQueueSize;
    private RegistryValueKind? _originalKeyboardQueueSizeKind;
    private bool _hadKeyboardQueueSize;

    public string Id => "peripherals-hid-queue-size";
    public string Name => "Latencia de Entrada HID (Buffer Ratón y Teclado)";
    public string Description => "Reduce el tamaño de la cola de eventos a 50 paquetes para disminuir input lag. (Aviso: No recomendado para ratones con tasa de sondeo mayor a 4000Hz).";
    public string Category => "Peripherals";
    public RiskLevel Risk => RiskLevel.Moderate;
    public bool RequiresRestart => true;

    public bool Apply()
    {
        try
        {
            // 1. MouseDataQueueSize en mouclass
            using (var mouKey = Registry.LocalMachine.CreateSubKey(MouclassPath, true))
            {
                if (mouKey != null)
                {
                    _originalMouseQueueSize = mouKey.GetValue("MouseDataQueueSize");
                    _hadMouseQueueSize = _originalMouseQueueSize != null;
                    if (_hadMouseQueueSize) _originalMouseQueueSizeKind = mouKey.GetValueKind("MouseDataQueueSize");

                    mouKey.SetValue("MouseDataQueueSize", 50, RegistryValueKind.DWord);
                }
            }

            // 2. KeyboardDataQueueSize en kbdclass
            using (var kbdKey = Registry.LocalMachine.CreateSubKey(KbdclassPath, true))
            {
                if (kbdKey != null)
                {
                    _originalKeyboardQueueSize = kbdKey.GetValue("KeyboardDataQueueSize");
                    _hadKeyboardQueueSize = _originalKeyboardQueueSize != null;
                    if (_hadKeyboardQueueSize) _originalKeyboardQueueSizeKind = kbdKey.GetValueKind("KeyboardDataQueueSize");

                    kbdKey.SetValue("KeyboardDataQueueSize", 50, RegistryValueKind.DWord);
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
            // 1. Revertir mouclass
            using (var mouKey = Registry.LocalMachine.OpenSubKey(MouclassPath, true))
            {
                if (mouKey != null)
                {
                    if (_hadMouseQueueSize && _originalMouseQueueSize != null && _originalMouseQueueSizeKind.HasValue)
                    {
                        mouKey.SetValue("MouseDataQueueSize", _originalMouseQueueSize, _originalMouseQueueSizeKind.Value);
                    }
                    else
                    {
                        mouKey.DeleteValue("MouseDataQueueSize", false);
                    }
                }
            }

            // 2. Revertir kbdclass
            using (var kbdKey = Registry.LocalMachine.OpenSubKey(KbdclassPath, true))
            {
                if (kbdKey != null)
                {
                    if (_hadKeyboardQueueSize && _originalKeyboardQueueSize != null && _originalKeyboardQueueSizeKind.HasValue)
                    {
                        kbdKey.SetValue("KeyboardDataQueueSize", _originalKeyboardQueueSize, _originalKeyboardQueueSizeKind.Value);
                    }
                    else
                    {
                        kbdKey.DeleteValue("KeyboardDataQueueSize", false);
                    }
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
