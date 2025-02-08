using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SoftCircuits.IniFileParser;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Ra2Helper
{
    public sealed partial class MainWindow : Window
    {
        private string gameDir;
        private readonly List<string> systemResolutions = new();
        public MainWindow()
        {
            InitializeComponent();
            AppWindow.SetPresenter(AppWindowPresenterKind.Default);
            AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1024, Height = 768 });

            var vDevMode = new DEVMODE();
            var i = 0;
            while (EnumDisplaySettings(null, i, ref vDevMode))
            {
                var resolution = $"{vDevMode.dmPelsWidth}x{vDevMode.dmPelsHeight}";
                Debug.WriteLine(resolution);
                if (!systemResolutions.Contains(resolution))
                {
                    systemResolutions.Add(resolution);
                }
                i++;
            }
            Resolutions.ItemsSource = systemResolutions;
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
                Notice.Message = "Operation cancelled.";
                Notice.Severity = InfoBarSeverity.Warning;
                Resolutions.IsEnabled = false;
                return;
            }
            gameDir = System.IO.Path.GetDirectoryName(file.Path);
            if (!System.IO.File.Exists(gameDir + "\\game.exe") && !System.IO.File.Exists(gameDir + "\\gamemd.exe"))
            {
                Notice.Message = "Invalid directory! This is not the Red Alert 2 command center!";
                Notice.Severity = InfoBarSeverity.Error;
                return;
            }
            Notice.Message = gameDir;
            Notice.Severity = InfoBarSeverity.Success;
            Resolutions.IsEnabled = true;
            Resolutions.BorderBrush = new SolidColorBrush(Colors.Green);
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
            Debug.Print("Resolutions_SelectionChanged");
            Debug.Print(Resolutions.SelectedItem.ToString());
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
            foreach (var iniFile in iniFiles)
            {
                var iniPath2 = gameDir + "\\" + iniFile;
                file.Load(iniPath2);
                file.SetSetting("Video", "AllowHiResModes", "yes");
                file.SetSetting("Video", "ScreenWidth", resolution.Split('x')[0]);
                file.SetSetting("Video", "ScreenHeight", resolution.Split('x')[1]);
                file.Save(iniPath2);
            }
        }

        /**
         * step 1: Add resolution to DDrawCompat.ini file
         */
        private void SetResolutionToDDrawCompatIniFile(string resolution)
        {
            if (!System.IO.File.Exists(gameDir + "\\DDrawCompat.ini"))
            {
                return;
            }
            var file = new IniFile();
            var iniPath = gameDir + "\\DDrawCompat.ini";
            file.Load(iniPath);

            var iniLineSupportedResolutions = file.GetSetting(IniFile.DefaultSectionName, "SupportedResolutions", string.Empty).Trim();

            // if iniLineSupportedResolutions contains resolution, return
            if (iniLineSupportedResolutions.Contains(resolution))
            {
                return;
            }
            // if iniLineSupportedResolutions is empty, add resolution to the end of line
            if (string.IsNullOrEmpty(iniLineSupportedResolutions))
            {
                file.SetSetting(IniFile.DefaultSectionName, "SupportedResolutions", resolution);
            }
            // add resolution to the end of line
            file.SetSetting(IniFile.DefaultSectionName, "SupportedResolutions", $"{iniLineSupportedResolutions}, {resolution}");

            file.Save(iniPath);
        }
    }
}
