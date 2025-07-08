using System;
using System.Windows;

namespace TA_W32TimeManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var splash = new SplashScreen();
            splash.Show();
        }
    }
}
