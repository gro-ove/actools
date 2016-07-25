using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Pages.Dialogs {
    public class SelectCategory : Displayable {
        [JsonProperty(@"name")]
        public sealed override string DisplayName { get; set; }

        [JsonProperty(@"description")]
        public string Description { get; set; }

        [JsonProperty(@"filter")]
        public string Filter { get; set; }

        [JsonProperty(@"icon")]
        public string Icon { get; set; }

        public static IEnumerable<SelectCategory> LoadCategories(string type) {
            return FilesStorage.Instance.GetContentDirectoryFiltered(@"*.json", type).Select(x => x.Filename).SelectMany(x => {
                try {
                    return JsonConvert.DeserializeObject<SelectCategory[]>(File.ReadAllText(x));
                } catch (Exception e) {
                    Logging.Warning($"Cannot load file {Path.GetFileName(x)}: {e}");
                    return new SelectCategory[0];
                }
            }).Select(x => {
                x.Icon = FilesStorage.Instance.GetContentFile(type, x.Icon ?? x.DisplayName + @".png").Filename;
                return x;
            });
        }
    }
}