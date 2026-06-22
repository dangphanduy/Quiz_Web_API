using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Quiz_Web.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public TokenService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var secretKey = jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Secret Key is not configured.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role?.Name ?? "User")
            };

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["DurationInMinutes"] ?? "180")),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<UserClaimData?> VerifyExternalTokenAsync(string provider, string token)
        {
            try
            {
                provider = provider.ToLower().Trim();
                if (provider == "google")
                {
                    var response = await _httpClient.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={token}");
                    if (!response.IsSuccessStatusCode) return null;

                    var jsonString = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(jsonString);

                    // Verify aud (audience) matches our Google Client ID to protect the backend
                    var aud = data["aud"]?.ToString();
                    var configClientId = _configuration["Authentication:Google:ClientId"];
                    if (!string.IsNullOrEmpty(configClientId) && aud != configClientId)
                    {
                        return null; // Unauthorized: Token issued for a different client app
                    }

                    return new UserClaimData
                    {
                        Email = data["email"]?.ToString() ?? string.Empty,
                        FullName = data["name"]?.ToString() ?? data["email"]?.ToString() ?? "Google User",
                        AvatarUrl = data["picture"]?.ToString()
                    };
                }
                else if (provider == "microsoft")
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode) return null;

                    var jsonString = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(jsonString);

                    string email = data["mail"]?.ToString() ?? data["userPrincipalName"]?.ToString() ?? string.Empty;
                    string name = data["displayName"]?.ToString() ?? email;

                    return new UserClaimData
                    {
                        Email = email,
                        FullName = name,
                        AvatarUrl = null // Graph API photo requires a separate stream call, so we default to null
                    };
                }
                else if (provider == "facebook")
                {
                    var response = await _httpClient.GetAsync($"https://graph.facebook.com/me?fields=id,name,email,picture.type(large)&access_token={token}");
                    if (!response.IsSuccessStatusCode) return null;

                    var jsonString = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(jsonString);

                    string email = data["email"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(email))
                    {
                        email = $"{data["id"]?.ToString()}@facebook.com"; // Fallback if email permission not granted
                    }

                    return new UserClaimData
                    {
                        Email = email,
                        FullName = data["name"]?.ToString() ?? "Facebook User",
                        AvatarUrl = data["picture"]?["data"]?["url"]?.ToString()
                    };
                }
            }
            catch
            {
                // Log exception if necessary
            }

            return null;
        }
    }
}
