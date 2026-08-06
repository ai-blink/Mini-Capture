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
    private void SetViewMode(ExplorerViewMode mode)
    {
        var selected = FileList.SelectedItem;
        _viewMode = mode;

        if (mode == ExplorerViewMode.Details)
        {
            FileList.View = CreateDetailsView();
            FileList.ItemTemplate = null;
            FileList.ItemsPanel = (ItemsPanelTemplate)Resources["DetailsItemsPanel"];
            FileList.ItemContainerStyle = (Style)Resources["ExplorerListItemStyle"];
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

    private void OnFileColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not GridViewColumnHeader { Tag: ViewerFileSortColumn sortColumn })
        {
            return;
        }

        if (_fileSortColumn == sortColumn)
        {
            _fileSortDirection = _fileSortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            _fileSortColumn = sortColumn;
            _fileSortDirection = sortColumn == ViewerFileSortColumn.ModifiedDate
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        ApplyFileSort();
        SetViewerStatus($"파일 정렬: {GetSortColumnLabel(_fileSortColumn)} {GetSortDirectionLabel(_fileSortDirection)}");
    }

    internal static bool RequiresDetailsView(int fileCount) =>
        fileCount > MaxIconViewFiles;

    private void ApplyStoredViewerLayout()
    {
        var settings = MiniCaptureSettingsStore.Load();
        ApplyStoredWindowBounds(settings);
        ApplyStoredExplorerLayout(settings);
        var viewMode = settings.ViewerExplorerViewMode;
        SetViewMode(IsDefinedViewMode(viewMode)
            ? viewMode.GetValueOrDefault()
            : ExplorerViewMode.Details);
    }

    private void ApplyStoredWindowBounds(MiniCaptureSettings settings)
    {
        if (settings.ViewerWidth is not { } width ||
            settings.ViewerHeight is not { } height ||
            settings.ViewerLeft is not { } left ||
            settings.ViewerTop is not { } top)
        {
            return;
        }

        width = Math.Max(MinStoredViewerWidth, width);
        height = Math.Max(MinStoredViewerHeight, height);
        var bounds = new WpfRect(left, top, width, height);
        if (!IntersectsVirtualScreen(bounds))
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;

        if (string.Equals(settings.ViewerWindowState, nameof(WindowState.Maximized), StringComparison.Ordinal))
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void ApplyStoredExplorerLayout(MiniCaptureSettings settings)
    {
        if (settings.ViewerFolderTreeWidth is { } folderWidth)
        {
            FolderTreeColumn.Width = new GridLength(ClampPanelWidth(folderWidth, FolderTreeColumn.MinWidth));
        }

        if (settings.ViewerFileListWidth is { } fileListWidth)
        {
            FileListColumn.Width = new GridLength(ClampPanelWidth(fileListWidth, FileListColumn.MinWidth));
        }
    }

    private void SaveViewerLayout()
    {
        var settings = MiniCaptureSettingsStore.Load();
        var bounds = WindowState == WindowState.Normal ? new WpfRect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.Width >= MinStoredViewerWidth && bounds.Height >= MinStoredViewerHeight)
        {
            settings.ViewerLeft = bounds.Left;
            settings.ViewerTop = bounds.Top;
            settings.ViewerWidth = bounds.Width;
            settings.ViewerHeight = bounds.Height;
        }

        settings.ViewerWindowState = WindowState == WindowState.Maximized
            ? nameof(WindowState.Maximized)
            : nameof(WindowState.Normal);
        settings.ViewerExplorerViewMode = _viewMode;
        settings.ViewerFolderTreeWidth = FolderTreeColumn.ActualWidth > 0
            ? FolderTreeColumn.ActualWidth
            : FolderTreeColumn.Width.Value;
        settings.ViewerFileListWidth = FileListColumn.ActualWidth > 0
            ? FileListColumn.ActualWidth
            : FileListColumn.Width.Value;
        MiniCaptureSettingsStore.Save(settings);
    }

    private void OnViewStateSaveTimerTick(object? sender, EventArgs e)
    {
        _viewStateSaveTimer.Stop();
        SaveCurrentImageViewState();
    }

    private void FlushCurrentImageViewState()
    {
        if (_viewStateSaveTimer.IsEnabled)
        {
            _viewStateSaveTimer.Stop();
        }

        SaveCurrentImageViewState();
    }

    private void ScheduleCurrentImageViewStateSave()
    {
        if (_currentFile is null || _editableImage is null || _isRestoringImageViewState)
        {
            return;
        }

        _viewStateSaveTimer.Stop();
        _viewStateSaveTimer.Start();
    }

    private void SaveCurrentImageViewState()
    {
        if (_currentFile is null || _editableImage is null || _isRestoringImageViewState)
        {
            return;
        }

        var settings = MiniCaptureSettingsStore.Load();
        var states = settings.ViewerImageStates
            .Where(state => !string.IsNullOrWhiteSpace(state.Path))
            .Where(state => !string.Equals(state.Path, _currentFile.Path, StringComparison.OrdinalIgnoreCase))
            .ToList();

        states.Insert(0, new ViewerImageViewState
        {
            Path = _currentFile.Path,
            Zoom = Math.Clamp(_zoom, MinZoom, MaxZoom),
            FitMode = _fitMode,
            HorizontalOffset = Math.Max(0, ImageScrollViewer.HorizontalOffset),
            VerticalOffset = Math.Max(0, ImageScrollViewer.VerticalOffset)
        });

        settings.ViewerImageStates = states
            .Take(MaxStoredImageViewStates)
            .ToList();
        MiniCaptureSettingsStore.Save(settings, notify: false);
    }

    private void RestoreImageViewState(string path)
    {
        var state = MiniCaptureSettingsStore.Load().ViewerImageStates
            .FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase));
        if (state is null)
        {
            _fitMode = true;
            FitToStage();
            return;
        }

        _isRestoringImageViewState = true;
        try
        {
            _fitMode = state.FitMode;
            if (_fitMode)
            {
                FitToStage();
            }
            else
            {
                _zoom = Math.Clamp(state.Zoom, MinZoom, MaxZoom);
                ApplyZoom();
            }

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    try
                    {
                        ImageScrollViewer.ScrollToHorizontalOffset(Math.Max(0, state.HorizontalOffset));
                        ImageScrollViewer.ScrollToVerticalOffset(Math.Max(0, state.VerticalOffset));
                    }
                    finally
                    {
                        _isRestoringImageViewState = false;
                    }
                }));
        }
        catch
        {
            _isRestoringImageViewState = false;
            throw;
        }
    }

    private static bool IsDefinedViewMode(ExplorerViewMode? mode) =>
        mode is { } value && Enum.IsDefined(value);

    private static double ClampPanelWidth(double width, double minWidth) =>
        Math.Clamp(width, minWidth, MaxStoredPanelWidth);

    private static bool IntersectsVirtualScreen(WpfRect bounds)
    {
        var virtualScreen = new WpfRect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        return virtualScreen.IntersectsWith(bounds);
    }

    private GridView CreateDetailsView()
    {
        return new GridView
        {
            Columns =
            {
                new GridViewColumn
                {
                    Header = CreateSortableColumnHeader("이름", ViewerFileSortColumn.Name),
                    Width = 180,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.FileName))
                },
                new GridViewColumn
                {
                    Header = CreateSortableColumnHeader("수정한 날짜", ViewerFileSortColumn.ModifiedDate),
                    Width = 116,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.ModifiedText))
                },
                new GridViewColumn
                {
                    Header = CreateSortableColumnHeader("유형", ViewerFileSortColumn.Type),
                    Width = 48,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.Extension))
                },
                new GridViewColumn
                {
                    Header = CreateSortableColumnHeader("크기", ViewerFileSortColumn.Size),
                    Width = 70,
                    DisplayMemberBinding = new WpfBinding(nameof(CaptureImageFile.SizeText))
                }
            }
        };
    }

    private GridViewColumnHeader CreateSortableColumnHeader(string label, ViewerFileSortColumn sortColumn)
    {
        var header = new GridViewColumnHeader
        {
            Tag = sortColumn,
            Content = GetSortHeaderText(label, sortColumn)
        };
        AutomationProperties.SetName(header, $"{label} 기준 정렬");
        header.Click += OnFileColumnHeaderClick;
        return header;
    }

    private void UpdateDetailsSortHeaders()
    {
        if (FileList.View is not GridView detailsView)
        {
            return;
        }

        foreach (var column in detailsView.Columns)
        {
            if (column.Header is GridViewColumnHeader { Tag: ViewerFileSortColumn sortColumn } header)
            {
                header.Content = GetSortHeaderText(GetSortColumnLabel(sortColumn), sortColumn);
            }
        }
    }

    private string GetSortHeaderText(string label, ViewerFileSortColumn sortColumn)
    {
        if (_fileSortColumn != sortColumn)
        {
            return label;
        }

        var arrow = _fileSortDirection == ListSortDirection.Ascending ? "▲" : "▼";
        return $"{label} {arrow}";
    }

    private static string GetSortColumnLabel(ViewerFileSortColumn sortColumn) => sortColumn switch
    {
        ViewerFileSortColumn.Name => "이름",
        ViewerFileSortColumn.ModifiedDate => "수정한 날짜",
        ViewerFileSortColumn.Type => "유형",
        ViewerFileSortColumn.Size => "크기",
        _ => "이름"
    };

    private static string GetSortDirectionLabel(ListSortDirection sortDirection) =>
        sortDirection == ListSortDirection.Ascending ? "오름차순" : "내림차순";
}
