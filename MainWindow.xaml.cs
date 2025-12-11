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
using System.Diagnostics.CodeAnalysis;
using Windows.UI.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;
using SoftCircuits.IniFileParser;
using Windows.ApplicationModel.Resources;
using WinRT.Interop;

namespace Ra2Helper
{
    public class ResolutionItem
    {
        public string Text { get; set; }
        public bool IsCorrect { get; set; }
        public FontWeight FontWeight => IsCorrect ? FontWeights.Bold : FontWeights.Normal;
        public Brush Foreground => IsWarning ? new SolidColorBrush(Microsoft.UI.Colors.OrangeRed) : new SolidColorBrush(Microsoft.UI.Colors.Black);
        public bool IsWarning { get; set; }
    }

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
            Ra2Resolutions.ItemsSource = GetAllResolutionsWithCorrectFlag();
            YuriResolutions.ItemsSource = GetAllResolutionsWithCorrectFlag();
            resourceLoader = ResourceLoader.GetForViewIndependentUse();
            this.Title = resourceLoader.GetString("AppDisplayName");
            DetectEaAndSteamGameDir();
            if (gameDir != null)
            {
                UpdateUiByGameDir();
            }
            else
            {
                DetectInstallPathFromRegistry();
            }
        }

        private void DetectEaAndSteamGameDir()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] eaPaths = new string[]
            {
                Path.Combine(programFiles, "EA Games", "Command and Conquer Red Alert II"),
                Path.Combine(programFilesX86, "EA Games", "Command and Conquer Red Alert II")
            };

            string[] steamPaths = new string[]
            {
                Path.Combine(programFilesX86, "Steam", "steamapps", "common", "Command & Conquer Red Alert II"),
                Path.Combine(programFiles, "Steam", "steamapps", "common", "Command & Conquer Red Alert II")
            };

            foreach (var path in eaPaths)
            {
                if (Directory.Exists(path))
                {
                    gameDir = path;
                    return;
                }
            }
            foreach (var path in steamPaths)
            {
                if (Directory.Exists(path))
                {
                    gameDir = path;
                    return;
                }
            }
        }

        private void DetectInstallPathFromRegistry()
        {
            try
            {
                // 64-bit: HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Westwood\Red Alert 2
                // 32-bit: HKEY_LOCAL_MACHINE\SOFTWARE\Westwood\Red Alert 2
                string keyPath = @"HKLM\SOFTWARE\WOW6432Node\Westwood\Red Alert 2";
                if (!Environment.Is64BitOperatingSystem)
                {
                    keyPath = @"HKLM\SOFTWARE\Westwood\Red Alert 2";
                }

                string valueName = "InstallPath";
                string command = $"reg query \"{keyPath}\" /v {valueName}";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = new Process
                {
                    StartInfo = processInfo,
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data) && e.Data.Contains(valueName))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(e.Data, $@"{valueName}\s+REG_SZ\s+(.+)");
                        if (match.Success)
                        {
                            gameDir = Path.GetDirectoryName(match.Groups[1].Value.Trim());
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                UpdateUiByGameDir();
                            });
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
            }
            catch (Exception ex)
            {
                Notice.Message = $"{resourceLoader.GetString("WerePinnedDown")}: {ex.Message}";
                Notice.Severity = InfoBarSeverity.Error;
            }
        }

        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ResolutionItem))]
        private List<ResolutionItem> GetAllResolutionsWithCorrectFlag()
        {
            List<string> all = new();
            var v = new DEVMODE();
            var i = 0;
            int maxW = 0;
            int maxH = 0;
            long maxArea = -1;
            while (EnumDisplaySettings(null, i, ref v))
            {
                var w = v.dmPelsWidth;
                var h = v.dmPelsHeight;
                var s = $"{w}x{h}";
                if (!all.Contains(s))
                {
                    all.Add(s);
                    var area = (long)w * h;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxW = w;
                        maxH = h;
                    }
                }
                i++;
            }
            double highest = maxH == 0 ? 0.0 : (double)maxW / maxH;
            double target = Math.Truncate(highest * 10.0) / 10.0;
            List<ResolutionItem> items = new();
            foreach (var s in all)
            {
                var parts = s.Split('x');
                if (parts.Length != 2)
                {
                    continue;
                }
                if (!int.TryParse(parts[0], out var w))
                {
                    continue;
                }
                if (!int.TryParse(parts[1], out var h))
                {
                    continue;
                }
                if (h == 0)
                {
                    continue;
                }
                var ratio = (double)w / h;
                var r = Math.Truncate(ratio * 10.0) / 10.0;
                items.Add(new ResolutionItem { Text = s, IsCorrect = Math.Abs(r - target) < 1e-9 });
            }
            items.Sort((a, b) =>
            {
                var pa = a.Text.Split('x');
                var pb = b.Text.Split('x');
                int wa = int.Parse(pa[0]);
                int ha = int.Parse(pa[1]);
                int wb = int.Parse(pb[0]);
                int hb = int.Parse(pb[1]);
                long aa = (long)wa * ha;
                long ab = (long)wb * hb;
                return ab.CompareTo(aa);
            });
            return items;
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
            UpdateUiByGameDir();
        }

        private void UpdateUiByGameDir()
        {
            if (!System.IO.File.Exists(gameDir + "\\game.exe") && !System.IO.File.Exists(gameDir + "\\gamemd.exe"))
            {
                Notice.Message = resourceLoader.GetString("HowAboutATarget");
                Notice.Severity = InfoBarSeverity.Error;
                EnableDisableGridElements(Features, false);
                Ra2.Visibility = Visibility.Collapsed;
                Yuri.Visibility = Visibility.Collapsed;
                return;
            }
            var hasRa2 = System.IO.File.Exists(gameDir + "\\game.exe");
            var hasYuri = System.IO.File.Exists(gameDir + "\\gamemd.exe");
            Ra2.Visibility = hasRa2 ? Visibility.Visible : Visibility.Collapsed;
            Yuri.Visibility = hasYuri ? Visibility.Visible : Visibility.Collapsed;
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
            Ra2Resolutions.SelectedItem = null;
            YuriResolutions.SelectedItem = null;
            EnableDisableGridElements(Features, true);
            Ra2Resolutions.IsEnabled = hasRa2;
            Ra2PlayIntroVideo.IsEnabled = hasRa2;
            YuriResolutions.IsEnabled = hasYuri;
            YuriPlayIntroVideo.IsEnabled = hasYuri;
            CheckPlayIntroVideo();
            AutoSelectResolutionFromIni();
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

        private void Ra2Resolutions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HandleResolutionSelection(Ra2Resolutions, "ra2.ini");
        }

        private void YuriResolutions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HandleResolutionSelection(YuriResolutions, "ra2md.ini");
        }

        private void HandleResolutionSelection(ComboBox combo, string iniFile)
        {
            if (combo?.SelectedItem == null)
            {
                return;
            }
            var resolution = (combo.SelectedItem as ResolutionItem)?.Text ?? combo.SelectedItem.ToString();
            SetResolutionToIniFile(iniFile, resolution);
            SetResolutionToDDrawCompatIniFile(resolution);
        }

        private void SetResolutionToIniFile(string iniFile, string resolution)
        {
            // step 2: add AllowHiResModes=yes to iniFile
            // step 3: add resolution to iniFile
            var file = new IniFile();
            var iniPath = Path.Combine(gameDir ?? string.Empty, iniFile);
            if (!File.Exists(iniPath))
            {
                return;
            }

            file.Load(iniPath);
            file.SetSetting("Video", "AllowHiResModes", "yes");
            var parts = resolution.Split('x');
            if (parts.Length >= 2)
            {
                file.SetSetting("Video", "ScreenWidth", parts[0]);
                file.SetSetting("Video", "ScreenHeight", parts[1]);
            }
            file.Save(iniPath);

            if (File.ReadAllText(iniPath).Contains("AllowHiResModes=yes")
                && file.GetSetting("Video", "ScreenWidth") == parts[0])
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

        private string GetResolutionFromIni(string iniFile)
        {
            var p = Path.Combine(gameDir ?? string.Empty, iniFile);
            if (!File.Exists(p))
            {
                return null;
            }

            var file = new IniFile();
            file.Load(p);
            var w = file.GetSetting("Video", "ScreenWidth");
            var h = file.GetSetting("Video", "ScreenHeight");
            if (int.TryParse(w, out var wi) && int.TryParse(h, out var hi) && wi > 0 && hi > 0)
            {
                return wi + "x" + hi;
            }

            return null;
        }

        private void AutoSelectResolutionFromIni()
        {
            AutoSelectResolutionFor(Ra2Resolutions, "ra2.ini");
            AutoSelectResolutionFor(YuriResolutions, "ra2md.ini");
        }

        private void AutoSelectResolutionFor(ComboBox combo, string iniFile)
        {
            var res = GetResolutionFromIni(iniFile);
            if (!string.IsNullOrEmpty(res) && combo.ItemsSource is List<ResolutionItem> items)
            {
                var found = items.Find(i => i.Text == res);
                if (found == null)
                {
                    found = new ResolutionItem { Text = res, IsCorrect = false, IsWarning = true };
                    items.Insert(0, found);
                    combo.ItemsSource = null;
                    combo.ItemsSource = items;
                }
                combo.SelectedItem = found;
            }
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

        private void Ra2PlayIntroVideo_Toggled(object sender, RoutedEventArgs e)
        {
            HandlePlayIntroToggle(sender as ToggleSwitch, "ra2.ini");
        }

        private void YuriPlayIntroVideo_Toggled(object sender, RoutedEventArgs e)
        {
            HandlePlayIntroToggle(sender as ToggleSwitch, "ra2md.ini");
        }

        private void HandlePlayIntroToggle(ToggleSwitch toggleSwitch, string iniFile)
        {
            if (toggleSwitch == null)
            {
                return;
            }
            var play = toggleSwitch.IsOn ? "yes" : "no";
            var file = new IniFile();
            var iniPath = Path.Combine(gameDir ?? string.Empty, iniFile);
            if (!File.Exists(iniPath))
            {
                return;
            }

            file.Load(iniPath);
            file.SetSetting("Intro", "Play", play);
            file.Save(iniPath);
        }

        private bool GetPlayIntroStatus(string iniFile)
        {
            var iniPath = Path.Combine(gameDir ?? string.Empty, iniFile);
            if (File.Exists(iniPath))
            {
                var file = new IniFile();
                file.Load(iniPath);
                return file.GetSetting("Intro", "Play") == "yes";
            }
            return false;
        }

        /**
         * check PlayIntroVideo is on or off, and set the toggle switch
         */
        private void CheckPlayIntroVideo()
        {
            Ra2PlayIntroVideo.IsOn = GetPlayIntroStatus("ra2.ini");
            YuriPlayIntroVideo.IsOn = GetPlayIntroStatus("ra2md.ini");
        }

        /**
         * start game.exe
         */
        private void StartRa2_Click(object sender, RoutedEventArgs e)
        {
            StartGame("game.exe");
        }

        private void StartYuri_Click(object sender, RoutedEventArgs e)
        {
            StartGame("gamemd.exe");
        }

        private void StartGame(string exeName)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(gameDir ?? string.Empty, exeName),
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
