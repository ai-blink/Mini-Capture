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

    private void CopyPixelSelection()
    {
        if (!TryGetPixelSelection(out var selection))
        {
            SetViewerStatus("먼저 복사할 영역을 선택하세요.");
            return;
        }

        try
        {
            CopyPixelSelectionToClipboard(selection);
            SetViewerStatus($"선택 영역을 클립보드에 복사했습니다: {selection.Width} × {selection.Height}px");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetViewerStatus($"선택 영역 복사 실패: {ex.Message}");
        }
    }

    private void CutPixelSelection()
    {
        if (!TryGetPixelSelection(out var selection))
        {
            SetViewerStatus("먼저 잘라낼 영역을 선택하세요.");
            return;
        }

        try
        {
            CopyPixelSelectionToClipboard(selection);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetViewerStatus($"선택 영역 복사 실패: {ex.Message}");
            return;
        }

        CommitAnnotationsToBitmap("잘라내기 전 주석을 이미지에 적용했습니다.", pushUndo: true);
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        SetEditableImage(ClearPixelRectangle(_editableImage, selection), "선택 영역을 잘라냈습니다.");
    }

    private void PasteIntoPixelSelection()
    {
        if (_editableImage is null)
        {
            SetViewerStatus("붙여넣을 이미지가 없습니다.");
            return;
        }

        BitmapSource? clipboardImage;
        try
        {
            clipboardImage = WpfClipboard.ContainsImage() ? WpfClipboard.GetImage() : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetViewerStatus($"클립보드를 읽을 수 없습니다: {ex.Message}");
            return;
        }

        if (clipboardImage is null || clipboardImage.PixelWidth < 1 || clipboardImage.PixelHeight < 1)
        {
            SetViewerStatus("클립보드에 붙여넣을 이미지가 없습니다.");
            return;
        }

        var target = GetPasteTarget(clipboardImage);
        if (target.Width < 1 || target.Height < 1)
        {
            SetViewerStatus("붙여넣을 수 있는 이미지 영역이 없습니다.");
            return;
        }

        CommitAnnotationsToBitmap("붙여넣기 전 주석을 이미지에 적용했습니다.", pushUndo: true);
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        _pixelSelection = target;
        SetEditableImage(PasteBitmap(_editableImage, clipboardImage, target), "선택 영역 위치에 이미지를 붙여넣었습니다.");
    }

    private void CopyPixelSelectionToClipboard(Int32Rect selection)
    {
        var copied = new CroppedBitmap(ComposeImageForExport(), selection);
        copied.Freeze();
        WpfClipboard.SetImage(copied);
    }

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
