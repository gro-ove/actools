using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject {
        private ICommand _viewInExplorerCommand;
        public virtual ICommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new RelayCommand(o => {
            ViewInExplorer();
        }));

        private RelayCommand _copyIdCommand;

        public RelayCommand CopyIdCommand => _copyIdCommand ?? (_copyIdCommand = new RelayCommand(o => {
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

        private ICommand _changeIdCommand;
        public virtual ICommand ChangeIdCommand => _changeIdCommand ?? (_changeIdCommand = new RelayCommand(o => {
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

        public virtual async Task CloneAsync(string id) {
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

        private ICommand _toggleCommand;
        public virtual ICommand ToggleCommand => _toggleCommand ?? (_toggleCommand = new RelayCommand(o => {
            try {
                Toggle();
            } catch (ToggleException ex) {
                NonfatalError.Notify(string.Format(ToolsStrings.AcObject_CannotToggleExt, ex.Message), ToolsStrings.AcObject_CannotToggle_Commentary);
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotToggle, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
            }
        }));

        private ICommand _deleteCommand;

        public virtual ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new RelayCommand(o => {
            try {
                Delete();
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotDelete, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
            }
        }));

        private ICommand _reloadCommand;
        public virtual ICommand ReloadCommand => _reloadCommand ?? (_reloadCommand = new RelayCommand(o => {
            if (o as string == @"full") {
                Manager.Reload(Id);
            } else {
                Reload();
            }
        }));

        private ICommand _saveCommand;
        public virtual ICommand SaveCommand => _saveCommand ?? (_saveCommand = new RelayCommand(o => {
            Save();
        }, o => Changed));
    }
}
