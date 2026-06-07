using System.Windows;
using System.Windows.Input;

namespace PhotoRetouch;

public partial class ColorInputWindow : Window
{
    public ColorInputWindow(string currentColor)
    {
        InitializeComponent();
        ColorTextBox.Text = currentColor;
    }

    public string ColorText => ColorTextBox.Text;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ColorTextBox.Focus();
        ColorTextBox.SelectAll();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
