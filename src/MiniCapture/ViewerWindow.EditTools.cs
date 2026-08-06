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
    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        UndoEdit();
    }

    private void OnRedoClick(object sender, RoutedEventArgs e)
    {
        RedoEdit();
    }

    private void OnDeleteAnnotationClick(object sender, RoutedEventArgs e)
    {
        DeleteSelectedAnnotation();
    }

    private void OnRotateLeftClick(object sender, RoutedEventArgs e)
    {
        RotateImage(-90);
    }

    private void OnRotateRightClick(object sender, RoutedEventArgs e)
    {
        RotateImage(90);
    }

    private void OnStrokeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _strokeThickness = Math.Max(1, e.NewValue);
        if (StrokeText is not null)
        {
            StrokeText.Text = $"{_strokeThickness:0}px";
        }
    }

    private void OnMosaicSizeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _mosaicBlockSize = Math.Clamp((int)Math.Round(e.NewValue), 6, 64);
        if (MosaicSizeText is not null)
        {
            MosaicSizeText.Text = $"{_mosaicBlockSize}px";
        }
    }

    private void OnMosaicTypeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag } ||
            !Enum.TryParse<ViewerMosaicType>(tag, out var mosaicType))
        {
            return;
        }

        _mosaicType = mosaicType;
        UpdateToolContext();
    }

    private void OnToolButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag } ||
            !Enum.TryParse<ViewerEditTool>(tag, out var tool))
        {
            return;
        }

        if (_isCropMode)
        {
            CloseCropMode();
        }

        _editTool = tool;
        if (tool == ViewerEditTool.PixelSelect)
        {
            SelectAnnotation(null);
        }

        UpdateToolButtons();
        SetViewerStatus(tool switch
        {
            ViewerEditTool.Pan => "핸드툴: 이미지를 끌어 이동합니다.",
            ViewerEditTool.Select => "선택: 보기 상태를 유지하고 편집하지 않습니다.",
            ViewerEditTool.PixelSelect => "영역 선택: 드래그한 영역을 복사, 잘라내기, 붙여넣기할 수 있습니다.",
            ViewerEditTool.Rectangle => "네모: 이미지 위에서 드래그해 그립니다.",
            ViewerEditTool.Ellipse => "동그라미: 이미지 위에서 드래그해 그립니다.",
            ViewerEditTool.Mosaic => "모자이크: 가릴 영역을 드래그합니다.",
            ViewerEditTool.Pen => "펜: 이미지 위에 자유선을 그립니다.",
            ViewerEditTool.Arrow => "화살표: 드래그해서 방향을 표시합니다.",
            _ => "텍스트: 이미지 위를 클릭해 입력합니다."
        });
    }

    private void OnCropToolClick(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null)
        {
            SetViewerStatus("자를 이미지가 없습니다.");
            return;
        }

        if (_isCropMode)
        {
            CloseCropMode();
            SetViewerStatus("자르기를 취소했습니다.");
            return;
        }

        _isCropMode = true;
        _editTool = ViewerEditTool.Pan;
        SelectAnnotation(null);
        ResetCropControls();
        CropPanel.Visibility = Visibility.Visible;
        UpdateToolButtons();
        RenderAnnotations();
        SetViewerStatus("자르기: 왼쪽 패널에서 값을 조절한 뒤 적용하세요.");
    }

    private void OnCropValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingCropControls || !_isCropMode)
        {
            return;
        }

        SynchronizeCropControls();
        RenderAnnotations();
    }

    private void OnCropTextLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ApplyCropTextValue(sender as System.Windows.Controls.TextBox);
    }

    private void OnCropTextPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        ApplyCropTextValue(sender as System.Windows.Controls.TextBox);
        e.Handled = true;
    }

    private void OnCancelCropClick(object sender, RoutedEventArgs e)
    {
        CloseCropMode();
        SetViewerStatus("자르기를 취소했습니다.");
    }

    private void OnApplyCropClick(object sender, RoutedEventArgs e)
    {
        ApplyCrop();
    }

    private void OnColorSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string colorText })
        {
            return;
        }

        if (System.Windows.Media.ColorConverter.ConvertFromString(colorText) is WpfColor color)
        {
            _selectedColor = color;
            UpdateColorSwatches();
            SetViewerStatus("색상을 선택했습니다.");
        }
    }

    private void OnImageMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_editableImage is null)
        {
            return;
        }

        var position = ClampToSurface(e.GetPosition(AnnotationOverlay));
        if (_editTool == ViewerEditTool.Pan || _spacePanActive)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(ImageScrollViewer);
            _panStartHorizontalOffset = ImageScrollViewer.HorizontalOffset;
            _panStartVerticalOffset = ImageScrollViewer.VerticalOffset;
            AnnotationOverlay.CaptureMouse();
            AnnotationOverlay.Cursor = WpfCursors.SizeAll;
            e.Handled = true;
            return;
        }

        if (_editTool == ViewerEditTool.Select)
        {
            SelectAnnotation(null);
            e.Handled = true;
            return;
        }

        if (_editTool == ViewerEditTool.PixelSelect)
        {
            _isDrawing = true;
            _drawStartPoint = position;
            _previewShape = CreatePixelSelectionPreview();
            AnnotationOverlay.Children.Add(_previewShape);
            UpdatePreviewShape(position);
            AnnotationOverlay.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (_editTool == ViewerEditTool.Pen)
        {
            _isDrawing = true;
            _penPoints.Clear();
            _penPoints.Add(position);
            _previewShape = CreatePenPreviewPath();
            AnnotationOverlay.Children.Add(_previewShape);
            AnnotationOverlay.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (_editTool == ViewerEditTool.Text)
        {
            AddTextAnnotation(position);
            e.Handled = true;
            return;
        }

        _isDrawing = true;
        _drawStartPoint = position;
        _previewShape = CreatePreviewShape();
        AnnotationOverlay.Children.Add(_previewShape);
        UpdatePreviewShape(position);
        AnnotationOverlay.CaptureMouse();
        e.Handled = true;
    }

    private void OnImageMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isPanning)
        {
            var current = e.GetPosition(ImageScrollViewer);
            ImageScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - (current.X - _panStartPoint.X));
            ImageScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - (current.Y - _panStartPoint.Y));
            e.Handled = true;
            return;
        }

        if (_isMovingAnnotation || _isResizingAnnotation)
        {
            UpdateSelectedAnnotationDrag(ClampToSurface(e.GetPosition(AnnotationOverlay)));
            e.Handled = true;
            return;
        }

        if (!_isDrawing)
        {
            return;
        }

        var point = ClampToSurface(e.GetPosition(AnnotationOverlay));
        if (_editTool == ViewerEditTool.Pen)
        {
            _penPoints.Add(point);
            UpdatePenPreviewPath();
        }
        else
        {
            UpdatePreviewShape(point);
        }

        e.Handled = true;
    }

    private void OnImageMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            AnnotationOverlay.ReleaseMouseCapture();
            AnnotationOverlay.Cursor = _editTool == ViewerEditTool.Pan ? WpfCursors.SizeAll : WpfCursors.Cross;
            ScheduleCurrentImageViewStateSave();
            e.Handled = true;
            return;
        }

        if (_isMovingAnnotation || _isResizingAnnotation)
        {
            _isMovingAnnotation = false;
            _isResizingAnnotation = false;
            AnnotationOverlay.ReleaseMouseCapture();
            MarkAnnotationChanged("선택 항목을 수정했습니다.");
            e.Handled = true;
            return;
        }

        if (!_isDrawing)
        {
            return;
        }

        var endPoint = ClampToSurface(e.GetPosition(AnnotationOverlay));
        if (_editTool == ViewerEditTool.Pen)
        {
            _penPoints.Add(endPoint);
            RemovePreviewShape();
            AddPenAnnotation(_penPoints);
            _penPoints.Clear();
            _isDrawing = false;
            AnnotationOverlay.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        var rect = GetPixelRect(_drawStartPoint, endPoint);
        RemovePreviewShape();

        if (_editTool == ViewerEditTool.PixelSelect)
        {
            _pixelSelection = rect.Width >= 2 && rect.Height >= 2 ? rect : null;
            _isDrawing = false;
            AnnotationOverlay.ReleaseMouseCapture();
            RenderAnnotations();
            SetViewerStatus(_pixelSelection is null
                ? "영역 선택을 해제했습니다."
                : $"영역 선택: {rect.Width} × {rect.Height}px");
            e.Handled = true;
            return;
        }

        if (rect.Width >= 2 && rect.Height >= 2)
        {
            if (_editTool == ViewerEditTool.Mosaic)
            {
                ApplyMosaic(rect);
            }
            else if (_editTool == ViewerEditTool.Arrow)
            {
                AddArrowAnnotation(_drawStartPoint, endPoint);
            }
            else
            {
                AddShapeAnnotation(rect, _editTool == ViewerEditTool.Ellipse);
            }
        }

        _isDrawing = false;
        AnnotationOverlay.ReleaseMouseCapture();
        e.Handled = true;
    }
}
