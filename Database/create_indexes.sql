-- Script tạo index tối ưu hóa tìm kiếm và sắp xếp cho Chatbot AI Course Assistant
-- Bảng áp dụng: Courses

-- 1. Index cho CategoryId để tối ưu hóa lọc theo danh mục
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Courses_CategoryId' AND object_id = OBJECT_ID('Courses'))
BEGIN
    CREATE INDEX IX_Courses_CategoryId ON Courses(CategoryId);
END
GO

-- 2. Index cho IsPublished kết hợp Title và AverageRating để tối ưu truy vấn tìm kiếm
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Courses_IsPublished_Search' AND object_id = OBJECT_ID('Courses'))
BEGIN
    CREATE INDEX IX_Courses_IsPublished_Search ON Courses(IsPublished) INCLUDE (Title, Summary, AverageRating);
END
GO

-- 3. Index sắp xếp theo rating giảm dần để tối ưu hóa việc lấy top khóa học nổi bật
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Courses_AverageRating_Desc' AND object_id = OBJECT_ID('Courses'))
BEGIN
    CREATE INDEX IX_Courses_AverageRating_Desc ON Courses(AverageRating DESC);
END
GO
