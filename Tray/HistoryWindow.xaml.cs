using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PowerMonitorApp.Services;

namespace PowerMonitorApp.Tray;

/// <summary>
/// Jendela riwayat pemakaian energi, dihitung dari histori tersimpan di
/// SQLite. Punya dua tab: harian (14 hari terakhir) dan bulanan (6 bulan
/// terakhir) — reuse kartu ringkasan/chart/list yang sama untuk keduanya.
/// </summary>
public partial class HistoryWindow : Window
{
    private const double ChartMaxBarHeight = 130;
    private const int DailyRangeDays = 14;
    private const int MonthlyRangeMonths = 6;

    private static readonly CultureInfo IdCulture = CultureInfo.GetCultureInfo("id-ID");
    private static readonly SolidColorBrush ActiveBrush = new(System.Windows.Media.Color.FromRgb(0x4F, 0xC3, 0xF7));
    private static readonly SolidColorBrush ActiveForeground = new(System.Windows.Media.Color.FromRgb(0x0A, 0x0A, 0x0A));
    private static readonly SolidColorBrush InactiveBrush = new(System.Windows.Media.Color.FromRgb(0x26, 0x26, 0x26));
    private static readonly SolidColorBrush InactiveForeground = new(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));

    private DatabaseService? _database;
    private CostCalculatorService? _costCalculator;

    public HistoryWindow()
    {
        InitializeComponent();
    }

    public void Initialize(DatabaseService database, CostCalculatorService costCalculator)
    {
        _database = database;
        _costCalculator = costCalculator;
        ShowDaily();
    }

    private void DailyTabButton_Click(object sender, RoutedEventArgs e) => ShowDaily();

    private void MonthlyTabButton_Click(object sender, RoutedEventArgs e) => ShowMonthly();

    private void ShowDaily()
    {
        SetActiveTab(isMonthly: false);
        ChartTitleText.Text = "TREN kWh PER HARI";
        AverageLabelText.Text = "RATA-RATA/HARI";

        var today = DateTime.Today;
        var periods = _database!.GetDailySummaries(DailyRangeDays)
            .Select(s => new PeriodRow(
                s.Date,
                s.TotalKwh,
                s.Date == today ? "Hari ini" : s.Date == today.AddDays(-1) ? "Kemarin" : s.Date.ToString("dddd, d MMM", IdCulture),
                s.Date.ToString("d/M", IdCulture),
                $"{s.Date.ToString("dddd, d MMM", IdCulture)} — {s.TotalKwh:F4} kWh"))
            .ToList();

        var rangeLabel = periods.Count switch
        {
            0 => "Belum ada data",
            1 => "Hari ini",
            _ => $"{periods.Count} hari · sejak {periods.Min(p => p.Date).ToString("d MMM yyyy", IdCulture)}"
        };

        Render(periods, rangeLabel);
    }

    private void ShowMonthly()
    {
        SetActiveTab(isMonthly: true);
        ChartTitleText.Text = "TREN kWh PER BULAN";
        AverageLabelText.Text = "RATA-RATA/BULAN";

        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var periods = _database!.GetMonthlySummaries(MonthlyRangeMonths)
            .Select(s => new PeriodRow(
                s.Month,
                s.TotalKwh,
                s.Month == thisMonth ? "Bulan ini" : s.Month.ToString("MMMM yyyy", IdCulture),
                s.Month.ToString("MMM", IdCulture),
                $"{s.Month.ToString("MMMM yyyy", IdCulture)} — {s.TotalKwh:F4} kWh"))
            .ToList();

        var rangeLabel = periods.Count switch
        {
            0 => "Belum ada data",
            1 => "Bulan ini",
            _ => $"{periods.Count} bulan · sejak {periods.Min(p => p.Date).ToString("MMMM yyyy", IdCulture)}"
        };

        Render(periods, rangeLabel);
    }

    private void Render(List<PeriodRow> periods, string rangeLabel)
    {
        SubHeaderText.Text = rangeLabel;

        if (periods.Count == 0)
        {
            SummaryCard.Visibility = Visibility.Collapsed;
            ChartCard.Visibility = Visibility.Collapsed;
            ListScrollViewer.Visibility = Visibility.Collapsed;
            EmptyStateText.Visibility = Visibility.Visible;
            return;
        }

        SummaryCard.Visibility = Visibility.Visible;
        ChartCard.Visibility = Visibility.Visible;
        ListScrollViewer.Visibility = Visibility.Visible;
        EmptyStateText.Visibility = Visibility.Collapsed;

        var totalKwh = periods.Sum(p => p.TotalKwh);
        TotalKwhText.Text = $"{totalKwh:F4}";
        TotalCostText.Text = $"Rp {_costCalculator!.CalculateCost(totalKwh):N2}";
        AverageKwhText.Text = $"{totalKwh / periods.Count:F4} kWh";

        HistoryItems.ItemsSource = periods
            .OrderByDescending(p => p.Date)
            .Select(p => new HistoryRow(p.ListLabel, $"{p.TotalKwh:F4} kWh", $"Rp {_costCalculator!.CalculateCost(p.TotalKwh):N2}"))
            .ToList();

        var maxKwh = Math.Max(periods.Max(p => p.TotalKwh), 0.0001);
        ChartItems.ItemsSource = periods
            .OrderBy(p => p.Date)
            .Select(p => new ChartBar(
                Math.Max(2, p.TotalKwh / maxKwh * ChartMaxBarHeight),
                p.ChartLabel,
                $"{p.TotalKwh:F2}",
                p.ChartTooltip))
            .ToList();
    }

    private void SetActiveTab(bool isMonthly)
    {
        DailyTabButton.Background = isMonthly ? InactiveBrush : ActiveBrush;
        DailyTabButton.Foreground = isMonthly ? InactiveForeground : ActiveForeground;
        MonthlyTabButton.Background = isMonthly ? ActiveBrush : InactiveBrush;
        MonthlyTabButton.Foreground = isMonthly ? ActiveForeground : InactiveForeground;
    }

    private record PeriodRow(DateTime Date, double TotalKwh, string ListLabel, string ChartLabel, string ChartTooltip);

    private record HistoryRow(string DateLabel, string KwhLabel, string CostLabel);

    private record ChartBar(double Height, string Label, string ValueLabel, string Tooltip);
}
