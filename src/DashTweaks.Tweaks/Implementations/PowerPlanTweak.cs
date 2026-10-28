using System.Diagnostics;
using DashTweaks.Core.Managers;

namespace DashTweaks.Tweaks.Implementations;

public class PowerPlanTweak : ITweak
{
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    public string Id => "power-plan-high-performance";
    public string Name => "Plan de Energía: Alto Rendimiento";
    public string Description => "Mantiene tu procesador a máxima potencia en todo momento. Evita caídas repentinas de FPS cuando el juego exige más rendimiento.";
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

    public bool CheckIsApplied()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/getactivescheme",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var run = ProcessRunner.Run(psi, 5000);
            if (run.ExitCode == 0 && !string.IsNullOrWhiteSpace(run.StdOut))
            {
                return run.StdOut.Contains(HighPerformanceGuid, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        catch
        {
            return false;
        }
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
