using System.Net;
using JetBrains.Annotations;

namespace AcManager.Workshop {
    public class WorkshopException : WorkshopHttpException {
        [CanBeNull]
        public string RemoteException { get; }

        [CanBeNull]
        public string[] RemoteStackTrace { get; }

        public WorkshopException(HttpStatusCode code, string errorMesage, string remoteException, string[] remoteStackTrace) : base(code, errorMesage) {
            RemoteException = remoteException;
            RemoteStackTrace = remoteStackTrace;
        }
    }
}