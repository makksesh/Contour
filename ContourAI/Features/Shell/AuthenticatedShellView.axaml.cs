/// <summary>
/// Code-behind для AuthenticatedShellView.
/// Используется Avalonia 12.x для загрузки XAML авторизованного shell-экрана.
/// Проект: DevAssistant / ContourAI.
/// </summary>
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContourAI.Features.Shell;

public partial class AuthenticatedShellView : UserControl
{
    public AuthenticatedShellView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
