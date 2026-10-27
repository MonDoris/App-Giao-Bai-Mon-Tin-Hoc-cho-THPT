-- =====================================================================
-- seed-users.sqlite.sql : bản SQLite của seed-users.sql (CHỈ PHỤC VỤ KIỂM THỬ với SQLite in-memory, KHÔNG dùng khi chạy ứng dụng)
-- Nội dung giống seed-users.sql: 1 giáo viên (giaovien), 3 học sinh (hs01..hs03), lớp 12A1/12A2 và giao bài mẫu.
-- Mật khẩu lưu dạng băm PBKDF2-HMAC-SHA512 (120000 vòng), không có mật khẩu thô. Idempotent (chạy lại không trùng).
-- Yêu cầu: đã có schema và đã chạy seed-sample.sqlite.sql (nếu chưa có bài tập thì phần giao bài tự bỏ qua).
-- =====================================================================
INSERT OR IGNORE INTO "Users" ("Username", "DisplayName", "PasswordHash", "Role", "IsActive", "MustChangePassword", "FailedLoginCount", "SecurityStamp", "CreatedUtc")
VALUES ('giaovien', 'Giáo viên Mẫu', 'PBKDF2-SHA512$120000$cqIjYq/HxefiV9yAjd6nCA==$jUZvALFAHKk1g9pdVopk7T6U7BF/0HEQsiRztUGWSIWMIVCKUNpnrN3tHN6QMxbK/YSZ1KoSre8P33AXlRYuhQ==', 'Teacher', 1, 0, 0, 'a1b2c3d4e5f60718293a4b5c6d7e8f01', strftime('%Y-%m-%d %H:%M:%f', 'now'));
INSERT OR IGNORE INTO "Users" ("Username", "DisplayName", "PasswordHash", "Role", "IsActive", "MustChangePassword", "FailedLoginCount", "SecurityStamp", "CreatedUtc")
VALUES ('hs01', 'Học sinh 01', 'PBKDF2-SHA512$120000$7edhFjPD65CNvO7DsSekeA==$M4S9k/eaFD1jBEmJbt4EEu8sbRW56HRgUsysFR5yh7m8aveBpXza0ZO9K/hIZxS8YdVhIYPnuzMRwt9QBKaOtA==', 'Student', 1, 0, 0, 'b2c3d4e5f60718293a4b5c6d7e8f0112', strftime('%Y-%m-%d %H:%M:%f', 'now'));
INSERT OR IGNORE INTO "Users" ("Username", "DisplayName", "PasswordHash", "Role", "IsActive", "MustChangePassword", "FailedLoginCount", "SecurityStamp", "CreatedUtc")
VALUES ('hs02', 'Học sinh 02', 'PBKDF2-SHA512$120000$kVaGuOpmbY+E3Unt2ABY4Q==$C9RdjprmklFL4n3xA6F8Hq1o9Hg+BaofIGDcGWiL3POpiRz0Woy3L9QTvyFFOgWyeLNKfsOjR63dOmt2wYM3ZQ==', 'Student', 1, 0, 0, 'c3d4e5f60718293a4b5c6d7e8f011223', strftime('%Y-%m-%d %H:%M:%f', 'now'));
INSERT OR IGNORE INTO "Users" ("Username", "DisplayName", "PasswordHash", "Role", "IsActive", "MustChangePassword", "FailedLoginCount", "SecurityStamp", "CreatedUtc")
VALUES ('hs03', 'Học sinh 03', 'PBKDF2-SHA512$120000$ZBNo0/xRAYHcBmsZGkjXGg==$BvXdFIkfQUgIGsFKX9AJZDzaEG2u8cCsqz9lNBfYyUmhMYnfygY3ABcYqXoEjbpPVMgLT0lCl0bLYEryQD2sqA==', 'Student', 1, 0, 0, 'd4e5f60718293a4b5c6d7e8f01122334', strftime('%Y-%m-%d %H:%M:%f', 'now'));
INSERT OR IGNORE INTO "Classes" ("Name", "CreatedUtc") VALUES ('12A1', strftime('%Y-%m-%d %H:%M:%f', 'now'));
INSERT OR IGNORE INTO "Classes" ("Name", "CreatedUtc") VALUES ('12A2', strftime('%Y-%m-%d %H:%M:%f', 'now'));
INSERT OR IGNORE INTO "ClassMembers" ("ClassId", "StudentId")
SELECT (SELECT "Id" FROM "Classes" WHERE "Name" = '12A1'), (SELECT "Id" FROM "Users" WHERE "Username" = 'hs01');
INSERT OR IGNORE INTO "ClassMembers" ("ClassId", "StudentId")
SELECT (SELECT "Id" FROM "Classes" WHERE "Name" = '12A1'), (SELECT "Id" FROM "Users" WHERE "Username" = 'hs02');
INSERT OR IGNORE INTO "ClassMembers" ("ClassId", "StudentId")
SELECT (SELECT "Id" FROM "Classes" WHERE "Name" = '12A2'), (SELECT "Id" FROM "Users" WHERE "Username" = 'hs03');
INSERT INTO "Assignments" ("ExerciseId", "AssignedToAll", "DueAtUtc", "Note", "CreatedByUserId", "CreatedUtc")
SELECT 'bai-01-tong-hai-so', 1, NULL, NULL, (SELECT "Id" FROM "Users" WHERE "Username" = 'giaovien'), strftime('%Y-%m-%d %H:%M:%f', 'now')
WHERE EXISTS (SELECT 1 FROM "Exercises" WHERE "Id" = 'bai-01-tong-hai-so')
  AND NOT EXISTS (SELECT 1 FROM "Assignments" WHERE "ExerciseId" = 'bai-01-tong-hai-so' AND "AssignedToAll" = 1 AND "CancelledUtc" IS NULL);
