using AcManager.Tools.Helpers.Loaders;
using AcTools.Utils.Helpers;

namespace AcManager.Tools {
    public class CmRequestHandler : ICmRequestHandler {
        public bool Test(string request) {
            return ArgumentsHandler.IsCustomUriScheme(request);
        }

        public string UnwrapDownloadUrl(string request) {
            return ArgumentsHandler.UnwrapDownloadRequest(request);
        }

        public void Handle(string request) {
            ArgumentsHandler.ProcessArguments(new[] { request }, true).Ignore();
        }
    }
}