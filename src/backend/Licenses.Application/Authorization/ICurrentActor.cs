namespace Licenses.Application.Authorization;

public interface ICurrentActor
{
    Guid? UserId { get; }
}
