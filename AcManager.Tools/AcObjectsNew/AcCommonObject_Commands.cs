using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject {
        private ICommandExt _viewInExplorerCommand;

        public virtual ICommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new DelegateCommand(ViewInExplorer));

        private ICommandExt _copyIdCommand;

        public ICommand CopyIdCommand => _copyIdCommand ?? (_copyIdCommand = new DelegateCommand<string>(o => {
            switch (o) {
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

        private ICommandExt _changeIdCommand;

        public virtual ICommand ChangeIdCommand => _changeIdCommand ?? (_changeIdCommand = new DelegateCommand<string>(o => {
            try {
                var newId = o?.Trim();
                if (string.IsNullOrWhiteSpace(newId)) return;
                Rename(newId);
            } catch (ToggleException ex) {
                NonfatalError.Notify(string.Format(ToolsStrings.AcObject_CannotChangeIdExt, ex.Message), ToolsStrings.AcObject_CannotToggle_Commentary);
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotChangeId, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
            }
        }, o => !string.IsNullOrWhiteSpace(o)));

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

        public ICommand CloneCommand => _cloneCommand ?? (_cloneCommand = new AsyncCommand<string>(CloneAsync, o => !string.IsNullOrWhiteSpace(o)));

        private ICommandExt _toggleCommand;

        public virtual ICommand ToggleCommand => _toggleCommand ?? (_toggleCommand = new DelegateCommand(() => {
            try {
                Toggle();
            } catch (ToggleException ex) {
                NonfatalError.Notify(string.Format(ToolsStrings.AcObject_CannotToggleExt, ex.Message), ToolsStrings.AcObject_CannotToggle_Commentary);
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotToggle, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
            }
        }));

        private ICommandExt _deleteCommand;

        public virtual ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
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

        private ICommandExt _reloadCommand;

        public virtual ICommand ReloadCommand => _reloadCommand ?? (_reloadCommand = new DelegateCommand<string>(o => {
            if (o == @"full") {
                Manager.Reload(Id);
            } else {
                Reload();
            }
        }));

        private ICommandExt _saveCommand;

        public virtual ICommand SaveCommand => _saveCommand ?? (_saveCommand = new DelegateCommand(Save, () => Changed));
    }
}
