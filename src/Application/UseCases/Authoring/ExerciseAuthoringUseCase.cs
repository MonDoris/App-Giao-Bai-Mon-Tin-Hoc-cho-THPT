using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Mapping;
using TinHoc12.Application.Security;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.UseCases.Authoring;

/// <summary>Soạn bài tập và bộ test case của giáo viên (AC-115..121, 124).</summary>
public sealed class ExerciseAuthoringUseCase
{
    public const int MinTestCases = 3;
    public const int MaxTestCases = 50;
    private static readonly string[] Difficulties = { "Easy", "Medium", "Hard" };

    private readonly ILessonAuthoringRepository _repository;
    private readonly IAssignmentRepository _assignments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExerciseAuthoringUseCase(
        ILessonAuthoringRepository repository, IAssignmentRepository assignments, IUnitOfWork unitOfWork, IClock clock)
    {
        _repository = repository;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<TeacherExerciseDto> GetAsync(CurrentUser user, string? id, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        return await ToDtoAsync(await LoadAsync(id, cancellationToken), cancellationToken);
    }

    public async Task<TeacherExerciseDto> CreateAsync(
        CurrentUser user, string? lessonId, ExerciseInputDto input, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        if (!IdRules.IsValid(lessonId) || !await _repository.LessonIdExistsAsync(lessonId!, cancellationToken))
        {
            throw new NotFoundException("Không tìm thấy bài học.");
        }

        var errors = new FieldErrors();
        var data = Validate(input, errors);
        string? manualId = null;
        if (!string.IsNullOrWhiteSpace(input.Id))
        {
            manualId = input.Id.Trim();
            if (!IdRules.IsValid(manualId))
            {
                errors.Add("id", "Mã chỉ gồm chữ thường không dấu, số và dấu gạch ngang, tối đa 100 ký tự.");
            }
        }

        errors.ThrowIfAny();

        string id;
        if (manualId is not null)
        {
            if (await _repository.ExerciseIdExistsAsync(manualId, cancellationToken))
            {
                throw new ConflictException("DUPLICATE_ID", "Mã bài tập đã tồn tại (mã bài tập là duy nhất toàn hệ thống), hãy chọn mã khác.");
            }

            id = manualId;
        }
        else
        {
            // Mã bài tập có tiền tố là mã bài học (QD-36).
            id = await SlugGenerator.UniqueAsync(
                SlugGenerator.Slugify(lessonId + "-" + SlugGenerator.Slugify(data!.Title, fallback: "bai-tap")),
                async candidate => await _repository.ExerciseIdExistsAsync(candidate, cancellationToken));
        }

        var exercise = new Exercise
        {
            Id = id,
            LessonId = lessonId!,
            Order = await _repository.NextExerciseOrderAsync(lessonId!, cancellationToken),
            IsPublished = false,
            IsArchived = false,
            CreatedByUserId = user.Id,
            Version = 1,
            UpdatedUtc = _clock.UtcNow,
        };
        Apply(exercise, data!);
        _repository.AddExercise(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(user, id, cancellationToken);
    }

    public async Task<TeacherExerciseDto> UpdateAsync(
        CurrentUser user, string? id, ExerciseInputDto input, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var exercise = await LoadAsync(id, cancellationToken);
        var errors = new FieldErrors();
        var data = Validate(input, errors);
        if (input.Version is null)
        {
            errors.Add("version", "Thiếu phiên bản (version) của bài tập đang sửa.");
        }

        errors.ThrowIfAny();

        if (exercise.Version != input.Version)
        {
            throw new ConflictException("CONFLICT", "Bài tập đã được người khác chỉnh sửa, hãy tải lại.");
        }

        Apply(exercise, data!);
        Touch(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(user, exercise.Id, cancellationToken);
    }

    public async Task<TeacherExerciseDto> SetPublishedAsync(CurrentUser user, string? id, bool isPublished, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var exercise = await LoadAsync(id, cancellationToken);
        if (isPublished && exercise.TestCases.Count < MinTestCases)
        {
            throw new ValidationFailedException("testCases", $"Bài tập cần ít nhất {MinTestCases} test case mới xuất bản được.");
        }

        exercise.IsPublished = isPublished;
        Touch(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(user, exercise.Id, cancellationToken);
    }

    public async Task<TeacherExerciseDto> SetArchivedAsync(CurrentUser user, string? id, bool isArchived, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var exercise = await LoadAsync(id, cancellationToken);
        exercise.IsArchived = isArchived;
        Touch(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(user, exercise.Id, cancellationToken);
    }

    public async Task DeleteAsync(CurrentUser user, string? id, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var exercise = await LoadAsync(id, cancellationToken);
        var dependencies = await _repository.GetExerciseDependenciesAsync(exercise.Id, cancellationToken);
        if (dependencies.Any)
        {
            throw new ConflictException(
                "HAS_DEPENDENCIES",
                $"Bài tập đã được giao ({dependencies.Assignments} lượt giao) hoặc đã có {dependencies.Submissions} bài nộp. "
                + "Hãy lưu trữ bài tập thay vì xóa.");
        }

        _repository.RemoveExercise(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(CurrentUser user, string? lessonId, IReadOnlyList<string>? ids, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        if (!IdRules.IsValid(lessonId) || !await _repository.LessonIdExistsAsync(lessonId!, cancellationToken))
        {
            throw new NotFoundException("Không tìm thấy bài học.");
        }

        var exercises = await _repository.GetExercisesOfLessonAsync(lessonId!, cancellationToken);
        await ReorderHelper.ApplyAsync(
            ids, exercises, e => e.Id, (e, order) => e.Order = order, _unitOfWork, "bài tập", cancellationToken);
    }

    private async Task<Exercise> LoadAsync(string? id, CancellationToken cancellationToken)
    {
        if (!IdRules.IsValid(id))
        {
            throw new NotFoundException("Không tìm thấy bài tập.");
        }

        return await _repository.GetExerciseForEditAsync(id!, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài tập.");
    }

    private async Task<TeacherExerciseDto> ToDtoAsync(Exercise exercise, CancellationToken cancellationToken)
    {
        var deps = await _repository.GetExerciseDependenciesAsync(exercise.Id, cancellationToken);
        var affected = await _assignments.CountStudentsReachedAsync(new[] { exercise.Id }, cancellationToken);
        return AuthoringMapper.ToDto(exercise, deps, affected);
    }

    private void Touch(Exercise exercise)
    {
        exercise.Version++;
        exercise.UpdatedUtc = _clock.UtcNow;
    }

    private static void Apply(Exercise exercise, ExerciseData d)
    {
        exercise.Title = d.Title;
        exercise.Difficulty = d.Difficulty;
        exercise.Statement = d.Statement;
        exercise.InputFormat = d.InputFormat;
        exercise.OutputFormat = d.OutputFormat;
        exercise.Constraints = d.Constraints;
        exercise.ExamplesJson = AuthoringMapper.ToJson(d.Examples);
        exercise.StarterCode = d.StarterCode;
        exercise.SolutionCode = d.SolutionCode;
        exercise.SolutionExplanation = d.SolutionExplanation;
        exercise.ConfirmEveryTime = d.ConfirmEveryTime;
        exercise.TimeLimitMs = d.TimeLimitMs;
        ReplaceTestCases(exercise, d.TestCases);
    }

    /// <summary>
    /// Thay toàn bộ bộ test: cập nhật tại chỗ theo vị trí, thêm test mới ở cuối, bỏ test thừa ở cuối.
    /// Các Order hiện có được giữ nguyên (luôn tăng dần), nên không bao giờ vi phạm ràng buộc duy nhất (ExerciseId, Order).
    /// </summary>
    private static void ReplaceTestCases(Exercise exercise, IReadOnlyList<TestCaseDto> tests)
    {
        var existing = exercise.TestCases.OrderBy(t => t.Order).ToList();
        for (var i = 0; i < tests.Count; i++)
        {
            if (i < existing.Count)
            {
                existing[i].Input = tests[i].Input;
                existing[i].ExpectedOutput = tests[i].ExpectedOutput;
                existing[i].IsHidden = tests[i].IsHidden;
            }
            else
            {
                var order = (exercise.TestCases.Count == 0 ? 0 : exercise.TestCases.Max(t => t.Order)) + 1;
                exercise.TestCases.Add(new TestCase
                {
                    Order = order,
                    Input = tests[i].Input,
                    ExpectedOutput = tests[i].ExpectedOutput,
                    IsHidden = tests[i].IsHidden,
                });
            }
        }

        for (var i = tests.Count; i < existing.Count; i++)
        {
            exercise.TestCases.Remove(existing[i]);
        }
    }

    private sealed record ExerciseData(
        string Title,
        string Difficulty,
        string Statement,
        string InputFormat,
        string OutputFormat,
        string? Constraints,
        List<ExampleDto> Examples,
        string StarterCode,
        string? SolutionCode,
        string? SolutionExplanation,
        bool ConfirmEveryTime,
        int TimeLimitMs,
        List<TestCaseDto> TestCases);

    private static ExerciseData? Validate(ExerciseInputDto input, FieldErrors errors)
    {
        var title = errors.Text("title", "Tiêu đề", input.Title, 200);
        string? difficulty = null;
        var match = Difficulties.FirstOrDefault(d => string.Equals(d, input.Difficulty?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            errors.Add("difficulty", "Độ khó phải là Easy, Medium hoặc Hard.");
        }
        else
        {
            difficulty = match;
        }

        var statement = errors.Text("statement", "Đề bài", input.Statement, 20_000, trim: false, allowMultiline: true);
        var inputFormat = errors.Text("inputFormat", "Định dạng dữ liệu vào", input.InputFormat, 5_000, trim: false, allowMultiline: true);
        var outputFormat = errors.Text("outputFormat", "Định dạng dữ liệu ra", input.OutputFormat, 5_000, trim: false, allowMultiline: true);
        var constraints = errors.Text("constraints", "Ràng buộc", input.Constraints, 5_000, required: false, trim: false, allowMultiline: true);
        if (string.IsNullOrWhiteSpace(constraints))
        {
            constraints = null;
        }

        var examples = ValidateExamples(input.Examples, errors);

        string? starter = null;
        if (input.StarterCode is null)
        {
            errors.Add("starterCode", "Mã khởi tạo không được null (có thể để trống).");
        }
        else
        {
            starter = errors.Text("starterCode", "Mã khởi tạo", input.StarterCode, 20_000, required: false, trim: false, allowMultiline: true);
        }

        var solution = errors.Text("solutionCode", "Đáp án", input.SolutionCode, 20_000, required: false, trim: false, allowMultiline: true);
        if (string.IsNullOrWhiteSpace(solution))
        {
            solution = null;
        }

        var explanation = errors.Text("solutionExplanation", "Giải thích đáp án", input.SolutionExplanation, 20_000, required: false, trim: false, allowMultiline: true);
        if (string.IsNullOrWhiteSpace(explanation))
        {
            explanation = null;
        }

        var timeLimit = input.TimeLimitMs ?? 5000;
        if (timeLimit < 500 || timeLimit > 5000)
        {
            errors.Add("timeLimitMs", "Giới hạn thời gian phải từ 500 đến 5000 ms.");
        }

        var tests = ValidateTests(input.TestCases, errors);

        if (title is null || difficulty is null || statement is null || inputFormat is null || outputFormat is null
            || examples is null || starter is null || tests is null || errors.Any)
        {
            return null;
        }

        return new ExerciseData(
            title, difficulty, statement, inputFormat, outputFormat, constraints, examples, starter,
            solution, explanation, input.ConfirmEveryTime ?? false, timeLimit, tests);
    }

    private static List<ExampleDto>? ValidateExamples(IReadOnlyList<ExampleDto>? examples, FieldErrors errors)
    {
        if (examples is null || examples.Count < 1)
        {
            errors.Add("examples", "Cần ít nhất 1 ví dụ.");
            return null;
        }

        if (examples.Count > 10)
        {
            errors.Add("examples", "Tối đa 10 ví dụ.");
            return null;
        }

        var result = new List<ExampleDto>();
        var ok = true;
        for (var i = 0; i < examples.Count; i++)
        {
            var ex = examples[i];
            var input = errors.Text($"examples[{i}].input", $"Input của ví dụ {i + 1}", ex?.Input, 5_000, required: false, trim: false, allowMultiline: true);
            var output = errors.Text($"examples[{i}].output", $"Output của ví dụ {i + 1}", ex?.Output, 5_000, required: false, trim: false, allowMultiline: true);
            var expl = errors.Text($"examples[{i}].explanation", $"Giải thích của ví dụ {i + 1}", ex?.Explanation, 5_000, required: false, trim: false, allowMultiline: true);
            if (ex is null || ex.Input is null || ex.Output is null)
            {
                errors.Add($"examples[{i}]", $"Ví dụ {i + 1} cần có cả Input và Output.");
                ok = false;
            }
            else if (input is not null && output is not null)
            {
                result.Add(new ExampleDto(input, output, string.IsNullOrWhiteSpace(expl) ? null : expl));
            }
            else
            {
                ok = false;
            }
        }

        return ok ? result : null;
    }

    private static List<TestCaseDto>? ValidateTests(IReadOnlyList<TestCaseDto>? tests, FieldErrors errors)
    {
        if (tests is null || tests.Count < MinTestCases)
        {
            errors.Add("testCases", $"Cần ít nhất {MinTestCases} test case.");
            return null;
        }

        if (tests.Count > MaxTestCases)
        {
            errors.Add("testCases", $"Tối đa {MaxTestCases} test case.");
            return null;
        }

        var result = new List<TestCaseDto>();
        var ok = true;
        for (var i = 0; i < tests.Count; i++)
        {
            var t = tests[i];
            var input = errors.Text($"testCases[{i}].input", $"Input của test {i + 1}", t?.Input, 10_000, required: false, trim: false, allowMultiline: true);
            var expected = errors.Text($"testCases[{i}].expectedOutput", $"Đầu ra mong đợi của test {i + 1}", t?.ExpectedOutput, 10_000, required: false, trim: false, allowMultiline: true);
            if (t is null || t.Input is null || t.ExpectedOutput is null)
            {
                errors.Add($"testCases[{i}]", $"Test {i + 1} cần có cả Input và đầu ra mong đợi.");
                ok = false;
            }
            else if (input is not null && expected is not null)
            {
                result.Add(new TestCaseDto(input, expected, t.IsHidden));
            }
            else
            {
                ok = false;
            }
        }

        return ok ? result : null;
    }
}
