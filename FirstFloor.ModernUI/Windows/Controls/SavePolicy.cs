namespace FirstFloor.ModernUI.Windows.Controls {
    public enum SavePolicy {
        /// <summary>
        /// Save or load only when URI is in one of original links.
        /// </summary>
        Strict,

        /// <summary>
        /// Save or load even if there is no link with that URI.
        /// </summary>
        Flexible,

        /// <summary>
        /// Do not load URI, but instead use current Source value.
        /// </summary>
        SkipLoading,

        /// <summary>
        /// Do not load URI, but instead use current Source value, even if there is no link with that URI.
        /// </summary>
        SkipLoadingFlexible
    }
}