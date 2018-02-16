using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Controls.ViewModels {
    public sealed class AiLimitationTyre : Displayable, IWithId {
        private bool _isAllowed = true;

        public bool IsAllowed {
            get => _isAllowed;
            set => Apply(value, ref _isAllowed);
        }

        public AiLimitationTyre(IniFileSection section) {
            DisplayName = section.GetTyreName();
        }

        public string Id => DisplayName;
    }
}