using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;
public sealed class RetouchControl : INotifyPropertyChanged
{
    private const double CurvePlotWidth = 270;
    private const double CurvePlotHeight = 180;
    private const double CurvePointRadius = 4;
    private const double CurvePlotOffset = CurvePointRadius;
    private const double CurveCanvasWidth = CurvePlotWidth + CurvePointRadius * 2;
    private const double CurveCanvasHeight = CurvePlotHeight + CurvePointRadius * 2;
    private const int MaxCurvePoints = 7;
    private const int CurveHistogramSampleLongSide = 512;
    private double _value;
    private readonly double _defaultValue;
    private CurveChannel _curveChannel = CurveChannel.All;
    private CurvePoint? _selectedCurvePoint;
    private PointCollection _curveHistogramPoints = new();
    private Dictionary<CurveChannel, PointCollection> _curveHistogramPointsByChannel = CreateEmptyCurveHistogramPointsByChannel();
    private readonly Dictionary<CurveChannel, ObservableCollection<CurvePoint>> _curvePointsByChannel = new();

    public RetouchControl(string id, string label, double minimum, double maximum, double value)
        : this(id, label, minimum, maximum, value, 1, false, "0")
    {
    }

    private RetouchControl(
        string id,
        string label,
        double minimum,
        double maximum,
        double value,
        double tickFrequency,
        bool isSnapToTickEnabled,
        string valueTextFormat)
    {
        Id = id;
        Label = label;
        Minimum = minimum;
        Maximum = maximum;
        TickFrequency = tickFrequency;
        IsSnapToTickEnabled = isSnapToTickEnabled;
        SmallChange = tickFrequency;
        LargeChange = Math.Max(tickFrequency, tickFrequency * 3);
        ValueTextFormat = valueTextFormat;
        _defaultValue = CoerceValue(value);
        _value = _defaultValue;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    public string Label { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double TickFrequency { get; }
    public double SmallChange { get; }
    public double LargeChange { get; }
    public double DefaultValue => _defaultValue;
    public bool IsSnapToTickEnabled { get; }
    public string ValueTextFormat { get; }
    public bool IsColorPicker { get; }
    public bool IsActionButton { get; }
    public bool IsBackgroundLibrary { get; }
    public bool IsCurveEditor { get; }
    public bool IsHeader { get; }
    public string? ColorValue { get; }
    public string? ActionText { get; }
    public IReadOnlyList<BackgroundOption> BackgroundOptions { get; } = Array.Empty<BackgroundOption>();
    public ObservableCollection<CurvePoint> CurvePoints => GetCurvePoints(CurveChannel);
    public CurvePoint? SelectedCurvePoint
    {
        get => _selectedCurvePoint;
        set
        {
            if (ReferenceEquals(_selectedCurvePoint, value))
            {
                return;
            }

            if (_selectedCurvePoint is not null)
            {
                _selectedCurvePoint.PropertyChanged -= SelectedCurvePoint_PropertyChanged;
            }

            _selectedCurvePoint = value;
            if (_selectedCurvePoint is not null)
            {
                _selectedCurvePoint.PropertyChanged += SelectedCurvePoint_PropertyChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCurveInputText));
            OnPropertyChanged(nameof(SelectedCurveOutputText));
            OnPropertyChanged(nameof(HasSelectedCurvePoint));
            OnPropertyChanged(nameof(IsSelectedCurveInputEditable));
        }
    }

    public bool HasSelectedCurvePoint => SelectedCurvePoint is not null;
    public bool IsSelectedCurveInputEditable => SelectedCurvePoint is not null;
    public string SelectedCurveInputText => SelectedCurvePoint is null ? "-" : SelectedCurvePoint.Input.ToString("0");
    public string SelectedCurveOutputText => SelectedCurvePoint is null ? "-" : SelectedCurvePoint.Output.ToString("0");
    public PointCollection CurveHistogramPoints
    {
        get => _curveHistogramPoints;
        private set
        {
            _curveHistogramPoints = value;
            OnPropertyChanged();
        }
    }

    public PointCollection CurvePolylinePoints
    {
        get
        {
            PointCollection points = new();
            byte[] lut = BuildCurveLookupTable(CurveChannel);
            for (int input = 0; input < lut.Length; input++)
            {
                points.Add(new System.Windows.Point(InputToCanvasX(input), OutputToCanvasY(lut[input])));
            }

            return points;
        }
    }

    public CurveChannel CurveChannel
    {
        get => _curveChannel;
        set
        {
            if (_curveChannel == value)
            {
                return;
            }

            _curveChannel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurvePoints));
            OnPropertyChanged(nameof(CurvePolylinePoints));
            OnPropertyChanged(nameof(CurveStrengthLabel));
            RefreshCurveHistogramPoints();
        }
    }

    public double Value
    {
        get => _value;
        set
        {
            double coercedValue = CoerceValue(value);
            if (Math.Abs(_value - coercedValue) < 0.001)
            {
                return;
            }

            _value = coercedValue;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayValue));
        }
    }

    public string DisplayValue => FormatDisplayValue(Value);
    public string CurveStrengthLabel => $"\uCEE4\uBE0C \uC801\uC6A9\uB7C9 ({CurveChannelDisplayName})";
    private string CurveChannelDisplayName => CurveChannel switch
    {
        CurveChannel.Red => "R",
        CurveChannel.Green => "G",
        CurveChannel.Blue => "B",
        _ => "\uC804\uCCB4"
    };

    public void ResetToDefault()
    {
        Value = _defaultValue;
        if (IsCurveEditor)
        {
            ResetAllCurveChannels();
        }
    }

    private void SelectedCurvePoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CurvePoint.Input) or nameof(CurvePoint.Output))
        {
            OnPropertyChanged(nameof(SelectedCurveInputText));
            OnPropertyChanged(nameof(SelectedCurveOutputText));
        }
    }

    public static RetouchControl CreateColor(string id, string label, string colorValue)
    {
        return new RetouchControl(id, label, 0, 0, 0, true, colorValue);
    }

    public static RetouchControl CreateAction(string id, string label, string actionText)
    {
        return new RetouchControl(id, label, 0, 0, 0, false, null, true, actionText);
    }

    public static RetouchControl CreateBackgroundLibrary(string id, string label, IReadOnlyList<BackgroundOption> backgroundOptions)
    {
        return new RetouchControl(id, label, backgroundOptions);
    }

    public static RetouchControl CreateCurve(string id, string label)
    {
        RetouchControl control = new(id, label, 0, 100, 100, isCurveEditor: true, isHeader: false);
        control.InitializeCurvePoints();
        return control;
    }

    public static RetouchControl CreateHeader(string id, string label)
    {
        return new RetouchControl(id, label, 0, 0, 0, isCurveEditor: false, isHeader: true);
    }

    public static RetouchControl CreateExposure(string id, string label)
    {
        return new RetouchControl(id, label, -15, 15, 0, 0.5, false, "0.0");
    }

    public static RetouchControl CreateContrast(string id, string label)
    {
        return new RetouchControl(id, label, -25, 25, 0);
    }

    public static RetouchControl CreateStage(string id, string label, int defaultValue)
    {
        return new RetouchControl(id, label, 1, 10, defaultValue, 1, true, "0");
    }

    public static RetouchControl CreateSwitch(string id, string label, bool defaultValue)
    {
        return new RetouchControl(id, label, 0, 1, defaultValue ? 1 : 0, 1, true, "0");
    }

    public string FormatDisplayValue(double value)
    {
        return value.ToString(ValueTextFormat, CultureInfo.InvariantCulture);
    }

    private double CoerceValue(double value)
    {
        double coerced = Math.Clamp(value, Minimum, Maximum);
        if (IsSnapToTickEnabled && TickFrequency > 0)
        {
            double steps = Math.Round((coerced - Minimum) / TickFrequency, MidpointRounding.AwayFromZero);
            coerced = Minimum + steps * TickFrequency;
        }

        return Math.Clamp(coerced, Minimum, Maximum);
    }

    private RetouchControl(string id, string label, double minimum, double maximum, double value, bool isColorPicker, string? colorValue)
        : this(id, label, minimum, maximum, value)
    {
        IsColorPicker = isColorPicker;
        ColorValue = colorValue;
    }

    private RetouchControl(
        string id,
        string label,
        double minimum,
        double maximum,
        double value,
        bool isColorPicker,
        string? colorValue,
        bool isActionButton,
        string? actionText)
        : this(id, label, minimum, maximum, value, isColorPicker, colorValue)
    {
        IsActionButton = isActionButton;
        ActionText = actionText;
    }

    private RetouchControl(string id, string label, IReadOnlyList<BackgroundOption> backgroundOptions)
        : this(id, label, 0, 0, 0)
    {
        IsBackgroundLibrary = true;
        BackgroundOptions = backgroundOptions;
    }

    private RetouchControl(string id, string label, double minimum, double maximum, double value, bool isCurveEditor, bool isHeader)
        : this(id, label, minimum, maximum, value)
    {
        IsCurveEditor = isCurveEditor;
        IsHeader = isHeader;
    }

    public void SetCurveHistogramSource(BitmapSource? source)
    {
        _curveHistogramPointsByChannel = source is null
            ? CreateEmptyCurveHistogramPointsByChannel()
            : CreateCurveHistogramPointsByChannel(source);
        RefreshCurveHistogramPoints();
    }

    private void RefreshCurveHistogramPoints()
    {
        CurveHistogramPoints = _curveHistogramPointsByChannel.TryGetValue(CurveChannel, out PointCollection? points)
            ? points
            : new PointCollection();
    }

    private static Dictionary<CurveChannel, PointCollection> CreateEmptyCurveHistogramPointsByChannel()
    {
        return Enum.GetValues<CurveChannel>()
            .ToDictionary(channel => channel, _ => new PointCollection());
    }

    private static Dictionary<CurveChannel, PointCollection> CreateCurveHistogramPointsByChannel(BitmapSource source)
    {
        BitmapSource sample = CreateCurveHistogramSample(source);
        BitmapSource bitmap = sample.Format == PixelFormats.Bgra32
            ? sample
            : new FormatConvertedBitmap(sample, PixelFormats.Bgra32, null, 0);

        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        int[] allBins = new int[256];
        int[] redBins = new int[256];
        int[] greenBins = new int[256];
        int[] blueBins = new int[256];
        for (int index = 0; index < pixels.Length; index += 4)
        {
            if (pixels[index + 3] == 0)
            {
                continue;
            }

            int blue = pixels[index];
            int green = pixels[index + 1];
            int red = pixels[index + 2];
            int luminance = Math.Clamp((int)Math.Round(red * 0.2126 + green * 0.7152 + blue * 0.0722), 0, 255);
            allBins[luminance]++;
            redBins[red]++;
            greenBins[green]++;
            blueBins[blue]++;
        }

        return new Dictionary<CurveChannel, PointCollection>
        {
            [CurveChannel.All] = CreateCurveHistogramPoints(allBins),
            [CurveChannel.Red] = CreateCurveHistogramPoints(redBins),
            [CurveChannel.Green] = CreateCurveHistogramPoints(greenBins),
            [CurveChannel.Blue] = CreateCurveHistogramPoints(blueBins)
        };
    }

    private static PointCollection CreateCurveHistogramPoints(int[] bins)
    {
        int maximum = bins.Max();
        if (maximum == 0)
        {
            return new PointCollection();
        }

        double logMaximum = Math.Log(maximum + 1);
        PointCollection points = new()
        {
            new System.Windows.Point(CurvePlotOffset, CurvePlotOffset + CurvePlotHeight)
        };

        for (int index = 0; index < bins.Length; index++)
        {
            double x = InputToCanvasX(index);
            double normalized = Math.Log(bins[index] + 1) / logMaximum;
            double y = CurvePlotOffset + CurvePlotHeight - normalized * (CurvePlotHeight * 0.88);
            points.Add(new System.Windows.Point(x, y));
        }

        points.Add(new System.Windows.Point(CurvePlotOffset + CurvePlotWidth, CurvePlotOffset + CurvePlotHeight));
        return points;
    }

    private static BitmapSource CreateCurveHistogramSample(BitmapSource source)
    {
        int longestSide = Math.Max(source.PixelWidth, source.PixelHeight);
        if (longestSide <= CurveHistogramSampleLongSide)
        {
            return source;
        }

        double scale = (double)CurveHistogramSampleLongSide / longestSide;
        TransformedBitmap sample = new(source, new ScaleTransform(scale, scale));
        sample.Freeze();
        return sample;
    }

    public CurvePoint? AddCurvePointFromCanvas(double canvasX, double canvasY)
    {
        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        if (points.Count >= MaxCurvePoints || !IsInsideCurvePlot(canvasX, canvasY))
        {
            return null;
        }

        double input = CanvasXToInput(canvasX);
        if (points.Any(point => Math.Abs(point.Input - input) <= 2))
        {
            return null;
        }

        double output = CanvasYToOutput(canvasY);
        CurvePoint point = new(input, output, isEndpoint: false);
        points.Add(point);
        SortCurvePoints(points);
        NotifyCurveChanged();
        return point;
    }

    private static bool IsInsideCurvePlot(double canvasX, double canvasY)
    {
        return canvasX >= CurvePlotOffset &&
               canvasX <= CurvePlotOffset + CurvePlotWidth &&
               canvasY >= CurvePlotOffset &&
               canvasY <= CurvePlotOffset + CurvePlotHeight;
    }

    public void MoveCurvePoint(CurvePoint point, double canvasX, double canvasY)
    {
        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        if (!points.Contains(point))
        {
            return;
        }

        (double minimumInput, double maximumInput) = GetInputBoundsForPoint(points, point);
        point.Input = Math.Clamp(CanvasXToInput(canvasX), minimumInput, maximumInput);
        point.Output = CanvasYToOutput(canvasY);

        SortCurvePoints(points);
        NotifyCurveChanged();
    }

    public bool SetSelectedCurvePointInput(double input)
    {
        if (SelectedCurvePoint is null)
        {
            return false;
        }

        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        if (!points.Contains(SelectedCurvePoint))
        {
            return false;
        }

        (double minimumInput, double maximumInput) = GetInputBoundsForPoint(points, SelectedCurvePoint);
        double clampedInput = Math.Clamp(input, minimumInput, maximumInput);
        if (Math.Abs(SelectedCurvePoint.Input - clampedInput) < 0.001)
        {
            return false;
        }

        SelectedCurvePoint.Input = clampedInput;
        SortCurvePoints(points);
        NotifyCurveChanged();
        return true;
    }

    public bool SetSelectedCurvePointOutput(double output)
    {
        if (SelectedCurvePoint is null)
        {
            return false;
        }

        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        if (!points.Contains(SelectedCurvePoint))
        {
            return false;
        }

        double clampedOutput = Math.Clamp(output, 0, 255);
        if (Math.Abs(SelectedCurvePoint.Output - clampedOutput) < 0.001)
        {
            return false;
        }

        SelectedCurvePoint.Output = clampedOutput;
        NotifyCurveChanged();
        return true;
    }

    public bool NudgeSelectedCurvePoint(double inputDelta, double outputDelta)
    {
        if (SelectedCurvePoint is null)
        {
            return false;
        }

        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        if (!points.Contains(SelectedCurvePoint))
        {
            return false;
        }

        double nextInput = SelectedCurvePoint.Input;
        bool inputChanged = false;
        if (Math.Abs(inputDelta) > 0.001)
        {
            (double minimumInput, double maximumInput) = GetInputBoundsForPoint(points, SelectedCurvePoint);
            nextInput = Math.Clamp(SelectedCurvePoint.Input + inputDelta, minimumInput, maximumInput);
            inputChanged = Math.Abs(SelectedCurvePoint.Input - nextInput) >= 0.001;
        }

        double nextOutput = Math.Clamp(SelectedCurvePoint.Output + outputDelta, 0, 255);
        bool outputChanged = Math.Abs(SelectedCurvePoint.Output - nextOutput) >= 0.001;
        if (!inputChanged && !outputChanged)
        {
            return false;
        }

        SelectedCurvePoint.Input = nextInput;
        SelectedCurvePoint.Output = nextOutput;
        if (inputChanged)
        {
            SortCurvePoints(points);
        }

        NotifyCurveChanged();
        return true;
    }

    public bool ResetCurrentCurveChannel()
    {
        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        bool isAlreadyReset =
            points.Count == 2 &&
            points.Any(point => Math.Abs(point.Input) < 0.001 && Math.Abs(point.Output) < 0.001) &&
            points.Any(point => Math.Abs(point.Input - 255) < 0.001 && Math.Abs(point.Output - 255) < 0.001);

        if (isAlreadyReset)
        {
            return false;
        }

        SelectedCurvePoint = null;
        points.Clear();
        points.Add(new CurvePoint(0, 0, isEndpoint: false));
        points.Add(new CurvePoint(255, 255, isEndpoint: false));
        NotifyCurveChanged();
        return true;
    }

    public static Dictionary<CurveChannel, CurvePointState[]> CreateDefaultCurvePointsByChannel()
    {
        return Enum.GetValues<CurveChannel>()
            .ToDictionary(
                channel => channel,
                _ => new[]
                {
                    new CurvePointState(0, 0, false),
                    new CurvePointState(255, 255, false)
                });
    }

    public Dictionary<CurveChannel, CurvePointState[]> ExportCurvePointsByChannel()
    {
        Dictionary<CurveChannel, CurvePointState[]> result = new();
        foreach (CurveChannel channel in Enum.GetValues<CurveChannel>())
        {
            result[channel] = GetCurvePoints(channel)
                .OrderBy(point => point.Input)
                .Select(point => new CurvePointState(point.Input, point.Output, point.IsEndpoint))
                .ToArray();
        }

        return result;
    }

    public void RestoreCurveState(
        CurveChannel channel,
        IReadOnlyDictionary<CurveChannel, CurvePointState[]> curvePointsByChannel)
    {
        SelectedCurvePoint = null;
        foreach (CurveChannel curveChannel in Enum.GetValues<CurveChannel>())
        {
            ObservableCollection<CurvePoint> points = GetCurvePoints(curveChannel);
            points.Clear();

            CurvePointState[] states = curvePointsByChannel.TryGetValue(curveChannel, out CurvePointState[]? savedStates)
                ? savedStates
                : CreateDefaultCurvePointsByChannel()[curveChannel];

            foreach (CurvePointState state in NormalizeCurvePointStates(states))
            {
                points.Add(new CurvePoint(state.Input, state.Output, state.IsEndpoint));
            }

            SortCurvePoints(points);
        }

        CurveChannel = Enum.IsDefined(channel) ? channel : CurveChannel.All;
        NotifyCurveChanged();
    }

    private void ResetAllCurveChannels()
    {
        SelectedCurvePoint = null;
        foreach (CurveChannel channel in Enum.GetValues<CurveChannel>())
        {
            ObservableCollection<CurvePoint> points = GetCurvePoints(channel);
            points.Clear();
            points.Add(new CurvePoint(0, 0, isEndpoint: false));
            points.Add(new CurvePoint(255, 255, isEndpoint: false));
        }

        CurveChannel = CurveChannel.All;
        NotifyCurveChanged();
    }

    private static CurvePointState[] NormalizeCurvePointStates(IEnumerable<CurvePointState>? states)
    {
        CurvePointState[] saved = states?.ToArray() ?? Array.Empty<CurvePointState>();
        CurvePointState[] normalized = saved
            .Select(point => new CurvePointState(
                Math.Clamp(point.Input, 0, 255),
                Math.Clamp(point.Output, 0, 255),
                false))
            .GroupBy(point => Math.Round(point.Input))
            .Select(group => group.Last())
            .OrderBy(point => point.Input)
            .Take(MaxCurvePoints)
            .ToArray();

        return normalized.Length == 0
            ? new[]
            {
                new CurvePointState(0, 0, false),
                new CurvePointState(255, 255, false)
            }
            : normalized;
    }

    private static (double Minimum, double Maximum) GetInputBoundsForPoint(ObservableCollection<CurvePoint> points, CurvePoint point)
    {
        return (0, 255);
    }

    public void MarkCurvePointForDeletion(CurvePoint point)
    {
        point.IsPendingDelete = true;
    }

    public bool DeleteCurvePointIfMarked(CurvePoint point)
    {
        return point.IsPendingDelete && DeleteCurvePoint(point);
    }

    public bool DeleteCurvePoint(CurvePoint point)
    {
        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        bool removed = points.Remove(point);
        if (removed)
        {
            NotifyCurveChanged();
        }

        return removed;
    }

    public byte[] BuildCurveLookupTable(CurveChannel channel)
    {
        ObservableCollection<CurvePoint> points = GetCurvePoints(channel);
        CurvePoint[] orderedPoints = points.OrderBy(point => point.Input).ToArray();
        byte[] lut = new byte[256];
        if (orderedPoints.Length < 2)
        {
            for (int input = 0; input < lut.Length; input++)
            {
                lut[input] = (byte)input;
            }

            return lut;
        }

        double[] tangents = CalculateCurveTangents(orderedPoints);
        int segmentIndex = 0;

        for (int input = 0; input < lut.Length; input++)
        {
            while (segmentIndex < orderedPoints.Length - 2 && input > orderedPoints[segmentIndex + 1].Input)
            {
                segmentIndex++;
            }

            CurvePoint left = orderedPoints[segmentIndex];
            CurvePoint right = orderedPoints[segmentIndex + 1];
            double output = EvaluateCubicHermite(
                left.Input,
                right.Input,
                left.Output,
                right.Output,
                tangents[segmentIndex],
                tangents[segmentIndex + 1],
                input);
            lut[input] = (byte)Math.Clamp((int)Math.Round(output), 0, 255);
        }

        return lut;
    }

    private static double[] CalculateCurveTangents(CurvePoint[] points)
    {
        double[] tangents = new double[points.Length];
        if (points.Length < 2)
        {
            return tangents;
        }

        if (points.Length == 2)
        {
            double slope = SegmentSlope(points[0], points[1]);
            tangents[0] = slope;
            tangents[1] = slope;
            return tangents;
        }

        double[] widths = new double[points.Length - 1];
        double[] slopes = new double[points.Length - 1];
        for (int index = 0; index < widths.Length; index++)
        {
            widths[index] = Math.Max(0.001, points[index + 1].Input - points[index].Input);
            slopes[index] = (points[index + 1].Output - points[index].Output) / widths[index];
        }

        tangents[0] = CalculateEndpointTangent(widths[0], widths[1], slopes[0], slopes[1]);
        tangents[^1] = CalculateEndpointTangent(widths[^1], widths[^2], slopes[^1], slopes[^2]);

        for (int index = 1; index < points.Length - 1; index++)
        {
            double previousSlope = slopes[index - 1];
            double nextSlope = slopes[index];
            if (Math.Abs(previousSlope) < 0.0001 ||
                Math.Abs(nextSlope) < 0.0001 ||
                Math.Sign(previousSlope) != Math.Sign(nextSlope))
            {
                tangents[index] = 0;
                continue;
            }

            double previousWidth = widths[index - 1];
            double nextWidth = widths[index];
            double weightA = 2 * nextWidth + previousWidth;
            double weightB = nextWidth + 2 * previousWidth;
            tangents[index] = (weightA + weightB) / (weightA / previousSlope + weightB / nextSlope);
        }

        return tangents;
    }

    private static double SegmentSlope(CurvePoint left, CurvePoint right)
    {
        return (right.Output - left.Output) / Math.Max(0.001, right.Input - left.Input);
    }

    private static double CalculateEndpointTangent(double width, double nextWidth, double slope, double nextSlope)
    {
        double tangent = ((2 * width + nextWidth) * slope - width * nextSlope) / (width + nextWidth);
        if (Math.Sign(tangent) != Math.Sign(slope))
        {
            return 0;
        }

        if (Math.Sign(slope) != Math.Sign(nextSlope) && Math.Abs(tangent) > Math.Abs(3 * slope))
        {
            return 3 * slope;
        }

        return tangent;
    }

    private static double EvaluateCubicHermite(
        double leftInput,
        double rightInput,
        double leftOutput,
        double rightOutput,
        double leftTangent,
        double rightTangent,
        double input)
    {
        double width = Math.Max(0.001, rightInput - leftInput);
        double t = Math.Clamp((input - leftInput) / width, 0, 1);
        double t2 = t * t;
        double t3 = t2 * t;

        double leftBlend = 2 * t3 - 3 * t2 + 1;
        double leftTangentBlend = t3 - 2 * t2 + t;
        double rightBlend = -2 * t3 + 3 * t2;
        double rightTangentBlend = t3 - t2;

        return leftBlend * leftOutput +
               leftTangentBlend * width * leftTangent +
               rightBlend * rightOutput +
               rightTangentBlend * width * rightTangent;
    }

    private void InitializeCurvePoints()
    {
        foreach (CurveChannel channel in Enum.GetValues<CurveChannel>())
        {
            _curvePointsByChannel[channel] = new ObservableCollection<CurvePoint>
            {
                new(0, 0, isEndpoint: false),
                new(255, 255, isEndpoint: false)
            };
        }
    }

    private ObservableCollection<CurvePoint> GetCurvePoints(CurveChannel channel)
    {
        if (!_curvePointsByChannel.TryGetValue(channel, out ObservableCollection<CurvePoint>? points))
        {
            points = new ObservableCollection<CurvePoint>
            {
                new(0, 0, isEndpoint: false),
                new(255, 255, isEndpoint: false)
            };
            _curvePointsByChannel[channel] = points;
        }

        return points;
    }

    private double InterpolateCurveOutput(CurveChannel channel, double input)
    {
        byte[] lut = BuildCurveLookupTable(channel);
        return lut[(int)Math.Clamp(Math.Round(input), 0, 255)];
    }

    private static double CanvasXToInput(double canvasX)
    {
        return Math.Clamp((canvasX - CurvePlotOffset) / CurvePlotWidth * 255, 0, 255);
    }

    private static double CanvasYToOutput(double canvasY)
    {
        return Math.Clamp(255 - (canvasY - CurvePlotOffset) / CurvePlotHeight * 255, 0, 255);
    }

    private static double InputToCanvasX(double input)
    {
        return CurvePlotOffset + input / 255d * CurvePlotWidth;
    }

    private static double OutputToCanvasY(double output)
    {
        return CurvePlotOffset + (255 - output) / 255d * CurvePlotHeight;
    }

    private void SortCurvePoints(ObservableCollection<CurvePoint> points)
    {
        CurvePoint[] orderedPoints = points.OrderBy(point => point.Input).ToArray();
        for (int index = 0; index < orderedPoints.Length; index++)
        {
            int currentIndex = points.IndexOf(orderedPoints[index]);
            if (currentIndex != index)
            {
                points.Move(currentIndex, index);
            }
        }
    }

    private void NotifyCurveChanged()
    {
        OnPropertyChanged(nameof(CurvePoints));
        OnPropertyChanged(nameof(CurvePolylinePoints));
        OnPropertyChanged(nameof(CurveChannel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class RetouchValueTextConverter : System.Windows.Data.IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not double value ||
            values[1] is not RetouchControl control)
        {
            return string.Empty;
        }

        return control.FormatDisplayValue(value);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
