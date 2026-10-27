using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Security;
using TinHoc12.Application.UseCases.Progress;
using TinHoc12.Application.UseCases.Students;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Tests;

public class StudentManagementTests
{
    private static (World W, User Gv, StudentManagementUseCase UseCase) Create()
    {
        var w = new World();
        var gv = w.AddUser("gv", Roles.Teacher);
        return (w, gv, new StudentManagementUseCase(w.UserRepo, w.ClassRepo, w.Hasher, w.UnitOfWork, w.Clock));
    }

    [Fact]
    public async Task Create_generates_temporary_password_forces_change_and_always_makes_a_student()
    {
        var (w, gv, useCase) = Create();
        var c = w.AddClass("12A1");

        var result = await useCase.CreateAsync(World.AsTeacher(gv), new CreateStudentRequest(" HS01 ", "Nguyễn Văn A", new[] { c.Id }, null));

        Assert.Equal("hs01", result.Username);
        Assert.NotNull(result.TemporaryPassword);
        Assert.Equal(10, result.TemporaryPassword!.Length);
        var user = w.Users.Last();
        Assert.Equal(Roles.Student, user.Role);
        Assert.True(user.MustChangePassword);
        Assert.True(user.IsActive);
        Assert.Equal("h:" + result.TemporaryPassword, user.PasswordHash);
        Assert.Single(c.Members);
    }

