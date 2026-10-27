using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Exceptions;
using TinHoc12.Domain.Entities;
using TinHoc12.Infrastructure.Persistence;
using TinHoc12.Infrastructure.Repositories;

namespace TinHoc12.Infrastructure.Tests;

public class AssignmentRepositoryTests
{
    private static async Task<(TestDb Db, User Teacher, User Hs1, User Hs3, SchoolClass C1, SchoolClass C2)> ArrangeAsync()
    {
        var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (teacher, hs1, hs2) = await Data.BasicUsersAsync(ctx);
        var c1 = new SchoolClass { Name = "12A1", CreatedUtc = DateTime.UtcNow };
        var c2 = new SchoolClass { Name = "12A2", CreatedUtc = DateTime.UtcNow };
        c1.Members.Add(new ClassMember { StudentId = hs1.Id });
        c2.Members.Add(new ClassMember { StudentId = hs2.Id });
        ctx.Classes.AddRange(c1, c2);
        ctx.Lessons.AddRange(TestDb.Lesson("b-1", 1, 2), TestDb.Lesson("b-2", 2, 1));
        await ctx.SaveChangesAsync();
        return (db, teacher, hs1, hs2, c1, c2);
    }

    [Fact]
    public async Task Visibility_follows_publish_archive_class_and_cancellation()
    {
        var (db, teacher, hs1, hs2, c1, _) = await ArrangeAsync();
        using var _ = db;
        await using (var ctx = db.CreateContext())
        {
            ctx.Assignments.Add(Data.Assignment("b-1-bt1", teacher.Id));
            ctx.Assignments.Add(Data.Assignment("b-1-bt2", teacher.Id, false, c1.Id));
            ctx.Assignments.Add(Data.Assignment("b-2-bt1", teacher.Id));
            await ctx.SaveChangesAsync();
        }

        await using var read = db.CreateContext();
        var repo = new AssignmentRepository(read);
        Assert.Equal(new[] { "b-1-bt1", "b-1-bt2", "b-2-bt1" }, (await repo.GetVisibleForStudentAsync(hs1.Id, DateTime.UtcNow)).Select(r => r.ExerciseId).OrderBy(x => x));
        Assert.Equal(new[] { "b-1-bt1", "b-2-bt1" }, (await repo.GetVisibleForStudentAsync(hs2.Id, DateTime.UtcNow)).Select(r => r.ExerciseId).OrderBy(x => x));

        await using (var upd = db.CreateContext())
        {
            (await upd.Lessons.SingleAsync(l => l.Id == "b-2")).IsPublished = false;
            (await upd.Exercises.SingleAsync(e => e.Id == "b-1-bt1")).IsArchived = true;
            (await upd.Assignments.SingleAsync(a => a.ExerciseId == "b-1-bt2")).CancelledUtc = DateTime.UtcNow;
            await upd.SaveChangesAsync();
        }

        await using var read2 = db.CreateContext();
        Assert.Empty(await new AssignmentRepository(read2).GetVisibleForStudentAsync(hs1.Id, DateTime.UtcNow));
    }

    [Fact]
    public async Task Moving_a_student_between_classes_changes_visibility_immediately()
    {
        var (db, teacher, hs1, hs2, c1, c2) = await ArrangeAsync();
        using var _ = db;
        await using (var ctx = db.CreateContext())
        {
            ctx.Assignments.Add(Data.Assignment("b-1-bt1", teacher.Id, false, c1.Id));
            await ctx.SaveChangesAsync();
        }

        await using var ctx2 = db.CreateContext();
        Assert.Empty(await new AssignmentRepository(ctx2).GetVisibleForStudentAsync(hs2.Id, DateTime.UtcNow));
        await new ClassRepository(ctx2).ReplaceMembershipsAsync(hs2.Id, new[] { c1.Id, c2.Id });
        await ctx2.SaveChangesAsync();
        Assert.Single(await new AssignmentRepository(ctx2).GetVisibleForStudentAsync(hs2.Id, DateTime.UtcNow));
        Assert.Equal(2, (await new ClassRepository(ctx2).GetClassesOfStudentsAsync(new[] { hs2.Id }))[hs2.Id].Count);
        Assert.NotNull(hs1);
    }

