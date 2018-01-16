using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        public class Session {
            internal Session() { }

            public bool IsActive { get; set; }

            /// <summary>
            /// Seconds.
            /// </summary>
            public long Duration { get; set; }

            public Game.SessionType Type { get; set; }

            public RaceMode RaceMode { get; set; }

            public int Inverted { get; set; }

            public string DisplayType => Type.GetDescription() ?? Type.ToString();

            private string _displayTypeShort;

            public string DisplayTypeShort => _displayTypeShort ?? (_displayTypeShort = DisplayType.Substring(0, 1));

            public string DisplayDuration {
                get {
                    switch (Type == Game.SessionType.Race ? RaceMode : RaceMode.Timed) {
                        case RaceMode.Laps:
                            return PluralizingConverter.PluralizeExt((int)Duration, ToolsStrings.Online_Session_LapsDuration);
                        case RaceMode.Timed:
                            return Duration.ToReadableTime();
                        case RaceMode.TimedExtra:
                            if (Inverted != 0) {
                                return $"{Duration.ToReadableTime()} (plus lap, plus inverted grid race)";
                            }

                            return $"{Duration.ToReadableTime()} (plus lap)";
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            protected bool Equals(Session other) {
                return IsActive == other.IsActive && Duration == other.Duration && Type == other.Type && Inverted == other.Inverted;
            }

            public override bool Equals(object obj) {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Session)obj);
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = IsActive.GetHashCode();
                    hashCode = (hashCode * 397) ^ Duration.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int)Type;
                    hashCode = (hashCode * 397) ^ Inverted;
                    return hashCode;
                }
            }
        }

        public class CarEntry : Displayable, IWithId {
            public string Id { get; }

            [CanBeNull]
            private AcItemWrapper _carObjectWrapper;

            [CanBeNull]
            public AcItemWrapper CarObjectWrapper {
                get => _carObjectWrapper;
                set {
                    if (Equals(value, _carObjectWrapper)) return;
                    _carObjectWrapper = value;
                    _availableSkinSet = false;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CarObject));
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(CarExists));
                    OnPropertyChanged(nameof(PreviewImage));
                    OnPropertyChanged(nameof(IsAvailable));
                }
            }

            public bool CarExists => CarObjectWrapper != null;

            [CanBeNull]
            public CarObject CarObject => (CarObject)CarObjectWrapper?.Loaded();

            private CarEntry(string carId, AcItemWrapper carObjectWrapper) {
                Id = carId;
                CarObjectWrapper = carObjectWrapper;
            }

            public CarEntry(string carId) : this(carId, CarsManager.Instance.GetWrapperById(carId)) {}

            public bool UpdateCarObject() {
                var co = CarsManager.Instance.GetWrapperById(Id);
                if (co != CarObjectWrapper) {
                    CarObjectWrapper = co;
                    return true;
                }

                return false;
            }

            private string _availableSkinId;

            [CanBeNull]
            public string AvailableSkinId {
                get => _availableSkinId;
                set {
                    if (Equals(value, _availableSkinId)) return;
                    _availableSkinId = value;
                    _availableSkinSet = false;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvailableSkin));
                    OnPropertyChanged(nameof(PreviewImage));

                    if (Total == 0 && value != null && CarObject != null) {
                        CarObject.SelectedSkin = AvailableSkin;
                    }
                }
            }

            private bool _availableSkinSet;
            private CarSkinObject _availableSkin;

            [CanBeNull]
            public CarSkinObject AvailableSkin {
                get {
                    if (!_availableSkinSet) {
                        _availableSkin = _availableSkinId == null ? null :
                                CarObject?.GetSkinById(_availableSkinId) ?? CarObject?.GetFirstSkinOrNull();
                        _availableSkinSet = true;
                    }

                    return _availableSkin;
                }
                set => AvailableSkinId = value?.Id;
            }

            public string PreviewImage => AvailableSkin?.PreviewImage ?? CarObject?.SelectedSkin?.PreviewImage;

            private int _total;

            public int Total {
                get => _total;
                set {
                    if (value == _total) return;
                    _total = value;
                    OnPropertyChanged();
                }
            }

            private int _available;

            public int Available {
                get => _available;
                set {
                    if (value == _available) return;
                    _available = value;
                    OnPropertyChanged();
                }
            }

            private bool _isAvailable;

            public bool IsAvailable {
                get => _isAvailable && CarExists;
                set {
                    if (Equals(value, _isAvailable)) return;
                    _isAvailable = value;
                    OnPropertyChanged();
                }
            }

            public override string DisplayName {
                get { return CarObjectWrapper?.Value.DisplayName ?? Id; }
                set { }
            }

            protected bool Equals(CarEntry other) {
                return Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj) {
                return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((CarEntry)obj));
            }

            public override int GetHashCode() {
                return Id.GetHashCode();
            }

            public override string ToString() {
                return DisplayName;
            }
        }

        public class CurrentDriver : NotifyPropertyChanged {
            private static readonly WeakList<CurrentDriver> Instances = new WeakList<CurrentDriver>();

            public string Name { get; }

            public string Team { get; }

            public string CarId { get; }

            public string CarSkinId { get; }

            public bool IsConnected { get; }

            public bool IsBookedForPlayer { get; }

            public CurrentDriver(ServerActualCarInformation x) {
                Name = x.DriverName;
                Team = x.DriverTeam;
                CarId = x.CarId;
                CarSkinId = x.CarSkinId;
                IsConnected = x.IsConnected;
                IsBookedForPlayer = x.IsRequestedGuid;

                Instances.Purge();
                Instances.Add(this);
            }

            public CarObject Car => _car ?? (_car = CarsManager.Instance.GetById(CarId));
            private CarObject _car;

            public CarSkinObject CarSkin => _carSkin ?? (_carSkin = CarSkinId != null ? Car?.GetSkinById(CarSkinId) : Car?.GetFirstSkinOrNull());
            private CarSkinObject _carSkin;

            internal void ResetCar() {
                _car = null;
                _carSkin = null;
                OnPropertyChanged(nameof(Car));
                OnPropertyChanged(nameof(CarSkin));
            }

            protected bool Equals(CurrentDriver other) {
                return string.Equals(Name, other.Name) && string.Equals(Team, other.Team) && string.Equals(CarId, other.CarId) &&
                        string.Equals(CarSkinId, other.CarSkinId) && IsConnected == other.IsConnected && IsBookedForPlayer == other.IsBookedForPlayer;
            }

            public override bool Equals(object obj) {
                return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((CurrentDriver)obj));
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = Name?.GetHashCode() ?? 0;
                    hashCode = (hashCode * 397) ^ (Team?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (CarId?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (CarSkinId?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ IsConnected.GetHashCode();
                    hashCode = (hashCode * 397) ^ IsBookedForPlayer.GetHashCode();
                    return hashCode;
                }
            }

            #region Groups
            private DriverTag[] _tags;

            public DriverTag[] Tags {
                get {
                    if (_tags == null) {
                        ReloadTags();
                    }
                    return _tags;
                }
                set {
                    if (Equals(value, _tags) || value.SequenceEqual(Tags)) return;
                    _tags = value;
                    DriverTag.SetTags(Name, value);
                    OnPropertyChanged();
                }
            }

            private void ReloadTags() {
                var data = DriverTag.GetTags(Name).ToArray();
                var initial = _tags == null;
                if (initial || !_tags.SequenceEqual(data)) {
                    _tags = data;

                    if (!initial) {
                        OnPropertyChanged(nameof(Tags));
                    }
                }
            }

            private string _convertedName;

            internal string ConvertedName => _convertedName ?? (_convertedName = DriverTag.ConvertName(Name, false));

            internal static void UpdateName(string convertedName) {
                Instances.Purge();
                foreach (var driver in Instances) {
                    if (driver.ConvertedName == convertedName) {
                        driver.ReloadTags();
                    }
                }
            }

            internal static void UpdateName(string convertedName, string removeTagId) {
                Instances.Purge();
                foreach (var driver in Instances) {
                    if (driver.ConvertedName == convertedName) {
                        driver._tags = driver.Tags.Where(x => x.Id != removeTagId).ToArray();
                        driver.OnPropertyChanged(nameof(Tags));
                    }
                }
            }

            internal static void UpdateName(string convertedName, DriverTag addTag) {
                Instances.Purge();
                foreach (var driver in Instances) {
                    if (driver.ConvertedName == convertedName) {
                        driver._tags = driver.Tags.Append(addTag).ToArray();
                        driver.OnPropertyChanged(nameof(Tags));
                    }
                }
            }

            private DelegateCommand<string> _addTagCommand;

            public DelegateCommand<string> AddTagCommand => _addTagCommand ?? (_addTagCommand = new DelegateCommand<string>(s => {
                if (s == null) return;
                if (Tags.GetByIdOrDefault(s) == null) {
                    Tags = Tags.Append(DriverTag.GetTag(s)).ToArray();
                }
            }));

            private DelegateCommand<string> _removeTagCommand;

            public DelegateCommand<string> RemoveTagCommand => _removeTagCommand ?? (_removeTagCommand = new DelegateCommand<string>(s => {
                Tags = Tags.Where(x => x.Id != s).ToArray();
            }));
            #endregion
        }

        public class DriverTag : NotifyPropertyChanged, IWithId {
            private const string KeyTagNamePrefix = "tagName/";
            private const string KeyTagColorPrefix = "tagColor/";
            private const string KeyTagsPrefix = "tags/";
            private const string KeyOriginalNamePrefix = "name/";
            private const string FriendTagId = "friend";

            public static readonly DriverTag FriendTag = new DriverTag(FriendTagId);

            private static readonly Storage TagsStorage = new Storage(FilesStorage.Instance.GetFilename("Progress", "Friends.data"));

            private static readonly Dictionary<string, DriverTag> Tags = new Dictionary<string, DriverTag> {
                [FriendTagId] = FriendTag
            };

            public static DriverTag GetTag(string tagId) {
                DriverTag result;
                if (!Tags.TryGetValue(tagId, out result)) {
                    result = new DriverTag(tagId);
                    Tags[tagId] = result;
                }

                return result;
            }

            public string Id { get; }

            private readonly string _keyName, _keyColor;

            private DriverTag(string id) {
                Id = id;
                _keyName = KeyTagNamePrefix + id;
                _keyColor = KeyTagColorPrefix + id;
            }

            private string DefaultName() {
                return Id == FriendTagId ? "Friend" : @"?";
            }

            private Color DefaultColor() {
                // See FavouriteBrush in FavouriteButton.xaml
                return Id == FriendTagId ? ((Application.Current.TryFindResource("FavouriteBrush") as SolidColorBrush)?.Color ?? Colors.Yellow)
                        : Colors.White;
            }

            private string _displayName;

            public string DisplayName {
                get => _displayName ?? (_displayName = TagsStorage.Get(_keyName, DefaultName()));
                set {
                    if (Equals(value, DisplayName)) return;
                    _displayName = value;
                    TagsStorage.Set(_keyName, value);
                    OnPropertyChanged();
                }
            }

            private Color? _color;

            public Color Color {
                get => _color ?? (_color = TagsStorage.Get(_keyColor, DefaultColor())).Value;
                set {
                    if (Equals(value, Color)) return;
                    _color = value;
                    TagsStorage.Set(_keyColor, value);
                    OnPropertyChanged();
                }
            }

            internal static string ConvertName(string original, bool save) {
                var converted = original.Trim().ToLower();
                if (save) {
                    TagsStorage.Set(KeyOriginalNamePrefix + converted, original);
                }

                return converted;
            }

            private static string GetKey([NotNull] string driverName, bool save) {
                if (driverName == null) throw new ArgumentNullException(nameof(driverName));
                return KeyTagsPrefix + ConvertName(driverName, save);
            }

            public static IEnumerable<DriverTag> GetTags() {
                yield return FriendTag;

                foreach (var storageKey in TagsStorage.Keys) {
                    if (storageKey.StartsWith(KeyTagNamePrefix)) {
                        var id = storageKey.Substring(KeyTagNamePrefix.Length);
                        if (id != FriendTagId) {
                            yield return GetTag(id);
                        }
                    }
                }
            }

            public static IEnumerable<DriverTag> GetTags(string driverName) {
                return TagsStorage.GetStringList(GetKey(driverName, false)).Select(GetTag);
            }

            public static void SetTags(string driverName, IReadOnlyList<DriverTag> tags) {
                TagsStorage.SetStringList(GetKey(driverName, true), tags.Select(x => x.Id));
                UpdateServerEntry(driverName, tags.Contains(FriendTag))?.RaiseDriversTagsChanged();
            }

            [CanBeNull]
            private static ServerEntry GetServerEntry(string driverName) {
                var convertedDriverName = ConvertName(driverName, false);
                return OnlineManager.Instance.List.FirstOrDefault(x => x.CurrentDrivers?.Any(y => y.ConvertedName == convertedDriverName) == true);
            }

            [CanBeNull]
            private static ServerEntry UpdateServerEntry(string driverName, bool hasFriendTag) {
                var server = GetServerEntry(driverName);
                if (server == null) return null;

                if (server.HasFriends) {
                    if (!hasFriendTag && server.CurrentDrivers?.Any(x => x.Tags.Contains(FriendTag)) != true) {
                        server.HasFriends = false;
                    }
                } else {
                    if (hasFriendTag) {
                        server.HasFriends = true;
                    }
                }
                return server;
            }

            public static DriverTag CreateTag(string name, Color color) {
                var existing = GetTags().Select(x => {
                    int v;
                    return int.TryParse(x.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : -1;
                }).Where(x => x >= 0).ToList();
                var id = existing.Count;
                while (existing.Contains(id)) {
                    id++;
                }

                var tag = GetTag(id.ToInvariantString());
                tag.DisplayName = name;
                tag.Color = color;

                return tag;
            }

            public static IEnumerable<string> GetNames(string tagId) {
                return from key in TagsStorage.Keys
                       where key.StartsWith(KeyTagsPrefix) && TagsStorage.GetStringList(key).Contains(tagId)
                       select key.Substring(KeyTagsPrefix.Length)
                       into converted
                       select TagsStorage.Get(KeyOriginalNamePrefix + converted, converted);
            }

            public static void SetNames(string tagId, IEnumerable<string> names) {
                var namesList = names.ToIReadOnlyListIfItIsNot();
                var namesConvertedList = namesList.Select(x => ConvertName(x, true)).ToList();
                var serversToRaiseEvent = new List<ServerEntry>(10);

                /* remove old entries */
                foreach (var key in TagsStorage.Keys.ToList()) {
                    if (key.StartsWith(KeyTagsPrefix)) {
                        var convertedName = key.Substring(KeyTagsPrefix.Length);
                        if (!namesConvertedList.Contains(convertedName)) {
                            var list = TagsStorage.GetStringList(key).ToList();
                            if (list.Remove(tagId)) {
                                TagsStorage.SetStringList(key, list);
                                CurrentDriver.UpdateName(convertedName, tagId);

                                var server = tagId == FriendTagId ? UpdateServerEntry(convertedName, false) : GetServerEntry(convertedName);
                                if (server != null && !serversToRaiseEvent.Contains(server)) {
                                    serversToRaiseEvent.Add(server);
                                }
                            }
                        }
                    }
                }

                if (tagId == FriendTagId) {
                    HasAnyFriends.Value = namesList.Count > 0;
                }

                /* add new entries */
                var tag = GetTag(tagId);
                foreach (var convertedName in namesConvertedList) {
                    var list = TagsStorage.GetStringList(KeyTagsPrefix + convertedName).ToList();
                    if (!list.Contains(tagId)) {
                        TagsStorage.SetStringList(KeyTagsPrefix + convertedName, list.Append(tagId));
                        CurrentDriver.UpdateName(convertedName, tag);

                        var server = tagId == FriendTagId ? UpdateServerEntry(convertedName, true) : GetServerEntry(convertedName);
                        if (server != null && !serversToRaiseEvent.Contains(server)) {
                            serversToRaiseEvent.Add(server);
                        }
                    }
                }

                foreach (var serverEntry in serversToRaiseEvent) {
                    serverEntry.RaiseDriversTagsChanged();
                }
            }

            public static void Remove(string tagId) {
                foreach (var key in TagsStorage.Keys.ToList()) {
                    if (key.StartsWith(KeyTagsPrefix)) {
                        var list = TagsStorage.GetStringList(key).ToList();
                        if (list.Remove(tagId)) {
                            TagsStorage.SetStringList(key, list);
                            var name = key.Substring(KeyTagsPrefix.Length);
                            CurrentDriver.UpdateName(name, tagId);
                        }
                    }
                }

                TagsStorage.Remove(tagId);
                TagsStorage.Remove(KeyTagNamePrefix + tagId);
                TagsStorage.Remove(KeyTagColorPrefix + tagId);
            }
        }

        public class HasAnyFriendsHolder : NotifyPropertyChanged {
            private const string KeyHasFriends = "_hasFriends";

            private bool _value = ValuesStorage.Get<bool>(KeyHasFriends);

            public bool Value {
                get => _value;
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                    ValuesStorage.Set(KeyHasFriends, value);
                }
            }
        }

        public static readonly HasAnyFriendsHolder HasAnyFriends = new HasAnyFriendsHolder();
    }
}

