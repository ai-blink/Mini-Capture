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
    private void AddShapeAnnotation(Int32Rect pixelRect, bool ellipse)
    {
        var annotation = new EditableAnnotation
        {
            Kind = ellipse ? ViewerAnnotationKind.Ellipse : ViewerAnnotationKind.Rectangle,
            Color = _selectedColor,
            StrokeThickness = GetPixelStrokeThickness(),
            PixelBounds = new WpfRect(pixelRect.X, pixelRect.Y, pixelRect.Width, pixelRect.Height)
        };

        AddAnnotation(annotation, ellipse ? "동그라미를 추가했습니다." : "네모를 추가했습니다.");
    }

    private void AddTextAnnotation(WpfPoint displayPoint)
    {
        if (_editableImage is null)
        {
            return;
        }

        var pixelPoint = DisplayToPixel(displayPoint);
        var text = string.IsNullOrWhiteSpace(AnnotationTextBox.Text)
            ? "Text"
            : AnnotationTextBox.Text.Trim();
        var annotation = new EditableAnnotation
        {
            Kind = ViewerAnnotationKind.Text,
            Color = _selectedColor,
            StrokeThickness = GetPixelStrokeThickness(),
            PixelBounds = new WpfRect(
                pixelPoint.X,
                pixelPoint.Y,
                Math.Min(320, Math.Max(120, _editableImage.PixelWidth - pixelPoint.X)),
                56),
            Text = text
        };

        AddAnnotation(annotation, "텍스트 상자를 추가했습니다.");
        if (annotation.Element is System.Windows.Controls.TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void AddPenAnnotation(IReadOnlyList<WpfPoint> displayPoints)
    {
        if (_editableImage is null || displayPoints.Count < 2)
        {
            return;
        }

        var annotation = new EditableAnnotation
        {
            Kind = ViewerAnnotationKind.Pen,
            Color = _selectedColor,
            StrokeThickness = GetPixelStrokeThickness()
        };
        foreach (var point in displayPoints)
        {
            annotation.PixelPoints.Add(DisplayToPixel(point));
        }

        annotation.PixelBounds = GetPixelPointsBounds(annotation.PixelPoints);
        AddAnnotation(annotation, "펜 선을 추가했습니다.");
    }

    private void AddArrowAnnotation(WpfPoint displayStart, WpfPoint displayEnd)
    {
        var annotation = new EditableAnnotation
        {
            Kind = ViewerAnnotationKind.Arrow,
            Color = _selectedColor,
            StrokeThickness = GetPixelStrokeThickness()
        };
        annotation.PixelPoints.Add(DisplayToPixel(displayStart));
        annotation.PixelPoints.Add(DisplayToPixel(displayEnd));
        annotation.PixelBounds = GetPixelPointsBounds(annotation.PixelPoints);
        AddAnnotation(annotation, "화살표를 추가했습니다.");
    }

    private void AddAnnotation(EditableAnnotation annotation, string status)
    {
        _annotations.Add(annotation);
        RenderAnnotation(annotation);
        SelectAnnotation(annotation);
        MarkAnnotationChanged(status);
    }

    private void RenderAnnotations()
    {
        AnnotationOverlay.Children.Clear();
        foreach (var annotation in _annotations)
        {
            RenderAnnotation(annotation);
        }

        RenderCropPreview();
        RenderSelectionFrame();
        RenderPixelSelectionFrame();
    }

    private void RenderCropPreview()
    {
        _cropPreviewMask = null;
        _cropPreviewFrame = null;
        if (!_isCropMode || _editableImage is null || AnnotationOverlay.ActualWidth <= 0 || AnnotationOverlay.ActualHeight <= 0)
        {
            return;
        }

        var crop = GetCropPixelRect();
        var rect = PixelToDisplayRect(new WpfRect(crop.X, crop.Y, crop.Width, crop.Height));
        var maskGeometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
        maskGeometry.Children.Add(new RectangleGeometry(new WpfRect(0, 0, AnnotationOverlay.ActualWidth, AnnotationOverlay.ActualHeight)));
        maskGeometry.Children.Add(new RectangleGeometry(rect));
        _cropPreviewMask = new WpfPath
        {
            Data = maskGeometry,
            Fill = new SolidColorBrush(WpfColor.FromArgb(150, 8, 15, 15)),
            IsHitTestVisible = false
        };
        _cropPreviewFrame = new WpfRectangle
        {
            Width = Math.Max(1, rect.Width),
            Height = Math.Max(1, rect.Height),
            Stroke = new SolidColorBrush(WpfColor.FromRgb(94, 234, 212)),
            StrokeThickness = 1.5,
            Fill = WpfBrushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_cropPreviewFrame, rect.Left);
        Canvas.SetTop(_cropPreviewFrame, rect.Top);
        AnnotationOverlay.Children.Add(_cropPreviewMask);
        AnnotationOverlay.Children.Add(_cropPreviewFrame);
    }

    private void RenderPixelSelectionFrame()
    {
        _pixelSelectionFrame = null;
        if (_pixelSelection is not { } selection || _editableImage is null)
        {
            return;
        }

        var rect = PixelToDisplayRect(new WpfRect(selection.X, selection.Y, selection.Width, selection.Height));
        _pixelSelectionFrame = new WpfRectangle
        {
            Width = Math.Max(1, rect.Width),
            Height = Math.Max(1, rect.Height),
            Stroke = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = WpfBrushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_pixelSelectionFrame, rect.Left);
        Canvas.SetTop(_pixelSelectionFrame, rect.Top);
        AnnotationOverlay.Children.Add(_pixelSelectionFrame);
    }

    private void RenderAnnotation(EditableAnnotation annotation)
    {
        var element = CreateAnnotationElement(annotation);
        annotation.Element = element;
        element.Tag = annotation;
        element.PreviewMouseLeftButtonDown += OnAnnotationElementMouseLeftButtonDown;
        element.PreviewMouseMove += OnAnnotationElementMouseMove;
        element.PreviewMouseLeftButtonUp += OnAnnotationElementMouseLeftButtonUp;
        AnnotationOverlay.Children.Add(element);
        PositionAnnotationElement(annotation);
    }

    private FrameworkElement CreateAnnotationElement(EditableAnnotation annotation)
    {
        var brush = new SolidColorBrush(annotation.Color);
        return annotation.Kind switch
        {
            ViewerAnnotationKind.Ellipse => new WpfEllipse
            {
                Stroke = brush,
                StrokeThickness = Math.Max(1, annotation.StrokeThickness * _zoom),
                Fill = WpfBrushes.Transparent
            },
            ViewerAnnotationKind.Text => CreateAnnotationTextBox(annotation, brush),
            ViewerAnnotationKind.Pen => new WpfPath
            {
                Stroke = brush,
                StrokeThickness = Math.Max(1, annotation.StrokeThickness * _zoom),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = WpfBrushes.Transparent,
                Data = CreateDisplayPenGeometry(annotation)
            },
            ViewerAnnotationKind.Arrow => new WpfPath
            {
                Stroke = brush,
                StrokeThickness = Math.Max(1, annotation.StrokeThickness * _zoom),
                Fill = brush,
                Data = CreateDisplayArrowGeometry(annotation)
            },
            _ => new WpfRectangle
            {
                Stroke = brush,
                StrokeThickness = Math.Max(1, annotation.StrokeThickness * _zoom),
                Fill = WpfBrushes.Transparent
            }
        };
    }

    private System.Windows.Controls.TextBox CreateAnnotationTextBox(EditableAnnotation annotation, System.Windows.Media.Brush brush)
    {
        var textBox = new System.Windows.Controls.TextBox
        {
            AcceptsReturn = true,
            Background = new SolidColorBrush(WpfColor.FromArgb(42, 0, 0, 0)),
            BorderBrush = brush,
            BorderThickness = new Thickness(1),
            Foreground = brush,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = Math.Clamp(annotation.StrokeThickness * 5 * _zoom, 12, 96),
            MinWidth = 48,
            MinHeight = 28,
            Padding = new Thickness(6, 3, 6, 3),
            Text = annotation.Text,
            TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetAutomationId(textBox, "ViewerEditableTextBox");
        textBox.TextChanged += (_, _) =>
        {
            annotation.Text = textBox.Text;
            MarkAnnotationChanged("텍스트를 수정했습니다.");
        };
        textBox.GotKeyboardFocus += (_, _) => SelectAnnotation(annotation);
        return textBox;
    }

    private void PositionAnnotationElement(EditableAnnotation annotation)
    {
        if (annotation.Element is null)
        {
            return;
        }

        if (annotation.Kind is ViewerAnnotationKind.Pen or ViewerAnnotationKind.Arrow)
        {
            Canvas.SetLeft(annotation.Element, 0);
            Canvas.SetTop(annotation.Element, 0);
            annotation.Element.Width = AnnotationOverlay.Width;
            annotation.Element.Height = AnnotationOverlay.Height;
            return;
        }

        var rect = PixelToDisplayRect(annotation.PixelBounds);
        Canvas.SetLeft(annotation.Element, rect.Left);
        Canvas.SetTop(annotation.Element, rect.Top);
        annotation.Element.Width = Math.Max(1, rect.Width);
        annotation.Element.Height = Math.Max(1, rect.Height);
    }

    private void SelectAnnotation(EditableAnnotation? annotation)
    {
        _selectedAnnotation = annotation;
        RenderSelectionFrame();
        UpdateEditButtons();
    }

    private void RenderSelectionFrame()
    {
        if (_selectionFrame is not null)
        {
            AnnotationOverlay.Children.Remove(_selectionFrame);
            _selectionFrame = null;
        }

        if (_resizeHandle is not null)
        {
            AnnotationOverlay.Children.Remove(_resizeHandle);
            _resizeHandle = null;
        }

        if (_selectedAnnotation is null)
        {
            return;
        }

        var rect = PixelToDisplayRect(GetAnnotationBounds(_selectedAnnotation));
        _selectionFrame = new WpfRectangle
        {
            Stroke = new SolidColorBrush(WpfColor.FromRgb(0, 120, 212)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = WpfBrushes.Transparent,
            IsHitTestVisible = false,
            Width = Math.Max(1, rect.Width),
            Height = Math.Max(1, rect.Height)
        };
        Canvas.SetLeft(_selectionFrame, rect.Left);
        Canvas.SetTop(_selectionFrame, rect.Top);
        AnnotationOverlay.Children.Add(_selectionFrame);

        _resizeHandle = new WpfRectangle
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(WpfColor.FromRgb(0, 120, 212)),
            Stroke = WpfBrushes.White,
            StrokeThickness = 1,
            Cursor = WpfCursors.SizeNWSE
        };
        _resizeHandle.PreviewMouseLeftButtonDown += OnResizeHandleMouseLeftButtonDown;
        Canvas.SetLeft(_resizeHandle, rect.Right - 6);
        Canvas.SetTop(_resizeHandle, rect.Bottom - 6);
        AnnotationOverlay.Children.Add(_resizeHandle);
    }

}
