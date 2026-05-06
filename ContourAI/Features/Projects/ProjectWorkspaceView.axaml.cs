using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;  // для StorageProvider
using System.Linq;

namespace ContourAI.Features.Projects;

public partial class ProjectWorkspaceView : UserControl
{
    public ProjectWorkspaceView()
    {
        AvaloniaXamlLoader.Load(this);

        // Подписываемся на смену DataContext — он может прийти позже
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ProjectWorkspaceViewModel vm)
                BindFilePicker(vm);
        };
    }

    private void BindFilePicker(ProjectWorkspaceViewModel vm)
    {
        vm.DocumentsViewModel.PickFileAsync = async () =>
        {
            // Поднимаемся до ближайшего Window
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null) return null;

            var files = await window.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title         = "Select a document",
                    AllowMultiple = false
                });

            return files.FirstOrDefault()?.TryGetLocalPath();
        };
    }
}