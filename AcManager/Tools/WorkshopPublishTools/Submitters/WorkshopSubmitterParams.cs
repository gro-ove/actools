using System.Collections.Generic;
using AcManager.Tools.AcObjectsNew;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using JetBrains.Annotations;

namespace AcManager.Tools.WorkshopPublishTools.Submitters {
    public class WorkshopSubmitterParams {
        public WorkshopSubmitterParams([NotNull] WorkshopClient client, [CanBeNull] IUploadLogger log, WorkshopOriginality originality,
                [CanBeNull] List<AcJsonObjectNew> disabledObjects) {
            Client = client;
            Log = log;
            Originality = originality;
            DisabledObjects = disabledObjects ?? new List<AcJsonObjectNew>();
        }

        [NotNull]
        public WorkshopClient Client { get; }

        [CanBeNull]
        public IUploadLogger Log { get; }

        public WorkshopOriginality Originality { get; }

        [NotNull]
        public List<AcJsonObjectNew> DisabledObjects { get; }
    }
}