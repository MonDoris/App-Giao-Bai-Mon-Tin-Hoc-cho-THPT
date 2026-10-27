using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Rules;
using TinHoc12.Application.Security;
using TinHoc12.Application.UseCases.Assignments;
using TinHoc12.Application.UseCases.Authoring;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Tests;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Tổng hai số", "tong-hai-so")]
    [InlineData("Đường đi ngắn nhất", "duong-di-ngan-nhat")]
    [InlineData("  Biến, kiểu dữ liệu & nhập/xuất!  ", "bien-kieu-du-lieu-nhap-xuat")]
    [InlineData("Bài 1: if--else", "bai-1-if-else")]
    [InlineData("!!!", "muc")]
    public void Slugify_removes_diacritics_and_symbols(string title, string expected)
    {
        Assert.Equal(expected, SlugGenerator.Slugify(title));
    }

    [Fact]
    public void Slugify_cuts_to_100_chars_without_trailing_dash()
    {
        var slug = SlugGenerator.Slugify(string.Join(" ", Enumerable.Repeat("chuoi dai", 30)));
        Assert.True(slug.Length <= 100);
        Assert.False(slug.EndsWith('-'));
        Assert.True(IdRules.IsValid(slug));
    }

    [Fact]
    public async Task Unique_adds_numeric_suffix_and_keeps_length_limit()
    {
        var taken = new HashSet<string> { "tong", "tong-2" };
        Assert.Equal("tong-3", await SlugGenerator.UniqueAsync("tong", s => Task.FromResult(taken.Contains(s))));

        var longBase = new string('a', 100);
        var taken2 = new HashSet<string> { longBase };
        var result = await SlugGenerator.UniqueAsync(longBase, s => Task.FromResult(taken2.Contains(s)));
        Assert.Equal(100, result.Length);
        Assert.EndsWith("-2", result);
    }
}

public class LessonAuthoringTests
{
    private static (World W, User Gv, LessonAuthoringUseCase UseCase) Create()
    {
        var w = new World();
        var gv = w.AddUser("gv", Roles.Teacher);
        return (w, gv, new LessonAuthoringUseCase(w.LessonRepo, w.AssignmentRepo, w.UnitOfWork, w.Clock));
    }

    private static LessonInputDto Input(string title = "Bài học đầu tiên", string? id = null, int? version = null) => new(
        id, title, "Mô tả ngắn", new[] { "Tóm tắt 1" }, new[] { "Lưu ý 1" }, new[] { "Tổng kết 1" }, version);

    [Fact]
    public async Task Create_generates_slug_order_draft_state_and_version()
    {
        var (w, gv, useCase) = Create();
        w.AddLesson("bai-01", 1);

        var lesson = await useCase.CreateAsync(World.AsTeacher(gv), Input());

        Assert.Equal("bai-hoc-dau-tien", lesson.Id);
        Assert.Equal(2, lesson.Order);
        Assert.False(lesson.IsPublished);
        Assert.Equal(1, lesson.Version);
        Assert.Equal(gv.Id, w.Lessons.Last().CreatedByUserId);
    }

