using System;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Helpers {
    public class GenericObjectFactoryExtension : MarkupExtension {
        public Type Type { get; set; }
        public Type T { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) {
            var genericType = Type.MakeGenericType(T);
            return Activator.CreateInstance(genericType);
        }
    }
}