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

    // P/Invoke a ntdll.dll para invocar el comando de purga de memoria en espera
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetSystemInformation(
        int systemInformationClass,
        IntPtr systemInformation,
        int systemInformationLength
    );

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
}