    [Fact]
    public async Task Create_with_duplicate_title_adds_suffix_and_manual_duplicate_id_conflicts()
    {
        var (_, gv, useCase) = Create();
        var a = await useCase.CreateAsync(World.AsTeacher(gv), Input());
        var b = await useCase.CreateAsync(World.AsTeacher(gv), Input());
        Assert.Equal("bai-hoc-dau-tien", a.Id);
        Assert.Equal("bai-hoc-dau-tien-2", b.Id);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.CreateAsync(World.AsTeacher(gv), Input(id: "bai-hoc-dau-tien")));
        Assert.Equal("DUPLICATE_ID", ex.Code);
        var manual = await useCase.CreateAsync(World.AsTeacher(gv), Input("Khác", id: "ma-tu-chon"));
        Assert.Equal("ma-tu-chon", manual.Id);
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(World.AsTeacher(gv), Input(id: "Sai Ma")));
    }

    [Fact]
    public async Task Validation_reports_each_field_in_vietnamese()
    {
        var (_, gv, useCase) = Create();
        var input = new LessonInputDto(null, "   ", new string('m', 501), Array.Empty<string>(), new[] { "ok", " " }, new[] { new string('x', 5001) }, null);

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(World.AsTeacher(gv), input));

        var fields = ex.Errors.Select(e => e.Field).ToArray();
        Assert.Contains("title", fields);
        Assert.Contains("description", fields);
        Assert.Contains("summary", fields);
        Assert.Contains("notes[1]", fields);
        Assert.Contains("conclusion[0]", fields);
        Assert.All(ex.Errors, e => Assert.False(string.IsNullOrWhiteSpace(e.Message)));
    }

    [Theory]
    [InlineData("a\u0001b")]
    [InlineData("tab\u0000")]
    public async Task Control_characters_are_rejected_but_newlines_in_blocks_are_fine(string bad)
    {
        var (_, gv, useCase) = Create();
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(World.AsTeacher(gv), Input(title: bad)));
        var ok = new LessonInputDto(null, "Hợp lệ", "m", new[] { "dòng 1\ndòng 2" }, new[] { "```python\nprint(1)\n```" }, new[] { "x" }, null);
        await useCase.CreateAsync(World.AsTeacher(gv), ok);
    }

    [Fact]
    public async Task Html_content_is_stored_verbatim()
    {
        var (w, gv, useCase) = Create();
        var xss = "<script>alert(1)</script><img src=x onerror=alert(1)>";
        var input = new LessonInputDto(null, xss, xss, new[] { xss }, new[] { xss }, new[] { xss }, null);
        var lesson = await useCase.CreateAsync(World.AsTeacher(gv), input);
        Assert.Equal(xss, lesson.Title);
        Assert.Equal(xss, lesson.Notes[0]);
        Assert.Contains(xss, w.Lessons.Last().NotesJson.Replace("\\u003C", "<").Replace("\\u003E", ">").Replace("\\u0027", "'"));
    }

    [Fact]
    public async Task Update_requires_matching_version_and_bumps_it()
    {
        var (_, gv, useCase) = Create();
        var created = await useCase.CreateAsync(World.AsTeacher(gv), Input());

        var updated = await useCase.UpdateAsync(World.AsTeacher(gv), created.Id, Input("Tiêu đề mới", version: created.Version));
        Assert.Equal(2, updated.Version);
        Assert.Equal("Tiêu đề mới", updated.Title);
        Assert.Equal(created.Id, updated.Id);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.UpdateAsync(World.AsTeacher(gv), created.Id, Input("Cũ", version: created.Version)));
        Assert.Equal("CONFLICT", ex.Code);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.UpdateAsync(World.AsTeacher(gv), "khong-co", Input(version: 1)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.UpdateAsync(World.AsTeacher(gv), created.Id, Input(version: null)));
    }

    [Fact]
    public async Task Publish_and_archive_toggle_flags()
    {
        var (_, gv, useCase) = Create();
        var created = await useCase.CreateAsync(World.AsTeacher(gv), Input());
        Assert.True((await useCase.SetPublishedAsync(World.AsTeacher(gv), created.Id, true)).IsPublished);
        var archived = await useCase.SetArchivedAsync(World.AsTeacher(gv), created.Id, true);
        Assert.True(archived.IsArchived);
        Assert.True(archived.IsPublished);
        Assert.Empty((await useCase.ListAsync(World.AsTeacher(gv), false)).Lessons);
        Assert.Single((await useCase.ListAsync(World.AsTeacher(gv), true)).Lessons);
        var restored = await useCase.SetArchivedAsync(World.AsTeacher(gv), created.Id, false);
        Assert.False(restored.IsArchived);
    }

    [Fact]
    public async Task Delete_works_without_dependencies_and_is_blocked_by_assignments_or_submissions()
    {
        var (w, gv, useCase) = Create();
        w.AddLesson("bai-01", 1);
        w.AddLesson("bai-02", 2);
        w.AddLesson("bai-03", 3);
        w.Assign("bai-02-bt1", all: true);
        w.Submissions.Add(new Submission { Id = 1, ExerciseId = "bai-03-bt1", StudentId = 5, AssignmentId = 99, PassedTests = 1, TotalTests = 2 });

        await useCase.DeleteAsync(World.AsTeacher(gv), "bai-01");
        Assert.DoesNotContain(w.Lessons, l => l.Id == "bai-01");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.DeleteAsync(World.AsTeacher(gv), "bai-02"));
        Assert.Equal("HAS_DEPENDENCIES", ex.Code);
        Assert.Contains("lưu trữ", ex.Message);
        await Assert.ThrowsAsync<ConflictException>(() => useCase.DeleteAsync(World.AsTeacher(gv), "bai-03"));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.DeleteAsync(World.AsTeacher(gv), "khong-co"));
    }

    [Fact]
    public async Task Reorder_requires_a_full_permutation_and_renumbers_from_one()
    {
        var (w, gv, useCase) = Create();
        w.AddLesson("a", 1);
        w.AddLesson("b", 2);
        w.AddLesson("c", 3);

        await useCase.ReorderAsync(World.AsTeacher(gv), new[] { "c", "a", "b" });
        Assert.Equal(new[] { "c", "a", "b" }, w.Lessons.OrderBy(l => l.Order).Select(l => l.Id));
        Assert.Equal(new[] { 1, 2, 3 }, w.Lessons.OrderBy(l => l.Order).Select(l => l.Order));

        foreach (var bad in new[] { new[] { "a", "b" }, new[] { "a", "b", "b" }, new[] { "a", "b", "z" }, new[] { "a", "b", "c", "a" } })
        {
            await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ReorderAsync(World.AsTeacher(gv), bad));
        }

        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ReorderAsync(World.AsTeacher(gv), null));
    }

    [Fact]
    public async Task Students_cannot_use_authoring()
    {
        var (w, _, useCase) = Create();
        var hs = w.AddUser("hs01");
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.ListAsync(World.AsStudent(hs), false));
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.CreateAsync(World.AsStudent(hs), Input()));
    }
}

