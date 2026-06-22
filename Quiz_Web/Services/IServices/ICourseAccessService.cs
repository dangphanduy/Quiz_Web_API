namespace Quiz_Web.Services.IServices;

public interface ICourseAccessService
{
    /// <summary>Kiểm tra mua lẻ trước, subscription sau; không tải entity vào memory.</summary>
    Task<bool> CheckCourseAccessAsync(
        int userId,
        int courseId,
        CancellationToken cancellationToken = default);

    /// <summary>Giảng viên sở hữu khóa học cũng luôn có quyền xem.</summary>
    Task<bool> CanAccessCourseAsync(
        int userId,
        int courseId,
        CancellationToken cancellationToken = default);
}
