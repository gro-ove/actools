using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AcManager.Controls.Dialogs;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Selected;
using AcManager.Tools.AcErrors.Solutions;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Utils.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.AcErrors {
    public class SolutionsFactory : ISolutionsFactory {
        IEnumerable<ISolution> ISolutionsFactory.GetSolutions(AcError error) {
            switch (error.Type) {
                case AcErrorType.Load_Base:
                    return null;

                case AcErrorType.Data_JsonIsMissing:
                    return new[] {
                        Solve.TryToCreateNewFile((AcJsonObjectNew)error.Target)
                    }.Concat(Solve.TryToFindRenamedFile(error.Target.Location, ((AcJsonObjectNew)error.Target).JsonFilename)).Where(x => x != null);

                case AcErrorType.Data_JsonIsDamaged:
                    return new[] {
                        new MultiSolution(
                                @"Try to restore JSON file",
                                @"App will make an attempt to read known properties from damaged JSON file (be carefull, data loss is possible)",
                                e => {
                                    var t = (AcJsonObjectNew)e.Target;
                                    if (!Solve.TryToRestoreDamagedJsonFile(t.JsonFilename, JObjectRestorationSchemeProvider.GetScheme(t))) {
                                        throw new SolvingException("Can’t restore damaged JSON");
                                    }
                                }),
                        Solve.TryToCreateNewFile((AcJsonObjectNew)error.Target)
                    }.Concat(Solve.TryToFindRenamedFile(error.Target.Location, ((AcJsonObjectNew)error.Target).JsonFilename)).Where(x => x != null);

                case AcErrorType.Data_ObjectNameIsMissing:
                    return new[] {
                        new MultiSolution(
                                @"Set name",
                                @"Just set a new name",
                                e => {
                                    var value = Prompt.Show("Enter a new name", "New Name", AcStringValues.NameFromId(e.Target.Id), maxLength: 200);
                                    if (value == null) throw new SolvingException();
                                    e.Target.NameEditable = value;
                                }) { IsUiSolution = true },
                        new MultiSolution(
                                @"Set name based on ID",
                                $@"New name will be: “{AcStringValues.NameFromId(error.Target.Id)}”",
                                e => {
                                    e.Target.NameEditable = AcStringValues.NameFromId(e.Target.Id);
                                }), 
                    };

                case AcErrorType.Data_CarBrandIsMissing: {
                    var guess = AcStringValues.BrandFromName(error.Target.DisplayName);
                    return new[] {
                        new Solution(
                                @"Set brand name",
                                @"Just set a new brand name",
                                e => {
                                    var value = Prompt.Show("Enter a new brand name", "New Brand Name", guess, maxLength: 200,
                                            suggestions: SuggestionLists.CarBrandsList);
                                    if (value == null) throw new SolvingException();
                                    ((CarObject)e.Target).Brand = value;
                                }) { IsUiSolution = true },
                        guess == null ? null : new Solution(
                                @"Set brand name based on car’s name",
                                $@"New brand name will be: “{guess}”",
                                e => {
                                    ((CarObject)e.Target).Brand = guess;
                                })
                    }.NonNull();
                }

                case AcErrorType.Data_IniIsMissing:
                    // TODO
                    break;

                case AcErrorType.Data_IniIsDamaged:
                    // TODO
                    break;

                case AcErrorType.Data_UiDirectoryIsMissing:
                    // TODO
                    break;

                case AcErrorType.Car_ParentIsMissing:
                    return new[] {
                        new MultiSolution(
                                @"Make independent",
                                @"Remove id of missing parent from ui_car.json",
                                e => {
                                    ((CarObject)e.Target).ParentId = null;
                                }),
                        new MultiSolution(
                                @"Change parent",
                                @"Select a new parent from cars list",
                                e => {
                                    var target = (CarObject)e.Target;
                                    new ChangeCarParentDialog(target).ShowDialog();
                                    if (target.Parent == null) {
                                        throw new SolvingException();
                                    }
                                }) { IsUiSolution = true }
                    }.Concat(Solve.TryToFindRenamedFile(error.Target.Location, ((AcJsonObjectNew)error.Target).JsonFilename)).NonNull();

                case AcErrorType.Car_BrandBadgeIsMissing: {
                    var car = (CarObject)error.Target;
                    var fit = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, $"{car.Brand}.png");
                    return new ISolution[] {
                        fit.Exists ? new MultiSolution(
                            $"Set {car.Brand} badge",
                            "Set the brand’s badge from Content storage",
                            e => {
                                var c = (CarObject)e.Target;
                                var f = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, $"{c.Brand}.png");
                                if (!f.Exists) return;
                                File.Copy(f.Filename, c.BrandBadge);
                            }) : null, 
                        new MultiSolution(
                                @"Change brand’s badge",
                                @"Select a new brand’s badge from the list",
                                e => {
                                    var target = (CarObject)e.Target;
                                    new BrandBadgeEditor(target).ShowDialog();
                                    if (!File.Exists(target.BrandBadge)) {
                                        throw new SolvingException();
                                    }
                                }) { IsUiSolution = true }
                    }.Concat(Solve.TryToFindRenamedFile(error.Target.Location, ((CarObject)error.Target).BrandBadge)).NonNull();
                }

                case AcErrorType.Car_UpgradeIconIsMissing: {
                    var car = (CarObject)error.Target;
                    var label = UpgradeIconEditor.TryToGuessLabel(car.DisplayName) ?? "S1";
                    var fit = FilesStorage.Instance.GetContentFile(ContentCategory.UpgradeIcons, $"{label}.png");
                    return new ISolution[] {
                        fit.Exists ? new MultiSolution(
                            $"Set “{label}” icon",
                            "Set the upgrade icon from Content storage",
                            e => {
                                var c = (CarObject)e.Target;
                                var l = UpgradeIconEditor.TryToGuessLabel(c.DisplayName) ?? "S1";
                                var f = FilesStorage.Instance.GetContentFile(ContentCategory.UpgradeIcons, $"{l}.png");
                                if (!f.Exists) return;
                                File.Copy(f.Filename, c.UpgradeIcon);
                            }) : null, 
                        new MultiSolution(
                                @"Change upgrade icon",
                                @"Select or create a new upgrade icon with the editor",
                                e => {
                                    var target = (CarObject)e.Target;
                                    new UpgradeIconEditor(target).ShowDialog();
                                    if (!File.Exists(target.UpgradeIcon)) {
                                        throw new SolvingException();
                                    }
                                }) { IsUiSolution = true }
                    }.Concat(Solve.TryToFindRenamedFile(error.Target.Location, ((CarObject)error.Target).UpgradeIcon)).NonNull();
                }

                case AcErrorType.Showroom_Kn5IsMissing:
                    return new[] {
                        new MultiSolution(
                                @"Make an empty model",
                                @"With nothing, only emptyness",
                                e => {
                                    Kn5.CreateEmpty().SaveAll(((ShowroomObject)e.Target).Kn5Filename);
                                })
                    }.Concat(Solve.TryToFindAnyFile(error.Target.Location, ((ShowroomObject)error.Target).Kn5Filename, "*.kn5")).Where(x => x != null);

                case AcErrorType.Data_KunosCareerEventsAreMissing:
                    break;
                case AcErrorType.Data_KunosCareerConditions:
                    break;
                case AcErrorType.Data_KunosCareerContentIsMissing:
                    break;
                case AcErrorType.Data_KunosCareerTrackIsMissing:
                    break;
                case AcErrorType.Data_KunosCareerCarIsMissing:
                    break;
                case AcErrorType.Data_KunosCareerCarSkinIsMissing:
                    break;
                case AcErrorType.Data_KunosCareerWeatherIsMissing:
                    break;

                case AcErrorType.CarSkins_SkinsAreMissing:
                    return new[] {
                        new MultiSolution(
                                @"Create empty skin",
                                @"Create a new empty skin called “default”",
                                e => {
                                    var target = Path.Combine(((CarObject)e.Target).SkinsDirectory, "default");
                                    Directory.CreateDirectory(target);
                                    File.WriteAllText(Path.Combine(target, "ui_skin.json"), JsonConvert.SerializeObject(new {
                                        skinname = "Default",
                                        drivername = "",
                                        country = "",
                                        team = "",
                                        number = 0
                                    }));
                                })
                    }.Union(((CarObject)error.Target).SkinsManager.WrappersList.Where(x => !x.Value.Enabled).Select(x => new MultiSolution(
                            $@"Enable {x.Value.DisplayName}",
                            "Enable disabled skin",
                            (IAcError e) => {
                                ((CarSkinObject)x.Loaded()).ToggleCommand.Execute(null);
                            }
                            )))
                     .Concat(Solve.TryToFindRenamedFile(error.Target.Location, ((CarObject)error.Target).SkinsDirectory, true)).NonNull();

                case AcErrorType.CarSkins_DirectoryIsUnavailable:
                    return null;

                case AcErrorType.Font_BitmapIsMissing: 
                    return Solve.TryToFindRenamedFile(Path.GetDirectoryName(error.Target.Location), ((FontObject)error.Target).FontBitmap);

                case AcErrorType.Font_UsedButDisabled:
                    return new[] {
                        new MultiSolution(
                                @"Enable",
                                @"Move font from “fonts-off” to “fonts”",
                                e => {
                                    e.Target.ToggleCommand.Execute(null);
                                })
                    };

                case AcErrorType.CarSetup_TrackIsMissing:
                    return new[] {
                        new Solution(
                                @"Find track",
                                @"Try to find track online",
                                e => {
                                    Process.Start($@"http://assetto-db.com/track/{((CarSetupObject)e.Target).TrackId}");
                                }),
                        new MultiSolution(
                                @"Make generic",
                                @"Move to generic folder",
                                e => {
                                    ((CarSetupObject)e.Target).TrackId = null;
                                })
                    };

                case AcErrorType.CarSkin_PreviewIsMissing:
                    return new ISolution[] {
                        new MultiSolution(
                                @"Generate new preview",
                                @"Generate a new preview using recently used preset",
                                e => {
                                    var list = e.ToList();
                                    var carId = ((CarSkinObject)list[0].Target).CarId;
                                    var skinIds = list.Select(x => x.Target.Id).ToArray();
                                    if (!new CarUpdatePreviewsDialog(CarsManager.Instance.GetById(carId), skinIds,
                                            SelectedCarPage.SelectedCarPageViewModel.GetAutoUpdatePreviewsDialogMode()).ShowDialog()) {
                                        throw new SolvingException();
                                    }
                                }) { IsUiSolution = true },
                        new MultiSolution(
                                @"Setup and generate new preview",
                                @"Select a new preview through settings",
                                e => {
                                    var list = e.ToList();
                                    var carId = ((CarSkinObject)list[0].Target).CarId;
                                    var skinIds = list.Select(x => x.Target.Id).ToArray();
                                    if (!new CarUpdatePreviewsDialog(CarsManager.Instance.GetById(carId), skinIds,
                                            CarUpdatePreviewsDialog.DialogMode.Options).ShowDialog()) {
                                        throw new SolvingException();
                                    }
                                }) { IsUiSolution = true }
                    };

                case AcErrorType.CarSkin_LiveryIsMissing:
                    return new ISolution[] {
                        new AsyncMultiSolution(
                                @"Generate new livery",
                                @"Generate a new livery using last settings of Livery Editor",
                                e => LiveryIconEditor.GenerateAsync((CarSkinObject)e.Target)),
                        new AsyncMultiSolution(
                                @"Generate random livery",
                                @"Generate a new livery using random settings",
                                e => LiveryIconEditor.GenerateRandomAsync((CarSkinObject)e.Target)),
                        new MultiSolution(
                                @"Setup new livery",
                                @"Select a new livery using Livery Editor",
                                e => {
                                    if (!new LiveryIconEditor((CarSkinObject)e.Target).ShowDialog()) {
                                        throw new SolvingException();
                                    }
                                }) { IsUiSolution = true }
                    };
            }

            return null;
        }
    }
}
