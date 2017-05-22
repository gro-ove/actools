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
                "To make a range, either click somewhere next to handle or move mouse carefully over it and pull out range marks.",
                #if DEBUG
                startupsDelay: int.MaxValue,
                #else
                startupsDelay: 0,
                #endif
                forced: true);

        public static readonly FancyHint DegressWind = new FancyHint(
                "degress-wind",
                "If needed, you can set a specific wind direction",
                "To do so, switch the mode in context menu.",
                startupsDelay: 4, probability: 0.8, triggersDelay: 0);

        public static readonly FancyHint AccidentallyRemoved = new FancyHint(
                "accidentally-removed",
                "All removed content ends up in the Windows’ Recycle Bin",
                "So, if you accidentally removed something useful, you can always restore it. And, if you just removed something, open any Explorer window, press Ctrl+Z and removed item will be restored.",
                startupsDelay: 5, probability: 0.999, triggersDelay: 0, forced: true);

        public static readonly FancyHint ResizeableWindow = new FancyHint(
                "resizeable-window",
                "Try to resize the dialog window",
                "Amount of information displayed depends on window’s size, so UI could be a bit more flexible. It’s not the best system, but it’s better than everything being fixed size.",
                startupsDelay: 1, probability: 0.8, triggersDelay: 0, closeOnResize: true);
    }
}