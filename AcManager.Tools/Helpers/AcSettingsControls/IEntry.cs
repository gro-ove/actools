using System.Collections.Generic;
using System.ComponentModel;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public interface IEntry : IWithId, INotifyPropertyChanged {
        string DisplayName { get; }

        bool Waiting { get; set; }

        void Clear();

        void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices);

        void Save(IniFile ini);
    }
}