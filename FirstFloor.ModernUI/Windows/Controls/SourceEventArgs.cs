using System;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// Provides data for events related to uri sources.
    /// </summary>
    public class SourceEventArgs : EventArgs {
        /// <summary>
        /// Initializes a new instance of the <see cref="SourceEventArgs"/> class.
        /// </summary>
        /// <param name="source"></param>
        public SourceEventArgs(Uri source) {
            Source = source;
        }

        /// <summary>
        /// Gets the source uri.
        /// </summary>
        public Uri Source { get; }
    }
}
