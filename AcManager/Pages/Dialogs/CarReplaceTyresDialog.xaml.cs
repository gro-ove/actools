using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Windows.Controls;
using System.Windows.Controls;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class CarReplaceTyresDialog {
        private CarReplaceTyresDialog(CarObject target, IList<TyresEntry> tyres) {
            DataContext = new ViewModel(target, tyres);
            InitializeComponent();
            
            Buttons = new[] { OkButton, CancelButton };
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public CarObject Target { get; }

            public IList<TyresEntry> Tyres { get; }

            public ViewModel(CarObject target, IList<TyresEntry> tyres) {
                Target = target;
                Tyres = tyres;
            }
        }

        public static async Task Run(CarObject target) {
            try {
                var wrappers = CarsManager.Instance.WrappersList.ToList();
                var list = new List<TyresEntry>(wrappers.Count);

                using (var waiting = new WaitingDialog("Getting a list of tyres…")) {
                    for (var i = 0; i < wrappers.Count; i++) {
                        if (waiting.CancellationToken.IsCancellationRequested) return;

                        var wrapper = wrappers[i];
                        var car = (CarObject)await wrapper.LoadedAsync();
                        waiting.Report(new AsyncProgressEntry(car.DisplayName, i, wrappers.Count));

                        if (car.Author != "Kunos") continue;

                        if (car.AcdData == null) return;
                        var tyres = car.AcdData.GetIniFile("tyres.ini");

                        var version = tyres["HEADER"].GetInt("VERSION", -1);
                        if (version < 3) continue;

                        list.AddRange(from id in tyres.GetSectionNames(@"FRONT", -1).Concat(tyres.GetSectionNames(@"REAR", -1))
                                      let section = tyres[id]
                                      let thermal = tyres[$@"THERMAL_{id}"]
                                      where section.GetNonEmpty("NAME") != null && thermal.GetNonEmpty("PERFORMANCE_CURVE") != null
                                      select new TyresEntry(car, version, tyres[id], thermal, id.StartsWith(@"FRONT")));

                        if (list.Count % 3 == 0) {
                            await Task.Delay(10);
                        }
                    }
                }

                new CarReplaceTyresDialog(target, list).ShowDialog();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t replace tyres", e);
            }
        }

        public sealed class TyresEntry : Displayable {
            public CarObject Source { get; }

            public int Version { get; }

            public IniFileSection MainSection { get; }

            public IniFileSection ThermalSection { get; }

            public bool RearTyres { get; set; }

            public TyresEntry(CarObject source, int version, IniFileSection mainSection, IniFileSection thermalSection, bool rearTyres) {
                Source = source;
                Version = version;
                MainSection = mainSection;
                ThermalSection = thermalSection;
                RearTyres = rearTyres;
                
                DisplayName = $@"{mainSection.GetNonEmpty("NAME")} {GetWidth()}/{GetProfile()}/R{GetRimRadius()}";
            }
            
            private double GetWidth() {
                return (MainSection.GetDouble("WIDTH", 0) * 1000).Round(1d);
            }

            private double GetRimRadius() {
                return (MainSection.GetDouble("RIM_RADIUS", 0) * 100 / 2.54 * 2 - 1).Round(0.1d);
            }

            private double GetProfile() {
                return (100d * (MainSection.GetDouble("RADIUS", 0) - (MainSection.GetDouble("RIM_RADIUS", 0) + 0.0127)) / MainSection.GetDouble("WIDTH", 0))
                        .Round(5d);
            }
        }
    }
}
