using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public interface ICarSetupObject : INotifyPropertyChanged {
        bool Enabled { get; }

        [NotNull]
        string DisplayName { get; }

        [NotNull]
        string CarId { get; }

        [CanBeNull]
        string TrackId { get; }

        [CanBeNull]
        TrackObject Track { get; }

        bool IsReadOnly { get; }

        Task EnsureDataLoaded();

        int? Tyres { get; }

        IEnumerable<KeyValuePair<string, double?>> Values { get; }

        double? GetValue(string key);

        void SetValue(string key, double entryValue);
    }
}