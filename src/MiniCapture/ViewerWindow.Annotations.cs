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

    private void AddMosaicAnnotation(Int32Rect pixelRect)
    {
        var annotation = new EditableAnnotation
        {
            Kind = ViewerAnnotationKind.Mosaic,
            Color = _selectedColor,
            StrokeThickness = 1,
            PixelBounds = new WpfRect(pixelRect.X, pixelRect.Y, pixelRect.Width, pixelRect.Height),
            MosaicType = _mosaicType,
            MosaicBlockSize = _mosaicBlockSize
        };

        AddAnnotation(annotation, (_mosaicType == ViewerMosaicType.Blur ? "흐림 모자이크를 추가했습니다." : "모자이크를 추가했습니다.") +
            " 핸들로 크기를, 위 설정 줄로 크기·종류를 바로 바꿀 수 있습니다. 옮기려면 객체 선택(V)을 쓰세요.");
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
        // 캡처 편집기 관례(Greenshot·ShareX): 방금 그린 객체는 도구를 바꾸지 않고 선택 상태로 두어
        // 핸들·설정 줄로 바로 고칠 수 있게 한다. 본문 드래그 이동은 객체 선택 도구에서만.
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
        element.Cursor = GetAnnotationCursor(element);
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
            ViewerAnnotationKind.Mosaic => CreateMosaicElement(annotation),
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

    private System.Windows.Controls.Image CreateMosaicElement(EditableAnnotation annotation)
    {
        var image = new System.Windows.Controls.Image
        {
            Source = GetMosaicPatch(annotation, out _),
            Stretch = Stretch.Fill
        };
        AutomationProperties.SetAutomationId(image, "ViewerMosaicObject");
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        return image;
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
        textBox.GotKeyboardFocus += (_, _) =>
        {
            textBox.Cursor = null;
            SelectAnnotation(annotation);
        };
        textBox.LostKeyboardFocus += (_, _) => textBox.Cursor = GetAnnotationCursor(textBox);
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
        if (annotation is { Kind: ViewerAnnotationKind.Mosaic })
        {
            // 선택한 모자이크의 값을 설정 줄에 반영한다. 슬라이더 핸들러는 값이 같으면 객체를 건드리지 않는다.
            _mosaicType = annotation.MosaicType;
            MosaicSizeSlider.Value = annotation.MosaicBlockSize;
        }

        RenderSelectionFrame();
        UpdateEditButtons();
        UpdateToolContext();
    }

    private void RenderSelectionFrame()
    {
        if (_selectionFrame is not null)
        {
            AnnotationOverlay.Children.Remove(_selectionFrame);
            _selectionFrame = null;
        }

        foreach (var handle in _resizeHandles)
        {
            AnnotationOverlay.Children.Remove(handle);
        }

        _resizeHandles.Clear();
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

        // 네 모서리 + 네 변 중앙, 8개 핸들(Greenshot·Flameshot 관례). 어느 도구에서든 선택된 객체의 크기를 바꾼다.
        foreach (var edges in ResizeHandleEdges)
        {
            var handle = new WpfRectangle
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(WpfColor.FromRgb(0, 120, 212)),
                Stroke = WpfBrushes.White,
                StrokeThickness = 1,
                Cursor = GetResizeCursor(edges),
                ToolTip = "끌어서 크기 변경",
                Tag = edges
            };
            AutomationProperties.SetName(handle, "크기 변경 핸들");
            handle.PreviewMouseLeftButtonDown += OnResizeHandleMouseLeftButtonDown;
            var x = edges.Horizontal switch { < 0 => rect.Left, 0 => rect.Left + rect.Width / 2, _ => rect.Right };
            var y = edges.Vertical switch { < 0 => rect.Top, 0 => rect.Top + rect.Height / 2, _ => rect.Bottom };
            Canvas.SetLeft(handle, x - 5);
            Canvas.SetTop(handle, y - 5);
            AnnotationOverlay.Children.Add(handle);
            _resizeHandles.Add(handle);
        }
    }

    private static readonly ResizeEdges[] ResizeHandleEdges =
    [
        new(-1, -1), new(0, -1), new(1, -1),
        new(1, 0), new(1, 1), new(0, 1),
        new(-1, 1), new(-1, 0)
    ];

    private static System.Windows.Input.Cursor GetResizeCursor(ResizeEdges edges) => edges switch
    {
        { Horizontal: 0 } => WpfCursors.SizeNS,
        { Vertical: 0 } => WpfCursors.SizeWE,
        _ when edges.Horizontal == edges.Vertical => WpfCursors.SizeNWSE,
        _ => WpfCursors.SizeNESW
    };

}
