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
