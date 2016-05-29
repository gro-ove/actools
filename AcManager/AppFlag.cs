namespace AcManager {
    /// <summary>
    /// Taken from command line arguments or from Arguments.txt in app's data
    /// directory (one argument per line).
    /// </summary>
    public enum AppFlag {
        /// <summary>
        /// Disables logging.
        /// Example: --disable-logging.
        /// </summary>
        DisableLogging,

        /// <summary>
        /// Saves Values.data without compressing.
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
        /// Ping timeout for online mode in milliseconds. Default value: 2000.
        /// Example: --ping-timeout=5000.
        /// </summary>
        PingTimeout,

        /// <summary>
        /// Ping timeout for scanning while manual adding new server in online mode, in milliseconds. Default value: 200.
        /// Example: --scan-ping-timeout=500.
        /// </summary>
        ScanPingTimeout,

        /// <summary>
        /// Testing option.
        /// Example: --force-steam-id=0.
        /// </summary>
        ForceSteamId,

        /// <summary>
        /// Disable WebBrowser emulation mode even if it was disabled before.
        /// Example: --force-disable-web-browser-emulation-mode
        /// </summary>
        ForceDisableWebBrowserEmulationMode,

        /// <summary>
        /// Don't affect WebBrowser emulation mode at all.
        /// Example: --prevent-disable-web-browser-emulation-mode
        /// </summary>
        PreventDisableWebBrowserEmulationMode,

        /// <summary>
        /// Number of servers being pinged simultaneosly (usually with pinging also goes
        /// loading of cars & cars skins information). Default value: 30.
        /// Example: --ping-concurrency=50
        /// </summary>
        PingConcurrency,

        /// <summary>
        /// Affects almost all objects in lists. Default value: 5.
        /// Example: --ac-objects-loading-concurrency=25
        /// </summary>
        AcObjectsLoadingConcurrency,

        /// <summary>
        /// Affects car, not always. Default value: 3.
        /// Example: --skins-loading-concurrency=5
        /// </summary>
        SkinsLoadingConcurrency,

        /// <summary>
        /// Use oldschool notifications instead of modern ones even in Windows 8/8.1/10.
        /// Modern notifications require for app to have its shortcut in Windows menu,
        /// could be annoying.
        /// Example: --force-toast-fallback-mode
        /// </summary>
        ForceToastFallbackMode,

        /// <summary>
        /// Timeouts for sockets using for scanning lan (ms). Default value: 200.
        /// Example: --lan-socket-timeout=25
        /// </summary>
        LanSocketTimeout,

        /// <summary>
        /// Poll timeouts for sockets using for scanning lan (ms). Default value: 100.
        /// Example: --lan-poll-timeout=50
        /// </summary>
        LanPollTimeout,

        /// <summary>
        /// Timeout for web requests for online requests (ms). Default value: 3000.
        /// Example: --web-request-timeout=5000
        /// </summary>
        WebRequestTimeout,

        /// <summary>
        /// Always get server information directly from server instead of using main AC server.
        /// Should be faster and better. Default value: true.
        /// Example: --always-get-information-directly=no
        /// </summary>
        AlwaysGetInformationDirectly,

        /// <summary>
        /// Less responsible UI, but could be a little bit faster.
        /// Example: --sync-navigation
        /// </summary>
        SyncNavigation,

        /// <summary>
        /// Disable transition animation completely.
        /// Example: --disable-transition-animation
        /// </summary>
        DisableTransitionAnimation,

        /// <summary>
        /// Size of queue of recently closed filters. Default value: 10.
        /// Example: --recently-closed-queue-size=20
        /// </summary>
        RecentlyClosedQueueSize,

        /// <summary>
        /// Mark current preset as changed only if it's actually changed, enabled by default.
        /// You can disable it to improve performance.
        /// Example: --smart-presets-changed-handling=false
        /// </summary>
        SmartPresetsChangedHandling,

        /// <summary>
        /// Restore original race.ini file, enabled by default. You can disable for whatever
        /// reason you want.
        /// Example: --enable-race-ini-restoration=false
        /// </summary>
        EnableRaceIniRestoration,

        /// <summary>
        /// Ignore skipped events (when first event is called “event5” or something like this,
        /// in other words — broken). I don't think it'll work, but anyway.
        /// Example: --kunos-career-ignore-skipped-events
        /// </summary>
        KunosCareerIgnoreSkippedEvents,

        /// <summary>
        /// Ignore system proxy settings (from IE). Disabled by default, could work faster if 
        /// enabled.
        /// Example: --ignore-system-proxy
        /// </summary>
        IgnoreSystemProxy,

        /// <summary>
        /// When started using command line args, don't show main window.
        /// Example: --lite-startup-mode-supported
        /// </summary>
        LiteStartupModeSupported
    }
}