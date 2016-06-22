using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Annotations;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using Microsoft.Win32;

namespace AcManager.Pages.Dialogs {
    public partial class CarSpecsEditor : INotifyPropertyChanged {
        public CarObject Car { get; private set; }

        private GraphData _torqueGraph, _powerGraph;

        public GraphData TorqueGraph {
            get { return _torqueGraph; }
            private set {
                if (Equals(value, _torqueGraph)) return;
                _torqueGraph = value;
                OnPropertyChanged();
            }
        }

        public GraphData PowerGraph {
            get { return _powerGraph; }
            private set {
                if (Equals(value, _powerGraph)) return;
                _powerGraph = value;
                OnPropertyChanged();
            }
        }

        private readonly TextBox[] _fixableInputs;

        public CarSpecsEditor(CarObject car) {
            _automaticallyRecalculate = ValuesStorage.GetBool(AutomaticallyRecalculateKey); 

            InitializeComponent();
            DataContext = this;

            Buttons = new[] {
                OkButton,
                CreateExtraDialogButton("Fix Formats", FixValues),
                CreateExtraDialogButton("Update Curves", UpdateCurves),
                CancelButton
            };

            _fixableInputs = new[] {
                BhpInput, TorqueInput, WeightInput, AccelerationInput, TopSpeedInput, PwRatioInput
            };

            foreach (var input in _fixableInputs) {
                input.PreviewMouseDown += FixableInput_MouseDown;
            }

            Car = car;
            TorqueGraph = car.SpecsTorqueCurve;
            PowerGraph = car.SpecsPowerCurve;

            Closing += CarSpecsEditor_Closing;
        }

        private static string GetTextBoxMask(FrameworkElement box) {
            return box.ToolTip as string;
        }

        private void FixableInput_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Right) return;

            var textBox = sender as TextBox;
            if (textBox == null) return;

            var contextMenu = new ContextMenu();

            MenuItem item;

            var mask = GetTextBoxMask(textBox);
            if (mask == null) return;

            if (!Regex.IsMatch(textBox.Text, "^" + mask.Replace("…", @"-?\d+(?:\.\d+)?") + "$")) {
                item = new MenuItem { Header = "Fix Format" };
                item.Click += (s, e1) => FixValue(textBox);
                item.ToolTip = "Set proper format for value";
                contextMenu.Items.Add(item);
            }

            if (Equals(textBox, PwRatioInput)) {
                item = new MenuItem { Header = "Recalculate" };
                item.Click += PwRatioRecalculate_OnClick;
                item.ToolTip = "Recalculate value using BHP and weight values";
                contextMenu.Items.Add(item);
            }

            contextMenu.AddTextBoxItems();

