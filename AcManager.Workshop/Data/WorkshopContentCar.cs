using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Workshop.Providers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    public class WorkshopContentCar : ContentInfoBase {
        public override string DisplayName {
            get {
                var name = Name;
                if (name == null) return Id;

                if (SettingsHolder.Content.CarsDisplayNameCleanUp) {
                    name = name.Replace(@"™", "");
                }

                var yearValue = Year;
                if (yearValue > 1900 && SettingsHolder.Content.CarsYearPostfix) {
                    if (SettingsHolder.Content.CarsYearPostfixAlt) {
                        return $@"{name} ({yearValue})";
                    }
                    var year = yearValue.ToString();
                    var index = name.Length - year.Length - 1;
                    if ((!name.EndsWith(year) || index > 0 && char.IsLetterOrDigit(name[index]))
                            && !AcStringValues.GetYearFromName(name).HasValue) {
                        return $@"{name} ’{yearValue % 100:D2}";
                    }
                }

                return name;
            }
        }

        public WorkshopContentCar() {
            BrandBadge = Lazier.CreateAsync(() => BrandBadgeProvider.GetAsync(Brand));
        }

        [JsonProperty("parentID")]
        public string ParentID { get; set; }

        [JsonProperty("carBrand")]
        public string Brand { get; set; }

        public Lazier<string> BrandBadge { get; }

        [JsonProperty("carClass")]
        public string CarClass { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("year")]
        public int Year { get; set; }

        [JsonProperty("weight")]
        public double Weight { get; set; }

        [JsonProperty("power")]
        public double Power { get; set; }

        [JsonProperty("torque")]
        public double Torque { get; set; }

        [JsonProperty("speed")]
        public double Speed { get; set; }

        [JsonProperty("acceleration")]
        public double Acceleration { get; set; }

        [JsonProperty("previewImage")]
        public string PreviewImage { get; set; }

        [JsonProperty("upgradeIcon")]
        public string UpgradeIcon { get; set; }

        [JsonProperty("defaultSkinsCount")]
        public int DefaultSkinsCount { get; set; }

        [JsonProperty("skinsCount")]
        public int SkinsCount { get; set; }

        [JsonProperty("skins"), CanBeNull]
        public List<WorkshopContentCarSkin> Skins { get; set; }

        private WorkshopContentCarSkin _selectedSkin;

        public WorkshopContentCarSkin SelectedSkin {
            get => _selectedSkin ?? (_selectedSkin = Skins?.FirstOrDefault());
            set => Apply(value, ref _selectedSkin);
        }

        [JsonIgnore, NotNull]
        public string ShortName => DisplayName.ApartFromFirst(Brand).TrimStart();
    }
}