    [Fact]
    public async Task Create_with_supplied_password_checks_policy_and_does_not_echo_it()
    {
        var (_, gv, useCase) = Create();
        var ok = await useCase.CreateAsync(World.AsTeacher(gv), new CreateStudentRequest("hs02", "B", null, "Matkhau2026"));
        Assert.Null(ok.TemporaryPassword);
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(World.AsTeacher(gv), new CreateStudentRequest("hs03", "C", null, "yeu")));
    }

    [Fact]
    public async Task Create_validates_username_display_name_classes_and_duplicates_case_insensitively()
    {
        var (w, gv, useCase) = Create();
        w.AddUser("hs01");
        var user = World.AsTeacher(gv);

        var dup = await Assert.ThrowsAsync<ConflictException>(() => useCase.CreateAsync(user, new CreateStudentRequest("HS01", "A", null, null)));
        Assert.Equal("DUPLICATE_USERNAME", dup.Code);
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(user, new CreateStudentRequest("a b", "A", null, null)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(user, new CreateStudentRequest("hs09", "", null, null)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(user, new CreateStudentRequest("hs09", new string('n', 101), null, null)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(user, new CreateStudentRequest("hs09", "A", new[] { 777 }, null)));
    }

    [Fact]
    public async Task Import_processes_rows_independently_and_enforces_limits()
    {
        var (w, gv, useCase) = Create();
        w.AddUser("co-san");
        var rows = new[]
        {
            new ImportRowDto("moi1", "Học sinh 1"),
            new ImportRowDto("co-san", "Trùng DB"),
            new ImportRowDto("moi1", "Trùng trong danh sách"),
            new ImportRowDto("sai ten", "X"),
            new ImportRowDto("moi2", ""),
            new ImportRowDto("moi3", "Học sinh 3"),
        };

        var result = await useCase.ImportAsync(World.AsTeacher(gv), new ImportRequest(null, rows));

        Assert.Equal(new[] { "created", "skipped", "skipped", "invalid", "invalid", "created" }, result.Results.Select(r => r.Status));
        Assert.All(result.Results.Where(r => r.Status == "created"), r => Assert.NotNull(r.TemporaryPassword));
        Assert.All(result.Results.Where(r => r.Status != "created"), r => Assert.Null(r.TemporaryPassword));
        Assert.Equal(1 + 2 + 1, w.Users.Count);

        var tooMany = Enumerable.Range(0, 201).Select(i => new ImportRowDto("u" + i, "U")).ToList();
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ImportAsync(World.AsTeacher(gv), new ImportRequest(null, tooMany)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ImportAsync(World.AsTeacher(gv), new ImportRequest(null, Array.Empty<ImportRowDto>())));
        var exactly200 = Enumerable.Range(0, 200).Select(i => new ImportRowDto("hs" + (1000 + i), "U")).ToList();
        Assert.Equal(200, (await useCase.ImportAsync(World.AsTeacher(gv), new ImportRequest(null, exactly200))).Results.Count(r => r.Status == "created"));
    }

    [Fact]
    public async Task Import_into_a_class_assigns_members()
    {
        var (w, gv, useCase) = Create();
        var c = w.AddClass("12A1");
        await useCase.ImportAsync(World.AsTeacher(gv), new ImportRequest(c.Id, new[] { new ImportRowDto("an01", "A"), new ImportRowDto("an02", "B") }));
        Assert.Equal(2, c.Members.Count);
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ImportAsync(World.AsTeacher(gv), new ImportRequest(999, new[] { new ImportRowDto("an03", "C") })));
    }

    [Fact]
    public async Task Reset_password_sets_flag_rotates_stamp_clears_lockout_and_rejects_teachers_and_unknown_ids()
    {
        var (w, gv, useCase) = Create();
        var hs = w.AddUser("hs01");
        hs.FailedLoginCount = 3;
        hs.LockoutEndUtc = w.Clock.UtcNow.AddMinutes(5);
        var stamp = hs.SecurityStamp;
        var other = w.AddUser("gv2", Roles.Teacher);

        var result = await useCase.ResetPasswordAsync(World.AsTeacher(gv), hs.Id);

        Assert.Equal(10, result.TemporaryPassword.Length);
        Assert.True(hs.MustChangePassword);
        Assert.NotEqual(stamp, hs.SecurityStamp);
        Assert.Null(hs.LockoutEndUtc);
        Assert.Equal(0, hs.FailedLoginCount);
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.ResetPasswordAsync(World.AsTeacher(gv), other.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ResetPasswordAsync(World.AsTeacher(gv), 999));
    }

    [Fact]
    public async Task Update_disables_account_and_revokes_sessions_changes_classes_and_never_touches_teachers()
    {
        var (w, gv, useCase) = Create();
        var hs = w.AddUser("hs01");
        var c = w.AddClass("12A1");
        var stamp = hs.SecurityStamp;

        var dto = await useCase.UpdateAsync(World.AsTeacher(gv), hs.Id, new UpdateStudentRequest(false, "Tên mới", new[] { c.Id }));

        Assert.False(dto.IsActive);
        Assert.Equal("Tên mới", dto.DisplayName);
        Assert.Equal(new[] { c.Id }, dto.ClassIds);
        Assert.NotEqual(stamp, hs.SecurityStamp);
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.UpdateAsync(World.AsTeacher(gv), gv.Id, new UpdateStudentRequest(false, null, null)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.UpdateAsync(World.AsTeacher(gv), hs.Id, new UpdateStudentRequest(null, "", null)));
    }

    [Fact]
    public async Task Unlock_clears_temporary_lockout()
    {
        var (w, gv, useCase) = Create();
        var hs = w.AddUser("hs01");
        hs.LockoutEndUtc = w.Clock.UtcNow.AddMinutes(10);
        hs.FailedLoginCount = 4;
        await useCase.UnlockAsync(World.AsTeacher(gv), hs.Id);
        Assert.Null(hs.LockoutEndUtc);
        Assert.Equal(0, hs.FailedLoginCount);
    }

    [Fact]
    public async Task Students_have_no_access_to_management()
    {
        var (w, _, useCase) = Create();
        var hs = w.AddUser("hs01");
        var gv2 = w.AddUser("gv2", Roles.Teacher);
        var me = World.AsStudent(hs);
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.ListAsync(me, null, null, 1, 50));
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.CreateAsync(me, new CreateStudentRequest("x123", "X", null, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.UpdateAsync(me, gv2.Id, new UpdateStudentRequest(false, null, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.ResetPasswordAsync(me, hs.Id));
    }

    [Fact]
    public async Task List_filters_by_search_and_class_and_paginates()
    {
        var (w, gv, useCase) = Create();
        var a = w.AddUser("hs01", displayName: "An");
        w.AddUser("hs02", displayName: "Bình");
        var c = w.AddClass("12A1", a);

        Assert.Equal(2, (await useCase.ListAsync(World.AsTeacher(gv), null, null, 1, 50)).Total);
        Assert.Equal("hs02", Assert.Single((await useCase.ListAsync(World.AsTeacher(gv), "bình", null, 1, 50)).Students).Username);
        Assert.Equal("hs01", Assert.Single((await useCase.ListAsync(World.AsTeacher(gv), null, c.Id, 1, 50)).Students).Username);
        var page = await useCase.ListAsync(World.AsTeacher(gv), null, null, 2, 1);
        Assert.Equal("hs02", Assert.Single(page.Students).Username);
        Assert.Equal(2, page.Total);
    }
}

public class ClassManagementTests
{
    private static (World W, User Gv, ClassManagementUseCase UseCase) Create()
    {
        var w = new World();
        var gv = w.AddUser("gv", Roles.Teacher);
        return (w, gv, new ClassManagementUseCase(w.ClassRepo, w.UnitOfWork, w.Clock));
    }

    [Fact]
    public async Task Create_update_and_unique_names_ignoring_case()
    {
        var (_, gv, useCase) = Create();
        var user = World.AsTeacher(gv);
        var c = await useCase.CreateAsync(user, new ClassInputDto(" 12A1 "));
        Assert.Equal("12A1", c.Name);
        await Assert.ThrowsAsync<ConflictException>(() => useCase.CreateAsync(user, new ClassInputDto("12a1")));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(user, new ClassInputDto("  ")));
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.CreateAsync(user, new ClassInputDto(new string('x', 101))));
        Assert.Equal("12A9", (await useCase.UpdateAsync(user, c.Id, new ClassInputDto("12A9"))).Name);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.UpdateAsync(user, 999, new ClassInputDto("Z")));
    }

    [Fact]
    public async Task Delete_is_blocked_by_active_assignments_but_keeps_students()
    {
        var (w, gv, useCase) = Create();
        var hs = w.AddUser("hs01");
        var used = w.AddClass("12A1", hs);
        var free = w.AddClass("12A2", hs);
        w.AddLesson();
        w.Assign("bai-01-bt1", false, null, used);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.DeleteAsync(World.AsTeacher(gv), used.Id));
        Assert.Equal("HAS_DEPENDENCIES", ex.Code);
        await useCase.DeleteAsync(World.AsTeacher(gv), free.Id);
        Assert.DoesNotContain(w.Classes, c => c.Id == free.Id);
        Assert.Contains(w.Users, u => u.Id == hs.Id);
    }
}

