using System;
using System.ComponentModel;
using System.Xml.Linq;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using static System.Net.Mime.MediaTypeNames;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Ra2Helper
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            var size = new Windows.Graphics.SizeInt32(800, 600);
            appWindow.Resize(size);

            var presenter = appWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }
        }

        /*
         * click button to select a folder
         */
        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            // Create a FileOpenPicker
            Windows.Storage.Pickers.FolderPicker folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            folderPicker.FileTypeFilter.Add("*");

            // Initialize with window handle
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(folderPicker, hwnd);

            // Show the picker
            Windows.Storage.StorageFolder folder = folderPicker.PickSingleFolderAsync().GetAwaiter().GetResult();
            if (folder == null)
            {
                notice.Text = "Operation cancelled.";
                return;
            }
            // Application now has read/write access to all contents in the picked folder
            // (including other sub-folder contents)
            Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
            notice.Text = folder.Path;
            // if game.exe and gamemd.exe not in the folder, show error message
            if (!System.IO.File.Exists(folder.Path + "\\game.exe") && !System.IO.File.Exists(folder.Path + "\\gamemd.exe"))
            {
                notice.Text = "Invalid directory! This is not the Red Alert 2 command center!";
                return;
            }
            if (!System.IO.File.Exists(folder.Path + "\\DDrawCompat.ini"))
            {
                notice.Text = "Unsupported INI file";
                return;
            }

            TextBox textBox = new TextBox();
            textBox.Name = "myTextBox";
            textBox.Text = "1920x1080";
            myStackPanel.Children.Add(textBox);

            Button addButton = new Button();
            addButton.Content = "Add Resolution";
            addButton.Click += AddResolution_Click;
            myStackPanel.Children.Add(addButton);
        }

        /**
         * click button to read resolution from myTextBox and write to DDrawCompat.ini
         */
        private async void AddResolution_Click(object sender, RoutedEventArgs e)
        {
            // Get the folder
            Windows.Storage.StorageFolder folder = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.GetFolderAsync("PickedFolderToken").GetAwaiter().GetResult();
            if (folder == null)
            {
                notice.Text = "Operation cancelled.";
                return;
            }
            // Get the file
            if (!System.IO.File.Exists(folder.Path + "\\DDrawCompat.ini"))
            {
                notice.Text = "Unsupported INI file, sir!";
                return;
            }
            Windows.Storage.StorageFile file = folder.GetFileAsync("DDrawCompat.ini").GetAwaiter().GetResult();
            // get value from dynamic element myTextBox
            TextBox myTextBox = (TextBox)myStackPanel.FindName("myTextBox");
            String resolution = myTextBox.Text;
            if (resolution.Trim() == "")
            {
                notice.Text = "Resolution is empty.";
                return;
            }
            // parse DDrawCompat.ini, find value by the key SupportedResolutions, if resolution not exists in the value, add it.
            String[] lines = System.IO.File.ReadAllLines(file.Path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("SupportedResolutions"))
                {
                    String[] resolutions = lines[i].Split('=')[1].Split(',');
                    for (int j = 0; j < resolutions.Length; j++)
                    {
                        if (resolutions[j].Trim().Equals(resolution))
                        {
                            notice.Text = "Resolution already exists.";
                            return;
                        }
                    }
                    lines[i] = lines[i] + ", " + resolution;
                    break;
                }
            }

            // Write lines to the file
            Windows.Storage.CachedFileManager.DeferUpdates(file);
            System.IO.File.WriteAllLines(file.Path, lines);
            Windows.Storage.Provider.FileUpdateStatus status = Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file).GetAwaiter().GetResult();
            if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
            {
                notice.Text = "Resolution added.";
            }
            else
            {
                notice.Text = "Resolution not added.";
            }
        }
    }
}
