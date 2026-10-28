using System.Runtime.InteropServices;

namespace DashTweaks.Tweaks.Implementations;

public class TimerResolutionTweak : ITweak
{
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetTimerResolution(uint desiredResolution, [MarshalAs(UnmanagedType.U1)] bool setResolution, out uint currentResolution);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtQueryTimerResolution(out uint maximumResolution, out uint minimumResolution, out uint currentResolution);

    // 0.5 ms en unidades de 100ns = 5,000
    private const uint TargetResolution100ns = 5000;

    public string Id => "system-timer-resolution-05ms";
    public string Name => "Resolución del Temporizador a 0.5ms (Input Lag Mínimo)";
    public string Description => "Acelera el reloj interno del sistema a 0.5 ms para que los fotogramas del juego y tus comandos se sincronicen con la máxima velocidad posible.";
    public string Category => "Power";
    public RiskLevel Risk => RiskLevel.Aggressive;
    public bool RequiresRestart => false;

    public bool IsActive { get; private set; }

    public bool CheckIsApplied() => IsActive;

    public bool Apply()
    {
        try
        {
            int status = NtSetTimerResolution(TargetResolution100ns, true, out uint currentResolution);
            if (status == 0) // STATUS_SUCCESS
            {
                IsActive = true;
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
            if (IsActive)
            {
                // SetResolution = false libera la resolución personalizada solicitada
                NtSetTimerResolution(TargetResolution100ns, false, out uint _);
                IsActive = false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
