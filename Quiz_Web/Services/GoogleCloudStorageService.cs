using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quiz_Web.Services.IServices;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Quiz_Web.Services
{
    public class GoogleCloudStorageService : IStorageService
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;
        private readonly ILogger<GoogleCloudStorageService> _logger;

        public GoogleCloudStorageService(
            IConfiguration configuration, 
            IWebHostEnvironment env, 
            ILogger<GoogleCloudStorageService> logger)
        {
            _logger = logger;
            var section = configuration.GetSection("GoogleCloudStorage");
            _bucketName = section["BucketName"] ?? throw new ArgumentNullException("GoogleCloudStorage:BucketName is not configured in appsettings.");
            
            var credentialFilePath = section["CredentialFilePath"] ?? "google-creds.json";
            
            // Nếu đường dẫn là tương đối, chuyển nó thành tuyệt đối dựa trên ContentRootPath
            if (!Path.IsPathRooted(credentialFilePath))
            {
                credentialFilePath = Path.Combine(env.ContentRootPath, credentialFilePath);
            }

            try
            {
                if (File.Exists(credentialFilePath))
                {
                    _logger.LogInformation("Initializing Google Cloud Storage with service account file: {Path}", credentialFilePath);
                    var credential = GoogleCredential.FromFile(credentialFilePath);
                    _storageClient = StorageClient.Create(credential);
                }
                else
                {
                    _logger.LogWarning("Credential file not found at: {Path}. Falling back to Application Default Credentials (ADC).", credentialFilePath);
                    _storageClient = StorageClient.Create();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Google Cloud Storage Client.");
                throw;
            }
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderPath)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File cannot be empty", nameof(file));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            
            // Xây dựng object name trên GCS (ví dụ: uploads/videos/abcd1234.mp4)
            var cleanFolderPath = folderPath.Trim('/').Replace("\\", "/");
            var objectName = string.IsNullOrEmpty(cleanFolderPath) ? fileName : $"{cleanFolderPath}/{fileName}";

            using var stream = file.OpenReadStream();
            return await UploadFileAsync(stream, objectName, file.ContentType);
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            var options = new UploadObjectOptions
            {
                PredefinedAcl = PredefinedObjectAcl.PublicRead // Cấp quyền xem công khai cho đối tượng tải lên
            };

            try
            {
                _logger.LogInformation("Uploading object {ObjectName} to bucket {BucketName}", fileName, _bucketName);
                
                // Upload file lên GCS với quyền PublicRead
                await _storageClient.UploadObjectAsync(_bucketName, fileName, contentType, fileStream, options);
            }
            catch (Google.GoogleApiException ex) when (ex.Error?.Code == 400 || ex.Message.Contains("CannotUseAclAndUniformBucketLevelAccess"))
            {
                // Fallback nếu bucket bật Uniform Bucket-Level Access hoặc Public Access Prevention (không cho phép đặt ACL riêng lẻ)
                _logger.LogWarning(ex, "Failed to upload with PublicRead ACL. Falling back to default bucket upload settings (Uniform Bucket-Level Access enabled).");
                
                if (fileStream.CanSeek)
                {
                    fileStream.Position = 0;
                }
                await _storageClient.UploadObjectAsync(_bucketName, fileName, contentType, fileStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {ObjectName} to Google Cloud Storage", fileName);
                throw;
            }

            // Trả về public URL để xem file trực tiếp
            return $"https://storage.googleapis.com/{_bucketName}/{fileName}";
        }

        public async Task<bool> DeleteFileAsync(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return false;

            try
            {
                var prefix = $"https://storage.googleapis.com/{_bucketName}/";
                if (fileUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    // Lấy ra phần Object Name phía sau prefix
                    var objectName = fileUrl.Substring(prefix.Length);
                    
                    // Giải mã URL decode (ví dụ: các ký tự đặc biệt hay khoảng trắng)
                    objectName = Uri.UnescapeDataString(objectName);
                    
                    _logger.LogInformation("Deleting object {ObjectName} from bucket {BucketName}", objectName, _bucketName);
                    await _storageClient.DeleteObjectAsync(_bucketName, objectName);
                    return true;
                }
                else
                {
                    _logger.LogWarning("File URL {FileUrl} does not match the expected bucket prefix {Prefix}", fileUrl, prefix);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileUrl} from Google Cloud Storage", fileUrl);
            }
            return false;
        }
    }
}
