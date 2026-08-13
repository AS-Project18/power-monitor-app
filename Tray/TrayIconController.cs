using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using PowerMonitorApp.Models;
using PowerMonitorApp.Services;
using WinForms = System.Windows.Forms;

namespace PowerMonitorApp.Tray;

/// <summary>
/// Menghubungkan tray icon (NotifyIcon), polling sensor berkala, dan popup
/// info. Klik kiri tray icon toggle popup; klik kanan buka menu (pengaturan,
/// riwayat, keluar). Semua perhitungan (baca sensor, kWh, biaya, histori)
/// tetap pakai service yang sudah ada di Services/, kelas ini murni
/// orkestrasi UI.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly PowerSensorService _sensorService;
    private readonly DatabaseService _database;
    private readonly AppConfig _config;
    private readonly string _configPath;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly DispatcherTimer _timer;
    private readonly PopupWindow _popup;

    private CostCalculatorService _costCalculator;
    private PowerReading? _latestReading;
    private DateTime _lastPurgeDate = DateTime.MinValue;

    public TrayIconController(PowerSensorService sensorService, DatabaseService database, AppConfig config, string configPath)
    {
        _sensorService = sensorService;
        _database = database;
        _config = config;
        _configPath = configPath;
        _costCalculator = new CostCalculatorService(config.TariffPerKwh);

        _popup = new PopupWindow();
        _popup.Deactivated += (_, _) => HidePopup();
        _popup.HistoryRequested += (_, _) => ShowHistory();

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Pengaturan...", null, (_, _) => ShowSettings());
        contextMenu.Items.Add("Lihat Riwayat...", null, (_, _) => ShowHistory());
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());

        var startupItem = new WinForms.ToolStripMenuItem("Jalankan saat Windows Start")
        {
            Checked = SafeIsStartupEnabled()
        };
        startupItem.Click += (_, _) => OnToggleStartupClicked(startupItem);
        contextMenu.Items.Add(startupItem);

        contextMenu.Items.Add("Diagnostik Sensor...", null, (_, _) => ShowDiagnostics());
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add("Keluar", null, OnExitClicked);

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "PC Power Monitor",
            ContextMenuStrip = contextMenu,
            Visible = true
        };
        _notifyIcon.MouseClick += OnTrayIconMouseClick;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(1, config.PollingIntervalSeconds))
        };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public async void Start()
    {
        _timer.Start();
        await PollAsync();
    }

    private async Task PollAsync()
    {
        var reading = await Task.Run(() => _sensorService.ReadCurrent());
        _database.InsertReading(reading);
        _latestReading = reading;

        await Task.Run(PurgeOldDataIfNeeded);

        if (_popup.IsVisible)
        {
            RefreshPopup();
        }
    }

    private void PurgeOldDataIfNeeded()
    {
        if (_config.RetentionDays <= 0) return;
        if (_lastPurgeDate == DateTime.Today) return;

        _database.PurgeOlderThan(DateTime.Today.AddDays(-_config.RetentionDays));
        _lastPurgeDate = DateTime.Today;
    }

    private void RefreshPopup()
    {
        if (_latestReading is null) return;

        var kwhToday = _database.GetTotalKwhSince(DateTime.Today);
        var costToday = _costCalculator.CalculateCost(kwhToday);
        _popup.UpdateData(_latestReading, kwhToday, costToday, _config.TariffPerKwh);
    }

    private void OnTrayIconMouseClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button != WinForms.MouseButtons.Left) return;
        TogglePopup();
    }

    private void TogglePopup()
    {
        if (_popup.IsVisible)
        {
            HidePopup();
        }
        else
        {
            ShowPopup();
        }
    }

    private void ShowPopup()
    {
        RefreshPopup();

        var workArea = SystemParameters.WorkArea;
        const double margin = 8;
        _popup.Left = workArea.Right - _popup.Width - margin;
        _popup.Top = workArea.Bottom - _popup.Height - margin;

        _popup.Show();
        _popup.Activate();
    }

    private void HidePopup()
    {
        if (_popup.IsVisible)
        {
            _popup.Hide();
        }
    }

    private void ShowSettings()
    {
        HidePopup();

        var dialog = new SettingsWindow(_config);
        if (dialog.ShowDialog() == true && dialog.Result is AppConfig updated)
        {
            _config.TariffPerKwh = updated.TariffPerKwh;
            _config.CpuTdpWatts = updated.CpuTdpWatts;
            _config.CpuIdleFloorWatts = updated.CpuIdleFloorWatts;
            _config.PollingIntervalSeconds = updated.PollingIntervalSeconds;
            _config.RetentionDays = updated.RetentionDays;

            _costCalculator = new CostCalculatorService(_config.TariffPerKwh);
            _sensorService.CpuTdpWattsForEstimate = _config.CpuTdpWatts;
            _sensorService.CpuIdleFloorWattsForEstimate = _config.CpuIdleFloorWatts;
            _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, _config.PollingIntervalSeconds));

            ConfigService.SaveConfig(_configPath, _config);
        }
    }

    private void ShowHistory()
    {
        HidePopup();

        var history = new HistoryWindow();
        history.Initialize(_database, _costCalculator);
        history.ShowDialog();
    }

    private void ShowDiagnostics()
    {
        HidePopup();

        var dump = _sensorService.DumpSensors();
        var diagnostics = new DiagnosticsWindow();
        diagnostics.LoadData(dump);
        diagnostics.ShowDialog();
    }

    private void OnToggleStartupClicked(WinForms.ToolStripMenuItem item)
    {
        try
        {
            if (item.Checked)
            {
                StartupService.Disable();
                item.Checked = false;
            }
            else
            {
                StartupService.Enable();
                item.Checked = true;
            }
        }
        catch (Exception ex)
        {
            WinForms.MessageBox.Show(
                $"Gagal mengubah pengaturan startup:\n{ex.Message}",
                "PC Power Monitor",
                WinForms.MessageBoxButtons.OK,
                WinForms.MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Ambil icon aplikasi dari resource exe (di-set lewat ApplicationIcon di
    /// .csproj, embed Assets/app.ico) supaya tray icon konsisten dengan icon
    /// di taskbar/Explorer. Fallback ke icon bawaan Windows kalau gagal.
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            // ApplicationIcon di .csproj menyematkan icon ke resource native
            // apphost (.exe), bukan ke assembly .dll terkelola — jadi harus
            // ambil path exe yang benar-benar berjalan (Environment.ProcessPath),
            // bukan Assembly.Location.
            var exePath = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

            if (exePath is null) return SystemIcons.Application;

            return Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private static bool SafeIsStartupEnabled()
    {
        try
        {
            return StartupService.IsEnabled();
        }
        catch
        {
            return false;
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _timer.Stop();
        _notifyIcon.MouseClick -= OnTrayIconMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _popup.Close();
    }
}
