using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;
public sealed record RetouchAdjustmentState(
    Dictionary<string, double> ControlValues,
    CurveChannel CurveChannel,
    Dictionary<CurveChannel, CurvePointState[]> CurvePointsByChannel);

public sealed record RetouchHistoryEntry(RetouchAdjustmentState Before, RetouchAdjustmentState After);

public sealed record CurvePointState(double Input, double Output, bool IsEndpoint);

