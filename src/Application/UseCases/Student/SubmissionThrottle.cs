using System.Collections.Concurrent;

namespace TinHoc12.Application.UseCases.Student;

/// <summary>
/// Giới hạn mỗi học sinh tối đa 1 lần nộp trong khoảng thời gian tối thiểu (AC-137), an toàn khi nhiều yêu cầu đến cùng lúc.
/// Trạng thái nằm trong bộ nhớ của tiến trình (đủ cho một máy chủ; nhiều máy chủ cần chia sẻ trạng thái).
/// </summary>
public sealed class SubmissionThrottle
{
    private readonly ConcurrentDictionary<int, DateTime> _lastAccepted = new();

    /// <returns>true nếu được phép nộp (và ghi nhận thời điểm); false nếu nộp quá nhanh.</returns>
    public bool TryAcquire(int studentId, DateTime nowUtc, TimeSpan minInterval)
    {
        while (true)
        {
            if (!_lastAccepted.TryGetValue(studentId, out var last))
            {
                if (_lastAccepted.TryAdd(studentId, nowUtc))
                {
                    Prune(nowUtc, minInterval);
                    return true;
                }

                continue;
            }

            if (nowUtc - last < minInterval)
            {
                return false;
            }

            if (_lastAccepted.TryUpdate(studentId, nowUtc, last))
            {
                return true;
            }
        }
    }

    private void Prune(DateTime nowUtc, TimeSpan minInterval)
    {
        if (_lastAccepted.Count < 1000)
        {
            return;
        }

        foreach (var pair in _lastAccepted)
        {
            if (nowUtc - pair.Value > minInterval)
            {
                _lastAccepted.TryRemove(pair);
            }
        }
    }
}
