namespace AcTools.Render.Kn5SpecificForwardDark {
    public static class DarkPreviewsUpdaterFactory {
        public static IDarkPreviewsUpdater Create(bool useAssettoCorsa, string acRoot, DarkPreviewsOptions options = null,
                DarkKn5ObjectRenderer existingRenderer = null) {
            return useAssettoCorsa
                    ? (IDarkPreviewsUpdater)new DarkPreviewsAcUpdater(acRoot, options) 
                    : new DarkPreviewsUpdater(acRoot, options, existingRenderer);
        }
    }
}