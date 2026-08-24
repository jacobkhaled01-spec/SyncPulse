using System;
using System.Windows;

namespace SyncPulse.Server
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"خطأ غير متوقع في الخادم:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "SyncPulse Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    MessageBox.Show($"خطأ فادح في الخادم:\n{ex.Message}\n\n{ex.StackTrace}",
                        "SyncPulse Fatal Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            base.OnStartup(e);
        }
    }
}
