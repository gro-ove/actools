using System;
using System.Windows.Input;
using AcTools.Kn5File;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using OxyPlot;

namespace AcManager.CustomShowroom {
    public class CustomShowroomLodDefinition : Displayable {
        private string _details;

        [CanBeNull]
        public string Details {
            get => _details;
            set => Apply(value, ref _details);
        }

        private string _filename;

        public string Filename {
            get => _filename;
            set => Apply(value, ref _filename);
        }

        private int _order;

        public int Order {
            get => _order;
            set => Apply(value, ref _order);
        }

        private int _lodIndex;

        public int LodIndex {
            get => _lodIndex;
            set => Apply(value, ref _lodIndex);
        }

        private bool _useCockpitLrByDefault;

        public bool UseCockpitLrByDefault {
            get => _useCockpitLrByDefault;
            set => Apply(value, ref _useCockpitLrByDefault);
        }

        private bool _isSelected;

        public bool IsSelected {
            get => _isSelected;
            set => Apply(value, ref _isSelected);
        }

        public string DisplayLodName => $"LOD {(char)('A' + LodIndex)}";

        public Func<IKn5, PlotModel> StatsFactory { get; set; }

        public Func<IKn5, ICommand> ViewDetailsFactory { get; set; }

        public bool IsGenerated => Order >= 10;

        [CanBeNull]
        public string Checksum { get; set; }
    }
}