using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public class OnlineItem : Panel {
        private readonly BbCodeBlock _name;

        static OnlineItem() {
            AppearanceManager.Instance.ThemeChange += OnThemeChange;
            AppearanceManager.Instance.PropertyChanged += OnThemeChange;
            FileBasedOnlineSources.Instance.Update += OnUserSourcesUpdated;
            FileBasedOnlineSources.Instance.LabelUpdate += OnUserSourcesUpdated;
        }

        private static void OnUserSourcesUpdated(object sender, EventArgs e) {
            CustomIcons.Clear();
            UpdateAll(s => {
                s._origins = null;
                s.UpdateReferences(s._server);
            });
        }

        private static void UpdateAll(Action<OnlineItem> callback) {
            foreach (var item in VisualTreeHelperEx.GetAllOfType<OnlineItem>()) {
                callback(item);
                item.Render();
            }
        }

        private static void OnThemeChange(object sender, EventArgs e) {
            OnlineResources.Reset();
            ResetResources();
            ResetIcons();
            CarsCache.Clear();
            SessionsCache.Clear();
        }

        public OnlineItem() {
            Height = 60d;
            Background = new SolidColorBrush(Colors.Transparent);
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);

            _name = new BbCodeBlock {
                FontSize = 16d,
                FontWeight = FontWeights.Normal,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(22, 2, 116, 32),
                Mode = EmojiSupport.WithoutBbCodes
            };

            Children.Add(_name);
        }

        private static bool _scrolling;

        private static readonly Dictionary<string, Tuple<FormattedText, bool>> SessionsCache = new Dictionary<string, Tuple<FormattedText, bool>>(8);
        private static readonly Dictionary<string, ObjToRender> CarsCache = new Dictionary<string, ObjToRender>(100);

        public static void SetScrolling(bool value) {
            if (value == _scrolling) return;
            _scrolling = value;
            if (!value) {
                CarsCache.Clear();
            }
        }

        private static long _toolTipId;
        private static string _toolTipKey;
        private static int _toolTipOwner;
        private static ToolTip _toolTip;
        private static DateTime _lastClosed;

        private async void ShowToolTip([CanBeNull] string key, [CanBeNull] Func<object> contentFn, [CanBeNull] Func<object> contextFn = null) {
            var owner = GetHashCode();
            if (_toolTipKey == key && _toolTipOwner == owner) {
                return;
            }

            var id = ++_toolTipId;
            if (key == null) {
                if (owner == _toolTipOwner) {
                    _toolTipKey = null;
                    if (_toolTip != null) {
                        _toolTip.IsOpen = false;
                        _toolTip.Content = null;
                        _toolTip = null;
                        _lastClosed = DateTime.Now;
                    }
                }

                return;
            }

            _toolTipOwner = owner;
            _toolTipKey = key;

            if (_toolTip != null) {
                _toolTip.IsOpen = false;
                _toolTip.Content = null;
                _toolTip = null;
                // _lastClosed = DateTime.Now;
            }

            _toolTip = new ToolTip();

            if (contentFn != null) {
                if (DateTime.Now - _lastClosed > TimeSpan.FromMilliseconds(300)) {
                    await Task.Delay(300);
                }

                if (id == _toolTipId && _toolTip != null) {
                    var content = contentFn();
                    if (content is ToolTip toolTip) {
                        var child = toolTip.Content;
                        if (child is FrameworkElement fe) {
                            fe.DataContext = contextFn?.Invoke() ?? _server;
                        }
                        toolTip.Content = null;
                        _toolTip.Content = child;
                    } else {
                        _toolTip.Content = content is string s ? new BbCodeBlock {
                            Text = s,
                            MaxWidth = 400
                        } : content;
                    }
                    _toolTip.IsOpen = true;
                }
            }
        }

        private static void ShowContextMenu([CanBeNull] ContextMenu menu) {
            if (menu != null) {
                menu.IsOpen = true;
            }
        }

        protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e) {
            if (_scrolling) return;

            var size = _size;
            var width = size.Width;

            var pos = this.GetMousePosition();
            var handled = e.Handled;
            e.Handled = true;

            if (!_fullyLoaded) return;

            if (GetTrackRect().Contains(pos)) {
                ShowContextMenu(_currentTrack == null ? null : ContextMenus.GetTrackContextMenu(null, _currentTrack));
                return;
            }

            var carsOffset = _wideMode ? 324d : 240d;
            if (GetCarsRect(carsOffset, width).Contains(pos)) {
                var cars = _cars;
                for (var i = 0; i < cars.Length; i++) {
                    var c = cars[i];
                    if (c == null || carsOffset > width) break;

                    var rect = new Rect(carsOffset, 34, c.Width + 8, 22);
                    if (rect.Contains(pos)) {
                        var car = _server.Cars?.ElementAtOrDefault(i);
                        ShowContextMenu(car?.CarObject == null ? null : ContextMenus.GetCarContextMenu(null, car.CarObject, car.AvailableSkin));
                        return;
                    }
                    carsOffset += rect.Width + 8;
                }
            }

            e.Handled = handled;
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            if (_scrolling) return;

            var size = _size;
            var width = size.Width;

            var pos = this.GetMousePosition();

            var referenceIconOffset = 22d;
            for (var i = 0; i < _icons.Length; i++) {
                var icon = _icons[i];
                if (icon == null) break;

                var iconWidth = icon.Width;
                var rect = new Rect(referenceIconOffset, 5, iconWidth, ReferenceIconSize);
                if (rect.Contains(pos)) {
                    ShowToolTip($@"ref:{i}", () => _iconToolTips.ArrayElementAtOrDefault(i));
                    return;
                }
                referenceIconOffset += iconWidth + ReferenceIconMargin;
            }

            if (!_fullyLoaded) return;

            var pieces = (_bookedForPlayer ? 1 : 0) + (_passwordRequired ? 1 : 0) + (_hasFriends ? 1 : 0);
            if (pieces < 3 && (_requiresCspAvailable || _requiresCspMissing)) {
                ++pieces;
            }
            var iconOffset = (60d - pieces * ReferenceIconHeight + (pieces - 1d) * ReferenceIconMargin) / 2d;
            if (pieces > 0) {
                const double iconHeight = 12d;
                const double iconMargin = 4d;
                if (_bookedForPlayer) {
                    if (new Rect(6, iconOffset, 12, iconHeight).Contains(pos)) {
                        ShowToolTip(@"iconBooked", () => "There is a place booked for you");
                        return;
                    }
                    iconOffset += iconHeight + iconMargin;
                }
                if (_passwordRequired) {
                    if (new Rect(6, iconOffset, 12, iconHeight).Contains(pos)) {
                        ShowToolTip(@"iconPassword", () => ControlsStrings.Online_PasswordRequired);
                        return;
                    }
                    iconOffset += iconHeight + iconMargin;
                }
                if (_hasFriends) {
                    if (new Rect(6, iconOffset, 12, iconHeight).Contains(pos)) {
                        ShowToolTip(@"iconFriends", () => "Your friend is here");
                        return;
                    }
                    iconOffset += iconHeight + iconMargin;
                }

                if (_requiresCspAvailable || _requiresCspMissing) {
                    if (new Rect(6, iconOffset, 12, iconHeight).Contains(pos)) {
                        ShowToolTip(@"iconCsp", () => _cspProhibited
                                ? "Custom Shaders Patch is not available on this server"
                                : "Custom Shaders Patch is required on this server");
                        return;
                    }
                }
            }

            if (new Rect(width - 80, 0, 40, 30).Contains(pos)) {
                ShowToolTip(@"clients", () => FindStaticResource<ToolTip>(@"ClientsTooltip"));
                return;
            }

            if (new Rect(width - 40, 0, 40, 30).Contains(pos)) {
                if (_errorFlag) {
                    ShowToolTip(@"error", () => _server.ErrorsString?.Trim());
                } else {
                    ShowToolTip(@"ping", () => $"Ping: {(_server.Ping.HasValue ? _server.Ping + " ms" : "not checked yet")}");
                }
                return;
            }

            if (GetCountryRect(width).Contains(pos)) {
                ShowToolTip(@"country", () => _server.Country);
                return;
            }

#if DEBUG
            if (new Rect(0, 0, width, 30).Contains(pos)) {
                ShowToolTip(@"name", () => $"Actual name: {BbCodeBlock.Encode(_server.ActualName)}\nSorting name: {_server.SortingName}");
                return;
            }
#endif

            if (GetTrackRect().Contains(pos)) {
                ShowToolTip(@"track", () => FindStaticResource<ToolTip>(@"TrackPreviewTooltip.Online"));
                return;
            }

            if (new Rect(146d, 35, _sessionsCount * 20 + (_wideMode ? 84d : 0), 20).Contains(pos)) {
                ShowToolTip(@"sessions", () => FindStaticResource<ToolTip>(@"SessionsItemTooltip"));
                return;
            }

            var carsOffset = _wideMode ? 324d : 240d;
            if (GetCarsRect(carsOffset, width).Contains(pos)) {
                var cars = _cars;
                for (var i = 0; i < cars.Length; i++) {
                    var c = cars[i];
                    if (c == null || carsOffset > width) break;

                    var rect = new Rect(carsOffset, 34, c.Width + 8, 22);
                    if (rect.Contains(pos)) {
                        ShowToolTip($@"car:{i}", () => FindStaticResource<ToolTip>(@"CarPreviewTooltip.Online"),
                                () => _server.Cars?.ElementAtOrDefault(i));
                        return;
                    }
                    carsOffset += rect.Width + 8;
                }
            }

            ShowToolTip(null, null);
        }

        private static Rect GetCarsRect(double carsOffset, double width) {
            return new Rect(carsOffset, 34, width - carsOffset, 22);
        }

        private static Rect GetTrackRect() {
            return new Rect(20, 30, 120, 30);
        }

        protected override void OnMouseLeave(MouseEventArgs e) {
            ShowToolTip(null, null);
        }

        public static readonly DependencyProperty ServerProperty = DependencyProperty.Register(nameof(Server), typeof(ServerEntry),
                typeof(OnlineItem), new PropertyMetadata(null, (o, e) => ((OnlineItem)o).SetServer((ServerEntry)e.NewValue)));

        private bool _dirty;
        private ServerEntry _server;

        public ServerEntry Server {
            get => _server;
            set => SetValue(ServerProperty, value);
        }

        private void SetServer(ServerEntry server) {
            OnServerChanged(_server, server);
            _server = server;
            _dirty = true;
            Render();
            RenderTimeLeft();
        }

        private static Style _labelStyle;
        private static Brush _areaBrush;
        private static Pen _cspProhibitedPen;
        private static Typeface _typeface, _labelTypeface;
        private static Brush _blockText, _blockTextActive, _blockBackground, _blockBackgroundActive;

        private static BitmapSource _alertIcon, _passwordIcon, _bookedIcon, _friendsIcon,
                _cspIconAvailable, _cspIconMissing;

        private static TextFormattingMode _formattingMode;
        private static Brush _foreground, _hint;

        private void InitializeResources() {
            if (_labelStyle != null) return;

            _labelStyle = FindStaticResource<Style>(@"Label");
            _areaBrush = FindStaticResource<Brush>(@"SlightBackgroundHint");
            _cspProhibitedPen = new Pen(FindStaticResource<Brush>(@"Error"), 1.5d);
            _typeface = new Typeface(new FontFamily(@"Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _labelTypeface = new Typeface(
                    new FontFamily(new Uri(@"pack://application:,,,/FirstFloor.ModernUI;component/Fonts/#Segoe Condensed", UriKind.Absolute),
                            @"Segoe Condensed Bold"),
                    FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

            _blockText = FindStaticResource<Brush>(@"ButtonText");
            _blockTextActive = FindStaticResource<Brush>(@"ButtonTextPressed");
            _blockBackground = FindStaticResource<Brush>(@"ButtonBackground");
            _blockBackgroundActive = FindStaticResource<Brush>(@"ButtonBackgroundPressed");

            _alertIcon = ToBitmap(@"AlertIconData", @"Error");
            _passwordIcon = ToBitmap(@"LockIconData", @"WindowText");
            _bookedIcon = ToBitmap(@"TriangleFlagIconData", @"Go");
            _friendsIcon = ToBitmap(@"MultiplePeopleIconData", @"WindowText");
            _cspIconAvailable = ToBitmap(@"BulbIconData", @"WindowText");
            _cspIconMissing = ToBitmap(@"BulbIconData", @"Error");

            _foreground = (Brush)GetValue(TextBlock.ForegroundProperty);
            _hint = _foreground is SolidColorBrush solidColorBrush ? GetHalfTransparent(solidColorBrush) : _foreground;
            _formattingMode = GetValue(TextOptions.TextFormattingModeProperty) as TextFormattingMode? ?? TextFormattingMode.Display;
        }

        private static void ResetResources() {
            _labelStyle = null;
            _pingPostfix = null;
        }

        private void Update() {
            if (!_dirty) return;
            _dirty = false;

            InitializeResources();
            InitializeStatic();

            var server = _server;
            UpdateName(server);
            UpdateReferences(server);
            UpdateFullyLoaded(server);
            UpdatePasswordState(server);
            UpdateBookedState(server);
            UpdateFriendsState(server);
            UpdateCspStateAvailable(server);
            UpdateCspStateMissing(server);
            UpdateCspStateProhibited(server);
            UpdatePing(server);
            UpdateTimeLeft(server);
            UpdateDisplayClients(server);
            UpdateCountryFlag(server);
            UpdateErrorFlag(server);
            UpdateTrack(server);
            UpdateCars(server);
            UpdateSessions(server);
        }

        private const int MaxReferenceIcons = 6;
        private const int MaxCarsCount = 10;
        private const int MaxSessionsCount = 4;

        private static double _textHeight, _textPadding;

        [CanBeNull]
        private static FormattedText _pingPostfix;

        [CanBeNull]
        private FormattedText _displayClients, _pingValue, _timeLeft;

        [CanBeNull]
        private ObjToRender _track;

        [CanBeNull]
        private TrackObjectBase _currentTrack;

        [CanBeNull]
        private string _currentTrackId;

        private ImageSource _countryBitmap;

        private bool _errorFlag, _fullyLoaded = true, _passwordRequired, _bookedForPlayer, _hasFriends,
                _requiresCspAvailable, _requiresCspMissing, _cspProhibited;

        private int _sessionsCount;

        private class ObjToRender {
            [CanBeNull]
            public readonly ImageSource Icon;

            [CanBeNull]
            public readonly FormattedText Text;

            [CanBeNull]
            public readonly ImageSource TextImage;

            public double Width, TextOffset;

            public ObjToRender([CanBeNull] ImageSource icon, [CanBeNull] string text, double maxWidth = double.NaN, bool isBlock = false,
                    bool? isComplex = null) {
                Icon = icon;
                TextOffset = icon?.Width + 4 ?? 0d;
                if (isComplex ?? IsComplex(text)) {
                    TextImage = ToBitmap(GetTextBlock(text, isBbCode: true, isBlock: true), double.IsNaN(maxWidth) ? 0 : maxWidth.RoundToInt(), 14);
                    Width = TextOffset + (TextImage?.Width ?? 0d);
                } else {
                    Text = GetText(text, isBlock: isBlock, maxWidth: maxWidth);
                    Width = TextOffset + (Text?.Width ?? 0d);
                }
            }
        }

        [NotNull]
        private readonly ObjToRender[] _cars = new ObjToRender[MaxCarsCount];

        [NotNull]
        private readonly Tuple<FormattedText, bool>[] _sessions = new Tuple<FormattedText, bool>[MaxSessionsCount];

        private static void InitializeStatic() {
            if (_pingPostfix == null) {
                _pingPostfix = GetText(ControlsStrings.Common_MillisecondsPostfix ?? string.Empty, isHint: true);
                _textHeight = _pingPostfix.Height;
                _textPadding = (30 - _textHeight) / 2;
            }
        }

        public static readonly DependencyProperty HideSourceIconsProperty = DependencyProperty.Register(nameof(HideSourceIcons), typeof(string[]),
                typeof(OnlineItem), new PropertyMetadata(null, (o, e) => ((OnlineItem)o)._hideSourceIcons = (string[])e.NewValue));

        [CanBeNull]
        private string[] _hideSourceIcons;

        /// <summary>
        /// ID of the source which icon is should be hidden.
        /// </summary>
        public string[] HideSourceIcons {
            get => _hideSourceIcons;
            set => SetValue(HideSourceIconsProperty, value);
        }

        private static readonly Lazier<BitmapSource> MinoratingIcon = Lazier.Create(() => ToBitmap(@"MinoratingIcon", 0, ReferenceIconSize));
        private static readonly Lazier<BitmapSource> LanIcon = Lazier.Create(() => ToBitmap(@"LanIcon", ReferenceIconSize));
        private static readonly Lazier<BitmapSource> FavouritesIcon = Lazier.Create(() => ToBitmap(@"FavouriteIcon", ReferenceIconSize));
        private static readonly Lazier<BitmapSource> HiddenIcon = Lazier.Create(() => ToBitmap(@"HiddenIcon", ReferenceIconSize));
        private static readonly Lazier<BitmapSource> RecentIcon = Lazier.Create(() => ToBitmap(@"RecentIcon", ReferenceIconSize));
        private static readonly Dictionary<string, Tuple<BitmapSource, string>> CustomIcons = new Dictionary<string, Tuple<BitmapSource, string>>();

        private static void ResetIcons() {
            MinoratingIcon.Reset();
            LanIcon.Reset();
            FavouritesIcon.Reset();
            HiddenIcon.Reset();
            RecentIcon.Reset();
            CustomIcons.Clear();
        }

        [CanBeNull]
        private BitmapSource GetReferenceIcon(string originId, out string toolTip) {
            if (_hideSourceIcons != null && Array.IndexOf(_hideSourceIcons, originId) != -1) {
                toolTip = null;
                return null;
            }

            switch (originId) {
                case FileBasedOnlineSources.FavouritesKey:
                    toolTip = "Favourite";
                    return FavouritesIcon.Value;
                case FileBasedOnlineSources.HiddenKey:
                    toolTip = "Hidden";
                    return HiddenIcon.Value;
                case FileBasedOnlineSources.RecentKey:
                    toolTip = "Recently used";
                    return RecentIcon.Value;
                case MinoratingOnlineSource.Key:
                    toolTip = "Minorating";
                    return MinoratingIcon.Value;
                case LanOnlineSource.Key:
                    toolTip = "LAN";
                    return LanIcon.Value;
                case KunosOnlineSource.Key:
                    toolTip = null;
                    return null;
                default:
                    if (!CustomIcons.TryGetValue(originId, out var result)) {
                        var information = FileBasedOnlineSources.Instance.GetInformation(originId);
                        if (string.IsNullOrWhiteSpace(information?.Label)) {
                            toolTip = null;
                            return null;
                        }

                        var baseIcon = (Decorator)TryFindResource(@"BaseIcon");
                        if (baseIcon == null) {
                            toolTip = null;
                            return null;
                        }

                        var text = (BbCodeBlock)baseIcon.Child;
                        text.Text = information.Label;
                        text.Foreground = new SolidColorBrush(information.Color ?? Colors.White);
                        text.ForceUpdate();
                        result = Tuple.Create(ToBitmap(baseIcon, 0, ReferenceIconSize), originId.ToTitle());
                        CustomIcons[originId] = result;
                    }

                    toolTip = result.Item2;
                    return result.Item1;
            }
        }

        private string _origins;

        private void UpdateName(ServerEntry server) {
            _name.Text = server.DisplayName;
        }

        private readonly BitmapSource[] _icons = new BitmapSource[MaxReferenceIcons];
        private readonly string[] _iconToolTips = new string[MaxReferenceIcons];
        private int _iconsCount;

        private const int ReferenceIconSize = 18;
        private const int ReferenceIconHeight = 12;
        private const int ReferenceIconMargin = 4;

        private void UpdateReferences(ServerEntry server) {
            if (server.ReferencesString != _origins) {
                _origins = server.ReferencesString;

                var list = server.GetReferencesIds();
                var index = 0;
                var totalWidth = 0d;

                for (var i = 0; i < list.Count && index < MaxReferenceIcons; i++) {
                    var icon = GetReferenceIcon(list[i], out var toolTip);
                    if (icon != null) {
                        _icons[index] = icon;
                        _iconToolTips[index++] = toolTip;
                        totalWidth += icon.Width + ReferenceIconMargin;
                    }
                }

                _iconsCount = index;
                for (; index < MaxReferenceIcons; index++) {
                    _icons[index] = null;
                }

                _name.Padding = new Thickness(_iconsCount > 0 ? 4 + totalWidth : 0, 0, 0, 0);
                Render();
            }
        }

        private void UpdateFullyLoaded(ServerEntry n) {
            if (_fullyLoaded == n.IsFullyLoaded) return;
            _fullyLoaded = n.IsFullyLoaded;
            _name.FontStyle = _fullyLoaded ? FontStyles.Normal : FontStyles.Italic;
        }

        private void UpdatePasswordState(ServerEntry n) {
            _passwordRequired = n.PasswordRequired;
        }

        private void UpdateBookedState(ServerEntry n) {
            _bookedForPlayer = n.IsBookedForPlayer;
        }

        private void UpdateFriendsState(ServerEntry n) {
            _hasFriends = n.HasFriends;
        }

        private void UpdateCspStateAvailable(ServerEntry n) {
            _requiresCspAvailable = n.CspRequiredAvailable;
        }

        private void UpdateCspStateMissing(ServerEntry n) {
            _requiresCspMissing = n.CspRequiredMissing;
        }

        private void UpdateCspStateProhibited(ServerEntry n) {
            _cspProhibited = n.RequiredCspVersion == PatchHelper.NonExistentVersion;
        }

        private void UpdateDisplayClients(ServerEntry server) {
            _displayClients = GetText(server.DisplayClients, TextAlignment.Center, 32d);
        }

        private void UpdatePing(ServerEntry server) {
            _pingValue = GetText(server.Ping?.ToString() ?? @"?");
        }

        private void UpdateTimeLeft(ServerEntry server) {
            _timeLeft = GetText(server.DisplayTimeLeft);
        }

        private void UpdateCountryFlag(ServerEntry server) {
            try {
                _countryBitmap = CountryIcon.LoadEntryAsync(server.CountryId, 24).Result.ImageSource;
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void UpdateErrorFlag(ServerEntry server) {
            _errorFlag = server.HasErrors || !server.IsFullyLoaded;
        }

        private void UpdateTrack(ServerEntry server) {
            var trackId = server.TrackId;
            if (ReferenceEquals(_currentTrackId, trackId)) return;

            var track = server.Track;
            _currentTrack = track;
            _currentTrackId = trackId;

            if (server.TrackId == null) {
                _track = new ObjToRender(null, "No information", isBlock: false, isComplex: false, maxWidth: 120);
            } else if (track == null) {
                _track = new ObjToRender(_alertIcon, server.TrackId, isBlock: false, isComplex: false, maxWidth: 104);
            } else {
                /*var src = BetterImage.LoadBitmapSource(track.OutlineImage, 32, 32).BitmapSource;
                _track = new ObjToRender(ToBitmap(new BetterImage {
                    Source = src,
                    CropTransparentAreas = true
                }), track.Name, isBlock: false, maxWidth: src == null ? 104 : 120);*/
                _track = new ObjToRender(null, track.Name, isBlock: false, maxWidth: 120);
            }
        }

        private static TextBlock GetTextBlock(string text, bool isBlock = false, bool isBbCode = false) {
            TextBlock result;

            if (isBbCode) {
                var bb = new BbCodeBlock {
                    Text = text,
                    Mode = EmojiSupport.WithoutBbCodes,
                    HighlightUrls = false,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                bb.ForceUpdate();
                result = bb;
            } else {
                result = new TextBlock { Text = text };
            }

            result.FontSize = 11;
            result.Foreground = isBlock ? _blockText : _foreground;

            RenderOptions.SetBitmapScalingMode(result, BitmapScalingMode.HighQuality);
            TextOptions.SetTextRenderingMode(result, TextRenderingMode.ClearType);
            TextOptions.SetTextFormattingMode(result, TextFormattingMode.Display);
            TextOptions.SetTextHintingMode(result, TextHintingMode.Fixed);
            return result;
        }

        private static bool IsComplex([CanBeNull] string name) {
            if (name == null) return false;
            for (var i = name.Length - 1; i >= 0; i--) {
                if (name[i] >= 0x203c) return true;
            }
            return false;
        }

        private static ObjToRender GetCarToRender([NotNull] ServerEntry.CarEntry carEntry) {
            if (CarsCache.TryGetValue(carEntry.Id, out var result)) return result;

            var car = carEntry.CarObject;
            if (car != null) {
                var badges = SettingsHolder.Online.ShowBrandBadges;

                ImageSource icon;
                try {
                    icon = badges ? CarIcon.GetCached(car.Brand, 12).ImageSource
                            ?? CarIcon.LoadEntryAsync(car.Brand, 12, car.BrandBadge).Result.ImageSource : null;
                } catch (Exception e) {
                    icon = null;
                    Logging.Error(e);
                }

                var name = badges ? car.ShortName : car.DisplayName;
                result = new ObjToRender(icon, name, isBlock: true);
            } else {
                result = new ObjToRender(_alertIcon, carEntry.Id, isBlock: true, isComplex: false);
            }

            return CarsCache[carEntry.Id] = result;
        }

        private void UpdateCars(ServerEntry server) {
            var c = server.Cars;
            var i = 0;
            if (c != null) {
                for (; i < MaxCarsCount && i < c.Count; i++) {
                    _cars[i] = GetCarToRender(c[i]);
                }
            }

            for (; i < MaxCarsCount; i++) {
                _cars[i] = null;
            }

            _blocks = null;
        }

        private static Tuple<FormattedText, bool> GetSessionsItem(ServerEntry.Session session) {
            var key = (session.IsActive ? @"1" : @"0") + session.DisplayTypeShort;
            if (!SessionsCache.TryGetValue(key, out var result)) {
                SessionsCache[key] = result = Tuple.Create(GetText(session.DisplayTypeShort,
                        isBlock: true, isLabel: true, isHint: session.IsActive), session.IsActive);
            }
            return result;
        }

        private void UpdateSessions(ServerEntry server) {
            var c = server.Sessions;
            var i = 0;
            if (c != null) {
                for (; i < MaxSessionsCount && i < c.Count; i++) {
                    _sessions[i] = GetSessionsItem(c[i]);
                }
            }

            _sessionsCount = i;
            for (; i < MaxSessionsCount; i++) {
                _sessions[i] = null;
            }

            _blocks = null;
        }

        private static readonly NumberSubstitution NumberSubstitution = new NumberSubstitution();

        [ContractAnnotation(@"text: null => null; text: notnull => notnull")]
        private static FormattedText GetText([CanBeNull] string text, TextAlignment textAlignment = TextAlignment.Left, double maxWidth = double.NaN,
                bool isHint = false, bool isBlock = false, bool isLabel = false) {
            if (text == null) return null;
            var formattedText = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    isLabel ? _labelTypeface : _typeface, isLabel ? 12d : 11d,
                    isBlock ? isHint ? _blockTextActive : _blockText : isHint ? _hint : _foreground,
                    NumberSubstitution, _formattingMode) {
                        Trimming = TextTrimming.CharacterEllipsis,
                        TextAlignment = textAlignment,
                        MaxLineCount = 1
                    };
            if (!double.IsNaN(maxWidth)) {
                formattedText.MaxTextWidth = maxWidth;
            }

            return formattedText;
        }

        private Size _size;
        private bool _onceMeasured;

        protected override Size MeasureOverride(Size constraint) {
            if (_size != constraint) {
                _size = constraint;
                _onceMeasured = false;
                _blocks = null;
                _wideMode = constraint.Width > 600d;
            }

            if (!_onceMeasured) {
                _onceMeasured = true;
                _name.Measure(constraint);
            }

            return constraint;
        }

        protected override Size ArrangeOverride(Size arrangeBounds) {
            Update();

            var rect = new Rect(arrangeBounds);
            _name.Arrange(rect);
            return arrangeBounds;
        }

        private static SolidColorBrush GetHalfTransparent(SolidColorBrush solidColorBrush1) {
            var result = new SolidColorBrush(solidColorBrush1.Color) { Opacity = 0.5 };
            result.Freeze();
            return result;
        }

        private static readonly Lazier<ResourceDictionary> OnlineResources = Lazier.Create(() => {
            var result = new ResourceDictionary {
                Source = new Uri("/AcManager.Controls;component/Assets/OnlineSpecific.xaml", UriKind.Relative)
            };
            result.MergedDictionaries.Add(Application.Current.Resources);
            return result;
        });

        [CanBeNull]
        private static T FindStaticResource<T>(object key) where T : class {
            return OnlineResources.RequireValue[key] as T;
        }

        [CanBeNull]
        private static BitmapSource ToBitmap(string iconKey, string colorKey, int width = 12, int height = -1) {
            return ToBitmap(new Path {
                Data = FindStaticResource<Geometry>(iconKey),
                Fill = FindStaticResource<Brush>(colorKey),
                Stretch = Stretch.Uniform
            }, width, height);
        }

        [CanBeNull]
        private static BitmapSource ToBitmap(string resourceKey, int width = 12, int height = -1) {
            return ToBitmap(FindStaticResource<FrameworkElement>(resourceKey), width, height);
        }

        [CanBeNull]
        private static BitmapSource ToBitmap(FrameworkElement block, int width = 12, int height = -1) {
            if (block == null) {
                return null;
            }

            if (height == -1) {
                height = width;
            }

            var size = new Size(width == 0 ? double.PositiveInfinity : width, height == 0 ? double.PositiveInfinity : height);
            block.Measure(size);

            if (double.IsPositiveInfinity(size.Width)) {
                size.Width = block.DesiredSize.Width;
            }

            if (double.IsPositiveInfinity(size.Height)) {
                size.Height = block.DesiredSize.Height;
            }

            block.Arrange(new Rect(size));
            block.ApplyTemplate();
            block.UpdateLayout();

            var renderWidth = (int)(size.Width * AppearanceManager.Instance.BitmapCacheScale).Ceiling();
            var renderHeight = (int)(size.Height * AppearanceManager.Instance.BitmapCacheScale).Ceiling();
            var renderDpi = 96 * AppearanceManager.Instance.BitmapCacheScale;
            var bmp = new RenderTargetBitmap(renderWidth, renderHeight, renderDpi, renderDpi, PixelFormats.Pbgra32);
            bmp.Render(block);
            return bmp;
        }

        private StreamGeometry _blocks;

        private readonly DrawingGroup _backingStore = new DrawingGroup();
        private readonly DrawingGroup _backingTimeLeftStore = new DrawingGroup();

        private void RenderTimeLeft() {
            if (_wideMode) {
                if (_renderTimeInProcess) return;
                _renderTimeInProcess = true;
                Dispatcher.BeginInvoke((Action)RenderInner, DispatcherPriority.Render);
            }

            void RenderInner() {
                _renderTimeInProcess = false;

                var timeLeft = _timeLeft;
                if (timeLeft == null) return;

                var dc = _backingTimeLeftStore.Open();
                dc.DrawText(timeLeft, new Point(218 - timeLeft.Width, 29 + _textPadding));
                dc.Close();
            }
        }

        private bool _wideMode = true;
        private bool _renderInProcess, _renderTimeInProcess;

        private static Rect GetCountryRect(double width) {
            return new Rect(width - 108, 7, 24, 16);
        }

        private void Render() {
            if (_renderInProcess) return;

            _renderInProcess = true;
            Dispatcher.BeginInvoke((Action)RenderInner, DispatcherPriority.Render);

            void RenderInner() {
                Update();
                _renderInProcess = false;

                var dc = _backingStore.Open();

                var size = _size;
                var width = size.Width;

                var referenceIconOffset = 22d;
                for (var i = 0; i < _icons.Length; i++) {
                    var icon = _icons[i];
                    if (icon == null) break;

                    var iconWidth = icon.Width;
                    dc.DrawImage(icon, new Rect(referenceIconOffset, 5, iconWidth, ReferenceIconSize));
                    referenceIconOffset += iconWidth + ReferenceIconMargin;
                }

                if (_errorFlag) {
                    dc.DrawImage(_alertIcon, new Rect(width - 40 + 14, 9, 12, 12));
                } else if (_pingValue != null) {
                    dc.DrawText(_pingValue, new Point(width - 40, _textPadding));
                    dc.DrawText(_pingPostfix, new Point(width - 40 + _pingValue.Width, _textPadding));
                }

                dc.DrawRectangle(_areaBrush, null, new Rect(20, 30, width - 18, size.Height - 30));

                if (_track != null) {
                    Draw(dc, _track, 24);
                }

                if (!_fullyLoaded) {
                    // dc.DrawText(_track, new Point(24, 39));
                } else {
                    /*if (_currentTrack == null) {
                        dc.DrawImage(_alertIcon, new Rect(22, 39, 12, 12));
                        dc.DrawText(_track, new Point(38, 39));
                    } else {
                        dc.DrawText(_track, new Point(24, 39));
                    }*/

                    var pieces = (_bookedForPlayer ? 1 : 0) + (_passwordRequired ? 1 : 0) + (_hasFriends ? 1 : 0);
                    if (pieces < 3 && (_requiresCspAvailable || _requiresCspMissing)) {
                        ++pieces;
                    }
                    if (pieces > 0) {
                        var iconOffset = (60d - pieces * ReferenceIconHeight + (pieces - 1d) * ReferenceIconMargin) / 2d;
                        if (_bookedForPlayer) {
                            dc.DrawImage(_bookedIcon, new Rect(6, iconOffset, 12, ReferenceIconHeight));
                            iconOffset += ReferenceIconHeight + ReferenceIconMargin;
                        }
                        if (_passwordRequired) {
                            dc.DrawImage(_passwordIcon, new Rect(6, iconOffset, 12, ReferenceIconHeight));
                            iconOffset += ReferenceIconHeight + ReferenceIconMargin;
                        }
                        if (_hasFriends) {
                            dc.DrawImage(_friendsIcon, new Rect(6, iconOffset, 12, ReferenceIconHeight));
                            iconOffset += ReferenceIconHeight + ReferenceIconMargin;
                        }

                        if (_requiresCspAvailable || _requiresCspMissing) {
                            dc.DrawImage(_requiresCspAvailable ? _cspIconAvailable : _cspIconMissing, new Rect(6, iconOffset, 12, ReferenceIconHeight));
                            if (_cspProhibited) {
                                dc.DrawLine(_cspProhibitedPen, new Point(6, iconOffset), new Point(18, iconOffset + ReferenceIconHeight));
                            }
                        }
                    }

                    dc.DrawImage(_countryBitmap, GetCountryRect(width));
                    dc.DrawText(_displayClients, new Point(width - 80, _textPadding));

                    var cars = _cars;
                    var sessions = _sessions;

                    var carsOffset = _wideMode ? 324d : 240d;
                    var sessionsOffset = _wideMode ? 230d : 146d;

                    if (_blocks == null) {
                        _blocks = CreateCarsBackgroundsGeometry(cars, _sessionsCount, carsOffset, sessionsOffset, width);
                    }

                    dc.DrawGeometry(_blockBackground, null, _blocks);

                    for (var i = 0; i < sessions.Length; i++) {
                        var c = sessions[i];
                        if (c == null || sessionsOffset > width) break;

                        if (c.Item2) {
                            dc.DrawRectangle(_blockBackgroundActive, null, new Rect(sessionsOffset, 35, 20, 20));
                        }

                        dc.DrawText(c.Item1, new Point(sessionsOffset + 7, 28 + _textPadding));
                        sessionsOffset += 20;
                    }

                    for (var i = 0; i < cars.Length; i++) {
                        var c = cars[i];
                        if (c == null || carsOffset > width) break;

                        Draw(dc, c, carsOffset);
                        carsOffset += c.Width + 16;
                    }
                }

                dc.Close();
            }

            void Draw(DrawingContext dc, ObjToRender obj, double offset) {
                if (obj.Icon != null) {
                    dc.DrawImage(obj.Icon, new Rect(offset + 4, 39, 12, 12));
                }

                if (obj.TextImage != null) {
                    dc.DrawImage(obj.TextImage, new Rect(offset + 4 + obj.TextOffset, 38, obj.TextImage.Width, 14));
                } else if (obj.Text != null) {
                    dc.DrawText(obj.Text, new Point(offset + 4 + obj.TextOffset, 30 + _textPadding));
                }
            }
        }

        private static StreamGeometry CreateCarsBackgroundsGeometry(ObjToRender[] cars, int sessionsCount,
                double carsOffset, double sessionsOffset, double width) {
            var streamGeometry = new StreamGeometry();
            using (var gc = streamGeometry.Open()) {
                if (sessionsCount > 0) {
                    gc.BeginFigure(new Point(sessionsOffset, 35), true, true);
                    gc.LineTo(new Point(sessionsOffset + sessionsCount * 20, 35), true, true);
                    gc.LineTo(new Point(sessionsOffset + sessionsCount * 20, 55), true, true);
                    gc.LineTo(new Point(sessionsOffset, 55), true, true);
                }

                for (var i = 0; i < cars.Length; i++) {
                    var c = cars[i];
                    if (c == null) continue;

                    var cWidth = c.Width;
                    gc.BeginFigure(new Point(carsOffset, 34), true, true);
                    gc.LineTo(new Point(carsOffset + cWidth + 8, 34), true, true);
                    gc.LineTo(new Point(carsOffset + cWidth + 8, 56), true, true);
                    gc.LineTo(new Point(carsOffset, 56), true, true);
                    carsOffset += cWidth + 16;

                    if (carsOffset > width) break;
                }
            }

            streamGeometry.Freeze();
            return streamGeometry;
        }

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            Render();
            RenderTimeLeft();

            dc.DrawDrawing(_backingStore);
            if (_wideMode) {
                dc.DrawDrawing(_backingTimeLeftStore);
            }
        }

        private void OnServerPropertyChanged(object sender, PropertyChangedEventArgs e) {
            var server = (ServerEntry)sender;
            switch (e.PropertyName) {
                case nameof(ServerEntry.DisplayName):
                    UpdateName(server);
                    break;
                case nameof(ServerEntry.ReferencesString):
                    UpdateReferences(server);
                    break;
                case nameof(ServerEntry.PasswordRequired):
                    UpdatePasswordState(server);
                    break;
                case nameof(ServerEntry.IsBookedForPlayer):
                    UpdateBookedState(server);
                    break;
                case nameof(ServerEntry.HasFriends):
                    UpdateFriendsState(server);
                    break;
                case nameof(ServerEntry.CspRequiredAvailable):
                    UpdateCspStateAvailable(server);
                    break;
                case nameof(ServerEntry.CspRequiredMissing):
                    UpdateCspStateMissing(server);
                    break;
                case nameof(ServerEntry.RequiredCspVersion):
                    UpdateCspStateProhibited(server);
                    break;
                case nameof(ServerEntry.HasErrors):
                    UpdateErrorFlag(server);
                    break;
                case nameof(ServerEntry.CountryId):
                    UpdateCountryFlag(server);
                    break;
                case nameof(ServerEntry.Ping):
                    UpdatePing(server);
                    break;
                case nameof(ServerEntry.DisplayClients):
                    UpdateDisplayClients(server);
                    break;
                case nameof(ServerEntry.DisplayTimeLeft):
                    UpdateTimeLeft(server);
                    RenderTimeLeft();
                    return;
                case nameof(ServerEntry.Track):
                case nameof(ServerEntry.TrackId):
                    UpdateTrack(server);
                    break;
                case nameof(ServerEntry.Sessions):
                case nameof(ServerEntry.CurrentSessionType):
                    UpdateSessions(server);
                    break;
                case nameof(ServerEntry.Cars):
                    UpdateCars(server);
                    break;
                case nameof(ServerEntry.IsFullyLoaded):
                    UpdateErrorFlag(server);
                    UpdateFullyLoaded(server);
                    break;
                default:
                    return;
            }

            Render();
        }

        private void OnContentNameChanged(object sender, EventArgs e) {
            var track = _currentTrack;
            if (track != null) {
                _currentTrack = null;
                UpdateTrack(_server);
            }

            UpdateCars(_server);
            Render();
        }

        private void OnServerChanged([CanBeNull] ServerEntry o, [CanBeNull] ServerEntry n) {
            if (o != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(o, nameof(ServerEntry.PropertyChanged), OnServerPropertyChanged);
                WeakEventManager<ServerEntry, EventArgs>.RemoveHandler(o, nameof(ServerEntry.ContentNameChanged), OnContentNameChanged);
            }

            if (n != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(n, nameof(ServerEntry.PropertyChanged), OnServerPropertyChanged);
                WeakEventManager<ServerEntry, EventArgs>.AddHandler(n, nameof(ServerEntry.ContentNameChanged), OnContentNameChanged);
            }
        }

        // TODO: Replace warning triangles with download icons if content is available to download
        // TODO: Context menus
    }
}