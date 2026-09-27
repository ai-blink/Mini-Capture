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
    private void OnAnnotationElementMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: EditableAnnotation annotation })
        {
            return;
        }

        // 본문 드래그 이동은 객체 선택 도구에서만 한다. 그리기 도구에서는 누른 객체만 기억하고 캔버스로 넘겨,
        // 놓을 때 클릭이면 선택, 끌었으면 새로 그린다(텍스트 상자 자체 클릭은 상자가 처리).
        if (_editTool != ViewerEditTool.Select)
        {
            if (_editTool == ViewerEditTool.Text && annotation.Element is not System.Windows.Controls.TextBox)
            {
                // 텍스트 도구는 누르는 순간 상자를 만들므로, 기존 객체 위 클릭은 새 상자 대신 선택으로 처리한다.
                SelectByClick(annotation);
                e.Handled = true;
            }
            else if (_editTool is not (ViewerEditTool.Pan or ViewerEditTool.PixelSelect))
            {
                _pressedAnnotation = annotation;
            }

            return;
        }

        SelectAnnotation(annotation);
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox)
        {
            return;
        }

        // 텍스트는 더블클릭하면 편집으로 들어간다(Greenshot·Flameshot·ksnip 관례). 한 번 클릭·드래그는 이동.
        if (e.ClickCount >= 2 && annotation.Element is System.Windows.Controls.TextBox textBox)
        {
            _isMovingAnnotation = false;
            AnnotationOverlay.ReleaseMouseCapture();
            textBox.Focus();
            textBox.CaretIndex = textBox.Text.Length;
            SetViewerStatus("텍스트 편집 중: 입력을 마치면 Esc를 누르거나 바깥을 클릭하세요.");
            e.Handled = true;
            return;
        }

        _isMovingAnnotation = true;
        _annotationDragStartPoint = ClampToSurface(e.GetPosition(AnnotationOverlay));
        _annotationStartBounds = GetAnnotationBounds(annotation);
        _annotationStartPoints = annotation.PixelPoints.ToList();
        AnnotationOverlay.CaptureMouse();
        e.Handled = true;
    }

    private void OnAnnotationElementMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isMovingAnnotation && !_isResizingAnnotation)
        {
            return;
        }

        UpdateSelectedAnnotationDrag(ClampToSurface(e.GetPosition(AnnotationOverlay)));
        e.Handled = true;
    }

    private void OnAnnotationElementMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMovingAnnotation && !_isResizingAnnotation)
        {
            return;
        }

        EndAnnotationDrag();
        e.Handled = true;
    }

    private void EndAnnotationDrag()
    {
        _isMovingAnnotation = false;
        _isResizingAnnotation = false;
        AnnotationOverlay.ReleaseMouseCapture();
        // 클릭만 하고 움직이지 않았으면 수정으로 치지 않는다(더블클릭 편집 진입 전 첫 클릭 포함).
        if (_selectedAnnotation is not null && GetAnnotationBounds(_selectedAnnotation) != _annotationStartBounds)
        {
            MarkAnnotationChanged("선택 항목을 수정했습니다.");
        }
    }

    private void OnResizeHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selectedAnnotation is null || sender is not FrameworkElement { Tag: ResizeEdges edges })
        {
            return;
        }

        _activeResizeEdges = edges;
        _isResizingAnnotation = true;
        _annotationDragStartPoint = ClampToSurface(e.GetPosition(AnnotationOverlay));
        _annotationStartBounds = GetAnnotationBounds(_selectedAnnotation);
        _annotationStartPoints = _selectedAnnotation.PixelPoints.ToList();
        AnnotationOverlay.CaptureMouse();
        e.Handled = true;
    }

    private void UpdateSelectedAnnotationDrag(WpfPoint displayPoint)
    {
        if (_selectedAnnotation is null || _editableImage is null)
        {
            return;
        }

        var startPixel = DisplayToPixel(_annotationDragStartPoint);
        var currentPixel = DisplayToPixel(displayPoint);
        var delta = currentPixel - startPixel;

        if (_isResizingAnnotation)
        {
            var resized = ClampPixelRect(ResizeBounds(_annotationStartBounds, _activeResizeEdges, delta, 8));
            _selectedAnnotation.PixelBounds = resized;
            ResizeAnnotationPoints(_selectedAnnotation, resized);
        }
        else if (_isMovingAnnotation)
        {
            MoveAnnotation(_selectedAnnotation, delta);
        }

        RefreshAnnotationVisual(_selectedAnnotation);
        RenderSelectionFrame();
    }

    private void MoveAnnotation(EditableAnnotation annotation, Vector delta)
    {
        annotation.PixelBounds = ClampPixelRect(new WpfRect(
            _annotationStartBounds.X + delta.X,
            _annotationStartBounds.Y + delta.Y,
            _annotationStartBounds.Width,
            _annotationStartBounds.Height));

        if (annotation.Kind is ViewerAnnotationKind.Pen or ViewerAnnotationKind.Arrow)
        {
            for (var i = 0; i < annotation.PixelPoints.Count; i++)
            {
                var startPoint = i < _annotationStartPoints.Count ? _annotationStartPoints[i] : annotation.PixelPoints[i];
                annotation.PixelPoints[i] = new WpfPoint(
                    Math.Clamp(startPoint.X + delta.X, 0, _editableImage?.PixelWidth ?? 1),
                    Math.Clamp(startPoint.Y + delta.Y, 0, _editableImage?.PixelHeight ?? 1));
            }
        }
    }

    // 잡은 핸들의 변만 움직이고 반대편 변은 고정한다. 최소 크기에서 멈추며 뒤집히지 않는다.
    internal static WpfRect ResizeBounds(WpfRect start, ResizeEdges edges, Vector delta, double minSize)
    {
        var left = start.Left;
        var top = start.Top;
        var right = start.Right;
        var bottom = start.Bottom;

        if (edges.Horizontal < 0)
        {
            left = Math.Min(start.Left + delta.X, start.Right - minSize);
        }
        else if (edges.Horizontal > 0)
        {
            right = Math.Max(start.Right + delta.X, start.Left + minSize);
        }

        if (edges.Vertical < 0)
        {
            top = Math.Min(start.Top + delta.Y, start.Bottom - minSize);
        }
        else if (edges.Vertical > 0)
        {
            bottom = Math.Max(start.Bottom + delta.Y, start.Top + minSize);
        }

        return new WpfRect(left, top, right - left, bottom - top);
    }

    private void ResizeAnnotationPoints(EditableAnnotation annotation, WpfRect newBounds)
    {
        if (annotation.Kind is not (ViewerAnnotationKind.Pen or ViewerAnnotationKind.Arrow) || _annotationStartPoints.Count == 0)
        {
            return;
        }

        var scaleX = newBounds.Width / Math.Max(1, _annotationStartBounds.Width);
        var scaleY = newBounds.Height / Math.Max(1, _annotationStartBounds.Height);
        annotation.PixelPoints.Clear();
        foreach (var point in _annotationStartPoints)
        {
            annotation.PixelPoints.Add(new WpfPoint(
                newBounds.X + ((point.X - _annotationStartBounds.X) * scaleX),
                newBounds.Y + ((point.Y - _annotationStartBounds.Y) * scaleY)));
        }

        annotation.PixelBounds = GetPixelPointsBounds(annotation.PixelPoints);
    }

    private void RefreshAnnotationVisual(EditableAnnotation annotation)
    {
        if (annotation.Element is WpfPath path)
        {
            path.Data = annotation.Kind == ViewerAnnotationKind.Arrow
                ? CreateDisplayArrowGeometry(annotation)
                : CreateDisplayPenGeometry(annotation);
        }
        else if (annotation.Element is System.Windows.Controls.Image mosaicImage && annotation.Kind == ViewerAnnotationKind.Mosaic)
        {
            mosaicImage.Source = GetMosaicPatch(annotation, out _);
        }

        PositionAnnotationElement(annotation);
    }

    private void DeleteSelectedAnnotation()
    {
        if (_selectedAnnotation is null)
        {
            return;
        }

        if (_selectedAnnotation.Element is not null)
        {
            AnnotationOverlay.Children.Remove(_selectedAnnotation.Element);
        }

        _annotations.Remove(_selectedAnnotation);
        SelectAnnotation(null);
        MarkAnnotationChanged("선택 항목을 삭제했습니다.");
    }

    private void ClearAnnotations()
    {
        _annotations.Clear();
        SelectAnnotation(null);
        AnnotationOverlay.Children.Clear();
    }

    private void MarkAnnotationChanged(string status)
    {
        _isDirty = true;
        UpdateDirtyIndicator();
        UpdateEditButtons();
        SetViewerStatus(status);
    }

    private void CommitAnnotationsToBitmap(string status, bool pushUndo)
    {
        if (_editableImage is null || _annotations.Count == 0)
        {
            return;
        }

        if (pushUndo)
        {
            PushUndoSnapshot();
        }

        SetEditableImage(ComposeImageForExport(), status);
        ClearAnnotations();
    }

}
