namespace PowerMonitorApp.Models;

/// <summary>Rekap konsumsi energi untuk satu hari kalender.</summary>
public class DailySummary
{
    public DateTime Date { get; set; }
    public double TotalKwh { get; set; }
}
