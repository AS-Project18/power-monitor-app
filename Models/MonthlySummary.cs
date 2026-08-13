namespace PowerMonitorApp.Models;

/// <summary>Rekap konsumsi energi untuk satu bulan kalender.</summary>
public class MonthlySummary
{
    public DateTime Month { get; set; }
    public double TotalKwh { get; set; }
}
