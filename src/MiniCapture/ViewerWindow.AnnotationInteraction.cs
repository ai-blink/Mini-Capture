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

        SelectAnnotation(annotation);
        if (_editTool != ViewerEditTool.Select || Keyboard.FocusedElement is System.Windows.Controls.TextBox)
        {
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

        _isMovingAnnotation = false;
        _isResizingAnnotation = false;
        AnnotationOverlay.ReleaseMouseCapture();
        MarkAnnotationChanged("선택 항목을 수정했습니다.");
        e.Handled = true;
    }

    private void OnResizeHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selectedAnnotation is null)
        {
            return;
        }

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
            var newWidth = Math.Max(8, _annotationStartBounds.Width + delta.X);
            var newHeight = Math.Max(8, _annotationStartBounds.Height + delta.Y);
            _selectedAnnotation.PixelBounds = ClampPixelRect(new WpfRect(_annotationStartBounds.X, _annotationStartBounds.Y, newWidth, newHeight));
            ResizeAnnotationPoints(_selectedAnnotation, newWidth, newHeight);
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

    private void ResizeAnnotationPoints(EditableAnnotation annotation, double newWidth, double newHeight)
    {
        if (annotation.Kind is not (ViewerAnnotationKind.Pen or ViewerAnnotationKind.Arrow) || _annotationStartPoints.Count == 0)
        {
            return;
        }

        var scaleX = newWidth / Math.Max(1, _annotationStartBounds.Width);
        var scaleY = newHeight / Math.Max(1, _annotationStartBounds.Height);
        annotation.PixelPoints.Clear();
        foreach (var point in _annotationStartPoints)
        {
            annotation.PixelPoints.Add(new WpfPoint(
                _annotationStartBounds.X + ((point.X - _annotationStartBounds.X) * scaleX),
                _annotationStartBounds.Y + ((point.Y - _annotationStartBounds.Y) * scaleY)));
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
