using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Navigation;
using AcManager.Controls.UserControls;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Drive {
    public partial class Rsr {
        private RsrViewModel Model => (RsrViewModel)DataContext;

        public Rsr() {
            DataContext = new RsrViewModel();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control))
            });
            InitializeComponent();
            WebBrowser.SetScriptProvider(new ScriptProvider(Model));
        }

        public class RsrViewModel : NotifyPropertyChanged {
            internal RsrViewModel() { }

            public string StartPage => "http://www.radiators-champ.com/RSRLiveTiming/index.php?page=events";

            private int? _eventId;

            public int? EventId {
                get { return _eventId; }
                set {
                    if (Equals(value, _eventId)) return;
                    _eventId = value;
                    OnPropertyChanged();
                    GoCommand.OnCanExecuteChanged();
                }
            }

            private RelayCommand _goCommand;

            public RelayCommand GoCommand => _goCommand ?? (_goCommand = new RelayCommand(o => {
                ;
            }, o => EventId.HasValue));
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        [ComVisible(true)]
        public class ScriptProvider : BaseScriptProvider {
            private readonly RsrViewModel _model;

            public ScriptProvider(RsrViewModel model) {
                _model = model;
            }

            public void SetEventId(int value) {
                _model.EventId = value;
            }
        }

        private void WebBrowser_OnNavigated(object sender, NavigationEventArgs e) {
            var match = Regex.Match(e.Uri.ToString(), @"\beventId=(\d+)");
            Model.EventId = match.Success ? match.Groups[1].Value.ToInvariantInt() : null;

            if (e.Uri.ToString().StartsWith("http://www.radiators-champ.com/RSRLiveTiming/")) {
                const string userCss = @"
body { background: black !important }
body, #cntdwn, .driver_profile { color: white !important }
* { font-family: Segoe UI, sans-serif !important }
a { color: #E20035 !important }
a:hover { color: #CA0030 !important }
a:focused, a:active { color: #CA0030 !important }
#page { background: #333 !important }
h1, h2 { font-weight: 100 !important; font-family: Segoe UI Light, Segoe UI, sans-serif !important }
.margin_top_0 { margin-top: 20px !important; }

#new_menu ul li a[href], #new_menu ul li ul li a[href] { background: #444 !important; text-shadow: none !important; color: white !important; font-weight: normal !important; border: none !important; font-size: 12px !important; }
#new_menu ul li a[href]:hover { background: #666 !important; }
#new_menu ul { border: none !important; box-shadow: 0 0 10px rgba(0,0,0,.2) !important; position: relative !important; z-index: 9999 !important }
#new_menu ul li ul { position: fixed !important; }

.rank_table thead tr th { background: #E20035 !important; font-size: 12px !important; padding: 4px !important; }

.driver_profile { border: none !important; background: transparent !important } 
.avatar_image, .submit_button, .upload_inputtext, input[type='submit'] { border: none !important; border-radius: 0 !important }
.submit_button { margin-left: 2px !important; display: block !important; margin-bottom: 24px !important }

input[type='submit'] { background: #E20035 !important; height: 36px !important; font-size: 14px !important; outline: none !important }
input[type='submit']:hover, input[type='submit']:active { background: #CA0030 !important }

table tbody tr:nth-child(even) td { background: #404040 !important; }
table tbody tr:nth-child(odd) td { background: #383838 !important; }
table tbody tr:hover td { background: #555 !important; }

#footer { background: transparent !important; height: 50px !important; overflow: hidden !important; font-size: 12px !important; line-height: 100px !important; position: relative !important; top: -20px !important; border: none !important; }

.lapok a { font-size: 14px !important; color: #E20035 !important; width: 23px !important; height: 24px !important; padding-right: 1px !important; line-height: 23px !important; font-weight: 700 !important; vertical-align: middle !important; text-align: center !important; display: inline-block !important; text-decoration: none !important; background-color: transparent !important; border: none !important; border-radius: 0 !important; }
.lapok a.active { color: #888 !important; }
.lapok a:hover { color: #CA0030 !important; }

#no_filter { vertical-align: top !important; }
.borderRadius, .borderRadiusTp, .driver_profile { border-radius: 0 !important; }

#header, #pb_bnr, #contentRSR .right_top_name, .change_style_div { display: none !important; }";
                WebBrowser.UserStyle = userCss;
            } else {
                WebBrowser.UserStyle = null;
            }
        }
    }
}
