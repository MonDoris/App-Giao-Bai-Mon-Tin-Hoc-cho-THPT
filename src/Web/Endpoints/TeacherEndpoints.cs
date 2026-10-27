using TinHoc12.Application.Dtos;
using TinHoc12.Application.UseCases.Assignments;
using TinHoc12.Application.UseCases.Authoring;
using TinHoc12.Application.UseCases.Progress;
using TinHoc12.Application.UseCases.Students;
using TinHoc12.Web.Middleware;
using TinHoc12.Web.Security;

namespace TinHoc12.Web.Endpoints;

/// <summary>Toàn bộ API dành cho giáo viên: /api/teacher/** (học sinh gọi nhận 403).</summary>
public static class TeacherEndpoints
{
    public static IEndpointRouteBuilder MapTeacherEndpoints(this IEndpointRouteBuilder app)
    {
        var teacher = app.MapGroup("/api/teacher").RequireAuthorization(AuthPolicies.TeacherOnly);

        MapLessons(teacher);
        MapExercises(teacher);
        MapAssignments(teacher);
        MapProgress(teacher);
        MapStudents(teacher);
        MapClasses(teacher);

        // Mọi đường dẫn /api/teacher/... khác (kể cả phương thức không hỗ trợ như DELETE học sinh): học sinh 403, giáo viên 404.
        teacher.MapFallback("{**rest}", () => ApiErrors.NotFound("Không tìm thấy tài nguyên."));
        return app;
    }

    private static void MapLessons(RouteGroupBuilder g)
    {
        g.MapGet("/lessons", async (bool? includeArchived, HttpContext http, LessonAuthoringUseCase u, CancellationToken ct) =>
            Results.Ok(await u.ListAsync(http.GetCurrentUser(), includeArchived ?? false, ct)));

        g.MapPost("/lessons", async (LessonInputDto input, HttpContext http, LessonAuthoringUseCase u, CancellationToken ct) =>
        {
            var lesson = await u.CreateAsync(http.GetCurrentUser(), input, ct);
            return Results.Created($"/api/teacher/lessons/{lesson.Id}", lesson);
        });

        g.MapPost("/lessons/reorder", async (ReorderInputDto input, HttpContext http, LessonAuthoringUseCase u, CancellationToken ct) =>
        {
            await u.ReorderAsync(http.GetCurrentUser(), input.Ids, ct);
            return Results.NoContent();
        });

        g.MapGet("/lessons/{id}", async (string id, HttpContext http, LessonAuthoringUseCase u, CancellationToken ct) =>
            Results.Ok(await u.GetAsync(http.GetCurrentUser(), id, ct)));

        g.MapPut("/lessons/{id}", async (string id, LessonInputDto input, HttpContext http, LessonAuthoringUseCase u, CancellationToken ct) =>
            Results.Ok(await u.UpdateAsync(http.GetCurrentUser(), id, input, ct)));

        g.MapDelete("/lessons/{id}", async (string id, HttpContext http, LessonAuthoringUseCase u, CancellationToken ct) =>
        {
            await u.DeleteAsync(http.GetCurrentUser(), id, ct);
            return Results.NoContent();
        });

        g.MapPatch("/lessons/{id}/publish", async (string id, PublishInputDto input, HttpContext http, LessonAuthoringUseCase u, CancellationToken ct) =>
            Results.Ok(await u.SetPublishedAsync(http.GetCurrentUser(), id, input.IsPublished, ct)));

        g.MapPatch("/lessons/{id}/archive", async (string id, ArchiveInputDto input, HttpContext http, LessonAuthoringUseCase u, CancellationToken ct) =>
            Results.Ok(await u.SetArchivedAsync(http.GetCurrentUser(), id, input.IsArchived, ct)));
    }

    private static void MapExercises(RouteGroupBuilder g)
    {
        g.MapPost("/lessons/{lessonId}/exercises", async (
            string lessonId, ExerciseInputDto input, HttpContext http, ExerciseAuthoringUseCase u, CancellationToken ct) =>
        {
            var exercise = await u.CreateAsync(http.GetCurrentUser(), lessonId, input, ct);
            return Results.Created($"/api/teacher/exercises/{exercise.Id}", exercise);
        });

        g.MapPost("/lessons/{lessonId}/exercises/reorder", async (
            string lessonId, ReorderInputDto input, HttpContext http, ExerciseAuthoringUseCase u, CancellationToken ct) =>
        {
            await u.ReorderAsync(http.GetCurrentUser(), lessonId, input.Ids, ct);
            return Results.NoContent();
        });

        g.MapGet("/exercises/{id}", async (string id, HttpContext http, ExerciseAuthoringUseCase u, CancellationToken ct) =>
            Results.Ok(await u.GetAsync(http.GetCurrentUser(), id, ct)));

        g.MapPut("/exercises/{id}", async (string id, ExerciseInputDto input, HttpContext http, ExerciseAuthoringUseCase u, CancellationToken ct) =>
            Results.Ok(await u.UpdateAsync(http.GetCurrentUser(), id, input, ct)));

        g.MapDelete("/exercises/{id}", async (string id, HttpContext http, ExerciseAuthoringUseCase u, CancellationToken ct) =>
        {
            await u.DeleteAsync(http.GetCurrentUser(), id, ct);
            return Results.NoContent();
        });

        g.MapPatch("/exercises/{id}/publish", async (string id, PublishInputDto input, HttpContext http, ExerciseAuthoringUseCase u, CancellationToken ct) =>
            Results.Ok(await u.SetPublishedAsync(http.GetCurrentUser(), id, input.IsPublished, ct)));

        g.MapPatch("/exercises/{id}/archive", async (string id, ArchiveInputDto input, HttpContext http, ExerciseAuthoringUseCase u, CancellationToken ct) =>
            Results.Ok(await u.SetArchivedAsync(http.GetCurrentUser(), id, input.IsArchived, ct)));
    }

