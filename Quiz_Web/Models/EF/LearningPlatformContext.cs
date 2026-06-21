using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.Entities;
using File = Quiz_Web.Models.Entities.File;

namespace Quiz_Web.Models.EF;

public partial class LearningPlatformContext : DbContext
{
    public LearningPlatformContext()
    {
    }

    public LearningPlatformContext(DbContextOptions<LearningPlatformContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AttemptAnswer> AttemptAnswers { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Certificate> Certificates { get; set; }

    public virtual DbSet<ContentShare> ContentShares { get; set; }

    public virtual DbSet<ContentTag> ContentTags { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<CourseCategory> CourseCategories { get; set; }

    public virtual DbSet<CourseChapter> CourseChapters { get; set; }

    public virtual DbSet<CourseProgress> CourseProgresses { get; set; }

    public virtual DbSet<CoursePurchase> CoursePurchases { get; set; }

    public virtual DbSet<CourseReview> CourseReviews { get; set; }

    public virtual DbSet<ErrorLog> ErrorLogs { get; set; }

    public virtual DbSet<File> Files { get; set; }

    public virtual DbSet<Flashcard> Flashcards { get; set; }

    public virtual DbSet<FlashcardPracticeLog> FlashcardPracticeLogs { get; set; }

    public virtual DbSet<FlashcardSet> FlashcardSets { get; set; }

    public virtual DbSet<Folder> Folders { get; set; }

    public virtual DbSet<Lesson> Lessons { get; set; }

    public virtual DbSet<LessonContent> LessonContents { get; set; }

    public virtual DbSet<Library> Libraries { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<QuestionClozeBlank> QuestionClozeBlanks { get; set; }

    public virtual DbSet<QuestionOption> QuestionOptions { get; set; }

    public virtual DbSet<QuestionRangeAnswer> QuestionRangeAnswers { get; set; }

    public virtual DbSet<Reminder> Reminders { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SavedItem> SavedItems { get; set; }

    public virtual DbSet<ShoppingCart> ShoppingCarts { get; set; }

    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

    public virtual DbSet<UserSubscription> UserSubscriptions { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<Test> Tests { get; set; }

    public virtual DbSet<TestAttempt> TestAttempts { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserInterest> UserInterests { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }

//    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
//#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
//        => optionsBuilder.UseSqlServer("Server=DESKTOP-3Q3UNK4\\MSSQLSERVER01;Initial Catalog=LearningPlatform;Persist Security Info=True;User ID=solar;Password=@Abcd@1234;Encrypt=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AttemptAnswer>(entity =>
        {
            entity.HasKey(e => e.AttemptAnswerId).HasName("PK__AttemptA__EC6FE54E12C4C90E");

            entity.Property(e => e.Score).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.Attempt).WithMany(p => p.AttemptAnswers)
                .HasForeignKey(d => d.AttemptId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AAnswers_Attempt");

            entity.HasOne(d => d.Grader).WithMany(p => p.AttemptAnswers)
                .HasForeignKey(d => d.GraderId)
                .HasConstraintName("FK_AAnswers_Grader");

            entity.HasOne(d => d.Question).WithMany(p => p.AttemptAnswers)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AAnswers_Question");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditId).HasName("PK__AuditLog__A17F2398427591F1");

            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.EntityType).HasMaxLength(50);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_AuditLogs_User");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.CartItemId).HasName("PK__CartItem__488B0B0A3432F85C");

            entity.HasIndex(e => new { e.CartId, e.CourseId }, "UQ_CartItems_Cart_Course").IsUnique();

            entity.Property(e => e.AddedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItems_Cart");

            entity.HasOne(d => d.Course).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItems_Course");
        });

        modelBuilder.Entity<Certificate>(entity =>
        {
            entity.HasKey(e => e.CertId).HasName("PK__Certific__E5BD38C54777E926");

            entity.HasIndex(e => e.VerifyCode, "UQ_Certificates_Verify").IsUnique();

            entity.Property(e => e.IssuedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Serial).HasMaxLength(50);
            entity.Property(e => e.VerifyCode).HasMaxLength(50);

            entity.HasOne(d => d.Course).WithMany(p => p.Certificates)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("FK_Certificates_Course");

            entity.HasOne(d => d.User).WithMany(p => p.Certificates)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Certificates_User");
        });

        modelBuilder.Entity<ContentShare>(entity =>
        {
            entity.HasKey(e => e.ShareId).HasName("PK__ContentS__D32A3FEEB660997F");

            entity.Property(e => e.CanView).HasDefaultValue(true);
            entity.Property(e => e.ContentType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.TargetType)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ContentShares)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContentShares_Creator");
        });

        modelBuilder.Entity<ContentTag>(entity =>
        {
            entity.HasKey(e => e.ContentTagId).HasName("PK__ContentT__8FE574855212AE62");

            entity.Property(e => e.ContentType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Tag).WithMany(p => p.ContentTags)
                .HasForeignKey(d => d.TagId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContentTags_Tag");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseId).HasName("PK__Courses__C92D71A708E633CE");

            entity.HasIndex(e => e.Slug, "UQ_Courses_Slug").IsUnique();

            entity.Property(e => e.AverageRating).HasColumnType("decimal(3, 2)");
            entity.Property(e => e.CoverUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Price).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Slug).HasMaxLength(200);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Category).WithMany(p => p.Courses)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_Courses_Category");

            entity.HasOne(d => d.Owner).WithMany(p => p.Courses)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Courses_Owner");
        });

        modelBuilder.Entity<CourseCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__CourseCa__19093A0B182C995A");

            entity.HasIndex(e => e.Slug, "UQ__CourseCa__BC7B5FB6F93D6A58").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IconUrl).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Slug).HasMaxLength(200);
        });

