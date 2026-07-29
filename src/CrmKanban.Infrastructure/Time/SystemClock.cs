using CrmKanban.Application.Abstractions;

namespace CrmKanban.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
