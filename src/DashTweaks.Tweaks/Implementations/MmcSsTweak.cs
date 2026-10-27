using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class MmcSsTweak : ITweak
{
    private const string SubKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

    // Respaldo en memoria de valores previos para reversibilidad exacta
    private object? _originalGpuPriority;
    private RegistryValueKind? _originalGpuPriorityKind;
    private bool _hadGpuPriority;

    private object? _originalPriority;
    private RegistryValueKind? _originalPriorityKind;
    private bool _hadPriority;

    private object? _originalSchedulingCategory;
    private RegistryValueKind? _originalSchedulingCategoryKind;
    private bool _hadSchedulingCategory;
    private bool _applied;

    public string Id => "system-mmcss-gaming-priority";
    public string Name => "Prioridad Multimedia (MMCSS) para Juegos";
    public string Description => "Ajusta la programación de tareas multimedia para asignar máxima prioridad de GPU y CPU a videojuegos frente a servicios secundarios.";
    public string Category => "GPU / Memory";
    public RiskLevel Risk => RiskLevel.Safe;
    public bool RequiresRestart => false;

    public bool Apply()
    {
        if (_applied) return true;

        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(SubKeyPath, true);
            if (key == null) return false;

            // 1. Respaldar en memoria los valores previos antes de sobrescribir
            _originalGpuPriority = key.GetValue("GPU Priority");
            _hadGpuPriority = _originalGpuPriority != null;
            if (_hadGpuPriority) _originalGpuPriorityKind = key.GetValueKind("GPU Priority");

            _originalPriority = key.GetValue("Priority");
            _hadPriority = _originalPriority != null;
            if (_hadPriority) _originalPriorityKind = key.GetValueKind("Priority");

            _originalSchedulingCategory = key.GetValue("Scheduling Category");
            _hadSchedulingCategory = _originalSchedulingCategory != null;
            if (_hadSchedulingCategory) _originalSchedulingCategoryKind = key.GetValueKind("Scheduling Category");

            // 2. Aplicar los valores de optimización solicitados
            key.SetValue("GPU Priority", 8, RegistryValueKind.DWord);
            key.SetValue("Priority", 6, RegistryValueKind.DWord);
            key.SetValue("Scheduling Category", "High", RegistryValueKind.String);

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
            using var key = Registry.LocalMachine.OpenSubKey(SubKeyPath, true);
            if (key == null) return false;

            // Restaurar 'GPU Priority'
            if (_hadGpuPriority && _originalGpuPriority != null && _originalGpuPriorityKind.HasValue)
            {
                key.SetValue("GPU Priority", _originalGpuPriority, _originalGpuPriorityKind.Value);
            }
            else
            {
                key.DeleteValue("GPU Priority", false);
            }

            // Restaurar 'Priority'
            if (_hadPriority && _originalPriority != null && _originalPriorityKind.HasValue)
            {
                key.SetValue("Priority", _originalPriority, _originalPriorityKind.Value);
            }
            else
            {
                key.DeleteValue("Priority", false);
            }

            // Restaurar 'Scheduling Category'
            if (_hadSchedulingCategory && _originalSchedulingCategory != null && _originalSchedulingCategoryKind.HasValue)
            {
                key.SetValue("Scheduling Category", _originalSchedulingCategory, _originalSchedulingCategoryKind.Value);
            }
            else
            {
                key.DeleteValue("Scheduling Category", false);
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
