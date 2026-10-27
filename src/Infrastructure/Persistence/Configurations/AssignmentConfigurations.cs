using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Infrastructure.Persistence.Configurations;

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd();
        builder.Property(a => a.ExerciseId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.AssignedToAll).IsRequired();
        builder.Property(a => a.StartAtUtc);
        builder.Property(a => a.DueAtUtc);
        builder.Property(a => a.Note).HasMaxLength(500);
        builder.Property(a => a.CreatedByUserId).IsRequired();
        builder.Property(a => a.CreatedUtc).IsRequired();
        builder.Property(a => a.CancelledUtc);
        builder.Ignore(a => a.IsActive);

        builder.HasIndex(a => a.ExerciseId);

        builder.HasOne(a => a.Exercise)
            .WithMany()
            .HasForeignKey(a => a.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.CreatedBy)
            .WithMany()
            .HasForeignKey(a => a.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(a => a.Classes)
            .WithOne(c => c.Assignment)
            .HasForeignKey(c => c.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AssignmentClassConfiguration : IEntityTypeConfiguration<AssignmentClass>
{
    public void Configure(EntityTypeBuilder<AssignmentClass> builder)
    {
        builder.ToTable("AssignmentClasses");
        builder.HasKey(c => new { c.AssignmentId, c.ClassId });
        builder.HasIndex(c => c.ClassId);
        builder.HasOne(c => c.SchoolClass)
            .WithMany()
            .HasForeignKey(c => c.ClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("Submissions", t =>
        {
            t.HasCheckConstraint("CK_Submissions_PassedTests", "[PassedTests] >= 0");
            t.HasCheckConstraint("CK_Submissions_TotalTests", "[TotalTests] >= 1");
            t.HasCheckConstraint("CK_Submissions_PassedLessEqTotal", "[PassedTests] <= [TotalTests]");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();
        builder.Property(s => s.ExerciseId).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Code).IsRequired();
        builder.Property(s => s.PassedTests).IsRequired();
        builder.Property(s => s.TotalTests).IsRequired();
        builder.Property(s => s.IsPassed).IsRequired();
        builder.Property(s => s.IsLate).IsRequired();
        builder.Property(s => s.ResultsJson);
        builder.Property(s => s.SubmittedUtc).IsRequired();

        builder.HasIndex(s => new { s.StudentId, s.ExerciseId, s.SubmittedUtc });
        builder.HasIndex(s => s.ExerciseId);
        builder.HasIndex(s => s.AssignmentId);

        builder.HasOne(s => s.Assignment).WithMany().HasForeignKey(s => s.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Exercise).WithMany().HasForeignKey(s => s.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SolutionViewConfiguration : IEntityTypeConfiguration<SolutionView>
{
    public void Configure(EntityTypeBuilder<SolutionView> builder)
    {
        builder.ToTable("SolutionViews");
        builder.HasKey(v => new { v.StudentId, v.ExerciseId });
        builder.Property(v => v.ExerciseId).HasMaxLength(100).IsRequired();
        builder.Property(v => v.FirstViewedUtc).IsRequired();
        builder.HasIndex(v => v.ExerciseId);

        builder.HasOne<User>().WithMany().HasForeignKey(v => v.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Exercise>().WithMany().HasForeignKey(v => v.ExerciseId).OnDelete(DeleteBehavior.Cascade);
    }
}
