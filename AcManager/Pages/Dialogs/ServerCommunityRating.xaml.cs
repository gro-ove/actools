using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Starters;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
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
            [JsonProperty("idx")]
            public int CommentIdx;

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

            [JsonProperty("likes")]
            public int Likes { get; set; }

            [JsonProperty("liked")]
            public bool Liked { get; set; }

            public int CustomFlag { get; set; }

            private AsyncCommand _toggleLikeCommand;

            public AsyncCommand ToggleLikeCommand => _toggleLikeCommand ?? (_toggleLikeCommand = new AsyncCommand(async () => {
                var newLiked = !Liked;
                try {
                    var result = await CmorProvider.PostAsync($"comment/{CommentIdx}/like", new JObject { ["liked"] = newLiked });
                    if (result.GetIntValueOnly("likes") is int likes) Likes = likes;
                    Liked = newLiked;
                    OnPropertyChanged(nameof(Liked));
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t like the comment", e);
                }
            }, () => !IsOwn));

            private AsyncCommand _reportCommand;

            public AsyncCommand ReportCommand => _reportCommand ?? (_reportCommand = new AsyncCommand(async () => {
                try {
                    if (ShowMessage("Report the comment? Message will be hidden, and with enough reports, deleted.", "Report comment", MessageBoxButton.YesNo)
                            == MessageBoxResult.Yes) {
                        await CmorProvider.PostAsync($"comment/{CommentIdx}/report", new JObject());
                        CustomFlag = 1;
                        OnPropertyChanged(nameof(CustomFlag));
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t report the comment", e);
                }
            }));

            private DelegateCommand _editCommand;

            public DelegateCommand EditCommand => _editCommand ?? (_editCommand = new DelegateCommand(() => {
                CustomFlag = 2;
                OnPropertyChanged(nameof(CustomFlag));
            }, () => IsOwn));

            [JsonProperty("own")]
            public bool IsOwn { get; set; }
        }

        public class ViewModel : NotifyPropertyChanged {
            public ServerEntry Entry { get; }

            private bool _loading = true;

            public bool Loading {
                get => _loading;
                set => Apply(value, ref _loading);
            }

            public ChangeableObservableCollection<CmorComment> Comments { get; } = new ChangeableObservableCollection<CmorComment>();

            public ViewModel(ServerEntry entry) {
                Entry = entry;
                RefreshAsync();
                Comments.ItemPropertyChanged += (sender, args) => {
                    if (args.PropertyName == nameof(CmorComment.CustomFlag)
                            && sender is CmorComment cc) {
                        if (cc.CustomFlag == 1) {
                            Comments.Remove(cc);
                        } else if (cc.CustomFlag == 2) {
                            cc.CustomFlag = 0;
                            RateCommand.ExecuteAsync(int.MaxValue).Ignore();
                        }
                    }
                };
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

            private static bool IsPublicIp(string ipString) {
                if (!IPAddress.TryParse(ipString, out var ip)) return true;
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    var bytes = ip.GetAddressBytes();
                    if (bytes[0] == 10) return false; // 10.0.0.0/8
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false; // 172.16.0.0/12
                    if (bytes[0] == 192 && bytes[1] == 168) return false; // 192.168.0.0/16
                    if (bytes[0] == 127) return false; // 127.0.0.0/8 (loopback)
                    if (bytes[0] == 169 && bytes[1] == 254) return false; // 169.254.0.0/16 (link-local)
                    if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return false; // 100.64.0.0/10 (CGNAT)
                    if (bytes[0] == 0) return false; // 0.0.0.0/8
                    if (bytes[0] >= 224) return false; // 224.0.0.0/4 (multicast) and 240.0.0.0/4 (reserved)
                    return true;
                }
                if (ip.AddressFamily == AddressFamily.InterNetworkV6) {
                    return false;
                }
                throw new ArgumentException("Unsupported address family", nameof(ipString));
            }

            private AsyncCommand<int> _rateCommand;

            public AsyncCommand<int> RateCommand => _rateCommand ?? (_rateCommand = new AsyncCommand<int>(async dir => {
                try {
                    if (!SteamStarter.Initialize(AcRootDirectory.Instance.RequireValue, true)) {
                        throw new InformativeException("Not available without Steam", 
                                "To be able to rate online servers, please enable “Connect to Steam API at launch” in CM settings/Integrated.");
                    }

                    var editMode = dir == int.MaxValue;
                    if (editMode) dir = Entry.OwnVote;
                    if (!editMode && Entry.OwnVote == dir) return;

                    if (Entry.Ip.Any(x => !char.IsLetterOrDigit(x) && x != '.' && x != '_' && x != '-')
                            || !IsPublicIp(Entry.Ip)) {
                        throw new InformativeException("Can’t rate this server", "Community server rating is not currently available for this server.");
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
                        var comment = (await Prompt.ShowAsync(
                                editMode ? "Edit comment, or erase it to remove (but leave the vote):" : "Add an optional comment to your vote:",
                                editMode ? "Edit the comment" : "Cast a vote",
                                placeholder: "Your comment", defaultValue: prevComment.Value,
                                comment: editMode
                                        ? $"You’re editing the comment of your {(dir > 0 ? "positive" : "negative")} vote."
                                        : $"You’re about to cast a {(dir > 0 ? "positive" : "negative")} vote. To help others further, " +
                                                "consider leaving a comment. Please keep things civil though.",
                                multiline: true, maxLength: 800, extraContent: anonymousCheckbox))?.Trim();
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