            e.Handled = true;
            contextMenu.IsOpen = true;
        }

        private void FixValue(TextBox textBox) {
            var mask = GetTextBoxMask(textBox);
            if (mask == null) return;

            var text = textBox.Text;
            if (Equals(textBox, AccelerationInput)) {
                text = text.Replace("0-100", "");
            }

            double value;
            if (!FlexibleParser.TryParseDouble(text, out value)) return;

            textBox.Text = mask.Replace("…", value.ToString(CultureInfo.InvariantCulture));
        }

        private void FixValues() {
            foreach (var input in _fixableInputs) {
                FixValue(input);
            }
        }

        private void UpdateCurves() {
            var contextMenu = new ContextMenu();

            var item = new MenuItem { Header = @"Scale Curves to BHP/Torque Values" };
            item.Click += ScaleCurves;
            item.ToolTip = @"Curves will be scaled to fit BHP/Torque values from ui_car.json (you can see them above)";
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = @"Recalculate Curves Using Data & BHP/Torque Values" };
            item.Click += RecalculateAndScaleCurves;
            item.ToolTip = @"Curves will be recalculated based on engine.ini, power.lut and BHP/Torque values from ui_car.json";
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = @"Recalculate Curves Using Data Only" };
            item.Click += RecalculateCurves;
            item.ToolTip = @"Curves will be recalculated based on engine.ini, but you’ll have to provide transmission loss";
            contextMenu.Items.Add(item);

            contextMenu.IsOpen = true;
        }

        private void ScaleCurves(object sender, RoutedEventArgs e) {
            double power, torque;
            if (!FlexibleParser.TryParseDouble(BhpInput.Text, out power) ||
                !FlexibleParser.TryParseDouble(TorqueInput.Text, out torque)) {
                ShowMessage("You have to specify BHP and Torque values first", "Can’t do", MessageBoxButton.OK);
                return;
            }

            TorqueGraph = TorqueGraph.ScaleTo(torque);
            PowerGraph = PowerGraph.ScaleTo(power);
        }

        private void RecalculateAndScaleCurves(object sender, RoutedEventArgs e) {
            double power, torque;
            if (!FlexibleParser.TryParseDouble(BhpInput.Text, out power) ||
                !FlexibleParser.TryParseDouble(TorqueInput.Text, out torque)) {
                ShowMessage("You have to specify BHP and Torque values first", "Can’t do", MessageBoxButton.OK);
                return;
            }

            Dictionary<double, double> torqueData;
            try {
                torqueData = TorquePhysicUtils.LoadCarTorque(Car.Location);
            } catch (FileNotFoundException) {
                return;
            }

            torqueData[0] = 0;
            var torqueDataOrdered = torqueData.OrderBy(x => x.Key);

            TorqueGraph = new GraphData(torqueDataOrdered.ToDictionary(x => x.Key, x => x.Value)).ScaleTo(torque);
            PowerGraph = new GraphData(torqueDataOrdered.ToDictionary(x => x.Key, x => x.Key * x.Value)).ScaleTo(power);
        }

        private void RecalculateCurves(object sender, RoutedEventArgs e) {
            var dlg = new CarTransmissionLossSelector(Car);
            dlg.ShowDialog();

            if (!dlg.IsResultOk) return;

            var lossMultipler = 100.0/(100.0 - dlg.Value);

            Dictionary<double, double> torqueData;
            try {
                torqueData = TorquePhysicUtils.LoadCarTorque(Car.Location);
            } catch (FileNotFoundException) {
                return;
            }

            torqueData[0] = 0;
            var torqueDataOrdered = torqueData.OrderBy(x => x.Key);

            var torqueDataResult = torqueDataOrdered.ToDictionary(x => x.Key, x => x.Value * lossMultipler);
            var powerDataResult = torqueDataResult.ToDictionary(x => x.Key,
                                                                x => TorquePhysicUtils.TorqueToPower(x.Value, x.Key));

            TorqueGraph = new GraphData(torqueDataResult);
            PowerGraph = new GraphData(powerDataResult);

            if (
                ModernDialog.ShowMessage(@"Copy new values to Torque and BHP?", @"One More Thing",
                                         MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                TorqueInput.Text = torqueDataResult.Values.Max().ToString("F0", CultureInfo.InvariantCulture) + "Nm";
                BhpInput.Text = powerDataResult.Values.Max().ToString("F0", CultureInfo.InvariantCulture) + " bhp";
            }
        }

        [UsedImplicitly]
        private void SetCurvesFromFile() {
            var contextMenu = new ContextMenu();

            var item = new MenuItem { Header = "Set Power Curve" };
            item.Click += SelectPowerCurveFile;
            contextMenu.Items.Add(item);

            contextMenu.IsOpen = true;
        }

        private void SelectPowerCurveFile(object sender, RoutedEventArgs e) {
            var dialog = new OpenFileDialog { Filter = FileDialogFilters.ImagesFilter, Title = "Select Image For Power Curve" };
            if (dialog.ShowDialog() == true) {
                SetPowerCurveFromFile(dialog.FileName);
            }
        }

        private void SetPowerCurveFromFile(string filename) {
            new CarSpecsEditor_CurveFromFile(filename).ShowDialog();
        }

        private const string AutomaticallyRecalculateKey = "__carspecseditor_autorecal";
        private bool _automaticallyRecalculate;

        public bool AutomaticallyRecalculate {
            get { return _automaticallyRecalculate; }
            set {
                _automaticallyRecalculate = value;
                ValuesStorage.Set(AutomaticallyRecalculateKey, value);
            }
        }

        private void CarSpecsEditor_Closing(object sender, CancelEventArgs e) {
            if (!IsResultOk) return;

            Car.SpecsBhp = BhpInput.Text;
            Car.SpecsTorque = TorqueInput.Text;
            Car.SpecsWeight = WeightInput.Text;
            Car.SpecsAcceleration = AccelerationInput.Text;
            Car.SpecsTopSpeed = TopSpeedInput.Text;
            Car.SpecsPwRatio = PwRatioInput.Text;

            Car.SpecsTorqueCurve = TorqueGraph;
            Car.SpecsPowerCurve = PowerGraph;
        }

        private void RecalculatePwRatio() {
            double power, weight;
            if (!FlexibleParser.TryParseDouble(BhpInput.Text, out power) ||
                !FlexibleParser.TryParseDouble(WeightInput.Text, out weight)) return;

            var ratio = weight/power;
            PwRatioInput.Text = ratio.ToString("F2", CultureInfo.InvariantCulture) + "kg/cv";
        }

        private void PwRatioRecalculate_OnClick(object sender, RoutedEventArgs e) {
            RecalculatePwRatio();
            e.Handled = true;
        }

        private void Input_OnTextChanged(object sender, TextChangedEventArgs e) {
            if (AutomaticallyRecalculate) {
                RecalculatePwRatio();
            }
        }

        private void Input_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key != Key.Up && e.Key != Key.Down) return;

            var textBox = sender as TextBox;
            if (textBox == null) return;

            var d = (e.Key == Key.Up ? 1 : -1)*
                    ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control ? 10 : 1);
            double value;
            if (!FlexibleParser.TryParseDouble(textBox.Text, out value)) return;
            textBox.Text = FlexibleParser.ReplaceDouble(textBox.Text, value + d);
            e.Handled = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