public class ExerciseAuthoringTests
{
    private static (World W, User Gv, ExerciseAuthoringUseCase UseCase) Create()
    {
        var w = new World();
        var gv = w.AddUser("gv", Roles.Teacher);
        w.AddLesson("bai-01", 1, exercises: 1);
        return (w, gv, new ExerciseAuthoringUseCase(w.LessonRepo, w.AssignmentRepo, w.UnitOfWork, w.Clock));
    }

    private static ExerciseInputDto Input(string title = "Tính tổng", int tests = 3, int? version = null, string? id = null) => new(
        id,
        title,
        "Easy",
        "Đề bài",
        "Vào",
        "Ra",
        null,
        new[] { new ExampleDto("1", "1", "giải thích") },
        "# viết code",
        "print(1)",
        "vì sao",
        false,
        5000,
        Enumerable.Range(1, tests).Select(i => new TestCaseDto(i.ToString(), i.ToString(), i == tests)).ToList(),
        version);

    [Fact]
    public async Task Create_prefixes_id_with_lesson_and_defaults_to_draft()
    {
        var (_, gv, useCase) = Create();
        var ex = await useCase.CreateAsync(World.AsTeacher(gv), "bai-01", Input());
        Assert.Equal("bai-01-tinh-tong", ex.Id);
        Assert.Equal(2, ex.Order);
        Assert.False(ex.IsPublished);
        Assert.Equal(3, ex.TestCases.Count);
        Assert.True(ex.TestCases[2].IsHidden);

        var dup = await useCase.CreateAsync(World.AsTeacher(gv), "bai-01", Input());
        Assert.Equal("bai-01-tinh-tong-2", dup.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.CreateAsync(World.AsTeacher(gv), "khong-co", Input()));
        var conflict = await Assert.ThrowsAsync<ConflictException>(() => useCase.CreateAsync(World.AsTeacher(gv), "bai-01", Input(id: "bai-01-bt1")));
        Assert.Equal("DUPLICATE_ID", conflict.Code);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(51)]
    [InlineData(0)]
    public async Task Test_case_count_must_be_between_3_and_50(int count)
    {
        var (_, gv, useCase) = Create();
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(World.AsTeacher(gv), "bai-01", Input(tests: count)));
        Assert.Contains(ex.Errors, e => e.Field == "testCases");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(50)]
    public async Task Boundary_test_case_counts_are_accepted(int count)
    {
        var (_, gv, useCase) = Create();
        Assert.Equal(count, (await useCase.CreateAsync(World.AsTeacher(gv), "bai-01", Input(tests: count))).TestCases.Count);
    }

    [Fact]
    public async Task Field_limits_difficulty_and_time_limit_are_validated()
    {
        var (_, gv, useCase) = Create();
        var bad = Input() with
        {
            Title = new string('t', 201),
            Difficulty = "Impossible",
            Statement = "  ",
            InputFormat = new string('i', 5001),
            Examples = Array.Empty<ExampleDto>(),
            StarterCode = null,
            TimeLimitMs = 100,
            TestCases = new[] { new TestCaseDto(new string('x', 10_001), "y", false), new TestCaseDto("1", "1", false), new TestCaseDto("1", "1", false) },
        };

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(World.AsTeacher(gv), "bai-01", bad));

        var fields = ex.Errors.Select(e => e.Field).ToArray();
        foreach (var expected in new[] { "title", "difficulty", "statement", "inputFormat", "examples", "starterCode", "timeLimitMs", "testCases[0].input" })
        {
            Assert.Contains(expected, fields);
        }
    }

    [Fact]
    public async Task Empty_starter_code_and_missing_solution_are_allowed_and_solution_is_optional()
    {
        var (_, gv, useCase) = Create();
        var ok = Input() with { StarterCode = string.Empty, SolutionCode = null, SolutionExplanation = null };
        var ex = await useCase.CreateAsync(World.AsTeacher(gv), "bai-01", ok);
        Assert.Equal(string.Empty, ex.StarterCode);
        Assert.Null(ex.SolutionCode);
    }

    [Fact]
    public async Task Update_replaces_test_cases_in_place_and_conflicts_on_stale_version()
    {
        var (w, gv, useCase) = Create();
        var created = await useCase.CreateAsync(World.AsTeacher(gv), "bai-01", Input(tests: 5));
        var exercise = w.Exercises.First(e => e.Id == created.Id);
        var originalOrders = exercise.TestCases.Select(t => t.Order).OrderBy(x => x).ToArray();

        var updated = await useCase.UpdateAsync(World.AsTeacher(gv), created.Id, Input("Đổi tên", tests: 3, version: created.Version));

        Assert.Equal(created.Version + 1, updated.Version);
        Assert.Equal(3, updated.TestCases.Count);
        Assert.Equal(originalOrders.Take(3), exercise.TestCases.Select(t => t.Order).OrderBy(x => x));
        await Assert.ThrowsAsync<ConflictException>(() => useCase.UpdateAsync(World.AsTeacher(gv), created.Id, Input(version: created.Version)));
    }

    [Fact]
    public async Task Publish_needs_three_tests_delete_is_blocked_when_assigned_and_reorder_is_a_permutation()
    {
        var (w, gv, useCase) = Create();
        var a = await useCase.CreateAsync(World.AsTeacher(gv), "bai-01", Input("A"));
        var b = await useCase.CreateAsync(World.AsTeacher(gv), "bai-01", Input("B"));
        Assert.True((await useCase.SetPublishedAsync(World.AsTeacher(gv), a.Id, true)).IsPublished);

        w.Assign(a.Id, all: true);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.DeleteAsync(World.AsTeacher(gv), a.Id));
        Assert.Equal("HAS_DEPENDENCIES", ex.Code);
        await useCase.DeleteAsync(World.AsTeacher(gv), b.Id);
        Assert.DoesNotContain(w.Exercises, e => e.Id == b.Id);

        await useCase.ReorderAsync(World.AsTeacher(gv), "bai-01", new[] { a.Id, "bai-01-bt1" });
        Assert.Equal(new[] { a.Id, "bai-01-bt1" }, w.Lessons[0].Exercises.OrderBy(e => e.Order).Select(e => e.Id));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ReorderAsync(World.AsTeacher(gv), "bai-01", new[] { a.Id }));
    }
}

