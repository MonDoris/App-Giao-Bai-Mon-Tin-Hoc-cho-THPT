using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t =>
            t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Teacher', 'Student')"));
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedOnAdd();
        builder.Property(u => u.Username).HasMaxLength(50).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(10).IsRequired();
        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(u => u.MustChangePassword).IsRequired().HasDefaultValue(false);
        builder.Property(u => u.FailedLoginCount).IsRequired().HasDefaultValue(0);
        builder.Property(u => u.LockoutEndUtc);
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(u => u.LastLoginUtc);
        builder.Property(u => u.CreatedUtc).IsRequired();

        builder.HasIndex(u => u.Username).IsUnique();

        builder.HasMany(u => u.ClassMemberships)
            .WithOne(m => m.Student)
            .HasForeignKey(m => m.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
