using System.Text.Json.Serialization;
using System.Text.Json;
using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Rules;
using TinHoc12.Application.Security;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.UseCases.Student;

/// <summary>
/// Nhận kết quả nộp bài đã chấm ở trình duyệt (AC-135, 137, 124). Danh tính lấy từ phiên; server gắn thời điểm,
/// assignment hiệu lực và cờ nộp muộn. Kết quả chưa được xác minh phía server (QD-32).
/// </summary>
public sealed class SubmitSolutionUseCase
{
    public const int MaxCodeLength = 20_000;
    private static readonly string[] ResultStatuses = { "Passed", "Wrong", "Error", "Timeout" };
    private static readonly JsonSerializerOptions ResultsJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ILessonRepository _lessons;
    private readonly IAssignmentRepository _assignments;
    private readonly ISubmissionRepository _submissions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly SubmissionThrottle _throttle;
    private readonly AuthOptions _options;

    public SubmitSolutionUseCase(
        ILessonRepository lessons,
        IAssignmentRepository assignments,
        ISubmissionRepository submissions,
        IUnitOfWork unitOfWork,
        IClock clock,
        SubmissionThrottle throttle,
        AuthOptions options)
    {
        _lessons = lessons;
        _assignments = assignments;
        _submissions = submissions;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _throttle = throttle;
        _options = options;
    }

    public async Task<SubmitResultDto> ExecuteAsync(
        CurrentUser user, string? exerciseId, SubmitRequest request, CancellationToken cancellationToken = default)
    {
        user.RequireStudent();
        if (!IdRules.IsValid(exerciseId))
        {
            throw new NotFoundException("Không tìm thấy bài tập.");
        }

        var rows = await _assignments.GetVisibleForStudentAsync(user.Id, _clock.UtcNow, cancellationToken);
        var effective = AssignmentRules.PickEffective(rows).GetValueOrDefault(exerciseId!)
            ?? throw new NotFoundException("Không tìm thấy bài tập.");

        ValidateShape(request);
        var passed = request.PassedTests!.Value;
        var total = request.TotalTests!.Value;

        var counts = await _lessons.GetTestCountsAsync(new[] { effective.ExerciseId }, cancellationToken);
        if (!counts.TryGetValue(effective.ExerciseId, out var currentTotal) || currentTotal != total)
        {
            throw new ConflictException("EXERCISE_CHANGED", "Đề bài vừa được cập nhật, hãy tải lại trang.");
        }

        ValidateResults(request.Results, passed, total);

        var now = _clock.UtcNow;
        if (!_throttle.TryAcquire(user.Id, now, TimeSpan.FromSeconds(_options.SubmissionMinIntervalSeconds)))
        {
            throw new TooManyRequestsException("Bạn nộp quá nhanh, hãy thử lại sau vài giây.", _options.SubmissionMinIntervalSeconds);
        }

        var attempt = await _submissions.CountAttemptsAsync(user.Id, effective.ExerciseId, cancellationToken) + 1;
        var isPassed = passed == total;
        var isLate = effective.DueAtUtc is { } due && now > due;
        var submission = new Submission
        {
            AssignmentId = effective.AssignmentId,
            ExerciseId = effective.ExerciseId,
            StudentId = user.Id,
            Code = request.Code!,
            PassedTests = passed,
            TotalTests = total,
            IsPassed = isPassed,
            IsLate = isLate,
            ResultsJson = request.Results is null
                ? null
                : JsonSerializer.Serialize(
                    request.Results.Select(r => new TestResultInputDto(r.Status, r.TimeMs)), ResultsJsonOptions),
            SubmittedUtc = now,
        };
        _submissions.Add(submission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubmitResultDto(
            submission.Id, attempt, isPassed, isLate, now, isPassed ? AssignmentRules.Passed : AssignmentRules.InProgress);
    }

    private static void ValidateShape(SubmitRequest request)
    {
        var errors = new FieldErrors();
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            errors.Add("code", "Bạn chưa viết code.");
        }
        else if (request.Code.Length > MaxCodeLength)
        {
            errors.Add("code", $"Mã nguồn tối đa {MaxCodeLength:N0} ký tự.");
        }

        if (request.TotalTests is null or < 1)
        {
            errors.Add("totalTests", "Số test phải là số nguyên từ 1 trở lên.");
        }

        if (request.PassedTests is null or < 0)
        {
            errors.Add("passedTests", "Số test đạt phải là số nguyên không âm.");
        }
        else if (request.TotalTests is { } t && request.PassedTests > t)
        {
            errors.Add("passedTests", "Số test đạt không được lớn hơn tổng số test.");
        }

        errors.ThrowIfAny();
    }

    private static void ValidateResults(IReadOnlyList<TestResultInputDto>? results, int passed, int total)
    {
        if (results is null)
        {
            return;
        }

        var errors = new FieldErrors();
        if (results.Count != total)
        {
            errors.Add("results", "Danh sách kết quả từng test phải đủ số test.");
        }
        else
        {
            if (results.Any(r => r is null || !ResultStatuses.Contains(r.Status) || r.TimeMs is < 0))
            {
                errors.Add("results", "Trạng thái từng test không hợp lệ.");
            }
            else if (results.Count(r => r.Status == "Passed") != passed)
            {
                errors.Add("results", "Số test đạt không khớp với kết quả từng test.");
            }
        }

        errors.ThrowIfAny();
    }
}
