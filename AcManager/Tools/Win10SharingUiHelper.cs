using System.Runtime.CompilerServices;
using AcManager.Controls.Helpers;

#if WIN8SUPPORTED
using FirstFloor.ModernUI.Win8Extension;
#endif

namespace AcManager.Tools {
    // Feels like it doesn’t really work, Windows 10 just isn’t there yet. Also, this way sharing
    // might be cancelled later, and if user will try again, it would be nice not to ask him details.
    public class Win10SharingUiHelper : ICustomSharingUiHelper {
        public bool ShowShared(string type, string link) {
            try {
                return TryToShow(type, link);
            } catch {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        // ReSharper disable UnusedParameter.Local
        private static bool TryToShow(string type, string link) {
            // ReSharper restore UnusedParameter.Local
#if WIN8SUPPORTED
            return Share.TryToShow(type, link);
#else
            return false;
#endif
        }

    }
}