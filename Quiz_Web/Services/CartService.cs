using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services
{
    public class CartService : ICartService
    {
        private readonly LearningPlatformContext _context;
        private readonly ILogger<CartService> _logger;
        private readonly ICourseAccessService _courseAccessService;

        public CartService(
            LearningPlatformContext context,
            ILogger<CartService> logger,
            ICourseAccessService courseAccessService)
        {
            _context = context;
            _logger = logger;
            _courseAccessService = courseAccessService;
        }

        public async Task<ShoppingCart?> GetOrCreateCartAsync(int userId)
        {
            try
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.CartItems)
                        .ThenInclude(ci => ci.Course)
                            .ThenInclude(c => c.Owner)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                {
                    cart = new ShoppingCart
                    {
                        UserId = userId,
                        CreatedAt = DateTimeHelper.Now
                    };
                    _context.ShoppingCarts.Add(cart);
                    await _context.SaveChangesAsync();
                }

                return cart;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting or creating cart for user {UserId}", userId);
                return null;
            }
        }

        public async Task<List<CartItem>> GetCartItemsAsync(int userId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                if (cart == null) return new List<CartItem>();

                return await _context.CartItems
                    .Include(ci => ci.Course)
                        .ThenInclude(c => c.Owner)
                    .Where(ci => ci.CartId == cart.CartId)
                    .OrderByDescending(ci => ci.AddedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart items for user {UserId}", userId);
                return new List<CartItem>();
            }
        }

        public async Task<int> GetCartItemCountAsync(int userId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                if (cart == null) return 0;

                return await _context.CartItems
                    .CountAsync(ci => ci.CartId == cart.CartId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart item count for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<decimal> GetCartTotalAsync(int userId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                if (cart == null) return 0;

                return await _context.CartItems
                    .Where(ci => ci.CartId == cart.CartId)
                    .Include(ci => ci.Course)
                    .SumAsync(ci => ci.Course.Price);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating cart total for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<bool> AddToCartAsync(int userId, int courseId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                if (cart == null) return false;

                // Check if already in cart
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cart.CartId && ci.CourseId == courseId);

                if (existingItem != null)
                {
                    _logger.LogWarning("Course {CourseId} already in cart for user {UserId}", courseId, userId);
                    return false;
                }

                // Check if course exists and is published
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.CourseId == courseId && c.IsPublished);

                if (course == null)
                {
                    _logger.LogWarning("Course {CourseId} not found or not published", courseId);
                    return false;
                }

                // Check if user is the owner of the course
                if (course.OwnerId == userId)
                {
                    _logger.LogWarning("User {UserId} cannot add their own course {CourseId} to cart", userId, courseId);
                    return false;
                }

                if (course.Price <= 0)
                {
                    _logger.LogWarning("Free course {CourseId} should not be added to cart for user {UserId}", courseId, userId);
                    return false;
                }

                var alreadyPurchased = await _courseAccessService.CheckCourseAccessAsync(userId, courseId);

                if (alreadyPurchased)
                {
                    _logger.LogWarning("User {UserId} already purchased course {CourseId}", userId, courseId);
                    return false;
                }

                var cartItem = new CartItem
                {
                    CartId = cart.CartId,
                    CourseId = courseId,
                    AddedAt = DateTimeHelper.Now
                };

                _context.CartItems.Add(cartItem);
                cart.UpdatedAt = DateTimeHelper.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Added course {CourseId} to cart for user {UserId}", courseId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding course {CourseId} to cart for user {UserId}", courseId, userId);
                return false;
            }
        }

        public async Task<bool> RemoveFromCartAsync(int userId, int courseId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                if (cart == null) return false;

                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cart.CartId && ci.CourseId == courseId);

                if (cartItem == null)
                {
                    _logger.LogWarning("Cart item not found for course {CourseId} and user {UserId}", courseId, userId);
                    return false;
                }

                _context.CartItems.Remove(cartItem);
                cart.UpdatedAt = DateTimeHelper.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Removed course {CourseId} from cart for user {UserId}", courseId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing course {CourseId} from cart for user {UserId}", courseId, userId);
                return false;
            }
        }

        public async Task<bool> ClearCartAsync(int userId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                if (cart == null) return false;

                var cartItems = await _context.CartItems
                    .Where(ci => ci.CartId == cart.CartId)
                    .ToListAsync();

                _context.CartItems.RemoveRange(cartItems);
                cart.UpdatedAt = DateTimeHelper.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleared cart for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> IsInCartAsync(int userId, int courseId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                if (cart == null) return false;

                return await _context.CartItems
                    .AnyAsync(ci => ci.CartId == cart.CartId && ci.CourseId == courseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if course {CourseId} is in cart for user {UserId}", courseId, userId);
                return false;
            }
        }
    }
}
