using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Rules;
using TinHoc12.Application.Security;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.UseCases.Students;

/// <summary>
/// Giáo viên quản lý tài khoản học sinh (AC-104..107, 110): tạo, nhập danh sách, đặt lại mật khẩu, khóa/mở khóa, đổi lớp.
/// Không bao giờ tạo/sửa được tài khoản giáo viên qua các thao tác này.
/// </summary>
public sealed class StudentManagementUseCase
{
    public const int MaxImportRows = 200;
    private const int MaxDisplayNameLength = 100;
    private const int MaxPageSize = 200;

    private readonly IUserRepository _users;
    private readonly IClassRepository _classes;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public StudentManagementUseCase(
        IUserRepository users, IClassRepository classes, IPasswordHasher hasher, IUnitOfWork unitOfWork, IClock clock)
    {
        _users = users;
        _classes = classes;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<StudentListDto> ListAsync(
        CurrentUser user, string? search, int? classId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, MaxPageSize);
        var (items, total) = await _users.SearchStudentsAsync(search?.Trim(), classId, page, pageSize, cancellationToken);
        var classesOf = await _classes.GetClassesOfStudentsAsync(items.Select(i => i.Id), cancellationToken);
        var now = _clock.UtcNow;
        var students = items.Select(u => ToDto(u, classesOf.GetValueOrDefault(u.Id) ?? Array.Empty<ClassRef>(), now)).ToList();
        return new StudentListDto(students, total, page, pageSize);
    }

