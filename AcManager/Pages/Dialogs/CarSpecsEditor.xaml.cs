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
using JetBrains.Annotations;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Dialogs {
    public sealed partial class CarSpecsEditor : INotifyPropertyChanged {
        public CarObject Car { get; private set; }

        private GraphData _torqueGraph, _powerGraph;

        public GraphData TorqueGraph {
            get { return _torqueGraph; }
            set {
                if (Equals(value, _torqueGraph)) return;
                _torqueGraph = value;
                OnPropertyChanged();
            }
        }

        public GraphData PowerGraph {
            get { return _powerGraph; }
            set {
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
                CreateExtraDialogButton(AppStrings.CarSpecs_FixFormats, FixValues),
                CreateExtraDialogButton(AppStrings.CarSpecs_UpdateCurves, UpdateCurves),
                CancelButton
            };

            _fixableInputs = new[] {
                PowerInput, TorqueInput, WeightInput, AccelerationInput, TopSpeedInput, PwRatioInput
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

            if (!Regex.IsMatch(textBox.Text, @"^" + mask.Replace(@"…", @"-?\d+(?:\.\d+)?") + @"$")) {
                item = new MenuItem { Header = AppStrings.CarSpecs_FixFormat };
                item.Click += (s, e1) => FixValue(textBox);
                item.ToolTip = AppStrings.CarSpecs_FixFormat_Tooltip;
                contextMenu.Items.Add(item);
            }

            if (Equals(textBox, WeightInput)) {
                item = new MenuItem { Header = AppStrings.CarSpecs_Recalculate };
                item.Click += WeightRecalculate_OnClick;
                item.ToolTip = AppStrings.CarSpecs_Recalculate_WeightTooltip;
                contextMenu.Items.Add(item);
            }

            if (Equals(textBox, PwRatioInput)) {
                item = new MenuItem { Header = AppStrings.CarSpecs_Recalculate };
                item.Click += PwRatioRecalculate_OnClick;
                item.ToolTip = AppStrings.CarSpecs_Recalculate_PwRatioTooltip;
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
                text = text.Replace(@"0-100", "");
            }

            double value;
            if (!FlexibleParser.TryParseDouble(text, out value)) return;

            textBox.Text = Format(mask, value.ToString(CultureInfo.InvariantCulture));
        }

        private void FixValues() {
            foreach (var input in _fixableInputs) {
                FixValue(input);
            }
        }

        private void UpdateCurves() {
            var contextMenu = new ContextMenu();

            var item = new MenuItem { Header = AppStrings.CarSpecs_ScaleCurvesToPowerTorqueHeader };
            item.Click += ScaleCurves;
            item.ToolTip = AppStrings.CarSpecs_ScaleCurvesToPowerTorque_Tooltip;
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = AppStrings.CarSpecs_RecalculateCurvesUsingDataAndPowerTorqueHeader };
            item.Click += RecalculateAndScaleCurves;
            item.ToolTip = AppStrings.CarSpecs_RecalculateCurvesUsingDataAndPowerTorque_Tooltip;
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = AppStrings.CarSpecs_RecalculateCurvesUsingDataOnlyHeader };
            item.Click += RecalculateCurves;
            item.ToolTip = AppStrings.CarSpecs_RecalculateCurvesUsingDataOnly_Tooltip;
            contextMenu.Items.Add(item);

            contextMenu.IsOpen = true;
        }

        private void ScaleCurves(object sender, RoutedEventArgs e) {
            double power, torque;
            if (!FlexibleParser.TryParseDouble(PowerInput.Text, out power) ||
                    !FlexibleParser.TryParseDouble(TorqueInput.Text, out torque)) {
                ShowMessage(AppStrings.CarSpecs_SpecifyPowerAndTorqueFirst, ToolsStrings.Common_CannotDo_Title, MessageBoxButton.OK);
                return;
            }

            TorqueGraph = TorqueGraph.ScaleTo(torque);
            PowerGraph = PowerGraph.ScaleTo(power);
        }

        private void RecalculateAndScaleCurves(object sender, RoutedEventArgs e) {
            double maxPower, maxTorque;
            if (!FlexibleParser.TryParseDouble(PowerInput.Text, out maxPower) ||
                    !FlexibleParser.TryParseDouble(TorqueInput.Text, out maxTorque)) {
                ShowMessage(AppStrings.CarSpecs_SpecifyPowerAndTorqueFirst, ToolsStrings.Common_CannotDo_Title, MessageBoxButton.OK);
                return;
            }

            Lut torque;
            try {
                torque = TorquePhysicUtils.LoadCarTorque(Car.AcdData);
            } catch (FileNotFoundException ex) {
                Logging.Warning(ex);
                return;
            }

            TorqueGraph = new GraphData(torque).ScaleTo(maxTorque);
            PowerGraph = new GraphData(torque.Transform(x => x.X * x.Y)).ScaleTo(maxPower);
        }

        private void RecalculateCurves(object sender, RoutedEventArgs e) {
            var dlg = new CarTransmissionLossSelector(Car);
            dlg.ShowDialog();
            if (!dlg.IsResultOk) return;

            var lossMultipler = 100.0 / (100.0 - dlg.Value);

            Lut torque;
            try {
                torque = TorquePhysicUtils.LoadCarTorque(Car.AcdData);
            } catch (FileNotFoundException) {
                return;
            }

            torque.TransformSelf(x => x.Y * lossMultipler);
            var power = TorquePhysicUtils.TorqueToPower(torque);

            TorqueGraph = new GraphData(torque);
            PowerGraph = new GraphData(power);

            if (ShowMessage(AppStrings.CarSpecs_CopyNewPowerAndTorque, AppStrings.Common_OneMoreThing, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                // MaxY values were updated while creating new GraphData instances above
                TorqueInput.Text = Format(AppStrings.CarSpecs_Torque_FormatTooltip, torque.MaxY.ToString(@"F0", CultureInfo.InvariantCulture));
                PowerInput.Text = Format(AppStrings.CarSpecs_Power_FormatTooltip, power.MaxY.ToString(@"F0", CultureInfo.InvariantCulture));
            }
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

            Car.SpecsBhp = PowerInput.Text;
            Car.SpecsTorque = TorqueInput.Text;
            Car.SpecsWeight = WeightInput.Text;
            Car.SpecsAcceleration = AccelerationInput.Text;
            Car.SpecsTopSpeed = TopSpeedInput.Text;
            Car.SpecsPwRatio = PwRatioInput.Text;

            Car.SpecsTorqueCurve = TorqueGraph;
            Car.SpecsPowerCurve = PowerGraph;
        }

        private static string Format(string key, object value) {
            return key.Replace(@"…", value.ToInvariantString());
        }

        private void RecalculatePwRatio() {
            double power, weight;
            if (!FlexibleParser.TryParseDouble(PowerInput.Text, out power) ||
                    !FlexibleParser.TryParseDouble(WeightInput.Text, out weight)) return;

            var ratio = weight / power;
            PwRatioInput.Text = Format(AppStrings.CarSpecs_PwRatio_FormatTooltip, ratio.Round(0.01));
        }

        private void RecalculateWeight() {
            var car = Car.AcdData.GetIniFile("car.ini");
            WeightInput.Text = Format(AppStrings.CarSpecs_Weight_FormatTooltip,
                    (car["BASIC"].GetInt("TOTALMASS", CommonAcConsts.DriverWeight) - CommonAcConsts.DriverWeight).ToString(@"F0", CultureInfo.InvariantCulture));
        }

        private void PwRatioRecalculate_OnClick(object sender, RoutedEventArgs e) {
            RecalculatePwRatio();
            e.Handled = true;
        }

        private void WeightRecalculate_OnClick(object sender, RoutedEventArgs e) {
            RecalculateWeight();
            e.Handled = true;
        }

        private void Pw_OnTextChanged(object sender, TextChangedEventArgs e) {
            if (AutomaticallyRecalculate) {
                RecalculatePwRatio();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
