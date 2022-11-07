using System;
using System.ComponentModel;
using AcManager.Controls.Helpers;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools {
    [Localizable(false)]
    public class UriProvider : IAcObjectsUriProvider {
        Uri IAcObjectsUriProvider.GetUri(AcObjectNew obj) {
            switch (obj?.GetType().Name) {
                case nameof(CarObject):
                    return UriExtension.Create(
                            SettingsHolder.Content.OldLayout ? "/Pages/Selected/SelectedCarPage.xaml?Id={0}" :
                                    "/Pages/Selected/SelectedCarPage_New.xaml?Id={0}",
                            obj.Id);

                case nameof(TrackObject):
                case nameof(TrackObjectBase):
                case nameof(TrackExtraLayoutObject):
                    return UriExtension.Create("/Pages/Selected/SelectedTrackPage.xaml?Id={0}", obj.Id);

                case nameof(ShowroomObject):
                    return UriExtension.Create("/Pages/Selected/SelectedShowroomPage.xaml?Id={0}", obj.Id);

                case nameof(WeatherObject):
                    return UriExtension.Create("/Pages/Selected/SelectedWeatherPage.xaml?Id={0}", obj.Id);

                case nameof(ReplayObject):
                    return UriExtension.Create("/Pages/Selected/SelectedReplayPage.xaml?Id={0}", obj.Id);

                case nameof(FontObject):
                    return UriExtension.Create("/Pages/Selected/SelectedFontPage.xaml?Id={0}", obj.Id);

                case nameof(DriverModelObject):
                    return UriExtension.Create("/Pages/Selected/SelectedDriverModelPage.xaml?Id={0}", obj.Id);

                case nameof(PpFilterObject):
                    return UriExtension.Create("/Pages/Selected/SelectedPpFilterPage.xaml?Id={0}", obj.Id);

                case nameof(LuaAppObject):
                    return UriExtension.Create("/Pages/Selected/SelectedLuaAppPage.xaml?Id={0}", obj.Id);

                case nameof(PythonAppObject):
                    return UriExtension.Create("/Pages/Selected/SelectedPythonAppPage.xaml?Id={0}", obj.Id);

                case nameof(UserChampionshipObject):
                    return UriExtension.Create("/Pages/Selected/SelectedUserChampionship.xaml?Id={0}", obj.Id);

                case nameof(CarSkinObject):
                    return UriExtension.Create("/Pages/Selected/SelectedCarSkinPage.xaml?Id={0}&CarId={1}", obj.Id, ((CarSkinObject)obj).CarId);

                case nameof(CarSetupObject):
                    return UriExtension.Create("/Pages/Selected/SelectedCarSetupPage.xaml?Id={0}&CarId={1}", obj.Id, ((CarSetupObject)obj).CarId);

                case nameof(TrackSkinObject):
                    return UriExtension.Create("/Pages/Selected/SelectedTrackSkinPage.xaml?Id={0}&TrackId={1}", obj.Id, ((TrackSkinObject)obj).TrackId);

                case nameof(RemoteCarSetupObject):
                    return UriExtension.Create("/Pages/Selected/SelectedRemoteCarSetupPage.xaml?Id={0}&CarId={1}&RemoteSource={2}", obj.Id,
                            ((RemoteCarSetupObject)obj).CarId, ((RemoteCarSetupObject)obj).Source);

                case nameof(ServerPresetObject):
                    return UriExtension.Create("/Pages/ServerPreset/SelectedPage.xaml?Id={0}", obj.Id);
            }

            throw new NotImplementedException("Not supported type: " + obj?.GetType().Name);
        }
    }
}