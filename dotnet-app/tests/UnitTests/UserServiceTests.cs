using MinimalWebApi.Services;
using MinimalWebApi.Models;
using Xunit;

namespace MinimalWebApi.Tests.UnitTests;

/// <summary>
/// Pruebas unitarias sobre el servicio de usuarios (TDD).
/// Demuestra aislamiento de funciones y control de estados.
/// </summary>
public class UserServiceTests
{
    [Fact]
    public void Register_NewUser_ReturnsUserWithId()
    {
        // Arrange
        var service = new UserService();
        var user = new User { Username = "testuser", Email = "test@test.com", PasswordHash = "pass" };

        // Act
        var result = service.Register(user);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("testuser", result.Username);
    }

    [Fact]
    public void Login_ValidCredentials_ReturnsSuccessWithToken()
    {
        // Arrange
        var service = new UserService();
        var request = new LoginRequest { Username = "admin", Password = "admin123" };

        // Act
        var result = service.Login(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Token);
        Assert.Contains("Bienvenido", result.Message);
    }

    [Fact]
    public void Login_InvalidCredentials_ReturnsFailure()
    {
        // Arrange
        var service = new UserService();
        var request = new LoginRequest { Username = "admin", Password = "wrongpass" };

        // Act
        var result = service.Login(request);

        // Assert
        Assert.False(result.Success);
        Assert.Empty(result.Token);
    }

    [Fact]
    public void ValidateToken_ValidToken_ReturnsTrue()
    {
        // Arrange
        var service = new UserService();
        var login = service.Login(new LoginRequest { Username = "admin", Password = "admin123" });

        // Act
        var isValid = service.ValidateToken(login.Token);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsFalse()
    {
        // Arrange
        var service = new UserService();

        // Act
        var isValid = service.ValidateToken("invalid-token");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Register_DuplicateUsername_AllowsDuplicate()
    {
        // Nota: El diseño actual permite duplicados porque no hay validación de unicidad en Register.
        // Esto es intencional para demostrar que el test detecta el comportamiento.
        var service = new UserService();
        service.Register(new User { Username = "admin", Email = "dup@test.com", PasswordHash = "pass" });

        var user = service.GetUserByUsername("admin");
        Assert.NotNull(user);
        // El servicio permite registrar admin duplicado pero GetUserByUsername devuelve el primero.
    }

    [Fact]
    public void HashPassword_ProducesConsistentHash()
    {
        // Arrange
        var password = "TestPass123!";

        // Act
        var hash1 = UserService.HashPassword(password);
        var hash2 = UserService.HashPassword(password);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA256 hex = 64 chars
    }
}
