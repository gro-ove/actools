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
        public static bool BasicMode;

        private static string _customSource;
        private static Dictionary<string, Dictionary<string, string>> _customDirect;

        public static void SetCustomSource(string directory) {
            _customSource = directory;
        }

        public static void SetCustomSource(Dictionary<string, Dictionary<string, string>> direct) {
            _customDirect = direct;
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
                Logging.Warning(e);
                return null;
            }
        }

        public new string GetString(string name, CultureInfo culture) {
            if (BasicMode) {
                return base.GetString(name, CultureInfo.InvariantCulture);
            }

            try {
                if (_customDirect != null && _custom == null) {
                    _customDirect.TryGetValue(BaseName.Split('.').Last(), out _custom);
                    if (_custom == null && _customSource == null) {
                        _custom = new Dictionary<string, string>();
                    }
                }

                if (_customSource != null && _custom == null) {
                    var location = Path.Combine(_customSource, BaseName.Split('.').Last() + "." + CultureInfo.CurrentUICulture + ".resx");
                    _custom = LoadCustomResource(location) ?? new Dictionary<string, string>();
                }

                if (_custom != null) {
                    string result;
                    if (_custom.TryGetValue(name, out result)) return result;
                }

                return base.GetString(name, culture);
            } catch (Exception e) {
                Logging.Warning(e);
                BasicMode = true;
                return base.GetString(name, culture);
            }
        }
    }
}