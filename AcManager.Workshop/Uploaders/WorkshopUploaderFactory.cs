using System;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Workshop.Uploaders {
    public static class WorkshopUploaderFactory {
        [NotNull]
        public static IWorkshopUploader Create([NotNull] JObject uploadParams) {
            if (uploadParams["version"].ToString() == "B2/1") {
                return new B2WorkshopUploader(uploadParams);
            }
            throw new NotImplementedException("Unsupported upload parameters");
        }
    }
}