INSERT INTO "Assignments" ("ExerciseId", "AssignedToAll", "DueAtUtc", "Note", "CreatedByUserId", "CreatedUtc")
SELECT 'bai-01-dien-tich-hcn', 1, NULL, NULL, (SELECT "Id" FROM "Users" WHERE "Username" = 'giaovien'), strftime('%Y-%m-%d %H:%M:%f', 'now')
WHERE EXISTS (SELECT 1 FROM "Exercises" WHERE "Id" = 'bai-01-dien-tich-hcn')
  AND NOT EXISTS (SELECT 1 FROM "Assignments" WHERE "ExerciseId" = 'bai-01-dien-tich-hcn' AND "AssignedToAll" = 1 AND "CancelledUtc" IS NULL);
INSERT INTO "Assignments" ("ExerciseId", "AssignedToAll", "DueAtUtc", "Note", "CreatedByUserId", "CreatedUtc")
SELECT 'bai-02-chan-le', 1, NULL, NULL, (SELECT "Id" FROM "Users" WHERE "Username" = 'giaovien'), strftime('%Y-%m-%d %H:%M:%f', 'now')
WHERE EXISTS (SELECT 1 FROM "Exercises" WHERE "Id" = 'bai-02-chan-le')
  AND NOT EXISTS (SELECT 1 FROM "Assignments" WHERE "ExerciseId" = 'bai-02-chan-le' AND "AssignedToAll" = 1 AND "CancelledUtc" IS NULL);
INSERT INTO "Assignments" ("ExerciseId", "AssignedToAll", "DueAtUtc", "Note", "CreatedByUserId", "CreatedUtc")
SELECT 'bai-02-xep-loai', 1, NULL, NULL, (SELECT "Id" FROM "Users" WHERE "Username" = 'giaovien'), strftime('%Y-%m-%d %H:%M:%f', 'now')
WHERE EXISTS (SELECT 1 FROM "Exercises" WHERE "Id" = 'bai-02-xep-loai')
  AND NOT EXISTS (SELECT 1 FROM "Assignments" WHERE "ExerciseId" = 'bai-02-xep-loai' AND "AssignedToAll" = 1 AND "CancelledUtc" IS NULL);
INSERT INTO "Assignments" ("ExerciseId", "AssignedToAll", "DueAtUtc", "Note", "CreatedByUserId", "CreatedUtc")
SELECT 'bai-03-tong-1-den-n', 0, NULL, NULL, (SELECT "Id" FROM "Users" WHERE "Username" = 'giaovien'), strftime('%Y-%m-%d %H:%M:%f', 'now')
WHERE EXISTS (SELECT 1 FROM "Exercises" WHERE "Id" = 'bai-03-tong-1-den-n')
  AND NOT EXISTS (SELECT 1 FROM "Assignments" a JOIN "AssignmentClasses" ac ON ac."AssignmentId" = a."Id"
                  WHERE a."ExerciseId" = 'bai-03-tong-1-den-n' AND a."AssignedToAll" = 0 AND a."CancelledUtc" IS NULL
                    AND ac."ClassId" = (SELECT "Id" FROM "Classes" WHERE "Name" = '12A1'));
INSERT OR IGNORE INTO "AssignmentClasses" ("AssignmentId", "ClassId")
SELECT a."Id", (SELECT "Id" FROM "Classes" WHERE "Name" = '12A1') FROM "Assignments" a
WHERE a."ExerciseId" = 'bai-03-tong-1-den-n' AND a."AssignedToAll" = 0 AND a."CancelledUtc" IS NULL
  AND NOT EXISTS (SELECT 1 FROM "AssignmentClasses" x WHERE x."AssignmentId" = a."Id");
INSERT INTO "Assignments" ("ExerciseId", "AssignedToAll", "DueAtUtc", "Note", "CreatedByUserId", "CreatedUtc")
SELECT 'bai-03-dem-uoc', 0, NULL, NULL, (SELECT "Id" FROM "Users" WHERE "Username" = 'giaovien'), strftime('%Y-%m-%d %H:%M:%f', 'now')
WHERE EXISTS (SELECT 1 FROM "Exercises" WHERE "Id" = 'bai-03-dem-uoc')
  AND NOT EXISTS (SELECT 1 FROM "Assignments" a JOIN "AssignmentClasses" ac ON ac."AssignmentId" = a."Id"
                  WHERE a."ExerciseId" = 'bai-03-dem-uoc' AND a."AssignedToAll" = 0 AND a."CancelledUtc" IS NULL
                    AND ac."ClassId" = (SELECT "Id" FROM "Classes" WHERE "Name" = '12A1'));
INSERT OR IGNORE INTO "AssignmentClasses" ("AssignmentId", "ClassId")
SELECT a."Id", (SELECT "Id" FROM "Classes" WHERE "Name" = '12A1') FROM "Assignments" a
WHERE a."ExerciseId" = 'bai-03-dem-uoc' AND a."AssignedToAll" = 0 AND a."CancelledUtc" IS NULL
  AND NOT EXISTS (SELECT 1 FROM "AssignmentClasses" x WHERE x."AssignmentId" = a."Id");
