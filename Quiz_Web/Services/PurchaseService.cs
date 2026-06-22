using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly LearningPlatformContext _context;
        private readonly ILogger<PurchaseService> _logger;

        public PurchaseService(LearningPlatformContext context, ILogger<PurchaseService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> HasUserPurchasedCourseAsync(int userId, int courseId)
        {
            return await _context.CoursePurchases
                .AnyAsync(cp => cp.BuyerId == userId && 
                               cp.CourseId == courseId && 
                               cp.Status == "Paid");
        }

		//public async Task<CoursePurchase> CreatePurchaseAsync(int userId, List<int> courseIds, decimal totalAmount)
		//{
		//    using var transaction = await _context.Database.BeginTransactionAsync();
		//    try
		//    {
		//        // Tạo purchase chính cho course đầu tiên
		//        var mainPurchase = new CoursePurchase
		//        {
		//            BuyerId = userId,
		//            CourseId = courseIds.First(),
		//            PricePaid = totalAmount,
		//            Currency = "VND",
		//            Status = "Pending",
		//            PurchasedAt = DateTimeHelper.Now
		//        };

		//        _context.CoursePurchases.Add(mainPurchase);
		//        await _context.SaveChangesAsync();

		//        // Tạo purchase cho các course còn lại (nếu có)
		//        foreach (var courseId in courseIds.Skip(1))
		//        {
		//            var purchase = new CoursePurchase
		//            {
		//                BuyerId = userId,
		//                CourseId = courseId,
		//                PricePaid = 0, // Đã tính trong purchase chính
		//                Currency = "VND",
		//                Status = "Pending",
		//                PurchasedAt = DateTimeHelper.Now
		//            };
		//            _context.CoursePurchases.Add(purchase);
		//        }

		//        await _context.SaveChangesAsync();
		//        await transaction.CommitAsync();

		//        return mainPurchase;
		//    }
		//    catch (Exception ex)
		//    {
		//        await transaction.RollbackAsync();
		//        _logger.LogError(ex, "Error creating purchase for user {UserId}", userId);
		//        throw;
		//    }
		//}
		public async Task<List<CoursePurchase>> CreatePurchaseAsync(int userId,Dictionary<int, decimal> coursePrices)
		{
			using var transaction = await _context.Database.BeginTransactionAsync();

			try
			{
				var newPurchases = new List<CoursePurchase>();
				var now = DateTimeHelper.Now;

				foreach (var item in coursePrices)
				{
					var courseId = item.Key;
					var price = item.Value;

					// Tạo purchase riêng cho từng khóa học
					var purchase = new CoursePurchase
					{
						BuyerId = userId,
						CourseId = courseId,
						PricePaid = price,
						Currency = "VND",
						Status = "Pending",
						PurchasedAt = now
					};

					newPurchases.Add(purchase);
					_context.CoursePurchases.Add(purchase);
				}

				await _context.SaveChangesAsync();
				await transaction.CommitAsync();

				return newPurchases;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error creating multi-course purchase for user {UserId}", userId);
				throw;
			}
		}
		public async Task<bool> CompletePurchaseAsync(int purchaseId, string providerRef)
        {
            try
            {
                var purchase = await _context.CoursePurchases
                    .FirstOrDefaultAsync(p => p.PurchaseId == purchaseId);

                if (purchase == null)
                {
                    _logger.LogWarning("Purchase not found: {PurchaseId}", purchaseId);
                    return false;
                }

                purchase.Status = "Paid";

                // Cập nhật tất cả purchase cùng user và cùng thời điểm (trong vòng 1 phút)
                var relatedPurchases = await _context.CoursePurchases
                    .Where(p => p.BuyerId == purchase.BuyerId &&
                               p.Status == "Pending" &&
                               Math.Abs(EF.Functions.DateDiffSecond(p.PurchasedAt, purchase.PurchasedAt)) <= 60)
                    .ToListAsync();

                foreach (var relatedPurchase in relatedPurchases)
                {
                    relatedPurchase.Status = "Paid";
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing purchase {PurchaseId}", purchaseId);
                return false;
            }
        }

        public async Task<List<CoursePurchase>> GetUserPurchasesAsync(int userId)
        {
            return await _context.CoursePurchases
                .Include(cp => cp.Course)
                .Where(cp => cp.BuyerId == userId && cp.Status == "Paid")
                .OrderByDescending(cp => cp.PurchasedAt)
                .ToListAsync();
        }
		public async Task GrantAccessAsync(int userId, int courseId)
		{
			var exists = await _context.CoursePurchases
				.AnyAsync(x => x.BuyerId == userId && x.CourseId == courseId && x.Status == "Paid");

			if (!exists)
			{
				// ✅ LẤY GIÁ KHÓA HỌC THỰC TẾ thay vì hardcode = 0
				var course = await _context.Courses.FindAsync(courseId);
				var price = course?.Price ?? 0;

				_context.CoursePurchases.Add(new CoursePurchase
				{
					BuyerId = userId,
					CourseId = courseId,
					PricePaid = price, // ✅ Lưu giá thực của khóa học
					Currency = "VND",
					Status = "Paid",
					PurchasedAt = DateTimeHelper.Now // ✅ Thêm thời gian mua
				});

				await _context.SaveChangesAsync();
			}
		}
	}
}