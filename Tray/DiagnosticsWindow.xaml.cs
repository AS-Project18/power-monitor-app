using System.Windows;

namespace PowerMonitorApp.Tray;

/// <summary>Jendela dump sensor mentah dari LibreHardwareMonitorLib, untuk troubleshoot pembacaan yang aneh (mis. CPU selalu 0 W).</summary>
public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow()
    {
        InitializeComponent();
    }

    public void LoadData(string dump)
    {
        DumpText.Text = dump;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(DumpText.Text);
    }
}