    private static void MapAssignments(RouteGroupBuilder g)
    {
        g.MapGet("/assignments", async (
            string? lessonId, int? classId, bool? includeCancelled, HttpContext http, AssignmentUseCase u, CancellationToken ct) =>
            Results.Ok(await u.ListAsync(http.GetCurrentUser(), lessonId, classId, includeCancelled ?? false, ct)));

        g.MapPost("/assignments", async (AssignInputDto input, HttpContext http, AssignmentUseCase u, CancellationToken ct) =>
            Results.Created("/api/teacher/assignments", await u.AssignAsync(http.GetCurrentUser(), input, ct)));

        g.MapPut("/assignments/{id:int}", async (int id, UpdateAssignmentInputDto input, HttpContext http, AssignmentUseCase u, CancellationToken ct) =>
            Results.Ok(await u.UpdateAsync(http.GetCurrentUser(), id, input, ct)));

        g.MapDelete("/assignments/{id:int}", async (int id, HttpContext http, AssignmentUseCase u, CancellationToken ct) =>
        {
            await u.DeleteAsync(http.GetCurrentUser(), id, ct);
            return Results.NoContent();
        });
    }

    private static void MapProgress(RouteGroupBuilder g)
    {
        g.MapGet("/progress", async (
            int? classId, string? lessonId, string? search, string? sort, int? page, int? pageSize,
            HttpContext http, ProgressUseCase u, CancellationToken ct) =>
            Results.Ok(await u.GetAsync(http.GetCurrentUser(), classId, lessonId, search, sort, page ?? 1, pageSize ?? 50, ct)));

        g.MapGet("/progress/students/{studentId:int}/exercises/{exerciseId}/submissions", async (
            int studentId, string exerciseId, HttpContext http, ProgressUseCase u, CancellationToken ct) =>
            Results.Ok(await u.GetStudentSubmissionsAsync(http.GetCurrentUser(), studentId, exerciseId, ct)));
    }

    private static void MapStudents(RouteGroupBuilder g)
    {
        g.MapGet("/students", async (
            string? search, int? classId, int? page, int? pageSize, HttpContext http, StudentManagementUseCase u, CancellationToken ct) =>
            Results.Ok(await u.ListAsync(http.GetCurrentUser(), search, classId, page ?? 1, pageSize ?? 50, ct)));

        g.MapPost("/students", async (CreateStudentRequest request, HttpContext http, StudentManagementUseCase u, CancellationToken ct) =>
        {
            var created = await u.CreateAsync(http.GetCurrentUser(), request, ct);
            return Results.Created($"/api/teacher/students/{created.Id}", created);
        });

        g.MapPost("/students/import", async (ImportRequest request, HttpContext http, StudentManagementUseCase u, CancellationToken ct) =>
            Results.Ok(await u.ImportAsync(http.GetCurrentUser(), request, ct)));

        g.MapPatch("/students/{id:int}", async (int id, UpdateStudentRequest request, HttpContext http, StudentManagementUseCase u, CancellationToken ct) =>
            Results.Ok(await u.UpdateAsync(http.GetCurrentUser(), id, request, ct)));

        g.MapPost("/students/{id:int}/reset-password", async (int id, HttpContext http, StudentManagementUseCase u, CancellationToken ct) =>
            Results.Ok(await u.ResetPasswordAsync(http.GetCurrentUser(), id, ct)));

        g.MapPost("/students/{id:int}/unlock", async (int id, HttpContext http, StudentManagementUseCase u, CancellationToken ct) =>
        {
            await u.UnlockAsync(http.GetCurrentUser(), id, ct);
            return Results.NoContent();
        });
    }

    private static void MapClasses(RouteGroupBuilder g)
    {
        g.MapGet("/classes", async (HttpContext http, ClassManagementUseCase u, CancellationToken ct) =>
            Results.Ok(await u.ListAsync(http.GetCurrentUser(), ct)));

        g.MapPost("/classes", async (ClassInputDto input, HttpContext http, ClassManagementUseCase u, CancellationToken ct) =>
        {
            var created = await u.CreateAsync(http.GetCurrentUser(), input, ct);
            return Results.Created($"/api/teacher/classes/{created.Id}", created);
        });

        g.MapPut("/classes/{id:int}", async (int id, ClassInputDto input, HttpContext http, ClassManagementUseCase u, CancellationToken ct) =>
            Results.Ok(await u.UpdateAsync(http.GetCurrentUser(), id, input, ct)));

        g.MapDelete("/classes/{id:int}", async (int id, HttpContext http, ClassManagementUseCase u, CancellationToken ct) =>
        {
            await u.DeleteAsync(http.GetCurrentUser(), id, ct);
            return Results.NoContent();
        });
    }
}
