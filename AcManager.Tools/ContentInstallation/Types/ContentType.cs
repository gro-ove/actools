using System.Collections.Generic;
using AcManager.Tools.AcManagersNew;

namespace AcManager.Tools.ContentInstallation.Types {
    public abstract class ContentType {
        public static readonly ContentType Car = new TypeCar();
        public static readonly ContentType CarSkin = new TypeCarSkin();
        public static readonly ContentType Track = new TypeTrack();
        public static readonly ContentType Showroom = new TypeShowroom();
        public static readonly ContentType Font = new TypeFont();
        public static readonly ContentType Weather = new TypeWeather();

        public string NewFormat { get; }

        public string ExistingFormat { get;}

        protected ContentType(string newFormat, string existingFormat) {
            NewFormat = newFormat;
            ExistingFormat = existingFormat;
        }

        public string GetNew(string displayName) {
            return string.Format(NewFormat, displayName);
        }

        public string GetExisting(string displayName) {
            return string.Format(ExistingFormat, displayName);
        }

        public abstract IFileAcManager GetManager();

        public virtual IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] {
                new UpdateOption(ToolsStrings.Installator_UpdateEverything),
                new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst) { RemoveExisting = true }
            };
        }
    }
}