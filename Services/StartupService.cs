using System.Diagnostics;

namespace PowerMonitorApp.Services;

/// <summary>
/// Mendaftarkan/menghapus scheduled task Windows supaya aplikasi otomatis
/// jalan saat user login. Pakai Task Scheduler (bukan registry Run key)
/// dengan RunLevel Highest, supaya app yang butuh admin ini bisa start
/// otomatis tanpa prompt UAC tiap login.
/// </summary>
public static class StartupService
{
    private const string TaskName = "PowerMonitorApp";

    public static bool IsEnabled()
    {
        return RunSchTasks($"/query /tn \"{TaskName}\"") == 0;
    }

    public static void Enable()
    {
        var exePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Tidak bisa menentukan lokasi executable.");

        var exitCode = RunSchTasks(
            $"/create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /rl highest /f");

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"schtasks gagal membuat task (exit code {exitCode}).");
        }
    }

    public static void Disable()
    {
        var exitCode = RunSchTasks($"/delete /tn \"{TaskName}\" /f");
        if (exitCode != 0 && exitCode != 1)
        {
            throw new InvalidOperationException($"schtasks gagal menghapus task (exit code {exitCode}).");
        }
    }

    private static int RunSchTasks(string arguments)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Tidak bisa menjalankan schtasks.exe.");
        process.WaitForExit();
        return process.ExitCode;
    }
}
