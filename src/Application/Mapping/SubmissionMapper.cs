using System.Text.Json;
using TinHoc12.Application.Dtos;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Mapping;

/// <summary>Chuyển bài nộp sang DTO chi tiết (có mã nguồn), dùng chung cho học sinh (của mình) và giáo viên.</summary>
internal static class SubmissionMapper
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static SubmissionDetailDto ToDetail(Submission s, int attemptNo)
    {
        IReadOnlyList<TestResultInputDto>? results = null;
        if (!string.IsNullOrWhiteSpace(s.ResultsJson))
        {
            try
            {
                results = JsonSerializer.Deserialize<List<TestResultInputDto>>(s.ResultsJson, Options);
            }
            catch (JsonException)
            {
                results = null;
            }
        }

        return new SubmissionDetailDto(s.Id, attemptNo, s.Code, s.PassedTests, s.TotalTests, s.IsPassed, s.IsLate, s.SubmittedUtc, results);
    }
}
