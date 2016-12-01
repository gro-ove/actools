using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using AcManager.Controls.Converters;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public class OnlineListBoxDetailedItem : Control {
        // TODO: add option
        public static int OptionCarsLimit = 10;

        private const int CarsPoolSize = 50;
        private const int SessionsPoolSize = 20;

        private Brush _blockText;
        private Brush _blockTextActive;
        private Brush _blockBackground;
        private Brush _blockBackgroundActive;
        private Brush _goBrush;
        private Brush _errorBrush;

        private static Style _labelStyle;

        private void InitializeBrushes() {
            if (_blockText != null) return;

            _blockText = (Brush)FindResource(@"ButtonText");
            _blockTextActive = (Brush)FindResource(@"ButtonTextPressed");
            _blockBackground = (Brush)FindResource(@"ButtonBackground");
            _blockBackgroundActive = (Brush)FindResource(@"ButtonBackgroundPressed");
            _goBrush = new SolidColorBrush((Color)FindResource(@"GoColor"));
            _errorBrush = new SolidColorBrush((Color)FindResource(@"ErrorColor"));

            if (_labelStyle == null) {
                _labelStyle = (Style)FindResource(@"Label");
            }
        }

        static OnlineListBoxDetailedItem() {
            AppearanceManager.Current.ThemeChange += OnThemeChange;
            DefaultStyleKeyProperty.OverrideMetadata(typeof(OnlineListBoxDetailedItem), new FrameworkPropertyMetadata(typeof(OnlineListBoxDetailedItem)));
        }

        private static void OnThemeChange(object sender, EventArgs e) {
            CarsPool.Clear();
            SessionsPool.Clear();
        }

        private Inline _errorIcon;

        private Inline GetErrorIconInline() {
            return _errorIcon?.Parent != null ? (Inline)FindResource(@"WarningIconInline") :
                    _errorIcon ?? (_errorIcon = (Inline)FindResource(@"WarningIconInline"));
        }

        private Inline _minoratingIcon;

        private Inline GetMinoratingIconInline() {
            return _minoratingIcon?.Parent != null ? (Inline)FindResource(@"MinoratingIconInline") :
                    _minoratingIcon ?? (_minoratingIcon = (Inline)FindResource(@"MinoratingIconInline"));
        }

        public static readonly DependencyProperty ServerProperty = DependencyProperty.Register(nameof(Server), typeof(ServerEntry),
                typeof(OnlineListBoxDetailedItem), new PropertyMetadata(OnServerChanged));

        public ServerEntry Server {
            get { return (ServerEntry)GetValue(ServerProperty); }
            set { SetValue(ServerProperty, value); }
        }

        private static void OnServerChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            try {
                ((OnlineListBoxDetailedItem)o).OnServerChanged((ServerEntry)e.OldValue, (ServerEntry)e.NewValue);
            } catch (Exception ex) {
                Logging.Error(ex);
            }
        }

        private void UpdateName(ServerEntry n) {
            if (n.OriginsFrom(MinoratingOnlineSource.Key)) {
                var inlines = _nameText.Inlines;

                /*if (inlines.Count == 2) {
                    ((Run)((IList)inlines)[1]).Text = n.DisplayName;
                } else {*/
                    inlines.Clear();
                    inlines.AddRange(new Inline[] {
                        GetMinoratingIconInline(),
                        new Run { Text = n.DisplayName }
                    });
                //}
            } else {
                _nameText.Text = n.DisplayName;
            }
        }

        private TrackObjectBase _bindedTrack;

        private void UpdateTrack(ServerEntry n) {
            if (_bindedTrack != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(_bindedTrack, nameof(INotifyPropertyChanged.PropertyChanged),
                        BindedTrack_PropertyChanged);
            }

            _bindedTrack = n.Track;
            if (_bindedTrack != null) {
                _trackNameText.Text = _bindedTrack.Name;
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(_bindedTrack, nameof(INotifyPropertyChanged.PropertyChanged),
                        BindedTrack_PropertyChanged);
            } else if (n.TrackId != null) {
                _trackNameText.Inlines.Clear();
                _trackNameText.Inlines.AddRange(new Inline[] {
                    GetErrorIconInline(),
                    new Run { Text = n.TrackId }
                });
            } else {
                _trackNameText.Text = "No information";
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

        private class TextBlockBindable : TextBlock {
            internal CarDisplayNameBind Bind;
        }

        private class CarDisplayNameBind : IDisposable {
            private readonly TextBlock _text;
            private readonly AcItemWrapper _wrapper;

            public CarDisplayNameBind(TextBlock text, AcItemWrapper wrapper) {
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

        private void UpdateCar(TextBlockBindable child, ServerEntry.CarOrOnlyCarIdEntry car) {
            if (ReferenceEquals(child.DataContext, car)) return;
            InitializeBrushes();

            DisposeHelper.Dispose(ref child.Bind);
            if (_scrolling) {
                _carsTooltipsSet = false;
                child.ToolTip = null;
            }

            child.DataContext = car;
            var wrapper = car.CarObjectWrapper;
            if (wrapper != null) {
                child.Tag = new CarDisplayNameBind(child, wrapper);
            } else {
                child.Inlines.Clear();
                child.Inlines.AddRange(new Inline[] {
                    GetErrorIconInline(),
                    new Run { Text = car.CarId }
                });
            }
        }

        private static Style _smallStyle;

        private void UpdateCars(ServerEntry n) {
            var array = n.CarsOrTheirIds;

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
            _countryName.Visibility = visibility;
            _countryFlagImage.Visibility = visibility;
            _pingText.Visibility = visibility;
            _clientsText.Visibility = visibility;
            _passwordIcon.Visibility = visibility;

            if (_timeLeftText != null) {
                _timeLeftText.Visibility = visibility;
            }
        }

        private void UpdateErrorFlag(ServerEntry n) {
            _hasErrorsGroup.Value = n.HasErrors || !n.IsFullyLoaded;
        }

        private void Update(ServerEntry n) {
            if (n == null || _passwordIcon == null) return;

            if (_scrolling) {
                _trackNameText.ToolTip = null;
                _trackTooltipSet = false;

                _clientsText.ToolTip = null;
                _clientsTooltipSet = false;
            }

            _passwordIcon.Visibility = n.PasswordRequired ? Visibility.Visible : Visibility.Collapsed;
            _countryFlagImage.Source = CountryIdToImageConverter.Instance.Convert(n.CountryId);
            _countryName.Text = n.Country;
            _pingText.Text = n.Ping?.ToString() ?? @"?";
            _clientsText.Text = n.DisplayClients;
            _errorMessageGroup.BbCode = n.ErrorMessage;
            
            if (_timeLeftText != null) {
                _timeLeftText.Text = n.DisplayTimeLeft;
            }

            UpdateName(n);
            UpdateTrack(n);
            UpdateSessions(n);
            UpdateCars(n);
            UpdateFullyLoaded(n);
            UpdateErrorFlag(n);
        }

        [CanBeNull]
        private FrameworkElement _passwordIcon;
        private BooleanSwitch _hasErrorsGroup;
        private BbCodeBlock _errorMessageGroup;
        private TextBlock _pingText;
        private TextBlock _clientsText;

        [CanBeNull]
        private TextBlock _timeLeftText;
        private Image _countryFlagImage;
        private TextBlock _nameText;
        private TextBlock _countryName;
        private TextBlock _trackNameText;
        private StackPanel _sessionsPanel;
        private StackPanel _carsPanel;

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
            _hasErrorsGroup = (BooleanSwitch)GetTemplateChild(@"HasErrorsGroup");
            _errorMessageGroup = (BbCodeBlock)GetTemplateChild(@"ErrorMessageGroup");
            _pingText = (TextBlock)GetTemplateChild(@"PingText");
            _clientsText = (TextBlock)GetTemplateChild(@"DisplayClientsText");
            _timeLeftText = (TextBlock)GetTemplateChild(@"TimeLeftText");
            _countryFlagImage = (Image)GetTemplateChild(@"CountryFlagImage");
            _nameText = (TextBlock)GetTemplateChild(@"DisplayNameText");
            _trackNameText = (TextBlock)GetTemplateChild(@"TrackNameText");
            _countryName = (TextBlock)GetTemplateChild(@"CountryName");
            _sessionsPanel = (StackPanel)GetTemplateChild(@"SessionsPanel");
            _carsPanel = (StackPanel)GetTemplateChild(@"CarsPanel");
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
            _trackNameText.ToolTip = FindResource(@"TrackPreviewTooltip");
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

            var toolTip = FindResource(@"CarPreviewTooltip");
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
                case nameof(ServerEntry.Origins):
                case nameof(ServerEntry.DisplayName):
                    UpdateName(n);
                    break;
                case nameof(ServerEntry.PasswordRequired):
                    _passwordIcon.Visibility = n.PasswordRequired ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ServerEntry.HasErrors):
                    UpdateErrorFlag(n);
                    break;
                case nameof(ServerEntry.CountryId):
                    _countryFlagImage.Source = CountryIdToImageConverter.Instance.Convert(n.CountryId);
                    break;
                case nameof(ServerEntry.Country):
                    _countryName.Text = n.Country;
                    break;
                case nameof(ServerEntry.Ping):
                    _pingText.Text = n.Ping?.ToString() ?? @"?";
                    break;
                case nameof(ServerEntry.DisplayClients):
                    _clientsText.Text = n.DisplayClients;
                    break;
                case nameof(ServerEntry.ErrorMessage):
                    _errorMessageGroup.BbCode = n.ErrorMessage;
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
                case nameof(ServerEntry.CarsOrTheirIds):
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