using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject {
        private ProperCommand _viewInExplorerCommand;

        public virtual ICommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new ProperCommand(o => {
            ViewInExplorer();
        }));

        private ProperCommand _copyIdCommand;

        public ICommand CopyIdCommand => _copyIdCommand ?? (_copyIdCommand = new ProperCommand(o => {
            switch (o as string) {
                case "name":
                    Clipboard.SetText(DisplayName);
                    break;

                case "path":
                    Clipboard.SetText(Location);
                    break;

                default:
                    Clipboard.SetText(Id);
                    break;
            }
        }));

        private ProperCommand _changeIdCommand;

        public virtual ICommand ChangeIdCommand => _changeIdCommand ?? (_changeIdCommand = new ProperCommand(o => {
            try {
                var newId = (o as string)?.Trim();
                if (string.IsNullOrWhiteSpace(newId)) return;
                Rename(newId);
            } catch (ToggleException ex) {
                NonfatalError.Notify(string.Format(ToolsStrings.AcObject_CannotChangeIdExt, ex.Message), ToolsStrings.AcObject_CannotToggle_Commentary);
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotChangeId, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
            }
        }, o => !string.IsNullOrWhiteSpace(o as string)));

        public async Task CloneAsync(string id) {
            try {
                id = id?.Trim();
                if (string.IsNullOrWhiteSpace(id)) return;

                await Task.Run(() => {
                    FileUtils.CopyRecursive(Location, FileUtils.EnsureUnique(Path.Combine(Path.GetDirectoryName(Location) ?? "", id)));
                });
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotClone, ToolsStrings.AcObject_CannotClone_Commentary, ex);
            }
        }

        private ICommand _cloneCommand;

        public ICommand CloneCommand => _cloneCommand ?? (_cloneCommand = new AsyncCommand(o => CloneAsync(o as string), o => !string.IsNullOrWhiteSpace(o as string)));

        private ProperCommand _toggleCommand;

        public virtual ICommand ToggleCommand => _toggleCommand ?? (_toggleCommand = new ProperCommand(o => {
            try {
                Toggle();
            } catch (ToggleException ex) {
                NonfatalError.Notify(string.Format(ToolsStrings.AcObject_CannotToggleExt, ex.Message), ToolsStrings.AcObject_CannotToggle_Commentary);
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotToggle, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
            }
        }));

        private ProperCommand _deleteCommand;

        public virtual ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new ProperCommand(o => {
            try {
                if (!SettingsHolder.Content.DeleteConfirmation ||
                        ModernDialog.ShowMessage(string.Format("Are you sure you want to move {0} to the Recycle Bin?", DisplayName), "Are You Sure?",
                                MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    Delete();
                }
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotDelete, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
            }
        }));

        private ProperCommand _reloadCommand;

        public virtual ICommand ReloadCommand => _reloadCommand ?? (_reloadCommand = new ProperCommand(o => {
            if (o as string == @"full") {
                Manager.Reload(Id);
            } else {
                Reload();
            }
        }));

        private ProperCommand _saveCommand;

        public virtual ICommand SaveCommand => _saveCommand ?? (_saveCommand = new ProperCommand(o => {
            Save();
        }, o => Changed));
    }
}
