using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Controls;
using AcManager.Internal;
using AcManager.Pages.Drive;
using AcManager.Pages.Lists;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools {
    public class ContextMenusProvider : IContextMenusProvider {
        public void SetCarObjectMenu(ContextMenu menu, CarObject car, CarSkinObject skin) {
            menu.AddItem("Manage setups", () => CarSetupsListPage.Open(car))
                .AddItem("Manage skins", () => CarSkinsListPage.Open(car))
                .AddSeparator();
            CarBlock.OnShowroomContextMenu(menu, car, skin);

            menu.AddSeparator();

            if (!QuickDrive.IsActive()) {
                menu.AddItem("Open car in Quick Drive", () => QuickDrive.Show(car, skin?.Id));
            }

            menu.AddItem("Open car in Content tab", () => CarsListPage.Show(car, skin?.Id))
                .AddItem(AppStrings.Toolbar_Folder, car.ViewInExplorer);
        }

        public void SetCarSkinObjectMenu(ContextMenu menu, CarSkinObject skin) {}

        public void SetTrackObjectMenu(ContextMenu menu, TrackObjectBase track) {
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

            if (!QuickDrive.IsActive()) {
                menu.AddItem("Open track in Quick Drive", () => QuickDrive.Show(track: track));
            }

            menu.AddItem("Open track in Content tab", () => TracksListPage.Show(track), isEnabled: AppKeyHolder.IsAllRight)
                .AddItem(AppStrings.Toolbar_Folder, track.ViewInExplorer);
        }
    }
}