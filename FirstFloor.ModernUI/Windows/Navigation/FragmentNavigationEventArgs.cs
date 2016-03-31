using System;

namespace FirstFloor.ModernUI.Windows.Navigation {
    /// <summary>
    /// Provides data for fragment navigation events.
    /// </summary>
    public class FragmentNavigationEventArgs
        : EventArgs {
        /// <summary>
        /// Gets the uniform resource identifier (URI) fragment.
        /// </summary>
        public string Fragment { get; internal set; }
    }
}
