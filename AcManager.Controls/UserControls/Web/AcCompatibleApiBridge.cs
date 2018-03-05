using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using AcManager.Controls.UserControls.Cef;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Controls.UserControls.Web {
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
    public class AcCompatibleApiBridge : JsBridgeBase {
        private readonly IniFile _raceConfig = Game.DefaultRaceConfig;

        public override string AcApiRequest(string url) {
            url = url.SubstringExt(AcApiHandlerFactory.AcSchemeName.Length + 3);
            Logging.Debug(url);

            var index = url.IndexOf('?');
            var pieces = (index == -1 ? url : url.Substring(0, index)).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            switch (pieces[0]) {
                case "getguid":
                    return SteamIdHelper.Instance.Value;
                case "setsetting":
                    switch (pieces.ArrayElementAtOrDefault(1)) {
                        case "race":
                            foreach (var parameter in GetParameters()) {
                                var p = parameter.Key.Split('/');
                                if (p.Length != 2) {
                                    Logging.Warning($"Invalid key: {parameter.Key}");
                                } else {
                                    Logging.Debug($"Parameter: {parameter.Key}={parameter.Value}");
                                    _raceConfig[p[0]].Set(p[1], parameter.Value);
                                }
                            }
                            break;
                        default:
                            Logging.Warning($"Unknown setting: {pieces.ArrayElementAtOrDefault(1)}");
                            break;
                    }
                    return string.Empty;
                case "start":
                    ActionExtension.InvokeInMainThread(() => {
                        GameWrapper.StartAsync(new Game.StartProperties {
                            PreparedConfig = _raceConfig
                        });
                    });
                    return string.Empty;
                default:
                    Logging.Warning($"Unknown request: {pieces[0]} (“{url}”)");
                    return null;
            }

            Dictionary<string, string> GetParameters() {
                return (index == -1 ? "" : url.Substring(index + 1))
                        .Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Split(new[] { '=' }, 2)).ToDictionary(
                                x => Uri.UnescapeDataString(x[0]),
                                x => Uri.UnescapeDataString(x.ArrayElementAtOrDefault(1) ?? ""));
            }
        }

        public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
            if (AcApiHosts.Contains(url.GetDomainNameFromUrl(), StringComparer.OrdinalIgnoreCase)) {
                toInject.Add(@"<script>!function(){
window.__AC = {};
window.__AC.CarsArray = JSON.parse(window.external.GetCars());
window.__AC.TracksArray = JSON.parse(window.external.GetTracks());
window.__AC.Cars = {};
window.__AC.Tracks = window.__AC.TracksArray.reduce(function (a, b){ a[b.id] = b; return a; }, {});
for (var i = 0; i < window.__AC.CarsArray.length; i++){
    var car = window.__AC.CarsArray[i];
    window.__AC.Cars[car.id] = car;
    car.__defineGetter__('skins', getSkins.bind(null, car));
    car.__defineGetter__('skinmeta', getSkinMeta.bind(null, car));
}
function getSkins(car){ return car._skins || (car._skins = JSON.parse(window.external.GetSkins(car.id))); }
function getSkinMeta(car){ return car._skinmeta || (car._skinmeta = JSON.parse(window.external.GetSkinMeta(car.id))); }
function find(a, f, p1, p2) { return p1 && a.filter(function (n){ return (p2 ? n[p1][p2] : n[p1]).indexOf(f) > -1; }); }
window.__AC.findCar = find.bind(null, __AC.CarsArray);
window.__AC.findTrack = find.bind(null, __AC.TracksArray);
}()</script>");
            }
        }

        [UsedImplicitly]
        public string GetSkins(string carId) {
            var car = CarsManager.Instance.GetById(carId);
            return car == null ? @"[]" : JsonConvert.SerializeObject(car.SkinsManager.WrappersList.Where(x => x.Value.Enabled).Select(x => x.Id));
        }

        [UsedImplicitly]
        public string GetSkinMeta(string carId) {
            var car = CarsManager.Instance.GetById(carId);
            if (car == null) return @"[]";

            car.SkinsManager.EnsureLoaded();
            return JsonConvert.SerializeObject(car.EnabledOnlySkins.Select(x => new {
                skinName = x.Name,
                driverName = x.DriverName,
                country = x.Country,
                number = x.SkinNumber,
                team = x.Team
            }).ToList());
        }

        private string _cars;

        [UsedImplicitly]
        public string GetCars() {
            return _cars ?? (_cars = JsonConvert.SerializeObject(CarsManager.Instance.Enabled.Select(x => new {
                id = x.Id,
                name = x.Name,
                brand = x.Brand,
                path = Tab.ConvertFilename(x.Location),
                badge = Tab.ConvertFilename(x.BrandBadge),

                // Is it needed or is it just a waste of time?
                // description = x.Description,
                // tags = x.Tags,
                // @class = x.CarClass,

                // Extra bits, not implemented yet:
                // "specs": { "bhp": "420bhp", "torque": "294Nm", "weight": "1050kg", "topspeed": "280+km/h", "acceleration": "", "pwratio": "2.50kg/hp", "range": 128 },
                // "torqueCurve": [ [ "0", "0" ], [ "500", "82" ], … ],
                // "powerCurve": [ … ],

                // This flag is to check if content has been bought or not. CM ignores all non-bought content,
                // so it’s always false
                dlc = false
            })));
        }

        private string _tracks;

        [UsedImplicitly]
        public string GetTracks() {
            return _tracks ?? (_tracks = JsonConvert.SerializeObject(TracksManager.Instance.Enabled.SelectMany(
                    x => x.MultiLayouts?.OfType<TrackObjectBase>() ?? new TrackObjectBase[] { x }).Select(x => new {
                        id = x.KunosIdWithLayout,
                        name = x.Name,
                        dlc = false,
                        preview = Tab.ConvertFilename(x.PreviewImage),
                        outline = Tab.ConvertFilename(x.OutlineImage),
                    })));
        }
    }
}