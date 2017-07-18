// #define WIN10_SHARE

using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Clipboard = System.Windows.Clipboard;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

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

        private CommandBase _removeCommand;

        public ICommand RemoveCommand => _removeCommand ?? (_removeCommand = new DelegateCommand<SharedEntry>(o => {
            // TODO
        }, o => o != null));

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
                    contentName = Prompt.Show(ControlsStrings.Share_EnterName, ControlsStrings.Share_EnterNameHeader, defaultName, ToolsStrings.Common_None, maxLength: 60);
                    if (contentName == null) return; // cancelled
                    if (string.IsNullOrWhiteSpace(contentName)) {
                        contentName = null;
                    }
                }

                string id = null;
                if (SettingsHolder.Sharing.CustomIds) {
                    id = Prompt.Show(ControlsStrings.Share_EnterCustomId, ControlsStrings.Share_EnterCustomIdHeader, "", ToolsStrings.Common_None, maxLength: 200)?.Trim();
                    if (id == null) return; // cancelled
                    if (string.IsNullOrWhiteSpace(id)) {
                        id = null;
                    }
                }

                var authorName = SettingsHolder.Sharing.ShareAnonymously ? null : SettingsHolder.Sharing.SharingName;
                if (SettingsHolder.Sharing.VerifyBeforeSharing && ModernDialog.ShowMessage(
                        string.Format(ControlsStrings.Share_VerifyMessage, authorName ?? @"?", contentName ?? @"?",
                                type.GetDescription()), ControlsStrings.Share_VerifyMessageHeader, MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    return;
                }

                string link;
                using (var waiting = new WaitingDialog()) {
                    waiting.Report(ControlsStrings.Share_InProgress);

                    link = await SharingHelper.ShareAsync(type, contentName, target, data, id, waiting.CancellationToken);
                    if (link == null) return;
                }

                ShowShared(type, link);
                await Task.Delay(2000);
            } catch (Exception e) {
                NonfatalError.Notify(string.Format(ControlsStrings.Share_CannotShare, type.GetDescription()), ToolsStrings.Common_CannotDownloadFile_Commentary, e);
            } finally {
                _sharingInProcess = false;
            }
        }

        public static void ShowShared(SharedEntryType type, string link) {
            ShowShared(type.GetDescription(), link);
        }

        public static void ShowShared(string type, string link) {
            if (SettingsHolder.Sharing.CopyLinkToClipboard) {
                Clipboard.SetText(link);
            }

            Toast.Show(string.Format(ControlsStrings.Share_Shared, type.ToTitle()),
                    SettingsHolder.Sharing.CopyLinkToClipboard ? ControlsStrings.Share_SharedMessage : ControlsStrings.Share_SharedMessageAlternative, () => {
                        Process.Start(link + "#noauto");
                    });

#if WIN10_SHARE
            try {
                var dataTransferManager = DataTransferManager.GetForCurrentView();
                dataTransferManager.DataRequested += OnDataRequested;
                DataTransferManager.ShowShareUI();
            } catch (Exception e) {
                Logging.Warning("DataTransferManager exception: " + e);
            }
#endif
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