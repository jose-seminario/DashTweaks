using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DashTweaks.Tweaks.Implementations;

public class EcoQosTweak : ITweak
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        int processInformationClass,
        ref PROCESS_POWER_THROTTLING_STATE processInformation,
        uint processInformationSize
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int ProcessPowerThrottling = 4;
    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
    private const uint PROCESS_SET_INFORMATION = 0x0200;

    // Procesos críticos del sistema operativo que NUNCA deben ralentizarse
    private static readonly HashSet<string> ProtectedSystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost", "csrss", "wininit", "winlogon", "services", "registry", 
        "system", "smss", "lsass", "fontdrvhost", "dwm", "spoolsv", "audiodg"
    };

    private readonly List<int> _throttledPids = new();

    public string Id => "processes-ecoqos-background-throttling";
    public string Name => "EcoQoS en Procesos de Fondo (Prioridad de CPU para Juegos)";
    public string Description => "Aplica estrangulamiento de eficiencia energética (EcoQoS) a aplicaciones secundarias sin ventana activa, liberando núcleos de alto rendimiento de la CPU para el juego.";
    public string Category => "Processes";
    public RiskLevel Risk => RiskLevel.Moderate;
    public bool RequiresRestart => false;

    public int ThrottledCount => _throttledPids.Count;

    public bool Apply()
    {
        _throttledPids.Clear();
        int currentPid = Environment.ProcessId;

        var processes = Process.GetProcesses();
        try
        {
            foreach (var proc in processes)
            {
                try
                {
                    // Salvaguarda 1: Proteger proceso actual de DashTweaks
                    if (proc.Id == currentPid) continue;

                    // Salvaguarda 2: Proteger procesos críticos del sistema operativo
                    if (ProtectedSystemProcesses.Contains(proc.ProcessName)) continue;

                    // Solo aplicar a procesos en segundo plano sin ventana principal activa
                    if (proc.MainWindowHandle != IntPtr.Zero) continue;

                    IntPtr handle = IntPtr.Zero;
                    try
                    {
                        handle = OpenProcess(PROCESS_SET_INFORMATION, false, proc.Id);
                        if (handle != IntPtr.Zero)
                        {
                            var state = new PROCESS_POWER_THROTTLING_STATE
                            {
                                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                                StateMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED
                            };

                            if (SetProcessInformation(handle, ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf(state)))
                            {
                                _throttledPids.Add(proc.Id);
                            }
                        }
                    }
                    finally
                    {
                        if (handle != IntPtr.Zero)
                        {
                            CloseHandle(handle);
                        }
                    }
                }
                catch
                {
                    // Continuar si algún proceso tiene acceso restringido
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

        return _throttledPids.Count > 0;
    }

    public bool Revert()
    {
        foreach (int pid in _throttledPids)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(PROCESS_SET_INFORMATION, false, pid);
                if (handle != IntPtr.Zero)
                {
                    var state = new PROCESS_POWER_THROTTLING_STATE
                    {
                        Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                        ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                        StateMask = 0 // Desactivar limitación de velocidad
                    };

                    SetProcessInformation(handle, ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf(state));
                }
            }
            catch
            {
                // Ignorar procesos que hayan cerrado
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    CloseHandle(handle);
                }
            }
        }

        _throttledPids.Clear();
        return true;
    }
}
