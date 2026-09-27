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
using MiniCapture.Resources;

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

        if (_selectedAnnotation is { Kind: ViewerAnnotationKind.Mosaic } mosaic &&
            mosaic.MosaicBlockSize != _mosaicBlockSize)
        {
            // 슬라이더를 끄는 동안 쏟아지는 값 변경은 한 동작으로 묶는다.
            PushUndoSnapshot((mosaic, nameof(EditableAnnotation.MosaicBlockSize)));
            mosaic.MosaicBlockSize = _mosaicBlockSize;
            RefreshAnnotationVisual(mosaic);
            MarkAnnotationChanged(string.Format(Strings.Viewer_Status_MosaicSizeChanged, _mosaicBlockSize));
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
        if (_selectedAnnotation is { Kind: ViewerAnnotationKind.Mosaic } mosaic &&
            mosaic.MosaicType != _mosaicType)
        {
            PushUndoSnapshot();
            mosaic.MosaicType = _mosaicType;
            RefreshAnnotationVisual(mosaic);
            MarkAnnotationChanged(_mosaicType == ViewerMosaicType.Blur ? Strings.Viewer_Status_ChangedToBlur : Strings.Viewer_Status_ChangedToBlock);
        }

        UpdateToolContext();
    }

    private void OnToolButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag } ||
            !Enum.TryParse<ViewerEditTool>(tag, out var tool))
        {
            return;
        }

        SetEditTool(tool);
    }

    // 그리기 도구의 드래그는 항상 새 객체를 만들고, 본문 드래그 이동은 객체 선택 도구에서만 한다.
    // 도구를 바꾸면(객체 선택으로 가는 경우 제외) 객체 선택을 푼다. 버튼·단축키가 모두 이 경로를 쓴다.
    private void SetEditTool(ViewerEditTool tool)
    {
        if (_isCropMode)
        {
            CloseCropMode();
        }

        _editTool = tool;
        if (tool != ViewerEditTool.Select)
        {
            SelectAnnotation(null);
        }

        UpdateToolButtons();
        var moveHint = Strings.Viewer_Hint_Move;
        SetViewerStatus(tool switch
        {
            ViewerEditTool.Pan => Strings.Viewer_ToolHint_Pan,
            ViewerEditTool.Select => Strings.Viewer_ToolHint_Select,
            ViewerEditTool.PixelSelect => Strings.Viewer_ToolHint_PixelSelect,
            ViewerEditTool.Rectangle => Strings.Viewer_ToolHint_Rectangle + moveHint,
            ViewerEditTool.Ellipse => Strings.Viewer_ToolHint_Ellipse + moveHint,
            ViewerEditTool.Mosaic => Strings.Viewer_ToolHint_Mosaic + moveHint,
            ViewerEditTool.Pen => Strings.Viewer_ToolHint_Pen + moveHint,
            ViewerEditTool.Arrow => Strings.Viewer_ToolHint_Arrow + moveHint,
            _ => Strings.Viewer_ToolHint_Text + moveHint
        });
    }

    private bool IsEditingAnnotationText() =>
        Keyboard.FocusedElement is System.Windows.Controls.TextBox { Tag: EditableAnnotation };

    private void EndAnnotationTextEditing()
    {
        if (IsEditingAnnotationText())
        {
            Focus();
            SetViewerStatus(Strings.Viewer_Status_TextEditEnded);
        }
    }

    private void OnCropToolClick(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null)
        {
            SetViewerStatus(Strings.Viewer_Status_NothingToCrop);
            return;
        }

        if (_isCropMode)
        {
            CloseCropMode();
            SetViewerStatus(Strings.Viewer_Status_CropCancelled);
            return;
        }

        _isCropMode = true;
        _editTool = ViewerEditTool.Pan;
        SelectAnnotation(null);
        ResetCropControls();
        CropPanel.Visibility = Visibility.Visible;
        UpdateToolButtons();
        RenderAnnotations();
        SetViewerStatus(Strings.Viewer_Status_CropStarted);
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
        SetViewerStatus(Strings.Viewer_Status_CropCancelled);
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
            SetViewerStatus(Strings.Viewer_Status_ColorSelected);
        }
    }

    private void OnImageMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_editableImage is null)
        {
            return;
        }

        // 텍스트 상자 바깥을 누르면 편집을 끝낸다(상자 안 클릭은 TextBox가 처리해 여기로 오지 않는다).
        // 텍스트 도구에서는 이 클릭이 편집 종료만 하고 새 상자를 만들지 않는다.
        var endedTextEditing = IsEditingAnnotationText();
        EndAnnotationTextEditing();
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
            if (_pixelSelection is { } selection && ContainsPixel(selection, DisplayToPixel(position)))
            {
                _isMovingPixelSelection = true;
                _drawStartPoint = position;
                _pixelSelectionStartRect = selection;
                AnnotationOverlay.CaptureMouse();
                AnnotationOverlay.Cursor = WpfCursors.SizeAll;
                e.Handled = true;
                return;
            }

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
            if (!endedTextEditing)
            {
                AddTextAnnotation(position);
            }

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

        if (_isMovingPixelSelection)
        {
            UpdatePixelSelectionDrag(ClampToSurface(e.GetPosition(AnnotationOverlay)));
            e.Handled = true;
            return;
        }

        if (!_isDrawing)
        {
            UpdateHoverCursor(ClampToSurface(e.GetPosition(AnnotationOverlay)));
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

    // 화면 기준 이 크기보다 작게 움직였으면 드래그가 아니라 클릭으로 본다(손떨림으로 작은 객체가 생기는 것 방지).
    private const double ClickGestureDisplayPixels = 4;

    internal static bool IsClickGesture(WpfRect displayExtent) =>
        displayExtent.Width < ClickGestureDisplayPixels && displayExtent.Height < ClickGestureDisplayPixels;

    private void SelectByClick(EditableAnnotation? annotation)
    {
        SelectAnnotation(annotation);
        SetViewerStatus(annotation is null
            ? Strings.Viewer_Status_SelectionClearedDrawHint
            : Strings.Viewer_Status_SelectedDrawHint);
    }

    // 픽셀 영역 도구에서 영역 안에 올리면 이동 커서를 보여 준다. 객체 위 이동 커서는 요소 Cursor가 맡는다.
    private void UpdateHoverCursor(WpfPoint displayPoint)
    {
        if (_editTool != ViewerEditTool.PixelSelect || _spacePanActive)
        {
            return;
        }

        AnnotationOverlay.Cursor = _pixelSelection is { } selection && ContainsPixel(selection, DisplayToPixel(displayPoint))
            ? WpfCursors.SizeAll
            : WpfCursors.Cross;
    }

    private void OnImageMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 누른 객체 기억은 이번 누름 한 번에만 유효하다(팬·이동 등 다른 경로로 끝나도 비운다).
        var pressedAnnotation = _pressedAnnotation;
        _pressedAnnotation = null;
        if (_isPanning)
        {
            _isPanning = false;
            AnnotationOverlay.ReleaseMouseCapture();
            AnnotationOverlay.Cursor = GetToolCursor();
            ScheduleCurrentImageViewStateSave();
            e.Handled = true;
            return;
        }

        if (_isMovingAnnotation || _isResizingAnnotation)
        {
            EndAnnotationDrag();
            e.Handled = true;
            return;
        }

        if (_isMovingPixelSelection)
        {
            _isMovingPixelSelection = false;
            AnnotationOverlay.ReleaseMouseCapture();
            UpdateHoverCursor(ClampToSurface(e.GetPosition(AnnotationOverlay)));
            if (_pixelSelection is { } moved)
            {
                SetViewerStatus(string.Format(Strings.Viewer_Status_PixelRegion, moved.Width, moved.Height, moved.X, moved.Y));
            }

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
            if (IsClickGesture(GetPixelPointsBounds(_penPoints)))
            {
                SelectByClick(pressedAnnotation);
            }
            else
            {
                AddPenAnnotation(_penPoints);
            }

            _penPoints.Clear();
            _isDrawing = false;
            AnnotationOverlay.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        var rect = GetPixelRect(_drawStartPoint, endPoint);
        RemovePreviewShape();

        // 그리기 도구에서 짧은 클릭은 새로 만들지 않고, 누른 객체를 선택(빈 곳이면 해제)한다. 끌어야 새로 그린다.
        if (_editTool != ViewerEditTool.PixelSelect && IsClickGesture(new WpfRect(_drawStartPoint, endPoint)))
        {
            SelectByClick(pressedAnnotation);
            _isDrawing = false;
            AnnotationOverlay.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_editTool == ViewerEditTool.PixelSelect)
        {
            _pixelSelection = rect.Width >= 2 && rect.Height >= 2 ? rect : null;
            _isDrawing = false;
            AnnotationOverlay.ReleaseMouseCapture();
            RenderAnnotations();
            UpdateToolContext();
            SetViewerStatus(_pixelSelection is null
                ? Strings.Viewer_Status_PixelRegionCleared
                : string.Format(Strings.Viewer_Status_PixelRegionHint, rect.Width, rect.Height));
            e.Handled = true;
            return;
        }

        if (rect.Width >= 2 && rect.Height >= 2)
        {
            if (_editTool == ViewerEditTool.Mosaic)
            {
                AddMosaicAnnotation(rect);
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
