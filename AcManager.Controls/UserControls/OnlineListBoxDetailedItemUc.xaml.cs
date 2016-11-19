using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Converters;
using AcManager.Tools.Managers.Online;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    // let’s see if dropping bindings will help with performance issues
    public partial class OnlineListBoxDetailedItemUc {
        private readonly Brush _buttonText;
        private readonly Brush _buttonTextPressed;
        private readonly Brush _buttonBackground;
        private readonly Brush _buttonBackgroundPressed;

        public OnlineListBoxDetailedItemUc() {
            // Logging.Here();

            InitializeComponent();

            _buttonText = (Brush)FindResource(@"ButtonText");
            _buttonTextPressed = (Brush)FindResource(@"ButtonTextPressed");

            _buttonBackground = (Brush)FindResource(@"ButtonBackground");
            _buttonBackgroundPressed = (Brush)FindResource(@"ButtonBackgroundPressed");

            // DataContextChanged += OnlineListBoxDetailedItem_DataContextChanged;
        }

        private void OnlineListBoxDetailedItem_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            // Logging.Warning(Parent.GetParent().GetParent());
        }

        public static readonly DependencyProperty ServerProperty = DependencyProperty.Register(nameof(Server), typeof(ServerEntry),
                typeof(OnlineListBoxDetailedItemUc), new PropertyMetadata(OnServerChanged));

        public ServerEntry Server {
            get { return (ServerEntry)GetValue(ServerProperty); }
            set { SetValue(ServerProperty, value); }
        }

        private static void OnServerChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            try {
                if (e.OldValue != null) {
                    Logging.Debug($"{e.OldValue ?? "<NULL>"} → {e.NewValue ?? "<NULL>"}");
                }
                ((OnlineListBoxDetailedItemUc)o).OnServerChanged((ServerEntry)e.OldValue, (ServerEntry)e.NewValue);
            } catch (Exception ex) {
                Logging.Error(ex);
            }
        }

        private void UpdateTrack(ServerEntry n) {
            if (n.Track != null) {
                TrackNameText.Foreground = _buttonText;
                TrackNameText.Background = _buttonBackground;
                TrackNameText.Text = n.Track.Name;
                // ((ToolTip)TrackNameText.ToolTip).DataContext = n.Track;
            } else {
                TrackNameText.Foreground = _buttonTextPressed;
                TrackNameText.Background = _buttonBackgroundPressed;
                TrackNameText.Text = n.TrackId;
            }
        }

        private static readonly List<TextBlock> SessionsPool = new List<TextBlock>(20);

        private void UpdateSession(TextBlock child, ServerEntry.Session session) {
            child.DataContext = session;
            child.Text = session.DisplayTypeShort;
            if (session.IsActive) {
                child.Foreground = _buttonTextPressed;
                child.Background = _buttonBackgroundPressed;
            } else {
                child.Foreground = _buttonText;
                child.Background = _buttonBackground;
            }
        }

        private void UpdateSessions(ServerEntry n) {
            var children = SessionsPanel.Children;
            for (var i = children.Count - n.Sessions.Count; i > 0; i--) {
                var last = children.Count - 1;
                if (SessionsPool.Count < 20) {
                    SessionsPool.Add((TextBlock)children[last]);
                }
                children.RemoveAt(last);
            }

            for (var i = 0; i < n.Sessions.Count; i++) {
                TextBlock child;
                if (i < children.Count) {
                    child = (TextBlock)children[i];
                } else {
                    if (SessionsPool.Count > 0) {
                        var last = SessionsPool.Count - 1;
                        child = SessionsPool[last];
                        SessionsPool.RemoveAt(last);
                    } else {
                        /* Foreground="{DynamicResource ButtonText}" Style="{StaticResource Label}" Background="{DynamicResource ButtonBackground}" Height="20" Width="20" TextAlignment="Center" Padding="0 2 0 0" */
                        child = new TextBlock {
                            TextAlignment = TextAlignment.Center,
                            Style = (Style)FindResource(@"Label"),
                            Padding = new Thickness(0, 2, 0, 0),
                            Height = 20d,
                            Width = 20d,
                            ToolTip = FindResource(@"SessionItemTooltip")
                        };
                    }

                    children.Add(child);
                }

                UpdateSession(child, n.Sessions[i]);
            }
        }

        private static readonly List<TextBlock> CarsPool = new List<TextBlock>(50);

        private void UpdateCar(TextBlock child, ServerEntry.CarOrOnlyCarIdEntry car) {
            var wrapper = car.CarObjectWrapper;
            if (wrapper != null) {
                child.Foreground = _buttonText;
                child.Background = _buttonBackground;
                child.SetBinding(TextBlock.TextProperty, new Binding {
                    Source = wrapper,
                    Path = new PropertyPath($@"{nameof(wrapper.Value)}.{nameof(wrapper.Value.DisplayName)}"),
                    Mode = BindingMode.OneWay
                });
            } else {
                child.Foreground = _buttonTextPressed;
                child.Background = _buttonBackgroundPressed;
                child.ClearValue(TextBlock.TextProperty);
                child.Text = car.CarId;
            }
        }

        private void UpdateCars(ServerEntry n) {
            var children = CarsPanel.Children;
            // Logging.Warning($"children.Count={children.Count}, n.CarsOrTheirIds.Count={n.CarsOrTheirIds.Count}");
            for (var i = children.Count - n.CarsOrTheirIds.Count; i > 0; i--) {
                var last = children.Count - 1;
                if (CarsPool.Count < 50) {
                    // Logging.Debug("stored");
                    CarsPool.Add((TextBlock)children[last]);
                } else {
                    // Logging.Debug("full");
                }
                children.RemoveAt(last);
            }

            for (var i = 0; i < n.CarsOrTheirIds.Count; i++) {
                TextBlock child;
                if (i < children.Count) {
                    child = (TextBlock)children[i];
                } else {
                    if (CarsPool.Count > 0) {
                        // Logging.Debug("reuse");
                        var last = CarsPool.Count - 1;
                        child = CarsPool[last];
                        CarsPool.RemoveAt(last);
                    } else {
                        // Logging.Debug("new");
                        child = new TextBlock {
                            Style = (Style)FindResource(@"Small"),
                            Margin = new Thickness(4, 0, 4, 0),
                            Padding = new Thickness(2),
                            Height = 20d,
                            ToolTip = FindResource(@"CarPreviewTooltip")
                        };
                    }

                    children.Add(child);
                }

                UpdateCar(child, n.CarsOrTheirIds[i]);
            }
        }

        private void Update(ServerEntry n) {
            if (n == null) return;
            PasswordIconGroup.Value = n.PasswordRequired;
            HasErrorsGroup.Value = n.HasErrors;
            DisplayNameText.Text = n.DisplayName;
            CountryFlagImage.Source = CountryIdToImageConverter.Instance.Convert(n.CountryId);
            PingText.Text = n.Ping?.ToString() ?? @"?";
            DisplayClientsText.Text = n.DisplayClients;
            ErrorMessageGroup.BbCode = n.ErrorMessage;
            UpdateTrack(n);
            UpdateSessions(n);
            UpdateCars(n);
        }

        private void OnMouseEnter(object sender, MouseEventArgs e) {
            /*var n = Server;
            if (n != null) {
                UpdateCurrentDrivers(n, true);
            }*/
        }

        private void Handler(object sender, PropertyChangedEventArgs e) {
            var n = (ServerEntry)sender;
            switch (e.PropertyName) {
                case nameof(ServerEntry.DisplayName):
                    DisplayNameText.Text = n.DisplayName;
                    break;
                case nameof(ServerEntry.PasswordRequired):
                    PasswordIconGroup.Value = n.PasswordRequired;
                    break;
                case nameof(ServerEntry.HasErrors):
                    HasErrorsGroup.Value = n.HasErrors;
                    break;
                case nameof(ServerEntry.CountryId):
                    CountryFlagImage.Source = CountryIdToImageConverter.Instance.Convert(n.CountryId);
                    break;
                case nameof(ServerEntry.Ping):
                    PingText.Text = n.Ping?.ToString() ?? @"?";
                    break;
                case nameof(ServerEntry.DisplayClients):
                    DisplayClientsText.Text = n.DisplayClients;
                    break;
                case nameof(ServerEntry.ErrorMessage):
                    ErrorMessageGroup.BbCode = n.ErrorMessage;
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
