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
    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var control = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        if (IsTextInputFocused())
        {
            if (control && key == Key.S)
            {
                OnSaveClick(sender, new RoutedEventArgs());
                e.Handled = true;
            }

            return;
        }

        if (control)
        {
            switch (key)
            {
                case Key.S:
                    OnSaveClick(sender, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                case Key.C:
                    if (shift)
                    {
                        OnCopyPathClick(sender, new RoutedEventArgs());
                    }
                    else if (_pixelSelection is not null)
                    {
                        CopyPixelSelection();
                    }
                    else
                    {
                        OnCopyImageClick(sender, new RoutedEventArgs());
                    }

                    e.Handled = true;
                    return;
                case Key.X:
                    CutPixelSelection();
                    e.Handled = true;
                    return;
                case Key.V:
                    PasteIntoPixelSelection();
                    e.Handled = true;
                    return;
                case Key.Z:
                    UndoEdit();
                    e.Handled = true;
                    return;
                case Key.Y:
                    RedoEdit();
                    e.Handled = true;
                    return;
                case Key.L:
                    RotateImage(-90);
                    e.Handled = true;
                    return;
                case Key.R:
                    RotateImage(90);
                    e.Handled = true;
                    return;
            }
        }

        switch (key)
        {
            case Key.F5:
                _ = RefreshCurrentFolderAsync();
                e.Handled = true;
                break;
            case Key.Left:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Right:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Add:
            case Key.OemPlus:
                SetZoom(_zoom * ZoomStep);
                e.Handled = true;
                break;
            case Key.Subtract:
            case Key.OemMinus:
                SetZoom(_zoom / ZoomStep);
                e.Handled = true;
                break;
            case Key.D1:
                SetZoom(1.0);
                e.Handled = true;
                break;
            case Key.F:
                _fitMode = true;
                FitToStage();
                e.Handled = true;
                break;
            case Key.Space:
                _spacePanActive = true;
                UpdateToolButtons();
                e.Handled = true;
                break;
            case Key.Delete:
                DeleteSelectedAnnotation();
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Space && _spacePanActive)
        {
            _spacePanActive = false;
            UpdateToolButtons();
            e.Handled = true;
        }
    }

    private static bool IsTextInputFocused()
    {
        return Keyboard.FocusedElement is System.Windows.Controls.TextBox;
    }
}
