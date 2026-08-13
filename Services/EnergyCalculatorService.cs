namespace PowerMonitorApp.Services;

/// <summary>
/// Mengakumulasi pembacaan daya (watt) sepanjang waktu menjadi energi (kWh)
/// menggunakan integrasi trapesium sederhana: energi = daya x durasi.
/// </summary>
public class EnergyCalculatorService
{
    private double _accumulatedWattHours;
    private DateTime? _lastTimestamp;

    public double TotalKilowattHours => _accumulatedWattHours / 1000.0;

    public void AddReading(DateTime timestamp, double watts)
    {
        if (_lastTimestamp is not null)
        {
            var elapsedHours = (timestamp - _lastTimestamp.Value).TotalHours;
            if (elapsedHours > 0)
            {
                _accumulatedWattHours += watts * elapsedHours;
            }
        }
        _lastTimestamp = timestamp;
    }

    /// <summary>Reset akumulasi, misalnya untuk mulai periode penghitungan baru (harian/bulanan).</summary>
    public void Reset()
    {
        _accumulatedWattHours = 0;
        _lastTimestamp = null;
    }
}
