using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class SvcHostSplitTweak : ITweak
{
    private const string ControlPath = @"SYSTEM\CurrentControlSet\Control";
    private const string ValueName = "SvcHostSplitThresholdInKB";

    private object? _originalValue;
    private RegistryValueKind? _originalValueKind;
    private bool _hadValue;
    private bool _applied;

    public string Id => "system-svchost-split-threshold";
    public string Name => "Separación de Procesos SvcHost (Memoria)";
    public string Description => "Ajusta el umbral de SvcHost a 64 MB (0x04000000) para aislar servicios en procesos independientes, mejorando estabilidad y reduciendo contención de hilos en PCs modernos con 8GB+ RAM.";
    public string Category => "Memory";
    public RiskLevel Risk => RiskLevel.Safe;
    public bool RequiresRestart => true;

    public bool Apply()
    {
        if (_applied) return true;

        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(ControlPath, true);
            if (key == null) return false;

            _originalValue = key.GetValue(ValueName);
            _hadValue = _originalValue != null;
            if (_hadValue) _originalValueKind = key.GetValueKind(ValueName);

            // 0x04000000 = 67108864 (64 MB en KB)
            key.SetValue(ValueName, 0x04000000, RegistryValueKind.DWord);
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
            using var key = Registry.LocalMachine.OpenSubKey(ControlPath, true);
            if (key == null) return false;

            if (_hadValue && _originalValue != null && _originalValueKind.HasValue)
            {
                key.SetValue(ValueName, _originalValue, _originalValueKind.Value);
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }

            _applied = false;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
