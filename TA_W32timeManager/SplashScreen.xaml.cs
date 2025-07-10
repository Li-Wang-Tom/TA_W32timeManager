using System.Threading.Tasks;
using System.Windows;
using W32TimeManager;

namespace TA_W32TimeManager
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Loaded += SplashScreen_Loaded;
        }

        private async void SplashScreen_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(4000); // 表示4秒
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
    }
}
