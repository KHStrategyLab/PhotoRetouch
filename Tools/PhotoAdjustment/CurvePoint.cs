using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;
public sealed class CurvePoint : INotifyPropertyChanged
{
    private const double CurvePlotWidth = 270;
    private const double CurvePlotHeight = 180;
    private const double CurvePointRadius = 4;
    private const double CurvePlotOffset = CurvePointRadius;

    private double _input;
    private double _output;
    private bool _isSelected;
    private bool _isPendingDelete;

    public CurvePoint(double input, double output, bool isEndpoint)
    {
        _input = input;
        _output = output;
        IsEndpoint = isEndpoint;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsEndpoint { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool IsPendingDelete
    {
        get => _isPendingDelete;
        set
        {
            if (_isPendingDelete == value)
            {
                return;
            }

            _isPendingDelete = value;
            OnPropertyChanged();
        }
    }

    public double Input
    {
        get => _input;
        set
        {
            if (Math.Abs(_input - value) < 0.001)
            {
                return;
            }

            _input = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanvasLeft));
        }
    }

    public double Output
    {
        get => _output;
        set
        {
            if (Math.Abs(_output - value) < 0.001)
            {
                return;
            }

            _output = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanvasTop));
        }
    }

    public double CanvasLeft => CurvePlotOffset + Input / 255d * CurvePlotWidth - CurvePointRadius;
    public double CanvasTop => CurvePlotOffset + (255 - Output) / 255d * CurvePlotHeight - CurvePointRadius;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

