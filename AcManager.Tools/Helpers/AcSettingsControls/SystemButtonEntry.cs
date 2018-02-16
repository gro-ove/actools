using System.Collections.Generic;
using System.Windows.Forms;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using FirstFloor.ModernUI.Commands;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class SystemButtonEntry : KeyboardButtonEntry {
        private readonly Keys? _defaultKey;

        public SystemButtonEntry([LocalizationRequired(false)] string id, string name, Keys? defaultKey) : base(id, name) {
            _defaultKey = defaultKey;
        }

        public override EntryLayer Layer => EntryLayer.CtrlShortcut;

        private string _displayInvertCombination;

        public string DisplayInvertCombination {
            get => _displayInvertCombination;
            set => Apply(value, ref _displayInvertCombination);
        }

        private DelegateCommand _resetCommand;

        public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
            if (_defaultKey.HasValue) {
                Input = AcSettingsHolder.Controls.GetKeyboardInputButton((int)_defaultKey.Value);
            }
        }, () => _defaultKey.HasValue));

        protected override void OnInputChanged(KeyboardInputButton oldValue, KeyboardInputButton newValue) {
            base.OnInputChanged(oldValue, newValue);
            DisplayInvertCombination = $"Ctrl+Shift+{newValue?.DisplayName}";
        }

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini[Id];
            Input = AcSettingsHolder.Controls.GetKeyboardInputButton(section.GetInt("KEY", _defaultKey.HasValue ? (int)_defaultKey.Value : -1));
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            var input = Input;
            section.SetCommentary("KEY", input?.DisplayName);
            section.Set("KEY", input == null || !CheckValue(input.Id) ? @"-1" : @"0x" + input.Id.ToString(@"X"));
        }
    }
}