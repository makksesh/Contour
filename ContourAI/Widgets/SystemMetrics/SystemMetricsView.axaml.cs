using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContourAI.Widgets.SystemMetrics;

public partial class SystemMetricsView : UserControl
{
    public SystemMetricsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
