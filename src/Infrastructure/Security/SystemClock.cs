using TinHoc12.Application.Abstractions;

namespace TinHoc12.Infrastructure.Security;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
