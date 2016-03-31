using System;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject {
        private ICommand _viewInExplorerCommand;
        public ICommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new RelayCommand(o => {
            ViewInExplorer();
        }));

        private ICommand _toggleCommand;
        public ICommand ToggleCommand => _toggleCommand ?? (_toggleCommand = new RelayCommand(o => {
            try {
                Toggle();
            } catch (ToggleException ex) {
                NonfatalError.Notify(@"Can't toggle: " + ex.Message, @"Make sure there is no runned app working with object's folder.");
            } catch (Exception ex) {
                NonfatalError.Notify(@"Can't toggle", @"Make sure there is no runned app working with object's folder.", ex);
            }
        }));

        private ICommand _deleteCommand;

        public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new RelayCommand(o => {
            try {
                Delete();
            } catch (Exception ex) {
                NonfatalError.Notify(@"Can't delete", @"Make sure there is no runned app working with object's folder.", ex);
            }
        }));

        private ICommand _reloadCommand;
        public ICommand ReloadCommand => _reloadCommand ?? (_reloadCommand = new RelayCommand(o => {
            Reload();
        }));

        private ICommand _saveCommand;
        public ICommand SaveCommand => _saveCommand ?? (_saveCommand = new RelayCommand(o => {
            Save();
        }, o => Changed));
    }
}
