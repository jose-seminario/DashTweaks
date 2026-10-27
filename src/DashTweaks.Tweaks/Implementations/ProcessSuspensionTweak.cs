using System.Diagnostics;
using System.Runtime.InteropServices;
using DashTweaks.Core.Managers;

namespace DashTweaks.Tweaks.Implementations;

public class ProcessSuspensionTweak : ITweak
{
    // P/Invoke a ntdll.dll para suspender y reanudar procesos
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtResumeProcess(IntPtr processHandle);

    // P/Invoke a kernel32.dll para apertura y cierre seguro de handles
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    // Permisos de proceso requeridos: PROCESS_SUSPEND_RESUME (0x0800)
    private const uint ProcessSuspendResumeAccess = 0x0800;

    // Procesos candidatos a suspender en modo juego
    private static readonly string[] TargetProcessNames = { "explorer", "chrome", "msedge", "steam", "discord" };

    // Lista en memoria de IDs de procesos que fueron efectivamente suspendidos
    private readonly List<int> _suspendedProcessIds = new();

    // Procesos que fueron omitidos por encontrarse en la Lista Blanca
    public List<string> SkippedProcesses { get; } = new();

    private readonly SettingsManager _settingsManager = new();

    public string Id => "system-suspend-background-processes";
    public string Name => "Suspensión de Procesos Secundarios";
    public string Description => "Suspende temporalmente explorer.exe y aplicaciones no esenciales para liberar hilos de CPU sin cerrarlas. Respeta la Lista Blanca de exclusiones.";
    public string Category => "Processes";
    public RiskLevel Risk => RiskLevel.Moderate;
    public bool RequiresRestart => false;

    public int SuspendedCount => _suspendedProcessIds.Count;

    public bool Apply()
    {
        _suspendedProcessIds.Clear();
        SkippedProcesses.Clear();

        // 1. Cargar la Lista Blanca persistente desde la configuración
        var settings = _settingsManager.LoadSettings();
        var whitelist = (settings.GameModeWhitelist ?? new List<string>())
            .Select(s => NormalizeProcessName(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2. Iterar procesos candidatos
        foreach (var processName in TargetProcessNames)
        {
            string normalizedTarget = NormalizeProcessName(processName);

            // Si está en la Lista Blanca, se omite por completo
            if (whitelist.Contains(normalizedTarget))
            {
                SkippedProcesses.Add(processName);
                continue;
            }

            try
            {
                var processes = Process.GetProcessesByName(processName);
                try
                {
                    foreach (var proc in processes)
                    {
                        IntPtr handle = IntPtr.Zero;
                        try
                        {
                            handle = OpenProcess(ProcessSuspendResumeAccess, false, proc.Id);
                            if (handle != IntPtr.Zero)
                            {
                                int status = NtSuspendProcess(handle);
                                if (status == 0) // STATUS_SUCCESS = 0
                                {
                                    _suspendedProcessIds.Add(proc.Id);
                                }
                            }
                        }
                        catch
                        {
                            // Continuar con los demás si uno falla
                        }
                        finally
                        {
                            // Crítico: Cerrar el handle inmediatamente para prevenir memory leaks
                            if (handle != IntPtr.Zero)
                            {
                                CloseHandle(handle);
                            }
                        }
                    }
                }
                finally
                {
                    foreach (var proc in processes)
                    {
                        try
                        {
                            proc.Dispose();
                        }
                        catch { }
                    }
                }
            }
            catch
            {
                // Ignorar errores al buscar un grupo de procesos
            }
        }

        return _suspendedProcessIds.Count > 0 || SkippedProcesses.Count > 0;
    }

    public bool Revert()
    {
        bool anyResumed = false;

        // Regla de seguridad: SOLO reanudar los procesos que fueron efectivamente suspendidos
        foreach (int pid in _suspendedProcessIds)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(ProcessSuspendResumeAccess, false, pid);
                if (handle != IntPtr.Zero)
                {
                    int status = NtResumeProcess(handle);
                    if (status == 0)
                    {
                        anyResumed = true;
                    }
                }
            }
            catch
            {
                // Ignorar errores puntuales si el proceso fue terminado externamente
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    CloseHandle(handle);
                }
            }
        }

        _suspendedProcessIds.Clear();
        SkippedProcesses.Clear();
        return anyResumed || true;
    }

    private static string NormalizeProcessName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        string clean = name.Trim();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(0, clean.Length - 4);
        }
        return clean.ToLowerInvariant();
    }
}
