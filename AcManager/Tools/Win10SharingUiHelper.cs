using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Interop;
using Windows.ApplicationModel.DataTransfer;
using AcManager.Controls.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools {
    // Feels like it doesn’t really work, Windows 10 just isn’t there yet. Also, this way sharing
    // might be cancelled later, and if user will try again, it would be nice not to ask him details.
    public class Win10SharingUiHelper : ICustomSharingUiHelper {
        public bool ShowShared(string type, string link) {
            try {
                return ShowSharedInner(type, link);
            } catch {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShowSharedInner(string type, string link) {
            var test = typeof(DataTransferManager);
            Logging.Debug(test.FullName);

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return false;

            _type = type;
            _link = link;
            ActionExtension.InvokeInMainThread(() => {
                var handle = new WindowInteropHelper(mainWindow).Handle;
                var dataTransferManager = DataTransferManagerHelper.GetForWindow(handle);
                dataTransferManager.DataRequested -= OnDataRequested;
                dataTransferManager.DataRequested += OnDataRequested;
                DataTransferManagerHelper.ShowShareUIForWindow(handle);
            });

            return true;
        }

        private static class DataTransferManagerHelper {
            private static readonly Guid Guid = new Guid("a5caee9b-8708-49d1-8d36-67d25a8da00c");

            // ReSharper disable once SuspiciousTypeConversion.Global
            private static IDataTransferManagerInterop DataTransferManagerInterop =>
                    (IDataTransferManagerInterop)WindowsRuntimeMarshal.GetActivationFactory(typeof(DataTransferManager));

            public static DataTransferManager GetForWindow(IntPtr hwnd) {
                return DataTransferManagerInterop.GetForWindow(hwnd, Guid);
            }

            public static void ShowShareUIForWindow(IntPtr hwnd) {
                DataTransferManagerInterop.ShowShareUIForWindow(hwnd);
            }

            [ComImport, Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IDataTransferManagerInterop {
                DataTransferManager GetForWindow([In] IntPtr appWindow, [In] ref Guid riid);
                void ShowShareUIForWindow(IntPtr appWindow);
            }
        }

        private static string _type;
        private static string _link;

        private static void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args) {
            args.Request.Data.SetWebLink(new Uri(_link));
            args.Request.Data.Properties.Title = _type.ToTitle();
            args.Request.Data.Properties.Description = "This item will be shared.";
        }
    }
}