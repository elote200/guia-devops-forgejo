# 2. La Pirámide de Testing en Sistemas Concurrentes

## 📌 Introducción

La **pirámide de testing** (conceptualizada por Mike Cohn) es un modelo que clasifica las pruebas automatizadas en capas según su granularidad, velocidad y costo de mantenimiento. En sistemas concurrentes y distribuidos, esta pirámide requiere adaptaciones significativas debido al **indeterminismo** intrínseco de los procesos paralelos.

---

## 2.1 La Pirámide de Testing Clásica

```
                    ╱╲
                   ╱  ╲
                  ╱ E2E╲
                 ╱ (UAT)╲
                ╱────────═══
               ╱            ╲
              ╱ Integración  ╲
             ╱  (API/DB)     ╲
            ╱────────────────═══
           ╱                    ╲
          ╱   Unitarias (TDD)    ╲
         ╱    (Mocks/Stubs)       ╲
        ╱──────────────────────────═══
```

| Capa | Velocidad | Costo | Cantidad | Propósito |
|------|-----------|-------|----------|-----------|
| Unitarias | ⚡ Milisegundos | Bajo | Muchas (70%) | Validar lógica aislada |
| Integración | ⏱ Segundos | Medio | Algunas (20%) | Validar interacción entre componentes |
| E2E / UAT | 🐢 Minutos | Alto | Pocas (10%) | Validar flujo completo del usuario |

---

## 2.2 Pruebas Unitarias (TDD)

### Test-Driven Development (TDD)

El TDD sigue el ciclo **Red-Green-Refactor**:

```
1. 🔴 RED: Escribir una prueba que falle (define el comportamiento deseado)
2. 🟢 GREEN: Escribir el código mínimo necesario para que pase
3. 🔵 REFACTOR: Mejorar el código sin cambiar su comportamiento
```

### Aislamiento de Funciones y Control de Estados

En sistemas concurrentes, probar unitariamente una función que accede a recursos compartidos es complejo. La solución son los **dobles de prueba (test doubles)**:

| Doble | Descripción | Uso en concurrencia |
|-------|-------------|---------------------|
| **Mock** | Objeto que verifica interacciones (métodos llamados, parámetros) | Simular respuestas de hilos/workers |
| **Stub** | Objeto que devuelve respuestas predefinidas | Proveer datos controlados a funciones concurrentes |
| **Fake** | Implementación simplificada de un componente | Base de datos en memoria en vez de real |
| **Spy** | Envuelve un objeto real y registra llamadas | Verificar acceso a secciones críticas |

### Simulando Hilos con Mocks — Ejemplo conceptual

```csharp
// Escenario: Procesador concurrente que reparte tareas entre workers
public class ConcurrentProcessor
{
    private readonly IWorker[] _workers;

    public ConcurrentProcessor(IWorker[] workers) => _workers = workers;

    public async Task<Result[]> ProcessAllAsync(WorkItem[] items)
    {
        var tasks = items.Select(item =>
            Task.Run(() => ProcessOnAnyWorker(item)));
        return await Task.WhenAll(tasks);
    }

    private Result ProcessOnAnyWorker(WorkItem item)
    {
        // Lógica de balanceo
        return _workers[0].Execute(item);
    }
}

// Test con xUnit + Moq
[Fact]
public void ProcessAllAsync_DistributesWorkAmongWorkers()
{
    var mockWorker = new Mock<IWorker>();
    mockWorker.Setup(w => w.Execute(It.IsAny<WorkItem>()))
              .Returns(new Result { Success = true });

    var processor = new ConcurrentProcessor(new[] { mockWorker.Object });
    var items = new[] { new WorkItem(1), new WorkItem(2) };

    var results = await processor.ProcessAllAsync(items);

    Assert.All(results, r => Assert.True(r.Success));
    mockWorker.Verify(w => w.Execute(It.IsAny<WorkItem>()), Times.Exactly(2));
}
```

### Buenas prácticas para Unit Testing en código concurrente

