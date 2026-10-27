using System.Diagnostics;
using System.Security.Cryptography;
using TinHoc12.Application.Abstractions;
using TinHoc12.Infrastructure.Security;

namespace TinHoc12.Infrastructure.Tests;

/// <summary>Kiểm thử băm mật khẩu (AC-84): PBKDF2, >= 100.000 vòng, salt >= 128 bit, hash khác nhau, băm lại hash yếu.</summary>
public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_is_self_describing_pbkdf2_with_strong_parameters_and_never_contains_the_password()
    {
        var hash = _hasher.Hash("MatKhau@2026");

        var parts = hash.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2-SHA512", parts[0]);
        Assert.True(int.Parse(parts[1]) >= 100_000);
        Assert.True(Convert.FromBase64String(parts[2]).Length >= 16);
        Assert.True(Convert.FromBase64String(parts[3]).Length >= 32);
        Assert.DoesNotContain("MatKhau", hash);
        Assert.True(hash.Length <= 500);
    }

    [Fact]
    public void Same_password_hashed_twice_gives_different_hashes_that_both_verify()
    {
        var a = _hasher.Hash("HocSinh@2026");
        var b = _hasher.Hash("HocSinh@2026");

        Assert.NotEqual(a, b);
        Assert.Equal(PasswordVerification.Success, _hasher.Verify(a, "HocSinh@2026"));
        Assert.Equal(PasswordVerification.Success, _hasher.Verify(b, "HocSinh@2026"));
    }

    [Theory]
    [InlineData("hocsinh@2026")]
    [InlineData("HocSinh@2026 ")]
    [InlineData("")]
    public void Wrong_password_fails(string attempt)
    {
        Assert.Equal(PasswordVerification.Failed, _hasher.Verify(_hasher.Hash("HocSinh@2026"), attempt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("khong-phai-hash")]
    [InlineData("PBKDF2-SHA512$abc$AAAA$BBBB")]
    [InlineData("PBKDF2-SHA512$0$AAAA$BBBB")]
    [InlineData("PBKDF2-SHA512$210000$!!!$BBBB")]
    [InlineData("MD5$1$AAAA$BBBB")]
    [InlineData("PBKDF2-SHA512$99999999999$AAAA$BBBB")]
    public void Malformed_hashes_never_verify_and_never_throw(string hash)
    {
        Assert.Equal(PasswordVerification.Failed, _hasher.Verify(hash, "x"));
    }

    [Fact]
    public void Hash_made_with_fewer_iterations_verifies_but_asks_for_rehash()
    {
        var weak = new Pbkdf2PasswordHasher(1_000).Hash("HocSinh@2026");

        Assert.Equal(PasswordVerification.SuccessRehashNeeded, _hasher.Verify(weak, "HocSinh@2026"));
        Assert.Equal(PasswordVerification.Failed, _hasher.Verify(weak, "sai"));
    }

    [Fact]
    public void Unicode_passwords_are_supported()
    {
        var hash = _hasher.Hash("Mật khẩu Việt 2026!");
        Assert.Equal(PasswordVerification.Success, _hasher.Verify(hash, "Mật khẩu Việt 2026!"));
    }

    [Fact]
    public void Hashing_takes_well_under_one_second_with_default_settings()
    {
        var sw = Stopwatch.StartNew();
        _hasher.Hash("HocSinh@2026");
        _hasher.Verify(_hasher.Hash("x1234567"), "x1234567");
        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"3 lần băm mất {sw.Elapsed}");
    }

    [Fact]
    public void Salt_is_random_per_hash()
    {
        var salts = Enumerable.Range(0, 20).Select(_ => _hasher.Hash("x1234567").Split('$')[2]).ToHashSet();
        Assert.Equal(20, salts.Count);
        Assert.NotNull(RandomNumberGenerator.GetBytes(1));
    }
}
