using System.ServiceProcess;
using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class ServiceStateInfo
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCurrentlyRunning { get; set; }
    public int CurrentStartType { get; set; } // 2: Auto, 3: Manual, 4: Disabled
    public bool IsSelected { get; set; } = true;
    public string StatusText => IsCurrentlyRunning ? "En ejecución" : "Detenido";
    public string StartTypeText => CurrentStartType switch
    {
        2 => "Automático",
        3 => "Manual",
        4 => "Deshabilitado",
        _ => $"Tipo {CurrentStartType}"
    };
}

public class ServiceManagerTweak
{
    private const string ServicesRegistryRoot = @"SYSTEM\CurrentControlSet\Services";

    // Respaldo en memoria de estados originales: ServiceName -> (WasRunning, OriginalStartType)
    private readonly Dictionary<string, (bool WasRunning, int OriginalStartType)> _originalStates = new(StringComparer.OrdinalIgnoreCase);

    // Lista fija de servicios optimizables para gaming
    public static readonly Dictionary<string, (string DisplayName, string Description)> TargetServices = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DiagTrack", ("Telemetría de Windows (DiagTrack)", "Servicio de seguimiento de diagnósticos y experiencias de usuario conectadas.") },
        { "SysMain", ("SysMain / Superfetch", "Mantiene y mejora el rendimiento del sistema cargando datos predictivos en RAM (puede causar uso alto de disco).") },
        { "WSearch", ("Windows Search", "Indexador de archivos y búsquedas de contenido en segundo plano.") }
    };

    /// <summary>
    /// Consulta el estado actual en vivo de los servicios monitorizados.
    /// </summary>
    public List<ServiceStateInfo> GetServiceStates()
    {
        var states = new List<ServiceStateInfo>();

        foreach (var (svcName, (dispName, desc)) in TargetServices)
        {
            bool isRunning = false;
            int startType = 3; // Por defecto Manual si no se puede leer

            // 1. Consultar estado en vivo con ServiceController
            try
            {
                using var sc = new ServiceController(svcName);
                isRunning = sc.Status == ServiceControllerStatus.Running;
            }
            catch
            {
                isRunning = false;
            }

            // 2. Consultar tipo de inicio en el registro
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"{ServicesRegistryRoot}\{svcName}");
                if (key != null)
                {
                    var val = key.GetValue("Start");
                    if (val is int intVal)
                    {
                        startType = intVal;
                    }
                }
            }
            catch
            {
                // Dejar valor predeterminado si falla lectura de registro
            }

            // Guardar estado original si aún no ha sido respaldado en la sesión
            if (!_originalStates.ContainsKey(svcName))
            {
                _originalStates[svcName] = (isRunning, startType);
            }

            states.Add(new ServiceStateInfo
            {
                ServiceName = svcName,
                DisplayName = dispName,
                Description = desc,
                IsCurrentlyRunning = isRunning,
                CurrentStartType = startType,
                IsSelected = true
            });
        }

        return states;
    }

    /// <summary>
    /// Detiene y deshabilita un servicio (Start = 4).
    /// </summary>
    public bool DisableService(string serviceName, out string message)
    {
        if (!TargetServices.ContainsKey(serviceName))
        {
            message = $"El servicio '{serviceName}' no está en la lista de gestión segura.";
            return false;
        }

        try
        {
            // 1. Guardar estado original si aún no existe
            EnsureOriginalStateSaved(serviceName);

            // 2. Detener el servicio si está en ejecución
            try
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
                {
                    if (sc.CanStop)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    }
                }
            }
            catch (Exception ex)
            {
                // Continuar hacia el cambio de inicio en el registro aunque la detención inmediata falle
                message = $"Aviso: No se pudo detener inmediatamente el servicio '{serviceName}': {ex.Message}";
            }

            // 3. Establecer tipo de inicio a 4 (Disabled) en el registro
            using (var key = Registry.LocalMachine.OpenSubKey($@"{ServicesRegistryRoot}\{serviceName}", true))
            {
                if (key == null)
                {
                    message = $"No se encontró la clave de registro del servicio '{serviceName}'.";
                    return false;
                }
                key.SetValue("Start", 4, RegistryValueKind.DWord);
            }

            message = $"Servicio '{serviceName}' detenido y deshabilitado (Start=4).";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Error al deshabilitar '{serviceName}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Restaura el tipo de inicio y el estado original del servicio.
    /// </summary>
    public bool RestoreService(string serviceName, out string message)
    {
        if (!_originalStates.TryGetValue(serviceName, out var original))
        {
            message = $"No se encontró registro del estado previo para el servicio '{serviceName}'.";
            return false;
        }

        try
        {
            // 1. Restaurar tipo de inicio original en el registro
            using (var key = Registry.LocalMachine.OpenSubKey($@"{ServicesRegistryRoot}\{serviceName}", true))
            {
                if (key != null)
                {
                    key.SetValue("Start", original.OriginalStartType, RegistryValueKind.DWord);
                }
            }

            // 2. Si estaba originalmente en ejecución, iniciarlo
            if (original.WasRunning)
            {
                try
                {
                    using var sc = new ServiceController(serviceName);
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                    }
                }
                catch (Exception ex)
                {
                    message = $"Inicio de '{serviceName}' restaurado (Tipo: {original.OriginalStartType}), pero falló al arrancar: {ex.Message}";
                    return true;
                }
            }

            message = $"Servicio '{serviceName}' restaurado con éxito a su estado original.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Fallo al restaurar el servicio '{serviceName}': {ex.Message}";
            return false;
        }
    }

    private void EnsureOriginalStateSaved(string serviceName)
    {
        if (_originalStates.ContainsKey(serviceName)) return;

        bool wasRunning = false;
        int origStart = 3;

        try
        {
            using var sc = new ServiceController(serviceName);
            wasRunning = sc.Status == ServiceControllerStatus.Running;
        }
        catch { }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{ServicesRegistryRoot}\{serviceName}");
            if (key?.GetValue("Start") is int val)
            {
                origStart = val;
            }
        }
        catch { }

        _originalStates[serviceName] = (wasRunning, origStart);
    }
}
