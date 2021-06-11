using System;
using System.Collections.Generic;
using System.Linq;

namespace AcTools.Utils.Helpers {
    public class DisposableList<T> : IDisposable where T : IDisposable {
        public DisposableList(List<T> items) {
            Items = items;
        }

        public List<T> Items { get; }

        public void Dispose() {
            Items.DisposeEverything();
        }
    }

    public class DisposableList {
        public static DisposableList<T> Create<T>(IEnumerable<T> list) where T : IDisposable {
            return new DisposableList<T>(list.ToList());
        }
    }
}