using AcTools.DataFile;
using AcTools.Utils;
#pragma warning disable 649

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private LazierThis<double?> _steerLock;
        public double? SteerLock => _steerLock.Get(() => AcdData?.GetIniFile("car.ini")["CONTROLS"].GetDoubleNullable("STEER_LOCK") * 2d);
    }
}
