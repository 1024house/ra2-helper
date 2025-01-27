using System;
using Microsoft.UI.Xaml;
using WinRT.Interop;

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
        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            myButton.Content = "Clicked";
        }

        /*
         * click button to select a folder
         */
        private void selectFolder_Click(object sender, RoutedEventArgs e)
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
                myButton.Content = "Operation cancelled.";
                return;
            }
            // Application now has read/write access to all contents in the picked folder
            // (including other sub-folder contents)
            Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
            myButton.Content = folder.Path;
            // if game.exe and gamemd.exe not in the folder, show error message
            if (!System.IO.File.Exists(folder.Path + "\\game.exe") && !System.IO.File.Exists(folder.Path + "\\gamemd.exe"))
            {
                myButton.Content = "Not the Red Alert 2 folder, sir!";
                return;
            }
        }
    }
}
