using System;

namespace FirstFloor.ModernUI.Windows {
    public interface IParametrizedUriContent {
        void OnUri(Uri uri);
    }

    public interface IImmediateContent {
        bool ImmediateChange(Uri uri);
    }
}