public class AssignmentUseCaseTests
{
    private static (World W, User Gv, AssignmentUseCase UseCase) Create()
    {
        var w = new World();
        var gv = w.AddUser("gv", Roles.Teacher);
        w.AddLesson("bai-01", 1, exercises: 2);
        return (w, gv, new AssignmentUseCase(w.LessonRepo, w.AssignmentRepo, w.ClassRepo, w.UnitOfWork, w.Clock));
    }

    // Hạn nộp bắt buộc: không truyền due thì dùng một hạn rất xa để các test khác chỉ kiểm tra điều chúng quan tâm.
    private static readonly DateTime FarDue = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AssignInputDto Input(string[] exercises, bool all = true, int[]? classes = null, DateTime? due = null, string? note = null, DateTime? start = null) =>
        new(exercises, new AssignmentTargetDto(all, classes), due ?? FarDue, note, start);

    [Fact]
    public async Task Assign_creates_one_assignment_per_exercise_for_all_students()
    {
        var (w, gv, useCase) = Create();
        w.AddUser("hs01");

        var result = await useCase.AssignAsync(World.AsTeacher(gv), Input(new[] { "bai-01-bt1", "bai-01-bt2" }, due: w.Clock.UtcNow.AddDays(3), note: "Làm kỹ"));

        Assert.Equal(2, result.Assignments.Count);
        Assert.Equal(1, result.StudentCount);
        Assert.All(w.Assignments, a => Assert.Equal("Làm kỹ", a.Note));
    }

