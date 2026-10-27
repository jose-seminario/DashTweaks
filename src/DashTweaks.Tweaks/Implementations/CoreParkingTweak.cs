using System.Diagnostics;
using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class CoreParkingTweak : ITweak
{
    private const string CoreParkingKeyPath = @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583";

    public string Id => "cpu-core-parking-disable";
    public string Name => "Desactivación de Core Parking (100% Núcleos Activos)";
    public string Description => "Mantiene todos los núcleos físicos y lógicos de la CPU al 100% activos sin estacionarse (CPMINCORES = 100), eliminando micro-tirones por reactivación de núcleos.";
    public string Category => "CPU / Power";
    public RiskLevel Risk => RiskLevel.Aggressive;
    public bool RequiresRestart => false;

    public bool IsApplied { get; private set; }

    public CoreParkingTweak()
    {
        IsApplied = CheckIsApplied();
    }

    public bool Apply()
    {
        try
        {
            // 1. Desbloquear la visibilidad de la opción de Core Parking en el registro
            using (var key = Registry.LocalMachine.OpenSubKey(CoreParkingKeyPath, true))
            {
                key?.SetValue("Attributes", 0, RegistryValueKind.DWord);
            }

            // 2. Establecer CPMINCORES a 100% en corriente alterna (AC)
            RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100");

            // 3. Reactivar el esquema actual para forzar la aplicación inmediata
            RunPowerCfg("/setactive SCHEME_CURRENT");

            IsApplied = true;
            return true;
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
            // 1. Ocultar nuevamente la opción en el registro
            using (var key = Registry.LocalMachine.OpenSubKey(CoreParkingKeyPath, true))
            {
                key?.SetValue("Attributes", 1, RegistryValueKind.DWord);
            }

            // 2. Devolver CPMINCORES al valor por defecto seguro de Windows (5%)
            RunPowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 5");

            // 3. Reactivar el esquema actual
            RunPowerCfg("/setactive SCHEME_CURRENT");

            IsApplied = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool CheckIsApplied()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(CoreParkingKeyPath, false);
            object? val = key?.GetValue("Attributes");
            return val is int intVal && intVal == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void RunPowerCfg(string arguments)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            });
            proc?.WaitForExit(3000);
        }
        catch { }
    }
}
