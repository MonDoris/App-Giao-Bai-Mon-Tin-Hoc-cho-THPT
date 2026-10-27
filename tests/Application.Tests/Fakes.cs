using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Security;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Tests;

/// <summary>Đồng hồ giả: kiểm thử khóa tạm/hạn nộp không phải chờ thật.</summary>
internal sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan span) => UtcNow += span;
}

/// <summary>Bộ băm giả: nhanh, đủ để kiểm thử logic (hash = "h:" + mật khẩu; "old:" là hash yếu cần băm lại).</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public int HashCalls { get; private set; }
    public int VerifyCalls { get; private set; }

    public string Hash(string password)
    {
        HashCalls++;
        return "h:" + password;
    }

    public PasswordVerification Verify(string hash, string password)
    {
        VerifyCalls++;
        if (hash == "h:" + password)
        {
            return PasswordVerification.Success;
        }

        return hash == "old:" + password ? PasswordVerification.SuccessRehashNeeded : PasswordVerification.Failed;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default) => await action();
}

/// <summary>Toàn bộ dữ liệu trong bộ nhớ và các repository giả dùng chung một nguồn.</summary>
internal sealed class World
{
    public List<Lesson> Lessons { get; } = new();
    public List<User> Users { get; } = new();
    public List<SchoolClass> Classes { get; } = new();
    public List<Assignment> Assignments { get; } = new();
    public List<Submission> Submissions { get; } = new();
    public List<SolutionView> Views { get; } = new();

    public FakeClock Clock { get; } = new();
    public FakePasswordHasher Hasher { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();
    public AuthOptions Options { get; } = new();

    public FakeLessonRepository LessonRepo { get; }
    public FakeUserRepository UserRepo { get; }
    public FakeClassRepository ClassRepo { get; }
    public FakeAssignmentRepository AssignmentRepo { get; }
    public FakeSubmissionRepository SubmissionRepo { get; }

    private int _nextUserId = 1;
    private int _nextAssignmentId = 1;
    private long _nextSubmissionId = 1;
    private int _nextClassId = 1;

    public World()
    {
        LessonRepo = new FakeLessonRepository(this);
        UserRepo = new FakeUserRepository(this);
        ClassRepo = new FakeClassRepository(this);
        AssignmentRepo = new FakeAssignmentRepository(this);
        SubmissionRepo = new FakeSubmissionRepository(this);
    }

    public IEnumerable<Exercise> Exercises => Lessons.SelectMany(l => l.Exercises);

    public static CurrentUser AsTeacher(User u) => new(u.Id, Roles.Teacher);

    public static CurrentUser AsStudent(User u) => new(u.Id, Roles.Student);

    public User AddUser(string username, string role = Roles.Student, string password = "Passw0rd1", bool active = true, string? displayName = null)
    {
        var user = new User
        {
            Id = _nextUserId++,
            Username = username,
            DisplayName = displayName ?? username,
            PasswordHash = "h:" + password,
            Role = role,
            IsActive = active,
            CreatedUtc = Clock.UtcNow,
        };
        Users.Add(user);
        return user;
    }

    public SchoolClass AddClass(string name, params User[] members)
    {
        var c = new SchoolClass { Id = _nextClassId++, Name = name, CreatedUtc = Clock.UtcNow };
        foreach (var m in members)
        {
            c.Members.Add(new ClassMember { ClassId = c.Id, StudentId = m.Id, SchoolClass = c, Student = m });
        }

        Classes.Add(c);
        return c;
    }

    public Lesson AddLesson(string id = "bai-01", int order = 1, bool published = true, bool archived = false, int exercises = 2)
    {
        var lesson = new Lesson
        {
            Id = id,
            Order = order,
            Title = "Bài " + order,
            Description = "Mô tả",
            SummaryJson = "[\"Tóm tắt\"]",
            NotesJson = "[\"Lưu ý 1\"]",
            ConclusionJson = "[\"Tổng kết\"]",
            IsPublished = published,
            IsArchived = archived,
        };
        for (var i = 1; i <= exercises; i++)
        {
            lesson.Exercises.Add(NewExercise($"{id}-bt{i}", id, i, i == 1 ? "print(1)" : null));
        }

        Lessons.Add(lesson);
        return lesson;
    }

    public static Exercise NewExercise(string id, string lessonId, int order, string? solution)
    {
        var e = new Exercise
        {
            Id = id,
            LessonId = lessonId,
            Order = order,
            Title = "Bài tập " + order,
            Difficulty = "Medium",
            Statement = "Đề",
            InputFormat = "Vào",
            OutputFormat = "Ra",
            ExamplesJson = "[{\"input\":\"1\",\"output\":\"1\",\"explanation\":\"x\"}]",
            StarterCode = "# code",
            SolutionCode = solution,
            SolutionExplanation = solution is null ? null : "Giải thích",
        };
        e.TestCases.Add(new TestCase { Order = 2, Input = "2", ExpectedOutput = "2", IsHidden = true });
        e.TestCases.Add(new TestCase { Order = 1, Input = "1", ExpectedOutput = "1", IsHidden = false });
        return e;
    }

    public Assignment Assign(string exerciseId, bool all = false, DateTime? due = null, params SchoolClass[] classes)
    {
        var a = new Assignment
        {
            Id = _nextAssignmentId++,
            ExerciseId = exerciseId,
            AssignedToAll = all,
            DueAtUtc = due,
            CreatedUtc = Clock.UtcNow,
            CreatedByUserId = 1,
        };
        foreach (var c in classes)
        {
            a.Classes.Add(new AssignmentClass { AssignmentId = a.Id, ClassId = c.Id });
        }

        Assignments.Add(a);
        return a;
    }

    public long NextSubmissionId() => _nextSubmissionId++;

    public int NextAssignmentId() => _nextAssignmentId++;

    public int NextClassId() => _nextClassId++;

    public int NextUserId() => _nextUserId++;
}

internal sealed class FakeLessonRepository : ILessonRepository, ILessonAuthoringRepository
{
    private readonly World _w;

