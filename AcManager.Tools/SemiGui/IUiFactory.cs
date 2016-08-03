using System;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public interface IUiFactory<out T> where T : IDisposable {
        [NotNull]
        T Create();
    }
}