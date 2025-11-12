using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Windows;

namespace GameLauncher
{
    enum LauncherStatus
    {
        Ready,
        Failed,
        DownloadingGame,
        DownloadingUpdate
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly string _rootPath;
        private readonly string _versionFile;
        private readonly string _gameZip;
        private readonly string _gameExe;
        
        private const string OnlineVersionUrl = "https://drive.google.com/uc?export=download&id=17U6jVvrcR_7zatDoGBx0-mYLgdn7UKrO";
        private const string OnlineGameZipId = "1MQfPGjsSgpk8rjTBq7TgLkJ6DIJSv_14";

        private LauncherStatus _status;

        private LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.Ready:
                        PlayButton.Content = "Play";
                        break;
                    case LauncherStatus.Failed:
                        PlayButton.Content = "Update Failed - Retry";
                        break;
                    case LauncherStatus.DownloadingGame:
                        PlayButton.Content = "Downloading Game";
                        break;
                    case LauncherStatus.DownloadingUpdate:
                        PlayButton.Content = "Downloading Update";
                        break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            _rootPath = Directory.GetCurrentDirectory();
            _versionFile = Path.Combine(_rootPath, "Version.txt");
            _gameZip = Path.Combine(_rootPath, "Build.zip");
            _gameExe = Path.Combine(_rootPath, "Build", "Mortier FU.exe");
        }

        private void CheckForUpdates()
        {
            if (File.Exists(_versionFile))
            {
                Version localVersion = new Version(File.ReadAllText(_versionFile));
                VersionText.Text = localVersion.ToString();

                try
                {
                    HttpClient httpClient = new HttpClient();
                    Version onlineVersion = new Version(httpClient.GetStringAsync(OnlineVersionUrl).Result);

                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        InstallGameFiles(true, onlineVersion);
                    }
                    else
                    {
                        Status = LauncherStatus.Ready;
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.Failed;
                    MessageBox.Show($"Error checking for game updates: {ex}");
                }
            }
            else
            {
                InstallGameFiles(false, Version.Zero);
            }
        }

        private async void InstallGameFiles(bool isUpdate, Version onlineVersion)
        {
            try
            {
                Status = isUpdate ? LauncherStatus.DownloadingUpdate : LauncherStatus.DownloadingGame;

                if (!isUpdate)
                {
                    using HttpClient httpClient = new HttpClient();
                    onlineVersion = new Version(httpClient.GetStringAsync(OnlineVersionUrl).Result);
                }

                // Téléchargement via Google Drive Helper
                await GoogleDriveHelperHttpClient.DownloadFileAsync(OnlineGameZipId, _gameZip);

                DownloadGameCompletedCallback(onlineVersion);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show($"Error installing game files: {ex}");
            }
        }



        private void DownloadGameCompletedCallback(Version onlineVersion)
        {
            try
            {
                // Vérifier l'entête ZIP
                byte[] header = File.ReadAllBytes(_gameZip).Take(4).ToArray();
                if (header[0] != 0x50 || header[1] != 0x4B)
                {
                    throw new InvalidDataException("Fichier téléchargé invalide ou lien expiré.");
                }

                ZipFile.ExtractToDirectory(_gameZip, _rootPath, true);
                File.Delete(_gameZip);

                File.WriteAllText(_versionFile, onlineVersion.ToString());
                VersionText.Text = onlineVersion.ToString();
                Status = LauncherStatus.Ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show($"Error finishing download: {ex}");
            }
        }



        private void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckForUpdates();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_gameExe) && Status == LauncherStatus.Ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(_gameExe)
                {
                    WorkingDirectory = Path.Combine(_rootPath, "Build")
                };
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.Failed)
            {
                CheckForUpdates();
            }
        }
    }

    internal readonly struct Version
    {
        internal static Version Zero = new Version(0, 0, 0);

        private readonly short _major;
        private readonly short _minor;
        private readonly short _subMinor;

        private Version(short major, short minor, short subMinor)
        {
            this._major = major;
            this._minor = minor;
            this._subMinor = subMinor;
        }
        internal Version(string version)
        {
            string[] versionStrings = version.Split('.');
            if (versionStrings.Length != 3)
            {
                _major = 0;
                _minor = 0;
                _subMinor = 0;
                return;
            }

            _major = short.Parse(versionStrings[0]);
            _minor = short.Parse(versionStrings[1]);
            _subMinor = short.Parse(versionStrings[2]);
        }

        internal bool IsDifferentThan(Version otherVersion)
        {
            if (_major != otherVersion._major)
            {
                return true;
            }
            else
            {
                if (_minor != otherVersion._minor)
                {
                    return true;
                }
                else
                {
                    if (_subMinor != otherVersion._subMinor)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override string ToString()
        {
            return $"{_major}.{_minor}.{_subMinor}";
        }
    }
}
