using System.Windows;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Windows.Attached;

namespace AcManager.Pages.ServerPreset {
    public class ServerPresetDriverEntryDraggableConverter : IDraggableDestinationConverter {
        object IDraggableDestinationConverter.Convert(IDataObject data) {
            if (data.GetData(ServerPresetDriverEntry.DraggableFormat) is ServerPresetDriverEntry entry) return entry;
            if (data.GetData(CarObject.DraggableFormat) is CarObject car) return new ServerPresetDriverEntry(car);
            if (data.GetData(ServerSavedDriver.DraggableFormat) is ServerSavedDriver saved) return new ServerPresetDriverEntry(saved);
            return null;
        }
    }
}