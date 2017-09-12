using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls.ViewModels;
using AcManager.LargeFilesSharing;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    public partial class UploaderBlock {
        [CanBeNull]
        private UploaderParams Model => DataContext as UploaderParams;

        private readonly SizeRelatedCondition<UploaderBlock, double> _sizeRelatedCondition;

        public UploaderBlock() {
            InitializeComponent();
            _sizeRelatedCondition = this.AddSizeCondition(x => (x.ActualWidth - 640) / 400)
                                        .Add(x => {
                                            if (UploaderParamsSwitch.Visibility == Visibility.Visible && Model?.SelectedUploader.IsReady != true) {
                                                var v = x > 0d ? 200 * (x.Saturate() + 1.5) : double.NaN;
                                                UploaderParams.Width = v;
                                                UploaderParamsSwitch.Width = Math.Max(double.IsNaN(v) ? ActualWidth - 144d : v - 124d, 1d);
                                                UploaderParams.SetValue(DockPanel.DockProperty, x > 0d ? Dock.Right : Dock.Bottom);
                                                UploaderParams.Margin = x > 0d ? new Thickness(4, 0, 0, 0) : new Thickness(0, 8, 0, 0);
                                            } else {
                                                UploaderParams.Width = 120d;
                                                UploaderParams.SetValue(DockPanel.DockProperty, Dock.Right);
                                                UploaderParams.Margin = new Thickness(4, 0, 0, 0);
                                            }
                                        })
                                        .ListenOnProperty(UploaderParams, BooleanSwitch.ValueProperty)
                                        .ListenOnProperty(UploaderParamsSwitch, IsVisibleProperty);
            Loaded += OnLoaded;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(Model.UploaderDirectories):
                    break;

                case nameof(Model.UploaderDirectory):
                    UploaderDirectoriesTreeView.SetSelectedItem(Model?.UploaderDirectory);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            var model = Model;
            if (model != null) {
                model.PropertyChanged += OnPropertyChanged;
                model.Prepare().Forget();
                _sizeRelatedCondition.ListenOnProperty(model, nameof(model.SelectedUploader));
            }
        }

        private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            var model = Model;
            if (model == null) return;
            model.UploaderDirectory = UploaderDirectoriesTreeView.SelectedItem as DirectoryEntry ?? model.UploaderDirectories?.FirstOrDefault();
        }
    }
}
