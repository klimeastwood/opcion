using System;
using System.Net;

namespace NalogParserWPF.Services
{
    public class NalogApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string? ResponseBody { get; }

        public NalogApiException(HttpStatusCode statusCode, string message, string? responseBody = null, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
