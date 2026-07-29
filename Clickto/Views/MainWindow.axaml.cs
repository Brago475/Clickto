using Avalonia.Controls;
using Clickto.ViewModels;

namespace Clickto.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The platform can override the bound size while the window is being
    /// created, so the saved size is re-applied once it is actually open.
    /// </summary>
    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainWindowViewModel vm)
            vm.OnWindowOpened();
    }
}
