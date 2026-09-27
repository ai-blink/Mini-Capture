using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IOPath = System.IO.Path;
using WpfBinding = System.Windows.Data.Binding;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfColor = System.Windows.Media.Color;
using WpfCursors = System.Windows.Input.Cursors;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfPath = System.Windows.Shapes.Path;
using WpfPen = System.Windows.Media.Pen;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfShape = System.Windows.Shapes.Shape;
using MiniCapture.Resources;

namespace MiniCapture;

public partial class ViewerWindow
{
    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        SetZoom(_zoom / ZoomStep);
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        SetZoom(_zoom * ZoomStep);
    }

    private void OnFitClick(object sender, RoutedEventArgs e)
    {
        _fitMode = true;
        FitToStage();
        ScheduleCurrentImageViewStateSave();
    }

    private void OnActualSizeClick(object sender, RoutedEventArgs e)
    {
        SetZoom(1.0);
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await RefreshCurrentFolderAsync();
    }

    private async Task RefreshCurrentFolderAsync()
    {
        var folderPath = _currentFolderPath;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            await RefreshIndexAsync();
            return;
        }

        var load = StartNewLoad();
        var stopwatch = Stopwatch.StartNew();
        var preferredPath = (FileList.SelectedItem as CaptureImageFile)?.Path;
        if (string.IsNullOrWhiteSpace(preferredPath) &&
            IsSameFolderSelection(folderPath, _currentFile?.FolderPath))
        {
            preferredPath = _currentFile?.Path;
        }

        SetViewerStatus(Strings.Viewer_Status_RefreshingFolder);

        try
        {
            await LoadFolderAsync(folderPath, preferredPath, load, stopwatch);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowFolderLoadFailure(ex);
        }
    }

    private void OnViewModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag } ||
            !Enum.TryParse<ExplorerViewMode>(tag, out var mode))
        {
            return;
        }

        if (mode != ExplorerViewMode.Details && RequiresDetailsView(_activeFolderFileCount))
        {
            var scope = _isExternalFolder
                ? Strings.Viewer_ExternalFolder
                : string.Format(Strings.Viewer_FileCountFiles, _activeFolderFileCount);
            SetViewerStatus(string.Format(Strings.Viewer_Status_DetailsOnly, scope));
            return;
        }

        SetViewMode(mode);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null)
        {
            SetViewerStatus(Strings.Viewer_Status_NothingToSave);
            return;
        }

        var targetPath = GetSaveTargetPath();
        if (targetPath is null)
        {
            SetViewerStatus(Strings.Viewer_Status_NoSaveTarget);
            return;
        }

        try
        {
            var exportedImage = ComposeImageForExport();
            SavePng(exportedImage, targetPath);
            ApplySavedFileState(targetPath);
            _isDirty = false;
            UpdateDirtyIndicator();
            UpdateNavigationButtons();
            SetViewerStatus(string.Format(Strings.Viewer_Status_Saved, targetPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            SetViewerStatus(string.Format(Strings.Viewer_Status_SaveFailed, ex.Message));
        }
    }

    private void ApplySavedFileState(string targetPath)
    {
        if (!CaptureFileIndex.TryCreateImageFile(targetPath, out var savedFile) || savedFile is null)
        {
            return;
        }

        var fileToSelect = FindFile(savedFile.Path);
        var matchesCurrentFile = PathsEqual(_currentFile?.Path, savedFile.Path);

        if (fileToSelect is not null)
        {
            fileToSelect.RefreshFromDisk();
            _isLoadingSelection = true;
            MoveFileToSortedPosition(fileToSelect);
            FileList.SelectedItem = fileToSelect;
            FileList.ScrollIntoView(fileToSelect);
            _isLoadingSelection = false;
            _currentFile = fileToSelect;
        }
        else if (IsSameFolderSelection(_currentFolderPath, savedFile.FolderPath))
        {
            fileToSelect = savedFile;
            _isLoadingSelection = true;
            _files.Insert(GetSortedFileInsertIndex(fileToSelect), fileToSelect);
            FileList.SelectedItem = fileToSelect;
            FileList.ScrollIntoView(fileToSelect);
            _isLoadingSelection = false;
            _currentFile = fileToSelect;
            FileCountText.Text = string.Format(Strings.Viewer_FileCount, _files.Count);
        }
        else if (matchesCurrentFile && _currentFile is not null)
        {
            _currentFile.RefreshFromDisk();
        }
        else
        {
            _isLoadingSelection = true;
            FileList.SelectedItem = null;
            _isLoadingSelection = false;
            _currentFile = savedFile;
        }

        _lastSavedPath = savedFile.Path;
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        var path = GetActiveFilePath();
        if (path is null || !File.Exists(path))
        {
            SetViewerStatus(Strings.Viewer_Status_NothingToOpen);
            return;
        }

        ShellService.OpenFile(path);
        SetViewerStatus(string.Format(Strings.Viewer_Status_Opened, path));
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var path = GetActiveFilePath();
        if (path is null)
        {
            ShellService.OpenFile(CaptureFileIndex.RootDirectory);
            SetViewerStatus(string.Format(Strings.Viewer_Status_FolderOpened, CaptureFileIndex.RootDirectory));
            return;
        }

        ShellService.OpenContainingFolder(path);
        SetViewerStatus(string.Format(Strings.Viewer_Status_FolderOpened, path));
    }

    private async void OnCopyPathClick(object sender, RoutedEventArgs e)
    {
        var path = GetActiveFilePath();
        if (path is null)
        {
            SetViewerStatus(Strings.Viewer_Status_NoPathToCopy);
            return;
        }

        try
        {
            await ClipboardService.SetTextAsync(path);
            SetViewerStatus(Strings.Main_Status_PathCopied);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetViewerStatus(string.Format(Strings.Viewer_Status_ClipboardCopyFailed, ex.Message));
        }
    }

    private void OnCopyImageClick(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null)
        {
            SetViewerStatus(Strings.Viewer_Status_NoImageToCopy);
            return;
        }

        try
        {
            ClipboardService.SetImage(ComposeImageForExport());
            SetViewerStatus(Strings.Main_Status_ImageCopied);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            SetViewerStatus(string.Format(Strings.Main_Status_ImageCopyFailed, ex.Message));
        }
    }

    private string? GetSaveTargetPath()
    {
        var sourcePath = _currentFile?.Path ?? _lastSavedPath;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        if (string.Equals(IOPath.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        var folder = IOPath.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = CaptureFileIndex.RootDirectory;
        }

        return IOPath.Combine(folder, $"{IOPath.GetFileNameWithoutExtension(sourcePath)}_edited.png");
    }

    private string? GetActiveFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_lastSavedPath) && File.Exists(_lastSavedPath))
        {
            return _lastSavedPath;
        }

        return _currentFile?.Path;
    }

    private static void SavePng(BitmapSource source, string targetPath)
    {
        var folder = IOPath.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var tempPath = IOPath.Combine(folder ?? CaptureFileIndex.RootDirectory, $"{IOPath.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = File.Open(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(stream);
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
