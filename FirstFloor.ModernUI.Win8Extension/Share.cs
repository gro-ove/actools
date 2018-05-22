#if WIN8SUPPORTED

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.ApplicationModel.DataTransfer;

namespace FirstFloor.ModernUI.Win8Extension {
    public static class Share {
        public static bool TryToShow(string title, string link) {
            try {
                return Show(title, link);
            } catch {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Show(string title, string link) {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return false;

            _title = title;
            _link = link;
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(() => {
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

        private static string _title;
        private static string _link;

        private static void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args) {
            args.Request.Data.SetWebLink(new Uri(_link));
            args.Request.Data.Properties.Title = _title;
            args.Request.Data.Properties.Description = "This item will be shared.";
        }
    }
}

#else

namespace FirstFloor.ModernUI.Win8Extension {
    public static class Share {
        public static bool TryToShow(string title, string link) {
            return false;
        }
    }
}

#endif