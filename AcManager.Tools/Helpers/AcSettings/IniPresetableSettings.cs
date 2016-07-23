using System.ComponentModel;
using AcTools.DataFile;

namespace AcManager.Tools.Helpers.AcSettings {
    public abstract class IniPresetableSettings : IniSettings {
        protected IniPresetableSettings([Localizable(false)] string name, bool reload = true, bool systemConfig = false) : base(name, reload, systemConfig) { }

        public void Import(string serialized) {
            if (serialized == null) return;
            Replace(IniFile.Parse(serialized));
        }

        public string Export() {
            var ini = new IniFile();
            SetToIni(ini);
            return ini.Stringify();
        }

        protected override void SetToIni() {
            SetToIni(Ini);
        }

        protected abstract void SetToIni(IniFile ini);

        protected override void Save() {
            base.Save();

            if (!IsLoading) {
                InvokeChanged();
            }
        }

        protected abstract void InvokeChanged();
    }
}