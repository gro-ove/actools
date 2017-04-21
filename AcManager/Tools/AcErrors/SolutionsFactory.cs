using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Dialogs;
using AcManager.Pages.Dialogs;
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
                                AppStrings.Solution_RestoreJsonFile,
                                AppStrings.Solution_RestoreJsonFile_Details,
                                e => {
                                    var t = (AcJsonObjectNew)e.Target;
                                    if (!Solve.TryToRestoreDamagedJsonFile(t.JsonFilename, JObjectRestorationSchemeProvider.GetScheme(t))) {
                                        throw new SolvingException(AppStrings.Solution_CannotRestoreJsonFile);
                                    }
                                }),
                        Solve.TryToCreateNewFile((AcJsonObjectNew)error.Target)
                    }.Concat(Solve.TryToFindRenamedFile(error.Target.Location, ((AcJsonObjectNew)error.Target).JsonFilename)).Where(x => x != null);

                case AcErrorType.Data_ObjectNameIsMissing:
                    return new[] {
                        new MultiSolution(
                                AppStrings.Solution_SetName,
                                AppStrings.Solution_SetName_Details,
                                e => {
                                    var value = Prompt.Show(AppStrings.Solution_SetName_Prompt, AppStrings.Common_NewName,
                                            AcStringValues.NameFromId(e.Target.Id), maxLength: 200);
                                    if (value == null) throw new SolvingException();
                                    e.Target.NameEditable = value;
                                }) { IsUiSolution = true },
                        new MultiSolution(
                                AppStrings.Solution_SetNameFromId,
                                string.Format(AppStrings.Solution_SetNameFromId_Details, AcStringValues.NameFromId(error.Target.Id)),
                                e => {
                                    e.Target.NameEditable = AcStringValues.NameFromId(e.Target.Id);
                                }),
                    };

                case AcErrorType.Data_CarBrandIsMissing: {
                    var guess = AcStringValues.BrandFromName(error.Target.DisplayName);
                    return new[] {
                        new Solution(
                                AppStrings.Solution_SetBrandName,
                                AppStrings.Solution_SetBrandName_Details,
                                e => {
                                    var value = Prompt.Show(AppStrings.Solution_SetBrandName_Prompt, AppStrings.Common_NewBrandName, guess,
                                            maxLength: 200,
                                            suggestions: SuggestionLists.CarBrandsList);
                                    if (value == null) throw new SolvingException();
                                    ((CarObject)e.Target).Brand = value;
                                }) { IsUiSolution = true },
                        guess == null ? null : new Solution(
                                AppStrings.Solution_SetBrandNameFromName,
                                string.Format(AppStrings.Solution_SetBrandNameFromName_Details, guess),
                                e => {
                                    ((CarObject)e.Target).Brand = guess;
                                })
                    }.NonNull();
                }

                case AcErrorType.Data_IniIsMissing:
                    return Solve.TryToFindRenamedFile(error.Target.Location, ((AcIniObject)error.Target).IniFilename).Union(new[] {
                        new MultiSolution(
                                AppStrings.Solution_RemoveObject,
                                AppStrings.Solution_RemoveObject_Details,
                                e => {
                                    e.Target.DeleteCommand.Execute(null);
                                })
                    });

                case AcErrorType.Weather_ColorCurvesIniIsMissing:
                    return Solve.TryToFindRenamedFile(error.Target.Location, ((WeatherObject)error.Target).ColorCurvesIniFilename).Union(new[] {
                        new MultiSolution(
                                AppStrings.Solution_GenerateNew,
                                AppStrings.Solution_GenerateNew_Details,
                                e => {
                                    File.WriteAllText(((WeatherObject)e.Target).ColorCurvesIniFilename, "");
                                }),
                        new MultiSolution(
                                AppStrings.Solution_RemoveObject,
                                AppStrings.Solution_RemoveObject_Details,
                                e => {
                                    e.Target.DeleteCommand.Execute(null);
                                })
                    });

                case AcErrorType.Data_IniIsDamaged:
                    return Solve.TryToFindRenamedFile(error.Target.Location, ((AcIniObject)error.Target).IniFilename).Union(new[] {
                        new MultiSolution(
                                AppStrings.Solution_RemoveObject,
                                AppStrings.Solution_RemoveObject_Details,
                                e => {
                                    e.Target.DeleteCommand.Execute(null);
                                })
                    });

                case AcErrorType.Data_UiDirectoryIsMissing:
                    // TODO
                    break;

                case AcErrorType.Car_ParentIsMissing:
                    return new[] {
                        new MultiSolution(
                                AppStrings.Solution_MakeIndependent,
                                AppStrings.Solution_MakeIndependent_Details,
                                e => {
                                    ((CarObject)e.Target).ParentId = null;
                                }),
                        new MultiSolution(
                                AppStrings.Solution_ChangeParent,
                                AppStrings.Solution_ChangeParent_Details,
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
                                string.Format(AppStrings.Solution_SetBrandBadge, car.Brand),
                                AppStrings.Solution_SetBrandBadge_Details,
                                e => {
                                    var c = (CarObject)e.Target;
                                    var f = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, $"{c.Brand}.png");
                                    if (!f.Exists) return;
                                    File.Copy(f.Filename, c.BrandBadge);
                                }) : null,
                        new MultiSolution(
                                AppStrings.Solution_ChangeBrandBadge,
                                AppStrings.Solution_ChangeBrandBadge_Details,
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
                    var label = UpgradeIconEditor.TryToGuessLabel(car.DisplayName) ?? @"S1";
                    var fit = FilesStorage.Instance.GetContentFile(ContentCategory.UpgradeIcons, $"{label}.png");
                    return new ISolution[] {
                        fit.Exists ? new MultiSolution(
                                string.Format(AppStrings.Solution_SetUpgradeIcon, label),
                                AppStrings.Solution_SetUpgradeIcon_Details,
                                e => {
                                    var c = (CarObject)e.Target;
                                    var l = UpgradeIconEditor.TryToGuessLabel(c.DisplayName) ?? @"S1";
                                    var f = FilesStorage.Instance.GetContentFile(ContentCategory.UpgradeIcons, $"{l}.png");
                                    if (!f.Exists) return;
                                    File.Copy(f.Filename, c.UpgradeIcon);
                                }) : null,
                        new MultiSolution(
                                AppStrings.Solution_ChangeUpgradeIcon,
                                AppStrings.Solution_ChangeUpgradeIcon_Details,
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
                                AppStrings.Solution_MakeEmptyModel,
                                AppStrings.Solution_MakeEmptyModel_Details,
                                e => {
                                    Kn5.CreateEmpty().Save(((ShowroomObject)e.Target).Kn5Filename);
                                })
                    }.Concat(Solve.TryToFindAnyFile(error.Target.Location, ((ShowroomObject)error.Target).Kn5Filename, @"*.kn5")).Where(x => x != null);

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
                                AppStrings.Solution_CreateEmptySkin,
                                AppStrings.Solution_CreateEmptySkin_Details,
                                e => {
                                    var target = Path.Combine(((CarObject)e.Target).SkinsDirectory, "default");
                                    Directory.CreateDirectory(target);
                                    File.WriteAllText(Path.Combine(target, "ui_skin.json"), JsonConvert.SerializeObject(new {
                                        skinname = @"Default",
                                        drivername = "",
                                        country = "",
                                        team = "",
                                        number = 0
                                    }));
                                })
                    }.Union(((CarObject)error.Target).SkinsManager.WrappersList.Where(x => !x.Value.Enabled).Select(x => new MultiSolution(
                            string.Format(AppStrings.Solution_EnableSkin, x.Value.DisplayName),
                            AppStrings.Solution_EnableSkin_Details,
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
                                AppStrings.Solution_Enable,
                                AppStrings.Solution_Enable_Details,
                                e => {
                                    e.Target.ToggleCommand.Execute(null);
                                })
                    };

                case AcErrorType.CarSetup_TrackIsMissing:
                    return new[] {
                        new Solution(
                                AppStrings.Solution_FindTrack,
                                AppStrings.Solution_FindTrack_Details,
                                e => {
                                    var trackId = ((CarSetupObject)e.Target).TrackId;
                                    if (trackId != null) {
                                        WindowsHelper.ViewInBrowser(
                                                SettingsHolder.Content.MissingContentSearch.GetUri(trackId, SettingsHolder.MissingContentType.Track));
                                    }
                                }),
                        new MultiSolution(
                                AppStrings.Solution_MakeGeneric,
                                AppStrings.Solution_MakeGeneric_Details,
                                e => {
                                    ((CarSetupObject)e.Target).TrackId = null;
                                })
                    };

                case AcErrorType.CarSkin_PreviewIsMissing:
                    return new ISolution[] {
                        new AsyncMultiSolution(
                                AppStrings.Solution_GeneratePreview,
                                AppStrings.Solution_GeneratePreview_Details,
                                async e => {
                                    var list = e.ToList();
                                    var carId = ((CarSkinObject)list[0].Target).CarId;
                                    var skinIds = list.Select(x => x.Target.Id).ToArray();
                                    var car = CarsManager.Instance.GetById(carId);
                                    if (car == null) throw new SolvingException();

                                    await new ToUpdatePreview(car, skinIds).Run();
                                }) { IsUiSolution = true },
                        new AsyncMultiSolution(
                                AppStrings.Solution_SetupPreview,
                                AppStrings.Solution_SetupPreview_Details,
                                async e => {
                                    var list = e.ToList();
                                    var carId = ((CarSkinObject)list[0].Target).CarId;
                                    var skinIds = list.Select(x => x.Target.Id).ToArray();
                                    var car = CarsManager.Instance.GetById(carId);
                                    if (car == null) throw new SolvingException();

                                    await new ToUpdatePreview(car, skinIds).Run(UpdatePreviewMode.Options);
                                }) { IsUiSolution = true }
                    };

                case AcErrorType.CarSkin_LiveryIsMissing:
                    return new ISolution[] {
                        new AsyncMultiSolution(
                                AppStrings.Solution_GenerateLivery,
                                AppStrings.Solution_GenerateLivery_Details,
                                e => LiveryIconEditor.GenerateAsync((CarSkinObject)e.Target)),
                        new AsyncMultiSolution(
                                AppStrings.Solution_RandomLivery,
                                AppStrings.Solution_RandomLivery_Details,
                                e => LiveryIconEditor.GenerateRandomAsync((CarSkinObject)e.Target)),
                        new MultiSolution(
                                AppStrings.Solution_SetupLivery,
                                AppStrings.Solution_SetupLivery_Details,
                                e => {
                                    if (!new LiveryIconEditor((CarSkinObject)e.Target).ShowDialog()) {
                                        throw new SolvingException();
                                    }
                                }) { IsUiSolution = true }
                    };


                case AcErrorType.Replay_TrackIsMissing:
                    return new[] {
                        new MultiSolution(
                                AppStrings.Solution_RemoveReplay,
                                AppStrings.Solution_RemoveReplay_Details,
                                e => {
                                    e.Target.DeleteCommand.Execute(null);
                                })
                    };

                case AcErrorType.Replay_InvalidName:
                    return new[] {
                        new MultiSolution(
                                AppStrings.Solution_FixName,
                                AppStrings.Solution_FixName_Details,
                                e => {
                                    e.Target.NameEditable = Regex.Replace(e.Target.NameEditable ?? @"-", @"[\[\]]", "");
                                })
                    };

                case AcErrorType.Data_UserChampionshipCarIsMissing:
                    break;
                case AcErrorType.Data_UserChampionshipTrackIsMissing:
                    break;
                case AcErrorType.Data_UserChampionshipCarSkinIsMissing:
                    break;
                case AcErrorType.ExtendedData_JsonIsDamaged:
                    break;

                case AcErrorType.Track_MapIsMissing:
                    return new ISolution[] {
                        new AsyncSolution(
                                "Create map from surfaces",
                                "Choose what surfaces to render to a map and save the result",
                                e => TrackMapRendererWrapper.Run((TrackObjectBase)e.Target, false)),
                        ((TrackObjectBase)error.Target).AiLaneFastExists ? new AsyncSolution(
                                "Create map from AI lane",
                                "Not sure but I think AC works similar way",
                                e => TrackMapRendererWrapper.Run((TrackObjectBase)e.Target, true)) : null
                    }.Concat(Solve.TryToFindRenamedFile(error.Target.Location, ((TrackObjectBase)error.Target).MapImage)).NonNull();

                case AcErrorType.Track_OutlineIsMissing:
                    return new ISolution[] {
                        new AsyncMultiSolution(
                                "Create outline",
                                "Create a new outline image with settings previosly used for this track",
                                async e => {
                                    foreach (var err in e) {
                                        await TrackOutlineRendererWrapper.UpdateAsync((TrackObjectBase)err.Target);
                                    }
                                }),
                        new AsyncSolution(
                                "Setup outline",
                                "Specify settings and create a new outline image",
                                e => TrackOutlineRendererWrapper.Run((TrackObjectBase)e.Target))
                    }.Concat(Solve.TryToFindRenamedFile(error.Target.Location, ((TrackObjectBase)error.Target).MapImage)).NonNull();

                case AcErrorType.Track_PreviewIsMissing:
                    return new ISolution[] {
                        new AsyncSolution(
                                "Create and set a new preview",
                                "Run AC, make a screenshot and apply it",
                                e => TrackPreviewsCreator.ShotAndApply((TrackObjectBase)e.Target)),
                        new AsyncSolution(
                                "Use existing screenshot",
                                "Just select any image you already have",
                                e => TrackPreviewsCreator.ApplyExisting((TrackObjectBase)e.Target))
                    }.Concat(Solve.TryToFindRenamedFile(error.Target.Location, ((TrackObjectBase)error.Target).MapImage)).NonNull();

                default:
                    return null;
            }

            return null;
        }
    }
}
