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
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
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

        public static Task ShareAsync(SharedEntryType type, [CanBeNull] string defaultName, [CanBeNull] string target, [NotNull] string data) {
            return ShareAsync(type, defaultName, target, Encoding.UTF8.GetBytes(data));
        }

        public static async Task ShareAsync(SharedEntryType type, [CanBeNull] string defaultName, [CanBeNull] string target, [NotNull] byte[] data) {
            if (_sharingInProcess) return;
            _sharingInProcess = true;

            try {
                var contentName = defaultName;
                if (!SettingsHolder.Sharing.ShareWithoutName) {
                    contentName = Prompt.Show(Resources.Share_EnterName, Resources.Share_EnterNameHeader, defaultName, Tools.Resources.Common_None, maxLength: 60);
                    if (contentName == null) return; // cancelled
                    if (string.IsNullOrWhiteSpace(contentName)) {
                        contentName = null;
                    }
                }

                string id = null;
                if (SettingsHolder.Sharing.CustomIds) {
                    id = Prompt.Show(Resources.Share_EnterCustomId, Resources.Share_EnterCustomIdHeader, "", Tools.Resources.Common_None, maxLength: 200)?.Trim();
                    if (id == null) return; // cancelled
                    if (string.IsNullOrWhiteSpace(id)) {
                        id = null;
                    }
                }

                var authorName = SettingsHolder.Sharing.ShareAnonymously ? null : SettingsHolder.Sharing.SharingName;
                if (SettingsHolder.Sharing.VerifyBeforeSharing && ModernDialog.ShowMessage(
                        string.Format(Resources.Share_VerifyMessage, authorName ?? @"?", contentName ?? @"?",
                                type.GetDescription()), Resources.Share_VerifyMessageHeader, MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    return;
                }

                string link;
                using (var waiting = new WaitingDialog()) {
                    waiting.Report(Resources.Share_InProgress);

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
                NonfatalError.Notify(string.Format(Resources.Share_CannotShare, type.GetDescription()), Resources.Share_CannotShare_Commentary, e);
            } finally {
                _sharingInProcess = false;
            }
        }

        public static void ShowShared(SharedEntryType type, string link) {
            if (SettingsHolder.Sharing.CopyLinkToClipboard) {
                Clipboard.SetText(link);
            }

            Toast.Show(string.Format(Resources.Share_Shared, type.GetDescription().ToTitle()),
                    SettingsHolder.Sharing.CopyLinkToClipboard ? Resources.Share_SharedMessage : Resources.Share_SharedMessageAlternative, () => {
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