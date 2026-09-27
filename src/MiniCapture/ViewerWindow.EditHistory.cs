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
    // 동작 1회 = 스냅샷 1개: 상태를 바꾸기 직전에 부른다. coalesceKey가 직전 기록과 같으면 같은 동작의 연속으로 보고 건너뛴다.
    private void PushUndoSnapshot(object? coalesceKey = null)
    {
        if (coalesceKey is not null && Equals(coalesceKey, _undoCoalesceKey))
        {
            return;
        }

        if (CaptureEditSnapshot() is { } snapshot)
        {
            RecordUndo(snapshot, coalesceKey);
        }
    }

    private void RecordUndo(EditSnapshot before, object? coalesceKey = null)
    {
        _editHistory.Push(before);
        _undoCoalesceKey = coalesceKey;
        UpdateEditButtons();
    }

    private EditSnapshot? CaptureEditSnapshot() =>
        _editableImage is null
            ? null
            : new EditSnapshot(_editableImage, _annotations.Select(annotation => annotation.Clone()).ToArray());

    private void UndoEdit()
    {
        FinishAnnotationGesture();
        if (CaptureEditSnapshot() is { } current && _editHistory.TryUndo(current, out var previous))
        {
            RestoreEditSnapshot(previous, "되돌렸습니다.");
        }
    }

    private void RedoEdit()
    {
        FinishAnnotationGesture();
        if (CaptureEditSnapshot() is { } current && _editHistory.TryRedo(current, out var next))
        {
            RestoreEditSnapshot(next, "다시 적용했습니다.");
        }
    }

    // 진행 중인 이동·크기 변경·텍스트 입력은 먼저 한 동작으로 확정해, 되돌리기가 그 동작부터 되돌리게 한다.
    private void FinishAnnotationGesture()
    {
        if (_isMovingAnnotation || _isResizingAnnotation)
        {
            EndAnnotationDrag();
        }

        EndAnnotationTextEditing();
    }

    private void RestoreEditSnapshot(EditSnapshot snapshot, string status)
    {
        _undoCoalesceKey = null;
        _pressedAnnotation = null;
        _annotations.Clear();
        // 스냅샷은 히스토리에 남아 다시 쓰이므로 복원본은 새로 복제한다.
        _annotations.AddRange(snapshot.Annotations.Select(annotation => annotation.Clone()));
        _selectedAnnotation = null;
        if (!ReferenceEquals(snapshot.Image, _editableImage))
        {
            SetEditableImage(snapshot.Image, status);
        }

        RenderAnnotations();
        SelectAnnotation(null);
        MarkAnnotationChanged(status);
    }

    private void ClearEditHistory()
    {
        _editHistory.Clear();
        _undoCoalesceKey = null;
        _dragStartSnapshot = null;
        UpdateEditButtons();
    }

    private void RotateImage(double angle)
    {
        if (_editableImage is null)
        {
            return;
        }

        PushUndoSnapshot();
        CommitAnnotationsToBitmap("회전 전 주석을 이미지에 적용했습니다.");
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

        PushUndoSnapshot();
        CommitAnnotationsToBitmap("자르기 전 주석을 이미지에 적용했습니다.");
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
