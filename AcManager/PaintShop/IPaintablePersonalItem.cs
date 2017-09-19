using System.Collections.Generic;

namespace AcManager.PaintShop {
    public interface IPaintablePersonalItem {
        // Does current item support various personalization features?
        bool IsNumberActive { get; }
        bool IsFlagActive { get; }
        IReadOnlyList<string> ActiveLabels { get; }

        // Set values for them
        int Number { set; }
        string FlagTexture { set; }
        void SetLabel(string role, string value);
    }
}