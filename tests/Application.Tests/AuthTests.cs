using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Security;
using TinHoc12.Application.UseCases.Auth;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Tests;

public class LoginUseCaseTests
{
    private static (World W, LoginUseCase UseCase) Create()
    {
        var w = new World();
        w.AddUser("giaovien", Roles.Teacher, "GiaoVien@2026");
        w.AddUser("hs01", Roles.Student, "HocSinh@2026");
        return (w, new LoginUseCase(w.UserRepo, w.Hasher, w.UnitOfWork, w.Clock, w.Options));
    }

    [Fact]
    public async Task Correct_login_returns_role_based_redirect_and_no_secrets()
    {
        var (_, useCase) = Create();
        var teacher = await useCase.ExecuteAsync(new LoginRequest("GiaoVien ", "GiaoVien@2026"));
        var student = await useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026"));

        Assert.Equal("/teacher/dashboard.html", teacher.RedirectUrl);
        Assert.Equal("/dashboard.html", student.RedirectUrl);
        Assert.Equal("Teacher", teacher.User.Role);
        var json = System.Text.Json.JsonSerializer.Serialize(new LoginResultDto(student.User, student.RedirectUrl));
        Assert.DoesNotContain("GiaoVien", json);
        Assert.DoesNotContain("h:", json);
        Assert.DoesNotContain(student.SecurityStamp, json);
    }

    [Fact]
    public async Task Valid_return_url_is_used_but_unsafe_or_forbidden_ones_are_ignored()
    {
        var (_, useCase) = Create();
        Assert.Equal("/problem.html?lesson=a&problem=b", (await useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026", "/problem.html?lesson=a&problem=b"))).RedirectUrl);
        foreach (var bad in new[] { "//evil.com", "https://evil.com", "/\\evil.com", "javascript:alert(1)", "/teacher/index.html", "data:text/html,x", "/a\r\nb", "/", "/api/lessons" })
        {
            Assert.Equal("/dashboard.html", (await useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026", bad))).RedirectUrl);
        }

        Assert.Equal("/teacher/progress.html", (await useCase.ExecuteAsync(new LoginRequest("giaovien", "GiaoVien@2026", "/teacher/progress.html"))).RedirectUrl);
        Assert.Equal("/dashboard.html", (await useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026", "/" + new string('a', 400)))).RedirectUrl);
    }

    [Fact]
    public async Task Must_change_password_redirects_to_change_password_page()
    {
        var (w, useCase) = Create();
        w.Users.First(u => u.Username == "hs01").MustChangePassword = true;
        var ticket = await useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026", "/lesson.html?id=x"));
        Assert.Equal("/change-password.html", ticket.RedirectUrl);
        Assert.True(ticket.User.MustChangePassword);
    }

    [Fact]
    public async Task Unknown_wrong_locked_and_disabled_all_give_the_same_generic_error()
    {
        var (w, useCase) = Create();
        w.AddUser("khoa", Roles.Student, "HocSinh@2026", active: false);
        w.AddUser("tamkhoa", Roles.Student, "HocSinh@2026").LockoutEndUtc = w.Clock.UtcNow.AddMinutes(5);

        var cases = new[] { ("khong-ton-tai", "x1234567"), ("hs01", "SaiMatKhau1"), ("khoa", "HocSinh@2026"), ("tamkhoa", "HocSinh@2026") };
        var messages = new List<string>();
        foreach (var (user, pass) in cases)
        {
            var ex = await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync(new LoginRequest(user, pass)));
            messages.Add(ex.Message);
        }

        Assert.All(messages, m => Assert.Equal(InvalidCredentialsException.GenericMessage, m));
    }

    [Fact]
    public async Task Unknown_username_still_performs_one_hash_verification()
    {
        var (w, useCase) = Create();
        var before = w.Hasher.VerifyCalls;
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync(new LoginRequest("khong-co", "abcdefg1")));
        Assert.Equal(before + 1, w.Hasher.VerifyCalls);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_15_minutes_even_for_the_correct_password()
    {
        var (w, useCase) = Create();
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync(new LoginRequest("hs01", "Sai" + i + "xxxxx")));
        }

        var user = w.Users.First(u => u.Username == "hs01");
        Assert.NotNull(user.LockoutEndUtc);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026")));
        w.Clock.Advance(TimeSpan.FromMinutes(14));
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026")));
        w.Clock.Advance(TimeSpan.FromMinutes(2));
        var ticket = await useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026"));
        Assert.Equal("hs01", ticket.User.Username);
        Assert.Null(user.LockoutEndUtc);
    }

    [Fact]
    public async Task Successful_login_before_threshold_resets_the_failure_counter()
    {
        var (w, useCase) = Create();
        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync(new LoginRequest("hs01", "Sai" + i + "xxxxx")));
        }

        await useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026"));
        Assert.Equal(0, w.Users.First(u => u.Username == "hs01").FailedLoginCount);

        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync(new LoginRequest("hs01", "Sai" + i + "xxxxx")));
        }

