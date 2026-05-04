/// <summary>
/// Code-behind для DashboardView.
/// Используется Avalonia 12.x для загрузки XAML-представления dashboard.
/// Проект: DevAssistant / ContourAI.
/// </summary>
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContourAI.Features.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
