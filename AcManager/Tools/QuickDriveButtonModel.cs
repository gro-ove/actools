using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Pages.Drive;
using AcManager.Tools.Managers.Presets;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools {
    public class QuickDriveButtonModel : NotifyPropertyChanged, IDisposable {
        public delegate Task RunCallback(bool setupRace, [CanBeNull] string presetFilename);

        private readonly RunCallback _run;

        public QuickDriveButtonModel(RunCallback run) {
            _run = run;
        }

        // Call when dropdown button is first clicked
        public void Initialize() {
            if (Presets == null) {
                Presets = _helper.Create(new PresetsCategory(QuickDrive.PresetableKeyValue), OnPresetSelected);
            }
        }

        private void OnPresetSelected(ISavedPresetEntry p) {
            _run(false, p.VirtualFilename).Ignore();
        }

        public HierarchicalItemsView Presets {
            get => _presets;
            set => Apply(value, ref _presets);
        }

        private HierarchicalItemsView _presets;
        private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

        private AsyncCommand _driveCommand;

        public AsyncCommand DriveCommand => _driveCommand ?? (_driveCommand = new AsyncCommand(() => _run(false, null)));

        private DelegateCommand _optionsCommand;

        public DelegateCommand OptionsCommand => _optionsCommand ?? (_optionsCommand = new DelegateCommand(() => _run(true, null)));

        public InputBinding[] GetInputBindingCommands() {
            return new[] {
                new InputBinding(DriveCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(OptionsCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)),
            };
        }

        public void Dispose() {
            _presets?.Dispose();
            _helper?.Dispose();
        }
    }
}