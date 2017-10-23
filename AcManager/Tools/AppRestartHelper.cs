/*using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;*/
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools {
    public class AppRestartHelper : FatalErrorMessage.IAppRestartHelper {
        /*public AppRestartHelper() {
            ShareTest();
        }*/

        void FatalErrorMessage.IAppRestartHelper.Restart() {
            WindowsHelper.RestartCurrentApplication();
        }

        /*private async void ShareTest() {
            await Task.Delay(1000);
            var dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += DataTransferManager_DataRequested;
            DataTransferManager.ShowShareUI();
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args) {
            args.Request.Data.SetWebLink(new Uri("http://radio.com/"));
            args.Request.Data.Properties.Title = "Radio";
            args.Request.Data.Properties.Description = "I am listening to Radio.";
            args.Request.Data.SetText("I am listening to Radio.");
        }*/
    }
}