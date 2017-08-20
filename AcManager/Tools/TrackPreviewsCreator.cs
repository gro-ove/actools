using System;
using System.IO;
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
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using Microsoft.Win32;

namespace AcManager.Tools {
    public static class TrackPreviewsCreator {
        private const string KeyUpdatePreviewMessageShown = "SelectTrackPage.UpdatePreviewMessageShown";

        private static async Task ShotAndApply(string filename, bool isEnabled, Func<Task> run) {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                await ApplyExisting(filename);
                return;
            }

            if (!isEnabled) return;
            if (!ValuesStorage.GetBool(KeyUpdatePreviewMessageShown) && ModernDialog.ShowMessage(
                    ImportantTips.Entries.GetByIdOrDefault(@"trackPreviews")?.Content, AppStrings.Common_HowTo_Title, MessageBoxButton.OK) !=
                    MessageBoxResult.OK) {
                return;
            }

            var directory = FileUtils.GetDocumentsScreensDirectory();
            var shots = FileUtils.GetFilesSafe(directory);

            await run();
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

            var shot = new ImageViewer(newShots, details: x => Path.GetFileName(x as string)) {
                Model = {
                    MaxImageHeight = CommonAcConsts.TrackPreviewHeight,
                    MaxImageWidth = CommonAcConsts.TrackPreviewWidth
                }
            }.ShowDialogInSelectFileMode();
            if (shot == null) return;

            ApplyExistring(shot, filename);
        }

        public static Task ShotAndApply(TrackObjectBase track) {
            return ShotAndApply(track.PreviewImage, track.Enabled, () => QuickDrive.RunAsync(track: track));
        }

        public static Task ShotAndApply(TrackSkinObject track) {
            return ShotAndApply(track.PreviewImage, track.Enabled, () => QuickDrive.RunAsync(trackSkin: track));
        }

        private static void ApplyExistring(string source, string previewImage) {
            try {
                var cropped = ImageEditor.Proceed(source, new Size(CommonAcConsts.TrackPreviewWidth, CommonAcConsts.TrackPreviewHeight));
                using (var t = FileUtils.RecycleOriginal(previewImage)) {
                    cropped?.SaveAsPng(t.Filename);
                }
            } catch (Exception e) {
                NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, e);
            }
        }

        private static Task ApplyExisting(string previewImage) {
            var dialog = new OpenFileDialog {
                Filter = FileDialogFilters.ImagesFilter,
                Title = AppStrings.Common_SelectImageForPreview,
                InitialDirectory = FileUtils.GetDocumentsScreensDirectory(),
                RestoreDirectory = true
            };

            if (dialog.ShowDialog() == true) {
                ApplyExistring(dialog.FileName, previewImage);
            }

            return Task.Delay(0);
        }

        public static Task ApplyExisting(TrackObjectBase track) {
            return ApplyExisting(track.PreviewImage);
        }

        public static Task ApplyExisting(TrackSkinObject trackSkin) {
            return ApplyExisting(trackSkin.PreviewImage);
        }
    }
}
