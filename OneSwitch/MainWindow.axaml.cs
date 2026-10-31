using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OneSwitch.ViewModels;

namespace OneSwitch;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ViewModel.IsDialogOpen)
        {
            ViewModel.CloseTopDialog();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void Headerbar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.ClickCount == 1)
            BeginMoveDrag(e);
    }

    private void Headerbar_DoubleTapped(object? sender, TappedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Scrim_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender))
            ViewModel.CloseTopDialog();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