1. **Aislar el código de la concurrencia:** Separar la lógica de negocio (pura) de la lógica de sincronización (locks, semáforos, canales).
2. **Usar `Task` controlados en vez de hilos reales:** En pruebas, se pueden usar `Task.CompletedTask` o `Task.FromResult` para evitar paralelismo real.
3. **Timeouts en las aserciones:** Para detectar deadlocks, usar `Assert.True(task.Wait(TimeSpan.FromSeconds(5)))`.
4. **Probar estados, no secuencias:** Verificar el estado _después_ de la operación concurrente, no el orden exacto de ejecución (que es indeterminista).

---

## 2.3 El Desafío del Indeterminismo

### ¿Por qué es difícil probar sistemas concurrentes?

Los sistemas concurrentes introducen **no-determinismo**: la misma ejecución con la misma entrada puede producir diferentes órdenes de interleaving, y por tanto diferentes resultados.

### Condiciones de Carrera (Race Conditions)

Ocurren cuando dos o más hilos acceden a un recurso compartido sin sincronización, y el resultado depende del orden de ejecución:

```csharp
// Ejemplo clásico de race condition
int counter = 0;

void Increment() => counter++;  // NO es atómico: read → increment → write

// Dos hilos ejecutando Increment() pueden dar counter=1 en vez de 2
// porque ambos leyeron 0 antes de que el otro escribiera 1
```

### Bloqueos Mutuos (Deadlocks)

Ocurren cuando dos o más hilos esperan recursos que el otro tiene, y ninguno puede avanzar:

```csharp
object lockA = new();
object lockB = new();

// Thread 1                        // Thread 2
lock (lockA)                        lock (lockB)
{                                   {
    lock (lockB)    ← DEADLOCK →        lock (lockA)
    {                                   {
        // nunca llega aquí              // nunca llega aquí
    }                                   }
}                                   }
```

### Estrategias para mitigar el indeterminismo en pruebas

| Estrategia | Descripción |
|------------|-------------|
| **Property-Based Testing** | Probar propiedades invariantes bajo múltiples interleavings (ej: `suma de operaciones = total esperado`) |
| **Chaos Engineering** | Pruebas de estrés donde se inyectan fallos (latencia, crash, particiones de red) |
| **Deterministic Simulation** | Usar schedulers controlados (ej: `Microsoft Coyote`) que exploran sistemáticamente interleavings |
| **Model Checking** | Verificación formal de modelos de concurrencia (TLA+, SPIN) |
| **Stress Testing** | Ejecutar la misma prueba cientos de veces para exponer race conditions latentes |
| **Instrumentación** | Usar detectores dinámicos como `TSan` (ThreadSanitizer) para C/C++ o `SafeGC` |

### Herramientas para testing de concurrencia en .NET

| Herramienta | Propósito |
|-------------|-----------|
| **Microsoft Coyote** | Testing controlado de concurrencia en C# (explora interleavings) |
| **Moq** + **xUnit** | Unit testing con mocks para aislar comportamiento concurrente |
| **NUnit** `[Retry]` | Reintentar pruebas que fallan por condiciones de carrera |
| **OpenCover / Coverlet** | Medir cobertura de código en pruebas concurrentes |
| **BenchmarkDotNet** | Detectar regresiones de rendimiento en código paralelo |

---

## 2.4 Pruebas de Aceptación de Usuario (UAT)

### Definición

Las **UAT (User Acceptance Testing)** validan que el sistema cumple con los requisitos del cliente desde su perspectiva. En el contexto de un pipeline CI/CD, las UAT se automatizan mediante herramientas de **browser automation** (Playwright, Selenium).

### ¿Qué validan las UAT?

- **Flujos completos:** Registro → Inicio de sesión → Operación crítica → Cierre de sesión.
- **Interacciones reales:** Clics, llenado de formularios, navegación entre páginas.
- **Estados de la UI:** Mensajes de error, estados de carga, elementos visibles/invisibles.
- **Responsive design:** Comportamiento en diferentes resoluciones.
- **Flujos concurrentes:** Lo que un usuario ve mientras otro realiza una acción (en sistemas multi-usuario).

### UAT con Playwright (.NET)

