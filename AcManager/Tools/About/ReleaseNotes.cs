namespace AcManager.Tools.About {
    public sealed class ReleaseNotes : PieceOfInformation {
        public string Version { get; }

        private ReleaseNotes(string version, string displayName, string description)
                : base(displayName, description) {
            Version = version;
        }

        public static readonly ReleaseNotes[] Notes = {
            new ReleaseNotes("0.2.69", @"Kunos career, improved driving",
                    @"Kunos career is ready! Single events, championships, etc. Don't forget to open settings and change driving options for your taste. Also, now CM can process driving properly (not everywhere, but still).

Of course, different bugs are more than possible. Just for in case, CM will make a backup for career.ini (you can find it in [i]…\Documents\Assetto Corsa\launcherdata\filestore[/i].

Other changes include improved performance and stability and a lot of bug fixes. As usual.

 [img=""http://i.imgur.com/xGmX4h3.jpg|240""]Check out this cat[/img]"),

            new ReleaseNotes("0.3.70", @"First release notes",
                    @"I thought this format will be pretty good to pointing out what key changes were made. Maybe not for every new build, only for something significant.

Note should be marked as read after about two seconds of being open."),

            new ReleaseNotes("0.4.71", @"Proper starters",
                    @"Introduce [b]Starter+[/b]! It works with Steam (including archievments and overlay) and, at the same time, should work without Internet and less conflict with original Kunos launcher. You can enable it by installing its addon and selecting option in Drive section of options.

Also, if your AC version is able to start the game directly without any tricks (only by launching [i]acs.exe[/i]), [b]Naive Starter[/b] was added.

And, of course, couple of beta-related changes: special option, which allows to upgrade only to tested versions (enabled by default) and button for automatically sending logs (please, use it in case of crashes)."),

            new ReleaseNotes("0.4.79", @"Assetto Corsa 1.5",
                    @"Auto-update Previews part was updated and fixed to match new previews. Feel free to delete “Studio Black Showroom (AT Previews Special)” showroom.

Only problem is that new Kunos showroom isn't quite match their previews, as you can see here:

 • [b]Original:[/b]

 [img=""http://i.imgur.com/lhyuFFy.jpg|320""]Nothing at the side[/img]

 • [b]Generated:[/b]

 [img=""http://i.imgur.com/rppyiAG.jpg|320""]Weird reflection at the side[/img]

If Kunos [url=""http://www.assettocorsa.net/forum/index.php?threads/custom-camera-position-in-showroom.26796/page-2#post-658117""]will respond[/url], I'll fix it."),

            new ReleaseNotes("0.4.111", @"Some changes", @"Some of important changes since 0.4.79:

 • [b]Mods Installation[/b]
Just try to drag-n-drop archive on app's window to add a new car or track or update it. Sadly, at the moment app supports only Zip, Rar (but not Rar5), 7Zip (non-encrypted only), Tar and GZip; also, unpacking for Rar-archives is pretty slow. But, if you're using WinRAR, don't worry — you can just drag'n'drop required folder from its window to CM.

 • [b]Skins Manager[/b]
Finally, it works! Just press [i]Ctrl+K[/i] on car's page. Livery editor isn't ready yet though.

 • [b]Filters[/b]
I'll make another page in Important Tips section, but for just some examples:
  [b][mono]bhp>500 & (weight<1000 | skins=5), brand:A*[/mono][/b] — as you can see, this one is for cars, filters all cars by their power and weight or number or skins; and, just as an addition, keeps all cars if their brand's name is started with “A” (“,” works as “|”, totally the same);
  [b][mono]length<5000 & pits=5[/mono][/b] — this one is for tracks, filters by length and number of pitstops;
  [b][mono]practice+ & qualification-[/mono][/b] — online servers, keeps only the ones with practice session, but without qualification one;
  [b][mono]available(bhp>500)[/mono][/b] — again online servers, at this time you'll get only servers which have available car with more than 500 bhp."),

            new ReleaseNotes("0.5.123", @"Assetto Corsa 1.5.8",
                    @"Auto-update Previews part was updated again. Also, some new options were added. One of them, “Shot in 3840×2160”, makes previews much smoother and nicer (but may take a bit more time). 

 [img=""http://i.imgur.com/IMvD9Y1.jpg|320""]Example of a new preview[/img]

Another big change is a special protocol which allows to install mods directly from different sites (and [url=""http://jsfiddle.net/x4fab/ppp0rjkm/1/embedded/result/""]something[/url] [url=""http://jsfiddle.net/x4fab/8dcj8b37/embedded/result/""]else[/url]).

 [img=""http://i.imgur.com/bWC2e3s.jpg|349""]AssettoCorsa.club[/img]

 [img=""http://i.imgur.com/0iPlIYH.jpg|349""]RaceDepartment.com[/img]

If you want to get those buttons, please, install [url=""https://greasyfork.org/en/scripts/18779-actools-content-manager-helper""]this userscript[/url]. Works for [url=""assettocorsa.club""]AssettoCorsa.club[/url] and [url=""racedepartment.com""]RaceDepartment.com[/url]."),

            new ReleaseNotes("0.5.130", @"Trello", @"Good news! I've made a [url=""https://trello.com/b/MwqpL8Bw""]Trello board[/url] for this app.

 [img=""http://i.imgur.com/ydstiDS.png|32""]Trello icon[/img]

It might be a pretty handy to send feedback, watch progress, report bugs and stuff using this board. Only problem is that for commenting and rating you'll have to sign up (or log in using Google), but I think it's worth it (seriosly, it's a superbly made service, Google could learn a lot from those guys about proper webapps).

Of course, you can still use e-mails for a feedback though.

[i]By the way, sorry to bother, but if you're going to join Trello, could you please register using [url=""https://trello.com/x4fab/recommend""]this link[/url]? Thanks in advance.[/i]."),

            new ReleaseNotes("0.5.133", @"New Custom Showroom", @"Old AcTools Custom Showroom is now replaced by the brand new Custom Showroom!

 [img=""http://i.imgur.com/UB2cku6.png|360""]Lite Showroom for skins[/img]

No more those terrible bugs with normals, very different shaders and poor optimization. This code is so nice it can be used for two different showrooms instead of only one! Apart from simple (and DirectX 10-compatible) Lite Showroom here is also Fancy Showroom. It's very heavy, poorly optimized and needed mostly for experiments, but still.

 [img=""http://i.imgur.com/HqDvMBP.png|360""]Day (SSLR!)[/img]

 [img=""http://i.imgur.com/3RJrHsV.png|360""]Night (dynamic lighting!)[/img]

Other features such as Ambient Shadow or Track Map renderers will be ported soon.")
        };
    }
}
