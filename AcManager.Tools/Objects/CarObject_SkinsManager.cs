using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;

namespace AcManager.Tools.Objects {
    public class CarObject_SkinsManager : FileAcManager<CarSkinObject> {
        public readonly string CarId;

        internal CarObject_SkinsManager(string carId, AcObjectTypeDirectories directories) {
            CarId = carId;
            Directories = directories;
        }

        public override AcObjectTypeDirectories Directories { get; }

        protected override CarSkinObject CreateAcObject(string id, bool enabled) {
            return new CarSkinObject(CarId, this, id, enabled);
        }
    }
}