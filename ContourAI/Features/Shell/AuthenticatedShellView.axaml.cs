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
