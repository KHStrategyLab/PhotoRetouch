using System.Windows;
using System.Windows.Input;

namespace PhotoRetouch;

public partial class RenamePhotoWindow : Window
{
    public RenamePhotoWindow(string currentName)
    {
        InitializeComponent();
        PhotoNameTextBox.Text = currentName;
    }

    public string PhotoName => PhotoNameTextBox.Text;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PhotoNameTextBox.Focus();
        PhotoNameTextBox.SelectAll();
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
