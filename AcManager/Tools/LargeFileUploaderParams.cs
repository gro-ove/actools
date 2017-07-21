using AcManager.Controls.ViewModels;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools {
    public static class LargeFileUploaderParams {
        public static UploaderParams Sharing { get; } = new UploaderParams(ValuesStorage.Storage);
        public static UploaderParams ServerPresets { get; } = new UploaderParams(ValuesStorage.Storage.GetSubstorage("_lfu:serverPresets_"));
    }
}