using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Wheel_System : INotifyPropertyChanged {
        public AcSettingsControls_Wheel_System() {
            InitializeComponent();
            this.AddWidthCondition(800).Add(x => {
                // Resources["ItemsPanelTemplate"] = TryFindResource(x ? "TwoColumnsPanel" : "OneColumnPanel");
                MainGrid.Columns = x ? 2 : 1;
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            AcSettingsHolder.Controls.ClearWaiting();
        }

        private readonly Dictionary<string, Storyboard> _storyboards = new Dictionary<string, Storyboard>();

        private DelegateCommand<string> _highlightDetailsCommand;

        public DelegateCommand<string> HighlightDetailsCommand => _highlightDetailsCommand ?? (_highlightDetailsCommand = new DelegateCommand<string>(name => {
            var p = this.FindVisualChildren<Border>().FirstOrDefault(x => x.Name == name);
            if (p == null) return;

            var d = new Duration(TimeSpan.FromSeconds(0.15));
            var c = (TryFindResource(@"AccentColor") as Color? ?? Colors.White).SetAlpha(80);

            if (_storyboards.TryGetValue(name, out var u)) {
                u.Stop();
            } else if (p.Background == null) {
                p.Background = new SolidColorBrush(c.SetAlpha(0));
            }

            u = new Storyboard {
                Children = {
                    new ColorAnimation { To = c, Duration = d },
                    new ColorAnimation { To = c.SetAlpha(0), Duration = d, BeginTime = TimeSpan.FromSeconds(0.35) }
                }
            };

            Storyboard.SetTargetProperty(u, new PropertyPath(@"(0).(1)", Border.BackgroundProperty, SolidColorBrush.ColorProperty));
            p.BeginStoryboard(u);
            _storyboards[name] = u;
        }));

        public event PropertyChangedEventHandler PropertyChanged {
            add { }
            remove { }
        }
    }
}