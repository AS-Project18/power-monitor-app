namespace PowerMonitorApp.Services;

/// <summary>
/// Menghitung biaya listrik dari total kWh berdasarkan tarif per kWh.
/// Untuk perhitungan yang lebih presisi sesuai golongan tarif PLN
/// (blok tarif progresif, biaya beban, dll), logic ini bisa diperluas
/// jadi tiered pricing.
/// </summary>
public class CostCalculatorService
{
    private readonly double _tariffPerKwh;

    public CostCalculatorService(double tariffPerKwh)
    {
        _tariffPerKwh = tariffPerKwh;
    }

    public double CalculateCost(double kilowattHours) => kilowattHours * _tariffPerKwh;
}
