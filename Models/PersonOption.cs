using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;
public sealed class PersonOption
{
    public PersonOption(string id, string name, string shortName)
    {
        Id = id;
        Name = name;
        ShortName = shortName;
    }

    public string Id { get; }
    public string Name { get; }
    public string ShortName { get; }
}

