using Microsoft.AspNetCore.Http;

namespace AeonDocGen.Infrastructure.Clients;

internal sealed class DefaultHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
