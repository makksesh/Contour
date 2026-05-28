/// <summary>
/// Code-behind для SidebarView.
/// Используется Avalonia 12.x для загрузки XAML виджета боковой навигации.
/// Проект: DevAssistant / ContourAI.
/// </summary>
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ContourAI.Features.Chat;

namespace ContourAI.Widgets.Shell;

public partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void GlobalChatItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        if (sender is not Control { DataContext: ChatThreadItemViewModel vm } || !vm.IsGlobal)
            return;

        vm.RaiseDeleteRequested();
        e.Handled = true;
    }
}
