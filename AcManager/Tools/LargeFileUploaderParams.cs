using AcManager.Controls.ViewModels;
using AcManager.Tools.Data;

namespace AcManager.Tools {
    public static class LargeFileUploaderParams {
        private static UploaderParams _shareReplays;
        public static UploaderParams ShareReplays => _shareReplays ?? (_shareReplays = new UploaderParams(AuthenticationStorage.ShareReplaysStorage));

        private static UploaderParams _shareContent;
        public static UploaderParams ShareContent => _shareContent ?? (_shareContent = new UploaderParams(AuthenticationStorage.ShareContentStorage));
    }
}