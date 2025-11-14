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
        private static readonly Mutex _mutex = new Mutex(true, "MortarGameLauncherMutex");

        protected override void OnStartup(StartupEventArgs e)
        {
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
            
            
            // Si déjà une instance en cours, on ferme
            if (!_mutex.WaitOne(TimeSpan.Zero, true))
            {
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            MainWindow window = new MainWindow();
            window.Show();
        }
    }
}