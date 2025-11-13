using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace GameLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool startedByUpdater = e.Args.Contains("--from-updater");

            if (!startedByUpdater)
            {
                // Le launcher n'a pas été lancé par l'updater
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string updaterPath = Path.Combine(currentDir, "LauncherUpdater.exe");

                if (File.Exists(updaterPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        Arguments = "--launch-game",
                        UseShellExecute = true
                    });
                }

                // Ferme le launcher actuel
                Current.Shutdown();
                return;
            }

            // Sinon continue normalement
            MainWindow window = new MainWindow();
            window.Show();
        }

    }
}
