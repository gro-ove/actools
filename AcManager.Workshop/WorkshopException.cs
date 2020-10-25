using System;
using System.Net;
using JetBrains.Annotations;

namespace AcManager.Workshop {
    public class WorkshopException : Exception {
        public HttpStatusCode Code { get; }

        [CanBeNull]
        public string RemoteException { get; }

        [CanBeNull]
        public string[] RemoteStackTrace { get; }

        public WorkshopException(HttpStatusCode code, string errorMesage, string remoteException, string[] remoteStackTrace) : base(errorMesage) {
            Code = code;
            RemoteException = remoteException;
            RemoteStackTrace = remoteStackTrace;
        }
    }
}