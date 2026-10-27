using System.Diagnostics;
using System.Timers;

namespace DashTweaks.Core.Managers;

public class GameDetector : IDisposable
{
    private readonly System.Timers.Timer _pollTimer;
    private readonly HashSet<string> _knownGameProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs2",
        "valorant",
        "fortnite",
        "fortniteclient-win64-shipping",
        "r5apex",
        "league of legends",
        "leagueclient",
        "leagueclientux",
        "dota2",
        "gta5"
    };

    // Almacena los juegos actualmente detectados como activos
    private readonly HashSet<string> _activeGames = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public event Action<string>? GameStarted;
    public event Action<string>? GameStopped;

    public bool IsMonitoring => _pollTimer.Enabled;

    public GameDetector(double intervalMs = 3000)
    {
        _pollTimer = new System.Timers.Timer(intervalMs);
        _pollTimer.Elapsed += OnTimerElapsed;
        _pollTimer.AutoReset = true;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (!_pollTimer.Enabled)
            {
                _pollTimer.Start();
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_pollTimer.Enabled)
            {
                _pollTimer.Stop();
            }
        }
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        lock (_lock)
        {
            try
            {
                var currentRunningGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var processes = Process.GetProcesses();
                try
                {
                    foreach (var proc in processes)
                    {
                        try
                        {
                            string name = proc.ProcessName.ToLowerInvariant();
                            if (_knownGameProcesses.Contains(name))
                            {
                                currentRunningGames.Add(name);
                            }
                        }
                        catch
                        {
                            // Proceso cerrado durante la lectura
                        }
                    }
                }
                finally
                {
                    foreach (var proc in processes)
                    {
                        try
                        {
                            proc.Dispose();
                        }
                        catch { }
                    }
                }

                // 1. Detectar juegos recién iniciados
                foreach (var game in currentRunningGames)
                {
                    if (!_activeGames.Contains(game))
                    {
                        _activeGames.Add(game);
                        GameStarted?.Invoke(game);
                    }
                }

                // 2. Detectar juegos que se acaban de cerrar
                var stoppedGames = _activeGames.Where(g => !currentRunningGames.Contains(g)).ToList();
                foreach (var stopped in stoppedGames)
                {
                    _activeGames.Remove(stopped);
                    GameStopped?.Invoke(stopped);
                }
            }
            catch
            {
                // Manejo de excepción silencioso en timer en segundo plano
            }
        }
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
    }
}
