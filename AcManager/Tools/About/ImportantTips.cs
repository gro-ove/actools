namespace AcManager.Tools.About {
    public sealed class ImportantTips : PieceOfInformation {
        private ImportantTips(string displayName, string description)
                : base(displayName, description) {}

        public static readonly ImportantTips[] Tips = {
            new ImportantTips(@"Command-Line Arguments", @"CM has a bunch of special options you can set by using command-line arguments. Or just create a new [i]Arguments.txt[/i] file in [i]…\AppData\Local\AcTools Content Manager[/i] and write them in it (one argument per line).

 [img=""http://i.imgur.com/qO7oOVn.png|240""]Example of that file[/img]

Here are some of the arguments:
 [b][mono]--manager-mode[/mono][/b] — enables “Content” section (keep in mind that we disabled it on purpose, it's not finished yet).
 [b][mono]--storage-location=LOCATION[/mono][/b] — change location of data folder (of course, this one should be used only as a command-line argument).
 [b][mono]--ping-concurrency=30[/mono][/b] — number of servers being pinged concurrently.
 [b][mono]--enable-race-ini-restoration=false[/mono][/b] — don't revert changes in [i]race.ini[/i].

You can see the full list [url=""https://github.com/gro-ove/actools/blob/master/AcManager/AppArguments.cs""]here[/url]."),

            new ImportantTips(@"Starters", @"[b]This part is very important. Please, take a moment.[/b]

Since Steam version of AC can't be started only by running [i]acs.exe[/i], CM has to use some weird ways to do the trick. At the moment there are three strange ways to start the game (apart from direct starting by [b]Naive Starter[/b]). All of them had their flaws, that's why you have to select one which suits you:

 • [b]Tricky Starter[/b]
First one. Uses pretty weird approach, which requires to temporary remove [i]AssettoCorsa.exe[/i] out of the way. On the bright side, this is the only change it does. So:
  + Steam-friendly: Steam overlay & archievments will work without any problems;
  + Tested: firstly was implemented in Cars Manager;
  − Unreliable: using approach could be blocked in any AC update;
  − Tricky: sadly, doesn't work without Internet connection;
  − Incompatible: can't be used when original launcher is running;
  − Potentially adverse: if CM will suddenly get killed, you'll have to restore [i]AssettoCorsa.exe[/i] from [i]AssettoCorsa_backup.exe[/i] manually;
  − Slow: using approach requires to wait some time at some point;
  − Limited: can't specify version (32-bit or 64-bit).

 • [b]Starter+[/b]
Changes original launcher a little.
  + Steam-friendly: Steam overlay & archievments will work without any problems;
  + Relieable: even though it changes original launcher, it still works as a launcher
  − Unreliable: if Kunos will change [i]AssettoCorsa.exe[/i], you won't get new features until addon will be updated;
  − Can't be enabled or disabled while [i]AssettoCorsa.exe[/i] is runned;
  − Slow: use original laucher, which isn't very fast.

 • [b]SSE Starter[/b]
Quite questionable solution. I won't spread about it too much, just basics:
  + Compatible: doesn't mess with [i]AssettoCorsa.exe[/i] at all, can work even if it's runned;
  + Tested: surprisingly solid, works without Internet;
  + Fast: faster, than everything else;
  − Steam-unfriendly: doesn't work for archievments & stuff.

 • [b]Naive Starter[/b]
CM just starts [i]acs.exe[/i] (or [i]acs_x86.exe[/i]), everything else depends on AC version you use. If you're using original Steam version — won't work at all.") {
                Key = "starters"
            }
        };
    }
}