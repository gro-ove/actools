using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Objects {
    public static class ExtraModels {
        public static readonly string KeyCrewExtra = "Crew.Extra";

        private static readonly List<IExtraModelProvider> Providers = new List<IExtraModelProvider>(1);

        public static void Register(IExtraModelProvider provider) {
            Providers.Add(provider);
        }

        [ItemCanBeNull]
        public static async Task<byte[]> GetAsync(string key) {
            foreach (var provider in Providers) {
                var data = await provider.GetModel(key).ConfigureAwait(false);
                if (data != null) return data;
            }

            return null;
        }
    }
}