        modelBuilder.Entity<CourseChapter>(entity =>
        {
            entity.HasKey(e => e.ChapterId).HasName("PK__CourseCh__0893A36A1A132FA9");

            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Course).WithMany(p => p.CourseChapters)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("FK_Chapters_Course");
        });

        modelBuilder.Entity<CourseProgress>(entity =>
        {
            entity.HasKey(e => e.ProgressId).HasName("PK__CoursePr__BAE29CA5857FEE59");

            entity.ToTable("CourseProgress");

            entity.Property(e => e.ContentType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.LastViewedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Score).HasColumnType("decimal(6, 2)");

            entity.HasOne(d => d.Course).WithMany(p => p.CourseProgresses)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("FK__CoursePro__Cours__3493CFA7");

            entity.HasOne(d => d.User).WithMany(p => p.CourseProgresses)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CoursePro__UserI__339FAB6E");
        });

        modelBuilder.Entity<CoursePurchase>(entity =>
        {
            entity.HasKey(e => e.PurchaseId).HasName("PK__CoursePu__6B0A6BBE3B207A0E");

            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.PricePaid).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.PurchasedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasIndex(e => new { e.BuyerId, e.CourseId, e.Status })
                .HasDatabaseName("IX_CoursePurchases_AccessCheck");

            entity.HasOne(d => d.Buyer).WithMany(p => p.CoursePurchases)
                .HasForeignKey(d => d.BuyerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CPurchases_Buyer");

            entity.HasOne(d => d.Course).WithMany(p => p.CoursePurchases)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("FK_CPurchases_Course");
        });

        modelBuilder.Entity<CourseReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PK__CourseRe__74BC79CE283AC994");

            entity.ToTable(tb => tb.HasTrigger("trg_UpdateCourseRating"));

            entity.HasIndex(e => e.CourseId, "IX_CourseReviews_Course");

            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsApproved).HasDefaultValue(true);
            entity.Property(e => e.Rating).HasColumnType("decimal(2, 1)");

            entity.HasOne(d => d.Course).WithMany(p => p.CourseReviews)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("FK__CourseRev__Cours__1CBC4616");

            entity.HasOne(d => d.User).WithMany(p => p.CourseReviews)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CourseRev__UserI__1DB06A4F");
        });

        modelBuilder.Entity<ErrorLog>(entity =>
        {
            entity.HasKey(e => e.ErrorId).HasName("PK__ErrorLog__35856A2A985924C4");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Message).HasMaxLength(4000);
            entity.Property(e => e.Severity)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<File>(entity =>
        {
            entity.HasKey(e => e.FileId).HasName("PK__Files__6F0F98BFEE73AFB7");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.MimeType).HasMaxLength(100);
            entity.Property(e => e.StoragePath).HasMaxLength(500);

            entity.HasOne(d => d.Owner).WithMany(p => p.Files)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Files_Owner");
        });

        modelBuilder.Entity<Flashcard>(entity =>
        {
            entity.HasKey(e => e.CardId).HasName("PK__Flashcar__55FECDAE22B679D4");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Hint).HasMaxLength(500);

            entity.HasOne(d => d.BackMedia).WithMany(p => p.FlashcardBackMedia)
                .HasForeignKey(d => d.BackMediaId)
                .HasConstraintName("FK_Flashcards_BackMedia");

            entity.HasOne(d => d.FrontMedia).WithMany(p => p.FlashcardFrontMedia)
                .HasForeignKey(d => d.FrontMediaId)
                .HasConstraintName("FK_Flashcards_FrontMedia");

            entity.HasOne(d => d.Set).WithMany(p => p.Flashcards)
                .HasForeignKey(d => d.SetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Flashcards_Set");
        });

        modelBuilder.Entity<FlashcardPracticeLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__Flashcar__5E5486486B3F9ECA");

            entity.Property(e => e.EaseFactor).HasColumnType("decimal(4, 2)");

            entity.HasOne(d => d.Card).WithMany(p => p.FlashcardPracticeLogs)
                .HasForeignKey(d => d.CardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FPL_Card");

            entity.HasOne(d => d.Set).WithMany(p => p.FlashcardPracticeLogs)
                .HasForeignKey(d => d.SetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FPL_Set");

            entity.HasOne(d => d.User).WithMany(p => p.FlashcardPracticeLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FPL_User");
        });

        modelBuilder.Entity<FlashcardSet>(entity =>
        {
            entity.HasKey(e => e.SetId).HasName("PK__Flashcar__7E08471D6D606900");

            entity.Property(e => e.CoverUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Language).HasMaxLength(20);
            entity.Property(e => e.TagsText).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Visibility)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Owner).WithMany(p => p.FlashcardSets)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FlashcardSets_Owner");
        });

        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(e => e.FolderId).HasName("PK__Folders__ACD7107F7D93F1ED");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Name).HasMaxLength(200);

            entity.HasOne(d => d.Library).WithMany(p => p.Folders)
                .HasForeignKey(d => d.LibraryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Folders_Library");

            entity.HasOne(d => d.ParentFolder).WithMany(p => p.InverseParentFolder)
                .HasForeignKey(d => d.ParentFolderId)
                .HasConstraintName("FK_Folders_Parent");
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.HasKey(e => e.LessonId).HasName("PK__Lessons__B084ACD022F804BF");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Visibility)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Course");

            entity.HasOne(d => d.Chapter).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.ChapterId)
                .HasConstraintName("FK_Lessons_Chapter");
        });

        modelBuilder.Entity<LessonContent>(entity =>
        {
            entity.HasKey(e => e.ContentId).HasName("PK__LessonCo__2907A81E0574D749");

            entity.HasIndex(e => new { e.LessonId, e.OrderIndex }, "IX_LessonContents_Lesson_Order");

            entity.Property(e => e.ContentType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.VideoUrl).HasMaxLength(500);

            entity.HasOne(d => d.Lesson).WithMany(p => p.LessonContents)
                .HasForeignKey(d => d.LessonId)
                .HasConstraintName("FK_LessonContents_Lesson");
        });

        modelBuilder.Entity<Library>(entity =>
        {
            entity.HasKey(e => e.LibraryId).HasName("PK__Librarie__A136475F60AB60B8");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Name).HasMaxLength(200);

            entity.HasOne(d => d.Owner).WithMany(p => p.Libraries)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Libraries_Owner");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E1246E72CAE");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Type)
                .HasMaxLength(40)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_User");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BCF2D965192");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Currency)
                .HasMaxLength(10)
                .HasDefaultValue("VND");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.Buyer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.BuyerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_User");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PK__OrderIte__727E838BEDC4FB4C");

            entity.Property(e => e.Price).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.Course).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItems_Course");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_OrderItems_Order");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A38D5990A46");

            entity.Property(e => e.Amount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.Provider)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.ProviderRef).HasMaxLength(200);
            entity.Property(e => e.Purpose)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(PaymentPurposes.Course);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TransactionId).HasMaxLength(200);

            entity.HasIndex(e => e.ProviderRef)
                .HasDatabaseName("IX_Payments_ProviderRef");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_Payments_Order");

            entity.HasOne(d => d.SubscriptionPlan).WithMany(p => p.Payments)
                .HasForeignKey(d => d.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Payments_SubscriptionPlan");
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("SubscriptionPlans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("PlanId");
            entity.Property(e => e.DurationInMonths).HasColumnName("DurationMonths");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Price).HasColumnType("decimal(12, 2)");
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.ToTable("UserSubscriptions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("SubscriptionId");
            entity.Property(e => e.StartDate).HasColumnName("SubscribedAt");
            entity.Property(e => e.EndDate).HasColumnName("ExpiresAt");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasIndex(e => new { e.UserId, e.Status, e.EndDate })
                .HasDatabaseName("IX_UserSubscriptions_AccessCheck");

            entity.HasOne(e => e.User).WithMany(e => e.UserSubscriptions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserSubscriptions_User");

            entity.HasOne(e => e.Plan).WithMany(e => e.UserSubscriptions)
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_UserSubscriptions_Plan");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("PK__Question__0DC06FACA15F1129");

            entity.Property(e => e.Points)
                .HasDefaultValue(1m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.StemMedia).WithMany(p => p.Questions)
                .HasForeignKey(d => d.StemMediaId)
                .HasConstraintName("FK_Questions_StemMedia");

            entity.HasOne(d => d.Test).WithMany(p => p.Questions)
                .HasForeignKey(d => d.TestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Questions_Test");
        });

        modelBuilder.Entity<QuestionClozeBlank>(entity =>
        {
            entity.HasKey(e => e.BlankId).HasName("PK__Question__F2BD63E7D863FE86");

            entity.Property(e => e.AcceptRegex).HasMaxLength(400);
            entity.Property(e => e.CorrectText).HasMaxLength(400);

            entity.HasOne(d => d.Question).WithMany(p => p.QuestionClozeBlanks)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QCloze_Question");
        });

        modelBuilder.Entity<QuestionOption>(entity =>
        {
            entity.HasKey(e => e.OptionId).HasName("PK__Question__92C7A1FFD74642E5");

            entity.HasOne(d => d.OptionMedia).WithMany(p => p.QuestionOptions)
                .HasForeignKey(d => d.OptionMediaId)
                .HasConstraintName("FK_QOptions_Media");

            entity.HasOne(d => d.Question).WithMany(p => p.QuestionOptions)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QOptions_Question");
        });

        modelBuilder.Entity<QuestionRangeAnswer>(entity =>
        {
            entity.HasKey(e => e.RangeId).HasName("PK__Question__6899CA14EF73FB91");

            entity.Property(e => e.MaxValue).HasColumnType("decimal(12, 4)");
            entity.Property(e => e.MinValue).HasColumnType("decimal(12, 4)");
            entity.Property(e => e.Tolerance).HasColumnType("decimal(12, 4)");

            entity.HasOne(d => d.Question).WithMany(p => p.QuestionRangeAnswers)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QRange_Question");
        });

        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.HasKey(e => e.ReminderId).HasName("PK__Reminder__01A83087F85448BC");

            entity.Property(e => e.RelatedType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.Reminders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reminders_User");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1AFBE627BB");

            entity.HasIndex(e => e.Name, "UQ__Roles__737584F6A098F8F5").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<SavedItem>(entity =>
        {
            entity.HasKey(e => e.SavedItemId).HasName("PK__SavedIte__1CBC88C8EC7DF7EA");

            entity.Property(e => e.AddedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ContentType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(d => d.Folder).WithMany(p => p.SavedItems)
                .HasForeignKey(d => d.FolderId)
                .HasConstraintName("FK_SavedItems_Folder");

            entity.HasOne(d => d.Library).WithMany(p => p.SavedItems)
                .HasForeignKey(d => d.LibraryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SavedItems_Library");
        });

        modelBuilder.Entity<ShoppingCart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Shopping__51BCD7B7C43CC63D");

            entity.HasIndex(e => e.UserId, "UQ_ShoppingCarts_UserId").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithOne(p => p.ShoppingCart)
                .HasForeignKey<ShoppingCart>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShoppingCarts_User");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.TagId).HasName("PK__Tags__657CF9ACE6461BCF");

            entity.HasIndex(e => e.Name, "UQ__Tags__737584F658F22BCC").IsUnique();

            entity.HasIndex(e => e.Slug, "UQ__Tags__BC7B5FB6571E0B70").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(80);
            entity.Property(e => e.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<Test>(entity =>
        {
            entity.HasKey(e => e.TestId).HasName("PK__Tests__8CC331602E3C05C8");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GradingMode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.MaxScore).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Visibility)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Owner).WithMany(p => p.Tests)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tests_Owner");
        });

        modelBuilder.Entity<TestAttempt>(entity =>
        {
            entity.HasKey(e => e.AttemptId).HasName("PK__TestAtte__891A68E64392AB4D");

            entity.Property(e => e.MaxScore).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.Score).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.StartedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status)
                .HasMaxLength(12)
                .IsUnicode(false);

            entity.HasOne(d => d.Test).WithMany(p => p.TestAttempts)
                .HasForeignKey(d => d.TestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attempts_Test");

            entity.HasOne(d => d.User).WithMany(p => p.TestAttempts)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attempts_User");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CE98FF591");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D1053455164E7D").IsUnique();

            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.PasswordResetToken).HasMaxLength(255);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status).HasDefaultValue(1);
            entity.Property(e => e.Username)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Roles");
        });

        modelBuilder.Entity<UserInterest>(entity =>
        {
            entity.HasKey(e => e.UserInterestId).HasName("PK__UserInte__28E6EBFEF254DB87");

            entity.HasIndex(e => new { e.UserId, e.CategoryId }, "UQ_UserInterests_User_Category").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Category).WithMany(p => p.UserInterests)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserInterests_Category");

            entity.HasOne(d => d.User).WithMany(p => p.UserInterests)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserInterests_User");
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__UserProf__1788CC4C494B0A05");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Bio).HasMaxLength(500);
            entity.Property(e => e.Gender).HasMaxLength(20);
            entity.Property(e => e.GradeLevel).HasMaxLength(50);
            entity.Property(e => e.Locale).HasMaxLength(10);
            entity.Property(e => e.SchoolName).HasMaxLength(200);
            entity.Property(e => e.TimeZone).HasMaxLength(64);

            entity.HasOne(d => d.User).WithOne(p => p.UserProfile)
                .HasForeignKey<UserProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserProfiles_User");
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__UserSett__1788CC4C101AA23F");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.EmailOptIn).HasDefaultValue(true);
            entity.Property(e => e.Language).HasMaxLength(10);
            entity.Property(e => e.PushOptIn).HasDefaultValue(true);
            entity.Property(e => e.TimeZone).HasMaxLength(64);
            entity.Property(e => e.UiTheme)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithOne(p => p.UserSetting)
                .HasForeignKey<UserSetting>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSettings_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
