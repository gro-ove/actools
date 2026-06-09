using System.Collections.Generic;
using System.IO;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class WeatherContentEntry : ContentEntryBase<WeatherObject> {
        public override double Priority => 25d;

        public WeatherContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(true, path, id, null, name, iconData: iconData) { }

        public override string GenericModTypeName => "Weather";
        public override string NewFormat => ToolsStrings.ContentInstallation_WeatherNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_WeatherExisting;

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool PreviewFilter(string x) {
                return x != @"preview.jpg";
            }

            IEnumerable<string> RemoveClouds(string location) {
                yield return Path.Combine(location, "clouds");
            }

            return new[] {
                new UpdateOption(ToolsStrings.Installator_UpdateEverything, false),
                new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst, true),
                new UpdateOption("Update over existing files, remove existing clouds if any", false){ CleanUp = RemoveClouds },
                new UpdateOption("Update over existing files, keep preview", false){ Filter = PreviewFilter },
                new UpdateOption("Update over existing files, remove existing clouds if any & keep preview", false){ Filter = PreviewFilter, CleanUp = RemoveClouds },
            };
        }

        protected override UpdateOption GetDefaultUpdateOption(UpdateOption[] list) {
            return list.ArrayElementAtOrDefault(2) ?? base.GetDefaultUpdateOption(list);
        }

        public override FileAcManager<WeatherObject> GetManager() {
            return WeatherManager.Instance;
        }
    }
}