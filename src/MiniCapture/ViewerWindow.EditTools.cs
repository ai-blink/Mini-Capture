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

        if (_selectedAnnotation is { Kind: ViewerAnnotationKind.Mosaic } mosaic &&
            mosaic.MosaicBlockSize != _mosaicBlockSize)
        {
            // 슬라이더를 끄는 동안 쏟아지는 값 변경은 한 동작으로 묶는다.
            PushUndoSnapshot((mosaic, nameof(EditableAnnotation.MosaicBlockSize)));
            mosaic.MosaicBlockSize = _mosaicBlockSize;
            RefreshAnnotationVisual(mosaic);
            MarkAnnotationChanged($"모자이크 크기를 {_mosaicBlockSize}px로 바꿨습니다.");
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
            MarkAnnotationChanged(_mosaicType == ViewerMosaicType.Blur ? "흐림 모자이크로 바꿨습니다." : "블록 모자이크로 바꿨습니다.");
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
        const string moveHint = " 옮기려면 객체 선택(V)을 쓰세요.";
        SetViewerStatus(tool switch
        {
            ViewerEditTool.Pan => "핸드툴(H): 이미지를 끌어 화면을 이동합니다.",
            ViewerEditTool.Select => "객체 선택(V): 도형·텍스트·모자이크를 클릭해 선택하고, 끌어서 옮기고, 테두리 핸들로 크기를 바꿉니다. 텍스트는 더블클릭해 고치고, Delete로 삭제합니다.",
            ViewerEditTool.PixelSelect => "픽셀 영역(M): 드래그해 영역을 잡고, 영역 안을 끌어 옮깁니다. 복사·잘라내기·붙여넣기에 씁니다. Esc로 해제합니다.",
            ViewerEditTool.Rectangle => "네모(R): 드래그해 새 네모를 그립니다." + moveHint,
            ViewerEditTool.Ellipse => "동그라미(E): 드래그해 새 동그라미를 그립니다." + moveHint,
            ViewerEditTool.Mosaic => "모자이크(B): 가릴 영역을 드래그하면 모자이크가 생깁니다. 만든 뒤에도 핸들과 위 설정 줄로 크기·종류를 바꿀 수 있습니다." + moveHint,
            ViewerEditTool.Pen => "펜(P): 드래그해 자유선을 그립니다." + moveHint,
            ViewerEditTool.Arrow => "화살표(A): 드래그해 방향을 표시합니다." + moveHint,
            _ => "텍스트(T): 이미지를 클릭하면 새 텍스트 상자가 생기고 바로 입력합니다." + moveHint
        });
    }

    private bool IsEditingAnnotationText() =>
        Keyboard.FocusedElement is System.Windows.Controls.TextBox { Tag: EditableAnnotation };

    private void EndAnnotationTextEditing()
    {
        if (IsEditingAnnotationText())
        {
            Focus();
            SetViewerStatus("텍스트 편집을 마쳤습니다.");
        }
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
            ? "선택을 해제했습니다. 끌면 새로 그립니다."
            : "선택했습니다. 핸들로 크기를 바꾸고, 옮기려면 객체 선택(V)을 쓰세요. 끌면 새로 그립니다.");
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
                SetViewerStatus($"픽셀 영역: {moved.Width} × {moved.Height}px ({moved.X}, {moved.Y})");
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
                ? "픽셀 영역을 해제했습니다."
                : $"픽셀 영역: {rect.Width} × {rect.Height}px — 영역 안을 끌어 옮기고, Ctrl+C/X로 복사·잘라내기, Esc로 해제합니다.");
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
