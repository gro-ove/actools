using System;
using System.Collections.Generic;
using System.Linq;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public class UpdateOption : NotifyPropertyChanged {
        private bool _enabled = true;

        public string Name { get; set; }

        public Func<string, bool> Filter { get; set; }

        public bool RemoveExisting { get; set; }

        public bool Enabled {
            get { return _enabled; }
            set {
                if (Equals(value, _enabled)) return;
                _enabled = value;
                OnPropertyChanged();
            }
        }

        public override string ToString() {
            return Name;
        }

        public static IEnumerable<UpdateOption> GetByType(AdditionalContentType type) {
            return new[] {
                new UpdateOption { Name = ToolsStrings.Installator_UpdateEverything },
                new UpdateOption { Name = ToolsStrings.Installator_RemoveExistingFirst, RemoveExisting = true }
            }.Union(GetByTypeOnly(type));
        }

        private static IEnumerable<UpdateOption> GetByTypeOnly(AdditionalContentType type) {
            switch (type) {
                case AdditionalContentType.Car: {
                        Func<string, bool> uiFilter =
                            x => x != @"ui\ui_car.json" && x != @"ui\brand.png" && x != @"logo.png" && (
                                !x.StartsWith(@"skins\") || !x.EndsWith(@"\ui_skin.json")
                            );
                        Func<string, bool> previewsFilter =
                            x => !x.StartsWith(@"skins\") || !x.EndsWith(@"\preview.jpg");
                        yield return new UpdateOption { Name = ToolsStrings.Installator_KeepUiInformation, Filter = uiFilter };
                        yield return new UpdateOption { Name = ToolsStrings.Installator_KeepSkinsPreviews, Filter = previewsFilter };
                        yield return new UpdateOption { Name = ToolsStrings.Installator_KeepUiInformationAndSkinsPreviews, Filter = x => uiFilter(x) && previewsFilter(x) };
                        break;
                    }

                case AdditionalContentType.Track: {
                        Func<string, bool> uiFilter =
                            x => !x.StartsWith(@"ui\") ||
                                 !x.EndsWith(@"\ui_track.json") && !x.EndsWith(@"\preview.png") &&
                                 !x.EndsWith(@"\outline.png");
                        yield return new UpdateOption { Name = ToolsStrings.Installator_KeepUiInformation, Filter = uiFilter };
                        break;
                    }

                case AdditionalContentType.CarSkin: {
                        Func<string, bool> uiFilter = x => x != @"ui_skin.json";
                        Func<string, bool> previewFilter = x => x != @"preview.jpg";
                        yield return new UpdateOption { Name = ToolsStrings.Installator_KeepUiInformation, Filter = uiFilter };
                        yield return new UpdateOption { Name = ToolsStrings.Installator_KeepSkinPreview, Filter = previewFilter };
                        yield return new UpdateOption { Name = ToolsStrings.Installator_KeepUiInformationAndSkinPreview, Filter = x => uiFilter(x) && previewFilter(x) };
                        break;
                    }

                case AdditionalContentType.Showroom: {
                        Func<string, bool> uiFilter =
                            x => x != @"ui\ui_showroom.json";
                        yield return new UpdateOption { Name = ToolsStrings.Installator_KeepUiInformation, Filter = uiFilter };
                        break;
                    }

                case AdditionalContentType.Font:
                    break;
            }
        }
    }
}