using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static PInvoke.User32;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Ra2Helper
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);

            SetWindowDetails(hwnd, 800, 600);

            m_window.Activate();
        }

        // ...

        private static void SetWindowDetails(IntPtr hwnd, int width, int height)
        {
            var dpi = GetDpiForWindow(hwnd);
            float scalingFactor = (float)dpi / 96;
            width = (int)(width * scalingFactor);
            height = (int)(height * scalingFactor);

            _ = SetWindowPos(hwnd, SpecialWindowHandles.HWND_TOP,
                                        0, 0, width, height,
                                        SetWindowPosFlags.SWP_NOMOVE);
            _ = SetWindowLong(hwnd,
                   WindowLongIndexFlags.GWL_STYLE,
                   (SetWindowLongFlags)(GetWindowLong(hwnd,
                      WindowLongIndexFlags.GWL_STYLE) &
                      ~(int)SetWindowLongFlags.WS_MINIMIZEBOX &
                      ~(int)SetWindowLongFlags.WS_MAXIMIZEBOX));
        }

        private Window? m_window;
    }
}
