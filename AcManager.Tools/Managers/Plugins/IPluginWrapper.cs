using AcTools.Utils.Helpers;

namespace AcManager.Tools.Managers.Plugins {
    /// <summary>
    /// There are some functions in CM which requires additional libraries, such as video player or 
    /// specific starters. PluginEntry is a short description; PluginWrapper — some thing which reacts on
    /// specific PluginEntry being enabled or disabled switches its library.
    /// </summary>
    public interface IPluginWrapper : IWithId {
        void Enable();

        void Disable();
    }
}