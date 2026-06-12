using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Starters;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.Dialogs {
    public partial class ServerCommunityRating {
        public ServerCommunityRating(ServerEntry entry) {
            DataContext = new ViewModel(entry);
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        [JsonObject(MemberSerialization.OptIn)]
        public class CmorComment : NotifyPropertyChanged {
            [JsonProperty("username"), CanBeNull]
            public string Username { get; set; }

            [JsonProperty("avatar"), CanBeNull]
            public string Avatar { get; set; }

            [JsonProperty("date")]
            public long DateTimestamp { get; set; }

            public DateTime Date => DateTimestamp.ToDateTime();

            [JsonProperty("comment")]
            public string Comment { get; set; }

            public string DisplayStats {
                get {
                    if (string.IsNullOrEmpty(Stats)) return null;
                    var pieces = Stats.Split('/');
                    var ret = $"Drove {pieces[0]} {(pieces[0] == "1" ? "time" : "times")}";
                    if (pieces[1] != @"0.0") ret += $", {pieces[1]} km";
                    if (pieces[2] != @"0.0") ret += $", {pieces[2]} hours";
                    return ret;
                }
            }

            [JsonProperty("stats"), CanBeNull]
            public string Stats { get; set; }

            [JsonProperty("vote")]
            public int Vote { get; set; }

            [JsonProperty("own")]
            public bool Own { get; set; }
        }

        public class ViewModel : NotifyPropertyChanged {
            public ServerEntry Entry { get; }

            private bool _loading = true;

            public bool Loading {
                get => _loading;
                set => Apply(value, ref _loading);
            }

            public BetterObservableCollection<CmorComment> Comments { get; } = new BetterObservableCollection<CmorComment>();

            public ViewModel(ServerEntry entry) {
                Entry = entry;
                RefreshAsync();
            }

            private void ApplyFreshData(JObject data) {
                Entry.VotesUp = data.GetIntValueOnly("likes") ?? 0;
                Entry.VotesDown = data.GetIntValueOnly("dislikes") ?? 0;
                Entry.OwnVote = data.GetIntValueOnly("ownVote") ?? 0;
                Comments.ReplaceEverythingBy_Direct(data["comments"]?.ToObject<CmorComment[]>() ?? new CmorComment[0]);
            }

            private async void RefreshAsync() {
                _loading = true;
                try {
                    var serverId = BitConverter.GetBytes(Entry.Id64).ToCutBase64Url();
                    var data = await CmorProvider.GetAsync($"rate/{serverId}", false);
                    ApplyFreshData(data);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Failed to refresh comments", e);
                } finally {
                    Loading = false;
                }
            }

            private AsyncCommand<int> _rateCommand;

            public AsyncCommand<int> RateCommand => _rateCommand ?? (_rateCommand = new AsyncCommand<int>(async dir => {
                try {
                    if (!SteamStarter.Initialize(MainExecutingFile.Directory, true)) {
                        throw new InformativeException("Not available without Steam", "For community server rating to work, enable Steam integration in CM settings.");
                    }

                    if (Entry.OwnVote == dir) return;
                    if (Entry.Ip.Any(x => !char.IsLetterOrDigit(x) && x != '.' && x != '_' && x != '-')
#if !DEBUG
                            || Entry.GetReferencesIds().All(x => x != KunosOnlineSource.Key)
#endif
                            || Entry.Ip.StartsWith(@"192.") || Entry.Ip.StartsWith(@"127.")) {
                        throw new InformativeException("Can’t rate this server", "Community server rating is available only for servers listed in Kunos lobby.");
                    }

                    var serverId = BitConverter.GetBytes(Entry.Id64).ToCutBase64Url();
                    var body = new JObject { ["vote"] = dir, ["serverName"] = Entry.DisplayName };
                    if (dir != 0) {
                        var prevComment = Stored.Get($".cmor.{serverId}.c", "");
                        var prevAnonymous = Stored.Get($".cmor.{serverId}.a", false);
                        var anonymousCheckbox = new CheckBox {
                            Content = new Label { Content = "Post anonymously" },
                            IsChecked = prevAnonymous.Value,
                            Margin = new Thickness(0d, 12d, 0d, 0d)
                        };
                        var comment = (await Prompt.ShowAsync("Add an optional comment to your vote:",
                                "Cast a vote", placeholder: "Your comment", defaultValue: prevComment.Value,
                                comment: $"You’re about to cast a {(dir > 0 ? "positive" : "negative")} vote. To help others further, " +
                                        "consider leaving a comment. Please keep things civil though.",
                                multiline: true, maxLength: 4000, extraContent: anonymousCheckbox))?.Trim();
                        if (comment == null) return;
                        prevComment.Value = comment;
                        prevAnonymous.Value = anonymousCheckbox.IsChecked == true;
                        if (!string.IsNullOrEmpty(comment)) {
                            body[@"comment"] = comment;
                        }
                        if (Entry.Stats.SessionsCount > 0) {
                            body[@"stats"] = $"{Entry.Stats.SessionsCount}/{Entry.Stats.DistanceKm:F1}/{Entry.Stats.Time.TotalHours:F1}";
                        }
                        if (anonymousCheckbox.IsChecked == true) {
                            body["anonymous"] = true;
                        }
                    }
                    ApplyFreshData(await CmorProvider.PostAsync($"rate/{serverId}", body));
                } catch (Exception ex) {
                    NonfatalError.Notify("Can’t post a comment", ex);
                }
            }, dir => Entry.OwnVote != dir));
        }
    }
}