using System.Diagnostics;
using System.Net;
using HtmlAgilityPack;
using System.Web;

namespace LauncherUpdater
{
    internal static class Program
    {
        private const string OnlineLauncherVersionUrl =
            "https://drive.google.com/uc?export=download&id=1YzTah-51gTQ4aXbro0gu7i5UVm5h31xr";

        private const string OnlineLauncherExeId = "1hjKUsueM6QvcVfLjiRNFaGVt6Lu_dJaM";

        private const string DotnetRuntimeUrl =
            "https://download.visualstudio.microsoft.com/download/pr/xxx/dotnet-runtime-9.0.0-win-x64.exe";

        static async Task Main()
        {
            string rootPath = AppDomain.CurrentDomain.BaseDirectory;
            string launcherExe = Path.Combine(rootPath, "GameLauncher.exe");
            string launcherVersionFile = Path.Combine(rootPath, "LauncherVersion.txt");

            Console.WriteLine("=== LauncherUpdater démarré ===");

            if (!IsDotNet9Installed())
            {
                Console.WriteLine(".NET 9 Runtime non trouvé. Téléchargement et installation...");
                await InstallDotNet9Async();
                Console.WriteLine(".NET 9 installé !");
            }
            else
            {
                Console.WriteLine(".NET 9 Runtime détecté.");
            }

            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            try
            {
                using HttpClient client = new HttpClient();

                // Récupérer la version en ligne
                string onlineVersionString = await client.GetStringAsync(OnlineLauncherVersionUrl);
                Version onlineVersion = new Version(onlineVersionString);

                // Lire la version locale
                Version localVersion = File.Exists(launcherVersionFile)
                    ? new Version(await File.ReadAllTextAsync(launcherVersionFile))
                    : Version.Zero;

                if (onlineVersion.IsDifferentThan(localVersion))
                {
                    Console.WriteLine($"Nouvelle version disponible : {onlineVersion}. Téléchargement en cours...");

                    // Télécharger le .exe mis à jour
                    await GoogleDriveHelperHttpClient.DownloadFileAsync(OnlineLauncherExeId, launcherExe);

                    // Mettre à jour la version locale
                    await File.WriteAllTextAsync(launcherVersionFile, onlineVersion.ToString());

                    Console.WriteLine("Launcher mis à jour !");
                }
                else
                {
                    Console.WriteLine("Launcher déjà à jour.");
                }

                Console.WriteLine("Lancement du launcher principal...");
                Process.Start(launcherExe);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur dans LauncherUpdater : {ex.Message}");
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = launcherExe,
                        Arguments = $"--from-updater",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    Console.WriteLine("Échec du lancement du launcher principal.");
                }
            }

            Console.WriteLine("=== Fin de LauncherUpdater ===");


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
                    string? output = process?.StandardOutput.ReadToEnd();
                    process?.WaitForExit();
                    return output != null && output.Contains("Microsoft.NETCore.App 9.");
                }
                catch
                {
                    return false;
                }
            }

            static async Task InstallDotNet9Async()
            {
                string installerPath = Path.Combine(Path.GetTempPath(), "dotnet-runtime-9.exe");

                using HttpClient client = new HttpClient();
                using HttpResponseMessage response = await client.GetAsync(DotnetRuntimeUrl);
                response.EnsureSuccessStatusCode();

                await using var fs = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/install /quiet /norestart",
                    UseShellExecute = true
                };
                await Process.Start(psi)?.WaitForExitAsync()!;
            }

        }

        readonly struct Version
        {
            internal static readonly Version Zero = new Version(0, 0, 0);
            private readonly short _major, _minor, _subMinor;

            private Version(short major, short minor, short subMinor)
            {
                _major = major;
                _minor = minor;
                _subMinor = subMinor;
            }

            internal Version(string version)
            {
                string[] parts = version.Split('.');
                if (parts.Length != 3)
                {
                    _major = 0;
                    _minor = 0;
                    _subMinor = 0;
                    return;
                }

                _major = short.Parse(parts[0]);
                _minor = short.Parse(parts[1]);
                _subMinor = short.Parse(parts[2]);
            }

            internal bool IsDifferentThan(Version other)
            {
                return _major != other._major || _minor != other._minor || _subMinor != other._subMinor;
            }

            public override string ToString() => $"{_major}.{_minor}.{_subMinor}";
        }

        private static class GoogleDriveHelperHttpClient
        {
            private static readonly HttpClientHandler Handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            private static readonly HttpClient Client = new HttpClient(Handler);

            public static async Task DownloadFileAsync(string fileId, string destinationPath)
            {
                string baseUrl = $"https://drive.google.com/uc?export=download&id={fileId}";

                // Étape 1 : Télécharger la page d'avertissement
                var htmlPage = await Client.GetStringAsync(baseUrl);

                // Étape 2 : Parser la page HTML
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlPage);

                var formNode = doc.DocumentNode.SelectSingleNode("//form[@id='download-form']");
                if (formNode == null)
                    throw new Exception("Formulaire de téléchargement introuvable sur la page Google Drive.");

                string actionUrl = formNode.GetAttributeValue("action", null!);
                if (string.IsNullOrEmpty(actionUrl))
                    throw new Exception("Attribut 'action' manquant dans le formulaire de téléchargement.");

                var inputs = formNode.SelectNodes(".//input[@type='hidden']");
                if (inputs == null)
                    throw new Exception("Champs de formulaire manquants dans la page Google Drive.");

                var query = HttpUtility.ParseQueryString(string.Empty);
                foreach (var input in inputs)
                {
                    string name = input.GetAttributeValue("name", "");
                    string value = input.GetAttributeValue("value", "");
                    if (!string.IsNullOrEmpty(name))
                        query[name] = value;
                }

                string downloadUrl = $"{actionUrl}?{query}";

                // Étape 3 : Télécharger le fichier réel
                using var response = await Client.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }
        }
    }
}
