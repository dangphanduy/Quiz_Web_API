using Microsoft.AspNetCore.Hosting;
using Quiz_Web.Services.IServices;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Quiz_Web.Services
{
    public class CertificateService : ICertificateService
    {
        private readonly IWebHostEnvironment _env;

        public CertificateService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public Task<byte[]> GenerateCertificateImageAsync(string studentName, string courseName, string instructorName, DateTime issuedAt, string verifyCode, string serial)
        {
            try
            {
                // Create canvas (1200x850 pixels)
                using var bitmap = new SKBitmap(1200, 850);
                using var canvas = new SKCanvas(bitmap);

                // Clear canvas with very light gray background (Udemy style)
                canvas.Clear(new SKColor(248, 249, 250));

                // Draw an elegant outer border
                using (var borderPaint = new SKPaint
                {
                    Color = new SKColor(232, 235, 237),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2,
                    IsAntialias = true
                })
                {
                    canvas.DrawRect(SKRect.Create(25, 25, 1200 - 50, 850 - 50), borderPaint);
                }

                // 1. Draw Logo
                var logoPath = Path.Combine(_env.WebRootPath, "asset", "image", "logo", "logo.png");
                if (File.Exists(logoPath))
                {
                    try
                    {
                        using var logo = SKBitmap.Decode(logoPath);
                        if (logo != null)
                        {
                            var logoHeight = 55;
                            var logoWidth = (int)((float)logo.Width / logo.Height * logoHeight);
                            canvas.DrawBitmap(logo, SKRect.Create(80, 80, logoWidth, logoHeight));
                        }
                        else
                        {
                            DrawLogoTextFallback(canvas);
                        }
                    }
                    catch
                    {
                        DrawLogoTextFallback(canvas);
                    }
                }
                else
                {
                    DrawLogoTextFallback(canvas);
                }

                // 2. Draw Certificate Info (Top Right)
                using (var rightTextPaint = new SKPaint
                {
                    Color = new SKColor(106, 111, 115),
                    TextSize = 13,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Right,
                    Typeface = SKTypeface.FromFamilyName("Arial")
                })
                {
                    canvas.DrawText($"Số giấy chứng nhận: {verifyCode}", 1120, 95, rightTextPaint);
                    canvas.DrawText($"Số tham chiếu: {serial}", 1120, 120, rightTextPaint);
                }

                // 3. Draw Subtitle: "GIẤY CHỨNG NHẬN HOÀN THÀNH"
                using (var certTitlePaint = new SKPaint
                {
                    Color = new SKColor(106, 111, 115),
                    TextSize = 16,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                })
                {
                    canvas.DrawText("GIẤY CHỨNG NHẬN HOÀN THÀNH", 80, 250, certTitlePaint);
                }

                // 4. Draw Course Title (Wrapped and scaled if needed)
                using (var courseTitlePaint = new SKPaint
                {
                    Color = new SKColor(28, 29, 31),
                    TextSize = 42,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                })
                {
                    var maxTitleWidth = 1040;
                    var titleLines = WrapText(courseName, maxTitleWidth, courseTitlePaint);
                    float currentY = 320;

                    foreach (var line in titleLines)
                    {
                        canvas.DrawText(line, 80, currentY, courseTitlePaint);
                        currentY += 55;
                    }

                    // 5. Draw Instructor
                    using (var instructorPaint = new SKPaint
                    {
                        Color = new SKColor(28, 29, 31),
                        TextSize = 16,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Arial")
                    })
                    {
                        canvas.DrawText($"Giảng viên: {instructorName}", 80, currentY + 15, instructorPaint);
                    }
                }

                // 6. Draw Student Name (Bottom Left)
                using (var studentNamePaint = new SKPaint
                {
                    Color = new SKColor(28, 29, 31),
                    TextSize = 44,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                })
                {
                    canvas.DrawText(studentName, 80, 680, studentNamePaint);
                }

                // 7. Draw Issue Date
                using (var datePaint = new SKPaint
                {
                    Color = new SKColor(28, 29, 31),
                    TextSize = 16,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial")
                })
                {
                    string formattedDate = $"Ngày {issuedAt.Day} tháng {issuedAt.Month} năm {issuedAt.Year}";
                    canvas.DrawText(formattedDate, 80, 725, datePaint);
                }

                // Encode the image to bytes (PNG)
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                
                return Task.FromResult(data.ToArray());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to generate certificate image", ex);
            }
        }

        private void DrawLogoTextFallback(SKCanvas canvas)
        {
            using var logoPaint = new SKPaint
            {
                Color = new SKColor(164, 53, 240), // Purple color
                TextSize = 34,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };
            canvas.DrawText("ymedu", 80, 120, logoPaint);
        }

        private List<string> WrapText(string text, float maxWidth, SKPaint paint)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                var width = paint.MeasureText(testLine);
                if (width > maxWidth)
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                    }
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines;
        }
    }
}
