using System.Windows;
using PowerMonitorApp.Services;
using PowerMonitorApp.Tray;

namespace PowerMonitorApp;

/// <summary>
/// Entry point WPF. Tidak ada main window — aplikasi jalan murni lewat
/// tray icon (lihat Tray/TrayIconController.cs).
/// </summary>
public partial class App : System.Windows.Application
{
    private const string ConfigPath = "config.json";

    private PowerSensorService? _sensorService;
    private TrayIconController? _trayController;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var config = ConfigService.LoadConfig(ConfigPath);

        _sensorService = new PowerSensorService(config.CpuTdpWatts, config.CpuIdleFloorWatts);
        var database = new DatabaseService();

        _trayController = new TrayIconController(_sensorService, database, config, ConfigPath);
        _trayController.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayController?.Dispose();
        _sensorService?.Dispose();
        base.OnExit(e);
    }
}
