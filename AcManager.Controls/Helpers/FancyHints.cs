using FirstFloor.ModernUI.Windows.Attached;

namespace AcManager.Controls.Helpers {
    public static class FancyHints {
        public static readonly FancyHint DragForContentSection = new FancyHint(
                "drag-for-content-section",
                "Drag’n’drop cars here to open them in Content section",
                "Works also with tracks, and the other way around — from Drive section to Content.",
                startupsDelay: 3, probability: 0.8);

        public static readonly FancyHint MoreDriveAssists = new FancyHint(
                "more-drive-assists",
                "Press those three dots to adjust assists",
                "Usually, some extra popup options are marked like this.",
                startupsDelay: 1, probability: 0.6, triggersDelay: 1);

        public static readonly FancyHint DoYouKnowAboutAndroid = new FancyHint(
                        "android-thing",
                        "Only first three numbers matter",
                        "Last number is a development build number, and second to last is release build number.[br][br]By the way, do you know that in Android, its build number usually has a little trick?",
                        startupsDelay: 3, probability: 0.4);

        public static readonly FancyHint DoubleSlider = new FancyHint(
                "double-slider",
                "This is a double slider allowing to specify a range",
                "To make a range, either click somewhere next to moving thing or move mouse carefully over it and pull out range marks.",
                startupsDelay: 0, forced: true);
    }
}