using System;
using System.Threading.Tasks;

namespace Quiz_Web.Services.IServices
{
    public interface ICertificateService
    {
        Task<byte[]> GenerateCertificateImageAsync(string studentName, string courseName, string instructorName, DateTime issuedAt, string verifyCode, string serial);
    }
}
