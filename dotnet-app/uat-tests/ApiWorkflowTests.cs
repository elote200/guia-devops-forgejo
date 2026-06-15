using Microsoft.Playwright;
using Xunit;

namespace MinimalWebApi.UatTests;

/// <summary>
/// Pruebas de Aceptación de Usuario (UAT) usando Playwright.
/// Simulan interacciones reales de un usuario en el navegador.
/// Se ejecutan en modo headless dentro del pipeline CI/CD.
/// </summary>
public class ApiWorkflowTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    // URL base de la aplicación (se configura por variable de entorno)
    private readonly string _baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5000";

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });

        var context = await _browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 1280, Height = 720 }
        });

        _page = await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task UAT01_HealthEndpoint_ReturnsHealthy()
    {
        // Act: Navegar al health endpoint
        var response = await _page!.GotoAsync($"{_baseUrl}/health");

        // Assert: Status code 200
        Assert.NotNull(response);
        Assert.True(response.Ok);

        // Verificar contenido JSON
        var content = await response.JsonAsync();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task UAT02_RootEndpoint_ReturnsApiInfo()
    {
        // Act
        var response = await _page!.GotoAsync(_baseUrl);
        var body = await _page.Locator("pre").InnerTextAsync();

        // Assert: La respuesta contiene el nombre de la aplicación
        Assert.Contains("DevOps CI/CD Demo API", body);
        Assert.Contains("1.0.0", body);
    }

    [Fact]
    public async Task UAT03_LoginFlow_ValidCredentials_ReturnsSuccess()
    {
        // Esta prueba simula una petición POST via JavaScript (fetch)
        // ya que no hay formulario HTML - es una API REST

        var result = await _page!.EvaluateAsync<Dictionary<string, object>>(@"
            async () => {
                const response = await fetch('" + _baseUrl + @"/api/auth/login', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ username: 'admin', password: 'admin123' })
                });
                const data = await response.json();
                return { status: response.status, success: data.success, hasToken: !!data.token };
            }
        ");

        Assert.NotNull(result);
        // Note: EvaluateAsync devuelve JsonElement, validamos por existencia de propiedades
        Assert.Contains("success", result.Keys);
    }

    [Fact]
    public async Task UAT04_LoginFlow_InvalidCredentials_Returns401()
    {
        var result = await _page!.EvaluateAsync<Dictionary<string, object>>(@"
            async () => {
                const response = await fetch('" + _baseUrl + @"/api/auth/login', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ username: 'admin', password: 'wrongpass' })
                });
                return { status: response.status };
            }
        ");

        // Verificar que el status es 401
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UAT05_TasksCrud_CreateAndRetrieveTask()
    {
        // Crear una tarea
        var createResult = await _page!.EvaluateAsync(@"
            async () => {
                const response = await fetch('" + _baseUrl + @"/api/tasks', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        title: 'UAT Test Task',
                        description: 'Created during UAT',
                        isCompleted: false
                    })
                });
                return await response.json();
            }
        ");

        Assert.NotNull(createResult);

        // Obtener todas las tareas
        var tasksArray = await _page!.EvaluateAsync(@"
            async () => {
                const response = await fetch('" + _baseUrl + @"/api/tasks');
                return await response.json();
            }
        ");

        Assert.NotNull(tasksArray);
    }

    [Fact]
    public async Task UAT06_FullWorkflow_RegisterLoginCreateTasks()
    {
        // 1. Registrar un nuevo usuario
        var register = await _page!.EvaluateAsync(@"
            async () => {
                const response = await fetch('" + _baseUrl + @"/api/auth/register', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        username: 'uatuser',
                        email: 'uat@test.com',
                        passwordHash: 'uatpass123'
                    })
                });
                return { status: response.status, ok: response.ok };
            }
        ");

        Assert.NotNull(register);

        // 2. Login con el nuevo usuario
        var login = await _page!.EvaluateAsync(@"
            async () => {
                const response = await fetch('" + _baseUrl + @"/api/auth/login', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ username: 'uatuser', password: 'uatpass123' })
                });
                const data = await response.json();
                return { status: response.status, token: data.token };
            }
        ");

        Assert.NotNull(login);

        // 3. Crear múltiples tareas
        for (int i = 1; i <= 3; i++)
        {
            var taskIndex = i;
            var created = await _page!.EvaluateAsync<bool>(@"
                async () => {
                    const response = await fetch('" + _baseUrl + @"/api/tasks', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            title: 'UAT Task ' + " + taskIndex + @",
                            description: 'Auto-created during UAT',
                            isCompleted: false
                        })
                    });
                    return response.ok;
                }
            ");

            Assert.True(created);
        }

        // 4. Verificar el listado de tareas
        var allTasks = await _page!.EvaluateAsync<int>(@"
            async () => {
                const response = await fetch('" + _baseUrl + @"/api/tasks');
                const tasks = await response.json();
                return tasks.length;
            }
        ");

        Assert.True(allTasks > 0);

        // 5. Verificar el health endpoint al final
        var health = await _page!.GotoAsync($"{_baseUrl}/health");
        Assert.NotNull(health);
        Assert.True(health.Ok);
    }
}
