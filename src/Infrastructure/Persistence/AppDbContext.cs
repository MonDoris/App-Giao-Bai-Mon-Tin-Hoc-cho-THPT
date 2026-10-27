using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<ClassMember> ClassMembers => Set<ClassMember>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentClass> AssignmentClasses => Set<AssignmentClass>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SolutionView> SolutionViews => Set<SolutionView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        if (Database.IsSqlite())
        {
            // Chỉ phục vụ kiểm thử (SQLite in-memory), không dùng khi chạy ứng dụng; gỡ khi test chuyển sang SQL Server.
            // SQL Server mặc định không phân biệt hoa/thường; SQLite thì có, nên đặt NOCASE để cùng hành vi.
            modelBuilder.Entity<User>().Property(u => u.Username).UseCollation("NOCASE");
            modelBuilder.Entity<SchoolClass>().Property(c => c.Name).UseCollation("NOCASE");
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Mọi thời điểm lưu theo UTC và đọc ra là DateTimeKind.Utc (JSON có hậu tố Z).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }

    private sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcDateTimeConverter()
            : base(
                v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
        {
        }
    }
}
