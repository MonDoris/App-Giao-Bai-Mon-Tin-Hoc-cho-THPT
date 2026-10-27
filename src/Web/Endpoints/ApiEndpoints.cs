using TinHoc12.Web.Middleware;

namespace TinHoc12.Web.Endpoints;

public static class ApiEndpoints
{
    /// <summary>Đăng ký toàn bộ endpoint /api. Endpoint chỉ gọi use case của Application và trả DTO.</summary>
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapContentEndpoints();
        app.MapTeacherEndpoints();

        // Mọi đường dẫn /api/... khác (kể cả ID rỗng) -> 401 nếu chưa đăng nhập, ngược lại 404 JSON thay vì rơi xuống trang tĩnh.
        app.MapGroup("/api").RequireAuthorization()
            .MapFallback("{**rest}", () => ApiErrors.NotFound("Không tìm thấy tài nguyên."));

        return app;
    }
}
