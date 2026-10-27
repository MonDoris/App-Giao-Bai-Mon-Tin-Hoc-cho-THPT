using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Infrastructure.Persistence.Configurations;

internal sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(l => l.Order).IsRequired();
        builder.Property(l => l.Title).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.SummaryJson).HasColumnName("Summary").IsRequired();
        builder.Property(l => l.NotesJson).HasColumnName("Notes").IsRequired();
        builder.Property(l => l.ConclusionJson).HasColumnName("Conclusion").IsRequired();

        // Mặc định ở DB (dữ liệu tạo bằng SQL cũ): xuất bản = 1, lưu trữ = 0, Version = 1.
        // ValueGeneratedNever: luôn ghi giá trị thật của ứng dụng (bản nháp = false không bị thay bằng mặc định).
        builder.Property(l => l.IsPublished).IsRequired().HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(l => l.IsArchived).IsRequired().HasDefaultValue(false);
        builder.Property(l => l.CreatedByUserId);
        builder.Property(l => l.UpdatedUtc);
        builder.Property(l => l.Version).IsRequired().HasDefaultValue(1).ValueGeneratedNever().IsConcurrencyToken();

        builder.HasIndex(l => l.Order).IsUnique();

        builder.HasMany(l => l.Exercises)
            .WithOne(e => e.Lesson)
            .HasForeignKey(e => e.LessonId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.CreatedBy)
            .WithMany()
            .HasForeignKey(l => l.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