        Assert.NotNull(await useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026")));
    }

    [Fact]
    public async Task Threshold_and_duration_come_from_options()
    {
        var (w, useCase) = Create();
        w.Options.MaxFailedAttempts = 2;
        w.Options.LockoutMinutes = 1;
        for (var i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync(new LoginRequest("hs01", "Sai" + i + "xxxxx")));
        }

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026")));
        w.Clock.Advance(TimeSpan.FromSeconds(61));
        Assert.NotNull(await useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026")));
    }

    [Fact]
    public async Task Weak_hash_is_upgraded_on_successful_login()
    {
        var (w, useCase) = Create();
        var user = w.Users.First(u => u.Username == "hs01");
        user.PasswordHash = "old:HocSinh@2026";
        await useCase.ExecuteAsync(new LoginRequest("hs01", "HocSinh@2026"));
        Assert.Equal("h:HocSinh@2026", user.PasswordHash);
    }

    [Theory]
    [InlineData("", "abc12345")]
    [InlineData("   ", "abc12345")]
    [InlineData("hs01", "")]
    [InlineData("hs01", "     ")]
    [InlineData(null, null)]
    public async Task Empty_input_is_rejected_with_field_errors_before_hashing(string? user, string? pass)
    {
        var (w, useCase) = Create();
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ExecuteAsync(new LoginRequest(user, pass)));
        Assert.NotEmpty(ex.Errors);
        Assert.Equal(0, w.Hasher.VerifyCalls);
    }

    [Fact]
    public async Task Overlong_input_is_rejected_before_hashing_and_sql_text_is_just_wrong_credentials()
    {
        var (w, useCase) = Create();
        var ex1 = await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ExecuteAsync(new LoginRequest(new string('a', 51), "abc12345")));
        Assert.Equal("username", ex1.Errors[0].Field);
        var ex2 = await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ExecuteAsync(new LoginRequest("hs01", new string('a', 129))));
        Assert.Equal("password", ex2.Errors[0].Field);
        Assert.Equal(0, w.Hasher.VerifyCalls);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync(new LoginRequest("' OR 1=1--", "' OR '1'='1")));
    }
}

public class SessionAndPasswordTests
{
    [Fact]
    public async Task Session_is_valid_only_while_active_and_stamp_matches()
    {
        var w = new World();
        var user = w.AddUser("hs01");
        var session = new SessionUseCase(w.UserRepo, w.UnitOfWork);

        Assert.NotNull(await session.ValidateAsync(user.Id, user.SecurityStamp));
        Assert.Null(await session.ValidateAsync(user.Id, "stamp-cu"));
        Assert.Null(await session.ValidateAsync(user.Id, null));
        Assert.Null(await session.ValidateAsync(999, user.SecurityStamp));

        user.IsActive = false;
        Assert.Null(await session.ValidateAsync(user.Id, user.SecurityStamp));
    }

    [Fact]
    public async Task Logout_rotates_stamp_and_is_idempotent_for_unknown_users()
    {
        var w = new World();
        var user = w.AddUser("hs01");
        var oldStamp = user.SecurityStamp;
        var session = new SessionUseCase(w.UserRepo, w.UnitOfWork);

        await session.LogoutAsync(user.Id);
        await session.LogoutAsync(12345);

        Assert.NotEqual(oldStamp, user.SecurityStamp);
        Assert.Null(await session.ValidateAsync(user.Id, oldStamp));
    }

    [Theory]
    [InlineData("Abcdef12", true)]
    [InlineData("matkhau2026", true)]
    [InlineData("short1A", false)]
    [InlineData("khongcoso", false)]
    [InlineData("12345678", false)]
    [InlineData("", false)]
    public void Password_policy_requires_length_letter_and_digit(string password, bool ok)
    {
        Assert.Equal(ok, PasswordPolicy.Validate(password, "hs01") is null);
    }

    [Fact]
    public void Password_policy_limits_length_and_rejects_username_and_keeps_spaces()
    {
        Assert.NotNull(PasswordPolicy.Validate(new string('a', 129) + "1", "x"));
        Assert.Null(PasswordPolicy.Validate(new string('a', 127) + "1", "x"));
        Assert.NotNull(PasswordPolicy.Validate("hs0123456", "HS0123456"));
        Assert.Null(PasswordPolicy.Validate("  abc 1234  ", "x"));
    }

