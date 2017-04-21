using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.CustomShowroom;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools {
    public static class ToUpdatePreviewExtension {
        private static UpdatePreviewMode GetAutoUpdatePreviewsDialogMode() {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
                return UpdatePreviewMode.Options;
            }

            if (!SettingsHolder.CustomShowroom.CustomShowroomPreviews && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                return UpdatePreviewMode.StartManual;
            }

            return UpdatePreviewMode.Start;
        }

        [ItemCanBeNull]
        public static Task<IReadOnlyList<UpdatePreviewError>> Run(this ToUpdatePreview toUpdate, string presetFilename) {
            return new[] { toUpdate }.Run(null, presetFilename);
        }

        [ItemCanBeNull]
        public static Task<IReadOnlyList<UpdatePreviewError>> Run(this ToUpdatePreview toUpdate, UpdatePreviewMode? mode = null, string presetFilename = null) {
            return new[] { toUpdate }.Run(mode, presetFilename);
        }

        [ItemCanBeNull]
        public static Task<IReadOnlyList<UpdatePreviewError>> Run(this IEnumerable<ToUpdatePreview> toUpdate, string presetFilename) {
            return toUpdate.Run(null, presetFilename);
        }

        [ItemCanBeNull]
        public static Task<IReadOnlyList<UpdatePreviewError>> Run(this IEnumerable<ToUpdatePreview> toUpdate, UpdatePreviewMode? mode = null, string presetFilename = null) {
            var list = toUpdate.ToIReadOnlyListIfItIsNot();
            var actualMode = mode ?? GetAutoUpdatePreviewsDialogMode();

            if (SettingsHolder.CustomShowroom.CustomShowroomPreviews) {
                switch (actualMode) {
                    case UpdatePreviewMode.Options:
                        return CmPreviewsWrapper.Run(list, presetFilename);
                    case UpdatePreviewMode.StartManual:
                        NonfatalError.Notify("Can’t update previews in Manual Mode with Custom Showroom",
                                "Why would you need it anyway? Just open Custom Previews settings, locate camera the way you want and update one or all previews.");
                        return Task.FromResult<IReadOnlyList<UpdatePreviewError>>(null);
                    default:
                        return CmPreviewsTools.UpdatePreviewAsync(list, presetFilename);
                }
            }

            var dialog = new CarUpdatePreviewsDialog(list, actualMode, presetFilename);
            return Task.FromResult<IReadOnlyList<UpdatePreviewError>>(dialog.ShowDialog() ? dialog.Errors.ToList() : null);
        }

        /*public static Task Run(this ToUpdatePreview toUpdate, UpdatePreviewMode? mode = null) {
            return new[] { toUpdate }.Run(mode);
        }
        
        public static async Task Run(this IEnumerable<ToUpdatePreview> toUpdate, UpdatePreviewMode? mode = null) {
            var errors = await toUpdate.Try(mode);
            if (errors?.Count > 0) {
                NonfatalError.Notify("Can’t update previews", errors.Select(x => x.Message.ToSentence()).JoinToString(Environment.NewLine));
            }
        }*/
    }
}