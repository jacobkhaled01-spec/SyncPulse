using System.ComponentModel;
using System.Windows;
using SyncPulse.Client.ViewModels;

namespace SyncPulse.Client.Views
{
    public partial class CallWindow : Window
    {
        public CallWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (DataContext is CallViewModel vm)
            {
                if (vm.EndCallCommand.CanExecute(null))
                {
                    vm.EndCallCommand.Execute(null);
                }
            }
        }
    }
}
