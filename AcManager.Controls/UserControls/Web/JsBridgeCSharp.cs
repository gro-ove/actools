using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Threading;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
    public abstract class JsBridgeBase {
        // Do not include “www.” here!
        internal Collection<string> AcApiHosts { get; } = new Collection<string>();

        internal bool IsHostAllowed(string url) {
            return AcApiHosts.Contains(url.GetDomainNameFromUrl(), StringComparer.OrdinalIgnoreCase);
        }

        internal WebTab Tab { get; set; }

        internal virtual string AcApiRequest(string url) {
            return null;
        }

        internal virtual void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {}

        internal virtual void PageHeaders(string url, IDictionary<string, string> headers) {}

        internal virtual void PageLoaded(string url) {}

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

    [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
    public abstract class JsBridgeCSharp : JsBridgeBase {
        [UsedImplicitly]
        public void Log(string message) {
            Logging.Write("" + message);
        }

        [UsedImplicitly]
        public void OnError(string error, string url, int line, int column) {
            Sync(() => {
                Logging.Warning($"[{url}:{line}:{column}] {error}");
                Tab?.OnError(error, url, line, column);
            });
        }

        [UsedImplicitly]
        public void Alert(string message) {
            Sync(() => ModernDialog.ShowMessage(message));
        }

        [UsedImplicitly]
        public string Prompt(string message, string defaultValue) {
            return Sync(() => FirstFloor.ModernUI.Dialogs.Prompt.Show(message, ControlsStrings.WebBrowser_Prompt, defaultValue));
        }

        [UsedImplicitly]
        public object CmTest() {
            return true;
        }
    }
}