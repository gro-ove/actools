using System;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Controls.Helpers {
    public interface IAcObjectsUriProvider {
        Uri GetUri(AcObjectNew obj);
    }
}