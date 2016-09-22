# AcTools (and Content Manager)

Set of utils and apps designed for Assetto Corsa. Some obsolete projects are moved [here](https://github.com/gro-ove/actools-utils).

## Common libraries

- ### [AcTools](https://github.com/gro-ove/actools/tree/master/AcTools)
    Main library, used by any other project (including [Cars Manager](https://ascobash.wordpress.com/2015/06/14/actools-uijson/) and even [modded KsEditor](https://ascobash.wordpress.com/2015/07/22/kseditor/)). Contains methods to work with common AC files, launches game and stuff.
    
- ### [AcTools.Render](https://github.com/gro-ove/actools/tree/master/AcTools.Render)
    A replacement for AcTools.Kn5Render. Has a much more thoughtful architecture and thereby contains two different renderers: Lite (simple skins-editing DX10-compatible version) and Deferred (deferred rendering & lighting, SSLR, HDR, dynamic shadows).

    Lite renderer supports a lot of different Kunos materials with a lot of properties, so it should be pretty close.
    
    [![Lite Showroom](http://i.imgur.com/neffgq2.png)](http://i.imgur.com/neffgq2.png)

    [![Custom Showroom](https://trello-attachments.s3.amazonaws.com/5717c5d2feb66091a673f1e8/1920x1080/237d1513a35509f5c48d969bdf4abd02/__custom_showroom_1461797524.jpg)](https://trello-attachments.s3.amazonaws.com/5717c5d2feb66091a673f1e8/1920x1080/237d1513a35509f5c48d969bdf4abd02/__custom_showroom_1461797524.jpg)

- ### [StringBasedFilter](https://github.com/gro-ove/actools/tree/master/StringBasedFilter)
    Small library for filtering objects by queries like `*ca & !(country:c* | year<1990)`.

## Content Manager

- ### [AcManager.Tools](https://github.com/gro-ove/actools/tree/master/AcManager.Tools)
    Library with logic, almost without any UI parts (like CarsManager and CarObject, for instance).

- ### [AcManager.LargeFilesSharing](https://github.com/gro-ove/actools/tree/master/AcManager.LargeFilesSharing)
    Small sub-library for uploading big files into clouds (only Google Drive for now). Could weight more than 2 MB with official Google library, but self-written takes only about 35 KB.

- ### [AcManager.Controls](https://github.com/gro-ove/actools/tree/master/AcManager.Controls)
    Library with common UI components (like AcListPage).

- ### [FirstFloor.ModernUI](https://github.com/gro-ove/actools/tree/master/FirstFloor.ModernUI)
    Main UI library. Slightly modified version of [Modern UI for WPF (MUI)](https://github.com/firstfloorsoftware/mui).

    [![New colorpicker control](http://i.imgur.com/5ZJnszR.png)](http://i.imgur.com/5ZJnszR.png)

- ### [AcManager](https://github.com/gro-ove/actools/tree/master/AcManager)
    App itself.

    [![Content Manager](http://i.imgur.com/WsovqYV.png)](http://i.imgur.com/WsovqYV.png)
    [![Content Manager](http://i.imgur.com/wvM1SMY.png)](http://i.imgur.com/wvM1SMY.png)

# Build notes

 - Some of references were added directly as DLL-files. I didn’t use Nuget for all of references because eigher I needed specific libraries locations (this way, I’m able to use 32-bit libraries for 32-bit build and otherwise using simplest .csproj “trick”) or I slightly modified some of them (for example, now JSON-parser works reads numbers starting with “0” according to JSON specifications). All these references are located in Library directory, you can get them [here](https://drive.google.com/file/d/0B6GfX1zRa8pOMnVwV1dBV1Z4cjQ/view?usp=drivesdk). Or just collect them from scratch, those changes aren’t be very important.

 - I recommend to switch to x86 (or, if you need, x64) platform. As far as I can see, most libraries (such as SlimDX, Magick.NET or Awesomium) won’t work with AnyCPU.

 - Take AcTools.Internal from [the unpacked version](https://trello.com/c/w5xT6ssZ/49-contacts) (or make your own compatible version, it mostly contains different API codes, addresses and salts for testing checksums).

 - You might need to install DirectX SDK to rebuild [AcTools.Render/Base/Shaders/ShadersTemplate.tt](https://github.com/gro-ove/actools/blob/master/AcTools.Render/Base/Shaders/ShadersTemplate.tt). But, just in case, builded *ShadersTemplate.cs* and *Shaders.resources* are already included.

 - Please, feel free to [contact me](https://trello.com/c/w5xT6ssZ/49-contacts) anytime. I don’t have any experience it open-source, there might be some things I forgot to mention.