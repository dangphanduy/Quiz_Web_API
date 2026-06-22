using Xunit;
using Quiz_Web.Utils;

namespace Quiz_Web.Tests
{
    public class ValidationTests
    {
        [Theory]
        [InlineData("test@example.com", true)]
        [InlineData("user.name+tag@domain.co.uk", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("invalid-email", false)]
        [InlineData("invalid@domain", false)]
        [InlineData("@domain.com", false)]
        [InlineData("test@.com", false)]
        public void IsValidEmail_ShouldValidateCorrectly(string? email, bool expected)
        {
            // Act
            bool result = Validation.IsValidEmail(email!);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("usr", true)]
        [InlineData("user123", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("  ", false)]
        [InlineData("ab", false)]
        public void IsValidUsername_ShouldValidateCorrectly(string? username, bool expected)
        {
            // Act
            bool result = Validation.IsValidUsername(username!);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsValidUsername_BoundaryTest()
        {
            // Arrange
            string validMax = new string('a', 100);
            string invalidMax = new string('a', 101);

            // Assert
            Assert.True(Validation.IsValidUsername(validMax));
            Assert.False(Validation.IsValidUsername(invalidMax));
        }

        [Theory]
        [InlineData("Valid123!", true)]
        [InlineData("Password123#", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("Short1!", false)] // < 8 chars
        [InlineData("noupper123!", false)] // no uppercase
        [InlineData("NOLOWER123!", false)] // no lowercase
        [InlineData("NoDigit!!", false)] // no digit
        [InlineData("NoSpecial123", false)] // no special char
        public void IsValidPassword_ShouldValidateCorrectly(string? password, bool expected)
        {
            // Act
            bool result = Validation.IsValidPassword(password!);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("John Doe", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        public void IsValidFullName_ShouldValidateCorrectly(string? fullName, bool expected)
        {
            // Act
            bool result = Validation.IsValidFullName(fullName!);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsValidFullName_BoundaryTest()
        {
            // Arrange
            string validMax = new string('A', 200);
            string invalidMax = new string('A', 201);

            // Assert
            Assert.True(Validation.IsValidFullName(validMax));
            Assert.False(Validation.IsValidFullName(invalidMax));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", true)]
        [InlineData("1234567890", true)]
        [InlineData("123-456-7890", true)]
        [InlineData("+1 (123) 456-7890", true)]
        [InlineData("abc", false)]
        [InlineData("123456789012345678901", false)] // > 20 chars
        public void IsValidPhone_ShouldValidateCorrectly(string? phone, bool expected)
        {
            // Act
            bool result = Validation.IsValidPhone(phone!);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("A Valid Title", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        public void IsValidTitle_ShouldValidateCorrectly(string? title, bool expected)
        {
            // Act
            bool result = Validation.IsValidTitle(title!);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsValidTitle_BoundaryTest()
        {
            // Arrange
            string validMax = new string('A', 200);
            string invalidMax = new string('A', 201);

            // Assert
            Assert.True(Validation.IsValidTitle(validMax));
            Assert.False(Validation.IsValidTitle(invalidMax));
        }

        [Theory]
        [InlineData("valid-slug-123", true)]
        [InlineData("slug", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("invalid_slug", false)]
        [InlineData("invalid slug", false)]
        [InlineData("Invalid-Slug", false)]
        public void IsValidSlug_ShouldValidateCorrectly(string? slug, bool expected)
        {
            // Act
            bool result = Validation.IsValidSlug(slug!);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsValidSlug_BoundaryTest()
        {
            // Arrange
            string validMax = new string('a', 200);
            string invalidMax = new string('a', 201);

            // Assert
            Assert.True(Validation.IsValidSlug(validMax));
            Assert.False(Validation.IsValidSlug(invalidMax));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData(0.0, true)]
        [InlineData(10.5, true)]
        [InlineData(-0.01, false)]
        [InlineData(-100.0, false)]
        public void IsValidPrice_ShouldValidateCorrectly(double? priceVal, bool expected)
        {
            // Arrange
            decimal? price = priceVal == null ? null : (decimal)priceVal.Value;

            // Act
            bool result = Validation.IsValidPrice(price);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("http://example.com", true)]
        [InlineData("https://www.google.com/search?q=dotnet", true)]
        [InlineData("invalid-url", false)]
        [InlineData("www.google.com", false)] // not absolute URL (missing scheme)
        public void IsValidUrl_ShouldValidateCorrectly(string? url, bool expected)
        {
            // Act
            bool result = Validation.IsValidUrl(url!);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData(1, true)]
        [InlineData(720, true)]
        [InlineData(1440, true)]
        [InlineData(0, false)]
        [InlineData(1441, false)]
        [InlineData(-5, false)]
        public void IsValidTimeLimit_ShouldValidateCorrectly(int? timeLimit, bool expected)
        {
            // Act
            bool result = Validation.IsValidTimeLimit(timeLimit);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0.1, true)]
        [InlineData(10.5, true)]
        [InlineData(100.0, true)]
        [InlineData(0.09, false)]
        [InlineData(100.1, false)]
        [InlineData(-1.0, false)]
        public void IsValidPoints_ShouldValidateCorrectly(double pointsVal, bool expected)
        {
            // Arrange
            decimal points = (decimal)pointsVal;

            // Act
            bool result = Validation.IsValidPoints(points);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData(1, true)]
        [InlineData(5, true)]
        [InlineData(10, true)]
        [InlineData(0, false)]
        [InlineData(11, false)]
        [InlineData(-1, false)]
        public void IsValidMaxAttempts_ShouldValidateCorrectly(int? maxAttempts, bool expected)
        {
            // Act
            bool result = Validation.IsValidMaxAttempts(maxAttempts);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
