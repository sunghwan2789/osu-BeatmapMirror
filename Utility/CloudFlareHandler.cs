using CloudflareSolverRe;
using CloudflareSolverRe.Types;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Utility
{
    public class CloudFlareHandler : DelegatingHandler
    {
        public HttpClientHandler HttpClientHandler
        {
            get
            {
                var innerHandler = InnerHandler;
                while (innerHandler is DelegatingHandler otherInnerHandler)
                {
                    innerHandler = otherInnerHandler.InnerHandler;
                }
                return innerHandler as HttpClientHandler;
            }
        }

        public CloudFlareHandler() : this(new HttpClientHandler())
        {
        }

        public CloudFlareHandler(HttpMessageHandler innerHandler) : base(new ClearanceHandler(innerHandler, Settings.UserAgent))
        {
            HttpClientHandler.CookieContainer.Add(Settings.CFSession);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var oldSessionCookies = SessionCookies.FromCookieContainer(HttpClientHandler.CookieContainer, request.RequestUri);

            var response = await base.SendAsync(request, cancellationToken);

            var sessionCookies = SessionCookies.FromCookieContainer(HttpClientHandler.CookieContainer, request.RequestUri);

            if (sessionCookies.Valid && !oldSessionCookies.Equals(sessionCookies))
            {
                Settings.CFSession = sessionCookies.AsCookieCollection();
            }

            return response;
        }
    }
}