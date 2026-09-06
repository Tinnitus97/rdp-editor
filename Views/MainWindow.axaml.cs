using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using RdpEditor.ViewModels;

namespace RdpEditor.Views;

public partial class MainWindow : Window
{
    /// <summary>Steht auf true, sobald das Verwerfen bestätigt wurde.</summary>
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Klick auf eine Kachel im Monitorplan. Als Ereignis statt als Befehl,
    /// weil die ganze Kachel anklickbar sein soll und nicht nur ein Knopf
    /// darin.
    /// </summary>
    private void MonitorTile_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: MonitorTileViewModel tile })
            tile.Toggle();
    }

    /// <summary>
    /// Nicht gespeicherte Änderungen nicht wortlos wegwerfen. Die Abfrage
    /// läuft asynchron, deshalb wird das Schließen zunächst abgebrochen und
    /// nach dem Ja wiederholt.
    /// </summary>
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed) return;
        if (DataContext is not MainWindowViewModel vm || !vm.IsDirty) return;

        e.Cancel = true;

        Dispatcher.UIThread.Post(async () =>
        {
            if (!await vm.ConfirmDiscard()) return;
            _closeConfirmed = true;
            Close();
        });
    }
}
