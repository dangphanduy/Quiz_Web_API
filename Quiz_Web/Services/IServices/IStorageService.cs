using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;

namespace Quiz_Web.Services.IServices
{
    public interface IStorageService
    {
        /// <summary>
        /// Tải một file lên Google Cloud Storage từ IFormFile.
        /// </summary>
        /// <param name="file">File từ client gửi lên.</param>
        /// <param name="folderPath">Đường dẫn thư mục ảo trên GCS (ví dụ: "uploads/videos" hoặc "uploads/images").</param>
        /// <returns>Đường dẫn public URL của file sau khi upload thành công.</returns>
        Task<string> UploadFileAsync(IFormFile file, string folderPath);

        /// <summary>
        /// Tải một file lên Google Cloud Storage từ Stream.
        /// </summary>
        /// <param name="fileStream">Stream dữ liệu của file.</param>
        /// <param name="fileName">Tên file mong muốn lưu trên GCS.</param>
        /// <param name="contentType">MIME type của file (ví dụ: "video/mp4", "image/png").</param>
        /// <returns>Đường dẫn public URL của file sau khi upload thành công.</returns>
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);

        /// <summary>
        /// Xóa file khỏi Google Cloud Storage.
        /// </summary>
        /// <param name="fileUrl">Đường dẫn URL của file cần xóa.</param>
        /// <returns>True nếu xóa thành công, ngược lại False.</returns>
        Task<bool> DeleteFileAsync(string fileUrl);
    }
}
