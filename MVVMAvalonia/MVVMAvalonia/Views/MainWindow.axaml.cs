using Avalonia.Controls;

namespace MVVMAvalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentControl.Content = new LoginWindow();
    }
    public void ChangeView()
    {
        ContentControl.Content = new MainView();
    }
}