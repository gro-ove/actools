using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.ContentRepair;
using AcManager.Controls.Dialogs;
using AcManager.CustomShowroom;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.ContentRepairUi;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataAnalyzer;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Data;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.ContentTools {
    public partial class CupList {
        protected override async Task<bool> LoadOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            progress.Report("Loading list of updates…", 0.01);
            await CupClient.Instance.LoadRegistries();

            var list = CupClient.Instance.List.Select(x => {
                var m = CupClient.Instance.GetAssociatedManager(x.Key.Type);
                return (ICupSupportedObject)m?.GetObjectById(x.Key.Id);
            }).Where(x => x?.IsCupUpdateAvailable == true).OrderBy(x => x.CupContentType).ToList();
            ItemsToUpdate = new BetterObservableCollection<ICupSupportedObject>(list);
            SelectedItem = list.FirstOrDefault();
            return list.Count > 0;

            // throw new NotImplementedException();
        }

        protected override void InitializeOverride(Uri uri) {
            // throw new NotImplementedException();
        }

        private BetterObservableCollection<ICupSupportedObject> _itemsToUpdate;

        public BetterObservableCollection<ICupSupportedObject> ItemsToUpdate {
            get => _itemsToUpdate;
            set {
                if (Equals(value, _itemsToUpdate)) return;
                _itemsToUpdate = value;
                OnPropertyChanged();
            }
        }

        private ICupSupportedObject _selectedItem;

        public ICupSupportedObject SelectedItem {
            get => _selectedItem;
            set {
                if (Equals(value, _selectedItem)) return;
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        #region As a separate tool
        private static WeakReference<ModernDialog> _analyzerDialog;

        public static void Run() {
            if (_analyzerDialog != null && _analyzerDialog.TryGetTarget(out ModernDialog dialog)) {
                dialog.Close();
            }

            dialog = new ModernDialog {
                ShowTitle = false,
                Title = "Content Updates",
                SizeToContent = SizeToContent.Manual,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                LocationAndSizeKey = @"lsContentUpdates",
                MinWidth = 800,
                MinHeight = 480,
                Width = 800,
                Height = 640,
                MaxWidth = 99999,
                MaxHeight = 99999,
                Content = new ModernFrame {
                    Source = UriExtension.Create("/Pages/ContentTools/CupList.xaml")
                }
            };

            dialog.Show();
            _analyzerDialog = new WeakReference<ModernDialog>(dialog);
        }
        #endregion
    }
}
