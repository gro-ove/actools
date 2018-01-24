using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Entries;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.ContentInstallation {
    public partial class ContentInstallationEntry {
        private static async Task<IReadOnlyList<ExtraOption>> GetGbwRelatedExtraOptions(ContentEntryBase[] entries) {
            const string gbwWeatherPart = "_gbW_";
            const string gbwPpFilterPart = "__gbW";

            if (!entries.Any(x => x.Id.Contains(gbwWeatherPart))) {
                // This is not the GBW pack
                return new ExtraOption[0];
            }

            var gbwWeatherIds = entries.Where(x => x.Id.Contains(gbwWeatherPart) && x is WeatherContentEntry)
                                    .Select(x => x.Id).ToList();
            if (gbwWeatherIds.Count < 10) {
                // It contains some GBW weather, but not a lot — not the pack
                return new ExtraOption[0];
            }

            foreach (var weather in entries.OfType<WeatherContentEntry>()) {
                weather.SelectedOption = weather.UpdateOptions.FirstOrDefault(x => x.RemoveExisting) ?? weather.SelectedOption;
            }

            await WeatherManager.Instance.EnsureLoadedAsync();

            // Now, when data is loaded, we’re ready to create some extra options
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
                    var gbwPpFilterIds = entries.Where(x => x.Id.Contains(gbwPpFilterPart) && x is PpFilterContentEntry)
                                                .Select(x => x.Id).ToList();
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