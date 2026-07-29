using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
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
    /// File dialogs are also handed to the ViewModel here, since they need
    /// a live window to attach to.
    /// </summary>
    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is not MainWindowViewModel vm) return;

        vm.RequestSavePath = SavePathAsync;
        vm.RequestOpenPath = OpenPathAsync;
        vm.OnWindowOpened();
    }

    private static IReadOnlyList<FilePickerFileType> SequenceTypes { get; } = new[]
    {
        new FilePickerFileType("Clickto sequence")
        {
            Patterns = new[] { "*.json" }
        }
    };

    private async Task<string?> SavePathAsync(string suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export sequence",
            SuggestedFileName = suggestedName,
            DefaultExtension = "json",
            FileTypeChoices = SequenceTypes
        });

        return file?.Path.LocalPath;
    }

    private async Task<string?> OpenPathAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import sequence",
            AllowMultiple = false,
            FileTypeFilter = SequenceTypes
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
