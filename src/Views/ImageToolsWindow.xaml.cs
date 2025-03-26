using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using Skatech.Components.Presentation;
using System.Windows.Media;
using System.Windows.Input;
using System.Threading.Tasks;
using Skatech.IO;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace Skatech.Euphoria;

partial class ImageToolsWindow : Window {
    readonly ImageToolsWindowController Controller;

    internal ImageToolsWindow(Window owner) {
        InitializeComponent();
        Owner = owner;
        DataContext = Controller = new ImageToolsWindowController();
        Components.Presentation.WindowBoundsKeeper.Register(this,
            $"{nameof(ImageToolsWindow)}Bounds", new Size(650, 750));
    }

    void OnSelectSource(object sender, RoutedEventArgs e) {
        var dialog = new Microsoft.Win32.OpenFileDialog() {
            Filter = String.Format(
                "Image or archive files (*{0};*{1})|*{0};*{1}|Image files (*{0})|*{0}|Archive files (*{1})|*{1}",
                ImageLocator.ImageFileExtension, ImageLocator.ArchiveFileExtension),
            InitialDirectory = Path.GetDirectoryName(Controller.Source) };
        if (dialog.ShowDialog() is true && dialog.FileName is string file) {
            Controller.Source = file;
        }
    }

    void OnPerformOperation(object sender, RoutedEventArgs e) {
        Controller.PerformOperation();
    }

    void OnKeyUp(object sender, KeyEventArgs e) {
        if (e.Handled = e.Key == Key.Escape)
            Close();
    }
}

class ImageToolsWindowController : LockableControllerBase {
    string _source = String.Empty;
    public string Source {
        get => _source;
        set {
            if (VisualCheckSourceValid(value) && TryUpdateField(ref _source, value))
                OnUpdateSource();
        }
    }

    public string Output { get; private set; } = String.Empty;

    public void PerformOperation() {
        if (LockMessage is null && String.IsNullOrEmpty(_source) is false) {
            if (FilePath.IsExtensionEqual(_source, ImageLocator.ArchiveFileExtension)) {
                async Task ExtractArchive(string arc) {
                    WriteOutput("Performing... ", false);
                    await Task.Delay(100).ConfigureAwait(false);
                    var sw = Stopwatch.StartNew();
                    try {
                        ImageArchive.UnwrapImageArchive(arc);
                        sw.Stop();
                        WriteOutput($"Image archive extracted in {sw.Elapsed.TotalSeconds:F2}s");
                    }
                    catch (Exception ex){
                        sw.Stop();
                        WriteOutput($"Image archive extraction failed: \"{ex.Message}\"");
                        LockWithErrorMessage(ex.Message);
                    }
                }
                LockUntilComplete(ExtractArchive(_source), "Extracting image archive...");
            }
            else {
                string selector = FilePath.ReplaceFileName(_source,
                    $"{Path.GetFileNameWithoutExtension(_source.AsSpan())}*{Path.GetExtension(_source.AsSpan())}");
                async Task CreateArchive(string sel) {
                    WriteOutput("Performing... ", false);
                    await Task.Delay(100).ConfigureAwait(false);
                    var sw = Stopwatch.StartNew();
                    try {
                        ImageArchive.CreateImageArchive(sel);
                        sw.Stop();
                        WriteOutput($"Image archive created in {sw.Elapsed.TotalSeconds:F2}s");
                    }
                    catch (Exception ex){
                        sw.Stop();
                        WriteOutput($"Image archive creation failed: \"{ex.Message}\"");
                        LockWithErrorMessage(ex.Message);
                    }
                }
                LockUntilComplete(CreateArchive(selector), "Creating image archive...");
            }
        }
    }

    void WriteOutput(string? text = default, bool writeLine = true) {
        Output += text ?? String.Empty;
        if (writeLine)
            Output += Environment.NewLine;
        OnPropertyChanged(nameof(Output));
    }

    void OnUpdateSource() {
        if (Output.Length > 0)
            WriteOutput();
        if (FilePath.IsExtensionEqual(_source, ImageLocator.ArchiveFileExtension)) {
            WriteOutput($"Ready to extract image archive: {_source}");
        }
        else {
            string selector = FilePath.ReplaceFileName(_source,
                $"{Path.GetFileNameWithoutExtension(_source.AsSpan())}*{Path.GetExtension(_source.AsSpan())}");
            if (Output.Length > 0)
                WriteOutput();
            WriteOutput(ImageArchive.GetImageArchiveCreationInfo(selector));
            WriteOutput("Ready to create image archive");
        }
    }

    bool VisualCheckSourceValid(string source) {
        if (FilePath.IsExtensionEqual(source, ImageLocator.ArchiveFileExtension)
                || FilePath.IsExtensionEqual(source, ImageLocator.ImageFileExtension)) {
            var fnm = Path.GetFileNameWithoutExtension(source);
            var loc = new ImageLocator(fnm);
            if (loc.IsValid && fnm.IndexOf(' ') < 0)
                return true;
            LockWithErrorMessage(loc.IsValid ? "Attributed file name" : "Invalid file name");
        }
        else LockWithErrorMessage("Invalid file type");
        return false;
    }
}