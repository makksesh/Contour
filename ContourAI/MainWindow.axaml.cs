/// <summary>
/// Главное окно приложения.
/// Отображает текущий auth-экран через ContentControl и DataTemplates Avalonia.
/// Проект: DevAssistant / ContourAI.
/// </summary>
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContourAI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
