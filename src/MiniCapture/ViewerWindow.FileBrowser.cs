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
using WpfClipboard = System.Windows.Clipboard;
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

namespace MiniCapture;

public partial class ViewerWindow
{
    private async Task RefreshIndexAsync()
    {
        var load = StartNewLoad();
        var stopwatch = Stopwatch.StartNew();
        var requestedPath = _pendingPath;
        _pendingPath = null;
        SetViewerStatus("캡처 라이브러리를 읽는 중입니다.");

        try
        {
            var snapshot = await Task.Run(() => BuildIndexSnapshot(requestedPath), load.Token);
            if (!IsCurrentLoad(load))
            {
                return;
            }

            _folderNodes = snapshot.FolderNodes;
            FolderTree.ItemsSource = _folderNodes;
            SelectFolderPath(snapshot.TargetFolder);
            await LoadFolderAsync(snapshot.TargetFolder, snapshot.TargetPath, load, stopwatch);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowFolderLoadFailure(ex);
        }
    }

    private static ViewerIndexSnapshot BuildIndexSnapshot(string? requestedPath)
    {
        var targetPath = ResolveTargetPath(requestedPath);
        var targetFolder = GetTargetFolder(targetPath);
        CaptureFileIndex.TryCreateImageFile(targetPath, out var knownLatest);
        var folderNodes = CaptureFileIndex.BuildFolderTree(targetFolder, knownLatest);
        return new ViewerIndexSnapshot(folderNodes, targetPath, targetFolder);
    }

    private static string? ResolveTargetPath(string? requestedPath)
    {
        if (CaptureFileIndex.IsImagePath(requestedPath))
        {
            return requestedPath;
        }

        return CaptureFileIndex.GetLatestImage()?.Path;
    }

    private static string GetTargetFolder(string? targetPath)
    {
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            var folder = IOPath.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                try
                {
                    return IOPath.GetFullPath(folder);
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }
        }

