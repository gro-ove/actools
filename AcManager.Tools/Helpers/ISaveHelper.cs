using System;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public interface ISaveHelper {
        [CanBeNull]
        string Key { get; }

        bool IsLoading { get; }

        void Initialize();

        void LoadOrReset();

        void Reset();

        bool Load();

        bool HasSavedData { get; }

        [CanBeNull]
        string ToSerializedString();

        void FromSerializedString([NotNull] string data);

        void FromSerializedStringWithoutSaving([NotNull] string data);

        void Save();

        bool SaveLater();

        void RegisterUpgrade<TObsolete>(Func<string, bool> test, Action<TObsolete> load);
    }
}