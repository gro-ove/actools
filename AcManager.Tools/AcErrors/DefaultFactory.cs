using AcManager.Tools.AcErrors.Solver;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Tools.AcErrors {
    public class DefaultFactory : ISolversFactory {
        public ISolver GetSolver([NotNull] AcObjectNew obj, [NotNull] AcError error) {
            switch (error.Type) {
                case AcErrorType.Load_Base:
                    return null;

                case AcErrorType.Data_JsonIsMissing:
                    return new Data_JsonIsMissingSolver((AcJsonObjectNew)obj, error);

                case AcErrorType.Data_JsonIsDamaged:
                    return new Data_JsonIsDamagedSolver((AcJsonObjectNew)obj, error);

                case AcErrorType.Data_CarBrandIsMissing:
                    // TODO
                    break;

                case AcErrorType.Data_ObjectNameIsMissing:
                    return new Data_ObjectNameIsMissingSolver((AcCommonObject)obj, error);

                case AcErrorType.Data_IniIsMissing:
                    // TODO
                    break;

                case AcErrorType.Data_IniIsDamaged:
                    // TODO
                    break;

                case AcErrorType.Data_UiDirectoryIsMissing:
                    // TODO
                    break;

                case AcErrorType.Car_ParentIsMissing:
                    return new Car_ParentIsMissingSolver((CarObject)obj, error);

                case AcErrorType.Showroom_Kn5IsMissing:
                    return new Showroom_Kn5IsMissingSolver((ShowroomObject)obj, error);
                    
                case AcErrorType.Data_KunosCareerEventsAreMissing:
                    break;
                case AcErrorType.Data_KunosCareerConditions:
                    break;
                case AcErrorType.Data_KunosCareerContentIsMissing:
                    break;
                case AcErrorType.Data_KunosCareerTrackIsMissing:
                    break;
                case AcErrorType.Data_KunosCareerCarIsMissing:
                    break;
                case AcErrorType.Data_KunosCareerCarSkinIsMissing:
                    break;
                case AcErrorType.Data_KunosCareerWeatherIsMissing:
                    break;
                case AcErrorType.CarSkins_SkinsAreMissing:
                    break;
                case AcErrorType.CarSkins_DirectoryIsUnavailable:
                    break;

                case AcErrorType.Font_BitmapIsMissing:
                    return new Font_BitmapIsMissingSolver((FontObject)obj, error);

                case AcErrorType.Font_UsedButDisabled:
                    return new Font_UsedButDisabledSolver((FontObject)obj, error);

                case AcErrorType.CarSetup_TrackIsMissing:
                    return new CarSetup_TrackIsMissingSolver((CarSetupObject)obj, error);

                case AcErrorType.CarSkin_LiveryIsMissing:
                    break;
                case AcErrorType.CarSkin_PreviewIsMissing:
                    break;

                default:
                    // throw new ArgumentOutOfRangeException();
                    return null;
            }

            return null;
        }
    }
}