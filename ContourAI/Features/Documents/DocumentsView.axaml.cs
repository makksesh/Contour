using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContourAI.Features.Documents;

public partial class DocumentsView : UserControl
{
    public DocumentsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
