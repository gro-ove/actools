using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Workshop.Data;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

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
                var r = await WorkshopHolder.Client.GetAsync<UserInfo>($"/users/{Uri.EscapeDataString(userId)}");
                _cache[userId] = r;
                return r;
            }, userId);
        }

        [ItemCanBeNull]
        public static Task<UserInfo> GetByUsername(string username) {
            // TODO: error handling
            var existing = _cache.Values.FirstOrDefault(x => x.Username == username);
            if (existing != null) {
                return Task.FromResult(existing);
            }

            return _tasks.Get(async () => {
                var r = await WorkshopHolder.Client.GetAsync<UserInfo[]>($"/users?username={Uri.EscapeDataString(username)}");
                if (r.Length == 1) {
                    _cache[r[0].UserId] = r[0];
                    return r[0];
                }
                return null;
            }, username);
        }
    }
}