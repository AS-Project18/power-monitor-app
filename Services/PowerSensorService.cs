using System.Text;
using LibreHardwareMonitor.Hardware;
using PowerMonitorApp.Models;

namespace PowerMonitorApp.Services;

/// <summary>
/// Membungkus LibreHardwareMonitorLib untuk membaca power draw CPU dan GPU
/// dari sensor internal hardware (RAPL/MSR untuk CPU, NVML/ADL untuk GPU, dll).
/// </summary>
public class PowerSensorService : IDisposable
{
    private readonly Computer _computer;

    /// <summary>
    /// Watt CPU saat core tersibuk mendekati 100% (dipakai sebagai batas atas
    /// estimasi fallback — lihat <see cref="CpuIdleFloorWattsForEstimate"/>).
    /// Bisa diubah kapan saja (mis. dari jendela Pengaturan) tanpa restart.
    /// </summary>
    public double CpuTdpWattsForEstimate { get; set; }

    /// <summary>
    /// Watt CPU saat benar-benar idle (SoC/IO die/memory controller tetap
    /// menyala walau semua core 0% load) — batas bawah estimasi fallback.
    /// </summary>
    public double CpuIdleFloorWattsForEstimate { get; set; }

    public PowerSensorService(double cpuTdpWattsForEstimate, double cpuIdleFloorWattsForEstimate)
    {
        CpuTdpWattsForEstimate = cpuTdpWattsForEstimate;
        CpuIdleFloorWattsForEstimate = cpuIdleFloorWattsForEstimate;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true
        };
        _computer.Open();
    }

    public PowerReading ReadCurrent()
    {
        double cpuWatts = 0;
        double gpuWatts = 0;
        var cpuSensorFound = false;
        var gpuSensorFound = false;
        var cpuEstimated = false;
        IHardware? cpuHardware = null;

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();

            if (hardware.HardwareType == HardwareType.Cpu)
            {
                cpuHardware = hardware;
                cpuWatts += SumPowerSensors(hardware, out var found);
                cpuSensorFound |= found;
            }
            else if (hardware.HardwareType is HardwareType.GpuNvidia
                                          or HardwareType.GpuAmd
                                          or HardwareType.GpuIntel)
            {
                gpuWatts += SumPowerSensors(hardware, out var found);
                gpuSensorFound |= found;
            }
        }

        // CPU yang menyala tidak pernah benar-benar 0 W. Kalau sensor Power-nya
        // tidak ada, atau ada tapi nyangkut di 0 (nilai HasValue=true tapi
        // selalu 0.00 — dukungan SMU belum lengkap untuk sebagian chip AMD),
        // fallback ke estimasi berbasis Load.
        //
        // Dasarnya pakai "CPU Core Max" (core paling sibuk), BUKAN "CPU Total"
        // (rata-rata semua thread) — soalnya workload sehari-hari sering cuma
        // nge-load 1-2 thread yang boost tinggi, sementara thread lain nganggur.
        // Rata-rata dari situ jadi kecil (mis. 6-10%) padahal daya rielnya udah
        // signifikan karena core yang aktif itu boost penuh. "Core Max" lebih
        // dekat merepresentasikan itu. Hasilnya diinterpolasi antara idle floor
        // (SoC/IO die yang selalu nyala) dan TDP (perkiraan daya saat core
        // tersibuk mendekati 100%).
        if (cpuHardware is not null && cpuWatts <= 0)
        {
            var loadPercent = FindLoadSensor(cpuHardware, "CPU Core Max")
                ?? FindLoadSensor(cpuHardware, "CPU Total");

            if (loadPercent.HasValue)
            {
                var idleFloor = Math.Min(CpuIdleFloorWattsForEstimate, CpuTdpWattsForEstimate);
                cpuWatts = idleFloor + loadPercent.Value / 100.0 * (CpuTdpWattsForEstimate - idleFloor);
                cpuSensorFound = false;
                cpuEstimated = true;
            }
        }

        return new PowerReading
        {
            Timestamp = DateTime.Now,
            CpuPowerWatts = cpuWatts,
            GpuPowerWatts = gpuWatts,
            CpuSensorAvailable = cpuSensorFound,
            GpuSensorAvailable = gpuSensorFound,
            CpuPowerEstimated = cpuEstimated
        };
    }

    private static double? FindLoadSensor(IHardware hardware, string sensorName)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Load && sensor.Name == sensorName && sensor.Value.HasValue)
            {
                return sensor.Value.Value;
            }
        }
        return null;
    }

    private static double SumPowerSensors(IHardware hardware, out bool sensorFound)
    {
        // Beberapa hardware melaporkan lebih dari satu power rail
        // (mis. "CPU Package", "CPU Cores", "CPU Memory" terpisah).
        // Ambil nilai tertinggi sebagai estimasi power total komponen tsb,
        // supaya tidak double-count rail yang overlap.
        double maxReading = 0;
        sensorFound = false;

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue)
            {
                sensorFound = true;
                maxReading = Math.Max(maxReading, sensor.Value.Value);
            }
        }

        // Sebagian mainboard menaruh sensor power di sub-hardware (mis. GPU
        // yang terdaftar sebagai "SubHardware" pada beberapa laptop).
        foreach (var sub in hardware.SubHardware)
        {
            sub.Update();
            maxReading += SumPowerSensors(sub, out var subFound);
            sensorFound |= subFound;
        }

        return maxReading;
    }

    /// <summary>
    /// Dump semua hardware dan sensor yang terdeteksi LibreHardwareMonitorLib,
    /// beserta nilainya saat ini. Dipakai untuk diagnosa kalau ada komponen
    /// yang pembacaan wattnya 0/tidak masuk akal — supaya kelihatan apakah
    /// sensor Power-nya memang tidak terdeteksi sama sekali atau nilainya
    /// null (bukan cuma "0.0 W" yang ambigu).
    /// </summary>
    public string DumpSensors()
    {
        var sb = new StringBuilder();
        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            AppendHardware(sb, hardware, 0);
        }
        return sb.ToString();
    }

    private static void AppendHardware(StringBuilder sb, IHardware hardware, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}[{hardware.HardwareType}] {hardware.Name}");

        foreach (var sensor in hardware.Sensors)
        {
            var value = sensor.Value.HasValue ? sensor.Value.Value.ToString("F2") : "null";
            sb.AppendLine($"{indent}  - {sensor.SensorType,-10} {sensor.Name} = {value}");
        }

        foreach (var sub in hardware.SubHardware)
        {
            sub.Update();
            AppendHardware(sb, sub, depth + 1);
        }
    }

    public void Dispose()
    {
        _computer.Close();
        GC.SuppressFinalize(this);
    }
}
