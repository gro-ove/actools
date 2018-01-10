namespace AcTools.WheelAngles {
    public interface IWheelSteerLockSetter {
        string ControllerName { get; }

        bool Test(string productGuid);
        bool Apply(int angle, bool isReset, out int appliedValue);

        int MaximumSteerLock { get; }
        int MinimumSteerLock { get; }
    }
}