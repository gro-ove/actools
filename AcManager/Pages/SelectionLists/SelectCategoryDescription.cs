using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Pages.SelectionLists {
    public class SelectCategoryDescription {
        [CanBeNull]
        public string Name { get; }

        [CanBeNull]
        public string Description { get; }

        [NotNull]
        public string Filter { get; }

        public double Order { get; }

        [CanBeNull]
        public FrameworkElement Icon { get; }

        [CanBeNull]
        public string Source { get; }

        [JsonConstructor]
        private SelectCategoryDescription([NotNull] string name, string description, string filter, string icon, double order) {
            Name = ContentUtils.Translate(name);
            Description = ContentUtils.Translate(description);
            Filter = filter ?? @"*";
            Order = order;
            Icon = ContentUtils.GetIcon(icon ?? name + @".png", _type).Get();
            Source = _source;
        }

        private static string _source;
        private static string _type;

        public static IEnumerable<SelectCategoryDescription> LoadCategories(string type) {
            _type = type;
            return FilesStorage.Instance.GetContentFilesFiltered(@"*.json", type).Select(x => x.Filename).SelectMany(x => {
                try {
                    _source = Path.GetFileNameWithoutExtension(x);
                    return JsonConvert.DeserializeObject<SelectCategoryDescription[]>(File.ReadAllText(x));
                } catch (Exception e) {
                    Logging.Warning($"Cannot load file {Path.GetFileName(x)}: {e}");
                    return new SelectCategoryDescription[0];
                }
            });
        }
    }
}