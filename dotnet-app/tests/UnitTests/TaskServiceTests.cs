using MinimalWebApi.Services;
using MinimalWebApi.Models;
using Xunit;

namespace MinimalWebApi.Tests.UnitTests;

/// <summary>
/// Pruebas unitarias sobre el servicio de tareas (CRUD concurrente).
/// Demuestra TDD aplicado a operaciones con estado compartido.
/// </summary>
public class TaskServiceTests
{
    [Fact]
    public void Add_ValidTask_ReturnsTaskWithId()
    {
        // Arrange
        var service = new TaskService();
        var task = new TaskItem { Title = "Test Task", Description = "A test task" };

        // Act
        var result = service.Add(task);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Test Task", result.Title);
    }

    [Fact]
    public void GetAll_ReturnsAllTasks()
    {
        // Arrange
        var service = new TaskService();
        var initialCount = service.Count;

        service.Add(new TaskItem { Title = "Task A" });
        service.Add(new TaskItem { Title = "Task B" });

        // Act
        var tasks = service.GetAll();

        // Assert
        Assert.Equal(initialCount + 2, tasks.Count);
    }

    [Fact]
    public void GetById_ExistingTask_ReturnsTask()
    {
        // Arrange
        var service = new TaskService();
        var added = service.Add(new TaskItem { Title = "Find Me" });

        // Act
        var found = service.GetById(added.Id);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(added.Id, found.Id);
        Assert.Equal("Find Me", found.Title);
    }

    [Fact]
    public void GetById_NonExistentTask_ReturnsNull()
    {
        // Arrange
        var service = new TaskService();

        // Act
        var found = service.GetById(9999);

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public void Update_ExistingTask_UpdatesFields()
    {
        // Arrange
        var service = new TaskService();
        var added = service.Add(new TaskItem { Title = "Original", IsCompleted = false });

        // Act
        var updated = service.Update(added.Id, new TaskItem { Title = "Updated", IsCompleted = true });

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Title);
        Assert.True(updated.IsCompleted);
    }

    [Fact]
    public void Delete_ExistingTask_RemovesAndReturnsTrue()
    {
        // Arrange
        var service = new TaskService();
        var added = service.Add(new TaskItem { Title = "Delete Me" });

        // Act
        var deleted = service.Delete(added.Id);

        // Assert
        Assert.True(deleted);
        Assert.Null(service.GetById(added.Id));
    }

    [Fact]
    public void Delete_NonExistentTask_ReturnsFalse()
    {
        // Arrange
        var service = new TaskService();

        // Act
        var deleted = service.Delete(9999);

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public void ConcurrentAccess_NoDataCorruption()
    {
        // Prueba de concurrencia: múltiples hilos agregando tareas simultáneamente
        var service = new TaskService();
        var count = 100;
        var tasks = new List<Task>();

        for (int i = 0; i < count; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                service.Add(new TaskItem { Title = $"Concurrent Task {index}" });
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Verificar que todas las tareas se agregaron sin pérdida de datos
        Assert.Equal(count + 3, service.Count); // +3 del seed data
    }
}
