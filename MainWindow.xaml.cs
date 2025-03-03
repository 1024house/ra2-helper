using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using SoftCircuits.IniFileParser;
using Windows.ApplicationModel.Resources;
using WinRT.Interop;

namespace Ra2Helper
{
    public sealed partial class MainWindow : Window
    {
        private string gameDir;
        private ResourceLoader resourceLoader;
        public MainWindow()
        {
            InitializeComponent();
            this.AppWindow.SetIcon("App.ico");
            AppWindow.SetPresenter(AppWindowPresenterKind.Default);
            AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1280, Height = 1024 });
            Resolutions.ItemsSource = GetSystemResolutions();
            resourceLoader = ResourceLoader.GetForViewIndependentUse();
            this.Title = resourceLoader.GetString("AppDisplayName");
        }

        private List<string> GetSystemResolutions()
        {
            List<string> systemResolutions = new();
            var vDevMode = new DEVMODE();
            var i = 0;
            while (EnumDisplaySettings(null, i, ref vDevMode))
            {
                var resolution = $"{vDevMode.dmPelsWidth}x{vDevMode.dmPelsHeight}";
                if (!systemResolutions.Contains(resolution))
                {
                    systemResolutions.Insert(0, resolution);
                }
                i++;
            }
            return systemResolutions;
        }

        private void SelectGame_Click(object sender, RoutedEventArgs e)
        {
            // select file game.exe
            var openPicker = new Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder
            };
            openPicker.FileTypeFilter.Add(".exe");

            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(openPicker, hwnd);

            var file = openPicker.PickSingleFileAsync().GetAwaiter().GetResult();
            if (file == null)
            {
                Notice.Message = resourceLoader.GetString("GiveMeATarget");
                Notice.Severity = InfoBarSeverity.Warning;
                EnableDisableGridElements(Features, false);
                return;
            }
            gameDir = System.IO.Path.GetDirectoryName(file.Path);
            if (!System.IO.File.Exists(gameDir + "\\game.exe") && !System.IO.File.Exists(gameDir + "\\gamemd.exe"))
            {
                Notice.Message = resourceLoader.GetString("HowAboutATarget");
                Notice.Severity = InfoBarSeverity.Error;
                EnableDisableGridElements(Features, false);
                Ra2.Visibility = Visibility.Collapsed;
                Yuri.Visibility = Visibility.Collapsed;
                return;
            }
            Ra2.Visibility = Visibility.Visible;
            if (System.IO.File.Exists(gameDir + "\\gamemd.exe"))
            {
                Yuri.Visibility = Visibility.Visible;
            }
            if (!IsDirectoryWritable(gameDir))
            {
                Notice.Message = resourceLoader.GetString("DirectoryRequiresAdmin");
                Notice.Severity = InfoBarSeverity.Error;
                EnableDisableGridElements(Features, false);
                FixPermission.Visibility = Visibility.Visible;
                return;
            }
            Notice.Message = gameDir;
            Notice.Severity = InfoBarSeverity.Success;
            Resolutions.SelectedItem = null;
            EnableDisableGridElements(Features, true);
            CheckPlayIntroVideo();
        }

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(
                 string deviceName, int modeNum, ref DEVMODE devMode);

        public enum ScreenOrientation : int
        {
            DMDO_DEFAULT = 0,
            DMDO_90 = 1,
            DMDO_180 = 2,
            DMDO_270 = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            private const int CCHDEVICENAME = 0x20;
            private const int CCHFORMNAME = 0x20;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public ScreenOrientation dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;

        }

        private void Resolutions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Resolutions.SelectedItem == null)
            {
                return;
            }
            var resolution = Resolutions.SelectedItem.ToString();
            SetResolutionToDDrawCompatIniFile(resolution);
            SetResolutionToRa2AndRa2mdIniFile(resolution);
        }

        private void SetResolutionToRa2AndRa2mdIniFile(string resolution)
        {
            // step 2: add AllowHiResModes=yes to ra2.ini and ra2md.ini
            // step 3: add resolution to ra2.ini and ra2md.ini
            string[] iniFiles = ["ra2.ini", "ra2md.ini"];
            var file = new IniFile();
            var successCount = 0;
            foreach (var iniFile in iniFiles)
            {
                var iniPath2 = gameDir + "\\" + iniFile;
                if (!File.Exists(iniPath2))
                {
                    continue;
                }
                file.Load(iniPath2);
                file.SetSetting("Video", "AllowHiResModes", "yes");
                file.SetSetting("Video", "ScreenWidth", resolution.Split('x')[0]);
                file.SetSetting("Video", "ScreenHeight", resolution.Split('x')[1]);
                file.Save(iniPath2);
                if (File.ReadAllText(iniPath2).Contains("AllowHiResModes=yes")
                    && file.GetSetting("Video", "ScreenWidth") == resolution.Split('x')[0])
                {
                    successCount++;
                }
            }
            if (successCount > 0)
            {
                Notice.Message = resourceLoader.GetString("VisibilityClear") + resolution;
                Notice.Severity = InfoBarSeverity.Success;
            }
            else
            {
                Notice.Message = resourceLoader.GetString("WerePinnedDown");
                Notice.Severity = InfoBarSeverity.Error;
            }
        }

        /**
         * step 1: Add resolution to DDrawCompat.ini file
         * DDrawCompat.ini have no section, it's not a standard ini file, fu*k!
         * so we can't use library but read and write file directly
         */
        private void SetResolutionToDDrawCompatIniFile(string resolution)
        {
            var iniFile = gameDir + "\\DDrawCompat.ini";
            if (!File.Exists(iniFile))
            {
                return;
            }
            var lines = File.ReadAllLines(iniFile);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("SupportedResolutions"))
                {
                    if (lines[i].Contains(resolution))
                    {
                        return;
                    }
                    lines[i] = lines[i] + ", " + resolution;
                    break;
                }
            }
            File.WriteAllLines(iniFile, lines);
        }

        // click button to fix lan battle program by unzip ipxwrapper.zip from Assets dir to game directory
        private void FixLanBattle_Click(object sender, RoutedEventArgs e)
        {
            var zipPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Assets\\ipxwrapper-0.7.1.zip");
            UnzipWithoutDirectory(zipPath, gameDir);
            Notice.Message = resourceLoader.GetString("IHaveTheTools") + "https://github.com/solemnwarning/ipxwrapper";
            Notice.Severity = InfoBarSeverity.Success;
            FixLanBattle.IsChecked = true;
        }

        public void UnzipWithoutDirectory(string zipFilePath, string destinationDirectory)
        {
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }
            using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string fileName = Path.GetFileName(entry.FullName);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }
                    string destinationPath = Path.Combine(destinationDirectory, fileName);
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }
        }

        private void EnableDisableGridElements(Panel panel, bool flag)
        {
            foreach (var child in panel.Children)
            {
                if (child is Panel childPanel)
                {
                    EnableDisableGridElements(childPanel, flag);
                }
                if (child is Control control)
                {
                    control.IsEnabled = flag;
                }
            }
        }

        static bool IsDirectoryWritable(string directoryPath)
        {
            try
            {
                File.Delete(Path.Combine(directoryPath, "Ra2Helper.log"));
                File.WriteAllText(Path.Combine(directoryPath, "Ra2Helper.log"), "check directory writable");
                File.Delete(Path.Combine(directoryPath, "Ra2Helper.log"));
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred: {ex.Message}");
                return false; // Other errors
            }
        }

        private void FixPermission_Click(object sender, RoutedEventArgs e)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c icacls \"{gameDir}\" /grant {WindowsIdentity.GetCurrent().Name}:(OI)(CI)F",
                UseShellExecute = true,
                Verb = "runas"
            };

            var process = new Process
            {
                StartInfo = processInfo,
                EnableRaisingEvents = true
            };

            process.Exited += (sender, e) =>
            {
                // This code will run when the process exits
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (IsDirectoryWritable(gameDir))
                    {
                        Notice.Message = resourceLoader.GetString("HesInMyScope");
                        Notice.Severity = InfoBarSeverity.Success;
                        FixPermission.Visibility = Visibility.Collapsed;
                        EnableDisableGridElements(Features, true);
                    }
                    else
                    {
                        Notice.Message = resourceLoader.GetString("WerePinnedDown");
                        Notice.Severity = InfoBarSeverity.Error;
                    }
                });
            };

            process.Start();
        }

        private void PlayIntroVideo_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch == null)
            {
                return;
            }
            var play = toggleSwitch.IsOn ? "yes" : "no";
            string[] iniFiles = ["ra2.ini", "ra2md.ini"];
            var file = new IniFile();
            foreach (var iniFile in iniFiles)
            {
                var iniPath2 = gameDir + "\\" + iniFile;
                if (!File.Exists(iniPath2))
                {
                    continue;
                }
                file.Load(iniPath2);
                file.SetSetting("Intro", "Play", play);
                file.Save(iniPath2);
            }
        }

        /**
         * check PlayIntroVideo is on or off, and set the toggle switch
         */
        private void CheckPlayIntroVideo()
        {
            string[] iniFiles = ["ra2.ini", "ra2md.ini"];
            var file = new IniFile();
            var yesCount = 0;
            foreach (var iniFile in iniFiles)
            {
                var iniPath2 = gameDir + "\\" + iniFile;
                if (!File.Exists(iniPath2))
                {
                    continue;
                }
                file.Load(iniPath2);
                if (file.GetSetting("Intro", "Play") == "yes")
                {
                    yesCount++;
                }
            }
            PlayIntroVideo.IsOn = yesCount == 2;
        }

        /**
         * start game.exe
         */
        private void StartRa2_Click(object sender, RoutedEventArgs e)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = gameDir + "\\game.exe",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized
            };

            var process = new Process
            {
                StartInfo = processInfo,
                EnableRaisingEvents = true
            };

            process.Start();
        }

        private void StartYuri_Click(object sender, RoutedEventArgs e)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = gameDir + "\\gamemd.exe",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized
            };

            var process = new Process
            {
                StartInfo = processInfo,
                EnableRaisingEvents = true
            };

            process.Start();
        }

        /**
         * Fix FATAL String Manager failed to initialized properly
         * click button to set the exe file's Windows Program Compatibility to WINXPSP2
         */
        private void FixLanFatalStringManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string gameExePath = gameDir + "\\game.exe";
                string gamemdExePath = gameDir + "\\gamemd.exe";
                string keyPath = $"HKCU\\Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers";
                string command = $"reg add \"{keyPath}\" /v \"{gameExePath}\" /d \"~ WINXPSP2\" /f";
                string command2 = $"reg add \"{keyPath}\" /v \"{gamemdExePath}\" /d \"~ WINXPSP2\" /f";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command} & {command2}",
                    UseShellExecute = true
                };

                var process = new Process
                {
                    StartInfo = processInfo,
                    EnableRaisingEvents = true
                };

                process.Exited += (s, args) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (process.ExitCode == 0)
                        {
                            Notice.Message = resourceLoader.GetString("HesInMyScope");
                            Notice.Severity = InfoBarSeverity.Success;
                        }
                        else
                        {
                            Notice.Message = resourceLoader.GetString("GiveMeATarget");
                            Notice.Severity = InfoBarSeverity.Error;
                        }
                    });
                };

                process.Start();
            }
            catch (Exception ex)
            {
                Notice.Message = $"{resourceLoader.GetString("WerePinnedDown")}: {ex.Message}";
                Notice.Severity = InfoBarSeverity.Error;
            }
        }
    }
}
