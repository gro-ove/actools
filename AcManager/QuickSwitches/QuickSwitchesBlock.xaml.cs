using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AcManager.Pages.Windows;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Attached;

namespace AcManager.QuickSwitches {
    public partial class QuickSwitchesBlock {
        public QuickSwitchesBlock() {
            InitializeComponent();
            Rebuild();
            SettingsHolder.Drive.PropertyChanged += OnDrivePropertyChanged;
        }

        private void OnDrivePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.Drive.QuickSwitches) ||
                    e.PropertyName == nameof(SettingsHolder.Drive.QuickSwitchesList)) {
                Rebuild();
            }
        }

        public IEnumerable<FrameworkElement> Items => List.Items.OfType<FrameworkElement>();

        private void Rebuild() {
            List.Items.Clear();
            if (!SettingsHolder.Drive.QuickSwitches) return;

            var active = SettingsHolder.Drive.QuickSwitchesList;
            foreach (var key in active) {
                FrameworkElement item;
                try {
                    item = (FrameworkElement)FindResource(key);
                } catch (Exception e) {
                    Logging.Write("Can’t find widget: " + key + ", " + e);
                    continue;
                }

                List.Items.Add(item);
            }
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e) {
            (Application.Current?.MainWindow as MainWindow)?.CloseQuickSwitches();
        }

        public static bool GetIsActive(FrameworkElement obj) {
            return obj.IsFocused;
        }

        public static void SetIsActive(FrameworkElement obj, bool value) {
            if (value) {
                obj.Focus();
            } else {
                obj.RemoveFocus();
            }
        }

    }
}
