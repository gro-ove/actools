using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public interface IPaintablePersonalItem {
        // Does current item support various personalization features?
        bool IsNumberActive { get; }
        bool IsFlagActive { get; }

        [CanBeNull]
        IReadOnlyList<string> ActiveLabels { get; }

        // Set values for them
        int Number { set; }

        [CanBeNull]
        string FlagTexture { set; }

        [CanBeNull]
        IReadOnlyDictionary<string, string> Labels { set; }
    }
}