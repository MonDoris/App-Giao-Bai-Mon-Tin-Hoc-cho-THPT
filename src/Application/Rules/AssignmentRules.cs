using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;

namespace TinHoc12.Application.Rules;

/// <summary>Quy tắc nghiệp vụ về assignment hiệu lực (QD-27) và trạng thái học sinh (AC-129..134, AC-139).</summary>
public static class AssignmentRules
{
    public const string NotStarted = "NotStarted";
    public const string InProgress = "InProgress";
    public const string Passed = "Passed";

    /// <summary>
    /// Với mỗi bài tập chọn assignment hiệu lực: hạn nộp sớm nhất (không hạn xếp sau), hòa thì Id nhỏ nhất.
    /// </summary>
    public static IReadOnlyDictionary<string, StudentAssignmentRow> PickEffective(IEnumerable<StudentAssignmentRow> rows)
    {
        return rows
            .GroupBy(r => r.ExerciseId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.DueAtUtc is null).ThenBy(r => r.DueAtUtc).ThenBy(r => r.AssignmentId).First());
    }

    public static string StatusOf(ExerciseStats? stats)
    {
        if (stats is null || stats.Attempts == 0)
        {
            return NotStarted;
        }

        return stats.AnyPassed ? Passed : InProgress;
    }

    public static AssignmentInfoDto BuildInfo(StudentAssignmentRow row, ExerciseStats? stats, bool viewedSolution, int totalTests, DateTime nowUtc)
    {
        var due = row.DueAtUtc;
        var passedOnTime = stats?.AnyPassedOnTime ?? false;
        var isOverdue = due is { } d && nowUtc > d && !passedOnTime;
        var passedLate = (stats?.AnyPassed ?? false) && !passedOnTime;
        return new AssignmentInfoDto(
            row.AssignmentId,
            due,
            row.Note,
            StatusOf(stats),
            isOverdue,
            stats?.Attempts ?? 0,
            stats?.BestPassedTests ?? 0,
            totalTests,
            viewedSolution,
            passedLate);
    }
}
