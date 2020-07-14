using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.DirectInput {
    public interface IDirectInputDevice {
        string InstanceId { get; }

        string ProductId { get; }

        bool IsVirtual { get; }

        bool IsController { get; }

        string DisplayName { get; }

        int Index { get; }

        IList<int> OriginalIniIds { get; }

        bool Same(IDirectInputDevice other);

        [CanBeNull]
        DirectInputAxle GetAxle(int id);

        [CanBeNull]
        DirectInputButton GetButton(int id);

        [CanBeNull]
        DirectInputPov GetPov(int id, DirectInputPovDirection direction);
    }

    public static class DirectInputDeviceExtension {
        public static bool IsSameAs(this IDirectInputDevice a, IDirectInputDevice b) {
            return a.InstanceId == b.InstanceId
                    || (a.InstanceId == null || b.InstanceId == null) && (a.ProductId == b.ProductId || a.DisplayName == b.DisplayName);
        }
    }
}