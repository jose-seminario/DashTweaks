using System.Security;
using Microsoft.Win32;

namespace DashTweaks.Core.Managers;

public enum StartupLocationType
{
    RegistryCurrentUser,
    RegistryLocalMachine,
    StartupFolder
}

public class StartupItem
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public StartupLocationType LocationType { get; set; }
    public bool IsEnabled { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
}

public class StartupManager
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DisabledSubKey = @"Software\DashTweaks\Disabled";

    private static readonly string StartupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
    private static readonly string StartupDisabledFolderPath = Path.Combine(StartupFolderPath, "Disabled");

    // Palabras clave para detectar controladores y componentes sensibles
    private static readonly string[] CriticalKeywords = { "security", "defender", "audio", "realtek", "nvidia", "intel", "amd" };

    /// <summary>
    /// Consulta todas las entradas de inicio en HKCU, HKLM y la carpeta de inicio (tanto activas como aisladas).
    /// </summary>
    public List<StartupItem> GetStartupItems()
    {
        var items = new List<StartupItem>();

        // 1. HKCU\Software\Microsoft\Windows\CurrentVersion\Run (Habilitados)
        ReadRegistryItems(Registry.CurrentUser, RunSubKey, StartupLocationType.RegistryCurrentUser, isEnabled: true, items);

        // 2. HKCU\Software\DashTweaks\Disabled (Deshabilitados)
        ReadRegistryItems(Registry.CurrentUser, DisabledSubKey, StartupLocationType.RegistryCurrentUser, isEnabled: false, items);

        // 3. HKLM\Software\Microsoft\Windows\CurrentVersion\Run (Habilitados)
        ReadRegistryItems(Registry.LocalMachine, RunSubKey, StartupLocationType.RegistryLocalMachine, isEnabled: true, items);

        // 4. HKLM\Software\DashTweaks\Disabled (Deshabilitados)
        ReadRegistryItems(Registry.LocalMachine, DisabledSubKey, StartupLocationType.RegistryLocalMachine, isEnabled: false, items);

        // 5. Carpeta de Inicio (Habilitados)
        ReadFolderItems(StartupFolderPath, isEnabled: true, items);

        // 6. Subcarpeta de Inicio Disabled (Deshabilitados)
        if (Directory.Exists(StartupDisabledFolderPath))
        {
            ReadFolderItems(StartupDisabledFolderPath, isEnabled: false, items);
        }

        return items.OrderBy(i => i.Name).ToList();
    }

    /// <summary>
    /// Desactiva una aplicación moviéndola de forma segura a DashTweaks\Disabled sin borrar datos.
    /// </summary>
    public bool DisableStartupItem(StartupItem item, out string message)
    {
        try
        {
            if (item.LocationType == StartupLocationType.RegistryCurrentUser)
            {
                return MoveRegistryValue(Registry.CurrentUser, RunSubKey, DisabledSubKey, item.Name, out message);
            }
            else if (item.LocationType == StartupLocationType.RegistryLocalMachine)
            {
                // Salvaguarda 1: Manejo de excepciones en HKLM
                try
                {
                    return MoveRegistryValue(Registry.LocalMachine, RunSubKey, DisabledSubKey, item.Name, out message);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
                {
                    message = "[ERROR] Se requieren permisos de Administrador para modificar el inicio de todos los usuarios (HKLM).";
                    return false;
                }
            }
            else if (item.LocationType == StartupLocationType.StartupFolder)
            {
                Directory.CreateDirectory(StartupDisabledFolderPath);
                string currentPath = item.FilePath;
                string fileName = Path.GetFileName(currentPath);
                string destinationPath = Path.Combine(StartupDisabledFolderPath, fileName);

                if (File.Exists(currentPath))
                {
                    if (File.Exists(destinationPath)) File.Delete(destinationPath);
                    File.Move(currentPath, destinationPath);
                    item.FilePath = destinationPath;
                    item.IsEnabled = false;
                    message = $"[INICIO] Desactivado: {item.Name} (Carpeta de Inicio).";
                    return true;
                }

                message = $"[ERROR] No se encontró el archivo de acceso directo en: {currentPath}";
                return false;
            }

            message = "[ERROR] Ubicación de inicio no reconocida.";
            return false;
        }
        catch (Exception ex)
        {
            message = $"[ERROR] Falló al desactivar {item.Name}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Reactiva una aplicación devolviéndola a su clave o carpeta original de arranque.
    /// </summary>
    public bool EnableStartupItem(StartupItem item, out string message)
    {
        try
        {
            if (item.LocationType == StartupLocationType.RegistryCurrentUser)
            {
                return MoveRegistryValue(Registry.CurrentUser, DisabledSubKey, RunSubKey, item.Name, out message);
            }
            else if (item.LocationType == StartupLocationType.RegistryLocalMachine)
            {
                try
                {
                    return MoveRegistryValue(Registry.LocalMachine, DisabledSubKey, RunSubKey, item.Name, out message);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
                {
                    message = "[ERROR] Se requieren permisos de Administrador para modificar el inicio de todos los usuarios (HKLM).";
                    return false;
                }
            }
            else if (item.LocationType == StartupLocationType.StartupFolder)
            {
                string currentPath = item.FilePath;
                string fileName = Path.GetFileName(currentPath);
                string destinationPath = Path.Combine(StartupFolderPath, fileName);

                if (File.Exists(currentPath))
                {
                    if (File.Exists(destinationPath)) File.Delete(destinationPath);
                    File.Move(currentPath, destinationPath);
                    item.FilePath = destinationPath;
                    item.IsEnabled = true;
                    message = $"[INICIO] Activado: {item.Name} (Carpeta de Inicio).";
                    return true;
                }

                message = $"[ERROR] No se encontró el archivo aislado en: {currentPath}";
                return false;
            }

            message = "[ERROR] Ubicación de inicio no reconocida.";
            return false;
        }
        catch (Exception ex)
        {
            message = $"[ERROR] Falló al activar {item.Name}: {ex.Message}";
            return false;
        }
    }

    private static bool MoveRegistryValue(RegistryKey hive, string sourceKeyPath, string targetKeyPath, string valueName, out string message)
    {
        using var sourceKey = hive.OpenSubKey(sourceKeyPath, true);
        if (sourceKey == null)
        {
            message = $"[ERROR] Clave de origen no encontrada: {sourceKeyPath}";
            return false;
        }

        object? val = sourceKey.GetValue(valueName);
        if (val == null)
        {
            message = $"[ERROR] Entrada no encontrada: {valueName}";
            return false;
        }

        var kind = sourceKey.GetValueKind(valueName);

        using var targetKey = hive.CreateSubKey(targetKeyPath, true);
        if (targetKey == null)
        {
            message = $"[ERROR] No se pudo crear o abrir la clave de destino: {targetKeyPath}";
            return false;
        }

        targetKey.SetValue(valueName, val, kind);
        sourceKey.DeleteValue(valueName, false);

        string hiveName = hive == Registry.CurrentUser ? "HKCU" : "HKLM";
        message = targetKeyPath.Contains("Disabled")
            ? $"[INICIO] Desactivado: {valueName} (Registro {hiveName})."
            : $"[INICIO] Activado: {valueName} (Registro {hiveName}).";
        return true;
    }

    private static void ReadRegistryItems(RegistryKey hive, string keyPath, StartupLocationType locationType, bool isEnabled, List<StartupItem> list)
    {
        try
        {
            using var key = hive.OpenSubKey(keyPath, false);
            if (key == null) return;

            foreach (var valName in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(valName)) continue;
                string cmd = key.GetValue(valName)?.ToString() ?? string.Empty;

                list.Add(new StartupItem
                {
                    Name = valName,
                    Command = cmd,
                    LocationType = locationType,
                    IsEnabled = isEnabled,
                    IsCritical = IsCriticalApplication(valName, cmd)
                });
            }
        }
        catch
        {
            // Ignorar claves inaccesibles
        }
    }

    private static void ReadFolderItems(string folderPath, bool isEnabled, List<StartupItem> list)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return;

            var files = Directory.GetFiles(folderPath);
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                // Ignorar archivos del sistema como desktop.ini o directorios
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                list.Add(new StartupItem
                {
                    Name = Path.GetFileNameWithoutExtension(fileName),
                    Command = file,
                    LocationType = StartupLocationType.StartupFolder,
                    IsEnabled = isEnabled,
                    FilePath = file,
                    IsCritical = IsCriticalApplication(fileName, file)
                });
            }
        }
        catch
        {
            // Ignorar errores de acceso
        }
    }

    private static bool IsCriticalApplication(string name, string command)
    {
        string combined = $"{name} {command}".ToLowerInvariant();
        return CriticalKeywords.Any(k => combined.Contains(k));
    }
}