public class ProgressUseCaseTests
{
    [Fact]
    public async Task Students_cannot_read_progress_and_unknown_lesson_is_404()
    {
        var w = new World();
        var hs = w.AddUser("hs01");
        var gv = w.AddUser("gv", Roles.Teacher);
        var useCase = new ProgressUseCase(new StubProgressRepository(), w.UserRepo, w.LessonRepo, w.SubmissionRepo);

        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.GetAsync(World.AsStudent(hs), null, "bai-01", null, null, 1, 50));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.GetAsync(World.AsTeacher(gv), null, "Sai Ma", null, null, 1, 50));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.GetAsync(World.AsTeacher(gv), null, "khong-co", null, null, 1, 50));
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.GetStudentSubmissionsAsync(World.AsStudent(hs), hs.Id, "x"));
    }

    [Fact]
    public async Task Teacher_gets_submissions_with_code_and_404_for_unknown_student_or_exercise()
    {
        var w = new World();
        var hs = w.AddUser("hs01");
        var gv = w.AddUser("gv", Roles.Teacher);
        w.AddLesson(exercises: 1);
        w.Submissions.Add(new Submission { Id = 1, AssignmentId = 1, ExerciseId = "bai-01-bt1", StudentId = hs.Id, Code = "print('<b>')", PassedTests = 1, TotalTests = 2, SubmittedUtc = w.Clock.UtcNow });
        var useCase = new ProgressUseCase(new StubProgressRepository(), w.UserRepo, w.LessonRepo, w.SubmissionRepo);

        var list = await useCase.GetStudentSubmissionsAsync(World.AsTeacher(gv), hs.Id, "bai-01-bt1");

        Assert.Equal("print('<b>')", Assert.Single(list.Submissions).Code);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.GetStudentSubmissionsAsync(World.AsTeacher(gv), 999, "bai-01-bt1"));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.GetStudentSubmissionsAsync(World.AsTeacher(gv), hs.Id, "khong-co"));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.GetStudentSubmissionsAsync(World.AsTeacher(gv), gv.Id, "bai-01-bt1"));
    }

    [Fact]
    public async Task Cells_are_not_assigned_not_started_in_progress_or_passed()
    {
        var w = new World();
        var gv = w.AddUser("gv", Roles.Teacher);
        var repo = new StubProgressRepository
        {
            Snapshot = new TinHoc12.Application.Abstractions.ProgressSnapshot(
                new[] { new TinHoc12.Application.Abstractions.ProgressExercise("e1", 1, "BT1"), new TinHoc12.Application.Abstractions.ProgressExercise("e2", 2, "BT2") },
                new[]
                {
                    new TinHoc12.Application.Abstractions.ProgressStudent(1, "hs01", "An", new[] { new TinHoc12.Application.Abstractions.ClassRef(10, "12A1") }),
                    new TinHoc12.Application.Abstractions.ProgressStudent(2, "hs03", "Cường", new[] { new TinHoc12.Application.Abstractions.ClassRef(20, "12A2") }),
                },
                2,
                new[]
                {
                    new TinHoc12.Application.Abstractions.ProgressAssignment("e1", true, Array.Empty<int>(), null),
                    new TinHoc12.Application.Abstractions.ProgressAssignment("e2", false, new[] { 10 }, w.Clock.UtcNow.AddDays(1)),
                },
                new Dictionary<(int, string), TinHoc12.Application.Abstractions.ExerciseStats>
                {
                    [(1, "e1")] = new("e1", 2, 2, true, true, w.Clock.UtcNow),
                    [(2, "e1")] = new("e1", 1, 1, false, false, w.Clock.UtcNow),
                },
                new HashSet<(int, string)> { (1, "e1") },
                new Dictionary<string, int> { ["e1"] = 2, ["e2"] = 3 }),
        };
        var useCase = new ProgressUseCase(repo, w.UserRepo, w.LessonRepo, w.SubmissionRepo);

        var result = await useCase.GetAsync(World.AsTeacher(gv), null, "bai-01", null, null, 1, 50);

        Assert.Equal(new[] { "Passed", "NotStarted" }, result.Rows[0].Cells.Select(c => c.Status));
        Assert.Equal(new[] { "InProgress", "NotAssigned" }, result.Rows[1].Cells.Select(c => c.Status));
        Assert.True(result.Rows[0].Cells[0].HasViewedSolution);
        Assert.Equal("12A1", result.Rows[0].ClassName);
        Assert.NotNull(result.Exercises[1].DueAt);
        Assert.Null(result.Exercises[0].DueAt);
    }

    private sealed class StubProgressRepository : TinHoc12.Application.Abstractions.IProgressRepository
    {
        public TinHoc12.Application.Abstractions.ProgressSnapshot? Snapshot { get; set; }

        public Task<TinHoc12.Application.Abstractions.ProgressSnapshot?> GetAsync(
            int? classId, string lessonId, string? search, string? sort, int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(Snapshot);
    }
}
