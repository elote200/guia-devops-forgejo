using Microsoft.Playwright;
using Xunit;

namespace MinimalWebApi.UatTests;

/// <summary>
/// Pruebas concurrentes: simula múltiples usuarios accediendo simultáneamente.
/// </summary>
public class ConcurrentUserTests
{
    private readonly string _baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5000";

    /// <summary>
    /// Simula N usuarios creando tareas concurrentemente.
    /// Cada usuario abre su propio browser context.
    /// </summary>
    [Fact]
    public async Task MultipleUsers_ConcurrentTaskCreation_NoDataLoss()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Args = new[] { "--no-sandbox" }
        });

        const int userCount = 5;
        const int tasksPerUser = 3;

        var tasks = new List<Task>();
        for (int u = 0; u < userCount; u++)
        {
            var userId = u;
            tasks.Add(Task.Run(async () =>
            {
                var context = await browser.NewContextAsync();
                var page = await context.NewPageAsync();

                for (int t = 0; t < tasksPerUser; t++)
                {
                    var taskIndex = t;
                    await page.EvaluateAsync(@"
                        async () => {
                            const response = await fetch('" + _baseUrl + @"/api/tasks', {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                body: JSON.stringify({
                                    title: 'Concurrent Task from User " + userId + @" - " + taskIndex + @"',
                                    description: 'Testing concurrency',
                                    isCompleted: false
                                })
                            });
                            return response.ok;
                        }
                    ");
                }

                await context.CloseAsync();
            }));
        }

        await Task.WhenAll(tasks);

        // Verificar que todas las tareas se crearon correctamente
        await using var verifyContext = await browser.NewContextAsync();
        var verifyPage = await verifyContext.NewPageAsync();
        var count = await verifyPage.EvaluateAsync<int>(@"
            async () => {
                const response = await fetch('" + _baseUrl + @"/api/tasks');
                const tasks = await response.json();
                return tasks.length;
            }
        ");

        // Deberían haberse creado userCount * tasksPerUser + algunas del seed
        Assert.True(count >= userCount * tasksPerUser);
    }
}
