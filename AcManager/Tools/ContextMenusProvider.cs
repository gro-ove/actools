using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Internal;
using AcManager.Pages.Drive;
using AcManager.Pages.Lists;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools {
    public class ContextMenusProvider : IContextMenusProvider {
        public void SetCarObjectMenu(ContextMenu menu, CarObject car, CarSkinObject skin) {
            menu.AddItem("Manage setups", () => CarSetupsListPage.Open(car))
                    .AddItem("Manage skins", () => CarSkinsListPage.Open(car))
                    .AddSeparator();
            CarBlock.OnShowroomContextMenu(menu, car, skin);

            menu.AddSeparator();

            var ratingBar = new RatingBar { Rating = car.Rating ?? 0 };
            ratingBar.SetBinding(RatingBar.RatingProperty, new Binding("Rating") { Source = car });
            menu.AddItem(new MenuItem {
                StaysOpenOnClick = true,
                Header = new DockPanel {
                    Margin = new Thickness(0d, 0d, -40d, 0d),
                    Children = {
                        new TextBlock { Text = "Rating:", Width = 80 },
                        ratingBar,
                        new FavouriteButton { IsChecked = car.IsFavourite }
                    }
                }
            });
            menu.AddItem(new MenuItem {
                StaysOpenOnClick = true,
                Header = new DockPanel {
                    Margin = new Thickness(0d, 0d, -40d, 0d),
                    Children = {
                        new TextBlock { Text = "Notes:", Width = 80 },
                        new NotesBlock { AcObject = car }
                    }
                }
            });

            menu.AddSeparator();

            if (!QuickDrive.IsActive()) {
                menu.AddItem("Open car in Quick Drive", () => QuickDrive.Show(car, skin?.Id));
            }

            menu.AddItem("Open car in Content tab", () => CarsListPage.Show(car, skin?.Id))
                    .AddItem(AppStrings.Toolbar_Folder, car.ViewInExplorer);
        }

        public void SetCarSkinObjectMenu(ContextMenu menu, CarSkinObject skin) { }

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

            menu.AddItem("Manage skins", () => TrackSkinsListPage.Open(track.MainTrackObject));

            menu.AddSeparator();

            var ratingBar = new RatingBar { Rating = track.MainTrackObject.Rating ?? 0 };
            ratingBar.SetBinding(RatingBar.RatingProperty, new Binding("Rating") { Source = track.MainTrackObject });
            menu.AddItem(new MenuItem {
                StaysOpenOnClick = true,
                Header = new DockPanel {
                    Margin = new Thickness(0d, 0d, -40d, 0d),
                    Children = {
                        new TextBlock { Text = "Rating:", Width = 80 },
                        ratingBar,
                        new FavouriteButton { IsChecked = track.MainTrackObject.IsFavourite }
                    }
                }
            });
            menu.AddItem(new MenuItem {
                StaysOpenOnClick = true,
                Header = new DockPanel {
                    Margin = new Thickness(0d, 0d, -40d, 0d),
                    Children = {
                        new TextBlock { Text = "Notes:", Width = 80 },
                        new NotesBlock { AcObject = track.MainTrackObject }
                    }
                }
            });

            menu.AddSeparator();

            if (!QuickDrive.IsActive()) {
                menu.AddItem("Open track in Quick Drive", () => QuickDrive.Show(track: track));
            }

            menu.AddItem("Open track in Content tab", () => TracksListPage.Show(track), isEnabled: InternalUtils.IsAllRight)
                    .AddItem(AppStrings.Toolbar_Folder, track.ViewInExplorer);
        }

        public void SetWeatherObjectMenu(ContextMenu menu, WeatherObject weather) {
            throw new NotImplementedException();
        }

        private static SharedResourceDictionary Icons = new SharedResourceDictionary {
            Source = new Uri("/AcManager.Controls;component/Assets/IconData.xaml", UriKind.Relative)
        };

        public void SetCupUpdateMenu(ContextMenu menu, ICupSupportedObject obj) {
            var client = CupClient.Instance;
            var information = obj.CupUpdateInformation;
            if (client == null || information == null) return;

            if (information.IsToUpdateManually) {
                menu.AddItem("Download and install update", new AsyncCommand(() =>
                        client.InstallUpdateAsync(obj.CupContentType, obj.Id)),
                        iconData: (Geometry)Icons["UpdateIconData"]);
                menu.AddItem("Download update",
                        new AsyncCommand(async () => { WindowsHelper.ViewInBrowser(await client.GetUpdateUrlAsync(obj.CupContentType, obj.Id)); }));
                menu.AddSeparator();
            } else {
                menu.AddItem("Get update", new AsyncCommand(async () => {
                    WindowsHelper.ViewInBrowser(await client.GetUpdateUrlAsync(obj.CupContentType, obj.Id)
                            ?? obj.CupUpdateInformation?.InformationUrl);
                }));
                menu.AddSeparator();
            }

            menu.AddItem("View information", () => new CupInformationDialog(obj).ShowDialog());

            if (!string.IsNullOrWhiteSpace(information.InformationUrl)) {
                menu.AddItem("Open information page", () => { WindowsHelper.ViewInBrowser(information.InformationUrl); });
            }

            menu.AddSeparator()
                    .AddItem("Ignore update", () => client.IgnoreUpdate(obj.CupContentType, obj.Id))
                    .AddItem("Ignore all updates", () => client.IgnoreAllUpdates(obj.CupContentType, obj.Id))
                    .AddItem("Report update as broken", new AsyncCommand(() => client.ReportUpdateAsync(obj.CupContentType, obj.Id)));
        }
    }
}