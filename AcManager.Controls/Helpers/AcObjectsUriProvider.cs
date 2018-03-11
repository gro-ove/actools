using System;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Controls.Helpers {
    public static class AcObjectsUriManager {
        private static IAcObjectsUriProvider _provider;

        public static void Register(IAcObjectsUriProvider provider) {
            if (_provider != null) {
                throw new Exception(@"Provider already assigned");
            }

            _provider = provider;
        }

        public static Uri GetUri(AcObjectNew obj) {
            if (_provider == null) {
                throw new Exception(@"Provider is missing");
            }

            return _provider.GetUri(obj);
        }
    }
}
