IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019021148_InitialCreate'
)
BEGIN
    CREATE TABLE [Lessons] (
        [Id] nvarchar(100) NOT NULL,
        [Order] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Summary] nvarchar(max) NOT NULL,
        [Notes] nvarchar(max) NOT NULL,
        [Conclusion] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Lessons] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019021148_InitialCreate'
)
BEGIN
    CREATE TABLE [Exercises] (
        [Id] nvarchar(100) NOT NULL,
        [LessonId] nvarchar(100) NOT NULL,
        [Order] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Difficulty] nvarchar(10) NOT NULL,
        [Statement] nvarchar(max) NOT NULL,
        [InputFormat] nvarchar(max) NOT NULL,
        [OutputFormat] nvarchar(max) NOT NULL,
        [Constraints] nvarchar(max) NULL,
        [Examples] nvarchar(max) NOT NULL,
        [StarterCode] nvarchar(max) NOT NULL,
        [SolutionCode] nvarchar(max) NULL,
        [SolutionExplanation] nvarchar(max) NULL,
        [ConfirmEveryTime] bit NOT NULL DEFAULT CAST(0 AS bit),
        [TimeLimitMs] int NOT NULL DEFAULT 5000,
        CONSTRAINT [PK_Exercises] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Exercises_Difficulty] CHECK ([Difficulty] IN ('Easy', 'Medium', 'Hard')),
        CONSTRAINT [FK_Exercises_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [Lessons] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019021148_InitialCreate'
)
BEGIN
    CREATE TABLE [TestCases] (
        [Id] int NOT NULL IDENTITY,
        [ExerciseId] nvarchar(100) NOT NULL,
        [Order] int NOT NULL,
        [Input] nvarchar(max) NOT NULL,
        [ExpectedOutput] nvarchar(max) NOT NULL,
        [IsHidden] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_TestCases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TestCases_Exercises_ExerciseId] FOREIGN KEY ([ExerciseId]) REFERENCES [Exercises] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019021148_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Exercises_LessonId_Order] ON [Exercises] ([LessonId], [Order]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019021148_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Lessons_Order] ON [Lessons] ([Order]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019021148_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TestCases_ExerciseId_Order] ON [TestCases] ([ExerciseId], [Order]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019021148_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019021148_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Lessons] ADD [CreatedByUserId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Lessons] ADD [IsArchived] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Lessons] ADD [IsPublished] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Lessons] ADD [UpdatedUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Lessons] ADD [Version] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Exercises] ADD [CreatedByUserId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Exercises] ADD [IsArchived] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Exercises] ADD [IsPublished] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Exercises] ADD [UpdatedUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Exercises] ADD [Version] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE TABLE [Classes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Classes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(50) NOT NULL,
        [DisplayName] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Role] nvarchar(10) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit),
        [FailedLoginCount] int NOT NULL DEFAULT 0,
        [LockoutEndUtc] datetime2 NULL,
        [SecurityStamp] nvarchar(64) NOT NULL,
        [LastLoginUtc] datetime2 NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Users_Role] CHECK ([Role] IN ('Teacher', 'Student'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE TABLE [Assignments] (
        [Id] int NOT NULL IDENTITY,
        [ExerciseId] nvarchar(100) NOT NULL,
        [AssignedToAll] bit NOT NULL,
        [DueAtUtc] datetime2 NULL,
        [Note] nvarchar(500) NULL,
        [CreatedByUserId] int NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [CancelledUtc] datetime2 NULL,
        CONSTRAINT [PK_Assignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Assignments_Exercises_ExerciseId] FOREIGN KEY ([ExerciseId]) REFERENCES [Exercises] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Assignments_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE TABLE [ClassMembers] (
        [ClassId] int NOT NULL,
        [StudentId] int NOT NULL,
        CONSTRAINT [PK_ClassMembers] PRIMARY KEY ([ClassId], [StudentId]),
        CONSTRAINT [FK_ClassMembers_Classes_ClassId] FOREIGN KEY ([ClassId]) REFERENCES [Classes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ClassMembers_Users_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE TABLE [SolutionViews] (
        [StudentId] int NOT NULL,
        [ExerciseId] nvarchar(100) NOT NULL,
        [FirstViewedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SolutionViews] PRIMARY KEY ([StudentId], [ExerciseId]),
        CONSTRAINT [FK_SolutionViews_Exercises_ExerciseId] FOREIGN KEY ([ExerciseId]) REFERENCES [Exercises] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SolutionViews_Users_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE TABLE [AssignmentClasses] (
        [AssignmentId] int NOT NULL,
        [ClassId] int NOT NULL,
        CONSTRAINT [PK_AssignmentClasses] PRIMARY KEY ([AssignmentId], [ClassId]),
        CONSTRAINT [FK_AssignmentClasses_Assignments_AssignmentId] FOREIGN KEY ([AssignmentId]) REFERENCES [Assignments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AssignmentClasses_Classes_ClassId] FOREIGN KEY ([ClassId]) REFERENCES [Classes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE TABLE [Submissions] (
        [Id] bigint NOT NULL IDENTITY,
        [AssignmentId] int NOT NULL,
        [ExerciseId] nvarchar(100) NOT NULL,
        [StudentId] int NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [PassedTests] int NOT NULL,
        [TotalTests] int NOT NULL,
        [IsPassed] bit NOT NULL,
        [IsLate] bit NOT NULL,
        [ResultsJson] nvarchar(max) NULL,
        [SubmittedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Submissions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Submissions_PassedLessEqTotal] CHECK ([PassedTests] <= [TotalTests]),
        CONSTRAINT [CK_Submissions_PassedTests] CHECK ([PassedTests] >= 0),
        CONSTRAINT [CK_Submissions_TotalTests] CHECK ([TotalTests] >= 1),
        CONSTRAINT [FK_Submissions_Assignments_AssignmentId] FOREIGN KEY ([AssignmentId]) REFERENCES [Assignments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Submissions_Exercises_ExerciseId] FOREIGN KEY ([ExerciseId]) REFERENCES [Exercises] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Submissions_Users_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE INDEX [IX_Lessons_CreatedByUserId] ON [Lessons] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE INDEX [IX_Exercises_CreatedByUserId] ON [Exercises] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE INDEX [IX_AssignmentClasses_ClassId] ON [AssignmentClasses] ([ClassId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE INDEX [IX_Assignments_CreatedByUserId] ON [Assignments] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE INDEX [IX_Assignments_ExerciseId] ON [Assignments] ([ExerciseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Classes_Name] ON [Classes] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE INDEX [IX_ClassMembers_StudentId] ON [ClassMembers] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE INDEX [IX_SolutionViews_ExerciseId] ON [SolutionViews] ([ExerciseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE INDEX [IX_Submissions_AssignmentId] ON [Submissions] ([AssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE INDEX [IX_Submissions_ExerciseId] ON [Submissions] ([ExerciseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE INDEX [IX_Submissions_StudentId_ExerciseId_SubmittedUtc] ON [Submissions] ([StudentId], [ExerciseId], [SubmittedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Exercises] ADD CONSTRAINT [FK_Exercises_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    ALTER TABLE [Lessons] ADD CONSTRAINT [FK_Lessons_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019042655_AddAuthAndAssignments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019042655_AddAuthAndAssignments', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027070645_AddAssignmentStartDate'
)
BEGIN
    ALTER TABLE [Assignments] ADD [StartAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027070645_AddAssignmentStartDate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027070645_AddAssignmentStartDate', N'10.0.12');
END;

COMMIT;
GO

