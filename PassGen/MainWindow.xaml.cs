using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using WinRT;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PassGen
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary> 

    [ObservableObject]
    public sealed partial class MainWindow : Window
    {
        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See separate sample below for implementation
        MicaController m_micaController;
        SystemBackdropConfiguration m_configurationSource;
        private AppWindow m_AppWindow;

        [ObservableProperty]
        private uint passwordLen = 4;

        [ObservableProperty]
        private bool upperOn = false;

        [ObservableProperty]
        private bool lowerOn = false;

        [ObservableProperty]
        private bool numberOn = false;

        [ObservableProperty]
        private bool symbolOn = false;

        private char[] UpperCase = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
        private char[] LowerCase = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
        private char[] Number = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
        private char[] SpecialCharacters = { '@', '!', '%', '*', '?', '$', '^', '(', ')', '_', '-', '+', '{', '}', '<', '>', '=', '[', ']', '#', '\\', '/', '\'', ';', '.', ',', '|', '~' };

        partial void OnPasswordLenChanged(uint value) => GeneratePassword();
        partial void OnUpperOnChanged(bool value) => GeneratePassword();
        partial void OnLowerOnChanged(bool value) => GeneratePassword();
        partial void OnNumberOnChanged(bool value) => GeneratePassword();
        partial void OnSymbolOnChanged(bool value) => GeneratePassword();

        public void GeneratePassword()
        {
            List<char> options = new();

            if (LowerOn) options.AddRange(LowerCase.ToList());
            if (UpperOn) options.AddRange(UpperCase.ToList());
            if (NumberOn) options.AddRange(Number.ToList());
            if (SymbolOn) options.AddRange(SpecialCharacters.ToList());

            if (options.Count < 1) return;
            StringBuilder builder = new();
            for (int i = 1; i <= PasswordLen; i++)
            {
                builder.Append(options[new Random().Next(0,options.Count - 1)]);
            }
            Password.Text= builder.ToString();
        }

        public MainWindow()
        {
            InitializeComponent();

            m_AppWindow = GetAppWindowForCurrentWindow();

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindowTitleBar titleBar = m_AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }
            else
            {
                AppTitleBar.Visibility = Visibility.Collapsed;
            }
            TrySetMicaBackdrop();

            var hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            OverlappedPresenter presenter = appWindow.Presenter as OverlappedPresenter;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            Windows.Graphics.SizeInt32 size = new(500,600);
            appWindow.Resize(size);
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }

        bool TrySetMicaBackdrop()
        {
            if (MicaController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Hooking up the policy object
                m_configurationSource = new SystemBackdropConfiguration();
                Activated += Window_Activated;
                Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_micaController = new MicaController();

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_micaController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // succeeded
            }

            return false; // Mica is not supported on this system
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_micaController != null)
            {
                m_micaController.Dispose();
                m_micaController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = SystemBackdropTheme.Default; break;
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (Password.Text.Length < 1 ) return;
            DataPackage dataPackage = new();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(Password.Text);
            Clipboard.SetContent(dataPackage);
        }
    }

    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA
                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
    }

}