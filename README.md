# AcTools (and Content Manager)

[![Build status](https://img.shields.io/appveyor/ci/gro-ove/actools.svg?label=Build&maxAge=60)](https://ci.appveyor.com/project/gro-ove/actools)
[![Works badge](https://cdn.rawgit.com/gro-ove/works-on-my-machine/5fc312b1/badge.svg)](https://github.com/nikku/works-on-my-machine)
[![License](https://img.shields.io/github/license/gro-ove/actools.svg?label=License&maxAge=86400)](./LICENSE.txt)
[![Release](https://img.shields.io/github/release/gro-ove/actools.svg?label=Release&maxAge=60)](https://github.com/gro-ove/actools/releases/latest)

Set of utils and apps designed for Assetto Corsa. Some obsolete projects are moved [here](https://github.com/gro-ove/actools-utils).

## Common libraries

- ### [AcTools](https://github.com/gro-ove/actools/tree/master/AcTools)
    Main library, used by any other project (including [Cars Manager](https://ascobash.wordpress.com/2015/06/14/actools-uijson/) and even [modded KsEditor](https://ascobash.wordpress.com/2015/07/22/kseditor/)). Contains methods to work with common AC files, launches game and stuff.
    
- ### [AcTools.GenericMods](https://github.com/gro-ove/actools/tree/master/AcTools.GenericMods)
    Small library for managing JSGME mods, fully compatible, but with optional hard links support.

- ### [AcTools.LapTimes](https://github.com/gro-ove/actools/tree/master/AcTools.LapTimes)
    Thing for reading best lap times from different sources. Uses LevelDb for reading from the original launcher (which saves times using Chromium’s IndexedDB).
    
- ### [AcTools.LapTimes.LevelDb](https://github.com/gro-ove/actools/tree/master/AcTools.LapTimes.LevelDb)
    Small “spin-off” which loads times from old AC database. It was made using IndexedDB in Chromium, which uses LevelDB underneath. Quite a mess if you want to read it. Thankfully, now it’s completely redundant, I’m going to remove it for good.

- ### [AcTools.Render](https://github.com/gro-ove/actools/tree/master/AcTools.Render)
    A replacement for AcTools.Kn5Render. Has a much more thoughtful architecture and thereby contains two different renderers: Lite (very simple skins-editing DX10-compatible version) and Dark (extended variation of Lite, with lighting, skinning and a lot of effects such as SSLR, SSAO, PCSS). Both use forward rendering. There was also deferred renderer, but it was quite poor and got moved away.
    
    Apart from simple rendering, has a bunch of special modes, allowing to update ambient shadows, AO maps, recalculate tracks’ maps and outlines.
    
    [![Dark Showroom](http://acstuff.ru/app/screens/__custom_showroom_1517748858.jpg)](http://acstuff.ru/app/screens/__custom_showroom_1517748858.jpg)
    
    [![Dark Showroom](http://acstuff.ru/app/screens/__custom_showroom_1517749976.jpg)](http://acstuff.ru/app/screens/__custom_showroom_1517749976.jpg)
        
    [![Dark Showroom](http://acstuff.ru/app/screens/__custom_showroom_1516289950.jpg)](http://acstuff.ru/app/screens/__custom_showroom_1516289950.jpg)

    [![Lite Showroom](http://acstuff.ru/app/screens/__custom_showroom_1517751713.jpg)](http://acstuff.ru/app/screens/__custom_showroom_1517751713.jpg)
    
- ### [LicensePlates](https://github.com/gro-ove/actools/tree/master/LicensePlates)
    Fully independent from AcTools.\* library which generates number plates using Lua to interpret style files and Magick.NET to create and save textures. [In action](http://i.imgur.com/T7SVlLF.gifv).
    
- ### [StringBasedFilter](https://github.com/gro-ove/actools/tree/master/StringBasedFilter)
    Small library for filtering objects by queries like `*ca & !(country:c* | year<1990)`.

## Content Manager

- ### [AcManager.Tools](https://github.com/gro-ove/actools/tree/master/AcManager.Tools)
    Library with logic, almost without any UI parts (like CarsManager and CarObject, for instance).

- ### [AcManager.AcSound](https://github.com/gro-ove/actools/tree/master/AcManager.AcSound)
    Wrapper for AcTools.SoundbankPlayer, which is, in its turn, just a very small thing built around FMOD library to play FMOD soundbanks.

- ### [AcManager.LargeFilesSharing](https://github.com/gro-ove/actools/tree/master/AcManager.LargeFilesSharing)
    Small sub-library for uploading big files into various clouds. Could weight more than 2 MB with only official Google library, but self-written takes much less.

- ### [AcManager.ContentRepair](https://github.com/gro-ove/actools/tree/master/AcManager.ContentRepair)
    Contains a bunch of diagnostics and repairs for common custom cars’ issues in AC.
    
- ### [AcManager.Controls](https://github.com/gro-ove/actools/tree/master/AcManager.Controls)
    Library with common UI components (like AcListPage).

- ### [FirstFloor.ModernUI](https://github.com/gro-ove/actools/tree/master/FirstFloor.ModernUI)
    Main UI library. Slightly modified version of [Modern UI for WPF (MUI)](https://github.com/firstfloorsoftware/mui).

    [![New colorpicker control](http://i.imgur.com/5ZJnszR.png)](http://i.imgur.com/5ZJnszR.png)

- ### [AcManager](https://github.com/gro-ove/actools/tree/master/AcManager)
    App itself.

    [![Content Manager](http://i.imgur.com/WsovqYV.png)](http://i.imgur.com/WsovqYV.png)
    [![Content Manager](http://i.imgur.com/wvM1SMY.png)](http://i.imgur.com/wvM1SMY.png)
    
## Other apps
    
- ### [CustomPreviewUpdater](https://github.com/gro-ove/actools/tree/master/CustomPreviewUpdater)
    Generates previews using AcTools.Render.
    
- ### [CustomShowroom](https://github.com/gro-ove/actools/tree/master/CustomShowroom)
    Small wrapper for AcTools.Render library, for standalone use without Content Manager.
    
- ### [LicensePlatesGenerator](https://github.com/gro-ove/actools/tree/master/LicensePlatesGenerator)
    Small console wrapper for LicensePlates library.

# Build notes

 - For now, only x86 platform is supported. Projects can be built in x64, but, most likely, it won’t work. It should be fixable, all referenced libraries have both x86 and x64 version. Going to solve it later.
 
 - If you need a support for new Windows 8, 8.1 and 10 notifications, make sure [this path](https://github.com/gro-ove/actools/blob/master/FirstFloor.ModernUI/FirstFloor.ModernUI.csproj#L91) is correct. If it’s not, or you don’t have that library, no problem, dependant piece of code will be disabled, and fallback notifications will be used.
 
 - I use a couple of small tools (mostly Cygwin shell scripts) to increment version number and auto-build T4 templates automatically, but they are [disabled by default](https://github.com/gro-ove/actools/blob/master/Libraries/PreBuildEvents.Templates.props#L3) unless you have `ACTOOLS_BUILD_SCRIPTS=On` environment variable set. So… Don’t set it, or make sure you have something compatible in your system. If needed, I’d be glad to send my scripts to you.

 - You might need to install DirectX SDK to rebuild [AcTools.Render/Shaders/Shaders.tt](https://github.com/gro-ove/actools/blob/master/AcTools.Render/Shaders/Shaders.tt). But, just in case, built *Shaders.cs* and *Shaders.resources* are already included. Also, it takes quite a long time to rebuild those shaders, up to 5–10 minutes on my PC.

 - Feel free to [contact me](https://trello.com/c/w5xT6ssZ/49-contacts) anytime. I don’t have any experience with open-source projects, but I’d be glad to learn.
