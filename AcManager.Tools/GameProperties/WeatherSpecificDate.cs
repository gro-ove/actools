using System;
using AcTools.DataFile;
using AcTools.Processes;

namespace AcManager.Tools.GameProperties {
    public class WeatherSpecificDate : Game.RaceIniProperties {
        public bool UseSpecificDate { get; }
        public DateTime Date { get; }

        public WeatherSpecificDate(bool useSpecificDate, DateTime specificDateValue) {
            UseSpecificDate = useSpecificDate;
            Date = specificDateValue;
        }

        public override void Set(IniFile file) {
            if (UseSpecificDate) {
                file["LIGHTING"].Set("__CM_DATE", (Date - new DateTime(1970, 1, 1)).TotalSeconds);
            } else {
                file["LIGHTING"].Remove("__CM_DATE");
            }
        }
    }
}