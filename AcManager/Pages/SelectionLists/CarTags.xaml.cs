using System;
using System.Windows;
using System.Windows.Controls;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.SelectionLists {
    public partial class CarTags {
        public CarTags() : base(CarsManager.Instance) {
            InitializeComponent();
        }

        protected override Uri GetPageAddress(SelectTag category) {
            return SelectCarDialog.TagUri(category.TagValue);
        }

        protected override bool IsIgnored(CarObject obj, string tagValue) {
            return string.Equals(obj.Brand, tagValue, StringComparison.OrdinalIgnoreCase);
        }

        // For testing StretchyWrapPanel:
        /*private StretchyWrapPanel Panel => this.FindVisualChild<StretchyWrapPanel>();

        private void OnRearrangeForBestFitChecked(object sender, RoutedEventArgs e) {
            Panel.RearrangeForBestFit = ((CheckBox)sender).IsChecked == true;
        }

        private void OnStretchToFillChecked(object sender, RoutedEventArgs e) {
            Panel.StretchToFill = ((CheckBox)sender).IsChecked == true;
        }

        private void OnStretchProportionallyChecked(object sender, RoutedEventArgs e) {
            Panel.StretchProportionally = ((CheckBox)sender).IsChecked == true;
        }

        private void OnFixedWidthChecked(object sender, RoutedEventArgs e) {
            Panel.ItemWidth = ((CheckBox)sender).IsChecked == true ? 80 : double.NaN;
        }*/
    }
}
