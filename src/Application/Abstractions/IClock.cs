namespace TinHoc12.Application.Abstractions;

/// <summary>Nguồn thời gian (UTC), tách riêng để kiểm thử không phải chờ thật.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
