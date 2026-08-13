namespace PowerMonitorApp.Models;

/// <summary>
/// Satu titik pembacaan daya pada waktu tertentu.
/// Hanya mencakup komponen yang melaporkan power draw-nya sendiri
/// (CPU package, GPU). Motherboard, storage, fan, dan efisiensi PSU
/// tidak tercakup di sini.
/// </summary>
public class PowerReading
{
    public DateTime Timestamp { get; set; }
    public double CpuPowerWatts { get; set; }
    public double GpuPowerWatts { get; set; }
    public double TotalPowerWatts => CpuPowerWatts + GpuPowerWatts;

    /// <summary>
    /// True kalau minimal satu sensor Power terbaca untuk CPU. Kalau false,
    /// CpuPowerWatts = 0 bukan berarti CPU idle — sensornya memang tidak
    /// terbaca (biasanya karena akses RAPL/MSR diblokir, mis. oleh Windows
    /// Memory Integrity / Core Isolation).
    /// </summary>
    public bool CpuSensorAvailable { get; set; }

    /// <summary>Sama seperti <see cref="CpuSensorAvailable"/> tapi untuk GPU.</summary>
    public bool GpuSensorAvailable { get; set; }

    /// <summary>
    /// True kalau CpuPowerWatts bukan hasil pengukuran sensor Power,
    /// melainkan estimasi dari CPU Load% x TDP (dipakai sebagai fallback
    /// waktu sensor Power CPU tidak terbaca).
    /// </summary>
    public bool CpuPowerEstimated { get; set; }
}
