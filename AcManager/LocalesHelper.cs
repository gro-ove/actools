using System.Globalization;
using System.Linq;

namespace AcManager {
    public static class LocalesHelper {
        public static readonly string[] SupportedLocales = { @"en-US" };

        /// <summary>
        /// Careful! Called before used libraries are plugged!
        /// </summary>
        public static void Initialize() {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            var forceLocale = AppArguments.Get(AppFlag.ForceLocale);
            if (forceLocale != null) {
                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(forceLocale);
            } else if (!SupportedLocales.Contains(CultureInfo.CurrentUICulture.Name)) {
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            }
        }
    }
}