        return CaptureFileIndex.RootDirectory;
    }

    private async Task LoadFolderAsync(
        string folderPath,
        string? preferredPath,
        CancellationTokenSource load,
        Stopwatch? existingStopwatch = null)
    {
        var stopwatch = existingStopwatch ?? Stopwatch.StartNew();
        _currentFolderPath = folderPath;
        SetViewerStatus("폴더를 읽는 중입니다.");
        FileCountText.Text = "읽는 중...";

        IReadOnlyList<CaptureImageFile> files;
        var requestedSortColumn = _fileSortColumn;
        var requestedSortDirection = _fileSortDirection;
        try
        {
            files = await Task.Run(
                () => SortFiles(
                    CaptureFileIndex.GetImages(folderPath),
                    requestedSortColumn,
                    requestedSortDirection),
                load.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ShowFolderLoadFailure(ex);
            return;
        }

        if (!IsCurrentLoad(load))
        {
            return;
        }

        _activeFolderFileCount = files.Count;
        _isExternalFolder = !CaptureFileIndex.IsUnderRoot(folderPath);
        _files.Clear();
        AddressText.Text = folderPath;
        FolderTreeScopeText.Text = CaptureFileIndex.IsUnderRoot(folderPath) ? "캡처 루트" : "전체 경로";

        var switchedToDetailsForLargeFolder = RequiresDetailsView(_activeFolderFileCount) &&
            _viewMode != ExplorerViewMode.Details;
        if (switchedToDetailsForLargeFolder)
        {
            SetViewMode(ExplorerViewMode.Details);
        }

        var preferredIsInFolder = false;
        if (CaptureFileIndex.TryCreateImageFile(preferredPath, out var preferredFile) && preferredFile is not null)
        {
            preferredIsInFolder = string.Equals(preferredFile.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            await PopulateFilesAsync(files, preferredPath, load.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ShowFolderLoadFailure(ex);
            return;
        }

        if (requestedSortColumn != _fileSortColumn || requestedSortDirection != _fileSortDirection)
        {
            ApplyFileSort();
        }

        if (!IsCurrentLoad(load))
        {
            return;
        }

        FileCountText.Text = $"{_files.Count:0}개";

        var selected = preferredIsInFolder
            ? FindFile(preferredPath) ?? FindFile(_currentFile?.Path) ?? _files.FirstOrDefault()
            : FindFile(_currentFile?.Path);

        if (selected is not null)
        {
            SelectFile(selected);
        }
        else if (_currentFile is null)
        {
            SelectFile(_files.FirstOrDefault());
        }
        else
        {
            _isLoadingSelection = true;
            FileList.SelectedItem = null;
            _isLoadingSelection = false;
            UpdateNavigationButtons();
        }

        if (_currentFile is null)
        {
            ClearImage("선택한 폴더에 이미지가 없습니다.");
        }

        var largeFolderSuffix = switchedToDetailsForLargeFolder
            ? _isExternalFolder
                ? " · 외부 폴더는 응답성을 위해 세부 정보 보기로 표시합니다."
                : " · 대용량 폴더는 세부 정보 보기로 표시합니다."
            : string.Empty;
        SetViewerStatus($"폴더 로딩 완료: {_files.Count:0}개, {stopwatch.ElapsedMilliseconds:0}ms{largeFolderSuffix}");
    }

    private async Task PopulateFilesAsync(
        IReadOnlyList<CaptureImageFile> files,
        string? preferredPath,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.Add(files[index]);

            if ((index + 1) % FilePopulateBatchSize == 0)
            {
                FileCountText.Text = $"{index + 1:0}/{files.Count:0}개";
                await Dispatcher.Yield(DispatcherPriority.Background);
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredPath) && FindFile(preferredPath) is { } preferredFile)
        {
            _isLoadingSelection = true;
            FileList.SelectedItem = preferredFile;
            FileList.ScrollIntoView(preferredFile);
            _isLoadingSelection = false;
        }
    }

    private CancellationTokenSource StartNewLoad()
    {
        _loadCancellation?.Cancel();
        _loadCancellation = new CancellationTokenSource();
        return _loadCancellation;
    }

    private CancellationTokenSource StartNewImageLoad()
    {
        CancelImageLoad();
        _imageLoadCancellation = new CancellationTokenSource();
        return _imageLoadCancellation;
    }

    private void CancelImageLoad()
    {
        _imageLoadCancellation?.Cancel();
    }

    private bool IsCurrentImageLoad(CancellationTokenSource load)
    {
        return ReferenceEquals(_imageLoadCancellation, load) && !load.IsCancellationRequested;
    }

    private bool IsCurrentLoad(CancellationTokenSource load)
    {
        return ReferenceEquals(_loadCancellation, load) && !load.IsCancellationRequested;
    }

    private CaptureImageFile? FindFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return _files.FirstOrDefault(file => string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsSameFolderSelection(string? currentFolderPath, string? selectedFolderPath)
    {
        return !string.IsNullOrWhiteSpace(currentFolderPath) &&
            string.Equals(currentFolderPath, selectedFolderPath, StringComparison.OrdinalIgnoreCase);
    }

    private void MoveFileToSortedPosition(CaptureImageFile file)
    {
        var oldIndex = _files.IndexOf(file);
        if (oldIndex < 0)
        {
            return;
        }

        _files.RemoveAt(oldIndex);
        _files.Insert(GetSortedFileInsertIndex(file), file);
    }

    private int GetSortedFileInsertIndex(CaptureImageFile file)
    {
        for (var index = 0; index < _files.Count; index++)
        {
            if (CompareFiles(file, _files[index], _fileSortColumn, _fileSortDirection) < 0)
            {
                return index;
            }
        }

        return _files.Count;
    }

    internal static IReadOnlyList<CaptureImageFile> SortFiles(
        IEnumerable<CaptureImageFile> files,
        ViewerFileSortColumn sortColumn,
        ListSortDirection sortDirection)
    {
        var sorted = files.ToList();
        sorted.Sort((left, right) => CompareFiles(left, right, sortColumn, sortDirection));
        return sorted;
    }

    internal static int CompareFiles(
        CaptureImageFile left,
        CaptureImageFile right,
        ViewerFileSortColumn sortColumn,
        ListSortDirection sortDirection)
    {
        var comparison = sortColumn switch
        {
            ViewerFileSortColumn.Name => StringComparer.CurrentCultureIgnoreCase.Compare(left.FileName, right.FileName),
            ViewerFileSortColumn.ModifiedDate => left.LastWriteTime.CompareTo(right.LastWriteTime),
            ViewerFileSortColumn.Type => StringComparer.CurrentCultureIgnoreCase.Compare(left.Extension, right.Extension),
            ViewerFileSortColumn.Size => left.Length.CompareTo(right.Length),
            _ => 0
        };

        if (sortDirection == ListSortDirection.Descending)
        {
            comparison = comparison switch
            {
                < 0 => 1,
                > 0 => -1,
                _ => 0
            };
        }

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.FileName, right.FileName);
        return comparison != 0
            ? comparison
            : StringComparer.OrdinalIgnoreCase.Compare(left.Path, right.Path);
    }

    private void ApplyFileSort()
    {
        var selectedPath = (FileList.SelectedItem as CaptureImageFile)?.Path;
        var sorted = SortFiles(_files, _fileSortColumn, _fileSortDirection);

        if (!sorted.SequenceEqual(_files))
        {
            _isLoadingSelection = true;
            _files.Clear();
            foreach (var file in sorted)
            {
                _files.Add(file);
            }

            FileList.SelectedItem = FindFile(selectedPath);
            if (FileList.SelectedItem is not null)
            {
                FileList.ScrollIntoView(FileList.SelectedItem);
            }

            _isLoadingSelection = false;
        }

        UpdateDetailsSortHeaders();
        UpdateNavigationButtons();
    }

    private static bool PathsEqual(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
            !string.IsNullOrWhiteSpace(right) &&
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private void OnFolderTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_isSelectingFolder)
        {
            return;
        }

        if (e.NewValue is not FolderTreeNode { Path: { } path })
        {
            return;
        }

        if (IsSameFolderSelection(_currentFolderPath, path))
        {
            return;
        }

        if (!CaptureFileIndex.IsUnderRoot(path))
        {
            _ = NavigateToExternalFolderAsync(path);
            return;
        }

        var load = StartNewLoad();
        _ = LoadFolderAsync(path, null, load);
    }

    private async Task NavigateToExternalFolderAsync(string folderPath)
    {
        var load = StartNewLoad();
        var stopwatch = Stopwatch.StartNew();
        SetViewerStatus("외부 이미지 폴더를 읽는 중입니다.");

        try
        {
            var folderNodes = await Task.Run(() => CaptureFileIndex.BuildFolderTree(folderPath), load.Token);
            if (!IsCurrentLoad(load))
            {
                return;
            }

            _folderNodes = folderNodes;
            FolderTree.ItemsSource = _folderNodes;
            SelectFolderPath(folderPath);
            await LoadFolderAsync(folderPath, null, load, stopwatch);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowFolderLoadFailure(ex);
        }
    }

    private void OnFileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSelection)
        {
            return;
        }

        if (FileList.SelectedItem is CaptureImageFile file)
        {
            LoadImage(file);
        }
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        MoveSelection(-1);
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        MoveSelection(1);
    }

    private void MoveSelection(int offset)
    {
        if (_files.Count == 0)
        {
            return;
        }

        var currentIndex = FileList.SelectedIndex < 0 ? 0 : FileList.SelectedIndex;
        var targetIndex = Math.Clamp(currentIndex + offset, 0, _files.Count - 1);
        SelectFile(_files[targetIndex]);
    }
}
