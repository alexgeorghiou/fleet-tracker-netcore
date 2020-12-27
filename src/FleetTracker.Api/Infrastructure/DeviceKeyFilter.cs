using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace FleetTracker.Api.Infrastructure
{
    /// <summary>
    /// Shared-secret check for telematics devices. Disabled when <c>Ingestion:DeviceApiKey</c> is empty so the
    /// demo works out of the box; production would issue per-device credentials instead.
    /// </summary>
    public class DeviceKeyFilter : IActionFilter
    {
        public const string HeaderName = "X-Device-Key";
        private readonly string _expected;

        public DeviceKeyFilter(IConfiguration configuration)
        {
            _expected = configuration["Ingestion:DeviceApiKey"];
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (string.IsNullOrEmpty(_expected))
                return;

            var supplied = context.HttpContext.Request.Headers[HeaderName].ToString();
            if (!FixedTimeEquals(supplied, _expected))
                context.Result = new UnauthorizedObjectResult(new ProblemDetails
                {
                    Title = "Invalid device key",
                    Status = 401,
                    Detail = $"Supply a valid {HeaderName} header."
                });
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var x = Encoding.UTF8.GetBytes(a ?? string.Empty);
            var y = Encoding.UTF8.GetBytes(b);
            return x.Length == y.Length && CryptographicOperations.FixedTimeEquals(x, y);
        }
    }
}
