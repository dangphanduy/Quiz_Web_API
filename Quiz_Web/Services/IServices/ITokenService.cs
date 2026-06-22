using Quiz_Web.Models.Entities;
using System.Threading.Tasks;

namespace Quiz_Web.Services.IServices
{
    public interface ITokenService
    {
        string GenerateJwtToken(User user);
        Task<UserClaimData?> VerifyExternalTokenAsync(string provider, string token);
    }

    public class UserClaimData
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }
}