    public async Task<CreateStudentResultDto> CreateAsync(CurrentUser user, CreateStudentRequest request, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var errors = new FieldErrors();
        var username = PasswordPolicy.NormalizeUsername(request.Username);
        if (!PasswordPolicy.IsValidUsername(username))
        {
            errors.Add("username", "Tên đăng nhập dài 3–50 ký tự, chỉ gồm chữ thường không dấu, số và các ký tự . _ -");
        }

        var displayName = errors.Text("displayName", "Họ tên", request.DisplayName, MaxDisplayNameLength);
        var classIds = (request.ClassIds ?? Array.Empty<int>()).Distinct().ToList();
        string? supplied = null;
        if (!string.IsNullOrEmpty(request.Password))
        {
            var policyError = PasswordPolicy.Validate(request.Password, username);
            if (policyError is not null)
            {
                errors.Add("password", policyError);
            }
            else
            {
                supplied = request.Password;
            }
        }

        errors.ThrowIfAny();
        await EnsureClassesExistAsync(classIds, cancellationToken);

        if (await _users.UsernameExistsAsync(username, cancellationToken))
        {
            throw new ConflictException("DUPLICATE_USERNAME", "Tên đăng nhập đã tồn tại.");
        }

        var password = supplied ?? TemporaryPasswordGenerator.Generate();
        var student = NewStudent(username, displayName!, _hasher.Hash(password));
        _users.Add(student);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (classIds.Count > 0)
        {
            await _classes.ReplaceMembershipsAsync(student.Id, classIds, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new CreateStudentResultDto(student.Id, student.Username, student.DisplayName, classIds, supplied is null ? password : null);
    }

    public async Task<ImportResultDto> ImportAsync(CurrentUser user, ImportRequest request, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var rows = request.Rows ?? Array.Empty<ImportRowDto>();
        if (rows.Count == 0)
        {
            throw new ValidationFailedException("rows", "Danh sách trống, hãy nhập ít nhất một dòng.");
        }

        if (rows.Count > MaxImportRows)
        {
            throw new ValidationFailedException("rows", $"Mỗi lần nhập tối đa {MaxImportRows} dòng.");
        }

        var classIds = new List<int>();
        if (request.ClassId is { } classId)
        {
            classIds.Add(classId);
            await EnsureClassesExistAsync(classIds, cancellationToken);
        }

        var normalized = rows.Select(r => PasswordPolicy.NormalizeUsername(r?.Username)).ToList();
        var existing = await _users.GetExistingUsernamesAsync(normalized.Where(PasswordPolicy.IsValidUsername), cancellationToken);

        var results = new ImportRowResultDto?[rows.Count];
        var pending = new List<(int Index, string Username, string DisplayName, string Password)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < rows.Count; i++)
        {
            var username = normalized[i];
            var errors = new FieldErrors();
            var displayName = errors.Text("displayName", "Họ tên", rows[i]?.DisplayName, MaxDisplayNameLength);
            if (!PasswordPolicy.IsValidUsername(username))
            {
                results[i] = new ImportRowResultDto(i + 1, "invalid", "Tên đăng nhập không hợp lệ (3–50 ký tự: chữ thường, số, . _ -).", username, null);
            }
            else if (displayName is null)
            {
                results[i] = new ImportRowResultDto(i + 1, "invalid", "Họ tên không hợp lệ (1–100 ký tự).", username, null);
            }
            else if (existing.Contains(username))
            {
                results[i] = new ImportRowResultDto(i + 1, "skipped", "Tên đăng nhập đã tồn tại.", username, null);
            }
            else if (!seen.Add(username))
            {
                results[i] = new ImportRowResultDto(i + 1, "skipped", "Trùng tên đăng nhập trong danh sách.", username, null);
            }
            else
            {
                pending.Add((i, username, displayName, TemporaryPasswordGenerator.Generate()));
            }
        }

        // Băm mật khẩu song song vì PBKDF2 cố ý chậm (tối đa 200 dòng).
        var hashes = new string[pending.Count];
        Parallel.For(0, pending.Count, i => hashes[i] = _hasher.Hash(pending[i].Password));

        var created = new List<User>();
        for (var i = 0; i < pending.Count; i++)
        {
            var (index, username, displayName, password) = pending[i];
            var student = NewStudent(username, displayName, hashes[i]);
            _users.Add(student);
            created.Add(student);
            results[index] = new ImportRowResultDto(index + 1, "created", null, username, password);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (classIds.Count > 0)
        {
            foreach (var student in created)
            {
                await _classes.ReplaceMembershipsAsync(student.Id, classIds, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ImportResultDto(results.Select(r => r!).ToList());
    }

    public async Task<ResetPasswordResultDto> ResetPasswordAsync(CurrentUser user, int studentId, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var student = await LoadStudentAsync(studentId, cancellationToken);
        var password = TemporaryPasswordGenerator.Generate();
        student.PasswordHash = _hasher.Hash(password);
        student.MustChangePassword = true;
        student.ClearLockout();
        student.RotateSecurityStamp();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ResetPasswordResultDto(password);
    }

    public async Task<StudentDto> UpdateAsync(CurrentUser user, int studentId, UpdateStudentRequest request, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var student = await LoadStudentAsync(studentId, cancellationToken);
        var errors = new FieldErrors();
        string? displayName = null;
        if (request.DisplayName is not null)
        {
            displayName = errors.Text("displayName", "Họ tên", request.DisplayName, MaxDisplayNameLength);
        }

        var classIds = request.ClassIds?.Distinct().ToList();
        errors.ThrowIfAny();
        if (classIds is not null)
        {
            await EnsureClassesExistAsync(classIds, cancellationToken);
        }

        if (displayName is not null)
        {
            student.DisplayName = displayName;
        }

        if (request.IsActive is { } active && active != student.IsActive)
        {
            student.IsActive = active;
            student.RotateSecurityStamp();
        }

        if (classIds is not null)
        {
            await _classes.ReplaceMembershipsAsync(student.Id, classIds, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var classesOf = await _classes.GetClassesOfStudentsAsync(new[] { student.Id }, cancellationToken);
        return ToDto(student, classesOf.GetValueOrDefault(student.Id) ?? Array.Empty<ClassRef>(), _clock.UtcNow);
    }

    public async Task UnlockAsync(CurrentUser user, int studentId, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var student = await LoadStudentAsync(studentId, cancellationToken);
        student.ClearLockout();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Id không tồn tại -> 404; Id của giáo viên -> 403 (không được tác động tới tài khoản giáo viên).</summary>
    private async Task<User> LoadStudentAsync(int id, CancellationToken cancellationToken)
    {
        var target = await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy học sinh.");
        if (target.Role != Roles.Student)
        {
            throw new ForbiddenException("Không được thay đổi tài khoản giáo viên qua chức năng quản lý học sinh.");
        }

        return target;
    }

    private async Task EnsureClassesExistAsync(IReadOnlyCollection<int> classIds, CancellationToken cancellationToken)
    {
        if (classIds.Count == 0)
        {
            return;
        }

        var found = await _classes.GetExistingAsync(classIds, cancellationToken);
        if (found.Count != classIds.Count)
        {
            throw new ValidationFailedException("classIds", "Có lớp không tồn tại.");
        }
    }

    private User NewStudent(string username, string displayName, string passwordHash) => new()
    {
        Username = username,
        DisplayName = displayName,
        PasswordHash = passwordHash,
        Role = Roles.Student,
        IsActive = true,
        MustChangePassword = true,
        CreatedUtc = _clock.UtcNow,
    };

    private static StudentDto ToDto(User u, IReadOnlyList<ClassRef> classes, DateTime now) => new(
        u.Id,
        u.Username,
        u.DisplayName,
        classes.Select(c => c.Id).ToList(),
        classes.Select(c => c.Name).ToList(),
        u.IsActive,
        u.IsLockedOut(now),
        u.MustChangePassword,
        u.LastLoginUtc);
}
