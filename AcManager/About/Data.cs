using AcManager.Tools.About;
using System.Linq;

namespace AcManager.About {
    public static class ImportantTips {
        public static readonly PieceOfInformation[] Entries = new []{
            new PieceOfInformation(@"21_1095401383_1831013213", @"trackPreviews", @"Track Previews How-To", null, @"If you want to generate a new preview image for any track, press “Update Preview” button on the track’s page in Content tab. Race will be started as usual, but instead of driving you need to make some picturesque shots (you’ll be able to choose one of them later).

Hints:
 • Press F8 to make a shot;
 • Press F7 to switch to a Free Camera mode (you can enable it in AC settings);
 • In Free Camera mode use arrows to move camera around; also you may hold Ctrl or Shift to adjust its speed;
 • And, as usual, PageUp/PageDown allow to tune exposure;
 • App will cut and resize them to match Kunos previews automatically.

 [img=""http://i.imgur.com/LzLLjAw.png|355""]Example[/img]

Good luck!", true, false),
            new PieceOfInformation(@"22_-1950881468_1645627313", null, @"Command-Line Arguments", null, @"CM has a bunch of special options you can set by using command-line arguments. Or just create a new [i]Arguments.txt[/i] file in [i]…\AppData\Local\AcTools Content Manager[/i] and write them in it (one argument per line).

 [img=""http://i.imgur.com/qO7oOVn.png|240""]Example of that file[/img]

Here are some of the arguments:
 [b][mono]--ignore-system-proxy=false[/mono][/b] — use proxy settings from Internet Explorer;
 [b][mono]--disable-logging[/mono][/b] — please, don’t use it now, app could crash in any time, and logs could be very helpful;
 [b][mono]--storage-location=LOCATION[/mono][/b] — change location of data folder (of course, this one should be used only as a command-line argument);
 [b][mono]--ping-concurrency=30[/mono][/b] — number of servers being pinged concurrently;
 [b][mono]--enable-race-ini-restoration=false[/mono][/b] — don’t revert changes in [i]race.ini[/i].

You can see the full list [url=""https://github.com/gro-ove/actools/blob/master/AcManager/AppFlag.cs""]here[/url].", false, false),
            new PieceOfInformation(@"8_-1601734236_1588375622", @"starters", @"Starters", null, @"[b]This part is very important. Please, read it.[/b]

Since Steam version of AC can’t be started only by running [i]acs.exe[/i], CM has to use some weird ways to do the trick. At the moment there are three strange ways to start the game (apart from direct starting by [b]Naive Starter[/b]). All of them had their flaws, that’s why you have to select one which suits you:

 • [b]Tricky Starter[/b]
First one. Uses pretty weird approach, which requires to temporary remove [i]AssettoCorsa.exe[/i] out of the way. On the bright side, this is the only change it does. So:
  + Steam-friendly: Steam overlay & archievments will work without any problems;
  + Tested: firstly was implemented in Cars Manager;
  − Unreliable: using approach could be blocked in any AC update;
  − Tricky: sadly, doesn’t work without Internet connection;
  − Incompatible: can’t be used when original launcher is running;
  − Potentially adverse: if CM will suddenly get killed, you’ll have to restore [i]AssettoCorsa.exe[/i] from [i]AssettoCorsa_backup.exe[/i] manually;
  − Slow: using approach requires to wait some time at some point;
  − Limited: can’t specify version (32-bit or 64-bit).

 • [b]Starter+[/b]
Changes original launcher a little.
  + Steam-friendly: Steam overlay & archievments will work without any problems;
  + Relieable: even though it changes original launcher, it still works as a launcher
  − Unreliable: if Kunos will change [i]AssettoCorsa.exe[/i], you won’t get new features until addon will be updated;
  − Can’t be enabled or disabled while [i]AssettoCorsa.exe[/i] is runned;
  − Slow: use original laucher, which isn’t very fast.

 • [b]SSE Starter (Obsolete)[/b]
Quite questionable solution. I won’t spread about it too much, just basics:
  + Compatible: doesn’t mess with [i]AssettoCorsa.exe[/i] at all, can work even if it’s runned;
  + Tested: surprisingly solid, works without Internet;
  − Steam-unfriendly: doesn’t work for archievments & stuff.
  − Obsoletable: you can miss some updates for AC using it.
  − Not supported: doesn’t work with AC 1.6.

 • [b]Naive Starter[/b]
CM just starts [i]acs.exe[/i] (or [i]acs_x86.exe[/i]), everything else depends on AC version you use. If you’re using original Steam version — won’t work at all.", false, false),
            new PieceOfInformation(@"17_-113008927_-924009265", @"iNeedYourHelp", @"Thank you!", null, @"Thank you all for support! It works! :)

[s]Since AC 1.6 SSE stoped working, and other two (Tricky and Starter+) require write-access to root AC folder, which could be unavailable. Also, both of them require the default launcher (AssettoCorsa.exe) to be replaced, and it could cause some troubles.

So, I’m asking Kunos to add some official support for custom launchers. [url=""http://www.assettocorsa.net/forum/index.php?threads/alternative-launchers-official-support.32894/""]Please, show that you’re interested in it![/url][/s]", false, true),
        }.Where(x => !x.IsHidden).ToArray();
    }

    public static class ReleaseNotes {
        public static readonly PieceOfInformation[] Entries = new []{
            new PieceOfInformation(@"30_-1151466864_1817729320", null, @"Kunos career, improved driving", @"0.2.69", @"Kunos career is ready! Single events, championships, etc. Don’t forget to open settings and change driving options for your taste. Also, now CM can process driving properly (not everywhere, but still).

Of course, different bugs are more than possible. Just for in case, CM will make a backup for career.ini (you can find it in [i]…\Documents\Assetto Corsa\launcherdata\filestore[/i].

Other changes include improved performance and stability and a lot of bug fixes. As usual.", false, false),
            new PieceOfInformation(@"19_-4318689_2054519835", null, @"First release notes", @"0.3.70", @"I thought this format will be pretty good to pointing out what key changes were made. Maybe not for every new build, only for something significant.

Note should be marked as read after about two seconds of being open.", false, false),
            new PieceOfInformation(@"15_1066670269_-107239235", null, @"Proper starters", @"0.4.71", @"Introduce [b]Starter+[/b]! It works with Steam (including archievments and overlay) and, at the same time, should work without Internet and less conflict with original Kunos launcher. You can enable it by installing its addon and selecting option in Drive section of options.

Also, if your AC version is able to start the game directly without any tricks (only by launching [i]acs.exe[/i]), [b]Naive Starter[/b] was added.

And, of course, couple of beta-related changes: special option, which allows to upgrade only to tested versions (enabled by default) and button for automatically sending logs (please, use it in case of crashes).", false, false),
            new PieceOfInformation(@"19_1647635780_-761346372", null, @"Assetto Corsa 1.5.8", @"0.5.123", @"Auto-update Previews part was updated again. Also, some new options were added. One of them, “Shot in 3840×2160”, makes previews much smoother and nicer (but may take a bit more time). 

 [img=""http://i.imgur.com/IMvD9Y1.jpg|320""]Example of a new preview[/img]

Another big change is a special protocol which allows to install mods directly from different sites (and [url=""http://jsfiddle.net/x4fab/ppp0rjkm/1/embedded/result/""]something[/url] [url=""http://jsfiddle.net/x4fab/8dcj8b37/embedded/result/""]else[/url]).

 [img=""http://i.imgur.com/bWC2e3s.jpg|349""]AssettoCorsa.club[/img]

 [img=""http://i.imgur.com/0iPlIYH.jpg|349""]RaceDepartment.com[/img]

If you want to get those buttons, please, install [url=""https://greasyfork.org/en/scripts/18779-actools-content-manager-helper""]this userscript[/url]. Works for [url=""assettocorsa.club""]AssettoCorsa.club[/url] and [url=""racedepartment.com""]RaceDepartment.com[/url].", false, false),
            new PieceOfInformation(@"19_-1105440277_-1065672958", null, @"New Custom Showroom", @"0.5.133", @"Old AcTools Custom Showroom is now replaced by the brand new Custom Showroom!

 [img=""http://i.imgur.com/UB2cku6.png|360""]Lite Showroom for skins[/img]

No more those terrible bugs with normals, very different shaders and poor optimization. This code is so nice it can be used for two different showrooms instead of only one! Apart from simple (and DirectX 10-compatible) Lite Showroom here is also Fancy Showroom. It’s very heavy, poorly optimized and needed mostly for experiments, but still.

 [img=""http://i.imgur.com/HqDvMBP.png|360""]Day (SSLR!)[/img]

 [img=""http://i.imgur.com/3RJrHsV.png|360""]Night (dynamic lighting!)[/img]

Other features such as Ambient Shadow or Track Map renderers will be ported soon.", false, false),
            new PieceOfInformation(@"17_-2123634934_-1011152748", null, @"Assetto Corsa 1.5", @"0.4.79", @"Auto-update Previews part was updated and fixed to match new previews. Feel free to delete “Studio Black Showroom (AT Previews Special)” showroom.

Only problem is that new Kunos showroom isn’t quite match their previews, as you can see here:

 • [b]Original:[/b]

 [img=""http://i.imgur.com/lhyuFFy.jpg|320""]Nothing at the side[/img]

 • [b]Generated:[/b]

 [img=""http://i.imgur.com/rppyiAG.jpg|320""]Weird reflection at the side[/img]

If Kunos [url=""http://www.assettocorsa.net/forum/index.php?threads/custom-camera-position-in-showroom.26796/page-2#post-658117""]will respond[/url], I’ll fix it.", false, false),
            new PieceOfInformation(@"39_159912475_-1471391675", null, @"Default previews preset updated (again)", @"0.5.207", @"[i]Honestly, it’s starting to get on my nerves.[/i]

Kunos previews preset changed again, now they returned to using S1-Showroom. Please, open Auto-Update Previews settings and reset to Kunos preset if you’re using their style.

Some of other changes since the recent notes:

 • [b]Information Finder[/b]
That old thing from Cars Manager, I think it’s quite useful one. Now you can select from a range of search engines (if you need some which not in the list, please, [url=""https://trello.com/c/1HrHi37u/32-other-suggestions""]tell[/url]).

 • [b]Weekends[/b]
If future I’m planning to add fully customizable grids. Also, if you need Drag-races, please, tell (I’m not sure if anybody ever used them).

 • [b]New packing system[/b]
As a replacement for [url=""http://enigmaprotector.com/en/aboutvb.html""]Enigma VB[/url] since almost all AVs (even Windows Defender) are getting angry at it. Transition still may cause some troubles (but most of them are fixed). Please, [url=""https://trello.com/c/6PLhkQXe/33-other-bugs""]tell us if there is something wrong[/url].

 • [b]Online sorting[/b]
Of course, UI will be reworked, but for now like this, sorry.
 [img=""https://trello-attachments.s3.amazonaws.com/5717c1b396f7190255bdc6e5/1013x783/2f4c6a0f652172b06c01249cd3805a6a/VvPUX9V.png""]It’s better to see[/img]

 • [b]Lite Showroom improvements[/b]
Now it has some new features such as texture/materials viewing, UV exporting or ambients shadows updating (using much better algorithm than before). [url=""https://www.youtube.com/watch?v=g-ar6rcNP0s""]Video demonstration[/url].

About current plans, right now missing features from Cars Manager are being moved to Content Manager.

Thanks for support, by the way, it really inspires us! :)", false, false),
            new PieceOfInformation(@"23_623075211_-737936088", null, @"AC settings and sharing", @"0.5.234", @"First of all, [b]we finally added AC settings[/b]. Go to [i]Settings[/i] tab (F4) and switch to [i]Assetto Corsa[/i]… subtab? (I should definetly add some hotkeys for subtabs.)

Video, audio, gameplay, even some system settings (such as Developer apps or Free camera) are finished. Some of them have wider diapason than original ones. By the way, if you have a suggestion about adding a new setting or expanding an existing one, please, [url=""https://trello.com/c/ZG9kuX01/18-ac-options""]tell us[/url].

Control settings, on the other hand, are still quite WIP — you can load and save presets, tune pedals, steering wheel, buttons, switch between wheel or keyboard modes or tune FFB, but everything about proper handling joystick changes (detaching/reattaching steering wheel, for example) is still not ready. And, of course, Xbox 360 controller (I don’t have one, so it will take some extra time).

[b]Another new feature is Sharing[/b]. Now you can press that button and direct link to preset page (such as [url=""http://acstuff.ru/s/smt#noauto""]this[/url]) will be copied to the clipboard (although preset page isn’t required — CM basically works with links like [mono]acmanager://shared?id=smt[/mono], without that page everybody who doesn’t have CM installed won’t see anything, not even a error message). I’m not sure how it will work in real cases (or is it a good idea at all), so let’s see. At the moment only Quick Drive and Controls presets are supported, more (such as cars setups) will be ready soon.

 [img=""http://i.imgur.com/VjS3dpw.png|360""]Shared entry page[/img]

[i]If you think that page design looks suspiciously similar to the Telegram one, you’re not wrong. I don’t think I could ever design something better. :)[/i]

Also, sorry for messed up unseen marks. You can use “Mark All As Read” option from context menu.", false, false),
            new PieceOfInformation(@"12_1719140425_-1493326153", null, @"Some changes", @"0.4.111", @"Some of important changes since 0.4.79:

 • [b]Mods Installation[/b]
Just try to drag-n-drop archive on app’s window to add a new car or track or update it. Sadly, at the moment app supports only Zip, Rar (but not Rar5), 7Zip (non-encrypted only), Tar and GZip; also, unpacking for Rar-archives is pretty slow. But, if you’re using WinRAR, don’t worry — you can just drag'n'drop required folder from its window to CM.

 • [b]Skins Manager[/b]
Finally, it works! Just press [i]Ctrl+K[/i] on car’s page. Livery editor isn’t ready yet though.

 • [b]Filters[/b]
I'll make another page in Important Tips section, but for just some examples:
  [b][mono]bhp>500 & (weight<1000 | skins=5), brand:A*[/mono][/b] — as you can see, this one is for cars, filters all cars by their power and weight or number or skins; and, just as an addition, keeps all cars if their brand’s name is started with “A” (“,” works as “|”, totally the same);
  [b][mono]length<5000 & pits=5[/mono][/b] — this one is for tracks, filters by length and number of pitstops;
  [b][mono]practice+ & qualification-[/mono][/b] — online servers, keeps only the ones with practice session, but without qualification one;
  [b][mono]available(bhp>500)[/mono][/b] — again online servers, at this time you’ll get only servers which have available car with more than 500 bhp.", false, false),
            new PieceOfInformation(@"6_-1163755601_-1228813017", null, @"Trello", @"0.5.130", @"Good news! I've made a [url=""https://trello.com/b/MwqpL8Bw""]Trello board[/url] for this app.

 [img=""http://i.imgur.com/ydstiDS.png|32""]Trello icon[/img]

It might be a pretty handy to send feedback, watch progress, report bugs and stuff using this board. Only problem is that for commenting and rating you’ll have to sign up (or log in using Google), but I think it’s worth it (seriosly, it’s a superbly made service, Google could learn a lot from those guys about proper webapps).

Of course, you can still use e-mails for a feedback though.

[i]By the way, sorry to bother, but if you’re going to join Trello, could you please register using [url=""https://trello.com/x4fab/recommend""]this link[/url]? Thanks in advance.[/i].", false, false),
            new PieceOfInformation(@"30_-116839_1723148843", null, @"Quick Switches & Livery Editor", @"0.6.285", @"Some of important recent changes:

 • [b]Quick Switches[/b]
Just a special popup menu for changing popular settings quickly (for example, you can switch between set of apps or control preset in two clicks). If you want some specific settings, [url=""https://trello.com/c/ad89Y9rL/71-quick-switches""]tell us[/url].

 • [b]Livery Editor[/b]
If you want to see some different style, shape or numbers style, tell me and I’ll try to add them.
Also, of course, if you’re familiar with WPF, you can add them yourself using [url=""https://github.com/gro-ove/actools/tree/master/AcManager/Assets/Livery""]this syntax[/url]. Either make a push request or, as an option, I think I could add some dynamic loading from “Content (User)” folder. No problem, just ask. :)

 • [b]New image rendering code[/b]
Now you can see if image is missing or not. I hope it will load images faster and smoother than before. And another thing: now you can choose level of quality for image drawing. Maybe it could help fix some performance issues if you have any.

 • [b]Optimized values storage[/b]
Theoretically, now it won’t grow so fast as it used to do. But still, use special button in CM settings to optimize it if you have performance troubles.

 • [b]Errors fixing[/b]
App still misses some solution schemes from Cars Manager, but some other are even better now.", false, false),
            new PieceOfInformation(@"40_30816971_-1833649618", null, @"RSR, SRS and official starter for AC 1.7", @"0.7.336", @"AC 1.7 released, and now it has an official support for custom launchers! So, no more replacing original launcher and related problems. So, I strongly recommend you to switch to a new Official starter (and, in case it tells you your AC version is obsolete, [url=""https://drive.google.com/file/d/0B6GfX1zRa8pOcUxlTlU2WWZZTWM/view?usp=drivesdk""]here[/url] is the latest launcher — other starters could mess up with AC updating).

And here are some changes since previous release notes:

 • [b]New control buttons for AC 1.7[/b]
Such as ABS, traction control and MGU-K buttons.

 • [b]RSR support[/b]
It could be even better if RSR devs would help with integration, but at least it works. Also, you can use [url=""https://chrome.google.com/webstore/detail/rsr-live-timing-content-m/gpapaefcdeoafonlilichclhkmnhalcc""]this Chrome extension[/url] if you want to start races directly from browser.

 • [b]Sim Racing System support[/b]
Again, same situation — SRS devs didn’t want to integrate it, so it might be buggy. Still in development, but could be used even now. And, just in case there’ll be any issues, you can always join to the race through Online tab.

 • [b]Chromium plugin (“Awesomium”)[/b]
I personally like how IE works — it’s fast and smooth — but if you’re experiencing any problems with it (for example, if you don’t have IE 11 installed), you can always switch to using Chromium engine. Just go to Settings/Plugins and download it.

 • [b]Improved Online section[/b]
Now CM can ping up to hundreds servers at once. Sadly, it affects accuracy, but you can choose what you prefer (accuracy or pinging speed) in Settings/Online. Also, I’ve added new sorting types and made interface more user-friendly (but, again, you can always switch to the old compact look and change sorting from context menu of the label with number of servers).

Some other improvements, such as booking or totally rearranged “Latest & Shortcuts” section (I’m going to remove it completely and replace with fixed tabs in Online and LAN sections) are coming.

 • [b]Replays[/b]
Replays section was seriously updated. Now it can show what car was used, supports filtering and even sharing (only with Google Drive for now, but still).

 • [b]Screenshots[/b]
This section is mosly a placeholder at the moment. I have to find a way how to render previews quickly without damaging scrolling smoothness first.", false, false),
        }.Where(x => !x.IsHidden).ToArray();
    }
}
