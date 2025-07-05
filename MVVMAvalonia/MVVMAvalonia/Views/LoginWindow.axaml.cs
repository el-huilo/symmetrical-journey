using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using MVVMAvalonia.ViewModels;
using System.Threading.Tasks;

namespace MVVMAvalonia.Views
{
    public partial class LoginWindow : UserControl
    {
        public LoginWindow()
        {
            InitializeComponent();
        }
        private MainWindow parent;
        private void btnCloseClick(object sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                lifetime.Shutdown();
        }
        private async void btnLogInClick(object sender, RoutedEventArgs e)
        {
            MainViewModel vm = (this.DataContext as MainViewModel);
            vm.Username = txtUser.Text; vm.Tag = tagUser.Text;
            MainWind.Effect = BlurEffect.Parse("blur(9)");
            await Task.Delay(1000);
            vm.Init_NetCode();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                parent = (MainWindow)lifetime.MainWindow;
                parent.ChangeView();
            }
        }
    }
}
