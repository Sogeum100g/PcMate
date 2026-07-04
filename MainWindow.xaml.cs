using System.Windows;
using System.Windows.Input;
using PcMate.Monitors;
using PcMate.Services;
using PcMate.ViewModels;

namespace PcMate;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(
            new MemoryMonitor(),
            new StateClassifier());

        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.Start();
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
