using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Mapping;
using TinHoc12.Application.Security;
using TinHoc12.Application.UseCases.Student;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Tests;

public class AssignmentRulesTests
{
    private static StudentAssignmentRowBuilder Row(int id, DateTime? due) => new(id, due);

    private sealed record StudentAssignmentRowBuilder(int Id, DateTime? Due)
    {
        public TinHoc12.Application.Abstractions.StudentAssignmentRow Build() =>
            new(Id, "bt1", "BT", 1, "Easy", "bai-01", "Bài", 1, Due, null);
    }

    [Fact]
    public void Effective_assignment_has_the_earliest_due_date_then_smallest_id_and_no_due_last()
    {
        var jan1 = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = new[] { Row(1, null).Build(), Row(2, jan1.AddDays(5)).Build(), Row(3, jan1).Build(), Row(4, jan1).Build() };

        var effective = AssignmentRules.PickEffective(rows)["bt1"];

        Assert.Equal(3, effective.AssignmentId);
        Assert.Equal(1, AssignmentRules.PickEffective(new[] { Row(5, null).Build(), Row(1, null).Build() })["bt1"].AssignmentId);
    }

    [Fact]
    public async Task All_without_due_plus_class_with_due_gives_hs01_the_due_date_and_hs03_none()
    {
        var w = new World();
        var hs01 = w.AddUser("hs01");
        var hs03 = w.AddUser("hs03");
        var c12a1 = w.AddClass("12A1", hs01);
        w.AddClass("12A2", hs03);
        w.AddLesson();
        var due = w.Clock.UtcNow.AddDays(10);
        w.Assign("bai-01-bt1", all: true);
        var classAssignment = w.Assign("bai-01-bt1", false, due, c12a1);

        var mine = await new GetMyAssignmentsUseCase(w.LessonRepo, w.AssignmentRepo, w.SubmissionRepo, w.Clock).ExecuteAsync(World.AsStudent(hs01));
        var theirs = await new GetMyAssignmentsUseCase(w.LessonRepo, w.AssignmentRepo, w.SubmissionRepo, w.Clock).ExecuteAsync(World.AsStudent(hs03));

        var a = Assert.Single(mine.Assignments);
        Assert.Equal(due, a.DueAt);
        Assert.Equal(classAssignment.Id, a.AssignmentId);
        var b = Assert.Single(theirs.Assignments);
        Assert.Null(b.DueAt);
    }

    [Fact]
    public async Task Class_targeted_assignment_is_invisible_to_students_of_other_classes_and_cancelled_ones_disappear()
    {
        var w = new World();
        var hs01 = w.AddUser("hs01");
        var hs03 = w.AddUser("hs03");
        var c1 = w.AddClass("12A1", hs01);
        w.AddClass("12A2", hs03);
        w.AddLesson();
        var asg = w.Assign("bai-01-bt1", false, null, c1);
        var useCase = new GetMyAssignmentsUseCase(w.LessonRepo, w.AssignmentRepo, w.SubmissionRepo, w.Clock);

        Assert.Single((await useCase.ExecuteAsync(World.AsStudent(hs01))).Assignments);
        Assert.Empty((await useCase.ExecuteAsync(World.AsStudent(hs03))).Assignments);

        asg.CancelledUtc = w.Clock.UtcNow;
        Assert.Empty((await useCase.ExecuteAsync(World.AsStudent(hs01))).Assignments);
    }