    [Fact]
    public async Task Counts_for_assignment_list_reached_submitted_and_passed()
    {
        var (db, teacher, hs1, hs2, c1, _) = await ArrangeAsync();
        using var _ = db;
        int assignmentId;
        await using (var ctx = db.CreateContext())
        {
            var a = Data.Assignment("b-1-bt1", teacher.Id);
            var b = Data.Assignment("b-1-bt2", teacher.Id, false, c1.Id);
            ctx.Assignments.AddRange(a, b);
            await ctx.SaveChangesAsync();
            assignmentId = a.Id;
            ctx.Submissions.Add(Data.Submission(a.Id, "b-1-bt1", hs1.Id, 3, 3));
            ctx.Submissions.Add(Data.Submission(a.Id, "b-1-bt1", hs2.Id, 1, 3));
            await ctx.SaveChangesAsync();
        }

        await using var read = db.CreateContext();
        var repo = new AssignmentRepository(read);
        var rows = await repo.ListAsync(null, null, false);
        var all = rows.Single(r => r.Assignment.Id == assignmentId);
        Assert.Equal(2, all.AssignedStudents);
        Assert.Equal(2, all.SubmittedStudents);
        Assert.Equal(1, all.PassedStudents);
        var byClass = rows.Single(r => r.Assignment.Id != assignmentId);
        Assert.Equal(1, byClass.AssignedStudents);
        Assert.Equal(new[] { "12A1" }, byClass.Classes.Select(c => c.Name));
        Assert.Equal(2, await repo.CountStudentsReachedAsync(new[] { "b-1-bt1" }));
        Assert.Equal(1, await repo.CountStudentsReachedAsync(new[] { "b-1-bt2" }));
        Assert.Single(await repo.ListAsync("b-1", c1.Id, false), r => r.Assignment.Id == assignmentId);
        Assert.Empty(await repo.ListAsync("b-2", null, false));
    }

    [Fact]
    public async Task Removing_a_class_clears_links_from_cancelled_assignments_and_keeps_students()
    {
        var (db, teacher, hs1, _, c1, _) = await ArrangeAsync();
        using var _ = db;
        await using (var ctx = db.CreateContext())
        {
            var a = Data.Assignment("b-1-bt1", teacher.Id, false, c1.Id);
            a.CancelledUtc = DateTime.UtcNow;
            ctx.Assignments.Add(a);
            await ctx.SaveChangesAsync();
        }

        await using var work = db.CreateContext();
        var repo = new ClassRepository(work);
        Assert.False(await repo.IsTargetOfActiveAssignmentAsync(c1.Id));
        await repo.RemoveAsync((await repo.GetByIdAsync(c1.Id))!);
        await work.SaveChangesAsync();

        await using var check = db.CreateContext();
        Assert.Equal(1, await check.Classes.CountAsync());
        Assert.Equal(0, await check.ClassMembers.CountAsync(m => m.StudentId == hs1.Id));
        Assert.Equal(3, await check.Users.CountAsync());
    }
}

public class SubmissionAndProgressRepositoryTests
{
    [Fact]
    public async Task Stats_aggregate_per_exercise_and_solution_view_is_idempotent()
    {
        using var db = new TestDb();
        int aId;
        User hs;
        await using (var ctx = db.CreateContext())
        {
            var (teacher, hs1, _) = await Data.BasicUsersAsync(ctx);
            hs = hs1;
            ctx.Lessons.Add(TestDb.Lesson("b-1", 1, 2));
            await ctx.SaveChangesAsync();
            var a = Data.Assignment("b-1-bt1", teacher.Id);
            ctx.Assignments.Add(a);
            await ctx.SaveChangesAsync();
            aId = a.Id;
            ctx.Submissions.AddRange(
                Data.Submission(a.Id, "b-1-bt1", hs.Id, 1, 3),
                Data.Submission(a.Id, "b-1-bt1", hs.Id, 3, 3, late: true),
                Data.Submission(a.Id, "b-1-bt1", hs.Id, 2, 3));
            await ctx.SaveChangesAsync();
        }

        await using var read = db.CreateContext();
        var repo = new SubmissionRepository(read);
        var stats = (await repo.GetStatsAsync(hs.Id))["b-1-bt1"];
        Assert.Equal(3, stats.Attempts);
        Assert.Equal(3, stats.BestPassedTests);
        Assert.True(stats.AnyPassed);
        Assert.False(stats.AnyPassedOnTime);
        Assert.Equal(DateTimeKind.Utc, stats.LastSubmittedUtc!.Value.Kind);
        Assert.Equal(3, await repo.CountAttemptsAsync(hs.Id, "b-1-bt1"));
        Assert.Equal(3, (await repo.GetByStudentExerciseAsync(hs.Id, "b-1-bt1")).Count);
        Assert.NotNull(await repo.GetOwnedAsync(1, hs.Id));
        Assert.Null(await repo.GetOwnedAsync(1, hs.Id + 1));

        await repo.RecordSolutionViewAsync(hs.Id, "b-1-bt1", DateTime.UtcNow);
        await repo.RecordSolutionViewAsync(hs.Id, "b-1-bt1", DateTime.UtcNow.AddMinutes(1));
        await using var check = db.CreateContext();
        Assert.Equal(1, await check.SolutionViews.CountAsync());
        Assert.Contains("b-1-bt1", await new SubmissionRepository(check).GetViewedSolutionExerciseIdsAsync(hs.Id));
        Assert.True(aId > 0);
    }