    [Fact]
    public async Task Reassigning_the_same_target_updates_instead_of_duplicating()
    {
        var (w, gv, useCase) = Create();
        await useCase.AssignAsync(World.AsTeacher(gv), Input(new[] { "bai-01-bt1" }, note: "a"));
        await useCase.AssignAsync(World.AsTeacher(gv), Input(new[] { "bai-01-bt1" }, due: w.Clock.UtcNow.AddDays(1), note: "b"));

        var only = Assert.Single(w.Assignments);
        Assert.Equal("b", only.Note);
        Assert.NotNull(only.DueAtUtc);

        var c = w.AddClass("12A1");
        await useCase.AssignAsync(World.AsTeacher(gv), Input(new[] { "bai-01-bt1" }, all: false, classes: new[] { c.Id }));
        Assert.Equal(2, w.Assignments.Count);
    }

    [Fact]
    public async Task Validation_target_due_date_classes_and_publish_state()
    {
        var (w, gv, useCase) = Create();
        var user = World.AsTeacher(gv);
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.AssignAsync(user, Input(new[] { "bai-01-bt1" }, all: false, classes: Array.Empty<int>())));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.AssignAsync(user, Input(Array.Empty<string>())));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.AssignAsync(user, Input(new[] { "bai-01-bt1" }, due: w.Clock.UtcNow.AddMinutes(-1))));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.AssignAsync(user, Input(new[] { "bai-01-bt1" }, all: false, classes: new[] { 999 })));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.AssignAsync(user, Input(new[] { "bai-01-bt1" }, note: new string('n', 501))));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.AssignAsync(user, Input(new[] { "khong-co" })));

        // Hạn nộp bắt buộc và phải sau ngày bắt đầu
        var noDue = await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.AssignAsync(user, new AssignInputDto(new[] { "bai-01-bt1" }, new AssignmentTargetDto(true, null), null, null)));
        Assert.Contains(noDue.Errors, e => e.Field == "dueAt");
        var start = w.Clock.UtcNow.AddDays(2);
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.AssignAsync(user, Input(new[] { "bai-01-bt1" }, due: start.AddHours(-1), start: start)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.AssignAsync(user, Input(new[] { "bai-01-bt1" }, due: start, start: start)));

        w.Lessons[0].Exercises.First().IsPublished = false;
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.AssignAsync(user, Input(new[] { "bai-01-bt1" })));
        Assert.Equal("EXERCISE_NOT_PUBLISHED", ex.Code);
        Assert.Empty(w.Assignments);
    }

    [Fact]
    public async Task Delete_removes_when_no_submissions_and_cancels_softly_otherwise()
    {
        var (w, gv, useCase) = Create();
        var a = w.Assign("bai-01-bt1", all: true);
        var b = w.Assign("bai-01-bt2", all: true);
        w.Submissions.Add(new Submission { Id = 1, AssignmentId = b.Id, ExerciseId = "bai-01-bt2", StudentId = 5, PassedTests = 1, TotalTests = 2 });

        await useCase.DeleteAsync(World.AsTeacher(gv), a.Id);
        await useCase.DeleteAsync(World.AsTeacher(gv), b.Id);

        Assert.DoesNotContain(w.Assignments, x => x.Id == a.Id);
        Assert.NotNull(w.Assignments.Single(x => x.Id == b.Id).CancelledUtc);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.DeleteAsync(World.AsTeacher(gv), 999));
    }

    [Fact]
    public async Task Update_changes_due_note_and_target_and_allows_past_due()
    {
        var (w, gv, useCase) = Create();
        var c1 = w.AddClass("12A1");
        var c2 = w.AddClass("12A2");
        var a = w.Assign("bai-01-bt1", false, null, c1);

        var updated = await useCase.UpdateAsync(World.AsTeacher(gv), a.Id,
            new UpdateAssignmentInputDto(new AssignmentTargetDto(false, new[] { c2.Id }), w.Clock.UtcNow.AddDays(-1), "mới"));

        Assert.Equal(new[] { c2.Id }, updated.ClassIds);
        Assert.Equal("mới", updated.Note);
        Assert.NotNull(updated.DueAt);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.UpdateAsync(World.AsTeacher(gv), 999, new UpdateAssignmentInputDto(new AssignmentTargetDto(true, null), null, null)));

        // Khi sửa: hạn nộp vẫn bắt buộc và phải sau ngày bắt đầu
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.UpdateAsync(World.AsTeacher(gv), a.Id, new UpdateAssignmentInputDto(new AssignmentTargetDto(true, null), null, null)));
        var start = w.Clock.UtcNow.AddDays(3);
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.UpdateAsync(World.AsTeacher(gv), a.Id, new UpdateAssignmentInputDto(new AssignmentTargetDto(true, null), start.AddDays(-1), null, start)));
        var moved = await useCase.UpdateAsync(World.AsTeacher(gv), a.Id, new UpdateAssignmentInputDto(new AssignmentTargetDto(true, null), start.AddDays(2), null, start));
        Assert.Equal(start, moved.StartAt);
    }

    [Fact]
    public async Task Start_defaults_to_now_and_students_only_see_the_exercise_from_the_start_date()
    {
        var (w, gv, useCase) = Create();
        var hs = w.AddUser("hs01");

        var now = await useCase.AssignAsync(World.AsTeacher(gv), Input(new[] { "bai-01-bt1" }, due: w.Clock.UtcNow.AddDays(7)));
        Assert.Equal(w.Clock.UtcNow, now.Assignments.Single().StartAt);

        var start = w.Clock.UtcNow.AddDays(1);
        var later = await useCase.AssignAsync(World.AsTeacher(gv), Input(new[] { "bai-01-bt2" }, due: start.AddDays(7), start: start));
        Assert.Equal(start, later.Assignments.Single().StartAt);

        async Task<int> Visible() => (await UseCases.Lessons(w).ExecuteAsync(World.AsStudent(hs))).Lessons.Single(l => l.Id == "bai-01").ExerciseCount;
        Assert.Equal(1, await Visible());

        // Giao lại bài đã mở với ngày bắt đầu muộn hơn: vẫn mở (không ẩn bài học sinh đang làm)
        await useCase.AssignAsync(World.AsTeacher(gv), Input(new[] { "bai-01-bt1" }, due: w.Clock.UtcNow.AddDays(9), start: w.Clock.UtcNow.AddDays(5)));
        Assert.Equal(1, await Visible());

        w.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(2, await Visible());
    }

    [Fact]
    public async Task Students_cannot_assign()
    {
        var (w, _, useCase) = Create();
        var hs = w.AddUser("hs01");
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.AssignAsync(World.AsStudent(hs), Input(new[] { "bai-01-bt1" })));
    }
}
