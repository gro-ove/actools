# AcTools (and Content Manager)

Set of utils and apps designed for Assetto Corsa. Some obsolete projects are moved [here](https://github.com/gro-ove/actools-utils).

## Common libraries

- ### [AcTools](https://github.com/gro-ove/actools/tree/master/AcTools)
    Main library, used by any other project (including [Cars Manager](https://ascobash.wordpress.com/2015/06/14/actools-uijson/) and even [modded KsEditor](https://ascobash.wordpress.com/2015/07/22/kseditor/)). Contains methods to work with common AC files, launches game and stuff.
    
- ### [AcTools.GenericMods](https://github.com/gro-ove/actools/tree/master/AcTools.GenericMods)
    Small library for managing JSGME mods, fully compatible, but with optional hard links support.

- ### [AcTools.LapTimes](https://github.com/gro-ove/actools/tree/master/AcTools.LapTimes)
    Thing for reading best lap times from different sources. Uses LevelDb for reading from the original launcher (which saves times using Chromium’s IndexedDB).
    
- ### [AcTools.LapTimes.LevelDb](https://github.com/gro-ove/actools/tree/master/AcTools.LapTimes.LevelDb)
    Small “spin-off” which loads times from old AC database. It was made using IndexedDB in Chromium, which uses LevelDB underneath. Quite a mess if you want to read it.

- ### [AcTools.Render](https://github.com/gro-ove/actools/tree/master/AcTools.Render)
    A replacement for AcTools.Kn5Render. Has a much more thoughtful architecture and thereby contains two different renderers: Lite (very simple skins-editing DX10-compatible version) and Dark (extended variation of Lite, with lighting, skinning and a lot of effects such as SSLR, SSAO, PCSS). Both use forward rendering. There was also deferred renderer, but it was quite poor and got moved away.
    
    Apart from simple rendering, has a bunch of special modes, allowing to update ambient shadows, AO maps, recalculate tracks’ maps and outlines.
    
    [![Dark Showroom](http://acstuff.ru/app/screens/__custom_showroom_1506887799.jpg)](http://acstuff.ru/app/screens/__custom_showroom_1506887799.jpg)
        
    [![Dark Showroom](http://i.imgur.com/uWV4zTw.jpg)](http://i.imgur.com/uWV4zTw.jpg)
    
    [![Lite Showroom](http://i.imgur.com/neffgq2.png)](http://i.imgur.com/neffgq2.png)
    
- ### [AcTools.Render.Deferred](https://github.com/gro-ove/actools/tree/master/AcTools.Render)
    Deferred rendering with dynamic lighting, dynamic shadows, HDR, tricky SSLR… Sadly, I couldn’t find a way to move all materials here correctly, so I decided to switch to forward rendering instead. Also, with forward, I can vary options on-fly, getting either very high-performance simple renderer (≈900 FPS) or pretty good looking one (≈60 FPS, without MSAA or higher pixel density).

    [![Custom Showroom](https://trello-attachments.s3.amazonaws.com/5717c5d2feb66091a673f1e8/1920x1080/237d1513a35509f5c48d969bdf4abd02/__custom_showroom_1461797524.jpg)](https://trello-attachments.s3.amazonaws.com/5717c5d2feb66091a673f1e8/1920x1080/237d1513a35509f5c48d969bdf4abd02/__custom_showroom_1461797524.jpg)
    
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

 - For now, please, ignore AcTools.NeuralTyres library, it’s so much WIP I’m not even sure I’ll keep it. Feel free to remove it from solution to clean things up.

 - I recommend to switch to x86 (or, if you need, x64) platform. As far as I can see, most libraries (such as SlimDX, Magick.NET or CefSharp) won’t work with AnyCPU.

 - You might need to install DirectX SDK to rebuild [AcTools.Render/Shaders/Shaders.tt](https://github.com/gro-ove/actools/blob/master/AcTools.Render/Shaders/Shaders.tt). But, just in case, built *Shaders.cs* and *Shaders.resources* are already included.

 - Please, feel free to [contact me](https://trello.com/c/w5xT6ssZ/49-contacts) anytime. I don’t have any experience it open-source, there might be some things I forgot to mention.
