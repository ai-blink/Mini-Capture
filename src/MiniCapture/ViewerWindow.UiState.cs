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
        StatusText.Text = _currentFile.Path;
        UpdateStatusMetadata(position);
    }

    private void UpdateStatusMetadata(int position = 0)
    {
        if (_currentFile is null || _editableImage is null)
        {
            StatusMetaText.Text = string.Empty;
            return;
        }

        var format = IOPath.GetExtension(_currentFile.Path).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(format))
        {
            format = "IMG";
        }

        var positionText = position > 0 && _files.Count > 0
            ? $" | {position}/{_files.Count}"
            : string.Empty;
        StatusMetaText.Text = $"{_editableImage.PixelWidth:0}x{_editableImage.PixelHeight:0} | {format} | {_currentFile.SizeText}{positionText}";
    }

    private void UpdateDirtyIndicator()
    {
        var fileName = _currentFile?.FileName ?? IOPath.GetFileName(_lastSavedPath) ?? string.Empty;
        var prefix = _isDirty ? "* " : string.Empty;
        CurrentFileText.Text = $"{prefix}{fileName}";
        Title = string.IsNullOrWhiteSpace(fileName)
            ? "Mini Capture Viewer"
            : $"{prefix}{fileName} - Mini Capture Viewer";
        SaveStateText.Text = _isDirty ? "변경됨" : "저장됨";
        SaveStateText.Foreground = _isDirty
            ? new SolidColorBrush(WpfColor.FromRgb(250, 204, 21))
            : new SolidColorBrush(WpfColor.FromRgb(94, 234, 212));
    }

    private void UpdateEditButtons()
    {
        if (UndoButton is not null)
        {
            UndoButton.IsEnabled = _undoImages.Count > 0;
        }

        if (RedoButton is not null)
        {
            RedoButton.IsEnabled = _redoImages.Count > 0;
        }

        if (DeleteAnnotationButton is not null)
        {
            DeleteAnnotationButton.IsEnabled = _selectedAnnotation is not null;
        }
    }

    private void UpdateViewButtons()
    {
        SetViewButtonState(DetailsViewButton, _viewMode == ExplorerViewMode.Details);
        SetViewButtonState(SmallViewButton, _viewMode == ExplorerViewMode.SmallIcons);
        SetViewButtonState(MediumViewButton, _viewMode == ExplorerViewMode.MediumIcons);
        SetViewButtonState(LargeViewButton, _viewMode == ExplorerViewMode.LargeIcons);
    }

    private void UpdateToolButtons()
    {
        SetViewButtonState(PanToolButton, _editTool == ViewerEditTool.Pan || _spacePanActive);
        SetViewButtonState(SelectToolButton, _editTool == ViewerEditTool.Select && !_spacePanActive);
        SetViewButtonState(PixelSelectToolButton, _editTool == ViewerEditTool.PixelSelect && !_spacePanActive);
        SetViewButtonState(RectangleToolButton, _editTool == ViewerEditTool.Rectangle);
        SetViewButtonState(EllipseToolButton, _editTool == ViewerEditTool.Ellipse);
        SetViewButtonState(MosaicToolButton, _editTool == ViewerEditTool.Mosaic);
        SetViewButtonState(TextToolButton, _editTool == ViewerEditTool.Text);
        SetViewButtonState(PenToolButton, _editTool == ViewerEditTool.Pen);
        SetViewButtonState(ArrowToolButton, _editTool == ViewerEditTool.Arrow);
        SetViewButtonState(CropToolButton, _isCropMode);
        UpdateToolContext();
        AnnotationOverlay.Cursor = _editTool switch
        {
            ViewerEditTool.Pan => WpfCursors.SizeAll,
            ViewerEditTool.Select when !_spacePanActive => WpfCursors.Arrow,
            _ when _spacePanActive => WpfCursors.SizeAll,
            _ => WpfCursors.Cross
        };
    }

    private void UpdateToolContext()
    {
        var showsStroke = _editTool is ViewerEditTool.Pen or
            ViewerEditTool.Arrow or
            ViewerEditTool.Rectangle or
            ViewerEditTool.Ellipse;
        var showsText = _editTool == ViewerEditTool.Text;
        var showsMosaic = _editTool == ViewerEditTool.Mosaic;

        ContextToolbar.Visibility = showsStroke || showsText || showsMosaic
            ? Visibility.Visible
            : Visibility.Collapsed;
        ColorContextGroup.Visibility = showsStroke || showsText
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrokeContextGroup.Visibility = showsStroke
            ? Visibility.Visible
            : Visibility.Collapsed;
        TextContextGroup.Visibility = showsText
            ? Visibility.Visible
            : Visibility.Collapsed;
        MosaicContextGroup.Visibility = showsMosaic
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (showsMosaic)
        {
            SetViewButtonState(MosaicTypeBlockButton, _mosaicType == ViewerMosaicType.Block);
            SetViewButtonState(MosaicTypeBlurButton, _mosaicType == ViewerMosaicType.Blur);
        }

        ContextToolName.Text = _editTool switch
        {
            ViewerEditTool.Pen => "펜 설정",
            ViewerEditTool.Arrow => "화살표 설정",
            ViewerEditTool.Rectangle => "네모 설정",
            ViewerEditTool.Ellipse => "원 설정",
            ViewerEditTool.Text => "텍스트 설정",
            ViewerEditTool.Mosaic => "모자이크 설정",
            _ => string.Empty
        };
    }

    private void UpdateColorSwatches()
    {
        if (ColorSwatches is null)
        {
            return;
        }

        foreach (var child in ColorSwatches.Children.OfType<System.Windows.Controls.Button>())
        {
            var selected = child.Tag is string colorText &&
                System.Windows.Media.ColorConverter.ConvertFromString(colorText) is WpfColor color &&
                color.Equals(_selectedColor);
            child.BorderBrush = selected
                ? new SolidColorBrush(WpfColor.FromRgb(25, 127, 120))
                : new SolidColorBrush(WpfColor.FromRgb(185, 196, 197));
            child.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            child.Padding = selected ? new Thickness(1) : new Thickness(2);
        }
    }

    private void SetViewerStatus(string message)
    {
        StatusText.Text = message;
        AutomationProperties.SetName(StatusText, message);
    }

    private void ShowFolderLoadFailure(Exception ex)
    {
        FileCountText.Text = "읽기 실패";
        SetViewerStatus($"폴더를 읽을 수 없습니다: {ex.Message}");
    }

    private static void SetViewButtonState(System.Windows.Controls.Button button, bool selected)
    {
        button.Background = selected
            ? new SolidColorBrush(WpfColor.FromRgb(216, 238, 235))
            : WpfBrushes.Transparent;
        button.Foreground = selected
            ? new SolidColorBrush(WpfColor.FromRgb(25, 127, 120))
            : new SolidColorBrush(WpfColor.FromRgb(89, 104, 106));
        button.BorderBrush = selected
            ? new SolidColorBrush(WpfColor.FromRgb(25, 127, 120))
            : WpfBrushes.Transparent;
        button.BorderThickness = selected ? new Thickness(1, 1, 1, 2) : new Thickness(1);
    }

    private void SelectFolderPath(string folderPath)
    {
        _isSelectingFolder = true;
        try
        {
            ClearFolderSelection(_folderNodes);
            _ = SelectFolderPath(_folderNodes, folderPath);
        }
        finally
        {
            _isSelectingFolder = false;
        }
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
