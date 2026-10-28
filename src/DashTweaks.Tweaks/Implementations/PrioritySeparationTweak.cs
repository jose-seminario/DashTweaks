using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class PrioritySeparationTweak : ITweak
{
    private const string PriorityControlKeyPath = @"SYSTEM\CurrentControlSet\Control\PriorityControl";
    private const int OptimizedValue = 26; // 0x1A: Quanta corto, variable, máxima prioridad a foreground
    private const int DefaultValue = 2;

    public string Id => "system-win32-priority-separation-26";
    public string Name => "Prioridad Cuántica de Procesos (Win32PrioritySeparation 26)";
    public string Description => "Concentra la atención del procesador en el juego que estás jugando, evitando que tareas de fondo causen bajones de fotogramas.";
    public string Category => "CPU / System";
    public RiskLevel Risk => RiskLevel.Moderate;
    public bool RequiresRestart => false;

    public bool IsApplied { get; private set; }

    public PrioritySeparationTweak()
    {
        IsApplied = CheckIsApplied();
    }

    public bool Apply()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(PriorityControlKeyPath, true);
            if (key != null)
            {
                key.SetValue("Win32PrioritySeparation", OptimizedValue, RegistryValueKind.DWord);
                IsApplied = true;
                return true;
            }
            return false;
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
            using var key = Registry.LocalMachine.OpenSubKey(PriorityControlKeyPath, true);
            if (key != null)
            {
                key.SetValue("Win32PrioritySeparation", DefaultValue, RegistryValueKind.DWord);
                IsApplied = false;
                return true;
            }
            return false;
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
            using var key = Registry.LocalMachine.OpenSubKey(PriorityControlKeyPath, false);
            object? val = key?.GetValue("Win32PrioritySeparation");
            return val is int intVal && intVal == OptimizedValue;
        }
        catch
        {
            return false;
        }
    }
}
