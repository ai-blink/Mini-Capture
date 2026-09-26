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
    private BitmapSource ComposeImageForExport()
    {
        if (_editableImage is null)
        {
            throw new InvalidOperationException("No image is loaded.");
        }

        var source = ConvertToPbgra32(_editableImage);
        if (_annotations.Count == 0)
        {
            return source;
        }

        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(source, new WpfRect(0, 0, source.PixelWidth, source.PixelHeight));
            foreach (var annotation in _annotations)
            {
                DrawAnnotation(drawing, annotation);
            }
        }

        return RenderBitmap(visual, source.PixelWidth, source.PixelHeight);
    }

    private void DrawAnnotation(DrawingContext drawing, EditableAnnotation annotation)
    {
        var brush = new SolidColorBrush(annotation.Color);
        var pen = new WpfPen(brush, Math.Max(1, annotation.StrokeThickness))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        switch (annotation.Kind)
        {
            case ViewerAnnotationKind.Ellipse:
                drawing.DrawEllipse(null, pen, new WpfPoint(annotation.PixelBounds.X + annotation.PixelBounds.Width / 2, annotation.PixelBounds.Y + annotation.PixelBounds.Height / 2), annotation.PixelBounds.Width / 2, annotation.PixelBounds.Height / 2);
                break;
            case ViewerAnnotationKind.Text:
                var formatted = new FormattedText(
                    annotation.Text,
                    CultureInfo.CurrentCulture,
                    WpfFlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    Math.Clamp(annotation.StrokeThickness * 5, 12, 96),
                    brush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip)
                {
                    MaxTextWidth = Math.Max(1, annotation.PixelBounds.Width)
                };
                drawing.DrawText(formatted, annotation.PixelBounds.TopLeft);
                break;
            case ViewerAnnotationKind.Pen:
                drawing.DrawGeometry(null, pen, CreatePixelPenGeometry(annotation));
                break;
            case ViewerAnnotationKind.Mosaic:
                var patch = GetMosaicPatch(annotation, out var region);
                drawing.DrawImage(patch, new WpfRect(region.X, region.Y, region.Width, region.Height));
                break;
            case ViewerAnnotationKind.Arrow:
                if (annotation.PixelPoints.Count >= 2)
                {
                    drawing.DrawLine(pen, annotation.PixelPoints[0], annotation.PixelPoints[1]);
                    DrawArrowHead(drawing, annotation.PixelPoints[0], annotation.PixelPoints[1], brush, annotation.StrokeThickness);
                }
                break;
            default:
                drawing.DrawRectangle(null, pen, annotation.PixelBounds);
                break;
        }
    }

    // 모자이크 객체는 픽셀을 굽지 않고, 현재 원본 이미지의 해당 영역으로 조각을 만들어 위에 덮는다.
    // 저장·복사 때 ComposeImageForExport가 같은 조각을 합성한다.
    private BitmapSource GetMosaicPatch(EditableAnnotation annotation, out Int32Rect region)
    {
        var image = _editableImage ?? throw new InvalidOperationException("No image is loaded.");
        region = ToMosaicRegion(annotation.PixelBounds, image.PixelWidth, image.PixelHeight);
        var key = (region, annotation.MosaicType, annotation.MosaicBlockSize, image);
        if (annotation.MosaicPatch is null || !Equals(annotation.MosaicPatchKey, key))
        {
            annotation.MosaicPatch = CreateMosaicPatch(image, region, annotation.MosaicType, annotation.MosaicBlockSize);
            annotation.MosaicPatchKey = key;
        }

        return annotation.MosaicPatch;
    }

    private static Int32Rect ToMosaicRegion(WpfRect bounds, int imageWidth, int imageHeight)
    {
        var x = Math.Clamp((int)Math.Floor(bounds.X), 0, imageWidth - 1);
        var y = Math.Clamp((int)Math.Floor(bounds.Y), 0, imageHeight - 1);
        var right = Math.Clamp((int)Math.Ceiling(bounds.Right), x + 1, imageWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(bounds.Bottom), y + 1, imageHeight);
        return new Int32Rect(x, y, right - x, bottom - y);
    }

    internal static BitmapSource CreateMosaicPatch(BitmapSource image, Int32Rect region, ViewerMosaicType mosaicType, int blockSize)
    {
        var stride = region.Width * 4;
        var pixels = new byte[stride * region.Height];
        ConvertToBgra32(image).CopyPixels(region, pixels, stride, 0);

        blockSize = Math.Max(1, blockSize);
        if (mosaicType == ViewerMosaicType.Blur)
        {
            ApplyGaussianBlurRegion(pixels, stride, 0, 0, region.Width, region.Height, blockSize / 2.0);
        }
        else
        {
            for (var y = 0; y < region.Height; y += blockSize)
            {
                for (var x = 0; x < region.Width; x += blockSize)
                {
                    ApplyMosaicBlock(pixels, stride, x, y, Math.Min(region.Width, x + blockSize), Math.Min(region.Height, y + blockSize));
                }
            }
        }

        var patch = new WriteableBitmap(region.Width, region.Height, 96, 96, PixelFormats.Bgra32, null);
        patch.WritePixels(new Int32Rect(0, 0, region.Width, region.Height), pixels, stride, 0);
        patch.Freeze();
        return patch;
    }

    private static void DrawArrowHead(DrawingContext drawing, WpfPoint start, WpfPoint end, System.Windows.Media.Brush brush, double strokeThickness)
    {
        var vector = start - end;
        if (vector.Length < 1)
        {
            return;
        }

        vector.Normalize();
        var headLength = Math.Max(10, strokeThickness * 4.5);
        var headWidth = Math.Max(7, strokeThickness * 2.8);
        var perpendicular = new Vector(-vector.Y, vector.X);
        var p1 = end + (vector * headLength) + (perpendicular * headWidth);
        var p2 = end + (vector * headLength) - (perpendicular * headWidth);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(end, isFilled: true, isClosed: true);
            context.LineTo(p1, isStroked: true, isSmoothJoin: true);
            context.LineTo(p2, isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();
        drawing.DrawGeometry(brush, null, geometry);
    }

    private static void ApplyMosaicBlock(byte[] pixels, int stride, int left, int top, int right, int bottom)
    {
        long blue = 0;
        long green = 0;
        long red = 0;
        long alpha = 0;
        var count = 0;

        for (var y = top; y < bottom; y++)
        {
            var row = y * stride;
            for (var x = left; x < right; x++)
            {
                var offset = row + (x * 4);
                blue += pixels[offset];
                green += pixels[offset + 1];
                red += pixels[offset + 2];
                alpha += pixels[offset + 3];
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        var averageBlue = (byte)(blue / count);
        var averageGreen = (byte)(green / count);
        var averageRed = (byte)(red / count);
        var averageAlpha = (byte)(alpha / count);

        for (var y = top; y < bottom; y++)
        {
            var row = y * stride;
            for (var x = left; x < right; x++)
            {
                var offset = row + (x * 4);
                pixels[offset] = averageBlue;
                pixels[offset + 1] = averageGreen;
                pixels[offset + 2] = averageRed;
                pixels[offset + 3] = averageAlpha;
            }
        }
    }

    internal static void ApplyGaussianBlurRegion(byte[] pixels, int stride, int left, int top, int right, int bottom, double radius)
    {
        if (right <= left || bottom <= top)
        {
            return;
        }

        var sigma = Math.Max(1.0, radius / 2.0);
        var kernelRadius = Math.Max(1, (int)Math.Ceiling(radius));
        var kernel = BuildGaussianKernel(kernelRadius, sigma);
        var regionWidth = right - left;
        var regionHeight = bottom - top;
        var horizontal = new byte[regionWidth * regionHeight * 4];

        for (var y = 0; y < regionHeight; y++)
        {
            var sourceRow = (top + y) * stride;
            var destRow = y * regionWidth * 4;
            for (var x = 0; x < regionWidth; x++)
            {
                double blue = 0;
                double green = 0;
                double red = 0;
                double alpha = 0;
                for (var k = -kernelRadius; k <= kernelRadius; k++)
                {
                    var sampleX = Math.Clamp(x + k, 0, regionWidth - 1) + left;
                    var offset = sourceRow + (sampleX * 4);
                    var weight = kernel[k + kernelRadius];
                    blue += pixels[offset] * weight;
                    green += pixels[offset + 1] * weight;
                    red += pixels[offset + 2] * weight;
                    alpha += pixels[offset + 3] * weight;
                }

                var destOffset = destRow + (x * 4);
                horizontal[destOffset] = (byte)Math.Clamp(Math.Round(blue), 0, 255);
                horizontal[destOffset + 1] = (byte)Math.Clamp(Math.Round(green), 0, 255);
                horizontal[destOffset + 2] = (byte)Math.Clamp(Math.Round(red), 0, 255);
                horizontal[destOffset + 3] = (byte)Math.Clamp(Math.Round(alpha), 0, 255);
            }
        }

        for (var y = 0; y < regionHeight; y++)
        {
            var destRow = (top + y) * stride;
            for (var x = 0; x < regionWidth; x++)
            {
                double blue = 0;
                double green = 0;
                double red = 0;
                double alpha = 0;
                for (var k = -kernelRadius; k <= kernelRadius; k++)
                {
                    var sampleY = Math.Clamp(y + k, 0, regionHeight - 1);
                    var offset = (sampleY * regionWidth * 4) + (x * 4);
                    var weight = kernel[k + kernelRadius];
                    blue += horizontal[offset] * weight;
                    green += horizontal[offset + 1] * weight;
                    red += horizontal[offset + 2] * weight;
                    alpha += horizontal[offset + 3] * weight;
                }

                var destOffset = destRow + ((left + x) * 4);
                pixels[destOffset] = (byte)Math.Clamp(Math.Round(blue), 0, 255);
                pixels[destOffset + 1] = (byte)Math.Clamp(Math.Round(green), 0, 255);
                pixels[destOffset + 2] = (byte)Math.Clamp(Math.Round(red), 0, 255);
                pixels[destOffset + 3] = (byte)Math.Clamp(Math.Round(alpha), 0, 255);
            }
        }
    }

    internal static double[] BuildGaussianKernel(int radius, double sigma)
    {
        var kernel = new double[(radius * 2) + 1];
        var sum = 0.0;
        for (var i = -radius; i <= radius; i++)
        {
            var value = Math.Exp(-(i * i) / (2 * sigma * sigma));
            kernel[i + radius] = value;
            sum += value;
        }

        for (var i = 0; i < kernel.Length; i++)
        {
            kernel[i] /= sum;
        }

        return kernel;
    }
}
