/// <summary>
/// Code-behind для LoginView.
/// Используется Avalonia 12.x для инициализации XAML-представления экрана входа.
/// Проект: DevAssistant / ContourAI.
/// </summary>
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContourAI.Features.Auth;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
