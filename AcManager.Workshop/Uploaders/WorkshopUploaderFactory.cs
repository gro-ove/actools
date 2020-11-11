using System;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Workshop.Uploaders {
    public static class WorkshopUploaderFactory {
        [NotNull]
        public static IWorkshopUploader Create(string uploaderId, [NotNull] JObject uploadParams) {
            if (uploaderId == "AS/1") {
                return new AcStuffWorkshopUploader(uploadParams);
            }
            if (uploaderId == "B2/1") {
                return new B2WorkshopUploader(uploadParams);
            }
            throw new NotImplementedException("Unsupported uploader ID");
        }
    }
}