    [Fact]
    public async Task My_assignments_are_sorted_by_due_date_with_undated_last_and_forbidden_for_teachers()
    {
        var w = new World();
        var hs = w.AddUser("hs01");
        var gv = w.AddUser("gv", Roles.Teacher);
        w.AddLesson(exercises: 2);
        w.AddLesson("bai-02", 2, exercises: 1);
        w.Assign("bai-01-bt1", all: true);
        w.Assign("bai-01-bt2", all: true, due: w.Clock.UtcNow.AddDays(5));
        w.Assign("bai-02-bt1", all: true, due: w.Clock.UtcNow.AddDays(1));
        var useCase = new GetMyAssignmentsUseCase(w.LessonRepo, w.AssignmentRepo, w.SubmissionRepo, w.Clock);

        var list = (await useCase.ExecuteAsync(World.AsStudent(hs))).Assignments;

        Assert.Equal(new[] { "bai-02-bt1", "bai-01-bt2", "bai-01-bt1" }, list.Select(a => a.ExerciseId));
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.ExecuteAsync(World.AsTeacher(gv)));
    }

    [Fact]
    public async Task Status_flags_overdue_and_passed_late_follow_submissions()
    {
        var w = new World();
        var hs = w.AddUser("hs01");
        w.AddLesson(exercises: 1);
        var due = w.Clock.UtcNow.AddDays(1);
        var asg = w.Assign("bai-01-bt1", all: true, due: due);
        var useCase = new GetMyAssignmentsUseCase(w.LessonRepo, w.AssignmentRepo, w.SubmissionRepo, w.Clock);

        Assert.Equal("NotStarted", (await useCase.ExecuteAsync(World.AsStudent(hs))).Assignments[0].Status);

        w.Submissions.Add(new Submission { Id = 1, AssignmentId = asg.Id, ExerciseId = "bai-01-bt1", StudentId = hs.Id, PassedTests = 1, TotalTests = 2, SubmittedUtc = w.Clock.UtcNow });
        var inProgress = (await useCase.ExecuteAsync(World.AsStudent(hs))).Assignments[0];
        Assert.Equal("InProgress", inProgress.Status);
        Assert.Equal(1, inProgress.BestPassedTests);
        Assert.Equal(2, inProgress.TotalTests);

        w.Clock.Advance(TimeSpan.FromDays(2));
        Assert.True((await useCase.ExecuteAsync(World.AsStudent(hs))).Assignments[0].IsOverdue);

        w.Submissions.Add(new Submission { Id = 2, AssignmentId = asg.Id, ExerciseId = "bai-01-bt1", StudentId = hs.Id, PassedTests = 2, TotalTests = 2, IsPassed = true, IsLate = true, SubmittedUtc = w.Clock.UtcNow });
        var late = (await useCase.ExecuteAsync(World.AsStudent(hs))).Assignments[0];
        Assert.Equal("Passed", late.Status);
        Assert.True(late.PassedLate);
    }
}

public class SubmitSolutionUseCaseTests
{
    private static (World W, User Hs, SubmitSolutionUseCase UseCase) Create(DateTime? due = null)
    {
        var w = new World();
        var hs = w.AddUser("hs01");
        w.AddLesson(exercises: 2);
        w.Assign("bai-01-bt1", all: true, due: due);
        return (w, hs, new SubmitSolutionUseCase(w.LessonRepo, w.AssignmentRepo, w.SubmissionRepo, w.UnitOfWork, w.Clock, new SubmissionThrottle(), w.Options));
    }

    private static SubmitRequest Req(int passed = 2, int total = 2, string code = "print(1)", IReadOnlyList<TestResultInputDto>? results = null) =>
        new(code, passed, total, results);

    [Fact]
    public async Task Valid_passing_submission_is_stored_with_server_side_identity_and_time()
    {
        var (w, hs, useCase) = Create();
        var result = await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req());

