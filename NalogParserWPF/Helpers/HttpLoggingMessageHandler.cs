using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NalogParserWPF.Helpers
{
    public class HttpLoggingMessageHandler : DelegatingHandler
    {
        private readonly ILogger<HttpLoggingMessageHandler> _logger;

        public HttpLoggingMessageHandler(ILogger<HttpLoggingMessageHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            string method = request.Method?.ToString() ?? "UNKNOWN";
            string url = request.RequestUri?.ToString() ?? string.Empty;

            try
            {
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
                stopwatch.Stop();

                _logger.LogInformation(
                    "HTTP {Method} {Url} → {StatusCode} за {ElapsedMs} мс",
                    method, url, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "HTTP {Method} {Url} завершился исключением за {ElapsedMs} мс",
                    method, url, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
