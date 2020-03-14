using FirstFloor.ModernUI.Windows.Attached;

namespace AcManager.Controls.Helpers {
    public static class FancyHints {
        public static readonly FancyHint DragForContentSection = new FancyHint(
                "drag-for-content-section",
                "Drag’n’drop cars here to open them in Content section",
                "Works also with tracks, and the other way around — from Drive section to Content.",
                startupsDelay: 3, probability: 0.18);

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

        public static readonly FancyHint DegreesWind = new FancyHint(
                "degrees-wind",
                "If needed, you can set a specific wind direction",
                "To do so, switch the mode in context menu.",
                startupsDelay: 4, probability: 0.8);

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

        public static readonly FancyHint DownloadsList = new FancyHint(
                "downloads-menu",
                "Download is started",
                "Here, you can check all the downloads and install things when loading is finished",
                startupsDelay: 0, forced: true);

        public static readonly FancyHint ContentUpdatesArrived = new FancyHint(
                "content-updates-arrived",
                "Updates for mods",
                "It seems like Content Manager just found some. Would you like to download them?",
                startupsDelay: 0, forced: true);

        public static readonly FancyHint MultiSelectionMode = new FancyHint(
                "multiselection-mode",
                "Hold [i]Ctrl[/i] or [i]Shift[/i] and click here for multi-selection mode",
                "In multi-selection mode, you can run commands for several objects at once. [i]Ctrl+A[/i] and [i]Ctrl+D[/i] shortcuts work as well. Press [i]Escape[/i] to disable multi-selection mode. Also, to change current object in multi-selection mode, click on it using right mouse button.",
                startupsDelay: 2, probability: 0.4);

        public static readonly FancyHint DoubleClickToQuickDrive = new FancyHint(
                "double-click-to-quick-drive",
                "Double-click for Quick Drive",
                "Make a double click on a car, skin, track or weather to quickly open it in Quick Drive section.",
                startupsDelay: 1, probability: 0.9);

        public static readonly FancyHint RecalculateCurves = new FancyHint(
                "recalculate-curves",
                "Click here to update curves",
                "If you’re working on a new mod or just want to make sure UI data is correct, press these three dots and update curves the way you want. But keep in mind it might not be very accurate or appropriate for UI data, so, please, don’t touch Kunos cars unless you know what you’re doing.",
                startupsDelay: 4, probability: 0.2);

        public static readonly FancyHint ChangeBrandBadge = new FancyHint(
                "change-brand-badge",
                "Change brand badge if needed",
                "Just click at it and you’ll see built-in library (which you can override and extend).",
                startupsDelay: 3, probability: 0.2);

        public static readonly FancyHint ChangeUpgradeIcon = new FancyHint(
                "change-upgrade-icon",
                "Change upgrade icon if needed",
                "Just click at it and you’ll see the editor and built-in library (which you can override and extend).",
                startupsDelay: 4, probability: 0.2);

        public static readonly FancyHint TagsContextMenu = new FancyHint(
                "tags-context-menu",
                "Sort and clean up tags",
                "There is a context menu for tags as well, which allows you to sort out tags.",
                startupsDelay: 4, probability: 0.2);

        public static readonly FancyHint SkinContextMenu = new FancyHint(
                "update-skin-preview",
                "Update skin’s preview or livery via context menu",
                "Each skin’s icon here has a context menu with some options. Or, if needed, just open Skins Manager with [i]Ctrl+K[/i].",
                startupsDelay: 4, probability: 0.2);

        public static readonly FancyHint GameDialogTableSize = new FancyHint(
                "game-dialog-table-size",
                "Make dialog wider for best laps",
                "In case you want to see best laps per driver, simply make this dialog window wider.",
                startupsDelay: 0);

        public static readonly FancyHint OnlineCarContextMenu = new FancyHint(
                "online-car-context-menu",
                "Prepare car setups using context menu",
                "Before the race, prepare car setups and more using context menu from here.",
                startupsDelay: 1, probability: 0.5);

        public static readonly FancyHint CarDialogThumbinalMode = new FancyHint(
                "car-dialog-thumbinal-mode",
                "Want to switch to thumbnail mode?",
                "Just make the list wider.",
                startupsDelay: 0);

        public static readonly FancyHint TrackDialogThumbinalMode = new FancyHint(
                "track-dialog-thumbinal-mode",
                "Want to switch to thumbnail mode?",
                "Just make the list wider.",
                startupsDelay: 2, probability: 0.25);
    }
}