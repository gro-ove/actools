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

And, of course, couple of beta-related changes: special option, which allows to upgrade only to tested versions (enabled by default) and button for automatically sending logs (please, use it in case of crashes).")
        };
    }
}
