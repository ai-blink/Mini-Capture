using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MiniCapture;

public sealed class FolderTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;

    public FolderTreeNode(string name, string? path, string iconGlyph = "\uE8B7", string? detail = null)
    {
        Name = name;
        Path = path;
        IconGlyph = iconGlyph;
        Detail = detail;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public string? Path { get; }

    public string IconGlyph { get; }

    public string? Detail { get; }

    public ObservableCollection<FolderTreeNode> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
