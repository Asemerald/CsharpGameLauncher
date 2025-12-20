using System.Threading;
using System.Windows;

namespace GameLauncher
{
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // If any other instance is running, quit
            using Mutex mutex = new Mutex(true, "GameLauncher_Unique_Mutex_Name", out bool isNewInstance);
            if (!isNewInstance)
            {
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);
            
        }
    }
}