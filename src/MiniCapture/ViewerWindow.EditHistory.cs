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
    private void PushUndoSnapshot()
    {
        if (_editableImage is null)
        {
            return;
        }

        _undoImages.Push(CloneBitmap(_editableImage));
        while (_undoImages.Count > MaxUndoSnapshots)
        {
            TrimUndoStack();
        }

        _redoImages.Clear();
        UpdateEditButtons();
    }

    private void TrimUndoStack()
    {
        if (_undoImages.Count <= MaxUndoSnapshots)
        {
            return;
        }

        var snapshots = _undoImages.Take(MaxUndoSnapshots).Reverse().ToArray();
        _undoImages.Clear();
        foreach (var snapshot in snapshots)
        {
            _undoImages.Push(snapshot);
        }
    }

    private void UndoEdit()
    {
        if (_editableImage is null || _undoImages.Count == 0)
        {
            return;
        }

        _redoImages.Push(CloneBitmap(_editableImage));
        SetEditableImage(_undoImages.Pop(), "되돌렸습니다.", markDirty: true);
        UpdateEditButtons();
    }

    private void RedoEdit()
    {
        if (_editableImage is null || _redoImages.Count == 0)
        {
            return;
        }

        _undoImages.Push(CloneBitmap(_editableImage));
        SetEditableImage(_redoImages.Pop(), "다시 적용했습니다.", markDirty: true);
        UpdateEditButtons();
    }

    private void ClearEditHistory()
    {
        _undoImages.Clear();
        _redoImages.Clear();
        UpdateEditButtons();
    }

    private static BitmapSource CloneBitmap(BitmapSource source)
    {
        var converted = ConvertToPbgra32(source);
        var clone = new WriteableBitmap(converted);
        clone.Freeze();
        return clone;
    }

    private void RotateImage(double angle)
    {
        if (_editableImage is null)
        {
            return;
        }

        CommitAnnotationsToBitmap("회전 전 주석을 이미지에 적용했습니다.", pushUndo: true);
        PushUndoSnapshot();
        var source = ConvertToPbgra32(_editableImage);
        var transformed = new TransformedBitmap(source, new RotateTransform(angle));
        transformed.Freeze();
        SetEditableImage(transformed, angle < 0 ? "왼쪽으로 회전했습니다." : "오른쪽으로 회전했습니다.");
    }

    private void ResetCropControls()
    {
        _isUpdatingCropControls = true;
        try
        {
            CropLeftSlider.Value = 0;
            CropRightSlider.Value = 0;
            CropTopSlider.Value = 0;
            CropBottomSlider.Value = 0;
        }
        finally
        {
            _isUpdatingCropControls = false;
        }

        SynchronizeCropControls();
    }

    private void SynchronizeCropControls()
    {
        if (_editableImage is null)
        {
            return;
        }

        var width = _editableImage.PixelWidth;
        var height = _editableImage.PixelHeight;
        _isUpdatingCropControls = true;
        try
        {
            var left = Math.Clamp((int)Math.Round(CropLeftSlider.Value), 0, Math.Max(0, width - 1));
            var right = Math.Clamp((int)Math.Round(CropRightSlider.Value), 0, Math.Max(0, width - left - 1));
            var top = Math.Clamp((int)Math.Round(CropTopSlider.Value), 0, Math.Max(0, height - 1));
            var bottom = Math.Clamp((int)Math.Round(CropBottomSlider.Value), 0, Math.Max(0, height - top - 1));

            CropLeftSlider.Maximum = Math.Max(0, width - right - 1);
            CropRightSlider.Maximum = Math.Max(0, width - left - 1);
            CropTopSlider.Maximum = Math.Max(0, height - bottom - 1);
            CropBottomSlider.Maximum = Math.Max(0, height - top - 1);
            CropLeftSlider.Value = Math.Min(left, CropLeftSlider.Maximum);
            CropRightSlider.Value = Math.Min(right, CropRightSlider.Maximum);
            CropTopSlider.Value = Math.Min(top, CropTopSlider.Maximum);
            CropBottomSlider.Value = Math.Min(bottom, CropBottomSlider.Maximum);
            CropLeftText.Text = ((int)Math.Round(CropLeftSlider.Value)).ToString(CultureInfo.CurrentCulture);
            CropRightText.Text = ((int)Math.Round(CropRightSlider.Value)).ToString(CultureInfo.CurrentCulture);
            CropTopText.Text = ((int)Math.Round(CropTopSlider.Value)).ToString(CultureInfo.CurrentCulture);
            CropBottomText.Text = ((int)Math.Round(CropBottomSlider.Value)).ToString(CultureInfo.CurrentCulture);
        }
        finally
        {
            _isUpdatingCropControls = false;
        }
    }

    private void ApplyCropTextValue(System.Windows.Controls.TextBox? textBox)
    {
        if (!_isCropMode || textBox?.Tag is not string side)
        {
            return;
        }

        if (!int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value) || value < 0)
        {
            SynchronizeCropControls();
            SetViewerStatus("자르기 값은 0 이상의 정수로 입력하세요.");
            return;
        }

        var slider = side switch
        {
            "Left" => CropLeftSlider,
            "Right" => CropRightSlider,
            "Top" => CropTopSlider,
            "Bottom" => CropBottomSlider,
            _ => null
        };
        if (slider is null)
        {
            return;
        }

        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        SynchronizeCropControls();
        RenderAnnotations();
    }

    private Int32Rect GetCropPixelRect()
    {
        if (_editableImage is null)
        {
            return new Int32Rect();
        }

        return CalculateCropRectangle(
            _editableImage.PixelWidth,
            _editableImage.PixelHeight,
            (int)Math.Round(CropLeftSlider.Value),
            (int)Math.Round(CropRightSlider.Value),
            (int)Math.Round(CropTopSlider.Value),
            (int)Math.Round(CropBottomSlider.Value));
    }

    internal static Int32Rect CalculateCropRectangle(int width, int height, int left, int right, int top, int bottom)
    {
        if (width < 1 || height < 1)
        {
            return new Int32Rect();
        }

        left = Math.Clamp(left, 0, width - 1);
        right = Math.Clamp(right, 0, width - left - 1);
        top = Math.Clamp(top, 0, height - 1);
        bottom = Math.Clamp(bottom, 0, height - top - 1);
        return new Int32Rect(left, top, width - left - right, height - top - bottom);
    }

    private void ApplyCrop()
    {
        if (_editableImage is null)
        {
            return;
        }

        var crop = GetCropPixelRect();
        if (crop.Width == _editableImage.PixelWidth && crop.Height == _editableImage.PixelHeight)
        {
            CloseCropMode();
            SetViewerStatus("자르기 값이 없어 원본을 유지했습니다.");
            return;
        }

        CommitAnnotationsToBitmap("자르기 전 주석을 이미지에 적용했습니다.", pushUndo: true);
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        var cropped = new CroppedBitmap(ConvertToPbgra32(_editableImage), crop);
        cropped.Freeze();
        _pixelSelection = null;
        CloseCropMode();
        SetEditableImage(cropped, $"이미지를 {crop.Width} × {crop.Height}px로 잘랐습니다.");
    }

    private void CloseCropMode()
    {
        _isCropMode = false;
        CropPanel.Visibility = Visibility.Collapsed;
        UpdateToolButtons();
        RenderAnnotations();
    }
}
