using JetBrains.Annotations;

namespace AcTools.WheelAngles {
    public interface IWheelSteerLockOptionsStorage {
        void LoadValues([NotNull] WheelOptionsBase obj);
        void StoreValues([NotNull] WheelOptionsBase obj);
    }
}