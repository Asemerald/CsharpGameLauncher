using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace LauncherUpdater
{
    class Program
    {
        // ⚡ MODIFIE CES VARIABLES AVEC TES IDS GOOGLE DRIVE
        private const string onlineLauncherVersionUrl =
            "https://drive.google.com/uc?export=download&id=XXX_LAUNCHER_VERSION_ID";

        private const string onlineLauncherZipId = "XXX_LAUNCHER_ZIP_ID";

        // Lien direct vers le runtime .NET 9 x64
        private const string dotnetRuntimeUrl =
            "https://download.visualstudio.microsoft.com/download/pr/xxx/dotnet-runtime-9.0.0-win-x64.exe";

        static void Main(string[] args)
        {
            string rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonJeu");
            string launcherExe = Path.Combine(rootPath, "GameLauncher.exe");
            string launcherZip = Path.Combine(rootPath, "Launcher.zip");
            string launcherVersionFile = Path.Combine(rootPath, "LauncherVersion.txt");

            Console.WriteLine("=== LauncherUpdater démarré ===");

            // 1️⃣ Vérifier .NET 9
            if (!IsDotNet9Installed())
            {
                Console.WriteLine(".NET 9 Runtime non trouvé. Téléchargement et installation...");
                InstallDotNet9();
                Console.WriteLine(".NET 9 installé !");
            }
            else
            {
                Console.WriteLine(".NET 9 Runtime détecté.");
            }

            // Crée le dossier si nécessaire
            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            WebClient webClient = new WebClient();

            // 2️⃣ Vérifier si le launcher existe
            if (!File.Exists(launcherExe))
            {
                Console.WriteLine("Launcher principal non trouvé, installation initiale...");

                GoogleDriveHelper.DownloadFile(onlineLauncherZipId, launcherZip);

                ZipFile.ExtractToDirectory(launcherZip, rootPath, true);
                File.Delete(launcherZip);

                Version initialVersion = new Version(webClient.DownloadString(onlineLauncherVersionUrl));
                File.WriteAllText(launcherVersionFile, initialVersion.ToString());

                Console.WriteLine("Installation initiale terminée !");
            }

            try
            {
                // 3️⃣ Vérifier la version en ligne
                Version onlineVersion = new Version(webClient.DownloadString(onlineLauncherVersionUrl));
                Version localVersion = File.Exists(launcherVersionFile)
                    ? new Version(File.ReadAllText(launcherVersionFile))
                    : Version.zero;

                if (onlineVersion.IsDifferentThan(localVersion))
                {
                    Console.WriteLine($"Nouvelle version disponible : {onlineVersion}. Téléchargement en cours...");

                    GoogleDriveHelper.DownloadFile(onlineLauncherZipId, launcherZip);

                    // Attendre que l'ancien launcher se ferme
                    var runningLauncher = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(launcherExe))
                        .FirstOrDefault();
                    runningLauncher?.WaitForExit();

                    // Extraire le nouveau launcher
                    ZipFile.ExtractToDirectory(launcherZip, rootPath, true);
                    File.Delete(launcherZip);

                    File.WriteAllText(launcherVersionFile, onlineVersion.ToString());

                    Console.WriteLine("Launcher mis à jour !");
                }
                else
                {
                    Console.WriteLine("Launcher déjà à jour.");
                }

                // 4️⃣ Lancer le launcher principal
                Console.WriteLine("Lancement du launcher principal...");
                Process.Start(launcherExe);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur dans LauncherUpdater : {ex.Message}");
                try
                {
                    Process.Start(launcherExe);
                }
                catch
                {
                }
            }

            Console.WriteLine("=== Fin de LauncherUpdater ===");
        }

        // Vérifie si .NET 9 est installé
        static bool IsDotNet9Installed()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-runtimes",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output.Contains("Microsoft.NETCore.App 9.");
            }
            catch
            {
                return false;
            }
        }

        // Télécharge et installe .NET 9 silencieusement
        static void InstallDotNet9()
        {
            string installerPath = Path.Combine(Path.GetTempPath(), "dotnet-runtime-9.exe");
            using WebClient client = new WebClient();
            client.DownloadFile(dotnetRuntimeUrl, installerPath);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/install /quiet /norestart",
                UseShellExecute = true
            };
            Process.Start(psi).WaitForExit();
        }
    }

    // Version struct identique à ton launcher
    struct Version
    {
        internal static Version zero = new Version(0, 0, 0);
        private short major, minor, subMinor;

        internal Version(short _major, short _minor, short _subMinor)
        {
            major = _major;
            minor = _minor;
            subMinor = _subMinor;
        }

        internal Version(string _version)
        {
            string[] parts = _version.Split('.');
            if (parts.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }

            major = short.Parse(parts[0]);
            minor = short.Parse(parts[1]);
            subMinor = short.Parse(parts[2]);
        }

        internal bool IsDifferentThan(Version other)
        {
            return major != other.major || minor != other.minor || subMinor != other.subMinor;
        }

        public override string ToString() => $"{major}.{minor}.{subMinor}";
    }

    // GoogleDriveHelper simplifié
    public static class GoogleDriveHelper
    {
        public static void DownloadFile(string fileId, string destinationPath)
        {
            string baseUrl = $"https://drive.google.com/uc?export=download&id={fileId}";
            using WebClient client = new WebClient();
            client.DownloadFile(baseUrl, destinationPath);
        }
    }
}
