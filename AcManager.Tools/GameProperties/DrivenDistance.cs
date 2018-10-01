using AcTools.DataFile;
using AcTools.Processes;

namespace AcManager.Tools.GameProperties {
    public class DrivenDistance : Game.RaceIniProperties {
        public double DrivenDistanceValue { get; }

        public DrivenDistance(double drivenDistanceValue) {
            DrivenDistanceValue = drivenDistanceValue;
        }

        public override void Set(IniFile file) {
            if (DrivenDistanceValue > 0d) {
                file["CAR_0"].Set("__CM_DRIVEN_DISTANCE", DrivenDistanceValue);
            }
        }
    }
}