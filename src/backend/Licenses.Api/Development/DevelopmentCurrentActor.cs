using Licenses.Application.Authorization;

namespace Licenses.Api.Development;

public sealed class DevelopmentCurrentActor(IHttpContextAccessor httpContextAccessor, IHostEnvironment environment) : ICurrentActor
{
    public Guid? UserId
    {
        get
        {
            if (!environment.IsDevelopment()) return null;
            var headerValue = httpContextAccessor.HttpContext?.Request.Headers["X-Dev-User-Id"].FirstOrDefault();
            return Guid.TryParse(headerValue, out var userId) ? userId : null;
        }
    }
}