    [Fact]
    public async Task Progress_snapshot_has_students_assignments_stats_and_test_counts()
    {
        using var db = new TestDb();
        User hs1;
        await using (var ctx = db.CreateContext())
        {
            var (teacher, a, b) = await Data.BasicUsersAsync(ctx);
            hs1 = a;
            var c = new SchoolClass { Name = "12A1", CreatedUtc = DateTime.UtcNow };
            c.Members.Add(new ClassMember { StudentId = a.Id });
            ctx.Classes.Add(c);
            ctx.Lessons.Add(TestDb.Lesson("b-1", 1, 2));
            await ctx.SaveChangesAsync();
            var asg = Data.Assignment("b-1-bt1", teacher.Id, false, c.Id);
            ctx.Assignments.Add(asg);
            await ctx.SaveChangesAsync();
            ctx.Submissions.Add(Data.Submission(asg.Id, "b-1-bt1", a.Id, 3, 3));
            await ctx.SaveChangesAsync();
            Assert.NotNull(b);
        }

        await using var read = db.CreateContext();
        var repo = new ProgressRepository(read);
        var all = await repo.GetAsync(null, "b-1", null, null, 1, 50);
        Assert.NotNull(all);
        Assert.Equal(2, all!.TotalStudents);
        Assert.Equal(new[] { "b-1-bt1", "b-1-bt2" }, all.Exercises.Select(e => e.Id));
        Assert.Equal(3, all.TestCounts["b-1-bt1"]);
        Assert.True(all.Stats[(hs1.Id, "b-1-bt1")].AnyPassed);
        Assert.Single(all.Assignments);

        var onlyClass = await repo.GetAsync(read.Classes.Select(c => c.Id).First(), "b-1", null, null, 1, 50);
        Assert.Equal(new[] { "hs01" }, onlyClass!.Students.Select(s => s.Username));
        Assert.Equal(new[] { "12A1" }, onlyClass.Students[0].Classes.Select(c => c.Name));
        Assert.Equal(new[] { "hs02" }, (await repo.GetAsync(null, "b-1", "HS02", null, 1, 50))!.Students.Select(s => s.Username));
        Assert.Single((await repo.GetAsync(null, "b-1", null, null, 2, 1))!.Students);
        Assert.Null(await repo.GetAsync(null, "khong-co", null, null, 1, 50));
    }

    [Fact]
    public async Task Sorting_by_passed_count_is_done_on_all_students_before_paging()
    {
        using var db = new TestDb();
        await using (var ctx = db.CreateContext())
        {
            var teacher = Data.User("giaovien", "Teacher");
            var students = Enumerable.Range(1, 6).Select(i => Data.User("hs" + i)).ToList();
            ctx.Users.Add(teacher);
            ctx.Users.AddRange(students);
            ctx.Lessons.Add(TestDb.Lesson("b-1", 1, 3));
            await ctx.SaveChangesAsync();
            var asg = Enumerable.Range(1, 3).Select(i => Data.Assignment("b-1-bt" + i, teacher.Id)).ToList();
            ctx.Assignments.AddRange(asg);
            await ctx.SaveChangesAsync();
            // hs6 đạt 3 bài, hs5 đạt 2, hs4 đạt 1; hs1..3 chưa đạt (tên đứng trước hs4..6 theo bảng chữ cái)
            for (var s = 3; s < 6; s++)
            {
                for (var e = 0; e <= s - 3; e++)
                {
                    ctx.Submissions.Add(Data.Submission(asg[e].Id, "b-1-bt" + (e + 1), students[s].Id, 3, 3));
                }
            }

            await ctx.SaveChangesAsync();
        }

        await using var read = db.CreateContext();
        var repo = new ProgressRepository(read);
        var p1 = await repo.GetAsync(null, "b-1", null, "passed", 1, 2);
        var p2 = await repo.GetAsync(null, "b-1", null, "passed", 2, 2);
        Assert.Equal(new[] { "hs6", "hs5" }, p1!.Students.Select(s => s.Username));
        Assert.Equal(new[] { "hs4", "hs1" }, p2!.Students.Select(s => s.Username));
        Assert.Equal(6, p1.TotalStudents);
    }

