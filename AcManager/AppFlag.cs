namespace AcManager {
    /// <summary>
    /// Taken from command line arguments or from Arguments.txt in app’s data
    /// directory (one argument per line).
    /// </summary>
    public enum AppFlag {
        /// <summary>
        /// Keep comments in INI-files while saving. Might cause some issues with invald
        /// files (for example, if there are two sections having the same name). Slower,
        /// so disabled by default.
        /// Example: --keep-ini-comments.
        /// </summary>
        KeepIniComments,

        /// <summary>
        /// Filter for ignoring specific in-game controls.
        /// Example: --ignore-controls=g27.
        /// </summary>
        IgnoreControls,

        /// <summary>
        /// Load images without blocking main UI thread. Enabled by default, but could cause
        /// some problems. In this case, try to disable it.
        /// Example: --load-images-in-background=no.
        /// </summary>
        LoadImagesInBackground,

        /// <summary>
        /// For internal use.
        /// Example: --log-packed.
        /// </summary>
        LogPacked,

        /// <summary>
        /// For internal use.
        /// Example: --race-out-debug.
        /// </summary>
        RaceOutDebug,

        /// <summary>
        /// Force specific locale even if it’s not supported at the moment. Could be useful
        /// if you want app to load locale from Locales folder.
        /// Example: --force-locale=ru.
        /// </summary>
        ForceLocale,

        /// <summary>
        /// UI scale (as a temporary solution for 4K screens). Make sure ideal formatting mode
        /// is enabled when using unusual scale.
        /// Example: --ui-scale=1.5.
        /// </summary>
        UiScale,

        /// <summary>
        /// Affects text rendering, disabled by default, but enabled if UI scale isn’t 100%.
        /// Example: --ideal-formatting-mode.
        /// </summary>
        IdealFormattingMode,

        /// <summary>
        /// Disables loading and saving, only for debugging purposes.
        /// Example: --disable-saving.
        /// </summary>
        DisableSaving,

        /// <summary>
        /// Disables logging.
        /// Example: --disable-logging.
        /// </summary>
        DisableLogging,

        /// <summary>
        /// Optimizes logging by keeping the stream open. Enabled by default.
        /// Example: --optimize-logging=no.
        /// </summary>
        OptimizeLogging,

        /// <summary>
        /// Saves Values.data without compressing. Takes more space.
        /// Example: --disable-values-compression.
        /// </summary>
        DisableValuesCompression,

        /// <summary>
        /// Changes path to data directory (somewhere in AppData\Local by default). 
        /// Folder will be created if missing.
        /// Example: --storage-location=LOCATION.
        /// </summary>
        StorageLocation,

        /// <summary>
        /// For testing and some special cases.
        /// Example: --offline-mode.
        /// </summary>
        OfflineMode,

        /// <summary>
        /// Disables checking of AC root directory, so any folder will pass.
        /// Use on your own risk.
        /// Example: --disable-ac-root-checking.
        /// </summary>
        DisableAcRootChecking,

        /// <summary>
        /// Ping timeout for scanning while manual adding new server in online mode, in milliseconds. Default value: 200.
        /// Example: --scan-ping-timeout=500.
        /// </summary>
        ScanPingTimeout,

        /// <summary>
        /// Command timeout in milliseconds. Default value: 3000.
        /// Example: --command-timeout=5000.
        /// </summary>
        CommandTimeout,

        /// <summary>
        /// Testing option.
        /// Example: --force-steam-id=0.
        /// </summary>
        ForceSteamId,

        /// <summary>
        /// Disable WebBrowser emulation mode even if it was disabled before.
        /// Example: --force-disable-web-browser-emulation-mode.
        /// </summary>
        ForceDisableWebBrowserEmulationMode,

        /// <summary>
        /// Don’t affect WebBrowser emulation mode at all.
        /// Example: --prevent-disable-web-browser-emulation-mode.
        /// </summary>
        PreventDisableWebBrowserEmulationMode,

        /// <summary>
        /// Affects almost all objects in lists. Default value: 5.
        /// Example: --ac-objects-loading-concurrency=25.
        /// </summary>
        AcObjectsLoadingConcurrency,

        /// <summary>
        /// Affects car, not always. Default value: 3.
        /// Example: --skins-loading-concurrency=5.
        /// </summary>
        SkinsLoadingConcurrency,

        /// <summary>
        /// Use oldschool notifications instead of modern ones even in Windows 8/8.1/10.
        /// Modern notifications require for app to have its shortcut in Windows menu,
        /// could be annoying.
        /// Example: --force-toast-fallback-mode.
        /// </summary>
        ForceToastFallbackMode,

        /// <summary>
        /// Timeouts for sockets using for scanning lan (ms). Default value: 200.
        /// Example: --lan-socket-timeout=25.
        /// </summary>
        LanSocketTimeout,

        /// <summary>
        /// Poll timeouts for sockets using for scanning lan (ms). Default value: 100.
        /// Example: --lan-poll-timeout=50.
        /// </summary>
        LanPollTimeout,

        /// <summary>
        /// Timeout for web requests for online requests (ms). Default value: 3000.
        /// Example: --web-request-timeout=5000.
        /// </summary>
        WebRequestTimeout,

        /// <summary>
        /// Less responsible UI, but could be a little bit faster.
        /// Example: --sync-navigation.
        /// </summary>
        SyncNavigation,

        /// <summary>
        /// Disable transition animation completely.
        /// Example: --disable-transition-animation.
        /// </summary>
        DisableTransitionAnimation,

        /// <summary>
        /// Size of queue of recently closed filters. Default value: 10.
        /// Example: --recently-closed-queue-size=20.
        /// </summary>
        RecentlyClosedQueueSize,

        /// <summary>
        /// Mark current preset as changed only if it’s actually changed, enabled by default.
        /// You can disable it to improve performance.
        /// Example: --smart-presets-changed-handling=no.
        /// </summary>
        SmartPresetsChangedHandling,

        /// <summary>
        /// Restore original race.ini file, disabled by default.
        /// Example: --enable-race-ini-restoration.
        /// </summary>
        EnableRaceIniRestoration,

        /// <summary>
        /// Only change race.ini, without running the game.
        /// Example: --enable-race-ini-test-mode.
        /// </summary>
        EnableRaceIniTestMode,

        /// <summary>
        /// Ignore skipped events (when first event is called “event5” or something like this,
        /// in other words — broken). I don’t think it’ll work, but anyway.
        /// Example: --kunos-career-ignore-skipped-events.
        /// </summary>
        KunosCareerIgnoreSkippedEvents,

        /// <summary>
        /// Ignore system proxy settings (from IE). Disabled by default, could work faster if 
        /// enabled.
        /// Example: --ignore-system-proxy.
        /// </summary>
        IgnoreSystemProxy,

        /// <summary>
        /// When started using command line args, don’t show main window.
        /// Example: --lite-startup-mode-supported.
        /// </summary>
        LiteStartupModeSupported,

        /// <summary>
        /// Nothing to see here, just some thing with the names.
        /// Example: --nfs-porsche-tribute.
        /// </summary>
        NfsPorscheTribute,

        /// <summary>
        /// Specific executable for SSE starter. Example: --sse-name=acs_x86_sse.exe.
        /// </summary>
        SseName,

        /// <summary>
        /// Custom background, could be animated (put a JPEG file nearby as a replacement
        /// when app isn’t active). If it’s not a full path, CM will look for a file(s) in
        /// …\AppData\Local\AcTools Content Manager\Themes\Backgrounds.
        /// Example: --background=mountain.webm.
        /// </summary>
        Background,

        /// <summary>
        /// Opacity of custom background. Default value: 0.5.
        /// Example: --background-opacity=0.2.
        /// </summary>
        BackgroundOpacity,

        /// <summary>
        /// Scan …\AppData\Local\AcTools Content Manager\Themes for custom themes and add them
        /// to themes list.
        /// Example: --custom-themes=on.
        /// </summary>
        CustomThemes
    }
}