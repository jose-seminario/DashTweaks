using System.Runtime.InteropServices;

namespace DashTweaks.Tweaks.Implementations;

public class MemoryPurgeTweak : ITweak
{
    // P/Invoke a kernel32.dll para consultar estado de memoria global
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public static MEMORYSTATUSEX Create()
        {
            return new MEMORYSTATUSEX
            {
                dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
            };
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr TokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength
    );

    // P/Invoke a ntdll.dll para invocar el comando de purga de memoria en espera
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetSystemInformation(
        int systemInformationClass,
        IntPtr systemInformation,
        int systemInformationLength
    );

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const string SeProfileSingleProcessName = "SeProfileSingleProcessPrivilege";

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID Luid;
        public uint Attributes;
    }

    // SystemMemoryListInformation = 80, FlushStandbyListCommand = 4
    private const int SystemMemoryListInformation = 80;
    private const int FlushStandbyListCommand = 4;

    public string Id => "memory-purge-standbylist";
    public string Name => "Purga de Memoria Standby (Solo Bajo Presión)";
    public string Description => "Vacia la lista de memoria en espera (Standby Cache) únicamente si el uso de RAM supera el 80%, evitando micro-tirones innecesarios.";
    public string Category => "Memory";
    public RiskLevel Risk => RiskLevel.Moderate;
    public bool RequiresRestart => false;

    // Mensaje descriptivo de la última ejecución para informar a la UI
    public string StatusMessage { get; private set; } = string.Empty;

    public bool Apply()
    {
        try
        {
            // 1. Validar uso actual de RAM con GlobalMemoryStatusEx
            var memStatus = MEMORYSTATUSEX.Create();
            if (!GlobalMemoryStatusEx(ref memStatus))
            {
                StatusMessage = "No se pudo consultar el estado actual de la memoria RAM.";
                return false;
            }

            // 2. CRÍTICO (Seguridad): Solo purgar si el uso de RAM es >= 80%
            if (memStatus.dwMemoryLoad < 80)
            {
                StatusMessage = $"Memoria suficiente ({memStatus.dwMemoryLoad}% en uso), purga omitida para evitar stutter.";
                return false;
            }

            // Salvaguarda: Habilitar SeProfileSingleProcessPrivilege en el token del proceso
            if (!TryEnableProfilePrivilege(out string privilegeError))
            {
                StatusMessage = privilegeError;
                return false;
            }

            // 3. Purgar Standby List invocando NtSetSystemInformation
            int command = FlushStandbyListCommand;
            GCHandle handle = GCHandle.Alloc(command, GCHandleType.Pinned);

            try
            {
                int ntStatus = NtSetSystemInformation(
                    SystemMemoryListInformation,
                    handle.AddrOfPinnedObject(),
                    Marshal.SizeOf(command)
                );

                if (ntStatus == 0) // STATUS_SUCCESS = 0
                {
                    StatusMessage = $"Standby List purgada con éxito. Presión de RAM reducida (Uso previo: {memStatus.dwMemoryLoad}%).";
                    return true;
                }
                else
                {
                    StatusMessage = $"Fallo al purgar Standby List. Código NTSTATUS: 0x{ntStatus:X8}. Requiere privilegios elevados.";
                    return false;
                }
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Excepción al ejecutar la purga de memoria: {ex.Message}";
            return false;
        }
    }

    public bool Revert()
    {
        // La memoria en espera no puede "des-purgarse" manualmente ya que Windows la reconstruye según la demanda
        StatusMessage = "La lista Standby se regenera dinámicamente según la actividad de Windows.";
        return true;
    }

    private static bool TryEnableProfilePrivilege(out string errorMessage)
    {
        errorMessage = string.Empty;
        IntPtr tokenHandle = IntPtr.Zero;

        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out tokenHandle))
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"[ERROR] No se pudo abrir el token del proceso (Error Win32: {err}).";
                return false;
            }

            if (!LookupPrivilegeValue(null, SeProfileSingleProcessName, out LUID luid))
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"[ERROR] No se pudo localizar el privilegio {SeProfileSingleProcessName} (Error Win32: {err}).";
                return false;
            }

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SE_PRIVILEGE_ENABLED
            };

            if (!AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"[ERROR] No se pudo obtener el privilegio {SeProfileSingleProcessName} (Error Win32: {err}). La purga de memoria está deshabilitada.";
                return false;
            }

            int lastError = Marshal.GetLastWin32Error();
            if (lastError == 1300) // ERROR_NOT_ALL_ASSIGNED
            {
                errorMessage = $"[ERROR] No se pudo obtener el privilegio {SeProfileSingleProcessName}. La purga de memoria está deshabilitada.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"[ERROR] No se pudo obtener el privilegio {SeProfileSingleProcessName}. La purga de memoria está deshabilitada: {ex.Message}";
            return false;
        }
        finally
        {
            if (tokenHandle != IntPtr.Zero)
            {
                CloseHandle(tokenHandle);
            }
        }
    }
}
