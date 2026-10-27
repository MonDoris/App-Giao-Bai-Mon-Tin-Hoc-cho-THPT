using System.Net;
using System.Text.Json.Nodes;

namespace TinHoc12.Web.Tests;

/// <summary>Hiển thị theo giao bài (QD-26), nộp bài, IDOR và cô lập dữ liệu giữa học sinh (AC-129..140, 144).</summary>
public class StudentApiTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public StudentApiTests(AppFactory factory)
    {
        _factory = factory;
    }

    private static async Task<int> TotalTestsAsync(TestBrowser browser, string lessonId, string exerciseId)
    {
        var detail = await TestBrowser.JsonAsync(await browser.GetAsync("/api/lessons/" + lessonId));
        return detail["exercises"]!.AsArray().Single(e => e!["id"].Str() == exerciseId)!["testCases"]!.AsArray().Count;
    }

    private async Task<TestBrowser> StudentAsync(string name)
    {
        var browser = _factory.NewBrowser();
        await browser.LoginOkAsync(name, Accounts.StudentPassword);
        return browser;
    }

    [Fact]
    public async Task Lesson_list_follows_class_assignments_hs01_sees_6_exercises_hs03_sees_4()
    {
        var hs01 = await StudentAsync("hs01");
        var hs03 = await StudentAsync("hs03");

        var a = await TestBrowser.JsonAsync(await hs01.GetAsync("/api/lessons"));
        var b = await TestBrowser.JsonAsync(await hs03.GetAsync("/api/lessons"));

        Assert.Equal(new[] { 2, 2, 2 }, a["lessons"]!.AsArray().Select(l => l!["exerciseCount"].Int()).ToArray());
        Assert.Equal(new[] { 2, 2, 0 }, b["lessons"]!.AsArray().Select(l => l!["exerciseCount"].Int()).ToArray());
        Assert.Equal(3, b["lessons"]!.AsArray().Count);
        Assert.All(b["lessons"]!.AsArray(), l => Assert.Equal(0, l!["passedCount"].Int()));
    }

    [Fact]
    public async Task Unassigned_exercises_are_invisible_in_detail_solution_and_submission_with_404()
    {
        var hs03 = await StudentAsync("hs03");
        var hs01 = await StudentAsync("hs01");

        var detail3 = await TestBrowser.JsonAsync(await hs03.GetAsync("/api/lessons/bai-03-vong-lap"));
        Assert.Empty(detail3["exercises"]!.AsArray());
        var detail1 = await TestBrowser.JsonAsync(await hs01.GetAsync("/api/lessons/bai-03-vong-lap"));
        Assert.Equal(2, detail1["exercises"]!.AsArray().Count);
        Assert.NotNull(detail1["exercises"]![0]!["assignment"]);

        Assert.Equal(HttpStatusCode.NotFound, (await hs03.GetAsync("/api/exercises/bai-03-tong-1-den-n/solution")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await hs01.GetAsync("/api/exercises/bai-03-tong-1-den-n/solution")).StatusCode);
        var post = await hs03.PostAsync("/api/exercises/bai-03-tong-1-den-n/submissions", new { code = "print(1)", passedTests = 1, totalTests = 1 });
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task Lesson_detail_never_contains_solution_content_and_exposes_hidden_tests_flag_only()
    {
        var hs01 = await StudentAsync("hs01");
        var text = await (await hs01.GetAsync("/api/lessons/bai-01-nhap-xuat")).Content.ReadAsStringAsync();
        var solution = await TestBrowser.JsonAsync(await hs01.GetAsync("/api/exercises/bai-01-tong-hai-so/solution"));

        Assert.DoesNotContain("solutionCode", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("solutionExplanation", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(solution["code"].Str(), text);
        Assert.Contains("\"isHidden\":true", text);
        foreach (var url in new[] { "/api/me/assignments", "/api/lessons" })
        {
            var body = await (await hs01.GetAsync(url)).Content.ReadAsStringAsync();
            Assert.DoesNotContain("solutionCode", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("solutionExplanation", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(solution["code"].Str(), body);
        }
    }

    [Fact]
    public async Task Viewing_a_solution_is_recorded_once_and_reflected_in_has_viewed_solution()
    {
        var hs02 = await StudentAsync("hs02");
        await hs02.GetAsync("/api/exercises/bai-02-chan-le/solution");
        await hs02.GetAsync("/api/exercises/bai-02-chan-le/solution");

        var mine = await TestBrowser.JsonAsync(await hs02.GetAsync("/api/me/assignments"));
        var row = mine["assignments"]!.AsArray().Single(a => a!["exerciseId"].Str() == "bai-02-chan-le")!;
        Assert.True(row["hasViewedSolution"]!.GetValue<bool>());
        var other = mine["assignments"]!.AsArray().Single(a => a!["exerciseId"].Str() == "bai-02-xep-loai")!;
        Assert.False(other["hasViewedSolution"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Submitting_stores_the_attempt_and_the_state_comes_from_the_server()
    {
        var hs01 = await StudentAsync("hs01");
        var detail = await TestBrowser.JsonAsync(await hs01.GetAsync("/api/lessons/bai-01-nhap-xuat"));
        var total = detail["exercises"]![0]!["testCases"]!.AsArray().Count;

        var response = await hs01.PostAsync("/api/exercises/bai-01-tong-hai-so/submissions", new
        {
            code = "a = int(input())\nprint(a)",
            passedTests = total,
            totalTests = total,
            results = Enumerable.Range(0, total).Select(_ => new { status = "Passed", timeMs = 3 }).ToArray(),
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await TestBrowser.JsonAsync(response);
        Assert.True(created["isPassed"]!.GetValue<bool>());
        Assert.False(created["isLate"]!.GetValue<bool>());
        Assert.Equal(1, created["attemptNo"].Int());
        Assert.Equal("Passed", created["status"].Str());

        var mine = await TestBrowser.JsonAsync(await hs01.GetAsync("/api/me/assignments"));
        var row = mine["assignments"]!.AsArray().Single(a => a!["exerciseId"].Str() == "bai-01-tong-hai-so")!;
        Assert.Equal("Passed", row["status"].Str());
        Assert.Equal(1, row["attempts"].Int());
        var lessons = await TestBrowser.JsonAsync(await hs01.GetAsync("/api/lessons"));
        Assert.Equal(1, lessons["lessons"]![0]!["passedCount"].Int());
        var history = await TestBrowser.JsonAsync(await hs01.GetAsync("/api/me/exercises/bai-01-tong-hai-so/submissions"));
        Assert.Single(history["submissions"]!.AsArray());
    }

    [Theory]
    [InlineData(99, 5)]
    [InlineData(-1, 5)]
    [InlineData(1, 0)]
    public async Task Invalid_numbers_give_422(int passed, int total)
    {
        var hs02 = await StudentAsync("hs02");
        _factory.Clock.Advance(TimeSpan.FromSeconds(10));
        var response = await hs02.PostAsync("/api/exercises/bai-01-dien-tich-hcn/submissions", new { code = "print(1)", passedTests = passed, totalTests = total });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await TestBrowser.JsonAsync(response))["error"].Str());
    }

    [Fact]
    public async Task Wrong_total_is_409_exercise_changed_empty_code_422_and_oversized_body_413()
    {
        var hs02 = await StudentAsync("hs02");
        _factory.Clock.Advance(TimeSpan.FromSeconds(10));
        var changed = await hs02.PostAsync("/api/exercises/bai-01-dien-tich-hcn/submissions", new { code = "print(1)", passedTests = 3, totalTests = 3 });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal("EXERCISE_CHANGED", (await TestBrowser.JsonAsync(changed))["error"].Str());

        var empty = await hs02.PostAsync("/api/exercises/bai-01-dien-tich-hcn/submissions", new { code = "   ", passedTests = 1, totalTests = 5 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, empty.StatusCode);

        var big = await hs02.PostAsync("/api/exercises/bai-01-dien-tich-hcn/submissions", "{\"code\":\"" + new string('x', 120_000) + "\",\"passedTests\":1,\"totalTests\":5}");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, big.StatusCode);
        var tooLong = await hs02.PostAsync("/api/exercises/bai-01-dien-tich-hcn/submissions", new { code = new string('x', 20_001), passedTests = 1, totalTests = 5 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLong.StatusCode);
    }

    [Fact]
    public async Task Ten_rapid_submissions_create_exactly_one_record()
    {
        var hs03 = await StudentAsync("hs03");
        _factory.Clock.Advance(TimeSpan.FromSeconds(10));
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 10; i++)
        {
            statuses.Add((await hs03.PostAsync("/api/exercises/bai-02-chan-le/submissions", new { code = "print(1)", passedTests = 1, totalTests = 6 })).StatusCode);
        }

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(9, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
        var history = await TestBrowser.JsonAsync(await hs03.GetAsync("/api/me/exercises/bai-02-chan-le/submissions"));
        Assert.Single(history["submissions"]!.AsArray());
    }

    [Fact]
    public async Task Students_cannot_read_each_others_submissions_or_codes()
    {
        var hs01 = await StudentAsync("hs01");
        var hs02 = await StudentAsync("hs02");
        _factory.Clock.Advance(TimeSpan.FromSeconds(10));
        var secret = "print('bi-mat-cua-hs02')";
        var made = await hs02.PostAsync("/api/exercises/bai-02-xep-loai/submissions", new { code = secret, passedTests = 1, totalTests = await TotalTestsAsync(hs02, "bai-02-re-nhanh", "bai-02-xep-loai") });
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        var id = (await TestBrowser.JsonAsync(made))["submissionId"].Int();

        Assert.Equal(HttpStatusCode.OK, (await hs02.GetAsync($"/api/me/submissions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await hs01.GetAsync($"/api/me/submissions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await hs01.GetAsync("/api/me/submissions/99999999")).StatusCode);

        foreach (var url in new[] { "/api/me/assignments", "/api/me/exercises/bai-02-xep-loai/submissions", "/api/lessons", "/api/lessons/bai-02-re-nhanh" })
        {
            var body = await (await hs01.GetAsync(url)).Content.ReadAsStringAsync();
            Assert.DoesNotContain("bi-mat-cua-hs02", body);
            Assert.DoesNotContain("hs02", body);
            Assert.DoesNotContain("studentId", body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await hs01.GetAsync("/api/teacher/progress?lessonId=bai-02-re-nhanh")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hs01.GetAsync($"/api/teacher/progress/students/{id}/exercises/bai-02-xep-loai/submissions")).StatusCode);
    }

    [Fact]
    public async Task Student_cannot_escalate_privileges_through_any_api()
    {
        var hs01 = await StudentAsync("hs01");
        foreach (var (method, url, body) in new (string, string, object?)[]
        {
            ("POST", "/api/teacher/students", new { username = "x-hack", displayName = "x", role = "Teacher" }),
            ("PATCH", "/api/teacher/students/1", new { role = "Teacher", isActive = true }),
            ("POST", "/api/teacher/students/1/reset-password", null),
            ("PUT", "/api/teacher/classes/1", new { name = "x" }),
            ("POST", "/api/teacher/assignments", new { }),
        })
        {
            var response = await hs01.SendAsync(new HttpMethod(method), url, body);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        var me = await TestBrowser.JsonAsync(await hs01.GetAsync("/api/auth/me"));
        Assert.Equal("Student", me["role"].Str());
    }
}

/// <summary>Luồng chính của giáo viên và học sinh từ đầu đến cuối qua API, kèm XSS, xung đột và vô hiệu hóa phiên.</summary>
public class TeacherFlowTests
{
    private static object LessonBody(string title, string text = "Nội dung") => new
    {
        title,
        description = "Mô tả " + title,
        summary = new[] { text },
        notes = new[] { "Lưu ý 1" },
        conclusion = new[] { "Tổng kết" },
    };

    private static object ExerciseBody(string title, int version = 0, int tests = 3, string statement = "Đề bài") => new
    {
        title,
        difficulty = "Easy",
        statement,
        inputFormat = "Một số",
        outputFormat = "Một số",
        constraints = (string?)null,
        examples = new[] { new { input = "1", output = "1", explanation = "vì 1" } },
        starterCode = "# code",
        solutionCode = "print(input())",
        solutionExplanation = "In lại số",
        confirmEveryTime = false,
        timeLimitMs = 5000,
        testCases = Enumerable.Range(1, tests).Select(i => new { input = i.ToString(), expectedOutput = i.ToString(), isHidden = i == tests }).ToArray(),
        version = version == 0 ? (int?)null : version,
    };

    [Fact]
    public async Task Due_date_is_required_and_students_only_see_the_exercise_from_the_start_date()
    {
        using var factory = new AppFactory();
        var teacher = factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var lesson = await TestBrowser.JsonAsync(await teacher.PostAsync("/api/teacher/lessons", LessonBody("Bài Hẹn Giờ")));
        var lessonId = lesson["id"].Str();
        var exerciseId = (await TestBrowser.JsonAsync(await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises", ExerciseBody("Mở sau"))))["id"].Str();
        await teacher.PatchAsync($"/api/teacher/lessons/{lessonId}/publish", new { isPublished = true });
        await teacher.PatchAsync($"/api/teacher/exercises/{exerciseId}/publish", new { isPublished = true });
        var target = new { all = true, classIds = Array.Empty<int>() };

        // Thiếu hạn nộp, hoặc hạn nộp không sau ngày bắt đầu: 422 trên trường dueAt
        var start = factory.Clock.UtcNow.AddHours(1);
        foreach (var body in new object[] { new { exerciseIds = new[] { exerciseId }, target }, new { exerciseIds = new[] { exerciseId }, target, startAt = start, dueAt = start } })
        {
            var bad = await teacher.PostAsync("/api/teacher/assignments", body);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
            Assert.Equal("dueAt", (await TestBrowser.JsonAsync(bad))["field"].Str());
        }

        var assigned = await TestBrowser.JsonAsync(await teacher.PostAsync("/api/teacher/assignments", new { exerciseIds = new[] { exerciseId }, target, startAt = start, dueAt = start.AddDays(1) }));
        Assert.Equal(start, assigned["assignments"]![0]!["startAt"]!.GetValue<DateTime>().ToUniversalTime());

        // Trước ngày bắt đầu: học sinh không thấy và không nộp được
        var hs = factory.NewBrowser();
        await hs.LoginOkAsync("hs01", Accounts.StudentPassword);
        var submission = new { code = "print(1)", passedTests = 3, totalTests = 3 };
        Assert.DoesNotContain((await TestBrowser.JsonAsync(await hs.GetAsync("/api/me/assignments")))["assignments"]!.AsArray(), r => r!["exerciseId"].Str() == exerciseId);
        Assert.NotEqual(HttpStatusCode.Created, (await hs.PostAsync($"/api/exercises/{exerciseId}/submissions", submission)).StatusCode);

        // Từ ngày bắt đầu: thấy và nộp được
        factory.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Contains((await TestBrowser.JsonAsync(await hs.GetAsync("/api/me/assignments")))["assignments"]!.AsArray(), r => r!["exerciseId"].Str() == exerciseId);
        Assert.Equal(HttpStatusCode.Created, (await hs.PostAsync($"/api/exercises/{exerciseId}/submissions", submission)).StatusCode);
    }

    [Fact]
    public async Task Full_flow_author_publish_assign_submit_and_see_progress()
    {
        using var factory = new AppFactory();
        var teacher = factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);

        // Lớp + học sinh mới (mật khẩu tạm, buộc đổi mật khẩu)
        var cls = await TestBrowser.JsonAsync(await teacher.PostAsync("/api/teacher/classes", new { name = "12B1" }));
        var student = await TestStudents.CreateAsync(teacher, "em.moi", "Em Mới", null, new[] { cls["id"].Int() });
        Assert.NotNull(student.TemporaryPassword);

        var em = factory.NewBrowser();
        var login = await TestBrowser.JsonAsync(await em.LoginAsync("em.moi", student.TemporaryPassword!));
        Assert.True(login["user"]!["mustChangePassword"]!.GetValue<bool>());
        Assert.Equal("/change-password.html", login["redirectUrl"].Str());
        var gated = await em.GetAsync("/api/lessons");
        Assert.Equal(HttpStatusCode.Forbidden, gated.StatusCode);
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", (await TestBrowser.JsonAsync(gated))["error"].Str());
        Assert.Equal(HttpStatusCode.OK, (await em.GetAsync("/api/auth/me")).StatusCode);
        var weak = await em.PostAsync("/api/auth/change-password", new { currentPassword = student.TemporaryPassword, newPassword = "yeu" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);
        Assert.Equal("newPassword", (await TestBrowser.JsonAsync(weak))["field"].Str());
        var wrongCurrent = await em.PostAsync("/api/auth/change-password", new { currentPassword = "Khong-dung-1", newPassword = "MatKhauMoi2026" });
        Assert.Equal("currentPassword", (await TestBrowser.JsonAsync(wrongCurrent))["field"].Str());
        Assert.Equal(HttpStatusCode.NoContent, (await em.PostAsync("/api/auth/change-password", new { currentPassword = student.TemporaryPassword, newPassword = "MatKhauMoi2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await em.GetAsync("/api/lessons")).StatusCode); // vẫn đăng nhập sau khi đổi (cookie cấp lại)

        // Soạn bài + bài tập (bản nháp), học sinh chưa thấy
        var lesson = await TestBrowser.JsonAsync(await teacher.PostAsync("/api/teacher/lessons", LessonBody("Bài Mới Của Cô")));
        var lessonId = lesson["id"].Str();
        Assert.Equal("bai-moi-cua-co", lessonId);
        Assert.False(lesson["isPublished"]!.GetValue<bool>());
        var exercise = await TestBrowser.JsonAsync(await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises", ExerciseBody("Đọc số")));
        var exerciseId = exercise["id"].Str();
        Assert.Equal("bai-moi-cua-co-doc-so", exerciseId);
        Assert.Equal(HttpStatusCode.NotFound, (await em.GetAsync($"/api/lessons/{lessonId}")).StatusCode);

        // Bài tập chưa xuất bản không giao được
        var early = await teacher.PostAsync("/api/teacher/assignments", new { exerciseIds = new[] { exerciseId }, target = new { all = false, classIds = new[] { cls["id"].Int() } }, dueAt = factory.Clock.UtcNow.AddDays(7) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, early.StatusCode);
        Assert.Equal("EXERCISE_NOT_PUBLISHED", (await TestBrowser.JsonAsync(early))["error"].Str());

        // Xuất bản và giao cho lớp với hạn nộp
        Assert.Equal(HttpStatusCode.OK, (await teacher.PatchAsync($"/api/teacher/lessons/{lessonId}/publish", new { isPublished = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.PatchAsync($"/api/teacher/exercises/{exerciseId}/publish", new { isPublished = true })).StatusCode);
        var due = factory.Clock.UtcNow.AddHours(2);
        var assigned = await teacher.PostAsync("/api/teacher/assignments", new { exerciseIds = new[] { exerciseId }, target = new { all = false, classIds = new[] { cls["id"].Int() } }, dueAt = due, note = "Làm trước giờ học" });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        var assignedJson = await TestBrowser.JsonAsync(assigned);
        Assert.Equal(1, assignedJson["studentCount"].Int());
        var assignmentId = assignedJson["assignments"]![0]!["id"].Int();

        var mine = await TestBrowser.JsonAsync(await em.GetAsync("/api/me/assignments"));
        var row = mine["assignments"]!.AsArray().Single(r => r!["exerciseId"].Str() == exerciseId)!;
        Assert.Equal("Làm trước giờ học", row["note"].Str());
        Assert.Equal("NotStarted", row["status"].Str());
        Assert.False(row["isOverdue"]!.GetValue<bool>());
        Assert.Equal(1, (await TestBrowser.JsonAsync(await em.GetAsync("/api/lessons")))["lessons"]!.AsArray().Count(l => l!["id"].Str() == lessonId));

        // Học sinh nộp bài đúng hạn rồi nộp muộn
        var ok = await em.PostAsync($"/api/exercises/{exerciseId}/submissions", new { code = "print(input())", passedTests = 3, totalTests = 3 });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        factory.Clock.Advance(TimeSpan.FromHours(3));
        var late = await TestBrowser.JsonAsync(await em.PostAsync($"/api/exercises/{exerciseId}/submissions", new { code = "print(input())  # muộn", passedTests = 2, totalTests = 3 }));
        Assert.True(late["isLate"]!.GetValue<bool>());
        Assert.Equal(2, late["attemptNo"].Int());

        // Giáo viên thấy tiến độ + mã đã nộp (nguyên văn)
        var progress = await TestBrowser.JsonAsync(await teacher.GetAsync($"/api/teacher/progress?lessonId={lessonId}&classId={cls["id"].Int()}"));
        var cell = progress["rows"]![0]!["cells"]![0]!;
        Assert.Equal("Passed", cell["status"].Str());
        Assert.Equal(2, cell["attempts"].Int());
        Assert.Equal(3, cell["bestPassedTests"].Int());
        Assert.Equal("em.moi", progress["rows"]![0]!["username"].Str());
        Assert.Equal("12B1", progress["rows"]![0]!["className"].Str());
        var studentId = progress["rows"]![0]!["studentId"].Int();
        var subs = await TestBrowser.JsonAsync(await teacher.GetAsync($"/api/teacher/progress/students/{studentId}/exercises/{exerciseId}/submissions"));
        Assert.Equal(2, subs["submissions"]!.AsArray().Count);
        Assert.Contains("# muộn", subs["submissions"]![1]!["code"].Str());
        Assert.True(subs["submissions"]![1]!["isLate"]!.GetValue<bool>());

        // Danh sách giao bài có số liệu
        var list = await TestBrowser.JsonAsync(await teacher.GetAsync($"/api/teacher/assignments?lessonId={lessonId}"));
        var a = list["assignments"]![0]!;
        Assert.Equal(1, a["assignedStudents"].Int());
        Assert.Equal(1, a["submittedStudents"].Int());
        Assert.Equal(1, a["passedStudents"].Int());

        // Không xóa được bài đã có bài nộp (409 gợi ý lưu trữ); lưu trữ thì học sinh không thấy
        var del = await teacher.DeleteAsync($"/api/teacher/lessons/{lessonId}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        var delJson = await TestBrowser.JsonAsync(del);
        Assert.Equal("HAS_DEPENDENCIES", delJson["error"].Str());
        Assert.Contains("lưu trữ", delJson["message"].Str());
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.DeleteAsync($"/api/teacher/exercises/{exerciseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.DeleteAsync($"/api/teacher/classes/{cls["id"].Int()}")).StatusCode);

        // Hủy giao (đã có bài nộp -> hủy mềm): học sinh mất quyền, giáo viên vẫn thấy kết quả
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/teacher/assignments/{assignmentId}")).StatusCode);
        Assert.DoesNotContain((await TestBrowser.JsonAsync(await em.GetAsync("/api/me/assignments")))["assignments"]!.AsArray(), r => r!["exerciseId"].Str() == exerciseId);
        Assert.Equal(HttpStatusCode.NotFound, (await em.PostAsync($"/api/exercises/{exerciseId}/submissions", new { code = "print(1)", passedTests = 3, totalTests = 3 })).StatusCode);
        var afterCancel = await TestBrowser.JsonAsync(await teacher.GetAsync($"/api/teacher/progress/students/{studentId}/exercises/{exerciseId}/submissions"));
        Assert.Equal(2, afterCancel["submissions"]!.AsArray().Count);
        Assert.Equal("NotAssigned", (await TestBrowser.JsonAsync(await teacher.GetAsync($"/api/teacher/progress?lessonId={lessonId}")))["rows"]!.AsArray().First(r => r!["username"].Str() == "em.moi")!["cells"]![0]!["status"].Str());

        Assert.Equal(HttpStatusCode.OK, (await teacher.PatchAsync($"/api/teacher/lessons/{lessonId}/archive", new { isArchived = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await em.GetAsync($"/api/lessons/{lessonId}")).StatusCode);
        var teacherLessons = await TestBrowser.JsonAsync(await teacher.GetAsync("/api/teacher/lessons"));
        Assert.DoesNotContain(teacherLessons["lessons"]!.AsArray(), l => l!["id"].Str() == lessonId);
        var withArchived = await TestBrowser.JsonAsync(await teacher.GetAsync("/api/teacher/lessons?includeArchived=true"));
        Assert.Contains(withArchived["lessons"]!.AsArray(), l => l!["id"].Str() == lessonId);
    }

    [Fact]
    public async Task Html_and_script_content_is_stored_and_returned_verbatim()
    {
        using var factory = new AppFactory();
        var teacher = factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var xss = "<script>alert(1)</script><img src=x onerror=alert(1)>\"><svg onload=alert(1)>";

        var lesson = await TestBrowser.JsonAsync(await teacher.PostAsync("/api/teacher/lessons", new { title = xss, description = xss, summary = new[] { xss }, notes = new[] { xss }, conclusion = new[] { xss } }));
        var lessonId = lesson["id"].Str();
        var exercise = await TestBrowser.JsonAsync(await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises", ExerciseBody(xss, statement: xss)));
        await teacher.PatchAsync($"/api/teacher/lessons/{lessonId}/publish", new { isPublished = true });
        await teacher.PatchAsync($"/api/teacher/exercises/{exercise["id"].Str()}/publish", new { isPublished = true });
        await teacher.PostAsync("/api/teacher/assignments", new { exerciseIds = new[] { exercise["id"].Str() }, target = new { all = true, classIds = Array.Empty<int>() }, dueAt = factory.Clock.UtcNow.AddDays(7), note = xss });
        var name = await TestStudents.CreateAsync(teacher, "xss.hs", xss, "Matkhau2026");

        var hs = factory.NewBrowser();
        await hs.LoginOkAsync("hs01", Accounts.StudentPassword);
        var detail = await TestBrowser.JsonAsync(await hs.GetAsync($"/api/lessons/{lessonId}"));
        Assert.Equal(xss, detail["title"].Str());
        Assert.Equal(xss, detail["notes"]![0].Str());
        Assert.Equal(xss, detail["exercises"]![0]!["statement"].Str());
        Assert.Equal(xss, detail["exercises"]![0]!["assignment"]!["note"].Str());
        var students = await TestBrowser.JsonAsync(await teacher.GetAsync("/api/teacher/students?search=xss.hs"));
        Assert.Equal(xss, students["students"]![0]!["displayName"].Str());
        Assert.True(name.Id > 0);
    }

    [Fact]
    public async Task Concurrent_edits_conflict_and_validation_errors_name_each_field()
    {
        using var factory = new AppFactory();
        var a = factory.NewBrowser();
        var b = factory.NewBrowser();
        await a.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        await b.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var lesson = await TestBrowser.JsonAsync(await a.PostAsync("/api/teacher/lessons", LessonBody("Tranh Chấp")));
        var id = lesson["id"].Str();
        var version = lesson["version"].Int();

        var first = await a.PutAsync($"/api/teacher/lessons/{id}", new { title = "Bản của A", description = "m", summary = new[] { "s" }, notes = new[] { "n" }, conclusion = new[] { "c" }, version });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await b.PutAsync($"/api/teacher/lessons/{id}", new { title = "Bản của B", description = "m", summary = new[] { "s" }, notes = new[] { "n" }, conclusion = new[] { "c" }, version });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("CONFLICT", (await TestBrowser.JsonAsync(second))["error"].Str());
        Assert.Equal("Bản của A", (await TestBrowser.JsonAsync(await b.GetAsync($"/api/teacher/lessons/{id}")))["title"].Str());

        var bad = await a.PostAsync("/api/teacher/lessons", new { title = "", description = new string('d', 501), summary = Array.Empty<string>(), notes = new[] { "ok", "" }, conclusion = new[] { "c" } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
        var fields = (await TestBrowser.JsonAsync(bad))["errors"]!.AsArray().Select(e => e!["field"].Str()).ToArray();
        Assert.Contains("title", fields);
        Assert.Contains("description", fields);
        Assert.Contains("summary", fields);
        Assert.Contains("notes[1]", fields);

        var dupId = await a.PostAsync("/api/teacher/lessons", new { id, title = "Trùng mã", description = "m", summary = new[] { "s" }, notes = new[] { "n" }, conclusion = new[] { "c" } });
        Assert.Equal(HttpStatusCode.Conflict, dupId.StatusCode);
        Assert.Equal("DUPLICATE_ID", (await TestBrowser.JsonAsync(dupId))["error"].Str());
        Assert.Equal(HttpStatusCode.NotFound, (await a.PutAsync("/api/teacher/lessons/khong-co", new { title = "x", version = 1 })).StatusCode);
    }

    [Fact]
    public async Task Exercise_rules_test_case_limits_reorder_and_duplicates()
    {
        using var factory = new AppFactory();
        var teacher = factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var lessonId = (await TestBrowser.JsonAsync(await teacher.PostAsync("/api/teacher/lessons", LessonBody("Bộ Test"))))["id"].Str();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises", ExerciseBody("Ít test", tests: 2))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises", ExerciseBody("Nhiều test", tests: 51))).StatusCode);
        var a = await TestBrowser.JsonAsync(await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises", ExerciseBody("Bài A", tests: 50)));
        var b = await TestBrowser.JsonAsync(await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises", ExerciseBody("Bài B")));
        Assert.Equal(50, a["testCases"]!.AsArray().Count);

        var dup = await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises", new { id = a["id"].Str(), title = "x" });
        Assert.True(dup.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity);

        var shrink = await teacher.PutAsync($"/api/teacher/exercises/{a["id"].Str()}", ExerciseBody("Bài A rút gọn", a["version"].Int(), tests: 4));
        Assert.Equal(HttpStatusCode.OK, shrink.StatusCode);
        Assert.Equal(4, (await TestBrowser.JsonAsync(shrink))["testCases"]!.AsArray().Count);

        var reorder = await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises/reorder", new { ids = new[] { b["id"].Str(), a["id"].Str() } });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);
        var order = (await TestBrowser.JsonAsync(await teacher.GetAsync($"/api/teacher/lessons/{lessonId}")))["exercises"]!.AsArray().Select(e => e!["id"].Str()).ToArray();
        Assert.Equal(new[] { b["id"].Str(), a["id"].Str() }, order);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises/reorder", new { ids = new[] { a["id"].Str() } })).StatusCode);

        // Bài tập không có đáp án: nút Đáp án ẩn, endpoint 404 với học sinh
        var plain = await TestBrowser.JsonAsync(await teacher.PostAsync($"/api/teacher/lessons/{lessonId}/exercises", new
        {
            title = "Không đáp án", difficulty = "Hard", statement = "s", inputFormat = "i", outputFormat = "o",
            examples = new[] { new { input = "1", output = "1" } }, starterCode = "", solutionCode = (string?)null,
            testCases = new[] { new { input = "1", expectedOutput = "1", isHidden = false }, new { input = "2", expectedOutput = "2", isHidden = false }, new { input = "3", expectedOutput = "3", isHidden = true } },
        }));
        await teacher.PatchAsync($"/api/teacher/lessons/{lessonId}/publish", new { isPublished = true });
        await teacher.PatchAsync($"/api/teacher/exercises/{plain["id"].Str()}/publish", new { isPublished = true });
        await teacher.PostAsync("/api/teacher/assignments", new { exerciseIds = new[] { plain["id"].Str() }, target = new { all = true, classIds = Array.Empty<int>() }, dueAt = factory.Clock.UtcNow.AddDays(7) });
        var hs = factory.NewBrowser();
        await hs.LoginOkAsync("hs02", Accounts.StudentPassword);
        var detail = await TestBrowser.JsonAsync(await hs.GetAsync($"/api/lessons/{lessonId}"));
        Assert.False(detail["exercises"]![0]!["hasSolution"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NotFound, (await hs.GetAsync($"/api/exercises/{plain["id"].Str()}/solution")).StatusCode);
    }

    [Fact]
    public async Task Disabling_or_resetting_a_student_revokes_the_session_on_the_next_request()
    {
        using var factory = new AppFactory();
        var teacher = factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var created = await TestStudents.CreateAsync(teacher, "se.bi.khoa", "Em Bị Khóa", "Matkhau2026");
        var other = await TestStudents.CreateAsync(teacher, "se.bi.reset", "Em Bị Reset", "Matkhau2026");
        var s1 = factory.NewBrowser();
        var s2 = factory.NewBrowser();
        await s1.LoginOkAsync("se.bi.khoa", "Matkhau2026");
        await s2.LoginOkAsync("se.bi.reset", "Matkhau2026");
        Assert.Equal(HttpStatusCode.OK, (await s1.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await teacher.PatchAsync($"/api/teacher/students/{created.Id}", new { isActive = false })).StatusCode);
        var reset = await TestBrowser.JsonAsync(await teacher.PostAsync($"/api/teacher/students/{other.Id}/reset-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, (await s1.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await s2.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.NewBrowser().LoginAsync("se.bi.khoa", "Matkhau2026")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.NewBrowser().LoginAsync("se.bi.reset", "Matkhau2026")).StatusCode);
        var relogin = await TestBrowser.JsonAsync(await factory.NewBrowser().LoginAsync("se.bi.reset", reset["temporaryPassword"].Str()));
        Assert.True(relogin["user"]!["mustChangePassword"]!.GetValue<bool>());

        Assert.Equal(HttpStatusCode.OK, (await teacher.PatchAsync($"/api/teacher/students/{created.Id}", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.NewBrowser().LoginAsync("se.bi.khoa", "Matkhau2026")).StatusCode);
    }

    [Fact]
    public async Task Unlock_and_student_class_changes_take_effect_immediately()
    {
        using var factory = new AppFactory();
        var teacher = factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var bro = await TestStudents.CreateAsync(teacher, "tam.khoa", "Em Tạm Khóa", "Matkhau2026");
        for (var i = 0; i < 5; i++)
        {
            await factory.NewBrowser().LoginAsync("tam.khoa", "Sai" + i + "xxxxxx");
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.NewBrowser().LoginAsync("tam.khoa", "Matkhau2026")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync($"/api/teacher/students/{bro.Id}/unlock")).StatusCode);
        var em = factory.NewBrowser();
        Assert.Equal(HttpStatusCode.OK, (await em.LoginAsync("tam.khoa", "Matkhau2026")).StatusCode);
        await em.PostAsync("/api/auth/change-password", new { currentPassword = "Matkhau2026", newPassword = "Matkhau2027" });

        var classes = await TestBrowser.JsonAsync(await teacher.GetAsync("/api/teacher/classes"));
        var c12a1 = classes["classes"]!.AsArray().Single(c => c!["name"].Str() == "12A1")!["id"].Int();
        Assert.Equal(0, (await TestBrowser.JsonAsync(await em.GetAsync("/api/lessons")))["lessons"]![2]!["exerciseCount"].Int());
        await teacher.PatchAsync($"/api/teacher/students/{bro.Id}", new { classIds = new[] { c12a1 } });
        Assert.Equal(2, (await TestBrowser.JsonAsync(await em.GetAsync("/api/lessons")))["lessons"]![2]!["exerciseCount"].Int());
    }
}
