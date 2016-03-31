using FirstFloor.ModernUI.Windows.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AcManager.Annotations;
using AcManager.Tools.Objects;

namespace AcManager.Pages.Dialogs {
    /// <summary>
    /// Interaction logic for CarTransmissionLoss.xaml
    /// </summary>
    public partial class CarTransmissionLossSelector : INotifyPropertyChanged {
        private int _value;

        public int Value {
            get { return _value; }
            private set {
                if (value == _value) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        public CarTransmissionLossSelector(CarObject car) {
            InitializeComponent();
            DataContext = this;

            Title = "Transmission Loss for " + car.DisplayName;

            Value = 20;
            Buttons = new [] { OkButton, CancelButton };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
