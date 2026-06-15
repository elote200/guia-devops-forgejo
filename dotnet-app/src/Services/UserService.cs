using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MinimalWebApi.Models;

namespace MinimalWebApi.Services;

/// <summary>
/// Servicio de usuarios con soporte concurrente (thread-safe).
/// Demuestra manejo de concurrencia en servicios web.
/// </summary>
public class UserService
{
    private readonly ConcurrentDictionary<int, User> _users = new();
    private readonly ConcurrentDictionary<string, string> _tokens = new(); // token -> username
    private int _nextId = 1;

    public UserService()
    {
        // Seed data
        Register(new User
        {
            Username = "admin",
            Email = "admin@example.com",
            PasswordHash = HashPassword("admin123")
        });
        Register(new User
        {
            Username = "user1",
            Email = "user1@example.com",
            PasswordHash = HashPassword("password123")
        });
    }

    public User Register(User user)
    {
        var id = Interlocked.Increment(ref _nextId);
        user.Id = id;
        _users[id] = user;
        return user;
    }

    public LoginResponse Login(LoginRequest request)
    {
        var user = _users.Values.FirstOrDefault(u =>
            u.Username == request.Username &&
            u.PasswordHash == HashPassword(request.Password));

        if (user == null)
            return new LoginResponse { Success = false, Message = "Credenciales inválidas" };

        var token = Guid.NewGuid().ToString("N");
        _tokens[token] = user.Username;

        return new LoginResponse
        {
            Success = true,
            Token = token,
            Message = $"Bienvenido, {user.Username}!"
        };
    }

    public User? GetUserByUsername(string username)
        => _users.Values.FirstOrDefault(u => u.Username == username);

    public bool ValidateToken(string token)
        => _tokens.ContainsKey(token);

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }
}
