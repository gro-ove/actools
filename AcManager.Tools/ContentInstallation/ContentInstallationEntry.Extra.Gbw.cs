using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public partial class ContentInstallationEntry {
        private static async Task<IReadOnlyList<ExtraOption>> GetGbwRelatedExtraOptions(EntryWrapper[] entries) {
            const string gbwWeatherPart = "_gbW_";
            const string gbwPpFilterPart = "__gbW";

            if (!entries.Any(x => x.Entry.Id.Contains(gbwWeatherPart))) {
                // this is not the GBW pack
                return new ExtraOption[0];
            }

            var gbwWeatherIds = entries.Where(x => x.Entry.Id.Contains(gbwWeatherPart) && x.Entry is WeatherContentEntry)
                                    .Select(x => x.Entry.Id).ToList();
            if (gbwWeatherIds.Count < 10) {
                // it contains some GBW weather, but not a lot — not the pack
                return new ExtraOption[0];
            }

            await WeatherManager.Instance.EnsureLoadedAsync();

            // now, when data is loaded, we’re ready to create some extra options
            IEnumerable<ExtraOption> GetOptions() {
                {
                    var installedWeatherIds = WeatherManager.Instance.WrappersList.Select(x => x.Id).Where(x => x.Contains(gbwWeatherPart)).ToList();
                    var obsoleteWeatherIds = installedWeatherIds.ApartFrom(gbwWeatherIds).ToList();

                    if (obsoleteWeatherIds.Count > 0) {
                        var obsoleteLine = obsoleteWeatherIds.Select(x => $"“{WeatherManager.Instance.GetById(x)?.DisplayName ?? x}”")
                                                             .JoinToReadableString();
                        yield return new ExtraOption("Remove obsolete GBW weather",
                                $"Installed, but not found here: {obsoleteLine}.",
                                async (progress, cancellation) => {
                                    progress.Report(AsyncProgressEntry.FromStringIndetermitate("Removing obsolete GBW weather…"));
                                    await WeatherManager.Instance.EnsureLoadedAsync();
                                    await WeatherManager.Instance.DeleteAsync(obsoleteWeatherIds);
                                }, activeByDefault: true);
                    }
                }

                {
                    var gbwPpFilterIds = entries.Where(x => x.Entry.Id.Contains(gbwPpFilterPart) && x.Entry is PpFilterContentEntry)
                                                .Select(x => x.Entry.Id).ToList();
                    var installedPpFilterIds = PpFiltersManager.Instance.WrappersList.Select(x => x.Id).Where(x => x.Contains(gbwPpFilterPart)).ToList();
                    var obsoletePpFilterIds = installedPpFilterIds.ApartFrom(gbwPpFilterIds).ToList();

                    if (obsoletePpFilterIds.Count > 0) {
                        var obsoleteLine = obsoletePpFilterIds.Select(x => $"“{PpFiltersManager.Instance.GetById(x)?.DisplayName ?? x}”")
                                                              .JoinToReadableString();
                        yield return new ExtraOption("Remove obsolete GBW PP-filters",
                                $"Installed, but not found here: {obsoleteLine}.",
                                async (progress, cancellation) => {
                                    progress.Report(AsyncProgressEntry.FromStringIndetermitate("Removing obsolete GBW PP-filters…"));
                                    await PpFiltersManager.Instance.EnsureLoadedAsync();
                                    await PpFiltersManager.Instance.DeleteAsync(obsoletePpFilterIds);
                                }, activeByDefault: true);
                    }
                }
            }

            return GetOptions().ToList();
        }
    }
}