using System;
using System.Collections.Generic;
using System.ComponentModel;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    [Flags]
    public enum EntryLayer {
        Basic = 1,
        CtrlShortcut = 2,
        ShiftShortcut = 4,
        AltShortcut = 8,
        NoIntersection = 16,
        CustomModifier = 32,
        ButtonModifier = 64
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