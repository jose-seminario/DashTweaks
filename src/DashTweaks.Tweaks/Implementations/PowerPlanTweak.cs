using System.Diagnostics;
using DashTweaks.Core.Managers;

namespace DashTweaks.Tweaks.Implementations;

public class PowerPlanTweak : ITweak
{
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    public string Id => "power-plan-high-performance";
    public string Name => "Plan de Energía: Alto Rendimiento";
    public string Description => "Activa el esquema de Alto Rendimiento para evitar caídas de frecuencia de CPU durante sesiones gaming.";
    public string Category => "Power";
    public RiskLevel Risk => RiskLevel.Safe;
    public bool RequiresRestart => false;

    public bool Apply()
    {
        return SetPowerPlan(HighPerformanceGuid);
    }

    public bool Revert()
    {
        return SetPowerPlan(BalancedGuid);
    }

    private static bool SetPowerPlan(string planGuid)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = $"/setactive {planGuid}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var run = ProcessRunner.Run(psi, 5000);
            return run.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
