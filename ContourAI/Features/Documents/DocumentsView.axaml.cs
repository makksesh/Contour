using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ContourAI.Features.Documents;

public partial class DocumentsView : UserControl
{
    public DocumentsView()
    {
        AvaloniaXamlLoader.Load(this);

        // Подписываемся на Click после загрузки XAML
        var uploadBtn = this.FindControl<Button>("UploadButton");
        if (uploadBtn != null)
            uploadBtn.Click += OnUploadClick;
    }

    /// <summary>
    /// Получает TopLevel (доступ к StorageProvider) и передаёт его в UploadCommand ViewModel.
    /// </summary>
    private void OnUploadClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DocumentsViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (vm.UploadCommand.CanExecute(topLevel))
            vm.UploadCommand.Execute(topLevel);
    }
}
