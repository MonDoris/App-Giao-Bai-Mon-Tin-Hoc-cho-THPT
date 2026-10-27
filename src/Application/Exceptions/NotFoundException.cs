namespace TinHoc12.Application.Exceptions;

/// <summary>Không tìm thấy bài học/bài tập/đáp án (hoặc mã không hợp lệ).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
