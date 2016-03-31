namespace AcManager.Tools.About {
    public sealed class ReleaseNotes : PieceOfInformation {
        public string Version { get; }

        private ReleaseNotes(string version, string displayName, string description)
                : base(displayName, description) {
            Version = version;
        }

        public static readonly ReleaseNotes[] Notes = {
            new ReleaseNotes("0.2.69", @"Kunos career, improved driving", @"Kunos career is ready! Single events, championships, etc. Don't forget to open settings and change driving options for your taste. Also, now CM can process driving properly (not everywhere, but still).

Of course, different bugs are more than possible. Just for in case, CM will make a backup for career.ini (you can find it in [i]…\Documents\Assetto Corsa\launcherdata\filestore[/i].

Other changes include improved performance and stability and a lot of bug fixes. As usual.

 [img=""http://i.imgur.com/xGmX4h3.jpg|240""]Check out this cat[/img]"),

            new ReleaseNotes("0.3.70", @"First release notes", @"I thought this format will be pretty good to pointing out what key changes were made. Maybe not for every new build, only for something significant.

Note should be marked as read after about two seconds of being open."),

            new ReleaseNotes("0.4.71", @"Proper starters", @"Introducing [b]Starter+[/b]! It works with Steam (including archievments and overlay) and, at the same time, should work without Internet and less conflict with original Kunos launcher. You can enable it by installing its addon and selecting option in Drive section of options.

Also, if your AC version is able to start the game directly without any tricks (only by launching [i]acs.exe[/i]), [b]Naive Starter[/b] was added.

And, of course, couple of beta-related changes: special option, which allows to upgrade only to tested versions (enabled by default) and button for automatically sending logs (please, use it in case of crashes)."),

            new ReleaseNotes("0.4.79", @"Assetto Corsa 1.5", @"Auto-update Previews part was updated and fixed to match new previews. Feel free to delete “Studio Black Showroom (AT Previews Special)” showroom.

Only problem is that new Kunos showroom isn't quite match their previews, as you can see here:

 • [b]Original:[/b]

 [img=""http://i.imgur.com/lhyuFFy.jpg|320""]Nothing at the side[/img]

 • [b]Generated:[/b]

 [img=""http://i.imgur.com/rppyiAG.jpg|320""]Weird reflection at the side[/img]

If Kunos [url=""http://www.assettocorsa.net/forum/index.php?threads/custom-camera-position-in-showroom.26796/page-2#post-658117""]will respond[/url], I'll fix it.")
        };
    }
}
