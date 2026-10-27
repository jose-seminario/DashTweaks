using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DashTweaks.Tweaks.Implementations;

public class ProcessSuspensionTweak : ITweak
{
    // P/Invoke a ntdll.dll para suspender y reanudar procesos
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern uint NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern uint NtResumeProcess(IntPtr processHandle);

    // P/Invoke a kernel32.dll para apertura y cierre seguro de handles
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    // Permisos de proceso requeridos: PROCESS_SUSPEND_RESUME (0x0800)
    private const uint ProcessSuspendResumeAccess = 0x0800;

    // Procesos objetivo a suspender
    private static readonly string[] TargetProcessNames = { "explorer", "chrome", "msedge", "steam", "discord" };

    // Lista en memoria de IDs de procesos suspendidos
    private readonly List<int> _suspendedProcessIds = new();

    public string Id => "system-suspend-background-processes";
    public string Name => "Suspensión de Procesos Secundarios";
    public string Description => "Suspende temporalmente explorer.exe, navegadores y launchers para liberar hilos de CPU y memoria sin cerrarlos. Revierte al terminar la partida.";
    public string Category => "Processes";
    public RiskLevel Risk => RiskLevel.Moderate;
    public bool RequiresRestart => false;

    public int SuspendedCount => _suspendedProcessIds.Count;

    public bool Apply()
    {
        _suspendedProcessIds.Clear();

        foreach (var processName in TargetProcessNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var proc in processes)
                {
                    IntPtr handle = IntPtr.Zero;
                    try
                    {
                        handle = OpenProcess(ProcessSuspendResumeAccess, false, proc.Id);
                        if (handle != IntPtr.Zero)
                        {
                            uint status = NtSuspendProcess(handle);
                            if (status == 0) // STATUS_SUCCESS = 0
                            {
                                _suspendedProcessIds.Add(proc.Id);
                            }
                        }
                    }
                    catch
                    {
                        // Continuar con los demás procesos si uno falla
                    }
                    finally
                    {
                        // Crítico: Cerrar el handle inmediatamente para evitar memory leaks
                        if (handle != IntPtr.Zero)
                        {
                            CloseHandle(handle);
                        }
                    }
                }
            }
            catch
            {
                // Ignorar errores al enumerar un grupo de procesos
            }
        }

        return _suspendedProcessIds.Count > 0;
    }

    public bool Revert()
    {
        bool anyResumed = false;

        foreach (int pid in _suspendedProcessIds)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(ProcessSuspendResumeAccess, false, pid);
                if (handle != IntPtr.Zero)
                {
                    uint status = NtResumeProcess(handle);
                    if (status == 0)
                    {
                        anyResumed = true;
                    }
                }
            }
            catch
            {
                // Ignorar errores puntuales al intentar reanudar
            }
            finally
            {
                // Crítico: Cerrar el handle inmediatamente en el Revert
                if (handle != IntPtr.Zero)
                {
                    CloseHandle(handle);
                }
            }
        }

        _suspendedProcessIds.Clear();
        return anyResumed || true;
    }
}
