using Microsoft.Data.Sqlite;
using PowerMonitorApp.Models;

namespace PowerMonitorApp.Services;

/// <summary>
/// Menyimpan histori pembacaan daya ke SQLite lokal, supaya bisa
/// direkap per hari/minggu/bulan tanpa perlu database server terpisah.
/// </summary>
public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string dbPath = "power_monitor.db")
    {
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS power_readings (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            cpu_watts REAL NOT NULL,
            gpu_watts REAL NOT NULL,
            total_watts REAL NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_power_readings_timestamp ON power_readings(timestamp);
        """;
        command.ExecuteNonQuery();
    }

    public void InsertReading(PowerReading reading)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        INSERT INTO power_readings (timestamp, cpu_watts, gpu_watts, total_watts)
        VALUES ($timestamp, $cpu, $gpu, $total);
        """;
        command.Parameters.AddWithValue("$timestamp", reading.Timestamp.ToString("O"));
        command.Parameters.AddWithValue("$cpu", reading.CpuPowerWatts);
        command.Parameters.AddWithValue("$gpu", reading.GpuPowerWatts);
        command.Parameters.AddWithValue("$total", reading.TotalPowerWatts);
        command.ExecuteNonQuery();
    }

    /// <summary>Total kWh terakumulasi dari seluruh histori (integrasi ulang dari data tersimpan).</summary>
    public double GetTotalKwhSince(DateTime since)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT timestamp, total_watts FROM power_readings
        WHERE timestamp >= $since
        ORDER BY timestamp ASC;
        """;
        command.Parameters.AddWithValue("$since", since.ToString("O"));

        using var reader = command.ExecuteReader();
        DateTime? last = null;
        double wattHours = 0;

        while (reader.Read())
        {
            var ts = DateTime.Parse(reader.GetString(0));
            var watts = reader.GetDouble(1);

            if (last is not null)
            {
                var hours = (ts - last.Value).TotalHours;
                if (hours > 0) wattHours += watts * hours;
            }
            last = ts;
        }

        return wattHours / 1000.0;
    }

    /// <summary>Tanggal pembacaan paling awal yang tersimpan, atau null kalau belum ada data sama sekali.</summary>
    public DateTime? GetEarliestReadingDate()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT MIN(timestamp) FROM power_readings;";
        var result = command.ExecuteScalar();

        return result is string text ? DateTime.Parse(text).Date : null;
    }

    /// <summary>
    /// Rekap kWh per hari kalender untuk maksimal N hari terakhir (termasuk
    /// hari ini), urut dari yang paling baru. Dibatasi ke tanggal pembacaan
    /// paling awal kalau app baru dipakai kurang dari N hari — supaya tidak
    /// ada hari "0 kWh" palsu untuk masa sebelum app pernah dijalankan.
    /// </summary>
    public IReadOnlyList<DailySummary> GetDailySummaries(int days)
    {
        var earliestReading = GetEarliestReadingDate();
        if (earliestReading is null)
        {
            return Array.Empty<DailySummary>();
        }

        var windowSince = DateTime.Today.AddDays(-(days - 1));
        var since = earliestReading.Value > windowSince ? earliestReading.Value : windowSince;
        var buckets = GetBucketedKwh(since, ts => ts.Date);

        var result = new List<DailySummary>();
        for (var day = since; day <= DateTime.Today; day = day.AddDays(1))
        {
            result.Add(new DailySummary { Date = day, TotalKwh = buckets.GetValueOrDefault(day) });
        }

        result.Reverse();
        return result;
    }

    /// <summary>
    /// Rekap kWh per bulan kalender untuk maksimal N bulan terakhir (termasuk
    /// bulan ini), urut dari yang paling baru. Dibatasi ke bulan pembacaan
    /// paling awal kalau app baru dipakai kurang dari N bulan.
    /// </summary>
    public IReadOnlyList<MonthlySummary> GetMonthlySummaries(int months)
    {
        var earliestReading = GetEarliestReadingDate();
        if (earliestReading is null)
        {
            return Array.Empty<MonthlySummary>();
        }

        var thisMonthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var windowSince = thisMonthStart.AddMonths(-(months - 1));
        var earliestMonthStart = new DateTime(earliestReading.Value.Year, earliestReading.Value.Month, 1);
        var since = earliestMonthStart > windowSince ? earliestMonthStart : windowSince;
        var buckets = GetBucketedKwh(since, ts => new DateTime(ts.Year, ts.Month, 1));

        var result = new List<MonthlySummary>();
        for (var month = since; month <= thisMonthStart; month = month.AddMonths(1))
        {
            result.Add(new MonthlySummary { Month = month, TotalKwh = buckets.GetValueOrDefault(month) });
        }

        result.Reverse();
        return result;
    }

    /// <summary>
    /// Ambil pembacaan sejak <paramref name="since"/> dan integrasikan (trapesium)
    /// jadi kWh, dikelompokkan per bucket waktu lewat <paramref name="bucketSelector"/>
    /// (mis. per hari atau per bulan). Integrasi di-reset tiap pergantian bucket,
    /// jadi watt-hour tidak "bocor" ke bucket sebelum/sesudahnya.
    /// </summary>
    private Dictionary<DateTime, double> GetBucketedKwh(DateTime since, Func<DateTime, DateTime> bucketSelector)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT timestamp, total_watts FROM power_readings
        WHERE timestamp >= $since
        ORDER BY timestamp ASC;
        """;
        command.Parameters.AddWithValue("$since", since.ToString("O"));

        var wattHoursByBucket = new Dictionary<DateTime, double>();
        using var reader = command.ExecuteReader();
        DateTime? lastTimestamp = null;
        DateTime? lastBucket = null;

        while (reader.Read())
        {
            var ts = DateTime.Parse(reader.GetString(0));
            var watts = reader.GetDouble(1);
            var bucket = bucketSelector(ts);

            if (lastTimestamp is not null && lastBucket == bucket)
            {
                var hours = (ts - lastTimestamp.Value).TotalHours;
                if (hours > 0)
                {
                    wattHoursByBucket[bucket] = wattHoursByBucket.GetValueOrDefault(bucket) + watts * hours;
                }
            }

            lastTimestamp = ts;
            lastBucket = bucket;
        }

        return wattHoursByBucket.ToDictionary(kv => kv.Key, kv => kv.Value / 1000.0);
    }

    /// <summary>Hapus pembacaan yang lebih lama dari <paramref name="cutoff"/>, untuk membatasi ukuran database.</summary>
    public void PurgeOlderThan(DateTime cutoff)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM power_readings WHERE timestamp < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        command.ExecuteNonQuery();
    }
}
