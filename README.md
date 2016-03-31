# AcTools Utils

Set of utils and apps designed for Assetto Corsa. If you have any interest in them, please, let me know.

## Common libraries

- ### [AcTools](https://github.com/gro-ove/actools/tree/master/AcTools)
    Main library, used by any other project (including [Cars Manager](https://ascobash.wordpress.com/2015/06/14/actools-uijson/) and even [modded KsEditor](https://ascobash.wordpress.com/2015/07/22/kseditor/)). Contains methods to work with common AC files, launches game and stuff.
    
- ### [AcTools.Kn5Render](https://github.com/gro-ove/actools/tree/master/AcTools.Kn5Render)
    First, basic DX11 renderer. Basic forward rendering without any features. Code is very bad, almost everything in one class, but it does a lot of cool stuff apart from being a custom showroom: it generates [new ambient shadows](http://i.imgur.com/i4vsn0M.png) with a pretty neat quality, [new track maps](https://i2.wp.com/i.imgur.com/mjnn0Rr.png), finds livery colors. Also, of course, as a custom showroom it helps to draw new skins by [auto-reloading changed psd-files automatically](https://www.youtube.com/watch?v=-pGj1zKXgY0).

    [![AcTools Showroom](https://ascobash.files.wordpress.com/2015/10/uzmhnps.png?w=320)](https://ascobash.files.wordpress.com/2015/10/uzmhnps.png)
    
- ### [AcTools.Render](https://github.com/gro-ove/actools/tree/master/AcTools.Render)
    A replacement for AcTools.Kn5Render. Development went in a wrong direction (deferred rendering & lighting, SSLR, HDR, bloom), but overall architecture is pretty good (at least it's much better than previous library), so regular forward rendering is coming soon.

    Main problem with those two projects, of course, is to make shaders as close as possible to Kunos ones. I have no idea how at the moment, my knowledge of shaders in common and DX11 in particular is too weak.

    [![Custom Showroom](https://ascobash.files.wordpress.com/2016/01/vas7al2.png?w=320)](https://ascobash.files.wordpress.com/2016/01/vas7al2.png)
    
## Other projects

- ### [Kn5Materials](https://github.com/gro-ove/actools/tree/master/Kn5Materials)
    Simple Windows Forms app to view/edit material properties of any kn5 file.

- ### [AcToolsShowroom](https://github.com/gro-ove/actools/tree/master/AcToolsShowroom)
    Just a small wrapper for AcTools.Kn5Render.

- ### [TrackMapGenerator](https://github.com/gro-ove/actools/tree/master/TrackMapGenerator)
    Same, only a small wrapper.

## Content Manager

- ### [AcManager.Tools](https://github.com/gro-ove/actools/tree/master/AcManager.Tools)
    Library with logic, almost without any UI stuff (like CarsManager and CarObject, for instance).

- ### [AcManager.Controls](https://github.com/gro-ove/actools/tree/master/AcManager.Controls)
    Library with common UI stuff (like AcListPage).

- ### [FirstFloor.ModernUI](https://github.com/gro-ove/actools/tree/master/FirstFloor.ModernUI)
    Basic UI library. Was taken from [here](https://github.com/firstfloorsoftware/mui) and then adjusted a little.

- ### [AcManager](https://github.com/gro-ove/actools/tree/master/AcManager)
    App itself.

    [![Content Manager](https://ascobash.files.wordpress.com/2015/10/content-manager_2016-02-15_02-31-14.png?w=320)](https://ascobash.files.wordpress.com/2015/10/content-manager_2016-02-15_02-31-14.png)
    [![Content Manager](https://ascobash.files.wordpress.com/2016/02/content-manager_2016-02-18_20-49-56.png?w=320)](https://ascobash.files.wordpress.com/2016/02/content-manager_2016-02-18_20-49-56.png)
