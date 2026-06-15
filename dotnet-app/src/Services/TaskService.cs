using System.Collections.Concurrent;
using MinimalWebApi.Models;

namespace MinimalWebApi.Services;

/// <summary>
/// Servicio de tareas thread-safe usando ConcurrentDictionary.
/// Demuestra operaciones concurrentes de lectura/escritura.
/// </summary>
public class TaskService
{
    private readonly ConcurrentDictionary<int, TaskItem> _tasks = new();
    private int _nextId = 1;

    public TaskService()
    {
        Add(new TaskItem { Title = "Configurar Forgejo", Description = "Instalar y configurar Forgejo con Docker", IsCompleted = false });
        Add(new TaskItem { Title = "Crear pipeline CI/CD", Description = "Implementar pipeline de 4 fases", IsCompleted = false });
        Add(new TaskItem { Title = "Escribir pruebas UAT", Description = "Automatizar pruebas con Playwright", IsCompleted = true });
    }

    public TaskItem Add(TaskItem item)
    {
        var id = Interlocked.Increment(ref _nextId);
        item.Id = id;
        _tasks[id] = item;
        return item;
    }

    public List<TaskItem> GetAll() => _tasks.Values.OrderBy(t => t.Id).ToList();

    public TaskItem? GetById(int id) => _tasks.GetValueOrDefault(id);

    public TaskItem? Update(int id, TaskItem updated)
    {
        if (_tasks.TryGetValue(id, out var existing))
        {
            updated.Id = id;
            _tasks.TryUpdate(id, updated, existing);
            return updated;
        }
        return null;
    }

    public bool Delete(int id) => _tasks.TryRemove(id, out _);

    public int Count => _tasks.Count;
}
