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
