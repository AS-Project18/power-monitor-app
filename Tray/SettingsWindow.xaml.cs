using System.Globalization;
using System.Windows;
using PowerMonitorApp.Models;

namespace PowerMonitorApp.Tray;

/// <summary>Dialog pengaturan: tarif, estimasi daya CPU (idle/maks), interval polling, dan retensi data.</summary>
public partial class SettingsWindow : Window
{
    public AppConfig? Result { get; private set; }

    public SettingsWindow(AppConfig current)
    {
        InitializeComponent();

        TariffBox.Text = current.TariffPerKwh.ToString("0.##", CultureInfo.InvariantCulture);
        CpuTdpBox.Text = current.CpuTdpWatts.ToString("0.##", CultureInfo.InvariantCulture);
        CpuIdleFloorBox.Text = current.CpuIdleFloorWatts.ToString("0.##", CultureInfo.InvariantCulture);
        PollingIntervalBox.Text = current.PollingIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        RetentionBox.Text = current.RetentionDays.ToString(CultureInfo.InvariantCulture);

        Loaded += (_, _) => TariffBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseDouble(TariffBox.Text, out var tariff) || tariff <= 0)
        {
            ShowError("Tarif per kWh harus angka lebih besar dari 0.");
            return;
        }

        if (!TryParseDouble(CpuTdpBox.Text, out var cpuTdp) || cpuTdp <= 0)
        {
            ShowError("Daya maksimum CPU harus angka lebih besar dari 0.");
            return;
        }

        if (!TryParseDouble(CpuIdleFloorBox.Text, out var cpuIdleFloor) || cpuIdleFloor < 0)
        {
            ShowError("Daya idle CPU tidak boleh negatif.");
            return;
        }

        if (cpuIdleFloor >= cpuTdp)
        {
            ShowError("Daya idle harus lebih kecil dari daya maksimum.");
            return;
        }

        if (!int.TryParse(PollingIntervalBox.Text.Trim(), out var pollingInterval) || pollingInterval < 1)
        {
            ShowError("Interval polling minimal 1 detik.");
            return;
        }

        if (!int.TryParse(RetentionBox.Text.Trim(), out var retentionDays) || retentionDays < 0)
        {
            ShowError("Retensi data tidak boleh negatif (0 = tidak pernah dihapus).");
            return;
        }

        Result = new AppConfig
        {
            TariffPerKwh = tariff,
            CpuTdpWatts = cpuTdp,
            CpuIdleFloorWatts = cpuIdleFloor,
            PollingIntervalSeconds = pollingInterval,
            RetentionDays = retentionDays
        };
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static bool TryParseDouble(string text, out double value)
    {
        text = text.Trim();
        return double.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
