using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HololensAirplaneViewer
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // Capture text and close the view
            string address = AddressTextBox.Text;
            
            // TODO: Pass the address back to the main view
            
            // Close this view
            Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().CoreWindow.Close();
        }
    }
}
