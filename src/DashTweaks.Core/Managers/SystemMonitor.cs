using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DashTweaks.Core.Managers;

public class SystemMetrics
{
    public double CpuUsagePercent { get; set; }
    public double RamUsagePercent { get; set; }
    public ulong UsedRamMb { get; set; }
    public ulong TotalRamMb { get; set; }
}

public class SystemMonitor : IDisposable
{
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

    private PerformanceCounter? _cpuCounter;
    private bool _isInitialized;

    public SystemMonitor()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            // Primera lectura para calibrar el contador
            _cpuCounter.NextValue();
            _isInitialized = true;
        }
        catch
        {
            _isInitialized = false;
        }
    }

    public SystemMetrics GetMetrics()
    {
        var metrics = new SystemMetrics();

        // 1. Obtener porcentaje de uso de CPU
        if (_isInitialized && _cpuCounter != null)
        {
            try
            {
                metrics.CpuUsagePercent = Math.Round(_cpuCounter.NextValue(), 1);
            }
            catch
            {
                metrics.CpuUsagePercent = 0;
            }
        }

        // 2. Obtener métricas de RAM con GlobalMemoryStatusEx
        try
        {
            var memStatus = MEMORYSTATUSEX.Create();
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                metrics.RamUsagePercent = memStatus.dwMemoryLoad;
                metrics.TotalRamMb = memStatus.ullTotalPhys / (1024 * 1024);
                ulong availRamMb = memStatus.ullAvailPhys / (1024 * 1024);
                metrics.UsedRamMb = metrics.TotalRamMb >= availRamMb ? metrics.TotalRamMb - availRamMb : 0;
            }
        }
        catch
        {
            // Retornar métricas vacías en caso de fallo de lectura
        }

        return metrics;
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _cpuCounter = null;
    }
}
