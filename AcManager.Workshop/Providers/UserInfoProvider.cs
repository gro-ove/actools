using System.Collections.Generic;
using System.Threading.Tasks;
using AcManager.Workshop.Data;
using AcTools.Utils.Helpers;

namespace AcManager.Workshop.Providers {
    public class UserInfoProvider {
        private static Dictionary<string, UserInfo> _cache = new Dictionary<string, UserInfo>();
        private static TaskCache _tasks = new TaskCache();

        public static Task<UserInfo> GetAsync(string userId) {
            // TODO: error handling
            if (_cache.TryGetValue(userId, out var ret)) {
                return Task.FromResult(ret);
            }

            return _tasks.Get(async () => {
                _cache[userId] = await WorkshopHolder.Client.GetAsync<UserInfo>($"/users/{userId}");
                return _cache[userId];
            }, userId);
        }
    }
}