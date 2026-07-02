using System.Globalization;
using System.Windows.Data;

namespace MiniCapture;

public sealed class ThumbnailImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CaptureImageFile file)
        {
            return null;
        }

        var pixelSize = 96;
        if (parameter is string text && int.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
        {
            pixelSize = parsed;
        }

        return file.GetThumbnail(pixelSize);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
