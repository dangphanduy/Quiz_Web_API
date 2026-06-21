/* =========================================================
   Learning Platform — SQL Server DDL (REFRESHED, no Class)
   Model: Course → Chapter → Lesson → LessonContents
   Tests: Public window (OpenAt, CloseAt), scoring
   ========================================================= */

------------------------------------------------------------
-- 0) CREATE DATABASE & BASIC SETTINGS
------------------------------------------------------------
IF DB_ID(N'LearningPlatform') IS NULL
BEGIN
    CREATE DATABASE [LearningPlatform];
END
GO
USE [LearningPlatform];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =========================================================
   1) USERS / ROLES / SETTINGS / PROFILES
   ========================================================= */
CREATE TABLE dbo.Roles (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    Name   NVARCHAR(50) NOT NULL UNIQUE
);
GO

CREATE TABLE dbo.Users (
    UserId                  INT IDENTITY(1,1) PRIMARY KEY,
    Username                VARCHAR(100)   NOT NULL,
    Email                   NVARCHAR(255)  NOT NULL UNIQUE,
    PasswordHash            NVARCHAR(255)  NOT NULL,
    FullName                NVARCHAR(200)  NOT NULL,
    AvatarUrl               NVARCHAR(500)  NULL,
    Phone                   VARCHAR(20)    NULL,
    RoleId                  INT            NOT NULL,
    Status                  INT            NOT NULL CONSTRAINT DF_Users_Status DEFAULT (1),
    CreatedAt               DATETIME2(7)   NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    LastLoginAt             DATETIME2(7)   NULL,
    PasswordResetToken      NVARCHAR(255)  NULL,
    PasswordResetTokenExpiry DATETIME2(7)  NULL,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
);
GO

