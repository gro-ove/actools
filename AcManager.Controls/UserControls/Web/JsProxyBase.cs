using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Threading;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
    public class JsProxyBase {
        private JsBridgeBase _bridge;

        public JsProxyBase(JsBridgeBase bridge) {
            _bridge = bridge;
        }

        [CanBeNull]
        protected WebTab Tab() {
            return _bridge.Tab;
        }

        protected void Sync(Action action) {
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(() => {
                try {
                    action();
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            });
        }

        protected T Sync<T>(Func<T> action) {
            return (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(() => {
                try {
                    return action();
                } catch (Exception e) {
                    Logging.Warning(e);
                    return default;
                }
            });
        }

        protected static string GetAcChecksum(string objectLocation, string relativePath) {
            if (objectLocation == null || !File.Exists(Path.Combine(objectLocation, relativePath))) return null;
            using (var md5 = MD5.Create()) {
                return md5.ComputeHash(File.ReadAllBytes(Path.Combine(objectLocation, relativePath))).ToHexString().ToLowerInvariant();
            }
        }
    }
}