using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FirstFloor.ModernUI.Windows {
    /// <summary>
    /// Loads XAML files using Application.LoadComponent.
    /// </summary>
    public class DefaultContentLoader : IContentLoader {
        /// <summary>
        /// Asynchronously loads content from specified uri.
        /// </summary>
        /// <param name="uri">The content uri.</param>
        /// <param name="cancellationToken">The token used to cancel the load content task.</param>
        /// <returns>The loaded content.</returns>
        public async Task<object> LoadContentAsync(Uri uri, CancellationToken cancellationToken) {
            if (ModernUiHelper.IsInDesignMode) return null;

            if (!Application.Current.Dispatcher.CheckAccess()) {
                throw new InvalidOperationException(Resources.UIThreadRequired);
            }

            // scheduler ensures LoadContent is executed on the current UI thread
            await Task.Delay(10, cancellationToken);

            var loaded = Application.LoadComponent(uri);
            (loaded as IParametrizedUriContent)?.OnUri(uri);

            var loadable = loaded as ILoadableContent;
            if (loadable == null) return loaded;

            await loadable.LoadAsync(cancellationToken);
            loadable.Initialize();
            return loadable;
        }

        /// <summary>
        /// Loads the content from specified uri.
        /// </summary>
        /// <param name="uri">The content uri</param>
        /// <returns>The loaded content.</returns>
        public virtual object LoadContent(Uri uri) {
            if (ModernUiHelper.IsInDesignMode) return null;

            var loaded = Application.LoadComponent(uri);
            (loaded as IParametrizedUriContent)?.OnUri(uri);

            var loadable = loaded as ILoadableContent;
            if (loadable == null) return loaded;

            loadable.Load();
            loadable.Initialize();
            return loadable;
        }
    }
}
