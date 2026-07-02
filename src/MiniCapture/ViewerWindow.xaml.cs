using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfBinding = System.Windows.Data.Binding;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;

namespace MiniCapture;

public partial class ViewerWindow : Window
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 8.0;
    private const double ZoomStep = 1.25;

    private readonly ObservableCollection<CaptureImageFile> _files = new();
    private ObservableCollection<FolderTreeNode> _folderNodes = new();
    private string? _pendingPath;
    private string? _currentFolderPath;
    private CaptureImageFile? _currentFile;
    private bool _fitMode = true;
    private bool _isLoadingSelection;
    private double _zoom = 1.0;
    private ExplorerViewMode _viewMode = ExplorerViewMode.Details;

    public ViewerWindow(string? imagePath)
    {
        InitializeComponent();
        _pendingPath = imagePath;
        FileList.ItemsSource = _files;
    }

    public void OpenImage(string? imagePath)
    {
        _pendingPath = imagePath;
        RefreshIndex();
        Activate();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetViewMode(ExplorerViewMode.Details);
        RefreshIndex();
    }

    private void RefreshIndex()
    {
        _folderNodes = CaptureFileIndex.BuildFolderTree();
        FolderTree.ItemsSource = _folderNodes;

        var targetPath = ResolveTargetPath(_pendingPath);
        var targetFolder = GetTargetFolder(targetPath);
        SelectFolderPath(targetFolder);
        LoadFolder(targetFolder, targetPath);
        _pendingPath = null;
    }

    private string? ResolveTargetPath(string? requestedPath)
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
            var folder = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                return folder;
            }
        }

        return CaptureFileIndex.RootDirectory;
    }

    private void LoadFolder(string folderPath, string? preferredPath)
    {
        _currentFolderPath = folderPath;
        _files.Clear();
        foreach (var file in CaptureFileIndex.GetImages(folderPath))
        {
            _files.Add(file);
        }

        AddressText.Text = folderPath;
        FileCountText.Text = $"{_files.Count:0}개";

        var selected = FindFile(preferredPath) ?? FindFile(_currentFile?.Path) ?? _files.FirstOrDefault();
        SelectFile(selected);

        if (selected is null)
        {
            ClearImage("선택한 폴더에 이미지가 없습니다.");
        }
    }

    private CaptureImageFile? FindFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return _files.FirstOrDefault(file => string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectFile(CaptureImageFile? file)
    {
        _isLoadingSelection = true;
        FileList.SelectedItem = file;
        if (file is not null)
        {
            FileList.ScrollIntoView(file);
        }

        _isLoadingSelection = false;
        LoadImage(file);
    }

    private void LoadImage(CaptureImageFile? file)
    {
        if (file is null)
        {
            ClearImage("표시할 이미지가 없습니다.");
            return;
        }

        try
        {
            using var stream = File.Open(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            _currentFile = file;
            PreviewImage.Source = image;
            EmptyMessage.Visibility = Visibility.Collapsed;
            CurrentFileText.Text = file.FileName;
            StatusText.Text = $"{file.Path}  |  {file.SizeText}";
            Title = $"{file.FileName} - Mini Capture Viewer";

            if (_fitMode)
            {
                FitToStage();
            }
            else
            {
                ApplyZoom();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ClearImage($"이미지를 열 수 없습니다: {ex.Message}");
        }

        UpdateNavigationButtons();
    }

    private void ClearImage(string message)
    {
        _currentFile = null;
        PreviewImage.Source = null;
        CurrentFileText.Text = string.Empty;
        StatusText.Text = message;
        EmptyMessage.Text = message;
        EmptyMessage.Visibility = Visibility.Visible;
        ZoomText.Text = "-";
        UpdateNavigationButtons();
    }

    private void OnFolderTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not FolderTreeNode { Path: { } path })
        {
            return;
        }

        LoadFolder(path, null);
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
    }

    private void OnActualSizeClick(object sender, RoutedEventArgs e)
    {
        SetZoom(1.0);
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        _pendingPath = _currentFile?.Path;
        RefreshIndex();
    }

    private void OnViewModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag } ||
            !Enum.TryParse<ExplorerViewMode>(tag, out var mode))
        {
            return;
        }

        SetViewMode(mode);
    }

    private void SetViewMode(ExplorerViewMode mode)
    {
        var selected = FileList.SelectedItem;
        _viewMode = mode;

        if (mode == ExplorerViewMode.Details)
        {
            FileList.View = CreateDetailsView();
            FileList.ItemTemplate = null;
            FileList.ItemsPanel = (ItemsPanelTemplate)Resources["DetailsItemsPanel"];
            FileList.ItemContainerStyle = null;
        }
        else
        {
            FileList.View = null;
            FileList.ItemsPanel = (ItemsPanelTemplate)Resources["IconItemsPanel"];
            FileList.ItemContainerStyle = (Style)Resources["IconListItemStyle"];
            FileList.ItemTemplate = (DataTemplate)Resources[mode switch
            {
                ExplorerViewMode.SmallIcons => "SmallIconTemplate",
                ExplorerViewMode.LargeIcons => "LargeIconTemplate",
                _ => "MediumIconTemplate"
            }];
        }

        FileList.SelectedItem = selected;
        UpdateViewButtons();
    }

    private static GridView CreateDetailsView()
    {
        return new GridView
        {
            Columns =
            {
                new GridViewColumn
                {
                    Header = "이름",
                    Width = 180,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.FileName))
                },
                new GridViewColumn
                {
                    Header = "수정한 날짜",
                    Width = 116,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.ModifiedText))
                },
                new GridViewColumn
                {
                    Header = "유형",
                    Width = 48,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.Extension))
                },
                new GridViewColumn
                {
                    Header = "크기",
                    Width = 70,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.SizeText))
                }
            }
        };
    }

    private void SetZoom(double zoom)
    {
        _fitMode = false;
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ApplyZoom();
    }

    private void FitToStage()
    {
        if (PreviewImage.Source is not BitmapSource bitmap)
        {
            return;
        }

        var viewportWidth = ImageScrollViewer.ViewportWidth;
        var viewportHeight = ImageScrollViewer.ViewportHeight;
        if (double.IsNaN(viewportWidth) || viewportWidth <= 0)
        {
            viewportWidth = ImageScrollViewer.ActualWidth;
        }

        if (double.IsNaN(viewportHeight) || viewportHeight <= 0)
        {
            viewportHeight = ImageScrollViewer.ActualHeight;
        }

        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            _zoom = 1.0;
        }
        else
        {
            var zoomX = Math.Max(1, viewportWidth - 28) / bitmap.PixelWidth;
            var zoomY = Math.Max(1, viewportHeight - 28) / bitmap.PixelHeight;
            _zoom = Math.Clamp(Math.Min(zoomX, zoomY), MinZoom, MaxZoom);
        }

        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (PreviewImage.Source is not BitmapSource bitmap)
        {
            ZoomText.Text = "-";
            return;
        }

        PreviewImage.Width = Math.Max(1, bitmap.PixelWidth * _zoom);
        PreviewImage.Height = Math.Max(1, bitmap.PixelHeight * _zoom);
        ZoomText.Text = $"{_zoom * 100:0}%";
    }

    private void OnImageStageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitMode)
        {
            FitToStage();
        }
    }

    private void OnImagePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        SetZoom(e.Delta > 0 ? _zoom * ZoomStep : _zoom / ZoomStep);
        e.Handled = true;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Right:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Add:
            case Key.OemPlus:
                SetZoom(_zoom * ZoomStep);
                e.Handled = true;
                break;
            case Key.Subtract:
            case Key.OemMinus:
                SetZoom(_zoom / ZoomStep);
                e.Handled = true;
                break;
            case Key.D1:
                SetZoom(1.0);
                e.Handled = true;
                break;
            case Key.F:
                _fitMode = true;
                FitToStage();
                e.Handled = true;
                break;
        }
    }

    private void UpdateNavigationButtons()
    {
        var index = FileList.SelectedIndex;
        PreviousButton.IsEnabled = index > 0;
        NextButton.IsEnabled = index >= 0 && index < _files.Count - 1;

        if (_currentFile is null)
        {
            return;
        }

        var position = index >= 0 ? index + 1 : 0;
        StatusText.Text = $"{_currentFile.Path}  |  {_currentFile.SizeText}  |  {position}/{_files.Count}";
    }

    private void UpdateViewButtons()
    {
        SetViewButtonState(DetailsViewButton, _viewMode == ExplorerViewMode.Details);
        SetViewButtonState(SmallViewButton, _viewMode == ExplorerViewMode.SmallIcons);
        SetViewButtonState(MediumViewButton, _viewMode == ExplorerViewMode.MediumIcons);
        SetViewButtonState(LargeViewButton, _viewMode == ExplorerViewMode.LargeIcons);
    }

    private static void SetViewButtonState(System.Windows.Controls.Button button, bool selected)
    {
        button.Background = selected
            ? new SolidColorBrush(WpfColor.FromRgb(18, 165, 148))
            : WpfBrushes.White;
        button.Foreground = selected ? WpfBrushes.White : new SolidColorBrush(WpfColor.FromRgb(24, 33, 47));
        button.BorderBrush = selected
            ? new SolidColorBrush(WpfColor.FromRgb(15, 118, 110))
            : new SolidColorBrush(WpfColor.FromRgb(207, 216, 227));
    }

    private void SelectFolderPath(string folderPath)
    {
        ClearFolderSelection(_folderNodes);
        _ = SelectFolderPath(_folderNodes, folderPath);
    }

    private static void ClearFolderSelection(IEnumerable<FolderTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsSelected = false;
            ClearFolderSelection(node.Children);
        }
    }

    private static bool SelectFolderPath(IEnumerable<FolderTreeNode> nodes, string folderPath)
    {
        foreach (var node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.Path) &&
                string.Equals(node.Path, folderPath, StringComparison.OrdinalIgnoreCase))
            {
                node.IsSelected = true;
                return true;
            }

            if (SelectFolderPath(node.Children, folderPath))
            {
                node.IsExpanded = true;
                return true;
            }
        }

        return false;
    }
}
