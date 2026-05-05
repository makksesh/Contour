/// <summary>
/// Code-behind ChatView.
/// Обрабатывает PointerPressed на элементах треда и Enter в поле ввода.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace ContourAI.Features.Chat;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ThreadItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ChatThreadItemViewModel vm })
            vm.Select();
    }

    private void ThreadDelete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ChatThreadItemViewModel vm })
            vm.Delete();
        e.Handled = true;
    }

    private void Input_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is ChatViewModel vm)
            vm.SendCommand.Execute(null);
        e.Handled = true;
    }
}
