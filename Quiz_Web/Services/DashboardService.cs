using Quiz_Web.Models.EF;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly LearningPlatformContext _context;

        public DashboardService(LearningPlatformContext context)
        {
            _context = context;
        }

        public DashboardOverviewViewModel GetOverviewData()
        {
            var totalUsers = _context.Users.Count();
            var totalCourses = _context.Courses.Count();
            var totalTests = _context.Tests.Count();
            
            // ✅ SỬA: Tính 40% tổng doanh thu cho admin (phí nền tảng)
            var totalPayments = _context.Payments.Where(p => p.Status == "Paid").Sum(p => (decimal?)p.Amount) ?? 0;
            var totalRevenue = totalPayments * 0.40m; // Admin nhận 40%, instructor nhận 60%

            // ✅ SỬA: Lấy dữ liệu từ 6 tháng trước để có đủ điểm cho đường biểu đồ
            var sixMonthsAgo = DateTimeHelper.Now.AddMonths(-6);
            var userGrowthDataFromDb = _context.Users
                .Where(u => u.CreatedAt >= sixMonthsAgo)
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToList();

            // ✅ Tạo đầy đủ 6 tháng với giá trị 0 cho tháng không có dữ liệu
            var userGrowthData = GenerateMonthlyData(sixMonthsAgo, DateTimeHelper.Now, userGrowthDataFromDb, (data, y, m) => 
                data.FirstOrDefault(x => x.Year == y && x.Month == m)?.Count ?? 0);

            // ✅ SỬA: Tính 40% doanh thu theo tháng cho admin với 6 tháng dữ liệu
            var revenueDataFromDb = _context.Payments
                .Where(p => p.Status == "Paid" && p.PaidAt.HasValue && p.PaidAt >= sixMonthsAgo)
                .GroupBy(p => new { p.PaidAt.Value.Year, p.PaidAt.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
                .ToList();

            // ✅ Tạo đầy đủ 6 tháng với giá trị 0 cho tháng không có dữ liệu
            var revenueData = GenerateMonthlyData(sixMonthsAgo, DateTimeHelper.Now, revenueDataFromDb, (data, y, m) =>
            {
                var monthData = data.FirstOrDefault(x => x.Year == y && x.Month == m);
                return monthData != null ? monthData.Total * 0.40m : 0; // Admin nhận 40%
            });

            return new DashboardOverviewViewModel
            {
                TotalUsers = totalUsers,
                TotalCourses = totalCourses,
                TotalTests = totalTests,
                TotalRevenue = totalRevenue,
                UserGrowthData = userGrowthData,
                RevenueData = revenueData
            };
        }

        public UserAnalyticsViewModel GetUserAnalytics()
        {
            var roleData = _context.Users
                .Join(_context.Roles, u => u.RoleId, r => r.RoleId, (u, r) => r.Name)
                .GroupBy(roleName => roleName)
                .Select(g => new { RoleName = g.Key, Count = g.Count() })
                .ToList();

            var colors = new[] { "#dc3545", "#28a745", "#007bff", "#ffc107", "#17a2b8", "#fd7e14", "#6f42c1" };
            var usersByRole = roleData.Select((g, index) => new PieChartData
            {
                Label = g.RoleName ?? "Unknown",
                Value = g.Count,
                Color = index < colors.Length ? colors[index] : "#6c757d"
            }).ToList();

            // ✅ SỬA: Luôn hiển thị 12 tháng để thấy rõ xu hướng
            var twelveMonthsAgo = DateTimeHelper.Now.AddMonths(-12);
            var newUsersDataFromDb = _context.Users
                .Where(u => u.CreatedAt >= twelveMonthsAgo)
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToList();

            var newUsersPerMonth = GenerateMonthlyData(twelveMonthsAgo, DateTimeHelper.Now, newUsersDataFromDb, (data, y, m) =>
                data.FirstOrDefault(x => x.Year == y && x.Month == m)?.Count ?? 0);

            // ✅ SỬA: Active users - hiển thị 30 ngày với đầy đủ các ngày
            var thirtyDaysAgo = DateTimeHelper.Now.AddDays(-30).Date;
            var activeUsersDataFromDb = _context.TestAttempts
                .Where(ta => ta.StartedAt >= thirtyDaysAgo)
                .GroupBy(ta => ta.StartedAt.Date)
                .Select(g => new { Date = g.Key, UserIds = g.Select(ta => ta.UserId).Distinct() })
                .ToList();

            var activeUsersData = GenerateDailyData(thirtyDaysAgo, DateTimeHelper.Now.Date, activeUsersDataFromDb, (data, date) =>
            {
                var dayData = data.FirstOrDefault(x => x.Date == date);
                return dayData?.UserIds.Count() ?? 0;
            });

            return new UserAnalyticsViewModel
            {
                UsersByRole = usersByRole,
                NewUsersPerMonth = newUsersPerMonth,
                ActiveUsersData = activeUsersData,
                TotalActiveUsers = _context.TestAttempts.Where(ta => ta.StartedAt >= DateTimeHelper.Now.AddDays(-30)).Select(ta => ta.UserId).Distinct().Count(),
                NewUsersThisMonth = _context.Users.Count(u => u.CreatedAt.Month == DateTimeHelper.Now.Month && u.CreatedAt.Year == DateTimeHelper.Now.Year)
            };
        }

        public LearningActivitiesViewModel GetLearningActivities()
        {
            // ✅ SỬA: Hiển thị 6 tháng đầy đủ
            var sixMonthsAgo = DateTimeHelper.Now.AddMonths(-6);
            
            var testsCompletedDataFromDb = _context.TestAttempts
                .Where(ta => ta.SubmittedAt.HasValue && ta.SubmittedAt >= sixMonthsAgo)
                .GroupBy(ta => new { ta.SubmittedAt.Value.Year, ta.SubmittedAt.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToList();

            var testsCompletedData = GenerateMonthlyData(sixMonthsAgo, DateTimeHelper.Now, testsCompletedDataFromDb, (data, y, m) =>
                data.FirstOrDefault(x => x.Year == y && x.Month == m)?.Count ?? 0);

            var coursesEnrolledDataFromDb = _context.CoursePurchases
                .Where(cp => cp.PurchasedAt >= sixMonthsAgo)
                .GroupBy(cp => new { cp.PurchasedAt.Year, cp.PurchasedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToList();

            var coursesEnrolledData = GenerateMonthlyData(sixMonthsAgo, DateTimeHelper.Now, coursesEnrolledDataFromDb, (data, y, m) =>
                data.FirstOrDefault(x => x.Year == y && x.Month == m)?.Count ?? 0);

            var popularCourses = _context.CoursePurchases
                .Join(_context.Courses, cp => cp.CourseId, c => c.CourseId, (cp, c) => c.Title)
                .GroupBy(title => title)
                .Select(g => new { Title = g.Key, Count = g.Count() })
                .ToList()
                .Select(g => new BarChartData
                {
                    Label = g.Title ?? "Unknown",
                    Value = g.Count,
                    Category = "Enrollments"
                }).OrderByDescending(x => x.Value).Take(10).ToList();

            return new LearningActivitiesViewModel
            {
                TestsCompletedData = testsCompletedData,
                CoursesEnrolledData = coursesEnrolledData,
                PopularCourses = popularCourses,
                TotalTestsCompleted = _context.TestAttempts.Count(ta => ta.SubmittedAt.HasValue),
                TotalEnrollments = _context.CoursePurchases.Count()
            };
        }

        public RevenuePaymentsViewModel GetRevenuePayments()
        {
            // ✅ SỬA: Tính 40% doanh thu hàng tháng cho admin với 12 tháng đầy đủ
            var twelveMonthsAgo = DateTimeHelper.Now.AddMonths(-12);
            var monthlyRevenueFromDb = _context.Payments
                .Where(p => p.Status == "Paid" && p.PaidAt.HasValue && p.PaidAt >= twelveMonthsAgo)
                .GroupBy(p => new { p.PaidAt.Value.Year, p.PaidAt.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
                .ToList();

            var monthlyRevenue = GenerateMonthlyData(twelveMonthsAgo, DateTimeHelper.Now, monthlyRevenueFromDb, (data, y, m) =>
            {
                var monthData = data.FirstOrDefault(x => x.Year == y && x.Month == m);
                return monthData != null ? monthData.Total * 0.40m : 0; // Admin nhận 40%
            });

            // ✅ SỬA: Tính 40% doanh thu theo phương thức thanh toán cho admin
            var revenueBySource = _context.Payments
                .Where(p => p.Status == "Paid")
                .GroupBy(p => p.Provider ?? "Unknown")
                .Select(g => new { Provider = g.Key, Total = g.Sum(p => p.Amount) })
                .ToList()
                .Select(g => new PieChartData
                {
                    Label = g.Provider,
                    Value = g.Total * 0.40m, // Admin nhận 40%
                    Color = GetPaymentMethodColor(g.Provider)
                }).ToList();

            // ✅ SỬA: Tính 40% doanh thu cho từng khóa học (phần của admin)
            // Lấy giá gốc từ Courses thay vì PricePaid để tránh trường hợp = 0
            var topSellingCourses = _context.CoursePurchases
                .Where(cp => cp.Status == "Paid") // ✅ Chỉ lấy purchase đã thanh toán
                .Join(_context.Courses, cp => cp.CourseId, c => c.CourseId, (cp, c) => new { 
                    c.Title, 
                    ActualPrice = cp.PricePaid > 0 ? cp.PricePaid : c.Price // ✅ Nếu PricePaid = 0 thì lấy giá gốc
                })
                .GroupBy(x => x.Title)
                .Select(g => new { 
                    Title = g.Key, 
                    Total = g.Sum(x => x.ActualPrice),
                    Count = g.Count() // ✅ Đếm số lượng mua
                })
                .ToList()
                .Select(g => new BarChartData
                {
                    Label = $"{g.Title ?? "Unknown"} ({g.Count} mua)", // ✅ Hiển thị số lượng mua
                    Value = g.Total * 0.40m, // Admin nhận 40%
                    Category = "Revenue"
                }).OrderByDescending(x => x.Value).Take(10).ToList();

            // ✅ SỬA: Tính 40% doanh thu tháng hiện tại và tháng trước
            var currentMonthTotal = _context.Payments
                .Where(p => p.Status == "Paid" && p.PaidAt.HasValue && p.PaidAt.Value.Month == DateTimeHelper.Now.Month && p.PaidAt.Value.Year == DateTimeHelper.Now.Year)
                .Sum(p => (decimal?)p.Amount) ?? 0;
            var currentMonthRevenue = currentMonthTotal * 0.40m; // Admin nhận 40%

            var lastMonthTotal = _context.Payments
                .Where(p => p.Status == "Paid" && p.PaidAt.HasValue && p.PaidAt.Value.Month == DateTimeHelper.Now.AddMonths(-1).Month && p.PaidAt.Value.Year == DateTimeHelper.Now.AddMonths(-1).Year)
                .Sum(p => (decimal?)p.Amount) ?? 0;
            var lastMonthRevenue = lastMonthTotal * 0.40m; // Admin nhận 40%

            var monthlyGrowth = lastMonthRevenue > 0 ? ((currentMonthRevenue - lastMonthRevenue) / lastMonthRevenue) * 100 : 0;

            // ✅ SỬA: Tính 40% tổng doanh thu cho admin
            var totalPayments = _context.Payments.Where(p => p.Status == "Paid").Sum(p => (decimal?)p.Amount) ?? 0;
            var totalRevenue = totalPayments * 0.40m; // Admin nhận 40%

            return new RevenuePaymentsViewModel
            {
                MonthlyRevenue = monthlyRevenue,
                RevenueBySource = revenueBySource,
                TopSellingCourses = topSellingCourses,
                TotalRevenue = totalRevenue,
                MonthlyGrowth = monthlyGrowth
            };
        }

        public LearningResultsViewModel GetLearningResults()
        {
            var testScores = _context.TestAttempts
                .Where(ta => ta.Score.HasValue)
                .Select(ta => ta.Score.Value >= 80 ? "Excellent" : ta.Score.Value >= 60 ? "Good" : "Needs Improvement")
                .GroupBy(category => category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToList()
                .Select(g => new BarChartData
                {
                    Label = g.Category,
                    Value = g.Count,
                    Category = "Test Results"
                }).ToList();

            var certificatesByType = _context.Certificates
                .Join(_context.Courses, c => c.CourseId, course => course.CourseId, (c, course) => course.Title ?? "General")
                .GroupBy(title => title)
                .Select(g => new { Title = g.Key, Count = g.Count() })
                .ToList()
                .Select(g => new PieChartData
                {
                    Label = g.Title,
                    Value = g.Count,
                    Color = GetCategoryColor(g.Title)
                }).ToList();

            var topPerformers = _context.TestAttempts
                .Where(ta => ta.Score.HasValue)
                .Join(_context.Users, ta => ta.UserId, u => u.UserId, (ta, u) => new { u.FullName, ta.Score })
                .GroupBy(x => x.FullName)
                .Select(g => new { Name = g.Key, AvgScore = g.Average(x => x.Score ?? 0) })
                .ToList()
                .Select(g => new TableSummaryData
                {
                    Name = g.Name ?? "Unknown",
                    Value = g.AvgScore.ToString("F1"),
                    Status = g.AvgScore >= 80 ? "Excellent" : "Good",
                    Date = DateTimeHelper.Now
                }).OrderByDescending(x => decimal.Parse(x.Value)).Take(10).ToList();

            var avgScore = _context.TestAttempts.Where(ta => ta.Score.HasValue).Select(ta => ta.Score.Value).DefaultIfEmpty(0).Average();

            return new LearningResultsViewModel
            {
                TestScores = testScores,
                CertificatesByType = certificatesByType,
                TopPerformers = topPerformers,
                AverageScore = (double)avgScore,
                TotalCertificates = _context.Certificates.Count()
            };
        }

        public SystemActivityViewModel GetSystemActivity()
        {
            var loginActivity = _context.AuditLogs
                .Where(al => al.Action == "Login" && al.CreatedAt >= DateTimeHelper.Now.AddDays(-7))
                .GroupBy(al => al.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToList()
                .Select(g => new BarChartData
                {
                    Label = g.Date.ToString("dd/MM"),
                    Value = g.Count,
                    Category = "Logins"
                }).OrderBy(x => x.Label).ToList();

            var systemLogs = _context.ErrorLogs
                .OrderByDescending(el => el.CreatedAt)
                .Take(10)
                .Select(el => new { el.Message, el.Severity, el.CreatedAt })
                .ToList()
                .Select(el => new TableSummaryData
                {
                    Name = el.Message ?? "Unknown Error",
                    Value = el.Severity ?? "Info",
                    Status = el.Severity == "Error" ? "Critical" : "Normal",
                    Date = el.CreatedAt
                }).ToList();

            var errorRates = _context.ErrorLogs
                .Where(el => el.CreatedAt >= DateTimeHelper.Now.AddDays(-7))
                .GroupBy(el => el.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToList()
                .Select(g => new ChartDataPoint
                {
                    Label = g.Date.ToString("dd/MM"),
                    Value = g.Count,
                    Date = g.Date
                }).OrderBy(x => x.Date).ToList();

            return new SystemActivityViewModel
            {
                LoginActivity = loginActivity,
                SystemLogs = systemLogs,
                ErrorRates = errorRates,
                TotalLogins = _context.AuditLogs.Count(al => al.Action == "Login" && al.CreatedAt >= DateTimeHelper.Now.AddDays(-30)),
                SystemErrors = _context.ErrorLogs.Count(el => el.CreatedAt >= DateTimeHelper.Now.AddDays(-30))
            };
        }

        // ✅ Helper method: Tạo dữ liệu đầy đủ theo tháng
        private List<ChartDataPoint> GenerateMonthlyData<T>(
            DateTime startDate, 
            DateTime endDate, 
            List<T> dataFromDb, 
            Func<List<T>, int, int, decimal> getValue)
        {
            var result = new List<ChartDataPoint>();
            var current = new DateTime(startDate.Year, startDate.Month, 1);
            var end = new DateTime(endDate.Year, endDate.Month, 1);

            while (current <= end)
            {
                result.Add(new ChartDataPoint
                {
                    Label = $"{current.Month}/{current.Year}",
                    Value = getValue(dataFromDb, current.Year, current.Month),
                    Date = current
                });
                current = current.AddMonths(1);
            }

            return result;
        }

        // ✅ Helper method: Tạo dữ liệu đầy đủ theo ngày
        private List<ChartDataPoint> GenerateDailyData<T>(
            DateTime startDate,
            DateTime endDate,
            List<T> dataFromDb,
            Func<List<T>, DateTime, decimal> getValue)
        {
            var result = new List<ChartDataPoint>();
            var current = startDate;

            while (current <= endDate)
            {
                result.Add(new ChartDataPoint
                {
                    Label = current.ToString("dd/MM"),
                    Value = getValue(dataFromDb, current),
                    Date = current
                });
                current = current.AddDays(1);
            }

            return result;
        }

        private string GetRoleColor(string? roleName)
        {
            var role = roleName?.Trim().ToLower();
            return role switch
            {
                "admin" => "#dc3545",     // Đỏ
                "teacher" => "#28a745",   // Xanh lá
                "student" => "#007bff",   // Xanh dương
                "moderator" => "#ffc107", // Vàng
                "guest" => "#17a2b8",     // Xanh cyan
                "instructor" => "#fd7e14", // Cam
                "manager" => "#6f42c1",   // Tím
                _ => "#6c757d"             // Xám
            };
        }

        private string GetPaymentMethodColor(string method) => method switch
        {
            "paypal" => "#ffc107",
            "stripe" => "#007bff",
            "vnpay" => "#28a745",
            "momo" => "#e91e63",
            _ => "#6c757d"
        };

        private string GetCategoryColor(string category) => category switch
        {
            "Technology" => "#007bff",
            "Business" => "#28a745",
            "Design" => "#dc3545",
            "Language" => "#ffc107",
            "Science" => "#17a2b8",
            _ => "#6c757d"
        };
    }
}