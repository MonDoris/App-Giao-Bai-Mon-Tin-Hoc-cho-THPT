using TinHoc12.Application.Dtos;
using TinHoc12.Application.UseCases;
using TinHoc12.Application.UseCases.Student;
using TinHoc12.Web.Middleware;
using TinHoc12.Web.Security;

namespace TinHoc12.Web.Endpoints;

/// <summary>Nội dung dùng chung (cần đăng nhập, lọc theo role) và các endpoint dành riêng cho học sinh.</summary>
public static class ContentEndpoints
{
    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/lessons", async (HttpContext http, GetLessonsUseCase useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(http.GetCurrentUser(), ct)));

        api.MapGet("/lessons/{id}", async (string id, HttpContext http, GetLessonDetailUseCase useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(http.GetCurrentUser(), id, ct)));

        api.MapGet("/exercises/{id}/solution", async (string id, HttpContext http, GetSolutionUseCase useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(http.GetCurrentUser(), id, ct)));

        var student = api.MapGroup(string.Empty).RequireAuthorization(AuthPolicies.StudentOnly);

        student.MapGet("/me/assignments", async (HttpContext http, GetMyAssignmentsUseCase useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(http.GetCurrentUser(), ct)));

        student.MapPost("/exercises/{id}/submissions", async (
            string id, SubmitRequest request, HttpContext http, SubmitSolutionUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(http.GetCurrentUser(), id, request, ct);
            return Results.Created($"/api/me/submissions/{result.SubmissionId}", result);
        });

        student.MapGet("/me/exercises/{id}/submissions", async (string id, HttpContext http, GetMySubmissionsUseCase useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ListAsync(http.GetCurrentUser(), id, ct)));

        student.MapGet("/me/submissions/{id:long}", async (long id, HttpContext http, GetMySubmissionsUseCase useCase, CancellationToken ct) =>
            Results.Ok(await useCase.GetAsync(http.GetCurrentUser(), id, ct)));

        // Mọi đường dẫn /api/me/... khác: giáo viên 403, học sinh 404 (sau khi xác thực).
        api.MapFallback("/me/{**rest}", () => ApiErrors.NotFound("Không tìm thấy tài nguyên."))
            .RequireAuthorization(AuthPolicies.StudentOnly);

        return app;
    }
}

public static class AuthPolicies
{
    public const string TeacherOnly = "TeacherOnly";
    public const string StudentOnly = "StudentOnly";
}
