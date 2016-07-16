namespace FirstFloor.ModernUI.Windows.Navigation {
    /// <summary>
    /// Identifies the types of navigation that are supported.
    /// </summary>
    public enum NavigationType {
        /// <summary>
        /// Navigating to new content.
        /// </summary>
        New,
        /// <summary>
        /// Navigating back in the back navigation history.
        /// </summary>
        Back,
        /// <summary>
        /// Navigating forward in the forward navigation future.
        /// </summary>
        Forward,
        /// <summary>
        /// Reloading the current content.
        /// </summary>
        Refresh
    }
}
