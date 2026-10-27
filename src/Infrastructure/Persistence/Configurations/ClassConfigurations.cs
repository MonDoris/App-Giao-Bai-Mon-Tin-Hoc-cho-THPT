using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Infrastructure.Persistence.Configurations;

internal sealed class SchoolClassConfiguration : IEntityTypeConfiguration<SchoolClass>
{
    public void Configure(EntityTypeBuilder<SchoolClass> builder)
    {
        builder.ToTable("Classes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreatedUtc).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();

        builder.HasMany(c => c.Members)
            .WithOne(m => m.SchoolClass)
            .HasForeignKey(m => m.ClassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ClassMemberConfiguration : IEntityTypeConfiguration<ClassMember>
{
    public void Configure(EntityTypeBuilder<ClassMember> builder)
    {
        builder.ToTable("ClassMembers");
        builder.HasKey(m => new { m.ClassId, m.StudentId });
        builder.HasIndex(m => m.StudentId);
    }
}
