using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Workshop.Data;
using AcTools.Utils.Helpers;

namespace AcManager.Workshop.Providers {
    public class BrandBadgeProvider {
        private static List<WorkshopContentCategory> _carBrands;
        private static TaskCache _tasks = new TaskCache();

        public static Task<string> GetAsync(string badgeName) {
            // TODO: error handling
            if (_carBrands != null) {
                return Task.FromResult(_carBrands.FirstOrDefault(x => x.Name == badgeName)?.Icon);
            }

            return _tasks.Get(async () => {
                _carBrands = await WorkshopHolder.Client.GetAsync<List<WorkshopContentCategory>>("/car-brands");
                return _carBrands;
            }).ContinueWith(r => {
                return r.Result.FirstOrDefault(x => x.Name == badgeName)?.Icon;
            });
        }
    }
}