    [Fact]
    public async Task Progress_for_200_students_x_30_exercises_x_3_submissions_is_fast()
    {
        using var db = new TestDb();
        await using (var ctx = db.CreateContext())
        {
            var teacher = Data.User("giaovien", "Teacher");
            ctx.Users.Add(teacher);
            var c = new SchoolClass { Name = "12A1", CreatedUtc = DateTime.UtcNow };
            ctx.Classes.Add(c);
            ctx.Lessons.Add(TestDb.Lesson("b-1", 1, 30));
            var students = Enumerable.Range(1, 200).Select(i => Data.User("hs" + i.ToString("000"))).ToList();
            ctx.Users.AddRange(students);
            await ctx.SaveChangesAsync();
            foreach (var s in students)
            {
                c.Members.Add(new ClassMember { StudentId = s.Id });
            }

            await ctx.SaveChangesAsync();
            var assignments = Enumerable.Range(1, 30).Select(i => Data.Assignment("b-1-bt" + i, teacher.Id)).ToList();
            ctx.Assignments.AddRange(assignments);
            await ctx.SaveChangesAsync();
            ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            foreach (var s in students)
            {
                for (var i = 0; i < 30; i++)
                {
                    for (var k = 0; k < 3; k++)
                    {
                        ctx.Submissions.Add(Data.Submission(assignments[i].Id, "b-1-bt" + (i + 1), s.Id, k, 3));
                    }
                }
            }

            await ctx.SaveChangesAsync();
        }

        await using var read = db.CreateContext();
        var classId = await read.Classes.Select(c => c.Id).FirstAsync();
        var sw = Stopwatch.StartNew();
        var snapshot = await new ProgressRepository(read).GetAsync(classId, "b-1", null, null, 1, 100);
        sw.Stop();

        Assert.Equal(200, snapshot!.TotalStudents);
        Assert.Equal(100, snapshot.Students.Count);
        Assert.Equal(100 * 30, snapshot.Stats.Count);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Bảng tiến độ mất {sw.Elapsed}");

        var hs = await read.Users.FirstAsync(u => u.Username == "hs001");
        var sw2 = Stopwatch.StartNew();
        var mine = await new SubmissionRepository(read).GetStatsAsync(hs.Id);
        sw2.Stop();
        Assert.Equal(30, mine.Count);
        Assert.True(sw2.Elapsed < TimeSpan.FromMilliseconds(500), $"Thống kê học sinh mất {sw2.Elapsed}");
    }
}

