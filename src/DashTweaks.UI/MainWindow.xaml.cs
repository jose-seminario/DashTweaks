using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DashTweaks.Core.Managers;
using DashTweaks.Core.Models;
using DashTweaks.Tweaks;
using DashTweaks.Tweaks.Implementations;
using DashTweaks.UI.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;

namespace DashTweaks.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly BackupManager _backupManager = new();
    private readonly RollbackManager _rollbackManager = new();
    private readonly ServiceManagerTweak _serviceManager = new();
    private readonly SettingsManager _settingsManager = new();
    private readonly GameDetector _gameDetector = new();
    private readonly SystemMonitor _systemMonitor = new();
    private readonly BloatwareManager _bloatwareManager = new();

    // Temporizador de monitoreo en tiempo real
    private readonly DispatcherTimer _monitorTimer = new();

    // Tweaks específicos para el Auto-Game Mode y control de energía
    private readonly PowerPlanTweak _powerPlanTweak = new();
    private readonly MmcSsTweak _mmcSsTweak = new();
    private readonly ProcessSuspensionTweak _processSuspensionTweak = new();
    private readonly TimerResolutionTweak _timerResolutionTweak = new();

    private DashTweaksSettings _settings = new();
    private bool _hasSessionCheckpoint = false;
    private bool _isBusy = false;
    private bool _isFinalShutdown = false;
    private bool _isAutoGameModeActive = false;

    // Lista de IDs que componen el Perfil Recomendado (Gaming)
    private static readonly HashSet<string> RecommendedProfileTweakIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "power-plan-high-performance",
        "gaming-disable-gamedvr",
        "system-mmcss-gaming-priority",
        "gpu-fullscreen-exclusive-sfio",
        "system-svchost-split-threshold"
    };

    public ObservableCollection<TweakItemViewModel> Tweaks { get; } = new();
    public ObservableCollection<ServiceItemViewModel> Services { get; } = new();
    public ObservableCollection<WhitelistProcessItemViewModel> AllProcesses { get; } = new();
    public ObservableCollection<WhitelistProcessItemViewModel> FilteredProcesses { get; } = new();
    public ObservableCollection<BloatwareItemViewModel> BloatwareApps { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _settings = _settingsManager.LoadSettings();

        InitializeTweaks();
        InitializeServices();
        InitializeProcesses();
        InitializeGameDetector();
        InitializeSystemMonitor();
        InitializeBloatware();

        TweaksItemsControl.ItemsSource = Tweaks;
        ServicesItemsControl.ItemsSource = Services;
        ProcessesItemsControl.ItemsSource = FilteredProcesses;
        BloatwareItemsControl.ItemsSource = BloatwareApps;

        ChkAutoGameMode.IsChecked = _settings.AutoGameModeEnabled;

        LogMessage("DashTweaks v1.6 (Bloatware Remover Edition) inicializado.");
        LogMessage($"Dashboard activo • Detector de Juegos: {(_settings.AutoGameModeEnabled ? "Activo" : "Desactivado")}.");
    }

    #region Inicialización y Ciclo de Vida de la Ventana

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == IsVisibleProperty)
        {
            if (IsVisible)
            {
                _monitorTimer.Start();
            }
            else
            {
                _monitorTimer.Stop();
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isFinalShutdown)
        {
            e.Cancel = true;
            _monitorTimer.Stop();
            Hide();
            App.Instance.ShowNotification("DashTweaks", "Ejecutándose en segundo plano en la bandeja del sistema.", BalloonIcon.Info);
        }
        else
        {
            _monitorTimer.Stop();
            _systemMonitor.Dispose();

            if (_timerResolutionTweak.IsActive)
            {
                _timerResolutionTweak.Revert();
            }
            _gameDetector.Dispose();
            base.OnClosing(e);
        }
    }

    public void PrepareForFinalShutdown()
    {
        _isFinalShutdown = true;

        _monitorTimer.Stop();
        _systemMonitor.Dispose();

        if (_isAutoGameModeActive)
        {
            _processSuspensionTweak.Revert();
            _mmcSsTweak.Revert();
            _powerPlanTweak.Revert();
        }

        if (_timerResolutionTweak.IsActive)
        {
            _timerResolutionTweak.Revert();
        }

        _gameDetector.Stop();
        _gameDetector.Dispose();

        Close();
    }

    public void SyncAutoGameModeState(bool isEnabled)
    {
        _settings.AutoGameModeEnabled = isEnabled;
        ChkAutoGameMode.IsChecked = isEnabled;
        LogMessage($"[CONFIG] Auto-Game Mode sincronizado: {(isEnabled ? "ACTIVADO" : "DESACTIVADO")}.");
    }

    #endregion

    #region Dashboard de Monitoreo en Tiempo Real

    private void InitializeSystemMonitor()
    {
        _monitorTimer.Interval = TimeSpan.FromMilliseconds(1000);
        _monitorTimer.Tick += MonitorTimer_Tick;
        _monitorTimer.Start();
    }

    private void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var metrics = _systemMonitor.GetMetrics();

            TxtCpuUsage.Text = $"{metrics.CpuUsagePercent:0.0} %";
            PbCpuUsage.Value = metrics.CpuUsagePercent;

            TxtRamUsage.Text = $"{metrics.RamUsagePercent}% ({metrics.UsedRamMb:N0} MB / {metrics.TotalRamMb:N0} MB)";
            PbRamUsage.Value = metrics.RamUsagePercent;
        }
        catch { }
    }

    #endregion

    #region Lógica del Perfil Recomendado (Gaming)

    private async void BtnApplyRecommendedProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var confirm = MessageBox.Show(
            "El Perfil Recomendado aplicará de forma segura las optimizaciones esenciales sin tocar red ni timers agresivos:\n\n" +
            "• Plan de Energía: Alto Rendimiento\n" +
            "• Desactivar GameDVR y capturas\n" +
            "• Prioridad MMCSS para videojuegos\n" +
            "• Pantalla completa exclusiva (FSE) y SFIO\n" +
            "• Separación de procesos SvcHost (64 MB)\n\n" +
            "¿Deseas aplicar este perfil ahora?",
            "DashTweaks - Perfil Recomendado (Gaming)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (confirm != MessageBoxResult.Yes)
        {
            LogMessage("[CANCELADO] El usuario canceló la aplicación del perfil recomendado.");
            return;
        }

        if (!_hasSessionCheckpoint)
        {
            bool checkpointOk = await EnsureSessionCheckpointAsync("Perfil Recomendado");
            if (!checkpointOk) return;
        }

        SetBusyState(true, "Aplicando Perfil Recomendado...");
        LogMessage("--- Aplicando Perfil Recomendado (Gaming) ---");

        int appliedCount = 0;
        int skippedCount = 0;

        foreach (var vm in Tweaks)
        {
            if (RecommendedProfileTweakIds.Contains(vm.Id))
            {
                if (vm.IsApplied)
                {
                    LogMessage($"[PERFIL] '{vm.Name}' ya estaba activo, omitiendo.");
                    skippedCount++;
                    continue;
                }

                LogMessage($"[PERFIL] Aplicando '{vm.Name}'...");
                bool ok = await Task.Run(() => vm.Tweak.Apply());

                if (ok)
                {
                    vm.IsApplied = true;
                    appliedCount++;
                    LogMessage($"[PERFIL] '{vm.Name}' aplicado con éxito.");
                }
                else
                {
                    LogMessage($"[ERROR] Falló la aplicación de '{vm.Name}'.");
                }
            }
        }

        SetBusyState(false, "Listo");

        string summary = $"Perfil Gaming aplicado con éxito ({appliedCount} nuevos, {skippedCount} ya activos). Reinicia para aplicar cambios de red/GPU.";
        LogMessage($"[PERFIL] {summary}");
        MessageBox.Show(summary, "DashTweaks - Perfil Recomendado", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Detector de Juegos (GameDetector)

    private void InitializeGameDetector()
    {
        _gameDetector.GameStarted += (gameName) =>
        {
            Dispatcher.InvokeAsync(async () =>
            {
                await HandleGameStartedAsync(gameName);
            });
        };

        _gameDetector.GameStopped += (gameName) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                HandleGameStopped(gameName);
            });
        };

        _gameDetector.Start();
    }

    private async Task HandleGameStartedAsync(string gameName)
    {
        if (!_settings.AutoGameModeEnabled) return;
        if (_isAutoGameModeActive) return;

        LogMessage($"[DETECTOR] Juego detectado: {gameName.ToUpperInvariant()}. Iniciando protocolo Auto-Game Mode...");

        if (!_hasSessionCheckpoint)
        {
            LogMessage("[SEGURIDAD] Generando Checkpoint previo a la auto-optimización...");
            BackupResult backupResult = await Task.Run(() => _backupManager.CreateCheckpoint());
            if (backupResult.Success)
            {
                _hasSessionCheckpoint = true;
                LogMessage($"[CHECKPOINT OK] Respaldo guardado en: {backupResult.BackupPath}");
            }
            else
            {
                LogMessage($"[AVISO] No se pudo crear el Checkpoint automático: {backupResult.Message}");
            }
        }

        await Task.Run(() =>
        {
            _powerPlanTweak.Apply();
            _mmcSsTweak.Apply();
            _processSuspensionTweak.Apply();
        });

        _isAutoGameModeActive = true;

        UpdateTweakVisualStatus(_powerPlanTweak.Id, true);
        UpdateTweakVisualStatus(_mmcSsTweak.Id, true);
        UpdateTweakVisualStatus(_processSuspensionTweak.Id, true);

        string notif = $"DashTweaks: Game Mode ACTIVADO para {gameName.ToUpperInvariant()}";
        LogMessage($"[AUTO-GAME MODE] Activado con éxito para '{gameName}'.");
        App.Instance.ShowNotification("DashTweaks - Modo Juego", notif, BalloonIcon.Info);
    }

    private void HandleGameStopped(string gameName)
    {
        if (!_isAutoGameModeActive) return;

        LogMessage($"[DETECTOR] Cierre de juego detectado: {gameName.ToUpperInvariant()}. Restaurando configuración...");

        _processSuspensionTweak.Revert();
        _mmcSsTweak.Revert();
        _powerPlanTweak.Revert();

        _isAutoGameModeActive = false;

        UpdateTweakVisualStatus(_powerPlanTweak.Id, false);
        UpdateTweakVisualStatus(_mmcSsTweak.Id, false);
        UpdateTweakVisualStatus(_processSuspensionTweak.Id, false);

        string notif = "DashTweaks: Game Mode DESACTIVADO. Sistema restaurado";
        LogMessage($"[AUTO-GAME MODE] Desactivado. Sistema restaurado a valores previos.");
        App.Instance.ShowNotification("DashTweaks - Modo Juego", notif, BalloonIcon.Info);
    }

    private void UpdateTweakVisualStatus(string tweakId, bool isApplied)
    {
        var vm = Tweaks.FirstOrDefault(t => t.Id == tweakId);
        if (vm != null)
        {
            vm.IsApplied = isApplied;
        }
    }

    private void ChkAutoGameMode_Changed(object sender, RoutedEventArgs e)
    {
        bool isEnabled = ChkAutoGameMode.IsChecked == true;
        _settings.AutoGameModeEnabled = isEnabled;
        _settingsManager.SaveSettings(_settings);

        LogMessage($"[CONFIG] Auto-Game Mode {(isEnabled ? "ACTIVADO" : "DESACTIVADO")}.");
        App.Instance.ShowNotification("DashTweaks", $"Auto-Game Mode {(isEnabled ? "ACTIVADO" : "DESACTIVADO")}", BalloonIcon.Info);
    }

    #endregion

    #region Desinstalador de Bloatware UWP

    private void InitializeBloatware()
    {
        _ = RefreshBloatwareAsync();
    }

    private async Task RefreshBloatwareAsync()
    {
        SetBusyState(true, "Consultando aplicaciones UWP instaladas...");
        LogMessage("[BLOATWARE] Escaneando aplicaciones preinstaladas de Windows...");

        try
        {
            var apps = await Task.Run(() => _bloatwareManager.GetInstalledBloatware());

            BloatwareApps.Clear();
            foreach (var app in apps)
            {
                BloatwareApps.Add(new BloatwareItemViewModel(app));
            }

            int installedCount = BloatwareApps.Count(a => a.IsInstalled);
            LogMessage($"[BLOATWARE] Escaneo completado: {installedCount} apps no esenciales detectadas.");
        }
        catch (Exception ex)
        {
            LogMessage($"[ERROR] Error al consultar Bloatware: {ex.Message}");
        }
        finally
        {
            SetBusyState(false, "Listo");
        }
    }

    private async void BtnUninstallBloatware_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selected = BloatwareApps.Where(a => a.IsSelected && a.IsInstalled).ToList();
        if (!selected.Any())
        {
            MessageBox.Show("Selecciona al menos una aplicación que esté instalada para desinstalar.", "DashTweaks - Bloatware", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"¿Deseas desinstalar las {selected.Count} aplicaciones seleccionadas?\n\n(Podrás volver a instalarlas desde la Microsoft Store en cualquier momento).",
            "Confirmación de Desinstalación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (confirm != MessageBoxResult.Yes) return;

        if (!_hasSessionCheckpoint)
        {
            bool checkpointOk = await EnsureSessionCheckpointAsync("Desinstalación de Bloatware");
            if (!checkpointOk) return;
        }

        SetBusyState(true, "Desinstalando aplicaciones seleccionadas...");
        LogMessage("--- Iniciando Desinstalación de Bloatware ---");

        int successCount = 0;

        await Task.Run(() =>
        {
            foreach (var item in selected)
            {
                bool ok = _bloatwareManager.UninstallApp(item.AppInfo, out string msg);
                Dispatcher.Invoke(() =>
                {
                    item.IsInstalled = item.AppInfo.IsInstalled;
                    if (ok)
                    {
                        successCount++;
                        LogMessage(msg);
                    }
                    else
                    {
                        LogMessage(msg);
                    }
                });
            }
        });

        SetBusyState(false, "Listo");
        MessageBox.Show($"Operación finalizada. {successCount} aplicaciones desinstaladas.", "DashTweaks - Bloatware", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnRestoreBloatware_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selected = BloatwareApps.Where(a => a.IsSelected).ToList();
        if (!selected.Any())
        {
            MessageBox.Show("Selecciona al menos una aplicación para restaurar.", "DashTweaks - Restauración de Apps", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetBusyState(true, "Restaurando aplicaciones vía Store / Winget...");
        LogMessage("--- Restaurando Aplicaciones UWP ---");

        await Task.Run(() =>
        {
            foreach (var item in selected)
            {
                bool ok = _bloatwareManager.RestoreApp(item.AppInfo, out string msg);
                Dispatcher.Invoke(() =>
                {
                    item.IsInstalled = item.AppInfo.IsInstalled;
                    LogMessage(msg);
                });
            }
        });

        SetBusyState(false, "Listo");
        MessageBox.Show("Proceso de restauración completado. Las aplicaciones iniciaron su reinstalación vía Winget o abrieron la tienda Microsoft Store.", "DashTweaks - Restauración", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnRefreshBloatware_Click(object sender, RoutedEventArgs e)
    {
        await RefreshBloatwareAsync();
    }

    #endregion

    #region Inicialización de Componentes

    private void InitializeTweaks()
    {
        Tweaks.Add(new TweakItemViewModel(_powerPlanTweak));
        Tweaks.Add(new TweakItemViewModel(new GameDvrTweak()));
        Tweaks.Add(new TweakItemViewModel(_mmcSsTweak));
        Tweaks.Add(new TweakItemViewModel(new NagleTweak()));
        Tweaks.Add(new TweakItemViewModel(_processSuspensionTweak));
        Tweaks.Add(new TweakItemViewModel(new MemoryPurgeTweak()));
        Tweaks.Add(new TweakItemViewModel(new NetworkAdvancedTweak()));
        Tweaks.Add(new TweakItemViewModel(new HidTweak()));
        Tweaks.Add(new TweakItemViewModel(new HagsTweak()));
        Tweaks.Add(new TweakItemViewModel(new SvcHostSplitTweak()));
        Tweaks.Add(new TweakItemViewModel(new MouseAccelerationTweak()));
        Tweaks.Add(new TweakItemViewModel(new FseOptimizationTweak()));
        Tweaks.Add(new TweakItemViewModel(_timerResolutionTweak));
        Tweaks.Add(new TweakItemViewModel(new EcoQosTweak()));
    }

    private void InitializeServices()
    {
        RefreshServiceStates();
    }

    private void InitializeProcesses()
    {
        RefreshRunningProcesses();
    }

    private void RefreshServiceStates()
    {
        try
        {
            var states = _serviceManager.GetServiceStates();
            Services.Clear();
            foreach (var st in states)
            {
                Services.Add(new ServiceItemViewModel(st));
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[AVISO] No se pudieron consultar los servicios: {ex.Message}");
        }
    }

    private void RefreshRunningProcesses()
    {
        try
        {
            AllProcesses.Clear();
            var whitelistSet = new HashSet<string>(
                _settings.GameModeWhitelist.Select(NormalizeName),
                StringComparer.OrdinalIgnoreCase
            );

            var processes = Process.GetProcesses()
                .Where(p =>
                {
                    try
                    {
                        return p.MainWindowHandle != IntPtr.Zero || IsKnownApp(p.ProcessName);
                    }
                    catch
                    {
                        return false;
                    }
                })
                .OrderBy(p => p.ProcessName);

            foreach (var proc in processes)
            {
                try
                {
                    string norm = NormalizeName(proc.ProcessName);
                    bool isWhitelisted = whitelistSet.Contains(norm);
                    AllProcesses.Add(new WhitelistProcessItemViewModel(proc.ProcessName, proc.MainWindowTitle, proc.Id, isWhitelisted));
                }
                catch { }
            }

            ApplyProcessFilter();
        }
        catch (Exception ex)
        {
            LogMessage($"[AVISO] Error al listar procesos activos: {ex.Message}");
        }
    }

    private void ApplyProcessFilter()
    {
        string query = TxtSearchProcess?.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        FilteredProcesses.Clear();
        foreach (var item in AllProcesses)
        {
            if (string.IsNullOrEmpty(query) ||
                item.ProcessName.ToLowerInvariant().Contains(query) ||
                item.MainWindowTitle.ToLowerInvariant().Contains(query))
            {
                FilteredProcesses.Add(item);
            }
        }
    }

    private static bool IsKnownApp(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower is "discord" or "steam" or "spotify" or "chrome" or "msedge" or "obs64" or "ts3client_win64" or "telegram";
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        string clean = name.Trim();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(0, clean.Length - 4);
        }
        return clean.ToLowerInvariant();
    }

    #endregion

    #region Manejo de Tweaks Individuales

    private async void TweakActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        if (sender is not Button btn || btn.DataContext is not TweakItemViewModel item) return;

        if (!item.IsApplied)
        {
            if (item.Risk == RiskLevel.Aggressive)
            {
                var confirmAggressive = MessageBox.Show(
                    "Esta optimización incrementa la frecuencia del reloj del sistema a 0.5ms para máxima responsividad competitiva, pero aumentará el consumo de batería y energía.\n\n¿Deseas activarla?",
                    "DashTweaks - Advertencia de Riesgo Agresivo",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (confirmAggressive != MessageBoxResult.Yes)
                {
                    LogMessage("[CANCELADO] El usuario canceló la optimización de nivel Agresivo.");
                    return;
                }
            }

            if (item.Tweak is ProcessSuspensionTweak)
            {
                var confirm = MessageBox.Show(
                    "La barra de tareas y el escritorio dejarán de responder temporalmente hasta que reviertas esta acción. Esto es normal y libera recursos para el juego.\n\n(Las aplicaciones en tu Lista Blanca NO serán suspendidas).\n\n¿Deseas suspender los procesos secundarios ahora?",
                    "DashTweaks - Confirmación de Suspensión de Procesos",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (confirm != MessageBoxResult.Yes)
                {
                    LogMessage("[CANCELADO] El usuario canceló la suspensión de procesos.");
                    return;
                }
            }

            if (!_hasSessionCheckpoint)
            {
                bool checkpointOk = await EnsureSessionCheckpointAsync(item.Name);
                if (!checkpointOk) return;
            }

            SetBusyState(true, $"Aplicando '{item.Name}'...");
            LogMessage($"Aplicando tweak: {item.Name}...");

            bool success = await Task.Run(() => item.Tweak.Apply());

            if (item.Tweak is ProcessSuspensionTweak procTweak)
            {
                if (success)
                {
                    item.IsApplied = true;

                    foreach (var skipped in procTweak.SkippedProcesses)
                    {
                        LogMessage($"[LISTA BLANCA] Saltando {skipped} (en Lista Blanca).");
                    }

                    LogMessage($"[ÉXITO] '{item.Name}' aplicado. ({procTweak.SuspendedCount} procesos suspendidos).");
                    MessageBox.Show(
                        $"Procesos secundarios suspendidos ({procTweak.SuspendedCount}).\nExclusiones respetadas: {procTweak.SkippedProcesses.Count}.\n\nPresiona 'REVERTIR' al finalizar la partida para restaurar el escritorio.",
                        "DashTweaks - Game Mode",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    LogMessage($"[AVISO] No se encontraron procesos activos para suspender ({item.Name}).");
                }
            }
            else if (item.Tweak is MemoryPurgeTweak memTweak)
            {
                if (success)
                {
                    item.IsApplied = true;
                    LogMessage($"[ÉXITO] {memTweak.StatusMessage}");
                    MessageBox.Show(memTweak.StatusMessage, "DashTweaks - Memoria", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LogMessage($"[AVISO] {memTweak.StatusMessage}");
                    MessageBox.Show(memTweak.StatusMessage, "DashTweaks - Purga Omitida", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else if (item.Tweak is EcoQosTweak ecoTweak)
            {
                if (success)
                {
                    item.IsApplied = true;
                    LogMessage($"[ÉXITO] EcoQoS activado en {ecoTweak.ThrottledCount} procesos de fondo.");
                    MessageBox.Show($"EcoQoS aplicado correctamente en {ecoTweak.ThrottledCount} procesos en segundo plano.", "DashTweaks - EcoQoS", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LogMessage($"[AVISO] No se detectaron procesos de usuario en segundo plano para limitar.");
                }
            }
            else
            {
                if (success)
                {
                    item.IsApplied = true;
                    string restartNotice = item.RequiresRestart ? " [IMPORTANTE: Requiere reiniciar el PC]" : "";
                    LogMessage($"[ÉXITO] '{item.Name}' aplicado correctamente.{restartNotice}");
                }
                else
                {
                    LogMessage($"[ERROR] Falló la aplicación de '{item.Name}'. Verifica permisos del registro.");
                    MessageBox.Show($"No se pudo aplicar '{item.Name}'.", "DashTweaks - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            SetBusyState(true, $"Revirtiendo '{item.Name}'...");
            LogMessage($"Revirtiendo tweak: {item.Name}...");

            bool success = await Task.Run(() => item.Tweak.Revert());

            if (success)
            {
                item.IsApplied = false;
                string restartNotice = item.RequiresRestart ? " [Reinicia para restablecer la configuración]" : "";
                LogMessage($"[REVERTIDO] '{item.Name}' restaurado a sus valores previos.{restartNotice}");

                if (item.Tweak is ProcessSuspensionTweak)
                {
                    MessageBox.Show(
                        "Todos los procesos suspendidos han sido reanudados. El escritorio y las aplicaciones han vuelto a la normalidad.",
                        "DashTweaks - Procesos Reanudados",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
            }
            else
            {
                LogMessage($"[ERROR] Falló la reversión de '{item.Name}'.");
                MessageBox.Show($"No se pudo revertir '{item.Name}'.", "DashTweaks - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        SetBusyState(false, "Listo");
    }

    #endregion

    #region Gestor de Servicios

    private async void BtnDisableServices_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selected = Services.Where(s => s.IsSelected).ToList();
        if (!selected.Any())
        {
            MessageBox.Show("Selecciona al menos un servicio con la casilla correspondiente.", "DashTweaks", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_hasSessionCheckpoint)
        {
            bool checkpointOk = await EnsureSessionCheckpointAsync("Gestor de Servicios");
            if (!checkpointOk) return;
        }

        SetBusyState(true, "Deshabilitando servicios seleccionados...");
        LogMessage("--- Deshabilitando Servicios Seleccionados ---");

        await Task.Run(() =>
        {
            foreach (var svc in selected)
            {
                bool ok = _serviceManager.DisableService(svc.ServiceName, out string msg);
                Dispatcher.Invoke(() =>
                {
                    if (ok) LogMessage($"[OK] {msg}");
                    else LogMessage($"[AVISO] {msg}");
                });
            }
        });

        RefreshServiceStates();
        SetBusyState(false, "Listo");
        MessageBox.Show("Los servicios seleccionados han sido detenidos y configurados como Deshabilitados.", "DashTweaks - Servicios", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnRestoreServices_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        SetBusyState(true, "Restaurando servicios a su estado original...");
        LogMessage("--- Restaurando Servicios Originales ---");

        await Task.Run(() =>
        {
            foreach (var svc in Services)
            {
                bool ok = _serviceManager.RestoreService(svc.ServiceName, out string msg);
                Dispatcher.Invoke(() =>
                {
                    if (ok) LogMessage($"[REVERTIDO] {msg}");
                    else LogMessage($"[AVISO] {msg}");
                });
            }
        });

        RefreshServiceStates();
        SetBusyState(false, "Listo");
        MessageBox.Show("Los servicios han sido restablecidos a sus tipos de inicio y estados originales.", "DashTweaks - Servicios", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnRefreshServices_Click(object sender, RoutedEventArgs e)
    {
        RefreshServiceStates();
        LogMessage("[ACTUALIZADO] Estados en vivo de servicios de Windows actualizados.");
    }

    #endregion

    #region Lista Blanca (Exclusiones Game Mode)

    private void TxtSearchProcess_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyProcessFilter();
    }

    private void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e)
    {
        RefreshRunningProcesses();
        LogMessage("[ACTUALIZADO] Lista de procesos activos actualizada.");
    }

    private void BtnSaveWhitelist_Click(object sender, RoutedEventArgs e)
    {
        var selectedNames = AllProcesses
            .Where(p => p.IsWhitelisted)
            .Select(p => p.ExeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _settings.GameModeWhitelist = selectedNames;
        bool ok = _settingsManager.SaveSettings(_settings);

        if (ok)
        {
            LogMessage($"[CONFIG] Lista Blanca guardada con {selectedNames.Count} exclusiones en config.json.");
            MessageBox.Show(
                $"Lista Blanca guardada correctamente con {selectedNames.Count} exclusiones.\n\nEstos procesos no serán suspendidos cuando actives el Game Mode.",
                "DashTweaks - Lista Blanca Guardada",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        else
        {
            LogMessage("[ERROR] No se pudo guardar la configuración de Lista Blanca.");
            MessageBox.Show("Error al guardar la configuración.", "DashTweaks - Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Checkpoints Globales y Auxiliares

    private async Task<bool> EnsureSessionCheckpointAsync(string contextName)
    {
        SetBusyState(true, "Creando punto de restauración... por favor espera");
        LogMessage($"[SEGURIDAD] Primera optimización solicitada ('{contextName}'). Generando Checkpoint obligatorio...");

        BackupResult backupResult = await Task.Run(() => _backupManager.CreateCheckpoint());

        if (!backupResult.Success)
        {
            LogMessage($"[CANCELADO] Falló la creación del Checkpoint de seguridad: {backupResult.Message}");
            LogMessage("[SEGURIDAD] La operación ha sido abortada para proteger el sistema.");
            MessageBox.Show(
                $"No se pudo crear el backup de seguridad obligatorio:\n\n{backupResult.Message}\n\nLa operación ha sido cancelada.",
                "DashTweaks - Operación Abortada",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            SetBusyState(false, "Operación cancelada por fallo en backup");
            return false;
        }

        _hasSessionCheckpoint = true;
        LogMessage($"[CHECKPOINT OK] Respaldo guardado en: {backupResult.BackupPath}");
        if (backupResult.RestorePointCreated)
        {
            LogMessage("[CHECKPOINT OK] Punto de restauración del sistema Windows creado exitosamente.");
        }
        else
        {
            LogMessage($"[AVISO] {backupResult.RestorePointMessage}");
        }

        return true;
    }

    private async void BtnCreateBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        SetBusyState(true, "Creando punto de restauración... por favor espera");
        LogMessage("--- Creando Checkpoint Manual ---");

        try
        {
            BackupResult result = await Task.Run(() => _backupManager.CreateCheckpoint());

            if (result.Success)
            {
                _hasSessionCheckpoint = true;
                LogMessage($"[ÉXITO] Backup creado en: {result.BackupPath}");
                if (result.RestorePointCreated)
                {
                    LogMessage("[OK] Punto de restauración del sistema creado exitosamente.");
                }
                else
                {
                    LogMessage($"[AVISO] {result.RestorePointMessage}");
                }

                MessageBox.Show(
                    result.Message,
                    "DashTweaks - Backup Manual",
                    MessageBoxButton.OK,
                    result.RestorePointCreated ? MessageBoxImage.Information : MessageBoxImage.Warning
                );
            }
            else
            {
                LogMessage($"[ERROR] {result.Message}");
                MessageBox.Show(result.Message, "DashTweaks - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[EXCEPCIÓN] {ex.Message}");
            MessageBox.Show($"Error inesperado: {ex.Message}", "DashTweaks", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, "Listo");
        }
    }

    private async void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        SetBusyState(true, "Consultando puntos de restauración disponibles...");
        LogMessage("--- Solicitud de Rollback Total ---");

        try
        {
            var backups = await Task.Run(() => _backupManager.GetAvailableBackups());

            if (!backups.Any())
            {
                LogMessage("[AVISO] No se encontraron checkpoints previos en disco.");
                MessageBox.Show(
                    "No se encontró ningún backup previo guardado en este equipo.",
                    "DashTweaks - Rollback",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            var latest = backups.First();
            var confirm = MessageBox.Show(
                $"¿Deseas restaurar la configuración completa desde el backup del {latest.CreatedAt:yyyy-MM-dd HH:mm:ss}?\n\nCarpeta: {latest.FolderPath}",
                "Confirmación de Rollback",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (confirm != MessageBoxResult.Yes)
            {
                LogMessage("[CANCELADO] El usuario canceló la restauración.");
                return;
            }

            SetBusyState(true, "Restaurando registro y esquemas de energía...");
            RollbackResult result = await Task.Run(() => _rollbackManager.RestoreFromBackup(latest.FolderPath));

            if (result.Success)
            {
                LogMessage($"[ÉXITO] {result.Message}");

                foreach (var tweakItem in Tweaks)
                {
                    tweakItem.IsApplied = false;
                }

                RefreshServiceStates();
                RefreshRunningProcesses();
                _ = RefreshBloatwareAsync();

                MessageBox.Show(result.Message, "DashTweaks - Rollback Exitoso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                LogMessage($"[ERROR] Falló la restauración: {result.Message}");
                foreach (var err in result.Errors)
                {
                    LogMessage($"       * {err}");
                }
                MessageBox.Show(result.Message, "DashTweaks - Error en Restauración", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[EXCEPCIÓN] {ex.Message}");
            MessageBox.Show($"Error durante la restauración: {ex.Message}", "DashTweaks", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, "Listo");
        }
    }

    private void SetBusyState(bool isBusy, string statusText)
    {
        _isBusy = isBusy;

        BtnCreateBackup.IsEnabled = !isBusy;
        BtnRestoreBackup.IsEnabled = !isBusy;
        BtnApplyRecommendedProfile.IsEnabled = !isBusy;
        BtnDisableServices.IsEnabled = !isBusy;
        BtnRestoreServices.IsEnabled = !isBusy;
        BtnRefreshServices.IsEnabled = !isBusy;
        BtnRefreshProcesses.IsEnabled = !isBusy;
        BtnSaveWhitelist.IsEnabled = !isBusy;
        BtnUninstallBloatware.IsEnabled = !isBusy;
        BtnRestoreBloatware.IsEnabled = !isBusy;
        BtnRefreshBloatware.IsEnabled = !isBusy;

        foreach (var tweak in Tweaks)
        {
            tweak.IsBusy = isBusy;
        }

        foreach (var app in BloatwareApps)
        {
            app.IsBusy = isBusy;
        }

        TxtStatus.Text = isBusy ? "Operando..." : statusText;
        TxtBusyStatus.Text = statusText;
        BusyBanner.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LogMessage(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        TxtLog.AppendText($"[{timestamp}] {message}\n");
        LogScrollViewer.ScrollToEnd();
    }

    #endregion
}
