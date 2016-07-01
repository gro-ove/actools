// #define WIN10_SHARE

using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using Clipboard = System.Windows.Clipboard;

#if WIN10_SHARE
using Windows.ApplicationModel.DataTransfer;
#endif

namespace AcManager.Controls.Helpers {
    public class SharingUiHelper {
        public static SharingUiHelper Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new SharingUiHelper();
        }

        private RelayCommand _removeCommand;

        public RelayCommand RemoveCommand => _removeCommand ?? (_removeCommand = new RelayCommand(o => {
            var entry = o as SharedEntry;
            if (entry == null) return;

            // TODO
        }, o => o is SharedEntry));

        private static bool _sharingInProcess;

        public static Task ShareAsync(SharedEntryType type, string defaultName, string target, string data) {
            return ShareAsync(type, defaultName, target, Encoding.UTF8.GetBytes(data));
        }

        public static async Task ShareAsync(SharedEntryType type, string defaultName, string target, byte[] data) {
            if (_sharingInProcess) return;
            _sharingInProcess = true;

            try {
                var contentName = defaultName;
                if (!SettingsHolder.Sharing.ShareWithoutName) {
                    contentName = Prompt.Show("Enter name or keep field empty:", "Share As", defaultName, "None", maxLength: 60);
                    if (contentName == null) return; // cancelled
                    if (string.IsNullOrWhiteSpace(contentName)) {
                        contentName = null;
                    }
                }

                string id = null;
                if (SettingsHolder.Sharing.CustomIds) {
                    id = Prompt.Show("Enter custom ID:", "Custom ID", "", "None", maxLength: 200)?.Trim();
                    if (id == null) return; // cancelled
                    if (string.IsNullOrWhiteSpace(id)) {
                        id = null;
                    }
                }

                var authorName = SettingsHolder.Sharing.ShareAnonymously ? null : SettingsHolder.Sharing.SharingName;
                if (SettingsHolder.Sharing.VerifyBeforeSharing && ModernDialog.ShowMessage($"Author name: [b]{authorName ?? "?"}[/b]\n" +
                        $"Entry name: [b]{contentName ?? "?"}[/b]\n" +
                        $"Entry type: [b]{type.GetDescription()}[/b]", "Is Everything OK?", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    return;
                }

                string link;
                using (var waiting = new WaitingDialog()) {
                    waiting.Report("Sharing…");

                    link = await SharingHelper.ShareAsync(type, contentName, target, data, id, waiting.CancellationToken);
                    if (link == null) return;
                }

                ShowShared(type, link);
                await Task.Delay(2000);

#if WIN10_SHARE
                try {
                    var dataTransferManager = DataTransferManager.GetForCurrentView();
                    dataTransferManager.DataRequested += OnDataRequested;
                    DataTransferManager.ShowShareUI();
                } catch (Exception e) {
                    Logging.Warning("[SharingUiHelper] DataTransferManager exception: " + e);
                }
#endif
            } catch (Exception e) {
                NonfatalError.Notify($"Can’t share {type.GetDescription()}", "Make sure Internet connection is available.", e);
            } finally {
                _sharingInProcess = false;
            }
        }

        public static void ShowShared(SharedEntryType type, string link) {
            if (SettingsHolder.Sharing.CopyLinkToClipboard) {
                Clipboard.SetText(link);
            }

            Toast.Show(type.GetDescription().ToTitle() + " Shared", SettingsHolder.Sharing.CopyLinkToClipboard ? "Link copied to clipboard" : "Click to open link", () => {
                Process.Start(link + "#noauto");
            });
        }

#if WIN10_SHARE
        private static void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args) {
            args.Request.Data.Properties.Title = "Code Samples";
            args.Request.Data.Properties.Description = "Here are some great code samples for Windows Phone.";
            args.Request.Data.SetWebLink(new Uri("http://code.msdn.com/wpapps"));
        }       }
#endif
    }
}