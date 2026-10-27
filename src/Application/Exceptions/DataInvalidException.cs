namespace TinHoc12.Application.Exceptions;

/// <summary>Dữ liệu của một bài học trong DB bị lỗi (thiếu trường, JSON hỏng, không có test case...).</summary>
public sealed class DataInvalidException : Exception
{
    public string Field { get; }

    public DataInvalidException(string field, string message) : base(message)
    {
        Field = field;
    }
}
