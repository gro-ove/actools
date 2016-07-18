using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Xml.Linq;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI {
    [Localizable(false)]
    public class CustomResourceManager : ResourceManager {
        private static string _customSource;

        public static void SetCustomSource(string directory) {
            _customSource = directory;
        }

        public CustomResourceManager(string baseName, Assembly assembly) : base(baseName, assembly) {}

        private Dictionary<string, string> _custom;

        private static Dictionary<string, string> LoadCustomResource(string filename) {
            try {
                return !File.Exists(filename) ? null :
                        XDocument.Parse(File.ReadAllText(filename)).Element("root")?.Elements("data").Select(x => new {
                            Key = x.Attribute("name")?.Value,
                            x.Element("value")?.Value
                        }).Where(x => x.Key != null && x.Value != null).ToDictionary(x => x.Key, x => x.Value);
            } catch (Exception e) {
                Logging.Warning("[CustomResourceManager] GetString(): " + e);
                return null;
            }
        } 

        public new string GetString(string name, CultureInfo culture) {
            if (_customSource != null) {
                if (_custom == null) {
                    var location = Path.Combine(_customSource, BaseName.Replace(".Resources", "") + ".resx");
                    _custom = LoadCustomResource(location) ?? new Dictionary<string, string>();
                }

                string result;
                if (_custom.TryGetValue(name, out result)) return result;
            }

            return base.GetString(name, culture);
        }
    }
}