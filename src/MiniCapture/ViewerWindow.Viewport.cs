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
using XamlAnimatedGif;
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
    private void SelectFile(CaptureImageFile? file)
    {
        _isLoadingSelection = true;
        FileList.SelectedItem = file;
        if (file is not null)
        {
            FileList.ScrollIntoView(file);
        }

        _isLoadingSelection = false;
        LoadImage(file);
    }

    private void LoadImage(CaptureImageFile? file)
    {
        _ = LoadImageAsync(file);
    }

    private async Task LoadImageAsync(CaptureImageFile? file)
    {
        if (file is null)
        {
            CancelImageLoad();
            ClearImage("표시할 이미지가 없습니다.");
            return;
        }

        var load = StartNewImageLoad();

        if (_currentFile is not null &&
            !string.Equals(_currentFile.Path, file.Path, StringComparison.OrdinalIgnoreCase))
        {
            FlushCurrentImageViewState();
        }

        SetViewerStatus("이미지를 불러오는 중입니다.");

        BitmapSource image;
        try
        {
            await ImageDecodeGate.WaitAsync(load.Token);
            try
            {
                load.Token.ThrowIfCancellationRequested();
                image = await Task.Run(() => DecodeImageFile(file.Path), load.Token);
            }
            finally
            {
                ImageDecodeGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            if (IsCurrentImageLoad(load))
            {
                ClearImage($"이미지를 열 수 없습니다: {ex.Message}");
            }

            return;
        }

        if (!IsCurrentImageLoad(load))
        {
            return;
        }

        _currentFile = file;
        _lastSavedPath = file.Path;
        _editableImage = image;
        ClearAnnotations();
        _pixelSelection = null;
        CloseCropMode();
        ClearEditHistory();
        _isDirty = false;
        SetPreviewSource(file.Path);
        EmptyMessage.Visibility = Visibility.Collapsed;
        StatusText.Text = file.Path;
        UpdateStatusMetadata();
        UpdateDirtyIndicator();

        if (_fitMode)
        {
            FitToStage();
        }
        else
        {
            ApplyZoom();
        }

        RestoreImageViewState(file.Path);
        UpdateNavigationButtons();
    }

    internal static BitmapSource DecodeImageFile(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void SetPreviewSource(string path)
    {
        AnimationBehavior.SetSourceUri(PreviewImage, null);
        PreviewImage.Source = null;

        if (IsGifFile(path))
        {
            AnimationBehavior.SetSourceUri(PreviewImage, new Uri(path, UriKind.Absolute));
            return;
        }

        PreviewImage.Source = _editableImage;
    }

    internal static bool IsGifFile(string path) =>
        string.Equals(IOPath.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);

    private void ClearImage(string message)
    {
        FlushCurrentImageViewState();
        _currentFile = null;
        _lastSavedPath = null;
        _editableImage = null;
        ClearAnnotations();
        _pixelSelection = null;
        CloseCropMode();
        ClearEditHistory();
        _isDirty = false;
        AnimationBehavior.SetSourceUri(PreviewImage, null);
        PreviewImage.Source = null;
        ImageSurface.Width = 0;
        ImageSurface.Height = 0;
        AnnotationOverlay.Width = 0;
        AnnotationOverlay.Height = 0;
        CurrentFileText.Text = string.Empty;
        StatusText.Text = message;
        StatusMetaText.Text = string.Empty;
        SaveStateText.Text = "대기";
        EmptyMessage.Text = message;
        EmptyMessage.Visibility = Visibility.Visible;
        ZoomText.Text = "-";
        StatusZoomText.Text = "-";
        UpdateZoomSlider(null);
        UpdateNavigationButtons();
        UpdateEditButtons();
    }

    private void SetZoom(double zoom)
    {
        _fitMode = false;
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ApplyZoom();
        ScheduleCurrentImageViewStateSave();
    }

    private void OnZoomTextGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ZoomText.SelectAll();
    }

    private void OnZoomTextLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ApplyZoomTextInput();
    }

    private void OnZoomTextPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Enter)
        {
            ApplyZoomTextInput();
            ImageScrollViewer.Focus();
            e.Handled = true;
        }
        else if (key == Key.Escape)
        {
            RestoreZoomText();
            ImageScrollViewer.Focus();
            e.Handled = true;
        }
    }

    private bool ApplyZoomTextInput()
    {
        if (_editableImage is null)
        {
            ZoomText.Text = "-";
            return false;
        }

        var valueText = ZoomText.Text.Trim().TrimEnd('%').Trim();
        var parsed = double.TryParse(valueText, NumberStyles.Float, CultureInfo.CurrentCulture, out var percent) ||
            double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out percent);
        if (!parsed || !double.IsFinite(percent))
        {
            RestoreZoomText();
            SetViewerStatus("확대/축소 값은 10%~800% 사이의 숫자로 입력하세요.");
            return false;
        }

        SetZoom(percent / 100.0);
        return true;
    }

    private void RestoreZoomText()
    {
        ZoomText.Text = _editableImage is null
            ? "-"
            : $"{_zoom * 100:0}%";
    }

    private void FitToStage()
    {
        if (_editableImage is not BitmapSource bitmap)
        {
            return;
        }

        var viewportWidth = ImageScrollViewer.ViewportWidth;
        var viewportHeight = ImageScrollViewer.ViewportHeight;
        if (double.IsNaN(viewportWidth) || viewportWidth <= 0)
        {
            viewportWidth = ImageScrollViewer.ActualWidth;
        }

        if (double.IsNaN(viewportHeight) || viewportHeight <= 0)
        {
            viewportHeight = ImageScrollViewer.ActualHeight;
        }

        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            _zoom = 1.0;
        }
        else
        {
            var zoomX = Math.Max(1, viewportWidth - 28) / bitmap.PixelWidth;
            var zoomY = Math.Max(1, viewportHeight - 28) / bitmap.PixelHeight;
            _zoom = Math.Clamp(Math.Min(zoomX, zoomY), MinZoom, MaxZoom);
        }

        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (_editableImage is not BitmapSource bitmap)
        {
            ZoomText.Text = "-";
            return;
        }

        var width = Math.Max(1, bitmap.PixelWidth * _zoom);
        var height = Math.Max(1, bitmap.PixelHeight * _zoom);
        ImageSurface.Width = width;
        ImageSurface.Height = height;
        PreviewImage.Width = width;
        PreviewImage.Height = height;
        AnnotationOverlay.Width = width;
        AnnotationOverlay.Height = height;
        RenderAnnotations();
        var zoomText = $"{_zoom * 100:0}%";
        ZoomText.Text = zoomText;
        StatusZoomText.Text = zoomText;
        UpdateZoomSlider(_zoom);
    }

    private void UpdateZoomSlider(double? zoom)
    {
        if (StatusZoomSlider is null)
        {
            return;
        }

        _isUpdatingZoomSlider = true;
        try
        {
            StatusZoomSlider.IsEnabled = zoom.HasValue;
            StatusZoomSlider.Value = zoom.HasValue
                ? Math.Clamp(zoom.Value * 100, MinZoom * 100, MaxZoom * 100)
                : MinZoom * 100;
        }
        finally
        {
            _isUpdatingZoomSlider = false;
        }
    }

    private void OnImageStageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitMode)
        {
            FitToStage();
        }
    }

    private void OnImageScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (Math.Abs(e.HorizontalChange) < 0.1 && Math.Abs(e.VerticalChange) < 0.1)
        {
            return;
        }

        ScheduleCurrentImageViewStateSave();
    }

    private void OnStatusZoomSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingZoomSlider || _editableImage is null)
        {
            return;
        }

        SetZoom(e.NewValue / 100.0);
    }

    private void OnImagePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        SetZoom(e.Delta > 0 ? _zoom * ZoomStep : _zoom / ZoomStep);
        e.Handled = true;
    }
}