    public FakeLessonRepository(World world)
    {
        _w = world;
    }

    public Task<IReadOnlyList<LessonHeader>> GetLessonHeadersAsync(CancellationToken cancellationToken = default)
    {
        // Cố ý không sắp xếp để kiểm tra use case tự sắp theo Order.
        IReadOnlyList<LessonHeader> result = _w.Lessons
            .Select(l => new LessonHeader(l.Id, l.Order, l.Title, l.Description, l.Exercises.Count, l.IsPublished, l.IsArchived))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<Lesson?> GetLessonWithExercisesAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Lessons.FirstOrDefault(l => l.Id == id));

    public Task<Exercise?> GetExerciseAsync(string id, CancellationToken cancellationToken = default)
    {
        var e = _w.Exercises.FirstOrDefault(x => x.Id == id);
        if (e is not null)
        {
            e.Lesson = _w.Lessons.First(l => l.Id == e.LessonId);
        }

        return Task.FromResult(e);
    }

    public Task<IReadOnlyDictionary<string, int>> GetTestCountsAsync(IEnumerable<string> exerciseIds, CancellationToken cancellationToken = default)
    {
        var ids = exerciseIds.ToHashSet();
        IReadOnlyDictionary<string, int> map = _w.Exercises.Where(e => ids.Contains(e.Id)).ToDictionary(e => e.Id, e => e.TestCases.Count);
        return Task.FromResult(map);
    }

    // ---- ILessonAuthoringRepository
    public Task<IReadOnlyList<TeacherLessonRow>> ListLessonsAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TeacherLessonRow> rows = _w.Lessons
            .Where(l => includeArchived || !l.IsArchived)
            .Select(l => new TeacherLessonRow(
                l.Id, l.Order, l.Title, l.Description, l.IsPublished, l.IsArchived, l.Exercises.Count,
                l.Exercises.Count(e => e.IsPublished), _w.Assignments.Count(a => l.Exercises.Any(e => e.Id == a.ExerciseId)),
                l.Version, l.UpdatedUtc, null))
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<Lesson?> GetLessonForEditAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Lessons.FirstOrDefault(l => l.Id == id));

    public Task<Exercise?> GetExerciseForEditAsync(string id, CancellationToken cancellationToken = default)
        => GetExerciseAsync(id, cancellationToken);