        Assert.True(result.IsPassed);
        Assert.False(result.IsLate);
        Assert.Equal(1, result.AttemptNo);
        Assert.Equal("Passed", result.Status);
        var stored = Assert.Single(w.Submissions);
        Assert.Equal(hs.Id, stored.StudentId);
        Assert.Equal(w.Clock.UtcNow, stored.SubmittedUtc);
        Assert.Equal("print(1)", stored.Code);
    }

    [Fact]
    public async Task Partial_result_is_in_progress_and_attempt_numbers_increase()
    {
        var (w, hs, useCase) = Create();
        var first = await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(passed: 1));
        w.Clock.Advance(TimeSpan.FromSeconds(4));
        var second = await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(passed: 2));
        Assert.False(first.IsPassed);
        Assert.Equal("InProgress", first.Status);
        Assert.Equal(2, second.AttemptNo);
    }

    [Fact]
    public async Task Late_flag_is_decided_by_server_time_against_the_effective_due_date()
    {
        var (w, hs, useCase) = Create(due: new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc));
        var onTime = await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req());
        w.Clock.Advance(TimeSpan.FromHours(2));
        var late = await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req());
        Assert.False(onTime.IsLate);
        Assert.True(late.IsLate);
        Assert.Equal(2, w.Submissions.Count);
    }

    [Theory]
    [InlineData(99, 5, "print(1)")]
    [InlineData(-1, 2, "print(1)")]
    [InlineData(1, 0, "print(1)")]
    [InlineData(1, 2, "")]
    [InlineData(1, 2, "   \n ")]
    public async Task Invalid_numbers_or_empty_code_give_validation_error_and_store_nothing(int passed, int total, string code)
    {
        var (w, hs, useCase) = Create();
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(passed, total, code)));
        Assert.Empty(w.Submissions);
    }

    [Fact]
    public async Task Code_longer_than_20000_chars_is_rejected_but_exactly_20000_is_accepted()
    {
        var (w, hs, useCase) = Create();
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(code: new string('x', 20_001))));
        await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(code: new string('x', 20_000)));
        Assert.Single(w.Submissions);
    }

    [Fact]
    public async Task Wrong_total_tests_means_exercise_changed_conflict()
    {
        var (w, hs, useCase) = Create();
        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(passed: 3, total: 3)));
        Assert.Equal("EXERCISE_CHANGED", ex.Code);
        Assert.Empty(w.Submissions);
    }

    [Fact]
    public async Task Results_must_match_total_and_passed_counts()
    {
        var (_, hs, useCase) = Create();
        var ok = new[] { new TestResultInputDto("Passed", 5), new TestResultInputDto("Wrong", 7) };
        await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(passed: 1, results: ok));

        foreach (var bad in new[]
        {
            new[] { new TestResultInputDto("Passed", 5) },
            new[] { new TestResultInputDto("Passed", 5), new TestResultInputDto("Passed", 5) },
            new[] { new TestResultInputDto("Hack", 5), new TestResultInputDto("Wrong", 5) },
        })
        {
            await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(passed: 1, results: bad)));
        }
    }

    [Fact]
    public async Task Unassigned_unpublished_or_unknown_exercises_are_404_and_teachers_are_forbidden()
    {
        var (w, hs, useCase) = Create();
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt2", Req()));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(World.AsStudent(hs), "khong-co", Req()));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(World.AsStudent(hs), "' OR 1=1", Req()));
        w.Lessons[0].IsPublished = false;
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req()));
        var gv = w.AddUser("gv", Roles.Teacher);
        await Assert.ThrowsAsync<ForbiddenException>(() => useCase.ExecuteAsync(World.AsTeacher(gv), "bai-01-bt1", Req()));
    }

    [Fact]
    public async Task Ten_rapid_submissions_produce_one_record_and_the_rest_are_throttled()
    {
        var (w, hs, useCase) = Create();
        var ok = 0;
        var throttled = 0;
        for (var i = 0; i < 10; i++)
        {
            try
            {
                await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req());
                ok++;
            }
            catch (TooManyRequestsException)
            {
                throttled++;
            }
        }

        Assert.Equal(1, ok);
        Assert.Equal(9, throttled);
        Assert.Single(w.Submissions);
    }

    [Fact]
    public async Task Failed_validation_does_not_consume_the_throttle_slot()
    {
        var (_, hs, useCase) = Create();
        await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(code: "")));
        await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req());
    }

    [Fact]
    public async Task Students_only_read_their_own_submissions_and_other_ids_are_404()
    {
        var (w, hs, useCase) = Create();
        var other = w.AddUser("hs02");
        await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(code: "print('hs01')"));
        w.Clock.Advance(TimeSpan.FromSeconds(5));
        await useCase.ExecuteAsync(World.AsStudent(other), "bai-01-bt1", Req(code: "print('hs02')"));
        var history = new GetMySubmissionsUseCase(w.SubmissionRepo);
        var mine = w.Submissions.First(s => s.StudentId == hs.Id);
        var theirs = w.Submissions.First(s => s.StudentId == other.Id);

        var list = await history.ListAsync(World.AsStudent(hs), "bai-01-bt1");
        Assert.Single(list.Submissions);
        Assert.Equal("print('hs01')", (await history.GetAsync(World.AsStudent(hs), mine.Id)).Code);
        await Assert.ThrowsAsync<NotFoundException>(() => history.GetAsync(World.AsStudent(hs), theirs.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => history.GetAsync(World.AsStudent(hs), 9999));
        Assert.DoesNotContain("hs02", UseCases.Json(list));
    }

    [Fact]
    public async Task History_is_newest_first_and_limited_to_50()
    {
        var (w, hs, useCase) = Create();
        for (var i = 0; i < 55; i++)
        {
            await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1", Req(passed: i % 2));
            w.Clock.Advance(TimeSpan.FromSeconds(4));
        }

        var list = await new GetMySubmissionsUseCase(w.SubmissionRepo).ListAsync(World.AsStudent(hs), "bai-01-bt1");
        Assert.Equal(50, list.Submissions.Count);
        Assert.Equal(55, list.Submissions[0].AttemptNo);
        Assert.True(list.Submissions[0].SubmittedAt > list.Submissions[1].SubmittedAt);
    }
}
