using System;
using System.ComponentModel;
using System.Windows;
using SyncPulse.Server.ViewModels;

namespace SyncPulse.Server
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (DataContext is ServerMainViewModel vm)
            {
                if (vm.StopCommand.CanExecute(null))
                {
                    vm.StopCommand.Execute(null);
                }
            }
            Environment.Exit(0);
        }
    }
}