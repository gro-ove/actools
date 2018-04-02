using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public class OnlineItem : Control {
        // TODO: add option
        public static int OptionCarsLimit = 10;

        private const int CarsPoolSize = 50;
        private const int SessionsPoolSize = 15;

        private Brush _blockText;
        private Brush _blockTextActive;
        private Brush _blockBackground;
        private Brush _blockBackgroundActive;

        private static Style _labelStyle;

        private void InitializeBrushes() {
            if (_blockText != null) return;

            _blockText = (Brush)FindResource(@"ButtonText");
            _blockTextActive = (Brush)FindResource(@"ButtonTextPressed");
            _blockBackground = (Brush)FindResource(@"ButtonBackground");
            _blockBackgroundActive = (Brush)FindResource(@"ButtonBackgroundPressed");

            if (_labelStyle == null) {
                _labelStyle = (Style)FindResource(@"Label");
            }
        }

        static OnlineItem() {
            AppearanceManager.Instance.ThemeChange += OnThemeChange;
            DefaultStyleKeyProperty.OverrideMetadata(typeof(OnlineItem), new FrameworkPropertyMetadata(typeof(OnlineItem)));
        }

        private static void OnThemeChange(object sender, EventArgs e) {
            CarsPool.Clear();
            SessionsPool.Clear();
        }

        private readonly List<Inline> _errorIcons = new List<Inline>(4);

        private Inline GetErrorIconInline() {
            var c = _errorIcons.Count;
            if (c > 0) {
                var result = _errorIcons[c - 1];
                _errorIcons.RemoveAt(c - 1);
                return result;
            }

            return (Inline)TryFindResource(@"WarningIconInline");
        }

        private void ReleaseErrorIconInline(Inline released) {
            _errorIcons.Add(released);
        }

        public static readonly DependencyProperty HideSourceIconsProperty = DependencyProperty.Register(nameof(HideSourceIcons), typeof(string[]),
                typeof(OnlineItem), new PropertyMetadata(null, (o, e) => {
                    ((OnlineItem)o)._hideSourceIcons = (string[])e.NewValue;
                }));

        private string[] _hideSourceIcons;

        /// <summary>
        /// ID of the source which icon is should be hidden.
        /// </summary>
        public string[] HideSourceIcons {
            get => _hideSourceIcons;
            set => SetValue(HideSourceIconsProperty, value);
        }

        private Inline _minoratingIcon;

        private Inline GetMinoratingIconInline() {
            return _minoratingIcon ?? (_minoratingIcon = (Inline)TryFindResource(@"MinoratingIconInline"));
        }

        private Inline _lanIcon;

        private Inline GetLanIconInline() {
            return _lanIcon ?? (_lanIcon = (Inline)TryFindResource(@"LanIconInline"));
        }

        private Inline _favouritesIcon;

        private Inline GetFavouritesIconInline() {
            return _favouritesIcon ?? (_favouritesIcon = (Inline)TryFindResource(@"FavouriteIconInline"));
        }

        private Inline _hiddenIcon;

        private Inline GetHiddenIconInline() {
            return _hiddenIcon ?? (_hiddenIcon = (Inline)TryFindResource(@"HiddenIconInline"));
        }

        private Inline _recentIcon;

        private Inline GetRecentIconInline() {
            return _recentIcon ?? (_recentIcon = (Inline)TryFindResource(@"RecentIconInline"));
        }

        public static readonly DependencyProperty ServerProperty = DependencyProperty.Register(nameof(Server), typeof(ServerEntry),
                typeof(OnlineItem), new PropertyMetadata(OnServerChanged));

        public ServerEntry Server {
            get => (ServerEntry)GetValue(ServerProperty);
            set => SetValue(ServerProperty, value);
        }

        private static void OnServerChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            try {
                ((OnlineItem)o).OnServerChanged((ServerEntry)e.OldValue, (ServerEntry)e.NewValue);
            } catch (Exception ex) {
                Logging.Error(ex);
            }
        }

        private readonly Dictionary<string, Inline> _customIcons = new Dictionary<string, Inline>();

        [CanBeNull]
        private Inline GetIcon(string originId) {
            if (_hideSourceIcons != null && Array.IndexOf(_hideSourceIcons, originId) != -1) return null;

            switch (originId) {
                case FileBasedOnlineSources.FavouritesKey:
                    return GetFavouritesIconInline();
                case FileBasedOnlineSources.HiddenKey:
                    return GetHiddenIconInline();
                case FileBasedOnlineSources.RecentKey:
                    return GetRecentIconInline();
                case MinoratingOnlineSource.Key:
                    return GetMinoratingIconInline();
                case LanOnlineSource.Key:
                    return GetLanIconInline();
                case KunosOnlineSource.Key:
                    return null;
                default:
                    if (!_customIcons.TryGetValue(originId, out var result)) {
                        var information = FileBasedOnlineSources.Instance.GetInformation(originId);

                        var baseIcon = (Decorator)TryFindResource(@"BaseIcon");
                        if (baseIcon == null) {
                            return null;
                        }

                        var text = (BbCodeBlock)baseIcon.Child;

                        text.SetBinding(BbCodeBlock.TextProperty, new Binding {
                            Path = new PropertyPath(nameof(information.Label)),
                            Source = information
                        });

                        text.SetBinding(TextBlock.ForegroundProperty, new Binding {
                            Path = new PropertyPath(nameof(information.Color)),
                            TargetNullValue = new SolidColorBrush(Colors.White),
                            Converter = ColorPicker.ColorToBrushConverter,
                            Source = information
                        });

                        var block = new Border {
                            Margin = (Thickness)TryFindResource(@"InlineIconMargin"),
                            Child = baseIcon
                        };

                        block.SetBinding(VisibilityProperty, new Binding {
                            Path = new PropertyPath(nameof(information.Label)),
                            Source = information,
                            Converter = new NullToVisibilityConverter(),
                            ConverterParameter = @"inverse"
                        });

                        result = new InlineUIContainer { Child = block };
                        _customIcons[originId] = result;
                    }

                    return result;
            }
        }

        private string _origins;
        private Span _icons;

        private void UpdateName(ServerEntry n) {
            if (n.ReferencesString != _origins) {
                _origins = n.ReferencesString;

                var icons = n.GetReferencesIds().Select(GetIcon).NonNull().ToList();
                if (icons.Any()) {
                    _icons = new Span();
                    _icons.Inlines.AddRange(icons);
                } else {
                    _icons = null;
                }
            }

            if (_icons == null) {
                _nameText.Text = n.DisplayName;
            } else {
                var inlines = _nameText.Inlines;
                inlines.Clear();
                inlines.Add(_icons);
                inlines.Add(BbCodeBlock.ParseEmoji(n.DisplayName, false, this));
            }
        }

        private void UpdateCountryFlag(ServerEntry n) {
            if (_hideSourceIcons == null || Array.IndexOf(_hideSourceIcons, LanOnlineSource.Key) == -1) {
                _countryFlagImage.Country = n.CountryId;
            } else if (_countryFlagImage.Visibility != Visibility.Hidden) {
                _countryFlagImage.Visibility = Visibility.Collapsed;

                var margin = _nameText.Margin;
                _nameText.Margin = new Thickness(margin.Left, margin.Top, margin.Right - 28, margin.Bottom);
            }
        }

        private TrackObjectBase _bindedTrack;

        private void UpdateTrack(ServerEntry n) {
            if (_bindedTrack != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(_bindedTrack, nameof(INotifyPropertyChanged.PropertyChanged),
                        BindedTrack_PropertyChanged);
            }

            var existingIcon = _trackNameText.Inlines.FirstInline as InlineUIContainer;

            _bindedTrack = n.Track;
            if (_bindedTrack != null) {
                _trackNameText.Text = _bindedTrack.Name;
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(_bindedTrack, nameof(INotifyPropertyChanged.PropertyChanged),
                        BindedTrack_PropertyChanged);
            } else if (n.TrackId != null) {
                _trackNameText.Inlines.Clear();
                _trackNameText.Inlines.AddRange(new [] {
                    existingIcon ?? GetErrorIconInline(),
                    new Run { Text = n.TrackId }
                }.NonNull());
                return;
            } else {
                _trackNameText.Text = "No information";
            }

            if (existingIcon != null) {
                ReleaseErrorIconInline(existingIcon);
            }
        }

        private void BindedTrack_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(TrackObjectBase.Name) && _bindedTrack != null) {
                _trackNameText.Text = _bindedTrack.Name;
            }
        }

        private static readonly List<TextBlock> SessionsPool = new List<TextBlock>(SessionsPoolSize);

        private void UpdateSession(TextBlock child, ServerEntry.Session session) {
            if (ReferenceEquals(child.DataContext, session)) return;

            if (_scrolling) {
                _sessionsTooltipsSet = false;
                child.ToolTip = null;
            }

            child.DataContext = session;
            child.Text = session.DisplayTypeShort;
            if (session.IsActive) {
                child.Foreground = _blockTextActive;
                child.Background = _blockBackgroundActive;
            } else {
                child.Foreground = _blockText;
                child.Background = _blockBackground;
            }
        }

        private void UpdateSessions(ServerEntry n) {
            InitializeBrushes();

            var array = n.Sessions;
            var children = _sessionsPanel.Children;
            for (var i = children.Count - array?.Count ?? children.Count; i > 0; i--) {
                var last = children.Count - 1;
                if (SessionsPool.Count < SessionsPoolSize) {
                    SessionsPool.Add((TextBlock)children[last]);
                }
                children.RemoveAt(last);
            }

            if (array == null) return;
            for (var i = 0; i < array.Count; i++) {
                TextBlock child;
                if (i < children.Count) {
                    child = (TextBlock)children[i];
                } else {
                    if (SessionsPool.Count > 0) {
                        var last = SessionsPool.Count - 1;
                        child = SessionsPool[last];
                        SessionsPool.RemoveAt(last);
                    } else {
                        child = new TextBlock {
                            TextAlignment = TextAlignment.Center,
                            Style = _labelStyle,
                            Padding = new Thickness(0, 2, 0, 0),
                            Height = 20d,
                            Width = 20d
                        };
                    }

                    children.Add(child);
                }

                UpdateSession(child, array[i]);
            }
        }

        private static readonly List<TextBlockBindable> CarsPool = new List<TextBlockBindable>(CarsPoolSize);

        private class TextBlockBindable : BbCodeBlock {
            internal CarDisplayNameBind Bind;
        }

        private class CarDisplayNameBind : IDisposable {
            private readonly BbCodeBlock _text;
            private readonly AcItemWrapper _wrapper;

            public AcItemWrapper Wrapper => _wrapper;

            public CarDisplayNameBind(BbCodeBlock text, AcItemWrapper wrapper) {
                _text = text;
                _wrapper = wrapper;

                WeakEventManager<AcItemWrapper, WrappedValueChangedEventArgs>.AddHandler(wrapper, nameof(AcItemWrapper.ValueChanged), Wrapper_ValueChanged);
                if (wrapper.IsLoaded) {
                    WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(wrapper.Value, nameof(INotifyPropertyChanged.PropertyChanged),
                            Value_PropertyChanged);
                }

                _text.Text = wrapper.Value.DisplayName;
            }

            private void Wrapper_ValueChanged(object sender, WrappedValueChangedEventArgs args) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(args.OldValue, nameof(INotifyPropertyChanged.PropertyChanged),
                        Value_PropertyChanged);

                var wrapper = (AcItemWrapper)sender;
                if (wrapper.IsLoaded) {
                    WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(args.NewValue, nameof(INotifyPropertyChanged.PropertyChanged),
                            Value_PropertyChanged);
                }

                _text.Text = args.NewValue.DisplayName;
            }

            private void Value_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(AcPlaceholderNew.DisplayName)) {
                    _text.Text = ((AcPlaceholderNew)sender).DisplayName;
                }
            }

            public void Dispose() {
                WeakEventManager<AcItemWrapper, WrappedValueChangedEventArgs>.RemoveHandler(_wrapper, nameof(AcItemWrapper.ValueChanged), Wrapper_ValueChanged);
                if (_wrapper.IsLoaded) {
                    WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(_wrapper.Value, nameof(INotifyPropertyChanged.PropertyChanged),
                            Value_PropertyChanged);
                }
            }
        }

        private void UpdateCar(TextBlockBindable child, ServerEntry.CarEntry car) {
            if (ReferenceEquals(car, child.DataContext) &&
                    // Additional check, in case CarObjectWrapper changed
                    ReferenceEquals(child.Bind?.Wrapper, car.CarObjectWrapper)) {
                return;
            }

            InitializeBrushes();

            DisposeHelper.Dispose(ref child.Bind);
            if (_scrolling) {
                _carsTooltipsSet = false;
                child.ToolTip = null;
            }

            var existingIcon = child.Inlines.FirstInline as InlineUIContainer;

            child.DataContext = car;
            var wrapper = car.CarObjectWrapper;
            if (wrapper != null) {
                child.Bind = new CarDisplayNameBind(child, wrapper);
            } else {
                child.Inlines.Clear();
                child.Inlines.AddRange(new [] {
                    existingIcon ?? GetErrorIconInline(),
                    new Run { Text = car.Id }
                }.NonNull());
                return;
            }

            if (existingIcon != null) {
                ReleaseErrorIconInline(existingIcon);
            }
        }

        private static Style _smallStyle;

        private void UpdateCars(ServerEntry n) {
            var array = n.Cars;
            var children = _carsPanel.Children;
            var carsCount = Math.Min(array?.Count ?? 0, OptionCarsLimit);

            for (var i = children.Count - carsCount; i > 0; i--) {
                var last = children.Count - 1;
                var child = (TextBlockBindable)children[last];
                DisposeHelper.Dispose(ref child.Bind);

                if (CarsPool.Count < CarsPoolSize) {
                    CarsPool.Add(child);
                }
                children.RemoveAt(last);
            }

            if (array == null) return;
            for (var i = 0; i < carsCount; i++) {
                TextBlockBindable child;
                if (i < children.Count) {
                    child = (TextBlockBindable)children[i];
                } else {
                    if (CarsPool.Count > 0) {
                        var last = CarsPool.Count - 1;
                        child = CarsPool[last];
                        CarsPool.RemoveAt(last);
                    } else {
                        if (_smallStyle == null) {
                            _smallStyle = (Style)FindResource(@"Small");
                        }

                        child = new TextBlockBindable {
                            Style = _smallStyle,
                            Margin = new Thickness(4, 0, 4, 0),
                            Padding = new Thickness(2),
                            Height = 20d,
                            Foreground = _blockText,
                            Background = _blockBackground
                        };
                    }

                    children.Add(child);
                }

                UpdateCar(child, array[i]);
            }
        }

        private void UpdateFullyLoaded(ServerEntry n) {
            if (_passwordIcon == null) return;

            var loaded = n.IsFullyLoaded;
            var visibility = loaded ? Visibility.Visible : Visibility.Collapsed;
            if (visibility == _countryFlagImage.Visibility) return;

            _nameText.FontStyle = loaded ? FontStyles.Normal : FontStyles.Italic;

            if (!n.FromLan) {
                _countryName.Visibility = visibility;
                _countryFlagImage.Visibility = visibility;
            }

            _pingText.Visibility = visibility;
            _clientsText.Visibility = visibility;

            if (_timeLeftText != null) {
                _timeLeftText.Visibility = visibility;
            }
        }

        private void UpdateErrorFlag(ServerEntry n) {
            _hasErrorsGroup.Value = n.HasErrors || !n.IsFullyLoaded;
        }

        private bool? _password;

        private void UpdatePasswordState(ServerEntry n) {
            if (_password != n.PasswordRequired) {
                if (_passwordIcon != null) {
                    _password = n.PasswordRequired;
                    _passwordIcon.Visibility = n.PasswordRequired ? Visibility.Visible : Visibility.Collapsed;
                }
            } else if (_passwordIcon == null) {
                _password = null;
            }
        }

        private bool _friends;

        private void UpdateFriendsState(ServerEntry n) {
            if (_friends != n.HasFriends) {
                _friends = n.HasFriends;
                _friendsIcon.Visibility = _friends ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool _bookedForPlayer;

        private void UpdateBookedState(ServerEntry n) {
            if (_bookedForPlayer != n.IsBookedForPlayer) {
                _bookedForPlayer = n.IsBookedForPlayer;
                _bookedIcon.Visibility = _bookedForPlayer ? Visibility.Visible : Visibility.Collapsed;
                if (_bookedForPlayer) {
                    _nameText.SetValue(TextBlock.ForegroundProperty, (Brush)TryFindResource(@"Go"));
                } else {
                    _nameText.ClearValue(TextBlock.ForegroundProperty);
                }
            }
        }

        private void Update(ServerEntry n) {
            if (n == null || _passwordIcon == null) return;

            if (_scrolling) {
                _trackNameText.ToolTip = null;
                _trackTooltipSet = false;

                _clientsText.ToolTip = null;
                _clientsTooltipSet = false;
            }

            if (!n.FromLan) {
                _countryName.Text = n.Country;
            }

            _pingText.Text = n.Ping?.ToString() ?? @"?";
            _clientsText.Text = n.DisplayClients;
            _errorMessageGroup.Text = n.ErrorsString;

            if (_timeLeftText != null) {
                _timeLeftText.Text = n.DisplayTimeLeft;
            }

            UpdateName(n);
            UpdateCountryFlag(n);
            UpdateTrack(n);
            UpdateSessions(n);
            UpdateCars(n);
            UpdateFullyLoaded(n);
            UpdateErrorFlag(n);
            UpdatePasswordState(n);
            UpdateFriendsState(n);
            UpdateBookedState(n);
        }

        [CanBeNull]
        private FrameworkElement _passwordIcon;
        private FrameworkElement _bookedIcon;
        private FrameworkElement _friendsIcon;
        private BooleanSwitch _hasErrorsGroup;
        private BbCodeBlock _errorMessageGroup;
        private TextBlock _pingText;
        private TextBlock _clientsText;

        [CanBeNull]
        private TextBlock _timeLeftText;
        private CountryIcon _countryFlagImage;
        private BbCodeBlock _nameText;
        private TextBlock _countryName;
        private BbCodeBlock _trackNameText;
        private Panel _sessionsPanel;
        private Panel _carsPanel;

        public override void OnApplyTemplate() {
            if (_carsPanel != null) {
                _carsPanel.MouseEnter -= CarsPanel_MouseEnter;
            }

            if (_carsPanel != null) {
                _sessionsPanel.MouseEnter -= SessionsPanel_MouseEnter;
            }

            if (_trackNameText != null) {
                _trackNameText.MouseEnter -= TrackNameText_MouseEnter;
            }

            if (_clientsText != null) {
                _clientsText.MouseEnter -= ClientsText_MouseEnter;
            }

            base.OnApplyTemplate();
            _passwordIcon = (FrameworkElement)GetTemplateChild(@"PasswordIcon");
            _bookedIcon = (FrameworkElement)GetTemplateChild(@"BookedForPlayerIcon");
            _friendsIcon = (FrameworkElement)GetTemplateChild(@"FriendsIcon");
            _hasErrorsGroup = (BooleanSwitch)GetTemplateChild(@"HasErrorsGroup");
            _errorMessageGroup = (BbCodeBlock)GetTemplateChild(@"ErrorMessageGroup");
            _pingText = (TextBlock)GetTemplateChild(@"PingText");
            _clientsText = (TextBlock)GetTemplateChild(@"DisplayClientsText");
            _timeLeftText = (TextBlock)GetTemplateChild(@"TimeLeftText");
            _countryFlagImage = (CountryIcon)GetTemplateChild(@"CountryFlagImage");
            _nameText = (BbCodeBlock)GetTemplateChild(@"DisplayNameText");
            _trackNameText = (BbCodeBlock)GetTemplateChild(@"TrackNameText");
            _countryName = (TextBlock)GetTemplateChild(@"CountryName");
            _sessionsPanel = (Panel)GetTemplateChild(@"SessionsPanel");
            _carsPanel = (Panel)GetTemplateChild(@"CarsPanel");
            Update(Server);

            if (_carsPanel != null) {
                _carsPanel.MouseEnter += CarsPanel_MouseEnter;
            }

            if (_sessionsPanel != null) {
                _sessionsPanel.MouseEnter += SessionsPanel_MouseEnter;
            }

            if (_trackNameText != null) {
                _trackNameText.MouseEnter += TrackNameText_MouseEnter;
            }

            if (_clientsText != null) {
                _clientsText.MouseEnter += ClientsText_MouseEnter;
            }
        }

        private bool _scrolling;

        public void SetScrolling(bool scrolling) {
            _scrolling = scrolling;
            // _clientsText.Background = new SolidColorBrush(_scrolling ? Colors.DarkMagenta : Colors.DarkOliveGreen);

            if (scrolling) {
            } else {
                if (_carsPanel?.IsMouseOver == true) {
                    SetCarsTooltips();
                } else if (_sessionsPanel?.IsMouseOver == true) {
                    SetSessionsTooltips();
                } else if (_trackNameText?.IsMouseOver == true) {
                    SetTrackTooltip();
                }
            }
        }

        private bool _clientsTooltipSet;

        private void SetClientsTooltip() {
            if (_clientsTooltipSet) return;
            _clientsText.ToolTip = FindResource(@"ClientsTooltip");
            _clientsTooltipSet = true;
        }

        private void ClientsText_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) {
            SetClientsTooltip();
        }

        private bool _trackTooltipSet;

        private void SetTrackTooltip() {
            if (_trackTooltipSet) return;
            _trackNameText.ToolTip = FindResource(@"TrackPreviewTooltip.Online");
            _trackTooltipSet = true;
        }

        private void TrackNameText_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) {
            SetTrackTooltip();
        }

        private bool _sessionsTooltipsSet;

        private void SetSessionsTooltips() {
            if (_sessionsTooltipsSet) return;

            var toolTip = FindResource(@"SessionItemTooltip");
            foreach (var item in _sessionsPanel.Children.Cast<TextBlock>()) {
                item.ToolTip = toolTip;
            }

            _sessionsTooltipsSet = true;
        }

        private void SessionsPanel_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) {
            SetSessionsTooltips();
        }

        private bool _carsTooltipsSet;

        private void SetCarsTooltips() {
            if (_carsTooltipsSet) return;

            var toolTip = FindResource(@"CarPreviewTooltip.Online");
            foreach (var item in _carsPanel.Children.Cast<TextBlock>()) {
                item.ToolTip = toolTip;
            }

            _carsTooltipsSet = true;
        }

        private void CarsPanel_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) {
            SetCarsTooltips();
        }

        private void Handler(object sender, PropertyChangedEventArgs e) {
            if (_passwordIcon == null) return;

            var n = (ServerEntry)sender;
            switch (e.PropertyName) {
                case nameof(ServerEntry.ReferencesString):
                case nameof(ServerEntry.DisplayName):
                    UpdateName(n);
                    break;
                case nameof(ServerEntry.PasswordRequired):
                    UpdatePasswordState(n);
                    break;
                case nameof(ServerEntry.IsBookedForPlayer):
                    UpdateBookedState(n);
                    break;
                case nameof(ServerEntry.HasFriends):
                    UpdateFriendsState(n);
                    break;
                case nameof(ServerEntry.HasErrors):
                    UpdateErrorFlag(n);
                    break;
                case nameof(ServerEntry.CountryId):
                    UpdateCountryFlag(n);
                    break;
                case nameof(ServerEntry.Country):
                    if (!n.FromLan) {
                        _countryName.Text = n.Country;
                    }
                    break;
                case nameof(ServerEntry.Ping):
                    _pingText.Text = n.Ping?.ToString() ?? @"?";
                    break;
                case nameof(ServerEntry.DisplayClients):
                    _clientsText.Text = n.DisplayClients;
                    break;
                case nameof(ServerEntry.ErrorsString):
                    _errorMessageGroup.Text = n.ErrorsString;
                    break;
                case nameof(ServerEntry.DisplayTimeLeft):
                    if (_timeLeftText != null) {
                        _timeLeftText.Text = n.DisplayTimeLeft;
                    }
                    break;
                case nameof(ServerEntry.Track):
                case nameof(ServerEntry.TrackId):
                    UpdateTrack(n);
                    break;
                case nameof(ServerEntry.Sessions):
                    UpdateSessions(n);
                    break;
                case nameof(ServerEntry.Cars):
                    UpdateCars(n);
                    break;
                case nameof(ServerEntry.IsFullyLoaded):
                    UpdateErrorFlag(n);
                    UpdateFullyLoaded(n);
                    break;
            }
        }

        private void OnServerChanged([CanBeNull] ServerEntry o, [CanBeNull] ServerEntry n) {
            if (o != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(o, nameof(INotifyPropertyChanged.PropertyChanged), Handler);
            }

            if (n != null) {
                Update(n);
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(n, nameof(INotifyPropertyChanged.PropertyChanged), Handler);
            }
        }
    }
}