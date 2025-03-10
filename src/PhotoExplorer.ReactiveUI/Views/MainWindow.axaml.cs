using Avalonia.ReactiveUI;
using PhotoExplorer.ReactiveUI.ViewModels;

namespace PhotoExplorer.ReactiveUI.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}