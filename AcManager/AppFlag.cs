namespace AcManager {
    /// <summary>
    /// Taken from command line arguments or from Arguments.txt in app’s data
    /// directory (one argument per line).
    /// </summary>
    public enum AppFlag {
        /// <summary>
        /// Use software rendering for UI, which might cause everything down. Use it if you have
        /// an issue with some overlay-showing apps, such as RivaTuner. But only as a last resort,
        /// for RivaTuner it’s better to simply add app to exceptions.
        /// </summary>
        SoftwareRendering,

        /// <summary>
        /// Encoding JPEG quality. Doesn’t have a lot of effect without Magick.NET plugin, because
        /// frankly Windows’ encoder is a piece of junk. Default value: 98.
        /// Example: --jpeg-quality=99.
        /// </summary>
        JpegQuality,

        /// <summary>
        /// Manually load ReShade’s dxgi.dll if found. Possible values: on/off/kn5only. With “kn5only”,
        /// CM will load it only if KN5-file to view was passed in arguments as well. Be careful!
        /// ReShade can mess up WPF windows!
        /// Example: --force-reshade=kn5only.
        /// </summary>
        ForceReshade,

        /// <summary>
        /// While loading from Google Drive, handle 304 response manually for debug purposes.
        /// Example: --google-drive-loader-manual-redirect.
        /// </summary>
        GoogleDriveLoaderManualRedirect,

        /// <summary>
        /// While loading from Google Drive, log a lot of information.
        /// Example: --google-drive-loader-debug-mode.
        /// </summary>
        GoogleDriveLoaderDebugMode,

        /// <summary>
        /// Detailed logging for pinging a specific by IP-address server online.
        /// Example: --debug-ping=103.62.50.22.
        /// </summary>
        DebugPing,

        /// <summary>
        /// For testing.
        /// Example: --sidekick-optimal-range-threshold=0.01.
        /// </summary>
        SidekickOptimalRangeThreshold,

        /// <summary>
        /// Put all information about generic mods activation/deactivation to log.
        /// Example: --generic-mods-logging.
        /// </summary>
        GenericModsLogging,

        /// <summary>
        /// Fancy hints are shown much more frequently.
        /// Example: --fancy-hints-debug-mode.
        /// </summary>
        FancyHintsDebugMode,

        /// <summary>
        /// Minimal delay between fancy hints. Default value: 30 minutes.
        /// Example: --fancy-hints-minimum-delay=00:10.
        /// </summary>
        FancyHintsMinimumDelay,

        /// <summary>
        /// Do not waste time checking if HTTPS stuff is valid. Who might want to hack you this way?
        /// Example: --ignore-https.
        /// </summary>
        IgnoreHttps,

        /// <summary>
        /// Filter specifying what content CM can pack and share. Default value: kunos-.
        /// Example: --can-pack=kunos-&!private.
        /// </summary>
        CanPack,

        /// <summary>
        /// Filter specifying which cars CM can pack and share. Default value: kunos-&!id:`^ad_`&!author:Race Sim Studio.
        /// Example: --can-pack-cars=kunos-|dlc-.
        /// </summary>
        CanPackCars,

        /// <summary>
        /// Threshold for finding similar data. Default value: 0.95 (95%).
        /// Example: --similar-threshold=0.9.
        /// </summary>
        [FlagDefaultValue("0.95")]
        SimilarThreshold,

        /// <summary>
        /// Additional IDs for searching for similar data (by default, only Kunos cars
        /// are checked as potential sources) separated by comma or semicolon.
        /// Example: --similar-additional-source-ids=.
        /// </summary>
        SimilarAdditionalSourceIds,

        /// <summary>
        /// Period for checking if there is an available slot using Auto-Join mode
        /// in Online section. Will be used only if smaller than auto-update period set
        /// in Settings. Default value: 5 seconds. Not sure if there is any sense for
        /// you to make it smaller unless you use immediate starters, such as UI Module
        /// or SSE.
        /// Example: --auto-connect-period=00:02.
        /// </summary>
        [FlagDefaultValue("00:05")]
        AutoConnectPeriod,

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
        /// How much RAM memory will be allocated for caching loaded from disk images, 10 MB
        /// by default. Feel free to modify if you want, I didn’t really test what values
        /// would work best here. Also, set it to 0 if you want to disable caching at all.
        /// Example: --images-cache-limit=50MB.
        /// </summary>
        [FlagDefaultValue("10MB")]
        ImagesCacheLimit,

        /// <summary>
        /// Images loaded from cache will be highlighted. For debugging purposes.
        /// Example: --images-mask-cached.
        /// </summary>
        ImagesMarkCached,

        /// <summary>
        /// For internal use.
        /// Example: --rd-loader-allowed.
        /// </summary>
        RdLoaderAllowed,

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
        /// Default value: 1 minute.
        /// Example: --web-request-timeout=02:00.
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
        /// While navigating between pages, load data synchronously (loading might be faster
        /// without need to switch between cores, but there will be inevitable hung ups as well).
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
        /// Nothing to see here, just some thing with the names.
        /// Example: --nfs-porsche-tribute.
        /// </summary>
        NfsPorscheTribute,

        /// <summary>
        /// Specific executable for SSE starter.
        /// Example: --sse-name=acs_x86_sse.exe.
        /// </summary>
        SseName,

        /// <summary>
        /// Enable logging for SSE starter.
        /// Example: --sse-logging.
        /// </summary>
        SseLogging,

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
        /// Example: --use-vlc-for-animated-background.
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