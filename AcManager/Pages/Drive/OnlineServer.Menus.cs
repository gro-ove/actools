using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls.UserControls;
using AcManager.Internal;
using AcManager.Pages.Lists;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.Drive {
    public partial class OnlineServer {
        [CanBeNull]
        private ContextMenu GetCarContextMenu() {
            var entry = Model.Entry.SelectedCarEntry;
            var car = entry?.CarObject;
            var skin = entry?.AvailableSkin;
            if (car == null) return null;

            var menu = new ContextMenu()
                    .AddItem("Manage setups", () => CarSetupsListPage.Open(car))
                    .AddItem("Manage skins", () => CarSkinsListPage.Open(car))
                    .AddSeparator();
            CarBlock.OnShowroomContextMenu(menu, car, skin);
            return menu.AddSeparator()
                .AddItem("Open car in Content tab", () => CarsListPage.Show(car, skin?.Id))
                .AddItem(AppStrings.Toolbar_Folder, () => car.ViewInExplorer());
        }

        private void OnCarContextMenu(object sender, ContextMenuButtonEventArgs e) {
            e.Menu = GetCarContextMenu();
        }

        private void OnCarRightButtonClick(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            GetCarContextMenu()?.SetValue(ContextMenu.IsOpenProperty, true);
        }

        [CanBeNull]
        private ContextMenu GetTrackContextMenu() {
            var track = Model.Entry.Track;
            if (track == null) return null;

            var menu = new ContextMenu();
            var mainTrack = track.MainTrackObject;
            mainTrack.SkinsManager.EnsureLoaded();
            if (mainTrack.EnabledOnlySkins.Count > 0) {
                foreach (var skinObject in mainTrack.EnabledOnlySkins) {
                    var item = new MenuItem {
                        Header = skinObject.DisplayName.ToTitle(),
                        IsCheckable = true,
                        StaysOpenOnClick = true,
                        ToolTip = skinObject.Description
                    };

                    item.SetBinding(MenuItem.IsCheckedProperty, new Binding {
                        Path = new PropertyPath(nameof(skinObject.IsActive)),
                        Source = skinObject
                    });

                    menu.Items.Add(item);
                }
                menu.AddSeparator();
            }

            return menu.AddItem("Open track in Content tab", () => {
                TracksListPage.Show(track);
            }, isEnabled: AppKeyHolder.IsAllRight)
                .AddItem(AppStrings.Toolbar_Folder, () => {
                    track.ViewInExplorer();
                });
        }

        private void OnTrackRightButtonClick(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            GetTrackContextMenu()?.SetValue(ContextMenu.IsOpenProperty, true);
        }

        private void OnTrackContextMenu(object sender, ContextMenuButtonEventArgs e) {
            e.Menu = GetTrackContextMenu();
        }
    }
}