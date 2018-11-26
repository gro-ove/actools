using System;

namespace AcManager.Tools.Helpers.DirectInput {
    public enum DirectInputPovDirection {
        Left = 0, Up = 1, Right = 2, Down = 3
    }

    public static class DirectInputPovDirectionExtension {
        public static bool IsInRange(this DirectInputPovDirection direction, int value) {
            switch (direction) {
                case DirectInputPovDirection.Left:
                    return value > 22500 && value <= 31500;
                case DirectInputPovDirection.Up:
                    return value >= 0 && value <= 4500 || value > 31500;
                case DirectInputPovDirection.Right:
                    return value > 4500 && value <= 13500;
                case DirectInputPovDirection.Down:
                    return value > 13500 && value <= 22500;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }
}