    [Theory]
    [InlineData("hs01", true)]
    [InlineData("12a1.nguyen-van_a", true)]
    [InlineData("ab", false)]
    [InlineData("co dau", false)]
    [InlineData("Hoa", false)]
    [InlineData("tiếng", false)]
    public void Username_rules(string username, bool ok)
    {
        Assert.Equal(ok, PasswordPolicy.IsValidUsername(username));
    }

    [Fact]
    public void Temporary_passwords_follow_the_policy_and_avoid_confusing_characters()
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < 500; i++)
        {
            var p = TemporaryPasswordGenerator.Generate();
            seen.Add(p);
            Assert.Equal(10, p.Length);
            Assert.Null(PasswordPolicy.Validate(p, "hs01"));
            Assert.DoesNotContain(p, c => "0O1lI".Contains(c));
        }

        Assert.True(seen.Count > 490);
    }

    private static (World W, User User, ChangePasswordUseCase UseCase) Create()
    {
        var w = new World();
        var user = w.AddUser("hs01", password: "Cu12345678");
        user.MustChangePassword = true;
        return (w, user, new ChangePasswordUseCase(w.UserRepo, w.Hasher, w.UnitOfWork, w.Clock, w.Options));
    }

    [Fact]
    public async Task Change_password_succeeds_clears_flag_and_rotates_stamp()
    {
        var (_, user, useCase) = Create();
        var oldStamp = user.SecurityStamp;

        var ticket = await useCase.ExecuteAsync(World.AsStudent(user), new ChangePasswordRequest("Cu12345678", "Moi87654321"));

        Assert.Equal("h:Moi87654321", user.PasswordHash);
        Assert.False(user.MustChangePassword);
        Assert.NotEqual(oldStamp, user.SecurityStamp);
        Assert.Equal(user.SecurityStamp, ticket.SecurityStamp);
        Assert.Equal("/dashboard.html", ticket.RedirectUrl);
    }

    [Fact]
    public async Task Wrong_current_password_is_a_field_error_and_counts_as_a_failed_attempt()
    {
        var (w, user, useCase) = Create();
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ExecuteAsync(World.AsStudent(user), new ChangePasswordRequest("SaiRoi123", "Moi87654321")));
        Assert.Equal("currentPassword", ex.Errors[0].Field);
        Assert.Equal(1, user.FailedLoginCount);

        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ExecuteAsync(World.AsStudent(user), new ChangePasswordRequest("SaiRoi123", "Moi87654321")));
        }

        Assert.True(user.IsLockedOut(w.Clock.UtcNow));
    }

    [Theory]
    [InlineData("ngan1")]
    [InlineData("khongcoso")]
    [InlineData("Cu12345678")]
    [InlineData("hs01")]
    public async Task Weak_or_same_new_password_is_rejected_on_new_password_field(string newPassword)
    {
        var (_, user, useCase) = Create();
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => useCase.ExecuteAsync(World.AsStudent(user), new ChangePasswordRequest("Cu12345678", newPassword)));
        Assert.Equal("newPassword", ex.Errors[0].Field);
        Assert.Equal("h:Cu12345678", user.PasswordHash);
    }

    [Fact]
    public async Task Default_password_detection_returns_only_usernames()
    {
        var w = new World();
        w.AddUser("giaovien", Roles.Teacher, "GiaoVien@2026");
        w.AddUser("hs01", Roles.Student, "DaDoiMatKhau1");
        var found = await new DetectDefaultPasswordsUseCase(w.UserRepo, w.Hasher).ExecuteAsync();
        Assert.Equal(new[] { "giaovien" }, found);
    }
}

public class SubmissionThrottleTests
{
    [Fact]
    public void Only_one_submission_per_interval_per_student()
    {
        var throttle = new TinHoc12.Application.UseCases.Student.SubmissionThrottle();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var interval = TimeSpan.FromSeconds(3);

        Assert.True(throttle.TryAcquire(1, t0, interval));
        Assert.False(throttle.TryAcquire(1, t0.AddSeconds(1), interval));
        Assert.True(throttle.TryAcquire(2, t0.AddSeconds(1), interval));
        Assert.True(throttle.TryAcquire(1, t0.AddSeconds(3), interval));
    }

    [Fact]
    public void Concurrent_requests_yield_exactly_one_winner()
    {
        var throttle = new TinHoc12.Application.UseCases.Student.SubmissionThrottle();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var wins = 0;
        Parallel.For(0, 50, _ =>
        {
            if (throttle.TryAcquire(7, t0, TimeSpan.FromSeconds(3)))
            {
                Interlocked.Increment(ref wins);
            }
        });
        Assert.Equal(1, wins);
    }
}
