using System;
using System.Net;

namespace AcManager.Workshop {
    public class WorkshopHttpException : Exception {
        public HttpStatusCode Code { get; }

        public WorkshopHttpException(HttpStatusCode code, string errorMesage) : base(errorMesage) {
            Code = code;
        }
    }
}