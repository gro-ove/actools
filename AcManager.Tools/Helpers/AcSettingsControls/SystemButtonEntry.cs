using System.Collections.Generic;
using System.ComponentModel;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class SystemButtonEntry : KeyboardButtonEntry {
        public SystemButtonEntry([LocalizationRequired(false)] string id, string name) : base(id, name) {}

        public override EntryLayer Layer => EntryLayer.CtrlShortcut;

        private string _displayInvertCombination;

        public string DisplayInvertCombination {
            get => _displayInvertCombination;
            set {
                if (Equals(value, _displayInvertCombination)) return;
                _displayInvertCombination = value;
                OnPropertyChanged();
            }
        }

        protected override void OnInputChanged(KeyboardInputButton oldValue, KeyboardInputButton newValue) {
            base.OnInputChanged(oldValue, newValue);
            DisplayInvertCombination = $"Ctrl+Shift+{newValue?.DisplayName}";
        }

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini[Id];
            Input = AcSettingsHolder.Controls.GetKeyboardInputButton(section.GetInt("KEY", -1));
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            var input = Input;
            section.SetCommentary("KEY", input?.DisplayName);
            section.Set("KEY", input == null || !CheckValue(input.Id) ? @"-1" : @"0x" + input.Id.ToString(@"X"));
        }
    }

    public class SystemButtonEntryCombined : NotifyPropertyChanged {
        public bool ShiftToInvert { get; }
        public bool CustomCommand { get; }

        public SystemButtonEntryCombined([LocalizationRequired(false)] string id, string displayName,
                bool shiftToInvert = false, bool customCommand = false) {
            WheelButton = new WheelButtonEntry(id, displayName);
            SystemButton = new SystemButtonEntry(id, displayName);
            ShiftToInvert = shiftToInvert;
            CustomCommand = customCommand;
            SystemButton.PropertyChanged += OnSystemButtonPropertyChanged;
        }

        private void OnSystemButtonPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(SystemButton.Input)) {
                OnPropertyChanged(nameof(IsWheelButtonAllowed));
            }
        }

        public WheelButtonEntry WheelButton { get; }
        public SystemButtonEntry SystemButton { get; }

        public bool IsWheelButtonAllowed => CustomCommand || SystemButton.Input != null;
    }
}