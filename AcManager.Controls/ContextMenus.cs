using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public interface IContextMenusProvider {
        void SetCarObjectMenu([NotNull] ContextMenu menu, [NotNull] CarObject car, [CanBeNull] CarSkinObject skin);
        void SetCarSkinObjectMenu([NotNull] ContextMenu menu, [NotNull] CarSkinObject skin);
        void SetTrackObjectMenu([NotNull] ContextMenu menu, [NotNull] TrackObjectBase track);
        void SetWeatherObjectMenu([NotNull] ContextMenu menu, [NotNull] WeatherObject weather);
        void SetCupUpdateMenu([NotNull] ContextMenu menu, [NotNull] ICupSupportedObject obj);
    }

    public class ContextMenusItems : Collection<object>{}

    public class ContextMenus {
        public static IContextMenusProvider ContextMenusProvider { get; set; }

        public static IList GetAdditionalItems(DependencyObject obj) {
            var collection = (IList)obj.GetValue(AdditionalItemsProperty);
            if (collection == null) {
                collection = new List<object>();
                obj.SetValue(AdditionalItemsProperty, collection);
            }

            return collection;
        }

        public static void SetAdditionalItems(DependencyObject obj, IList value) {
            obj.SetValue(AdditionalItemsProperty, value);
        }

        public static readonly DependencyProperty AdditionalItemsProperty = DependencyProperty.RegisterAttached("AdditionalItems", typeof(IList),
                typeof(ContextMenus), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));


        public static CarObject GetCar(DependencyObject obj) {
            return (CarObject)obj.GetValue(CarProperty);
        }

        public static void SetCar(DependencyObject obj, CarObject value) {
            obj.SetValue(CarProperty, value);
        }

        public static readonly DependencyProperty CarProperty = DependencyProperty.RegisterAttached("Car", typeof(CarObject),
                typeof(ContextMenus), new UIPropertyMetadata(OnContextMenuChanged));

        public static CarSkinObject GetCarSkin(DependencyObject obj) {
            return (CarSkinObject)obj.GetValue(CarSkinProperty);
        }

        public static void SetCarSkin(DependencyObject obj, CarSkinObject value) {
            obj.SetValue(CarSkinProperty, value);
        }

        public static readonly DependencyProperty CarSkinProperty = DependencyProperty.RegisterAttached("CarSkin", typeof(CarSkinObject),
                typeof(ContextMenus), new UIPropertyMetadata(OnContextMenuChanged));

        public static TrackObjectBase GetTrack(DependencyObject obj) {
            return (TrackObjectBase)obj.GetValue(TrackProperty);
        }

        public static void SetTrack(DependencyObject obj, TrackObjectBase value) {
            obj.SetValue(TrackProperty, value);
        }

        public static readonly DependencyProperty TrackProperty = DependencyProperty.RegisterAttached("Track", typeof(TrackObjectBase),
                typeof(ContextMenus), new UIPropertyMetadata(OnContextMenuChanged));

        public static WeatherObject GetWeather(DependencyObject obj) {
            return (WeatherObject)obj.GetValue(WeatherProperty);
        }

        public static void SetWeather(DependencyObject obj, WeatherObject value) {
            obj.SetValue(WeatherProperty, value);
        }

        public static readonly DependencyProperty WeatherProperty = DependencyProperty.RegisterAttached("Weather", typeof(WeatherObject),
                typeof(ContextMenus), new UIPropertyMetadata(OnContextMenuChanged));

        public static ICupSupportedObject GetCupUpdate(DependencyObject obj) {
            return (ICupSupportedObject)obj.GetValue(CupUpdateProperty);
        }

        public static void SetCupUpdate(DependencyObject obj, ICupSupportedObject value) {
            obj.SetValue(CupUpdateProperty, value);
        }

        public static readonly DependencyProperty CupUpdateProperty = DependencyProperty.RegisterAttached("CupUpdate", typeof(ICupSupportedObject),
                typeof(ContextMenus), new UIPropertyMetadata(OnContextMenuChanged));

        private static void OnContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is FrameworkElement element && e.NewValue != null) {
                ResetContextMenu(element);
            }
        }

        private static void ResetContextMenu(FrameworkElement element) {
            SetIsDirty(element, true);
            element.MouseEnter -= OnElementMouseEnter;
            element.MouseEnter += OnElementMouseEnter;
            element.PreviewGotKeyboardFocus -= OnElementMouseEnter;
            element.PreviewGotKeyboardFocus += OnElementMouseEnter;

            if (element.IsMouseOver) {
                UpdateContextMenu(element);
            }
        }

        public static AcObjectNew GetGenericObject(DependencyObject obj) {
            return (AcObjectNew)obj.GetValue(GenericObjectProperty);
        }

        public static void SetGenericObject(DependencyObject obj, AcObjectNew value) {
            obj.SetValue(GenericObjectProperty, value);
        }

        public static readonly DependencyProperty GenericObjectProperty = DependencyProperty.RegisterAttached("GenericObject", typeof(AcObjectNew),
                typeof(ContextMenus), new UIPropertyMetadata(OnGenericObjectChanged));

        private static void OnGenericObjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            switch (e.NewValue) {
                case CarObject o:
                    SetCar(d, o);
                    break;
                case TrackObjectBase o:
                    SetTrack(d, o);
                    break;
            }
        }

        private static bool GetIsDirty(DependencyObject obj) {
            return obj.GetValue(IsDirtyProperty) as bool? == true;
        }

        private static void SetIsDirty(DependencyObject obj, bool value) {
            obj.SetValue(IsDirtyProperty, value);
        }

        public static readonly DependencyProperty IsDirtyProperty = DependencyProperty.RegisterAttached("IsDirty", typeof(bool),
                typeof(ContextMenus), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        private static ContextMenu CreateContextMenu([CanBeNull] FrameworkElement element, [CanBeNull] Action<ContextMenu> action) {
            var menu = new ContextMenu();

            var list = element?.GetValue(AdditionalItemsProperty) as IList;
            if (list?.Count > 0) {
                foreach (var o in list) {
                    if (o is FrameworkElement fe) {
                        fe.DataContext = element.DataContext;
                        switch (fe.Parent) {
                            case ItemsControl itemsControl:
                                itemsControl.Items.Remove(fe);
                                break;
                            case Panel panel:
                                panel.Children.Remove(fe);
                                break;
                        }
                    }

                    menu.Items.Add(o);
                }

                menu.Items.Add(new Separator());
            }

            action?.Invoke(menu);
            return menu.Items.Count != 0 ? menu : null;
        }

        [CanBeNull]
        public static ContextMenu GetCarContextMenu([CanBeNull] FrameworkElement element, [NotNull] CarObject car, [CanBeNull] CarSkinObject carSkin) {
            return CreateContextMenu(element, menu => ContextMenusProvider?.SetCarObjectMenu(menu, car, carSkin));
        }

        [CanBeNull]
        public static ContextMenu GetCarSkinContextMenu([CanBeNull] FrameworkElement element, [NotNull] CarSkinObject carSkin) {
            return CreateContextMenu(element, menu => ContextMenusProvider?.SetCarSkinObjectMenu(menu, carSkin));
        }

        [CanBeNull]
        public static ContextMenu GetTrackContextMenu([CanBeNull] FrameworkElement element, [NotNull] TrackObjectBase track) {
            return CreateContextMenu(element, menu => ContextMenusProvider?.SetTrackObjectMenu(menu, track));
        }

        [CanBeNull]
        public static ContextMenu GetWeatherContextMenu([CanBeNull] FrameworkElement element, [NotNull] WeatherObject weather) {
            return CreateContextMenu(element, menu => ContextMenusProvider?.SetWeatherObjectMenu(menu, weather));
        }

        [CanBeNull]
        public static ContextMenu GetCupUpdateMenu([CanBeNull] FrameworkElement element, [NotNull] ICupSupportedObject obj) {
            return CreateContextMenu(element, menu => ContextMenusProvider?.SetCupUpdateMenu(menu, obj));
        }

        private static void SetContextMenu(FrameworkElement obj, [CanBeNull] ContextMenu t, object dataContext) {
            if (t == null) {
                obj.ContextMenu = null;
                return;
            }

            t.DataContext = dataContext;
            obj.ContextMenu = t;
        }

        private static void UpdateContextMenu(FrameworkElement element) {
            if (!GetIsDirty(element)) return;
            SetIsDirty(element, false);

            var car = GetCar(element);
            if (car != null) {
                SetContextMenu(element, GetCarContextMenu(element, car, GetCarSkin(element) ?? car.SelectedSkin), car);
                return;
            }

            var skin = GetCarSkin(element);
            if (skin != null) {
                SetContextMenu(element, GetCarSkinContextMenu(element, skin), skin);
                return;
            }

            var track = GetTrack(element);
            if (track != null) {
                SetContextMenu(element, GetTrackContextMenu(element, track), track);
                return;
            }

            var weather = GetWeather(element);
            if (weather != null) {
                SetContextMenu(element, GetWeatherContextMenu(element, weather), weather);
                return;
            }

            var cupUpdate = GetCupUpdate(element);
            if (cupUpdate != null) {
                SetContextMenu(element, GetCupUpdateMenu(element, cupUpdate), cupUpdate);
                return;
            }
        }

        private static void OnElementMouseEnter(object sender, EventArgs mouseEventArgs) {
            UpdateContextMenu((FrameworkElement)sender);
        }
    }
}