using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace ContourAI.Features.Chat;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        AvaloniaXamlLoader.Load(this);

        // Подписываемся на Messages как только DataContext становится ChatViewModel
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ChatViewModel vm)
            {
                vm.Messages.CollectionChanged += OnMessagesChanged;
            }
        };
    }

    // ── Автоскролл ───────────────────────────────────────────────────────────

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // Откладываем до следующего фрейма Avalonia UI,
        // чтобы сцена успела отрисовать новый элемент перед скроллом.
        Dispatcher.UIThread.Post(() =>
        {
            var scroll = this.FindControl<ScrollViewer>("MessagesScroll");
            scroll?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    // ── Треды ─────────────────────────────────────────────────────────────────

    private void ThreadItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ChatThreadItemViewModel vm })
            vm.RaiseSelected();
    }

    private void ThreadDelete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ChatThreadItemViewModel vm })
            vm.RaiseDeleteRequested();
        e.Handled = true;
    }

    // ── Поле ввода ────────────────────────────────────────────────────────────

    private void Input_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        if (DataContext is ChatViewModel vm)
            vm.SendCommand.Execute(null);
        e.Handled = true;
    }
}
