using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Infrastructure.Persistence.Configurations;

internal sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises", t =>
            t.HasCheckConstraint("CK_Exercises_Difficulty", "[Difficulty] IN ('Easy', 'Medium', 'Hard')"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(e => e.LessonId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Difficulty).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Statement).IsRequired();
        builder.Property(e => e.InputFormat).IsRequired();
        builder.Property(e => e.OutputFormat).IsRequired();
        builder.Property(e => e.Constraints);
        builder.Property(e => e.ExamplesJson).HasColumnName("Examples").IsRequired();
        builder.Property(e => e.StarterCode).IsRequired();
        builder.Property(e => e.SolutionCode);
        builder.Property(e => e.SolutionExplanation);
        builder.Property(e => e.ConfirmEveryTime).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.TimeLimitMs).IsRequired().HasDefaultValue(5000);

        // Xem ghi chú ở LessonConfiguration.
        builder.Property(e => e.IsPublished).IsRequired().HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedUtc);
        builder.Property(e => e.Version).IsRequired().HasDefaultValue(1).ValueGeneratedNever().IsConcurrencyToken();

        builder.Ignore(e => e.HasSolution);

        builder.HasIndex(e => new { e.LessonId, e.Order }).IsUnique();

        builder.HasMany(e => e.TestCases)
            .WithOne(t => t.Exercise)
            .HasForeignKey(t => t.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
