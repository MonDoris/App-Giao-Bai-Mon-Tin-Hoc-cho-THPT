using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Infrastructure.Persistence.Configurations;

internal sealed class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> builder)
    {
        builder.ToTable("TestCases");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();
        builder.Property(t => t.ExerciseId).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Order).IsRequired();
        builder.Property(t => t.Input).IsRequired();
        builder.Property(t => t.ExpectedOutput).IsRequired();
        builder.Property(t => t.IsHidden).IsRequired().HasDefaultValue(false);

        builder.HasIndex(t => new { t.ExerciseId, t.Order }).IsUnique();
    }
}
