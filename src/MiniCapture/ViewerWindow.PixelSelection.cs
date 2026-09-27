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
    private void OnCopySelectionClick(object sender, RoutedEventArgs e)
    {
        CopyPixelSelection();
    }

    private void OnCutSelectionClick(object sender, RoutedEventArgs e)
    {
        CutPixelSelection();
    }

    private void OnPasteSelectionClick(object sender, RoutedEventArgs e)
    {
        PasteIntoPixelSelection();
    }

    private void ClearPixelSelection()
    {
        _pixelSelection = null;
        if (_pixelSelectionFrame is not null)
        {
            AnnotationOverlay.Children.Remove(_pixelSelectionFrame);
            _pixelSelectionFrame = null;
        }
    }

    private void CopyPixelSelection()
    {
        if (!TryGetPixelSelection(out var selection))
        {
            SetViewerStatus(Strings.Viewer_Status_SelectRegionToCopy);
            return;
        }

        try
        {
            CopyPixelSelectionToClipboard(selection);
            SetViewerStatus(string.Format(Strings.Viewer_Status_RegionCopied, selection.Width, selection.Height));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            SetViewerStatus(string.Format(Strings.Viewer_Status_RegionCopyFailed, ex.Message));
        }
    }

    private void CutPixelSelection()
    {
        if (!TryGetPixelSelection(out var selection))
        {
            SetViewerStatus(Strings.Viewer_Status_SelectRegionToCut);
            return;
        }

        try
        {
            CopyPixelSelectionToClipboard(selection);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            SetViewerStatus(string.Format(Strings.Viewer_Status_RegionCopyFailed, ex.Message));
            return;
        }

        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        CommitAnnotationsToBitmap(Strings.Viewer_Status_CommittedBeforeCut);
        SetEditableImage(ClearPixelRectangle(_editableImage, selection), Strings.Viewer_Status_RegionCut);
    }

    private void PasteIntoPixelSelection()
    {
        if (_editableImage is null)
        {
            SetViewerStatus(Strings.Viewer_Status_NothingToPasteInto);
            return;
        }

        BitmapSource? clipboardImage;
        try
        {
            clipboardImage = WpfClipboard.ContainsImage() ? WpfClipboard.GetImage() : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetViewerStatus(string.Format(Strings.Viewer_Status_ClipboardUnreadable, ex.Message));
            return;
        }

        if (clipboardImage is null || clipboardImage.PixelWidth < 1 || clipboardImage.PixelHeight < 1)
        {
            SetViewerStatus(Strings.Viewer_Status_ClipboardNoImage);
            return;
        }

        var target = GetPasteTarget(clipboardImage);
        if (target.Width < 1 || target.Height < 1)
        {
            SetViewerStatus(Strings.Viewer_Status_NoPasteArea);
            return;
        }

        PushUndoSnapshot();
        CommitAnnotationsToBitmap(Strings.Viewer_Status_CommittedBeforePaste);
        _pixelSelection = target;
        SetEditableImage(PasteBitmap(_editableImage, clipboardImage, target), Strings.Viewer_Status_Pasted);
    }

    private void CopyPixelSelectionToClipboard(Int32Rect selection)
    {
        var copied = new CroppedBitmap(ComposeImageForExport(), selection);
        copied.Freeze();
        ClipboardService.SetImage(copied);
    }

    private void UpdatePixelSelectionDrag(WpfPoint displayPoint)
    {
        if (_editableImage is null)
        {
            return;
        }

        var delta = DisplayToPixel(displayPoint) - DisplayToPixel(_drawStartPoint);
        var moved = MovePixelRect(
            _pixelSelectionStartRect,
            (int)Math.Round(delta.X),
            (int)Math.Round(delta.Y),
            _editableImage.PixelWidth,
            _editableImage.PixelHeight);
        _pixelSelection = moved;

        if (_pixelSelectionFrame is not null)
        {
            var rect = PixelToDisplayRect(new WpfRect(moved.X, moved.Y, moved.Width, moved.Height));
            Canvas.SetLeft(_pixelSelectionFrame, rect.Left);
            Canvas.SetTop(_pixelSelectionFrame, rect.Top);
        }
    }

    private static bool ContainsPixel(Int32Rect rect, WpfPoint pixel) =>
        pixel.X >= rect.X && pixel.X < rect.X + rect.Width &&
        pixel.Y >= rect.Y && pixel.Y < rect.Y + rect.Height;

    internal static Int32Rect MovePixelRect(Int32Rect start, int deltaX, int deltaY, int imageWidth, int imageHeight) =>
        new(
            Math.Clamp(start.X + deltaX, 0, Math.Max(0, imageWidth - start.Width)),
            Math.Clamp(start.Y + deltaY, 0, Math.Max(0, imageHeight - start.Height)),
            start.Width,
            start.Height);

    private bool TryGetPixelSelection(out Int32Rect selection)
    {
        if (_editableImage is not null && _pixelSelection is { } current &&
            current.Width > 0 && current.Height > 0)
        {
            selection = current;
            return true;
        }

        selection = new Int32Rect();
        return false;
    }

    private Int32Rect GetPasteTarget(BitmapSource pastedImage)
    {
        if (_editableImage is null)
        {
            return new Int32Rect();
        }

        var sourceWidth = _editableImage.PixelWidth;
        var sourceHeight = _editableImage.PixelHeight;
        var x = _pixelSelection?.X ?? Math.Max(0, (sourceWidth - pastedImage.PixelWidth) / 2);
        var y = _pixelSelection?.Y ?? Math.Max(0, (sourceHeight - pastedImage.PixelHeight) / 2);
        x = Math.Clamp(x, 0, sourceWidth - 1);
        y = Math.Clamp(y, 0, sourceHeight - 1);
        return new Int32Rect(
            x,
            y,
            Math.Min(pastedImage.PixelWidth, sourceWidth - x),
            Math.Min(pastedImage.PixelHeight, sourceHeight - y));
    }

    private static BitmapSource ClearPixelRectangle(BitmapSource source, Int32Rect selection)
    {
        var bitmapSource = ConvertToBgra32(source);
        var width = bitmapSource.PixelWidth;
        var height = bitmapSource.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmapSource.CopyPixels(pixels, stride, 0);

        var right = Math.Min(width, selection.X + selection.Width);
        var bottom = Math.Min(height, selection.Y + selection.Height);
        for (var y = Math.Max(0, selection.Y); y < bottom; y++)
        {
            Array.Clear(pixels, (y * stride) + (Math.Max(0, selection.X) * 4), Math.Max(0, right - selection.X) * 4);
        }

        var cleared = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        cleared.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        cleared.Freeze();
        return cleared;
    }

    private static BitmapSource PasteBitmap(BitmapSource source, BitmapSource pastedImage, Int32Rect target)
    {
        var sourceBitmap = ConvertToPbgra32(source);
        var pastedBitmap = ConvertToPbgra32(pastedImage);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(sourceBitmap, new WpfRect(0, 0, sourceBitmap.PixelWidth, sourceBitmap.PixelHeight));
            drawing.PushClip(new RectangleGeometry(new WpfRect(target.X, target.Y, target.Width, target.Height)));
            drawing.DrawImage(pastedBitmap, new WpfRect(target.X, target.Y, pastedBitmap.PixelWidth, pastedBitmap.PixelHeight));
            drawing.Pop();
        }

        return RenderBitmap(visual, sourceBitmap.PixelWidth, sourceBitmap.PixelHeight);
    }
}