```csharp
using Microsoft.Playwright;

[Fact]
public async Task LoginFlow_ValidCredentials_RedirectsToDashboard()
{
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new()
    {
        Headless = true  // Modo sin interfaz gráfica
    });

    var page = await browser.NewPageAsync();
    await page.GotoAsync("http://localhost:5000/login");

    // Llenar formulario
    await page.FillAsync("input[name='username']", "admin");
    await page.FillAsync("input[name='password']", "password123");
    await page.ClickAsync("button[type='submit']");

    // Validar redirección
    await Assert.That(page.Url).Contains("/dashboard");

    // Validar elemento visible
    var welcome = await page.WaitForSelectorAsync("text=Bienvenido");
    Assert.NotNull(welcome);
}
```

### UAT con Selenium (.NET)

```csharp
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

[Fact]
public void LoginFlow_ValidCredentials_ShowsDashboard()
{
    var options = new ChromeOptions();
    options.AddArgument("--headless");  // Modo sin interfaz gráfica
    options.AddArgument("--no-sandbox");
    options.AddArgument("--disable-dev-shm-usage");

    using var driver = new ChromeDriver(options);
    driver.Navigate().GoToUrl("http://localhost:5000/login");

    driver.FindElement(By.CssSelector("input[name='username']")).SendKeys("admin");
    driver.FindElement(By.CssSelector("input[name='password']")).SendKeys("password123");
    driver.FindElement(By.CssSelector("button[type='submit']")).Click();

    Assert.Contains("/dashboard", driver.Url);
    Assert.NotNull(driver.FindElement(By.XPath("//*[contains(text(),'Bienvenido')]")));
}
```

### Desafíos de UAT en entornos distribuidos

| Desafío | Impacto | Mitigación |
|---------|---------|------------|
| Entorno no determinista | Pruebas que fallan intermitentemente | Retry policies, explicit waits |
| Datos compartidos | Un test afecta a otro | Isolación: cada test crea/limpia sus datos |
| Tiempo de ejecución | Pruebas E2E son lentas | Ejecución paralela por features |
| Dependencia de servicios externos | Caída de API afecta validación | Mocks para externos, solo E2E real en staging |
| Sesiones concurrentes (WebSocket) | Estado compartido entre usuarios | Tests multi-browser simultáneos |

---

## 2.5 Pirámide de Testing Adaptada para Sistemas Concurrentes

```
                    ╱╲
                   ╱  ╲
                  ╱ UAT ╲
                 ╱ (E2E) ╲
                ╱────────═══
               ╱            ╲
              ╱ Integración  ╲
             ╱  Distribuida  ╲
            ╱────────────────═══
           ╱                    ╲
          ╱  Unitarias (TDD)     ╲
         ╱   con Mocks/Stubs      ╲
        ╱──────────────────────────═══
       ╱                              ╲
      ╱  Pruebas de Concurrencia       ╲
     ╱   (Coyote, Stress, ModelCheck)   ╲
    ╱────────────────────────────────────═══
```

**Nueva capa base — pruebas de concurrencia:** Validan la corrección del modelo de concurrencia mismo (ausencia de deadlocks, invariantes bajo interleaving) antes de aplicar TDD.

---

## 🧠 Conclusión

Probar sistemas concurrentes y distribuidos es inherentemente más complejo que probar software secuencial. La clave está en:

1. **Maximizar las pruebas unitarias** con dobles de prueba que permitan controlar el comportamiento concurrente.
2. **Aceptar y trabajar con el indeterminismo** en lugar de ignorarlo (tests repetibles, property-based testing).
3. **Automatizar las UAT** en el pipeline CI/CD para garantizar que el producto final cumple con las expectativas del usuario.
4. **Agregar una capa específica de pruebas de concurrencia** en la base de la pirámide para detectar race conditions y deadlocks tempranamente.

---

## 📚 Referencias

- Cohn, M. (2009). *Succeeding with Agile: Software Development Using Scrum*.
- Fowler, M. (2014). *TestPyramid*. martinfowler.com.
- Microsoft Research. *Coyote: Systematic Testing of Concurrent Code*.
- Playwright Documentation. *playwright.dev*.
- Selenium Documentation. *selenium.dev*.
