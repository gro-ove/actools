using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AcManager.Workshop.Providers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class WorkshopCollabModel : NotifyPropertyChanged {
        [JsonProperty("collabs")]
        public ChangeableObservableCollection<WorkshopCollabReference> References { get; } = new ChangeableObservableCollection<WorkshopCollabReference>();

        public WorkshopCollabModel() {
            References.ItemPropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(WorkshopCollabReference.DeleteCommand)) {
                    References.Remove(sender as WorkshopCollabReference);
                }
            };
        }

        private string _userRole;

        [JsonProperty("mainUserRole")]
        public string UserRole {
            get => _userRole;
            set => Apply(value, ref _userRole);
        }

        private bool IsAllowed(string username) {
            return username != WorkshopHolder.Model.AuthorizedAs?.Username
                    && References.All(y => y.UserInfo.Value?.Username != username);
        }

        private string _lastFullSuggestionsValue;
        private List<string> _lastFullSuggestions;

        private async Task<IEnumerable<string>> GetSuggestionsAsync(string value) {
            if (_lastFullSuggestionsValue?.StartsWith(value) == true) {
                return _lastFullSuggestions.ToList();
            }

            var partial = false;
            var data = (await WorkshopHolder.Client.GetAsync<string[]>($"/user-suggestions/{value}",
                    (statusCode, headers) => { partial = statusCode == HttpStatusCode.PartialContent; })).Where(IsAllowed).ToList();
            if (!partial) {
                _lastFullSuggestionsValue = value;
                _lastFullSuggestions = data;
            }
            return data;
        }

        private AsyncCommand _addReferenceCommand;

        public AsyncCommand AddReferenceCommand => _addReferenceCommand ?? (_addReferenceCommand = new AsyncCommand(async () => {
            try {
                _lastFullSuggestionsValue = null;
                var username = Prompt.Show("Enter username:", "Add collaborator",
                        suggestionsCallback: GetSuggestionsAsync,
                        verificationCallback: async value => !IsAllowed(value) ? "This author is already added"
                                : await UserInfoProvider.GetByUsername(value) != null ? null : "No user with such username",
                        suggestionsAsList: true);
                if (username == null) return;

                if (!IsAllowed(username)) {
                    throw new Exception("This author is already added");
                }

                var userInfo = await UserInfoProvider.GetByUsername(username);
                if (userInfo == null) {
                    throw new Exception("No user with such username");
                }

                References.Add(new WorkshopCollabReference { UserId = userInfo.UserId });
            } catch (Exception e) {
                NonfatalError.Notify("Failed to add collaborator", e);
            }
        }));
    }
}