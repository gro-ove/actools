using System.Collections.Generic;
using System.ComponentModel;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public enum EntryLayer {
        Basic = 1,
        CtrlShortcut = 2,
        ShiftShortcut = 3,
        AltShortcut = 4,
        NoIntersection = 5,
        CustomModifier = 6
    }

    public interface IEntry : IWithId, INotifyPropertyChanged {
        string DisplayName { get; }

        bool IsWaiting { get; set; }

        EntryLayer Layer { get; }

        void Clear();

        void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices);

        void Save(IniFile ini);
    }
}