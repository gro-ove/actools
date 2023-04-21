using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FirstFloor.ModernUI.Windows {
    public sealed class ImmediateContentLoader : IContentLoader {
        public Task<object> LoadContentAsync(Uri uri, CancellationToken cancellationToken) {
            if (ModernUiHelper.IsInDesignMode) return null;

            if (Application.Current?.Dispatcher.CheckAccess() == false) {
                throw new InvalidOperationException(UiStrings.UIThreadRequired);
            }

            var loaded = Application.LoadComponent(uri);
            (loaded as IParametrizedUriContent)?.OnUri(uri);

            var loadable = loaded as ILoadableContent;
            if (loadable != null) {
                loadable.Load();
                loadable.Initialize();
            }

            return Task.FromResult(loaded);
        }
        
        public object LoadContent(Uri uri) {
            if (ModernUiHelper.IsInDesignMode) return null;

            var loaded = Application.LoadComponent(uri);
            (loaded as IParametrizedUriContent)?.OnUri(uri);

            var loadable = loaded as ILoadableContent;
            if (loadable != null) {
                loadable.Load();
                loadable.Initialize();
            }

            return loaded;
        }
    }
}