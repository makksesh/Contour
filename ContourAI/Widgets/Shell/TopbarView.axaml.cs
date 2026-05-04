/// <summary>
/// Code-behind для TopbarView.
/// Используется Avalonia 12.x для загрузки XAML верхней панели authenticated shell.
/// Проект: DevAssistant / ContourAI.
/// </summary>
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContourAI.Widgets.Shell;

public partial class TopbarView : UserControl
{
    public TopbarView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
