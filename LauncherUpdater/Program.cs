using System.Diagnostics;
using System.Net;
using HtmlAgilityPack;
using System.Web;
using System.IO.Compression;

namespace LauncherUpdater
{
    internal static class Program
    {
        private const string OnlineLauncherVersionUrl =
            "https://drive.google.com/uc?export=download&id=1YzTah-51gTQ4aXbro0gu7i5UVm5h31xr";

        private const string LauncherZipId =
            "1hjKUsueM6QvcVfLjiRNFaGVt6Lu_dJaM"; // ID de ton zip
        private const string DotnetRuntimeUrl =
            "https://download.visualstudio.microsoft.com/download/pr/xxx/dotnet-runtime-9.0.0-win-x64.exe";

        static async Task Main()
        {
            string rootPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Mortar Game");

            string versionFile = Path.Combine(rootPath, "LauncherVersion.txt");
            string launcherExe = Path.Combine(rootPath, "GameLauncher.exe");

            Console.WriteLine("=== LauncherUpdater démarré ===");

            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            if (!IsDotNet9Installed())
            {
                Console.WriteLine(".NET 9 Runtime absent → installation…");
                await InstallDotNet9Async();
            }
            else
            {
                Console.WriteLine(".NET 9 Runtime détecté.");
            }

            // ---- Vérifier version ----
            using HttpClient client = new HttpClient();
            string onlineVersionStr = await client.GetStringAsync(OnlineLauncherVersionUrl);
            Version onlineVersion = new Version(onlineVersionStr);

            Version localVersion = File.Exists(versionFile)
                ? new Version(await File.ReadAllTextAsync(versionFile))
                : Version.Zero;

            bool needUpdate = !File.Exists(launcherExe) || localVersion.IsDifferentThan(onlineVersion);

            if (needUpdate)
            {
                Console.WriteLine($"Téléchargement ZIP v{onlineVersion}…");
                string zipPath = Path.Combine(Path.GetTempPath(), "GameLauncher.zip");

                await GoogleDriveHelperHttpClient.DownloadFileAsync(LauncherZipId, zipPath, handleConfirmation: true);

                Console.WriteLine("Décompression…");
                ExtractZipSafe(zipPath, rootPath);
                File.Delete(zipPath);

                await File.WriteAllTextAsync(versionFile, onlineVersion.ToString());
                Console.WriteLine("Mise à jour appliquée !");
            }
            else
            {
                Console.WriteLine("Launcher déjà à jour.");
            }

            // ---- Lancer le launcher ----
            Console.WriteLine("Lancement du launcher…");
            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(launcherExe)).Length == 0)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = launcherExe,
                    Arguments = "--from-updater",
                    UseShellExecute = false,
                    CreateNoWindow = false
                });
            }

        }

        // ------------------ UTILITAIRES ------------------

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

        static void ExtractZipSafe(string zipPath, string destDir)
        {
            if (Directory.Exists(destDir))
            {
                //Do nothing, we will overwrite files
            }
            else Directory.CreateDirectory(destDir);

            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
            {
                string fullPath = Path.Combine(destDir, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                entry.ExtractToFile(fullPath, overwrite: true);
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
                    _major = 0; _minor = 0; _subMinor = 0;
                    return;
                }
                _major = short.Parse(parts[0]);
                _minor = short.Parse(parts[1]);
                _subMinor = short.Parse(parts[2]);
            }

            internal bool IsDifferentThan(Version other)
                => _major != other._major || _minor != other._minor || _subMinor != other._subMinor;

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

            public static async Task DownloadFileAsync(string fileId, string destinationPath, bool handleConfirmation)
            {
                string url = $"https://drive.google.com/uc?export=download&id={fileId}";

                if (!handleConfirmation)
                {
                    using var response = await Client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                    return;
                }

                // Récupération page HTML pour confirmation
                var html = await Client.GetStringAsync(url);
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                var form = doc.DocumentNode.SelectSingleNode("//form[@id='download-form']");
                if (form == null)
                {
                    // Pas de formulaire → fichier < 50Mo, téléchargement direct
                    using var resp = await Client.GetAsync(url);
                    resp.EnsureSuccessStatusCode();
                    await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await resp.Content.CopyToAsync(fs);
                    return;
                }

                string action = form.GetAttributeValue("action", null!);
                var inputs = form.SelectNodes(".//input[@type='hidden']");
                var query = HttpUtility.ParseQueryString(string.Empty);
                foreach (var input in inputs)
                {
                    string name = input.GetAttributeValue("name", "");
                    string value = input.GetAttributeValue("value", "");
                    if (!string.IsNullOrEmpty(name)) query[name] = value;
                }

                string downloadUrl = $"{action}?{query}";
                using var response2 = await Client.GetAsync(downloadUrl);
                response2.EnsureSuccessStatusCode();
                await using var fs2 = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response2.Content.CopyToAsync(fs2);
            }
        }
    }
}
