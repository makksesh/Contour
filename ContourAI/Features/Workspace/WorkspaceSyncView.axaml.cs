using Avalonia.Controls;

namespace ContourAI.Features.Workspace;

public partial class WorkspaceSyncView : UserControl
{
    public WorkspaceSyncView()
    {
        InitializeComponent();
    }

    // Передаём TopLevel в команду через code-behind,
    // поскольку StorageProvider недоступен из XAML CommandParameter.
    private void OnBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (DataContext is WorkspaceSyncViewModel vm)
            vm.BrowseLocalPathCommand.Execute(topLevel);
    }
}
