using System;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.AcErrors {
    public static class JObjectRestorationSchemeProvider {
        public static JObjectRestorationScheme GetScheme(AcJsonObjectNew target) {
            if (target is ShowroomObject) {
                return new JObjectRestorationScheme(
                    new JObjectRestorationScheme.Field("name", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("country", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("description", JObjectRestorationScheme.FieldType.StringMultiline),
                    new JObjectRestorationScheme.Field("tags", JObjectRestorationScheme.FieldType.StringsArray),

                    new JObjectRestorationScheme.Field("year", JObjectRestorationScheme.FieldType.Number),
                    new JObjectRestorationScheme.Field("author", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("url", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("version", JObjectRestorationScheme.FieldType.String)
                );
            }

            if (target is CarObject) {
                return new JObjectRestorationScheme(
                    new JObjectRestorationScheme.Field("name", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("brand", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("class", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("country", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("description", JObjectRestorationScheme.FieldType.StringMultiline),
                    new JObjectRestorationScheme.Field("tags", JObjectRestorationScheme.FieldType.StringsArray),

                    new JObjectRestorationScheme.Field("torqueCurve", JObjectRestorationScheme.FieldType.PairsArray),
                    new JObjectRestorationScheme.Field("powerCurve", JObjectRestorationScheme.FieldType.PairsArray),

                    new JObjectRestorationScheme.Field("bhp", "specs", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("torque", "specs", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("weight", "specs", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("topspeed", "specs", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("acceleration", "specs", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("pwratio", "specs", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("range", "specs", JObjectRestorationScheme.FieldType.String),

                    new JObjectRestorationScheme.Field("year", JObjectRestorationScheme.FieldType.Number),
                    new JObjectRestorationScheme.Field("author", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("url", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("version", JObjectRestorationScheme.FieldType.String)
                );
            }

            if (target is TrackObject) {
                return new JObjectRestorationScheme(
                    new JObjectRestorationScheme.Field("name", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("description", JObjectRestorationScheme.FieldType.StringMultiline),
                    new JObjectRestorationScheme.Field("tags", JObjectRestorationScheme.FieldType.StringsArray),
                    new JObjectRestorationScheme.Field("geotags", JObjectRestorationScheme.FieldType.StringsArray),

                    new JObjectRestorationScheme.Field("country", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("city", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("length", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("width", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("pitboxes", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("run", JObjectRestorationScheme.FieldType.String),

                    new JObjectRestorationScheme.Field("year", JObjectRestorationScheme.FieldType.Number),
                    new JObjectRestorationScheme.Field("author", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("url", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("version", JObjectRestorationScheme.FieldType.String)
                );
            }

            if (target is CarSkinObject) {
                return new JObjectRestorationScheme(
                    new JObjectRestorationScheme.Field("skinname", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("drivername", JObjectRestorationScheme.FieldType.NonNullString),
                    new JObjectRestorationScheme.Field("country", JObjectRestorationScheme.FieldType.NonNullString),
                    new JObjectRestorationScheme.Field("team", JObjectRestorationScheme.FieldType.NonNullString),
                    new JObjectRestorationScheme.Field("number", JObjectRestorationScheme.FieldType.NonNullString),
                    new JObjectRestorationScheme.Field("priority", JObjectRestorationScheme.FieldType.Number),

                    new JObjectRestorationScheme.Field("year", JObjectRestorationScheme.FieldType.Number),
                    new JObjectRestorationScheme.Field("author", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("url", JObjectRestorationScheme.FieldType.String),
                    new JObjectRestorationScheme.Field("version", JObjectRestorationScheme.FieldType.String)
                );
            }

            throw new NotImplementedException();
        }
    }
}