CREATE TABLE dbo.UserProfiles (
    UserId      INT           NOT NULL PRIMARY KEY,
    DoB         DATE          NULL,
    Gender      NVARCHAR(20)  NULL,
    Bio         NVARCHAR(500) NULL,
    SchoolName  NVARCHAR(200) NULL,
    GradeLevel  NVARCHAR(50)  NULL,
    Locale      NVARCHAR(10)  NULL,
    TimeZone    NVARCHAR(64)  NULL,
    CONSTRAINT FK_UserProfiles_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
GO

CREATE TABLE dbo.UserSettings (
    UserId     INT          NOT NULL PRIMARY KEY,
    UiTheme    VARCHAR(20)  NULL,  -- light/dark/system
    Language   NVARCHAR(10) NULL,
    TimeZone   NVARCHAR(64) NULL,
    EmailOptIn BIT NOT NULL CONSTRAINT DF_UserSettings_EmailOptIn DEFAULT (1),
    PushOptIn  BIT NOT NULL CONSTRAINT DF_UserSettings_PushOptIn  DEFAULT (1),
    CONSTRAINT FK_UserSettings_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
GO

/* =========================================================
   2) FILES / TAGGING (multi-entity)
   ========================================================= */
CREATE TABLE dbo.Files (
    FileId       INT IDENTITY(1,1) PRIMARY KEY,
    OwnerId      INT            NOT NULL,
    FileName     NVARCHAR(255)  NOT NULL,
    MimeType     NVARCHAR(100)  NOT NULL,
    SizeBytes    BIGINT         NOT NULL,
    StoragePath  NVARCHAR(500)  NOT NULL,
    Width        INT            NULL,
    Height       INT            NULL,
    DurationSec  INT            NULL,
    CreatedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_Files_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Files_Owner FOREIGN KEY (OwnerId) REFERENCES dbo.Users(UserId)
);
GO

/* =========================================================
   3) FLASHCARDS (Quizlet-like)
   ========================================================= */
CREATE TABLE dbo.FlashcardSets (
    SetId       INT IDENTITY(1,1) PRIMARY KEY,
    OwnerId     INT            NOT NULL,
    Title       NVARCHAR(200)  NOT NULL,
    Description NVARCHAR(MAX)  NULL,
    Visibility  VARCHAR(20)    NOT NULL, -- Private/Public/Course
    CoverUrl    NVARCHAR(500)  NULL,
    TagsText    NVARCHAR(500)  NULL,
    Language    NVARCHAR(20)   NULL,
    CreatedAt   DATETIME2(7)   NOT NULL CONSTRAINT DF_FlashcardSets_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2(7)   NULL,
    IsDeleted   BIT            NOT NULL CONSTRAINT DF_FlashcardSets_IsDeleted DEFAULT (0),
    CONSTRAINT CK_FlashcardSets_Visibility CHECK (Visibility IN ('Private','Public','Course')),
    CONSTRAINT FK_FlashcardSets_Owner FOREIGN KEY (OwnerId) REFERENCES dbo.Users(UserId)
);
GO

CREATE TABLE dbo.Flashcards (
    CardId       INT IDENTITY(1,1) PRIMARY KEY,
    SetId        INT            NOT NULL,
    FrontText    NVARCHAR(MAX)  NOT NULL,
    BackText     NVARCHAR(MAX)  NOT NULL,
    FrontMediaId INT            NULL,
    BackMediaId  INT            NULL,
    Hint         NVARCHAR(500)  NULL,
    OrderIndex   INT            NOT NULL CONSTRAINT DF_Flashcards_OrderIndex DEFAULT (0),
    CreatedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_Flashcards_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2(7)   NULL,
    CONSTRAINT FK_Flashcards_Set        FOREIGN KEY (SetId)        REFERENCES dbo.FlashcardSets(SetId),
    CONSTRAINT FK_Flashcards_FrontMedia FOREIGN KEY (FrontMediaId) REFERENCES dbo.Files(FileId),
    CONSTRAINT FK_Flashcards_BackMedia  FOREIGN KEY (BackMediaId)  REFERENCES dbo.Files(FileId)
);
GO

CREATE TABLE dbo.FlashcardPracticeLogs (
    LogId            INT IDENTITY(1,1) PRIMARY KEY,
    UserId           INT            NOT NULL,
    CardId           INT            NOT NULL,
    SetId            INT            NOT NULL,
    ScheduledAt      DATETIME2(7)   NULL,
    ReviewedAt       DATETIME2(7)   NULL,
    QualityScore     INT            NULL,        -- 0..5
    NextIntervalDays INT            NULL,
    EaseFactor       DECIMAL(4,2)   NULL,
    CONSTRAINT FK_FPL_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_FPL_Card FOREIGN KEY (CardId) REFERENCES dbo.Flashcards(CardId),
    CONSTRAINT FK_FPL_Set  FOREIGN KEY (SetId)  REFERENCES dbo.FlashcardSets(SetId)
);
GO

/* =========================================================
   4) TESTS / QUESTIONS (Public window)
   ========================================================= */
CREATE TABLE dbo.Tests (
    TestId           INT IDENTITY(1,1) PRIMARY KEY,
    OwnerId          INT            NOT NULL,
    Title            NVARCHAR(200)  NOT NULL,
    Description      NVARCHAR(MAX)  NULL,
    Visibility       VARCHAR(20)    NOT NULL, -- Private/Public/Course
    TimeLimitSec     INT            NULL,
    MaxAttempts      INT            NULL,
    ShuffleQuestions BIT            NOT NULL CONSTRAINT DF_Tests_ShuffleQ DEFAULT (0),
    ShuffleOptions   BIT            NOT NULL CONSTRAINT DF_Tests_ShuffleO DEFAULT (0),
    GradingMode      VARCHAR(10)    NOT NULL, -- Auto/Manual/Mixed
    MaxScore         DECIMAL(6,2)   NULL,     -- tổng điểm mục tiêu
    OpenAt           DATETIME2(7)   NULL,     -- thời điểm mở
    CloseAt          DATETIME2(7)   NULL,     -- thời điểm đóng
    CreatedAt        DATETIME2(7)   NOT NULL CONSTRAINT DF_Tests_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(7)   NULL,
    IsDeleted        BIT            NOT NULL CONSTRAINT DF_Tests_IsDeleted DEFAULT (0),
    CONSTRAINT CK_Tests_Visibility CHECK (Visibility IN ('Private','Public','Course')),
    CONSTRAINT CK_Tests_GradingMode CHECK (GradingMode IN ('Auto','Manual','Mixed')),
    CONSTRAINT FK_Tests_Owner FOREIGN KEY (OwnerId) REFERENCES dbo.Users(UserId)
);
GO

CREATE TABLE dbo.Questions (
    QuestionId  INT IDENTITY(1,1) PRIMARY KEY,
    TestId      INT           NOT NULL,
    Type        VARCHAR(20)   NOT NULL, -- MCQ_Single/MCQ_Multi/TrueFalse/Cloze/Range/ShortText
    StemText    NVARCHAR(MAX) NOT NULL,
    StemMediaId INT           NULL,
    Points      DECIMAL(5,2)  NOT NULL CONSTRAINT DF_Questions_Points DEFAULT (1),
    OrderIndex  INT           NOT NULL CONSTRAINT DF_Questions_Order DEFAULT (0),
    Metadata    NVARCHAR(MAX) NULL,     -- JSON
    CONSTRAINT CK_Questions_Type CHECK (Type IN ('MCQ_Single','MCQ_Multi','TrueFalse','Cloze','Range','ShortText')),
    CONSTRAINT FK_Questions_Test FOREIGN KEY (TestId) REFERENCES dbo.Tests(TestId),
    CONSTRAINT FK_Questions_StemMedia FOREIGN KEY (StemMediaId) REFERENCES dbo.Files(FileId)
);
GO

CREATE TABLE dbo.QuestionOptions (
    OptionId      INT IDENTITY(1,1) PRIMARY KEY,
    QuestionId    INT           NOT NULL,
    OptionText    NVARCHAR(MAX) NOT NULL,
    OptionMediaId INT           NULL,
    IsCorrect     BIT           NOT NULL CONSTRAINT DF_QuestionOptions_IsCorrect DEFAULT (0),
    OrderIndex    INT           NOT NULL CONSTRAINT DF_QuestionOptions_Order DEFAULT (0),
    CONSTRAINT FK_QOptions_Question FOREIGN KEY (QuestionId) REFERENCES dbo.Questions(QuestionId),
    CONSTRAINT FK_QOptions_Media    FOREIGN KEY (OptionMediaId) REFERENCES dbo.Files(FileId)
);
GO

CREATE TABLE dbo.QuestionClozeBlanks (
    BlankId      INT IDENTITY(1,1) PRIMARY KEY,
    QuestionId   INT           NOT NULL,
    BlankIndex   INT           NOT NULL,
    CorrectText  NVARCHAR(400) NOT NULL,
    AcceptRegex  NVARCHAR(400) NULL,
    CaseSensitive BIT          NOT NULL CONSTRAINT DF_QCloze_Case DEFAULT (0),
    CONSTRAINT FK_QCloze_Question FOREIGN KEY (QuestionId) REFERENCES dbo.Questions(QuestionId)
);
GO

CREATE TABLE dbo.QuestionRangeAnswers (
    RangeId    INT IDENTITY(1,1) PRIMARY KEY,
    QuestionId INT            NOT NULL,
    MinValue   DECIMAL(12,4)  NOT NULL,
    MaxValue   DECIMAL(12,4)  NOT NULL,
    Tolerance  DECIMAL(12,4)  NULL,
    CONSTRAINT FK_QRange_Question FOREIGN KEY (QuestionId) REFERENCES dbo.Questions(QuestionId)
);
GO

CREATE TABLE dbo.TestAttempts (
    AttemptId    INT IDENTITY(1,1) PRIMARY KEY,
    TestId       INT           NOT NULL,
    UserId       INT           NOT NULL,
    StartedAt    DATETIME2(7)  NOT NULL CONSTRAINT DF_TestAttempts_StartedAt DEFAULT SYSUTCDATETIME(),
    SubmittedAt  DATETIME2(7)  NULL,
    Status       VARCHAR(12)   NOT NULL,   -- InProgress/Submitted/Graded/Expired
    TimeSpentSec INT           NULL,
    Score        DECIMAL(6,2)  NULL,
    MaxScore     DECIMAL(6,2)  NULL,
    CONSTRAINT CK_TestAttempts_Status CHECK (Status IN ('InProgress','Submitted','Graded','Expired')),
    CONSTRAINT FK_Attempts_Test FOREIGN KEY (TestId) REFERENCES dbo.Tests(TestId),
    CONSTRAINT FK_Attempts_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
GO

CREATE TABLE dbo.AttemptAnswers (
    AttemptAnswerId INT IDENTITY(1,1) PRIMARY KEY,
    AttemptId       INT           NOT NULL,
    QuestionId      INT           NOT NULL,
    AnswerPayload   NVARCHAR(MAX) NULL, -- JSON
    IsCorrect       BIT           NULL,
    Score           DECIMAL(5,2)  NULL,
    AutoGraded      BIT           NOT NULL CONSTRAINT DF_AttemptAnswers_Auto DEFAULT (0),
    GradedAt        DATETIME2(7)  NULL,
    GraderId        INT           NULL,
    CONSTRAINT FK_AAnswers_Attempt  FOREIGN KEY (AttemptId)  REFERENCES dbo.TestAttempts(AttemptId),
    CONSTRAINT FK_AAnswers_Question FOREIGN KEY (QuestionId)  REFERENCES dbo.Questions(QuestionId),
    CONSTRAINT FK_AAnswers_Grader   FOREIGN KEY (GraderId)    REFERENCES dbo.Users(UserId)
);
GO

/* =========================================================
   5) COURSES (Commerce)  → CHAPTERS → LESSONS → LESSON CONTENTS
   ========================================================= */
   CREATE TABLE dbo.CourseCategories (
    CategoryId   INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(200) NOT NULL,
    Slug         NVARCHAR(200) NOT NULL UNIQUE,
    Description  NVARCHAR(MAX) NULL,
    IconUrl      NVARCHAR(500) NULL,
    DisplayOrder INT NOT NULL DEFAULT (0),
    CreatedAt    DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Courses (
    CourseId      INT IDENTITY(1,1) PRIMARY KEY,
    OwnerId       INT            NOT NULL,
    CategoryId    INT            NULL,  -- liên kết 1-n
    Title         NVARCHAR(200)  NOT NULL,
    Slug          NVARCHAR(200)  NOT NULL,
    Summary       NVARCHAR(MAX)  NULL,
    CoverUrl      NVARCHAR(500)  NULL,
    Price         DECIMAL(12,2)  NOT NULL CONSTRAINT DF_Courses_Price DEFAULT (0),
    IsPublished   BIT            NOT NULL CONSTRAINT DF_Courses_IsPublished DEFAULT (0),
    AverageRating DECIMAL(3,2)   NOT NULL DEFAULT (0),
    TotalReviews  INT            NOT NULL DEFAULT (0),
    CreatedAt     DATETIME2(7)   NOT NULL CONSTRAINT DF_Courses_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2(7)   NULL,
    CONSTRAINT UQ_Courses_Slug UNIQUE (Slug),
    CONSTRAINT FK_Courses_Owner FOREIGN KEY (OwnerId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Courses_Category FOREIGN KEY (CategoryId) REFERENCES dbo.CourseCategories(CategoryId)
);
GO

/* =========================================================
   5.1) COURSE REVIEWS — Ratings and Feedback
   ========================================================= */
CREATE TABLE dbo.CourseReviews (
    ReviewId     INT IDENTITY(1,1) PRIMARY KEY,
    CourseId     INT NOT NULL,
    UserId       INT NOT NULL,
    Rating       DECIMAL(2,1) NOT NULL CHECK (Rating BETWEEN 0 AND 5),
    Comment      NVARCHAR(1000) NULL,
    CreatedAt    DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2(7) NULL,
    IsApproved   BIT NOT NULL DEFAULT (1),
    FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
GO

CREATE INDEX IX_CourseReviews_Course ON dbo.CourseReviews(CourseId);
GO

/* =========================================================
   5.2) TRIGGER — Update Course Rating on Review Changes
   ========================================================= */
CREATE TRIGGER trg_UpdateCourseRating
ON dbo.CourseReviews
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE c
    SET 
        AverageRating = ISNULL((
            SELECT AVG(Rating) 
            FROM dbo.CourseReviews r 
            WHERE r.CourseId = c.CourseId AND r.IsApproved = 1
        ), 0),
        TotalReviews = (
            SELECT COUNT(*) 
            FROM dbo.CourseReviews r 
            WHERE r.CourseId = c.CourseId AND r.IsApproved = 1
        )
    FROM dbo.Courses c
    WHERE c.CourseId IN (
        SELECT DISTINCT CourseId FROM inserted
        UNION
        SELECT DISTINCT CourseId FROM deleted
    );
END;
GO

/* =========================================================
   5.3) COURSE CHAPTERS / LESSONS / LESSON CONTENTS / PROGRESS
   ========================================================= */
CREATE TABLE dbo.CourseChapters (
    ChapterId   INT IDENTITY(1,1) PRIMARY KEY,
    CourseId    INT            NOT NULL,
    Title       NVARCHAR(200)  NOT NULL,
    Description NVARCHAR(MAX)  NULL,
    OrderIndex  INT            NOT NULL CONSTRAINT DF_Chapters_Order DEFAULT (0),
    CONSTRAINT FK_Chapters_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.Lessons (
    LessonId    INT IDENTITY(1,1) PRIMARY KEY,
    ChapterId   INT            NOT NULL,
    Title       NVARCHAR(200)  NOT NULL,
    Description NVARCHAR(MAX)  NULL,
    OrderIndex  INT            NOT NULL CONSTRAINT DF_Lessons_Order DEFAULT (0),
    Visibility  VARCHAR(20)    NOT NULL CONSTRAINT DF_Lessons_Visibility DEFAULT ('Course'),
    CreatedAt   DATETIME2(7)   NOT NULL CONSTRAINT DF_Lessons_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2(7)   NULL,
    CONSTRAINT CK_Lessons_Visibility CHECK (Visibility IN ('Private','Public','Course')),
    CONSTRAINT FK_Lessons_Chapter FOREIGN KEY (ChapterId) REFERENCES dbo.CourseChapters(ChapterId) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.LessonContents (
    ContentId   INT IDENTITY(1,1) PRIMARY KEY,
    LessonId    INT            NOT NULL,
    ContentType VARCHAR(20)    NOT NULL,
    RefId       INT            NULL,
    Title       NVARCHAR(200)  NULL,
    Body        NVARCHAR(MAX)  NULL,
	VideoUrl	NVARCHAR(500)	NULL,
	DocumentUrl	NVARCHAR(500)	NULL,
    OrderIndex  INT            NOT NULL CONSTRAINT DF_LessonContents_Order DEFAULT (0),
    CreatedAt   DATETIME2(7)   NOT NULL CONSTRAINT DF_LessonContents_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_LessonContents_Type CHECK (ContentType IN ('Video','Theory','FlashcardSet','Test')),
    CONSTRAINT FK_LessonContents_Lesson FOREIGN KEY (LessonId) REFERENCES dbo.Lessons(LessonId) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.CourseProgress (
    ProgressId    INT IDENTITY(1,1) PRIMARY KEY,
    UserId        INT            NOT NULL,
    CourseId      INT            NOT NULL,
    LessonId      INT            NULL,
    ContentType   VARCHAR(20)    NOT NULL,
    ContentId     INT            NOT NULL,
    IsCompleted   BIT            NOT NULL CONSTRAINT DF_CourseProgress_Completed DEFAULT (0),
    CompletionAt  DATETIME2(7)   NULL,
    LastViewedAt  DATETIME2(7)   DEFAULT SYSUTCDATETIME(),
    Score         DECIMAL(6,2)   NULL,
    DurationSec   INT            NULL,
    FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId) ON DELETE CASCADE
);
GO

CREATE INDEX IX_LessonContents_Lesson_Order ON dbo.LessonContents(LessonId, OrderIndex);
GO

/* =========================================================
   6) PURCHASES / PAYMENTS / CERTIFICATES
   ========================================================= */
CREATE TABLE dbo.CoursePurchases (
    PurchaseId  INT IDENTITY(1,1) PRIMARY KEY,
    CourseId    INT           NOT NULL,
    BuyerId     INT           NOT NULL,
    PricePaid   DECIMAL(12,2) NOT NULL,
    Currency    NVARCHAR(10)  NOT NULL,
    Status      VARCHAR(20)   NOT NULL, -- Pending/Paid/Refunded/Failed
    PurchasedAt DATETIME2(7)  NOT NULL CONSTRAINT DF_CoursePurchases_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_CoursePurchases_Status CHECK (Status IN ('Pending','Paid','Refunded','Failed')),
    CONSTRAINT FK_CPurchases_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId) ON DELETE CASCADE,
    CONSTRAINT FK_CPurchases_Buyer  FOREIGN KEY (BuyerId)  REFERENCES dbo.Users(UserId)
);
GO

--CREATE TABLE dbo.Payments (
 --   PaymentId   INT IDENTITY(1,1) PRIMARY KEY,
 --   PurchaseId  INT           NOT NULL,
 --   Provider    VARCHAR(40)   NOT NULL, -- VNPay/Stripe/...
 --   ProviderRef NVARCHAR(120) NULL,
--    Amount      DECIMAL(12,2) NOT NULL,
--    Currency    NVARCHAR(10)  NOT NULL,
--    Status      VARCHAR(20)   NOT NULL, -- Pending/Paid/Failed/Refunded
--    PaidAt      DATETIME2(7)  NULL,
--    RawPayload  NVARCHAR(MAX) NULL,
--    CONSTRAINT FK_Payments_Purchase FOREIGN KEY (PurchaseId) REFERENCES dbo.CoursePurchases(PurchaseId) ON DELETE CASCADE
--);
GO

CREATE TABLE dbo.Certificates (
    CertId     INT IDENTITY(1,1) PRIMARY KEY,
    CourseId   INT           NOT NULL,
    UserId     INT           NOT NULL,
    IssuedAt   DATETIME2(7)  NOT NULL CONSTRAINT DF_Certificates_IssuedAt DEFAULT SYSUTCDATETIME(),
    Serial     NVARCHAR(50)  NULL,
    VerifyCode NVARCHAR(50)  NOT NULL,
    CONSTRAINT UQ_Certificates_Verify UNIQUE (VerifyCode),
    CONSTRAINT FK_Certificates_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId) ON DELETE CASCADE,
    CONSTRAINT FK_Certificates_User   FOREIGN KEY (UserId)   REFERENCES dbo.Users(UserId)
);
GO


CREATE TABLE Orders (
    OrderId INT IDENTITY(1,1) PRIMARY KEY,
    BuyerId INT NOT NULL,
    TotalAmount DECIMAL(12,2) NOT NULL,
    Currency NVARCHAR(10) NOT NULL DEFAULT 'VND',
    Status VARCHAR(20) NOT NULL, -- Pending/Paid/Failed
    CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    PaidAt DATETIME2(7) NULL,

    CONSTRAINT FK_Orders_User FOREIGN KEY (BuyerId)
        REFERENCES dbo.Users(UserId)
);

CREATE TABLE OrderItems (
    ItemId INT IDENTITY(1,1) PRIMARY KEY,
    OrderId INT NOT NULL,
    CourseId INT NOT NULL,
    Price DECIMAL(12,2) NOT NULL,

    CONSTRAINT FK_OrderItems_Order FOREIGN KEY (OrderId)
        REFERENCES Orders(OrderId) ON DELETE CASCADE,

    CONSTRAINT FK_OrderItems_Course FOREIGN KEY (CourseId)
        REFERENCES Courses(CourseId) ON DELETE CASCADE
);
CREATE TABLE Payments (
    PaymentId INT IDENTITY(1,1) PRIMARY KEY,
    OrderId INT NOT NULL,
    Provider VARCHAR(40) NOT NULL,
    ProviderRef NVARCHAR(200) NULL,
    Amount DECIMAL(12,2) NOT NULL,
    Currency NVARCHAR(10) NOT NULL,
    Status VARCHAR(20) NOT NULL, -- Pending/Paid/Failed
    PaidAt DATETIME2(7) NULL,
    RawPayload NVARCHAR(MAX) NULL,

    CONSTRAINT FK_Payments_Order FOREIGN KEY (OrderId)
        REFERENCES Orders(OrderId) ON DELETE CASCADE
);

/* =========================================================
   7) LIBRARIES (save/favorite)
   ========================================================= */
CREATE TABLE dbo.Libraries (
    LibraryId   INT IDENTITY(1,1) PRIMARY KEY,
    OwnerId     INT            NOT NULL,
    Name        NVARCHAR(200)  NOT NULL,
    Description NVARCHAR(MAX)  NULL,
    CreatedAt   DATETIME2(7)   NOT NULL CONSTRAINT DF_Libraries_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Libraries_Owner FOREIGN KEY (OwnerId) REFERENCES dbo.Users(UserId)
);
GO

CREATE TABLE dbo.Folders (
    FolderId       INT IDENTITY(1,1) PRIMARY KEY,
    LibraryId      INT            NOT NULL,
    ParentFolderId INT            NULL,
    Name           NVARCHAR(200)  NOT NULL,
    OrderIndex     INT            NOT NULL CONSTRAINT DF_Folders_Order DEFAULT (0),
    CreatedAt      DATETIME2(7)   NOT NULL CONSTRAINT DF_Folders_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Folders_Library FOREIGN KEY (LibraryId)      REFERENCES dbo.Libraries(LibraryId),
    CONSTRAINT FK_Folders_Parent  FOREIGN KEY (ParentFolderId) REFERENCES dbo.Folders(FolderId)
);
GO

CREATE TABLE dbo.SavedItems (
    SavedItemId INT IDENTITY(1,1) PRIMARY KEY,
    LibraryId   INT          NOT NULL,
    FolderId    INT          NULL,
    ContentType VARCHAR(20)  NOT NULL, -- Course/Lesson/FlashcardSet/Test
    ContentId   INT          NOT NULL,
    Note        NVARCHAR(500) NULL,
    AddedAt     DATETIME2(7) NOT NULL CONSTRAINT DF_SavedItems_AddedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_SavedItems_Type CHECK (ContentType IN ('Course','Lesson','FlashcardSet','Test')),
    CONSTRAINT FK_SavedItems_Library FOREIGN KEY (LibraryId) REFERENCES dbo.Libraries(LibraryId),
    CONSTRAINT FK_SavedItems_Folder  FOREIGN KEY (FolderId)  REFERENCES dbo.Folders(FolderId)
);
GO

/* =========================================================
   8) SHARING (optional, multi-entity)
   ========================================================= */
CREATE TABLE dbo.ContentShares (
    ShareId     INT IDENTITY(1,1) PRIMARY KEY,
    ContentType VARCHAR(20)  NOT NULL, -- FlashcardSet/Test/Lesson/Course
    ContentId   INT          NOT NULL,
    TargetType  VARCHAR(10)  NOT NULL, -- User/Public
    TargetId    INT          NULL,     -- NULL if Public
    CanView     BIT          NOT NULL CONSTRAINT DF_ContentShares_View DEFAULT (1),
    CanEdit     BIT          NOT NULL CONSTRAINT DF_ContentShares_Edit DEFAULT (0),
    CanAssign   BIT          NOT NULL CONSTRAINT DF_ContentShares_Assign DEFAULT (0),
    ExpiresAt   DATETIME2(7) NULL,
    CreatedBy   INT          NOT NULL,
    CreatedAt   DATETIME2(7) NOT NULL CONSTRAINT DF_ContentShares_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_ContentShares_ContentType CHECK (ContentType IN ('FlashcardSet','Test','Lesson','Course')),
    CONSTRAINT CK_ContentShares_TargetType  CHECK (TargetType  IN ('User','Public')),
    CONSTRAINT FK_ContentShares_Creator FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId)
);
GO

/* =========================================================
   9) NOTIFICATIONS / REMINDERS / AUDIT / ERRORS
   ========================================================= */
CREATE TABLE dbo.Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT            NOT NULL,
    Type           VARCHAR(40)    NOT NULL,
    Title          NVARCHAR(200)  NOT NULL,
    Body           NVARCHAR(MAX)  NULL,
    Data           NVARCHAR(MAX)  NULL,
    IsRead         BIT            NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
    CreatedAt      DATETIME2(7)   NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT SYSUTCDATETIME(),
    ReadAt         DATETIME2(7)   NULL,
    CONSTRAINT FK_Notifications_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
GO

/* Đổi loại liên kết Reminder cho phù hợp (không còn Assignment/Class) */
CREATE TABLE dbo.Reminders (
    ReminderId  INT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT           NOT NULL,
    RelatedType VARCHAR(20)   NOT NULL, -- Test/TestAttempt/Course/Lesson
    RelatedId   INT           NOT NULL,
    TriggerAt   DATETIME2(7)  NOT NULL,
    SentAt      DATETIME2(7)  NULL,
    Status      VARCHAR(10)   NOT NULL, -- Pending/Sent/Cancelled
    CONSTRAINT CK_Reminders_RelatedType CHECK (RelatedType IN ('Test','TestAttempt','Course','Lesson')),
    CONSTRAINT CK_Reminders_Status CHECK (Status IN ('Pending','Sent','Cancelled')),
    CONSTRAINT FK_Reminders_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
GO

CREATE TABLE dbo.AuditLogs (
    AuditId    INT IDENTITY(1,1) PRIMARY KEY,
    UserId     INT            NULL,
    Action     NVARCHAR(100)  NOT NULL,
    EntityType NVARCHAR(50)   NOT NULL,
    EntityId   INT            NULL,
    Before     NVARCHAR(MAX)  NULL, -- JSON
    After      NVARCHAR(MAX)  NULL, -- JSON
    CreatedAt  DATETIME2(7)   NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT SYSUTCDATETIME(),
    IpAddress  VARCHAR(45)    NULL,
    CONSTRAINT FK_AuditLogs_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
GO


CREATE TABLE dbo.UserInterests (
    UserInterestId INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT NOT NULL,
    CategoryId     INT NOT NULL,
    CreatedAt      DATETIME2(7) NOT NULL CONSTRAINT DF_UserInterests_CreatedAt DEFAULT SYSUTCDATETIME(),
    
    CONSTRAINT FK_UserInterests_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_UserInterests_Category FOREIGN KEY (CategoryId) REFERENCES dbo.CourseCategories(CategoryId),
    
    -- Đảm bảo người dùng không chọn trùng lặp một chủ đề
    CONSTRAINT UQ_UserInterests_User_Category UNIQUE (UserId, CategoryId)
);
GO

CREATE TABLE dbo.ShoppingCarts (
    CartId    INT IDENTITY(1,1) PRIMARY KEY,
    UserId    INT NOT NULL,
    CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ShoppingCarts_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2(7) NULL,
    
    CONSTRAINT FK_ShoppingCarts_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    -- Đảm bảo mỗi người dùng chỉ có 1 giỏ hàng
    CONSTRAINT UQ_ShoppingCarts_UserId UNIQUE (UserId) 
);
GO

CREATE TABLE dbo.CartItems (
    CartItemId INT IDENTITY(1,1) PRIMARY KEY,
    CartId     INT NOT NULL,
    CourseId   INT NOT NULL,
    AddedAt    DATETIME2(7) NOT NULL CONSTRAINT DF_CartItems_AddedAt DEFAULT SYSUTCDATETIME(),
    
    CONSTRAINT FK_CartItems_Cart FOREIGN KEY (CartId) REFERENCES dbo.ShoppingCarts(CartId),
    CONSTRAINT FK_CartItems_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId) ON DELETE CASCADE,
    
    -- Đảm bảo không thêm trùng 1 khóa học vào giỏ
    CONSTRAINT UQ_CartItems_Cart_Course UNIQUE (CartId, CourseId)
);
GO

/* =========================================================
   10) SEED DATA (tối thiểu để chạy thử)
   ========================================================= */
INSERT INTO dbo.Roles (Name) VALUES (N'Admin'), (N'User');
GO

INSERT INTO dbo.Users (Username,Email,PasswordHash,FullName,RoleId,Phone)
VALUES
('admin','admin@learn.vn', N'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=', N'Quản trị viên', 1, '0909000001'),
('teacher','teacher@learn.vn', N'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=', N'Trần Minh Khoa', 2, '0909000002'),
('student1','student1@learn.vn', N'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=', N'Nguyễn Lan Anh', 2, '0909000003');
GO

/* Tag mẫu */
--INSERT INTO dbo.Tags (Name, Slug)
--VALUES (N'Lập trình', 'lap-trinh'), (N'SQL', 'sql'), (N'C#', 'csharp');
--GO

/* Flashcard mẫu */
INSERT INTO dbo.FlashcardSets (OwnerId, Title, Description, Visibility, Language)
VALUES (2, N'Flashcards SQL Cơ bản', N'Lệnh SELECT/INSERT/UPDATE', 'Public', 'vi');
GO
INSERT INTO dbo.Flashcards (SetId, FrontText, BackText, Hint, OrderIndex)
VALUES
(1, N'Lệnh SELECT dùng để?', N'Truy vấn dữ liệu', N'Đọc dữ liệu', 1),
(1, N'Lệnh INSERT dùng để?', N'Thêm bản ghi', N'Thêm dữ liệu', 2);
GO

/* Test mẫu (mở trong 7 ngày) */
INSERT INTO dbo.Tests (OwnerId, Title, Description, Visibility, GradingMode, TimeLimitSec, MaxAttempts, MaxScore, OpenAt, CloseAt)
VALUES (2, N'Bài kiểm tra SQL Cơ bản', N'10 câu trắc nghiệm', 'Public', 'Auto', 900, 3, 10,
        DATEADD(DAY,-1, SYSUTCDATETIME()), DATEADD(DAY, 6, SYSUTCDATETIME()));
GO
INSERT INTO dbo.Questions (TestId, Type, StemText, Points, OrderIndex)
VALUES
(1, 'MCQ_Single', N'Lệnh nào dùng để chọn dữ liệu?', 1, 1),
(1, 'TrueFalse',  N'RAM là bộ nhớ tạm thời.', 1, 2);
GO
INSERT INTO dbo.QuestionOptions (QuestionId, OptionText, IsCorrect, OrderIndex)
VALUES
(1, N'SELECT', 1, 1),
(1, N'INSERT', 0, 2),
(2, N'Đúng',   1, 1),
(2, N'Sai',    0, 2);
GO

/* Course → Chapter → Lesson → LessonContents (Video/Theory/Flashcard/Test) */

/* Danh mục khóa học */
INSERT INTO dbo.CourseCategories (Name, Slug, Description)
VALUES
(N'Lập trình', 'lap-trinh', N'Học các ngôn ngữ lập trình'),
(N'Cơ sở dữ liệu', 'co-so-du-lieu', N'Học SQL, thiết kế dữ liệu'),
(N'Phân tích dữ liệu', 'phan-tich-du-lieu', N'Học Power BI, Python cho Data'),
(N'Trí tuệ nhân tạo', 'tri-tue-nhan-tao', N'Machine Learning, Deep Learning');
GO

/* Khóa học mẫu */
INSERT INTO dbo.Courses (OwnerId, CategoryId, Title, Slug, Summary, Price, IsPublished)
VALUES
(2, 2, N'Khoá học SQL Cơ bản', 'sql-co-ban', N'Học SQL từ cơ bản đến nâng cao', 199000, 1),
(2, 1, N'Lập trình C# cơ bản', 'lap-trinh-csharp', N'Học C# và OOP cho người mới bắt đầu', 299000, 1),
(2, 3, N'Phân tích dữ liệu với Excel', 'phan-tich-excel', N'Học kỹ năng phân tích dữ liệu bằng Excel', 149000, 1);
GO


INSERT INTO dbo.Courses (OwnerId, CategoryId, Title, Slug, Summary, Price,  IsPublished)
VALUES (2, 2, N'Khoá học SQL Cơ bản', 'sql-co-ban2', N'Học SQL từ cơ bản', 199000,  1);
GO



INSERT INTO dbo.CourseChapters (CourseId, Title, Description, OrderIndex)
VALUES (1, N'Chương 1: Giới thiệu', N'Khởi động với SQL', 1);
GO

INSERT INTO dbo.Lessons (ChapterId, Title, Description, OrderIndex, Visibility)
VALUES (1, N'Bài 1: Tổng quan SQL', N'Hiểu khái niệm và ứng dụng', 1, 'Course');
GO

/* (Giả sử chưa có file video thật; dùng Theory + Flashcards + Test) */
INSERT INTO dbo.LessonContents (LessonId, ContentType, Title, Body, OrderIndex)
VALUES (1, 'Theory', N'Giới thiệu lý thuyết', N'SQL là ngôn ngữ truy vấn...', 1);
GO

/* Gắn FlashcardSet (SetId=1) vào bài học */
INSERT INTO dbo.LessonContents (LessonId, ContentType, RefId, Title, OrderIndex)
VALUES (1, 'FlashcardSet', 1, N'Ôn tập Flashcards SQL', 2);
GO

/* Gắn Test (TestId=1) vào bài học */
INSERT INTO dbo.LessonContents (LessonId, ContentType, RefId, Title, OrderIndex)
VALUES (1, 'Test', 1, N'Kiểm tra nhanh SQL', 3);
GO

/* Gắn tag cho Course, Chapter, Lesson, Test */
--INSERT INTO dbo.ContentTags (ContentType, ContentId, TagId)
--VALUES ('Course', 1, 2),   -- SQL
--       ('Chapter',1, 2),
--       ('Lesson', 1, 2),
--       ('Test',   1, 2);
--GO
/* =========================================================
   SEED DATA DEMO REVIEWS
   ========================================================= */
INSERT INTO dbo.CourseReviews (CourseId, UserId, Rating, Comment)
VALUES
(1, 3, 4.5, N'Khóa học rất dễ hiểu, ví dụ rõ ràng!'),
(1, 2, 5.0, N'Rất hữu ích, giảng viên hướng dẫn chi tiết.');
GO

-- Test xem điểm trung bình được cập nhật
SELECT Title, AverageRating, TotalReviews FROM dbo.Courses;
GO

