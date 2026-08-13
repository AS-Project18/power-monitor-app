namespace PowerMonitorApp.Models;

/// <summary>
/// Konfigurasi aplikasi: tarif listrik dan interval polling sensor.
/// Disimpan/dibaca dari config.json lewat ConfigService.
/// </summary>
public class AppConfig
{
    public double TariffPerKwh { get; set; }
    public int PollingIntervalSeconds { get; set; }

    /// <summary>Berapa hari histori pembacaan disimpan sebelum dihapus otomatis. 0 = tidak pernah dihapus.</summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Perkiraan watt CPU saat core tersibuk mendekati 100% — dipakai sebagai
    /// batas atas estimasi fallback kalau sensor Power CPU tidak terbaca sama
    /// sekali (mis. dukungan SMU AMD yang belum lengkap di
    /// LibreHardwareMonitorLib untuk chip tertentu). Boleh lebih tinggi dari
    /// TDP resmi CPU kamu (mis. AMD AM5 sering boost sampai ~1.3x TDP-nya).
    /// </summary>
    public double CpuTdpWatts { get; set; } = 65;

    /// <summary>
    /// Perkiraan watt CPU saat benar-benar idle (SoC/IO die/memory controller
    /// tetap menyala walau semua core 0% load) — batas bawah estimasi fallback.
    /// </summary>
    public double CpuIdleFloorWatts { get; set; } = 15;
}
