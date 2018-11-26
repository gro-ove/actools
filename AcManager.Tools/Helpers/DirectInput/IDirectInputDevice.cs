using System.Collections.Generic;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.DirectInput {
    public interface IDirectInputDevice : IWithId {
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
}