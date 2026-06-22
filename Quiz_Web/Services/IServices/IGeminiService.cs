using System.Threading.Tasks;

namespace Quiz_Web.Services.IServices;

public interface IGeminiService
{
    Task<string> GetResponseAsync(string prompt, string context, string? pdfUrl = null, string? systemInstruction = null);
}
