using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(Children))]
    public abstract class ListSwitch : BaseSwitch, IAddChild, IList {
        protected readonly List<UIElement> UiElements = new List<UIElement>(2);

        public IList Children => this;

        public void CopyTo(Array array, int index) {
            ((IList)UiElements).CopyTo(array, index);
        }

        public virtual int Count => UiElements.Count;

        public object SyncRoot => ((IList)UiElements).SyncRoot;

        public bool IsSynchronized => ((IList)UiElements).IsSynchronized;

        public void RemoveAt(int index) {
            RegisterChild(UiElements[index], null);
            UiElements.RemoveAt(index);
        }

        public UIElement this[int index] {
            get => UiElements[index];
            set {
                var vc = UiElements;
                if (!ReferenceEquals(vc[index], value)) {
                    RegisterChild(vc[index], value);
                    vc[index] = value;
                    InvalidateMeasure();
                }
            }
        }

        public bool Contains(UIElement element) {
            return UiElements.Contains(element);
        }

        public virtual void Clear() {
            ClearRegisteredChildren();
            UiElements.Clear();
        }

        public int Add(UIElement element) {
            InvalidateMeasure();
            UiElements.Add(element);
            RegisterChild(null, element);
            return UiElements.Count;
        }

        public int IndexOf(UIElement element) {
            return UiElements.IndexOf(element);
        }

        public void Insert(int index, UIElement element) {
            InvalidateMeasure();
            RegisterChild(null, element);
            UiElements.Insert(index, element);
        }

        public void Remove(UIElement element) {
            UiElements.Remove(element);
            RegisterChild(element, null);
        }

        int IList.Add(object value) {
            return Add((UIElement)value);
        }

        bool IList.Contains(object value) {
            return Contains(value as UIElement);
        }

        int IList.IndexOf(object value) {
            return IndexOf(value as UIElement);
        }

        void IList.Insert(int index, object value) {
            Insert(index, (UIElement)value);
        }

        bool IList.IsFixedSize => false;

        bool IList.IsReadOnly => false;

        void IList.Remove(object value) {
            Remove(value as UIElement);
        }

        object IList.this[int index] {
            get => this[index];
            set => this[index] = (UIElement)value;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return UiElements.GetEnumerator();
        }

        void IAddChild.AddChild(object value) {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var element = value as UIElement;
            if (element == null) throw new ArgumentException("Only UIElement supported", nameof(value));

            Add(element);
        }

        void IAddChild.AddText(string text) {
            if (!string.IsNullOrWhiteSpace(text)) throw new NotSupportedException();
        }

        protected abstract bool TestChild(UIElement child);

        protected override UIElement GetChild() {
            return UiElements?.FirstOrDefault(TestChild);
        }
    }
}