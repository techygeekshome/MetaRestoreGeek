using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MetaRestoreGeek.ViewModels;
using TechyGeeksHome.Common;

namespace MetaRestoreGeek.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        DataContextChanged += (_, _) => Wire();
        Wire();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void Wire()
    {
        if (Vm is null) return;
        Vm.AboutRequested += () => new AboutWindow(AppFacts.Info).ShowDialog(this);
        Vm.RequestOpenFolderDialog = PickFolderAsync;
        Vm.RequestRevealInExplorer = RevealInExplorer;
    }

    // ------------------------------------------------------------------ drag and drop

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is null) return;
        var items = e.Data.GetFiles();
        // A folder dropped from Explorer arrives as a storage item whose local path is a
        // directory; a file dropped by mistake is pointed at its containing folder instead of
        // being rejected outright, since that is almost certainly what the person meant.
        var path = items?.Select(i => i.TryGetLocalPath()).FirstOrDefault(p => p is not null);
        if (path is null) return;

        var folder = Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path);
        if (folder is not null)
            Vm.LoadFolder(folder);
    }

    // ------------------------------------------------------------------ folder picker

    private async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose your Takeout \"Google Photos\" folder",
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    // ------------------------------------------------------------------ reveal in explorer

    private static void RevealInExplorer(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start("explorer.exe", $"\"{path}\"");
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", $"\"{path}\"");
            else
                Process.Start("xdg-open", $"\"{path}\"");
        }
        catch (Exception ex)
        {
            Log.Write($"Reveal in explorer failed: {ex.Message}");
        }
    }

    private void OnRevealResult(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Vm?.RevealResult();
}
