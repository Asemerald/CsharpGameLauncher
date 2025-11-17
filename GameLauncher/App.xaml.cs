using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GameLauncher
{
    public partial class App : Application
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
            
            bool startedByUpdater = e.Args.Contains("--from-updater");

            // Si lancé directement, lancer l'updater et quitter
            if (!startedByUpdater)
            {
                string updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LauncherUpdater.exe");
                if (File.Exists(updaterPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        UseShellExecute = true
                    });
                }
                
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            /*MainWindow window = new MainWindow();
            window.Show();*/
        }
    }
}