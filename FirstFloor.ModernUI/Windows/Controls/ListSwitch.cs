using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(Children))]
    public abstract class ListSwitch : BaseSwitch, IAddChild, IList {
        private readonly List<UIElement> _uiElements = new List<UIElement>(2);

        public IList Children => this;

        public void CopyTo(Array array, int index) {
            ((IList)_uiElements).CopyTo(array, index);
        }

        public virtual int Count => _uiElements.Count;

        public object SyncRoot => ((IList)_uiElements).SyncRoot;

        public bool IsSynchronized => ((IList)_uiElements).IsSynchronized;

        public void RemoveAt(int index) {
            _uiElements.RemoveAt(index);
        }

        public UIElement this[int index] {
            get { return _uiElements[index]; }
            set {
                var vc = _uiElements;
                if (!ReferenceEquals(vc[index], value)) {
                    vc[index] = value;
                    InvalidateMeasure();
                }
            }
        }

        public bool Contains(UIElement element) {
            return _uiElements.Contains(element);
        }

        public virtual void Clear() {
            _uiElements.Clear();
        }

        public int Add(UIElement element) {
            InvalidateMeasure();

            _uiElements.Add(element);
            return _uiElements.Count;
        }

        public int IndexOf(UIElement element) {
            return _uiElements.IndexOf(element);
        }

        public void Insert(int index, UIElement element) {
            InvalidateMeasure();
            _uiElements.Insert(index, element);
        }

        public void Remove(UIElement element) {
            _uiElements.Remove(element);
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
            get { return this[index]; }
            set { this[index] = (UIElement)value; }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return _uiElements.GetEnumerator();
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
            return _uiElements?.FirstOrDefault(TestChild);
        }
    }
}