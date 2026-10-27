using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Rules;
using TinHoc12.Application.Security;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.UseCases.Students;

/// <summary>Quản lý lớp (AC-108): tên 1–100 ký tự, duy nhất không phân biệt hoa/thường; xóa lớp không xóa học sinh.</summary>
public sealed class ClassManagementUseCase
{
    private readonly IClassRepository _classes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ClassManagementUseCase(IClassRepository classes, IUnitOfWork unitOfWork, IClock clock)
    {
        _classes = classes;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<ClassListDto> ListAsync(CurrentUser user, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var rows = await _classes.ListAsync(cancellationToken);
        return new ClassListDto(rows.Select(r => new ClassDto(r.Id, r.Name, r.MemberCount)).ToList());
    }

    public async Task<ClassDto> CreateAsync(CurrentUser user, ClassInputDto input, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var name = ValidateName(input);
        if (await _classes.NameExistsAsync(name, null, cancellationToken))
        {
            throw new ConflictException("CONFLICT", "Tên lớp đã tồn tại.");
        }

        var schoolClass = new SchoolClass { Name = name, CreatedUtc = _clock.UtcNow };
        _classes.Add(schoolClass);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ClassDto(schoolClass.Id, schoolClass.Name, 0);
    }

    public async Task<ClassDto> UpdateAsync(CurrentUser user, int id, ClassInputDto input, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var schoolClass = await _classes.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lớp.");
        var name = ValidateName(input);
        if (await _classes.NameExistsAsync(name, id, cancellationToken))
        {
            throw new ConflictException("CONFLICT", "Tên lớp đã tồn tại.");
        }

        schoolClass.Name = name;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ClassDto(schoolClass.Id, schoolClass.Name, schoolClass.Members.Count);
    }

    public async Task DeleteAsync(CurrentUser user, int id, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var schoolClass = await _classes.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lớp.");
        if (await _classes.IsTargetOfActiveAssignmentAsync(id, cancellationToken))
        {
            throw new ConflictException(
                "HAS_DEPENDENCIES", "Lớp đang là đối tượng của bài được giao. Hãy hủy hoặc đổi đối tượng giao bài trước khi xóa lớp.");
        }

        await _classes.RemoveAsync(schoolClass, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string ValidateName(ClassInputDto input)
    {
        var errors = new FieldErrors();
        var name = errors.Text("name", "Tên lớp", input.Name, 100);
        errors.ThrowIfAny();
        return name!;
    }
}
