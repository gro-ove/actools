using Windows.Foundation.Metadata;

namespace AcManager {
    /// <summary>
    /// Taken from command line arguments or from Arguments.txt in app’s data
    /// directory (one argument per line).
    /// </summary>
    public enum AppFlag {
        /// <summary>
        /// Maximum size of generated track map. Default value is 8192, could be upped
        /// to 16384 (DX11 required), but 16K maps might cause performance issues.
        /// Example: --track-map-generator-max-size=16384.
        /// </summary>
        TrackMapGeneratorMaxSize,

        /// <summary>
        /// How often shared memory will be read in Live state, in milliseconds. You might
        /// want to decrease this value to make webpage with race information work smoother.
        /// Default value: 50.
        /// Example: --shared-memory-live-reading-interval=20.
        /// </summary>
        SharedMemoryLiveReadingInterval,

        /// <summary>
        /// Run webserver with real-time updating player stats at specified port. Set to
        /// 0 to disable (default value). Don’t forget to enable selected port using netsh
        /// (if it’s not enabled, the required command will be shown in CM’s logs).
        /// Example: --run-race-information-webserver=18081.
        /// </summary>
        RunRaceInformationWebserver,

        /// <summary>
        /// Specify index file used by web server. Use either absolute path or path relative
        /// to CM’s directory (by default located in AppData/Local).
        /// Example: --race-information-webserver-file=Server.
        /// </summary>
        RaceInformationWebserverFile,

        /// <summary>
        /// Load libraries directly, without dumping them on disk.
        /// Example: --direct-assemblies-loading.
        /// </summary>
        DirectAssembliesLoading,

        /// <summary>
        /// Considering how often skins’ IDs might change, this option is enabled by default.
        /// Example: --ignore-missing-skins-in-kunos-events=no.
        /// </summary>
        IgnoreMissingSkinsInKunosEvents,

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
        /// How much RAM memory will be allocated for caching loaded from disk images, 10 MB
        /// by default. Feel free to modify if you want, I didn’t really test what values
        /// would work best here. Also, set it to 0 if you want to disable caching at all.
        /// Example: --images-cache-limit=50MB.
        /// </summary>
        [FlagDefaultValue("10MB")]
        ImagesCacheLimit,

        /// <summary>
        /// Maximum size of a cacheable image, 100 KB by default. Feel free to modify, I didn’t
        /// really test what values would work best here.
        /// Example: --images-cache-limit-per-image=1MB.
        /// </summary>
        [FlagDefaultValue("100KB")]
        ImagesCacheLimitPerImage,
        
        /// <summary>
        /// Images loaded from cache will be highlighted. For debugging purposes.
        /// Example: --images-mask-cached.
        /// </summary>
        ImagesMarkCached,

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
        /// For internal use. 
        /// Example: --test-if-acd-available.
        /// </summary>
        TestIfAcdAvailable,

        /// <summary>
        /// Disables checking of AC root directory, so any folder will pass.
        /// Use on your own risk.
        /// Example: --disable-ac-root-checking.
        /// </summary>
        DisableAcRootChecking,

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
        /// Timeout for online requests sent to Kunos servers (time interval or seconds).
        /// Default value: 10.
        /// Example: --web-request-timeout=00:05.
        /// </summary>
        WebRequestTimeout,

        /// <summary>
        /// Timeout for online requests sent to actual race servers, by default smaller
        /// because there is little to none sence in dealing with server which can’t respond
        /// in two seconds (time interval or seconds).
        /// Default value: 2.
        /// Example: --direct-request-timeout=00:05.
        /// </summary>
        DirectRequestTimeout,

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
        /// Set per-app proxy settings, not compatible with “--no-proxy”. Disabled by default (app will
        /// use proxy from IE settings, but then, again, not if “--no-proxy” was set).
        /// Example: --proxy=127.0.0.1:12345.
        /// </summary>
        Proxy,

        /// <summary>
        /// Ignore system (or custom) proxy settings (from IE). Disabled by default, could work faster if 
        /// enabled.
        /// Example: --no-proxy.
        /// </summary>
        NoProxy,

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
        /// For animated background, VLC plugin will be used. If this flag isn’t set (by default),
        /// default Windows approach will be used (so video will be played using Windows Media Player).
        /// I highly recommend not to use VLC because it’s not very reliable and fast (C#-library isn’t very
        /// good), but don’t forget to enable Windows Media Player and install required codecs first.
        /// </summary>
        UseVlcForAnimatedBackground,

        /// <summary>
        /// Scan …\AppData\Local\AcTools Content Manager\Themes for custom themes and add them
        /// to themes list.
        /// Example: --custom-themes.
        /// </summary>
        CustomThemes,

        /// <summary>
        /// For internal use.
        /// Example: --single-log-file.
        /// </summary>
        SingleLogFile
    }
}