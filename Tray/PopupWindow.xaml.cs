using System.Windows;
using PowerMonitorApp.Models;

namespace PowerMonitorApp.Tray;

/// <summary>
/// Popup kecil yang muncul saat tray icon diklik: menampilkan watt CPU/GPU
/// saat ini, akumulasi kWh hari ini, dan biayanya. Tertutup otomatis saat
/// kehilangan fokus (klik di luar popup).
/// </summary>
public partial class PopupWindow : Window
{
    public event EventHandler? HistoryRequested;

    public PopupWindow()
    {
        InitializeComponent();
    }

    public void UpdateData(PowerReading reading, double kwhToday, double costToday, double tariffPerKwh)
    {
        CpuText.Text = reading.CpuPowerEstimated
            ? $"≈{reading.CpuPowerWatts:F1} W"
            : reading.CpuSensorAvailable ? $"{reading.CpuPowerWatts:F1} W" : "N/A";
        GpuText.Text = reading.GpuSensorAvailable ? $"{reading.GpuPowerWatts:F1} W" : "N/A";
        TotalText.Text = $"{reading.TotalPowerWatts:F1} W";
        KwhText.Text = $"{kwhToday:F4} kWh";
        CostText.Text = $"Rp {costToday:N2}";
        TariffText.Text = $"Tarif: Rp {tariffPerKwh:N2}/kWh";
        UpdatedText.Text = $"Update: {reading.Timestamp:HH:mm:ss}";
        EstimateNoteText.Visibility = reading.CpuPowerEstimated ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        HistoryRequested?.Invoke(this, EventArgs.Empty);
    }
}
