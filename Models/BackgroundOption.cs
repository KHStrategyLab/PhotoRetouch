using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;
public sealed class BackgroundOption
{
    public BackgroundOption(string name, string previewColor)
    {
        Name = name;
        PreviewColor = previewColor;
    }

    public string Name { get; }
    public string PreviewColor { get; }
}

