using System.Runtime.InteropServices;
using System.Windows;

namespace DashTweaks.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetTimerResolution(uint desiredResolution, bool setResolution, out uint currentResolution);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Salvaguarda Crítica: Asegurar que la resolución del timer del sistema se libere al salir
        AppDomain.CurrentDomain.ProcessExit += (s, ev) =>
        {
            try
            {
                NtSetTimerResolution(5000, false, out uint _);
            }
            catch
            {
                // Ignorar excepciones al cerrar el proceso
            }
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            NtSetTimerResolution(5000, false, out uint _);
        }
        catch
        {
            // Ignorar excepciones al salir
        }

        base.OnExit(e);
    }
}
