using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Extensions.Http;

namespace NalogParserWPF.Helpers
{
    public class HttpRetryDelegatingHandler : DelegatingHandler
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public HttpRetryDelegatingHandler(int retryCount)
        {
            if (retryCount <= 0)
                retryCount = 3;

            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(retryCount,
                    attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _retryPolicy.ExecuteAsync(ct => base.SendAsync(request, ct), cancellationToken);
        }
    }
}
