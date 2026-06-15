using MinimalWebApi.Models;
using MinimalWebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Registrar servicios como singletons para mantener estado en memoria
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<TaskService>();

// Configurar CORS para el UAT desde cualquier origen
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

// ========================
// ENDPOINTS PÚBLICOS
// ========================

app.MapGet("/", () => Results.Ok(new
{
    Application = "DevOps CI/CD Demo API",
    Version = "1.0.0",
    Status = "running",
    Endpoints = new[]
    {
        "GET  /health",
        "POST /api/auth/register",
        "POST /api/auth/login",
        "GET  /api/auth/me?username={username}",
        "GET  /api/tasks",
        "POST /api/tasks",
        "GET  /api/tasks/{id}",
        "PUT  /api/tasks/{id}",
        "DELETE /api/tasks/{id}"
    }
}));

app.MapGet("/health", () => Results.Ok(new
{
    Status = "healthy",
    Timestamp = DateTime.UtcNow,
    Uptime = Environment.TickCount64 / 1000
}));

// ========================
// ENDPOINTS DE AUTENTICACIÓN
// ========================

app.MapPost("/api/auth/register", (User user, UserService userService) =>
{
    if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.PasswordHash))
        return Results.BadRequest(new { Error = "Username y PasswordHash son requeridos" });

    if (userService.GetUserByUsername(user.Username) != null)
        return Results.Conflict(new { Error = "El usuario ya existe" });

    user.PasswordHash = UserService.HashPassword(user.PasswordHash);
    var created = userService.Register(user);
    return Results.Created($"/api/auth/me?username={created.Username}", new
    {
        created.Id,
        created.Username,
        created.Email
    });
});

app.MapPost("/api/auth/login", (LoginRequest request, UserService userService) =>
{
    var result = userService.Login(request);
    if (!result.Success)
        return Results.Unauthorized();

    return Results.Ok(result);
});

app.MapGet("/api/auth/me", (string username, UserService userService) =>
{
    var user = userService.GetUserByUsername(username);
    if (user == null)
        return Results.NotFound(new { Error = "Usuario no encontrado" });

    return Results.Ok(new { user.Id, user.Username, user.Email });
});

// ========================
// ENDPOINTS DE TAREAS (CRUD)
// ========================

app.MapGet("/api/tasks", (TaskService taskService) =>
{
    return Results.Ok(taskService.GetAll());
});

app.MapGet("/api/tasks/{id:int}", (int id, TaskService taskService) =>
{
    var task = taskService.GetById(id);
    return task is not null ? Results.Ok(task) : Results.NotFound();
});

app.MapPost("/api/tasks", (TaskItem task, TaskService taskService) =>
{
    if (string.IsNullOrWhiteSpace(task.Title))
        return Results.BadRequest(new { Error = "El título es requerido" });

    var created = taskService.Add(task);
    return Results.Created($"/api/tasks/{created.Id}", created);
});

app.MapPut("/api/tasks/{id:int}", (int id, TaskItem task, TaskService taskService) =>
{
    var updated = taskService.Update(id, task);
    return updated is not null ? Results.Ok(updated) : Results.NotFound();
});

app.MapDelete("/api/tasks/{id:int}", (int id, TaskService taskService) =>
{
    var deleted = taskService.Delete(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});

app.Run();
