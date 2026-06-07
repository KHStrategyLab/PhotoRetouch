using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;
public sealed class RetouchSection : INotifyPropertyChanged
{
    private bool _isExpanded;
    private double _dragGapBefore;
    private double _dragGapAfter;

    public RetouchSection(string id, string title, bool isExpanded, IReadOnlyList<RetouchControl> controls)
    {
        Id = id;
        Title = title;
        _isExpanded = isExpanded;
        Controls = controls;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    public string Title { get; }
    public Thickness SectionPadding => new(0, DragGapBefore, 0, DragGapAfter);
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public double DragGapBefore
    {
        get => _dragGapBefore;
        set
        {
            if (Math.Abs(_dragGapBefore - value) < 0.001)
            {
                return;
            }

            _dragGapBefore = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SectionPadding));
        }
    }

    public double DragGapAfter
    {
        get => _dragGapAfter;
        set
        {
            if (Math.Abs(_dragGapAfter - value) < 0.001)
            {
                return;
            }

            _dragGapAfter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SectionPadding));
        }
    }

    public IReadOnlyList<RetouchControl> Controls { get; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

