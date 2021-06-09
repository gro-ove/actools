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
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Controls.Helpers {
    public interface ICustomSharingUiHelper {
        bool ShowShared(string type, string link);
    }

    public class SharingUiHelper {
        public static SharingUiHelper Instance { get; private set; }

        [CanBeNull]
        private static ICustomSharingUiHelper _custom;

        public static void Initialize([CanBeNull] ICustomSharingUiHelper custom) {
            Debug.Assert(Instance == null);
            Instance = new SharingUiHelper();
            _custom = custom;
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
                if (!SettingsHolder.Sharing.ShareWithoutName && type != SharedEntryType.Results) {
                    contentName = Prompt.Show(ControlsStrings.Share_EnterName, ControlsStrings.Share_EnterNameHeader, defaultName, ToolsStrings.Common_None,
                            maxLength: 60);
                    if (contentName == null) return; // cancelled
                    if (string.IsNullOrWhiteSpace(contentName)) {
                        contentName = null;
                    }
                }

                string id = null;
                if (SettingsHolder.Sharing.CustomIds && type != SharedEntryType.Results) {
                    id = Prompt.Show(ControlsStrings.Share_EnterCustomId, ControlsStrings.Share_EnterCustomIdHeader, "",
                            ToolsStrings.Common_None, maxLength: 200)?.Trim();
                    if (id == null) return; // cancelled
                    if (string.IsNullOrWhiteSpace(id)) {
                        id = null;
                    }
                }

                var authorName = SettingsHolder.Sharing.ShareAnonymously ? null : SettingsHolder.Sharing.SharingName;
                if (SettingsHolder.Sharing.VerifyBeforeSharing && ModernDialog.ShowMessage(
                        string.Format(ControlsStrings.Share_VerifyMessage, authorName ?? @"?", contentName ?? @"?",
                                type.GetDescription()), ControlsStrings.Share_VerifyMessageHeader, MessageBoxButton.YesNo) != MessageBoxResult.Yes
                        && type != SharedEntryType.Results) {
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
                NonfatalError.Notify(string.Format(ControlsStrings.Share_CannotShare, type.GetDescription()),
                        ToolsStrings.Common_CannotDownloadFile_Commentary, e);
            } finally {
                _sharingInProcess = false;
            }
        }

        public static void ShowShared(SharedEntryType type, string link) {
            ShowShared(type.GetDescription(), link, true);
        }

        public static void ShowShared(string type, string link, bool showDialog) {
            if (_custom?.ShowShared(type, link) == true) return;

            if (SettingsHolder.Sharing.CopyLinkToClipboard) {
                try {
                    ClipboardHelper.SetText(link);
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            if (showDialog && SettingsHolder.Sharing.ShowSharedDialog) {
                var copied = false;
                try {
                    copied = SettingsHolder.Sharing.CopyLinkToClipboard && Clipboard.GetText() == link;
                } catch (Exception e) {
                    Logging.Warning(e);
                }

                var buttons = copied ? new MessageDialogButton(MessageBoxButton.OKCancel) {
                    [MessageBoxResult.OK] = "Open link in browser",
                    [MessageBoxResult.Cancel] = "Close"
                } : new MessageDialogButton(MessageBoxButton.YesNoCancel) {
                    [MessageBoxResult.Yes] = "Copy link to the clipboard",
                    [MessageBoxResult.No] = "Open link in browser",
                    [MessageBoxResult.Cancel] = "Close"
                };

                var response = MessageDialog.Show(copied
                        ? $"Link has been copied to the clipboard:[br]{link}"
                        : $"Here is the link:[br]{link}", type, buttons);

                if (response == MessageBoxResult.OK || response == MessageBoxResult.Yes) {
                    ClipboardHelper.SetText(link);
                }

                if (response == MessageBoxResult.No) {
                    WindowsHelper.ViewInBrowser(link + "#noauto");
                }
            } else {
                // Toast.Show(string.Format(ControlsStrings.Share_Shared, type.ToTitle()),
                Toast.Show(type,
                        SettingsHolder.Sharing.CopyLinkToClipboard && ClipboardHelper.GetText() == link
                                ? ControlsStrings.Share_SharedMessage : ControlsStrings.Share_SharedMessageAlternative,
                        () => WindowsHelper.ViewInBrowser(link + "#noauto"));
            }
        }
    }
}