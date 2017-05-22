using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.GameProperties {
    public class DriverName : Game.RaceIniProperties {
        private readonly string _driverName;
        private readonly string _nationality;

        public DriverName() {}

        public DriverName(string driverName, string nationality) {
            _driverName = driverName;
            _nationality = nationality;
        }

        public static string GetOnline() {
            var drive = SettingsHolder.Drive;
            return drive.DifferentPlayerNameOnline ? drive.PlayerNameOnline : drive.PlayerName;
        }

        public override void Set(IniFile file) {
            var settings = SettingsHolder.Drive;

            if (_driverName != null) {
                if (file["REMOTE"].GetBool("ACTIVE", false)) {
                    file["REMOTE"].Set("NAME", _driverName);
                    file["CAR_0"].Set("DRIVER_NAME", _driverName);
                    file["CAR_0"].Set("NATIONALITY", _nationality ?? settings.PlayerNationality);
                    file["CAR_0"].Set("NATION_CODE", NationCodeProvider.Instance.GetNationCode(_nationality ?? settings.PlayerNationality));
                } else {
                    file["CAR_0"].Set("DRIVER_NAME", _driverName);
                    file["CAR_0"].Set("NATIONALITY", _nationality ?? settings.PlayerNationality);
                    file["CAR_0"].Set("NATION_CODE", NationCodeProvider.Instance.GetNationCode(_nationality ?? settings.PlayerNationality));
                }
            }

            if (file["REMOTE"].GetBool("ACTIVE", false)) {
                var driverName = GetOnline();
                file["REMOTE"].Set("NAME", driverName);
                file["CAR_0"].Set("DRIVER_NAME", driverName);
                file["CAR_0"].Set("NATIONALITY", settings.PlayerNationality);
                file["CAR_0"].Set("NATION_CODE", NationCodeProvider.Instance.GetNationCode(settings.PlayerNationality));
            } else {
                var playerName = settings.PlayerName;
                if (SettingsHolder.Live.RsrEnabled && SettingsHolder.Live.RsrDifferentPlayerName &&
                        AcSettingsHolder.Forms.Entries.GetByIdOrDefault(RsrMark.FormId)?.IsVisible == true) {
                    playerName = SettingsHolder.Live.RsrPlayerName;
                }

                file["CAR_0"].Set("DRIVER_NAME", playerName);
                file["CAR_0"].Set("NATIONALITY", settings.PlayerNationality);
                file["CAR_0"].Set("NATION_CODE", NationCodeProvider.Instance.GetNationCode(settings.PlayerNationality));
            }
        }
    }
}