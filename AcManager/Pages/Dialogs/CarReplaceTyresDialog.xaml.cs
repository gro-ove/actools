using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Windows.Controls;
using System.Windows.Controls;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
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

                        if (car.AcdData == null) return;
                        var tyres = car.AcdData.GetIniFile("tyres.ini");

                        var version = tyres["HEADER"].GetInt("VERSION", -1);
                        if (version < 3) continue;

                        list.AddRange(from id in tyres.GetSectionNames(@"TYRES", -1)
                                      let section = tyres[id]
                                      let thermal = tyres[$@"THERMAL_{id}"]
                                      where section.GetNonEmpty("NAME") != null && thermal.GetNonEmpty("PERFORMANCE_CURVE") != null
                                      select new TyresEntry(car, version, tyres[id], thermal));

                        //if (list.Count % 5 == 0) {
                            await Task.Delay(10);
                        //}
                    }
                }

                new CarReplaceTyresDialog(target, list).ShowDialog();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t replace tyres", e);
            }
        }

        public class TyresEntry : Displayable {
            public CarObject Source { get; }

            public int Version { get; }

            public IniFileSection MainSection { get; }

            public IniFileSection ThermalSection { get; }

            public TyresEntry(CarObject source, int version, IniFileSection mainSection, IniFileSection thermalSection) {
                Source = source;
                Version = version;
                MainSection = mainSection;
                ThermalSection = thermalSection;
            }
        }
    }
}