    public Task<bool> LessonIdExistsAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Lessons.Any(l => l.Id == id));

    public Task<bool> ExerciseIdExistsAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Exercises.Any(e => e.Id == id));

    public Task<int> NextLessonOrderAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((_w.Lessons.Count == 0 ? 0 : _w.Lessons.Max(l => l.Order)) + 1);

    public Task<int> NextExerciseOrderAsync(string lessonId, CancellationToken cancellationToken = default)
    {
        var l = _w.Lessons.FirstOrDefault(x => x.Id == lessonId);
        return Task.FromResult((l is null || l.Exercises.Count == 0 ? 0 : l.Exercises.Max(e => e.Order)) + 1);
    }

    public Task<Dependencies> GetLessonDependenciesAsync(string lessonId, CancellationToken cancellationToken = default)
    {
        var ids = _w.Lessons.First(l => l.Id == lessonId).Exercises.Select(e => e.Id).ToHashSet();
        return Task.FromResult(new Dependencies(
            _w.Assignments.Count(a => ids.Contains(a.ExerciseId)), _w.Submissions.Count(s => ids.Contains(s.ExerciseId))));
    }

    public Task<Dependencies> GetExerciseDependenciesAsync(string exerciseId, CancellationToken cancellationToken = default)
        => Task.FromResult(new Dependencies(
            _w.Assignments.Count(a => a.ExerciseId == exerciseId), _w.Submissions.Count(s => s.ExerciseId == exerciseId)));

    public Task<IReadOnlyList<Lesson>> GetAllLessonsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Lesson>>(_w.Lessons.OrderBy(l => l.Order).ToList());

    public Task<IReadOnlyList<Exercise>> GetExercisesOfLessonAsync(string lessonId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Exercise>>(_w.Lessons.First(l => l.Id == lessonId).Exercises.OrderBy(e => e.Order).ToList());

    public void AddLesson(Lesson lesson) => _w.Lessons.Add(lesson);

    public void AddExercise(Exercise exercise)
    {
        var lesson = _w.Lessons.First(l => l.Id == exercise.LessonId);
        exercise.Lesson = lesson;
        lesson.Exercises.Add(exercise);
    }

    public void RemoveLesson(Lesson lesson) => _w.Lessons.Remove(lesson);

    public void RemoveExercise(Exercise exercise) => _w.Lessons.First(l => l.Id == exercise.LessonId).Exercises.Remove(exercise);
}

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly World _w;

    public FakeUserRepository(World world)
    {
        _w = world;
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Users.FirstOrDefault(u => string.Equals(u.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Users.Any(u => string.Equals(u.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlySet<string>> GetExistingUsernamesAsync(IEnumerable<string> normalizedUsernames, CancellationToken cancellationToken = default)
    {
        var wanted = normalizedUsernames.ToHashSet();
        IReadOnlySet<string> found = _w.Users.Where(u => wanted.Contains(u.Username)).Select(u => u.Username).ToHashSet();
        return Task.FromResult(found);
    }

    public void Add(User user)
    {
        if (user.Id == 0)
        {
            user.Id = _w.NextUserId();
        }

        _w.Users.Add(user);
    }

    public Task<(IReadOnlyList<User> Items, int Total)> SearchStudentsAsync(
        string? search, int? classId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var q = _w.Users.Where(u => u.Role == Roles.Student);
        if (classId is { } cid)
        {
            q = q.Where(u => _w.Classes.Any(c => c.Id == cid && c.Members.Any(m => m.StudentId == u.Id)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(u => u.Username.Contains(search, StringComparison.OrdinalIgnoreCase) || u.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var all = q.OrderBy(u => u.DisplayName).ToList();
        IReadOnlyList<User> items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((items, all.Count));
    }

    public Task<int> CountActiveStudentsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Users.Count(u => u.Role == Roles.Student && u.IsActive));
}

internal sealed class FakeClassRepository : IClassRepository
{
    private readonly World _w;

    public FakeClassRepository(World world)
    {
        _w = world;
    }

    public Task<IReadOnlyList<ClassRow>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ClassRow>>(_w.Classes.OrderBy(c => c.Name).Select(c => new ClassRow(c.Id, c.Name, c.Members.Count)).ToList());

    public Task<SchoolClass?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Classes.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<ClassRef>> GetExistingAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var wanted = ids.ToHashSet();
        return Task.FromResult<IReadOnlyList<ClassRef>>(_w.Classes.Where(c => wanted.Contains(c.Id)).Select(c => new ClassRef(c.Id, c.Name)).ToList());
    }

    public Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Classes.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) && c.Id != exceptId));

    public Task<bool> IsTargetOfActiveAssignmentAsync(int classId, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Assignments.Any(a => a.IsActive && a.Classes.Any(c => c.ClassId == classId)));

    public Task<IReadOnlyDictionary<int, IReadOnlyList<ClassRef>>> GetClassesOfStudentsAsync(IEnumerable<int> studentIds, CancellationToken cancellationToken = default)
    {
        var ids = studentIds.ToHashSet();
        IReadOnlyDictionary<int, IReadOnlyList<ClassRef>> map = _w.Classes
            .SelectMany(c => c.Members.Where(m => ids.Contains(m.StudentId)).Select(m => (m.StudentId, Ref: new ClassRef(c.Id, c.Name))))
            .GroupBy(x => x.StudentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ClassRef>)g.Select(x => x.Ref).ToList());
        return Task.FromResult(map);
    }

    public Task ReplaceMembershipsAsync(int studentId, IReadOnlyCollection<int> classIds, CancellationToken cancellationToken = default)
    {
        foreach (var c in _w.Classes)
        {
            c.Members.Where(m => m.StudentId == studentId && !classIds.Contains(c.Id)).ToList().ForEach(m => c.Members.Remove(m));
            if (classIds.Contains(c.Id) && c.Members.All(m => m.StudentId != studentId))
            {
                c.Members.Add(new ClassMember { ClassId = c.Id, StudentId = studentId });
            }
        }

        return Task.CompletedTask;
    }

    public void Add(SchoolClass schoolClass)
    {
        schoolClass.Id = _w.NextClassId();
        _w.Classes.Add(schoolClass);
    }

    public Task RemoveAsync(SchoolClass schoolClass, CancellationToken cancellationToken = default)
    {
        _w.Classes.Remove(schoolClass);
        return Task.CompletedTask;
    }
}

internal sealed class FakeAssignmentRepository : IAssignmentRepository
{
    private readonly World _w;

    public FakeAssignmentRepository(World world)
    {
        _w = world;
    }

    public Task<IReadOnlyList<StudentAssignmentRow>> GetVisibleForStudentAsync(int studentId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var myClasses = _w.Classes.Where(c => c.Members.Any(m => m.StudentId == studentId)).Select(c => c.Id).ToHashSet();
        var rows = new List<StudentAssignmentRow>();
        foreach (var a in _w.Assignments.Where(a => a.IsActive && (a.StartAtUtc is null || a.StartAtUtc <= nowUtc) && (a.AssignedToAll || a.Classes.Any(c => myClasses.Contains(c.ClassId)))))
        {
            var e = _w.Exercises.FirstOrDefault(x => x.Id == a.ExerciseId);
            var l = e is null ? null : _w.Lessons.First(x => x.Id == e.LessonId);
            if (e is null || l is null || !e.IsPublished || e.IsArchived || !l.IsPublished || l.IsArchived)
            {
                continue;
            }

            rows.Add(new StudentAssignmentRow(a.Id, e.Id, e.Title, e.Order, e.Difficulty, l.Id, l.Title, l.Order, a.DueAtUtc, a.Note));
        }

        return Task.FromResult<IReadOnlyList<StudentAssignmentRow>>(rows);
    }

    public Task<Assignment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Assignments.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Assignment>> GetActiveForExercisesAsync(IEnumerable<string> exerciseIds, CancellationToken cancellationToken = default)
    {
        var ids = exerciseIds.ToHashSet();
        return Task.FromResult<IReadOnlyList<Assignment>>(_w.Assignments.Where(a => a.IsActive && ids.Contains(a.ExerciseId)).ToList());
    }

    public Task<IReadOnlyList<AssignmentListRow>> ListAsync(string? lessonId, int? classId, bool includeCancelled, CancellationToken cancellationToken = default)
        => ListByIdsAsync(_w.Assignments.Where(a => includeCancelled || a.IsActive).Select(a => a.Id), cancellationToken);

    public Task<IReadOnlyList<AssignmentListRow>> ListByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var wanted = ids.ToHashSet();
        var rows = _w.Assignments.Where(a => wanted.Contains(a.Id)).Select(a =>
        {
            var e = _w.Exercises.First(x => x.Id == a.ExerciseId);
            var l = _w.Lessons.First(x => x.Id == e.LessonId);
            var classes = a.Classes.Select(c => _w.Classes.First(k => k.Id == c.ClassId)).Select(k => new ClassRef(k.Id, k.Name)).ToList();
            return new AssignmentListRow(a, e.Title, l.Id, l.Title, classes, 0, 0, 0);
        }).ToList();
        return Task.FromResult<IReadOnlyList<AssignmentListRow>>(rows);
    }

    public Task<bool> HasSubmissionsAsync(int assignmentId, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Submissions.Any(s => s.AssignmentId == assignmentId));

    public Task<int> CountStudentsReachedAsync(IEnumerable<string> exerciseIds, CancellationToken cancellationToken = default)
    {
        var ids = exerciseIds.ToHashSet();
        var active = _w.Assignments.Where(a => a.IsActive && ids.Contains(a.ExerciseId)).ToList();
        if (active.Any(a => a.AssignedToAll))
        {
            return Task.FromResult(_w.Users.Count(u => u.Role == Roles.Student && u.IsActive));
        }

        var classIds = active.SelectMany(a => a.Classes.Select(c => c.ClassId)).ToHashSet();
        return Task.FromResult(_w.Classes.Where(c => classIds.Contains(c.Id)).SelectMany(c => c.Members.Select(m => m.StudentId)).Distinct().Count());
    }

    public void Add(Assignment assignment)
    {
        assignment.Id = _w.NextAssignmentId();
        _w.Assignments.Add(assignment);
    }

    public void Remove(Assignment assignment) => _w.Assignments.Remove(assignment);
}

internal sealed class FakeSubmissionRepository : ISubmissionRepository
{
    private readonly World _w;

    public FakeSubmissionRepository(World world)
    {
        _w = world;
    }

    public void Add(Submission submission)
    {
        submission.Id = _w.NextSubmissionId();
        _w.Submissions.Add(submission);
    }

    public Task<IReadOnlyDictionary<string, ExerciseStats>> GetStatsAsync(int studentId, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, ExerciseStats> map = _w.Submissions.Where(s => s.StudentId == studentId)
            .GroupBy(s => s.ExerciseId)
            .ToDictionary(
                g => g.Key,
                g => new ExerciseStats(
                    g.Key, g.Count(), g.Max(x => x.PassedTests), g.Any(x => x.IsPassed), g.Any(x => x.IsPassed && !x.IsLate), g.Max(x => x.SubmittedUtc)));
        return Task.FromResult(map);
    }

    public Task<IReadOnlySet<string>> GetViewedSolutionExerciseIdsAsync(int studentId, CancellationToken cancellationToken = default)
    {
        IReadOnlySet<string> set = _w.Views.Where(v => v.StudentId == studentId).Select(v => v.ExerciseId).ToHashSet();
        return Task.FromResult(set);
    }

    public Task RecordSolutionViewAsync(int studentId, string exerciseId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (!_w.Views.Any(v => v.StudentId == studentId && v.ExerciseId == exerciseId))
        {
            _w.Views.Add(new SolutionView { StudentId = studentId, ExerciseId = exerciseId, FirstViewedUtc = nowUtc });
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Submission>> GetByStudentExerciseAsync(int studentId, string exerciseId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Submission>>(
            _w.Submissions.Where(s => s.StudentId == studentId && s.ExerciseId == exerciseId).OrderBy(s => s.SubmittedUtc).ThenBy(s => s.Id).ToList());

    public Task<Submission?> GetOwnedAsync(long id, int studentId, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Submissions.FirstOrDefault(s => s.Id == id && s.StudentId == studentId));

    public Task<int> CountAttemptsAsync(int studentId, string exerciseId, CancellationToken cancellationToken = default)
        => Task.FromResult(_w.Submissions.Count(s => s.StudentId == studentId && s.ExerciseId == exerciseId));
}
