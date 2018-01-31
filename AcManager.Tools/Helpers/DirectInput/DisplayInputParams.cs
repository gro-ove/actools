using System;
using System.IO;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.DirectInput {
    public class DisplayInputParams {
        private readonly int _take;
        private readonly Func<int, bool> _test;
        private readonly Func<int, string> _name;

        public bool Test(int v) {
            return v < _take && _test(v);
        }

        [CanBeNull]
        public string Name(int v) {
            return _name(v);
        }

        public DisplayInputParams(JToken token) {
            switch (token) {
                case JArray tokenArray:
                    _take = tokenArray.Count;
                    _test = x => true;
                    _name = x => x >= 0 && x < tokenArray.Count ? ContentUtils.Translate(tokenArray[x]?.ToString()) : null;
                    break;
                case JObject tokenObject:
                    _take = int.MaxValue;
                    _test = x => tokenObject[x.ToInvariantString()] != null;
                    _name = x => ContentUtils.Translate(tokenObject[x.ToInvariantString()]?.ToString());
                    break;
                default:
                    _take = token?.Type == JTokenType.Integer ? (int)token : int.MaxValue;
                    _test = x => true;
                    _name = x => null;
                    break;
            }
        }

        [ContractAnnotation(@"
                => displayName:null, axes:null, buttons:null, povs:null, false;
                => displayName:notnull, axes:notnull, buttons:notnull, povs:notnull, true")]
        public static bool Get([NotNull] string guid, out string displayName, out DisplayInputParams axes, out DisplayInputParams buttons, out DisplayInputParams povs) {
            var file = FilesStorage.Instance.GetContentFile(ContentCategory.Controllers, $"{guid}.json");
            if (file.Exists) {
                try {
                    var jData = JsonExtension.Parse(File.ReadAllText(file.Filename));
                    displayName = ContentUtils.Translate(jData.GetStringValueOnly("name"));
                    axes = new DisplayInputParams(jData["axis"] ?? jData["axes"] ?? jData["axles"]);
                    buttons = new DisplayInputParams(jData["buttons"]);
                    povs = new DisplayInputParams(jData["pov"] ?? jData["povs"] ?? jData["pointOfViews"]);
                    return true;
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            displayName = null;
            axes = null;
            buttons = null;
            povs = null;
            return false;
        }
    }
}