public class AuthoringAndUnitOfWorkTests
{
    [Fact]
    public async Task Concurrent_edits_to_the_same_lesson_version_produce_a_conflict()
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            seed.Lessons.Add(TestDb.Lesson("b-1", 1, 1));
            await seed.SaveChangesAsync();
        }

        await using var ctx1 = db.CreateContext();
        await using var ctx2 = db.CreateContext();
        var l1 = await new LessonAuthoringRepository(ctx1).GetLessonForEditAsync("b-1");
        var l2 = await new LessonAuthoringRepository(ctx2).GetLessonForEditAsync("b-1");
        l1!.Title = "Người 1";
        l1.Version++;
        l2!.Title = "Người 2";
        l2.Version++;

        await new EfUnitOfWork(ctx1).SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ConflictException>(() => new EfUnitOfWork(ctx2).SaveChangesAsync());
        Assert.Equal("CONFLICT", ex.Code);

        await using var check = db.CreateContext();
        Assert.Equal("Người 1", (await check.Lessons.SingleAsync()).Title);
    }

    [Fact]
    public async Task Duplicate_username_becomes_a_conflict_exception()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        ctx.Users.Add(Data.User("hs01"));
        await ctx.SaveChangesAsync();
        ctx.Users.Add(Data.User("hs01"));
        await Assert.ThrowsAsync<ConflictException>(() => new EfUnitOfWork(ctx).SaveChangesAsync());
    }

    [Fact]
    public async Task Reorder_swaps_orders_inside_a_transaction_without_violating_unique_index()
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            seed.Lessons.AddRange(TestDb.Lesson("a", 1, 0), TestDb.Lesson("b", 2, 0), TestDb.Lesson("c", 3, 0));
            await seed.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        var repo = new LessonAuthoringRepository(ctx);
        var uow = new EfUnitOfWork(ctx);
        var useCase = new TinHoc12.Application.UseCases.Authoring.LessonAuthoringUseCase(
            repo, new AssignmentRepository(ctx), uow, new TinHoc12.Infrastructure.Security.SystemClock());

        await useCase.ReorderAsync(new TinHoc12.Application.Security.CurrentUser(1, "Teacher"), new[] { "c", "a", "b" });

        await using var check = db.CreateContext();
        Assert.Equal(new[] { "c", "a", "b" }, await check.Lessons.OrderBy(l => l.Order).Select(l => l.Id).ToListAsync());
        Assert.Equal(new[] { 1, 2, 3 }, await check.Lessons.OrderBy(l => l.Order).Select(l => l.Order).ToListAsync());
    }

    [Fact]
    public async Task Failed_transaction_rolls_everything_back()
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            seed.Lessons.AddRange(TestDb.Lesson("a", 1, 0), TestDb.Lesson("b", 2, 0));
            await seed.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        var uow = new EfUnitOfWork(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async () =>
        {
            (await ctx.Lessons.SingleAsync(l => l.Id == "a")).Order = 10;
            await uow.SaveChangesAsync();
            throw new InvalidOperationException("lỗi giữa chừng");
        }));

        await using var check = db.CreateContext();
        Assert.Equal(1, (await check.Lessons.SingleAsync(l => l.Id == "a")).Order);
    }

    [Fact]
    public async Task List_rows_have_counts_and_creator_names_and_user_search_is_case_insensitive()
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            var (teacher, _, _) = await Data.BasicUsersAsync(seed);
            teacher.DisplayName = "Cô Lan";
            var lesson = TestDb.Lesson("b-1", 1, 2);
            lesson.CreatedByUserId = teacher.Id;
            lesson.Exercises.First().IsPublished = false;
            seed.Lessons.Add(lesson);
            seed.Lessons.Add(TestDb.Lesson("b-2", 2, 0));
            await seed.SaveChangesAsync();
            (await seed.Lessons.SingleAsync(l => l.Id == "b-2")).IsArchived = true;
            await seed.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        var rows = await new LessonAuthoringRepository(ctx).ListLessonsAsync(false);
        var row = Assert.Single(rows);
        Assert.Equal(2, row.ExerciseCount);
        Assert.Equal(1, row.PublishedExerciseCount);
        Assert.Equal("Cô Lan", row.CreatedByName);
        Assert.Equal(2, (await new LessonAuthoringRepository(ctx).ListLessonsAsync(true)).Count);

        var users = new UserRepository(ctx);
        Assert.NotNull(await users.GetByUsernameAsync("hs01"));
        Assert.True(await users.UsernameExistsAsync("hs01"));
        Assert.Equal(2, (await users.SearchStudentsAsync("HS", null, 1, 10)).Total);
        Assert.Equal(2, await users.CountActiveStudentsAsync());
        var existing = await users.GetExistingUsernamesAsync(new[] { "hs01", "nobody" });
        Assert.Equal(new[] { "hs01" }, existing.ToArray());
    }

    [Fact]
    public async Task Replacing_test_cases_in_place_keeps_unique_orders()
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            seed.Lessons.Add(TestDb.Lesson("b-1", 1, 1));
            await seed.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        var useCase = new TinHoc12.Application.UseCases.Authoring.ExerciseAuthoringUseCase(
            new LessonAuthoringRepository(ctx), new AssignmentRepository(ctx), new EfUnitOfWork(ctx), new TinHoc12.Infrastructure.Security.SystemClock());
        var teacher = new TinHoc12.Application.Security.CurrentUser(1, "Teacher");
        var current = await useCase.GetAsync(teacher, "b-1-bt1");

        TinHoc12.Application.Dtos.ExerciseInputDto Input(int tests, int version) => new(
            null, "Đề mới", "Hard", "s", "i", "o", null,
            new[] { new TinHoc12.Application.Dtos.ExampleDto("1", "1", null) }, "", null, null, false, 2000,
            Enumerable.Range(1, tests).Select(n => new TinHoc12.Application.Dtos.TestCaseDto("in" + n, "out" + n, n == 1)).ToList(), version);

        var more = await useCase.UpdateAsync(teacher, "b-1-bt1", Input(6, current.Version));
        Assert.Equal(6, more.TestCases.Count);
        var fewer = await useCase.UpdateAsync(teacher, "b-1-bt1", Input(3, more.Version));
        Assert.Equal(3, fewer.TestCases.Count);
        Assert.Equal("in1", fewer.TestCases[0].Input);

        await using var check = db.CreateContext();
        var orders = await check.TestCases.OrderBy(t => t.Order).Select(t => t.Order).ToListAsync();
        Assert.Equal(orders.Distinct().Count(), orders.Count);
        Assert.Equal(3, orders.Count);
        Assert.Equal(more.Version + 1, fewer.Version);
    }
}
