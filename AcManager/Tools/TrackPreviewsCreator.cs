using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.About;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using Microsoft.Win32;

namespace AcManager.Tools {
    public static class TrackPreviewsCreator {
        private const string KeyUpdatePreviewMessageShown = "SelectTrackPage.UpdatePreviewMessageShown";

        public static async Task ShotAndApply(TrackObjectBase track) {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                await ApplyExisting(track);
                return;
            }

            if (!track.Enabled) return;
            if (!ValuesStorage.GetBool(KeyUpdatePreviewMessageShown) && ModernDialog.ShowMessage(
                    ImportantTips.Entries.GetByIdOrDefault(@"trackPreviews")?.Content, AppStrings.Common_HowTo_Title, MessageBoxButton.OK) !=
                    MessageBoxResult.OK) {
                return;
            }

            var directory = FileUtils.GetDocumentsScreensDirectory();
            var shots = FileUtils.GetFilesSafe(directory);

            await QuickDrive.RunAsync(track: track);
            if (ScreenshotsConverter.CurrentConversion?.IsCompleted == false) {
                await ScreenshotsConverter.CurrentConversion;
            }

            var newShots = FileUtils.GetFilesSafe(directory)
                                    .Where(x => !shots.Contains(x) && Regex.IsMatch(x, @"\.(jpe?g|png|bmp)$", RegexOptions.IgnoreCase)).ToList();
            if (!newShots.Any()) {
                NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, ControlsStrings.AcObject_CannotUpdatePreview_TrackCommentary);
                return;
            }

            ValuesStorage.Set(KeyUpdatePreviewMessageShown, true);

            var shot = new ImageViewer(newShots) {
                Model = {
                    MaxImageHeight = CommonAcConsts.TrackPreviewHeight,
                    MaxImageWidth = CommonAcConsts.TrackPreviewWidth
                }
            }.ShowDialogInSelectFileMode();
            if (shot == null) return;

            try {
                var cropped = ImageEditor.Proceed(shot, new Size(CommonAcConsts.TrackPreviewWidth, CommonAcConsts.TrackPreviewHeight));
                using (var t = FileUtils.RecycleOriginal(track.PreviewImage)) {
                    cropped?.SaveAsPng(t.Filename);
                }
            } catch (Exception e) {
                NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, e);
            }
        }

        public static Task ApplyExisting(TrackObjectBase track) {
            var dialog = new OpenFileDialog {
                Filter = FileDialogFilters.ImagesFilter,
                Title = AppStrings.Common_SelectImageForPreview,
                InitialDirectory = FileUtils.GetDocumentsScreensDirectory(),
                RestoreDirectory = true
            };

            if (dialog.ShowDialog() == true) {
                try {
                    var cropped = ImageEditor.Proceed(dialog.FileName, new Size(CommonAcConsts.TrackPreviewWidth, CommonAcConsts.TrackPreviewHeight));
                    using (var t = FileUtils.RecycleOriginal(track.PreviewImage)) {
                        cropped?.SaveAsPng(t.Filename);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, e);
                }
            }

            return Task.Delay(0);
        }
    }
}
