using System.ComponentModel;
using System.Windows;
using SyncPulse.Client.ViewModels;

namespace SyncPulse.Client.Views
{
    public partial class CallWindow : Window
    {
        private bool _isClosingHandled;

        public CallWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isClosingHandled)
            {
                base.OnClosing(e);
                return;
            }

            _isClosingHandled = true;
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
