using System;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools {
    public static partial class ArgumentsHandler {
        private enum Choise {
            ApplyAndSave,
            Apply,
            Save,
            Extra,
            Cancel
        }

        /// <summary>
        /// Shows dialog with all information about shared entry and offers a choise to user what to do with it.
        /// </summary>
        /// <param name="shared">Shared entry.</param>
        /// <param name="additionalButton">Label of additional button.</param>
        /// <param name="saveable">Can be saved.</param>
        /// <param name="applyable">Can be applied.</param>
        /// <param name="appliableWithoutSaving">Can be applied without saving.</param>
        /// <returns>User choise.</returns>
        private static Choise ShowDialog(SharedEntry shared, string additionalButton = null, bool saveable = true, bool applyable = true,
                bool appliableWithoutSaving = true) {
            var description = string.Format(AppStrings.Arguments_SharedMessage, shared.Name ?? AppStrings.Arguments_SharedMessage_EmptyValue,
                    shared.EntryType == SharedEntryType.Weather
                            ? AppStrings.Arguments_SharedMessage_Id : AppStrings.Arguments_SharedMessage_For,
                    shared.Target ?? AppStrings.Arguments_SharedMessage_EmptyValue,
                    shared.Author ?? AppStrings.Arguments_SharedMessage_EmptyValue);

            var dlg = new ModernDialog {
                Title = shared.EntryType.GetDescription().ToTitle(),
                Content = new ScrollViewer {
                    Content = new BbCodeBlock {
                        Text = description + '\n' + '\n' + (
                                saveable ? AppStrings.Arguments_Shared_ShouldApplyOrSave : AppStrings.Arguments_Shared_ShouldApply),
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                MinHeight = 0,
                MinWidth = 0,
                MaxHeight = 480,
                MaxWidth = 640
            };

            dlg.Buttons = new[] {
                applyable && saveable ? dlg.CreateCloseDialogButton(
                        appliableWithoutSaving ? AppStrings.Arguments_Shared_ApplyAndSave : AppStrings.Arguments_Shared_SaveAndApply,
                        true, false, MessageBoxResult.Yes) : null,
                appliableWithoutSaving && applyable
                        ? dlg.CreateCloseDialogButton(saveable ? AppStrings.Arguments_Shared_ApplyOnly : AppStrings.Arguments_Shared_Apply,
                                true, false, MessageBoxResult.OK) : null,
                saveable ? dlg.CreateCloseDialogButton(
                        applyable && appliableWithoutSaving ? AppStrings.Arguments_Shared_SaveOnly : AppStrings.Toolbar_Save,
                        true, false, MessageBoxResult.No) : null,
                additionalButton == null ? null : dlg.CreateCloseDialogButton(additionalButton, true, false, MessageBoxResult.None),
                dlg.CancelButton
            }.NonNull();
            dlg.ShowDialog();

            switch (dlg.MessageBoxResult) {
                case MessageBoxResult.None:
                    return Choise.Extra;
                case MessageBoxResult.OK:
                    return Choise.Apply;
                case MessageBoxResult.Cancel:
                    return Choise.Cancel;
                case MessageBoxResult.Yes:
                    return Choise.ApplyAndSave;
                case MessageBoxResult.No:
                    return Choise.Save;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}