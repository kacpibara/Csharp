### Dependency Injection w ASP.NET Core

DI to mechanizm który **dostarcza zależności do klas zamiast pozwalać im tworzyć je samodzielnie**. ASP.NET Core ma wbudowany kontener DI — nie potrzebujesz Autofac ani Ninject.

---

### 1. Fundamenty — po co DI

csharp

```csharp
// BEZ DI — tight coupling, niemożliwe do testowania
public class ZlySerwisZamowien
{
    private readonly BazaDanychContext _ctx;     // tworzy sam!
    private readonly SmtpEmailSerwis  _email;   // tworzy sam!
    private readonly FileLogger       _logger;  // tworzy sam!

    public ZlySerwisZamowien()
    {
        _ctx    = new BazaDanychContext("Server=...");  // hardcoded!
        _email  = new SmtpEmailSerwis("smtp.gmail.com", 587);
        _logger = new FileLogger("logs/app.log");
    }
    // Problemy:
    // ❌ Nie można podmienić implementacji bez zmiany kodu
    // ❌ Nie można przetestować bez prawdziwej bazy/emaila/pliku
    // ❌ Hardcoded konfiguracja
    // ❌ Klasa zarządza cyklem życia zależności
}

// Z DI — loose coupling, łatwe testowanie
public class DobrySerwisZamowien
{
    private readonly IDbContext     _ctx;    // interfejs!
    private readonly IEmailSerwis   _email;  // interfejs!
    private readonly ILogger<DobrySerwisZamowien> _logger;

    // Zależności WSTRZYKIWANE przez konstruktor
    public DobrySerwisZamowien(
        IDbContext     ctx,
        IEmailSerwis   email,
        ILogger<DobrySerwisZamowien> logger)
    {
        _ctx    = ctx;
        _email  = email;
        _logger = logger;
    }
    // Zalety:
    // ✅ Podmień implementację w jednym miejscu (Program.cs)
    // ✅ Test: podaj mock zamiast prawdziwego serwisu
    // ✅ Cykl życia zarządzany przez kontener
    // ✅ Konfiguracja w jednym miejscu
}
```

---

### 2. Trzy czasy życia — szczegółowo

csharp

```csharp
// TRANSIENT — nowa instancja za każdym razem gdy pobierasz z kontenera
// SCOPED    — jedna instancja na request HTTP (lub scope)
// SINGLETON — jedna instancja przez całe życie aplikacji

// Demonstracja różnic
public interface ILicznikSerwis
{
    Guid InstanceId { get; }
    int  Wywolania  { get; }
    void Inkrementuj();
}

public class LicznikSerwis : ILicznikSerwis
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public int  Wywolania  { get; private set; } = 0;
    public void Inkrementuj() => Wywolania++;
}

// Rejestracja różnych czasów życia
builder.Services.AddTransient<ILicznikSerwis, LicznikSerwis>();   // nowy za każdym razem
// builder.Services.AddScoped<ILicznikSerwis, LicznikSerwis>();    // jeden na request
// builder.Services.AddSingleton<ILicznikSerwis, LicznikSerwis>(); // jeden na aplikację

// Kontroler który pobiera serwis TRZY RAZY
[ApiController]
[Route("api/di-demo")]
public class DiDemoController : ControllerBase
{
    private readonly ILicznikSerwis _s1;
    private readonly ILicznikSerwis _s2;
    private readonly IServiceProvider _sp;

    public DiDemoController(
        ILicznikSerwis s1,   // 1. pobranie
        ILicznikSerwis s2,   // 2. pobranie
        IServiceProvider sp)
    {
        _s1 = s1;
        _s2 = s2;
        _sp = sp;
    }

    [HttpGet]
    public IActionResult Demo()
    {
        var s3 = _sp.GetRequiredService<ILicznikSerwis>(); // 3. pobranie

        _s1.Inkrementuj();
        _s2.Inkrementuj();
        s3.Inkrementuj();

        return Ok(new
        {
            s1 = new { _s1.InstanceId, _s1.Wywolania },
            s2 = new { _s2.InstanceId, _s2.Wywolania },
            s3 = new { s3.InstanceId,  s3.Wywolania  },
            s1_equals_s2 = _s1.InstanceId == _s2.InstanceId,
            s2_equals_s3 = _s2.InstanceId == s3.InstanceId
        });
    }
}

// WYNIKI dla różnych czasów życia:
//
// TRANSIENT:
// s1: { id: "AAA", wywolania: 1 }
// s2: { id: "BBB", wywolania: 1 }  ← inny obiekt!
// s3: { id: "CCC", wywolania: 1 }  ← jeszcze inny!
// s1_equals_s2: false
//
// SCOPED:
// s1: { id: "AAA", wywolania: 1 }
// s2: { id: "AAA", wywolania: 2 }  ← TEN SAM obiekt w tym request!
// s3: { id: "AAA", wywolania: 3 }  ← TEN SAM!
// s1_equals_s2: true
// (przy następnym request: nowy obiekt "BBB")
//
// SINGLETON:
// s1: { id: "AAA", wywolania: 1 }
// s2: { id: "AAA", wywolania: 2 }  ← TEN SAM przez całe życie!
// s3: { id: "AAA", wywolania: 3 }  ← ZAWSZE ten sam!
// s1_equals_s2: true
// (przy następnym request: wciąż "AAA", wywolania: 4, 5, 6...)
```

---

### 3. Kiedy co używać — zasady

csharp

```csharp
// TRANSIENT — bezstanowe serwisy pomocnicze
// ✅ Mapper, validator, formatter
// ✅ Serwisy które nie trzymają stanu
// ✅ Lekkie obiekty
// ❌ Serwisy z zasobami (DB connection, HTTP client) — za dużo alokacji

builder.Services.AddTransient<IEmailValidator, EmailValidator>();
builder.Services.AddTransient<IPasswordHasher, BcryptPasswordHasher>();

// SCOPED — jeden na request HTTP — NAJCZĘSTSZY wybór
// ✅ DbContext — jedna transakcja na request
// ✅ Repository, UnitOfWork
// ✅ Serwisy domenowe
// ✅ Cokolwiek co powinno dzielić stan w obrębie jednego request
// ❌ Nie może być wstrzykiwane do Singleton!

builder.Services.AddScoped<AppDbContext>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProduktSerwis, ProduktSerwis>();
builder.Services.AddScoped<IZamowieniaSerwis, ZamowieniaSerwis>();

// SINGLETON — jeden przez całe życie aplikacji
// ✅ Cache (MemoryCache, Redis client)
// ✅ Konfiguracja (już odczytana)
// ✅ Połączenie do zewnętrznych serwisów (HttpClient przez IHttpClientFactory)
// ✅ Background services
// ✅ Ciężkie do inicjalizacji obiekty
// ❌ NIE może zależeć od Scoped/Transient (captive dependency!)
// ❌ NIE może trzymać mutowalnego stanu bez thread safety

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddSingleton<ICacheManager, MemoryCacheManager>();
builder.Services.AddSingleton<IEmailTemplateEngine, RazorEmailEngine>();

// TABELA REGUŁ:
// ┌────────────┬───────────────┬──────────────────────────────────────────┐
// │ Czas życia │ Może zależeć  │ Może być wstrzyknięty do                 │
// │            │ od            │                                          │
// ├────────────┼───────────────┼──────────────────────────────────────────┤
// │ Transient  │ wszystkich    │ Transient, Scoped, Singleton             │
// │ Scoped     │ Transient     │ Scoped (tego samego scope)               │
// │ Singleton  │ Singleton     │ Singleton                                │
// │            │ Transient     │ (ostrożnie — trzyma ref do Transient!)   │
// └────────────┴───────────────┴──────────────────────────────────────────┘
```

---

### 4. Captive Dependency — najważniejsza pułapka

csharp

```csharp
// CAPTIVE DEPENDENCY — Singleton trzyma Scoped dłużej niż powinien!
// ASP.NET Core WYKRYWA to w Development i rzuca wyjątek

// ŹLE — Singleton zależy od Scoped
public class ZlySingletonSerwis
{
    private readonly AppDbContext _ctx;  // Scoped! Żyje tylko 1 request!

    // PROBLEM: Singleton żyje przez całą aplikację
    // DbContext będzie używany przez WIELE requestów
    // DbContext nie jest thread-safe!
    // To BŁĄD — nie rób tego!
    public ZlySingletonSerwis(AppDbContext ctx) => _ctx = ctx;
}

builder.Services.AddSingleton<ZlySingletonSerwis>();  // rzuci wyjątek!
// InvalidOperationException: Cannot consume scoped service 'AppDbContext'
//   from singleton 'ZlySingletonSerwis'

// DOBRZE — Singleton używa IServiceScopeFactory
public class DobrySingletonSerwis
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DobrySingletonSerwis(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public async Task WykonajZPotrzebnymScope()
    {
        // Twórz scope gdy potrzebujesz Scoped serwisu
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Użyj ctx — zostanie zniszczony po using
        var produkty = await ctx.Produkty.ToListAsync();
    }
}

// DOBRZE — Singleton zależy tylko od Transient (ale ostrożnie!)
public class SerwisZTransient
{
    private readonly IEmailValidator _validator;  // Transient — OK

    // Transient wstrzyknięty do Singleton staje się EFEKTYWNIE Singleton
    // przez czas życia SerwisZTransient
    // OK gdy validator jest bezstanowy — NIE OK gdy trzyma stan
    public SerwisZTransient(IEmailValidator validator) => _validator = validator;
}

// Wykrywanie captive dependencies w testach
var host = Host.CreateDefaultBuilder()
    .ConfigureServices(services =>
    {
        services.AddScoped<AppDbContext>();
        services.AddSingleton<ZlySerwis>();  // spróbuje użyć AppDbContext
    })
    // Validate on build — rzuć wyjątek jeśli jakikolwiek problem
    .UseDefaultServiceProvider(opt =>
    {
        opt.ValidateScopes   = true;  // domyślnie true w Development
        opt.ValidateOnBuild  = true;  // sprawdź przy budowaniu hosta!
    })
    .Build();
```

---

### 5. IServiceCollection — rejestracja

csharp

```csharp
// Program.cs — wszystkie sposoby rejestracji

// === PODSTAWOWE ===
builder.Services.AddTransient<IFoo, Foo>();
builder.Services.AddScoped<IBar, Bar>();
builder.Services.AddSingleton<IBaz, Baz>();

// Implementacja = interfejs (jeden typ)
builder.Services.AddTransient<ConcreteService>();

// Bezpośrednia instancja (zawsze Singleton)
builder.Services.AddSingleton<IConfig>(new MyConfig { Url = "..." });

// === FABRYKI ===
// Fabryka z dostępem do kontenera
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new SqlConnection(config.GetConnectionString("Default"));
});

// Fabryka warunkowa
builder.Services.AddScoped<IEmailSerwis>(sp =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    return env.IsDevelopment()
        ? new FakeEmailSerwis()           // mock w dev
        : new SmtpEmailSerwis(sp.GetRequiredService<IOptions<SmtpOpcje>>());
});

// === WIELE IMPLEMENTACJI JEDNEGO INTERFEJSU ===
builder.Services.AddScoped<INotifikacja, EmailNotifikacja>();
builder.Services.AddScoped<INotifikacja, SmsNotifikacja>();
builder.Services.AddScoped<INotifikacja, PushNotifikacja>();

// Pobieranie wszystkich
public class NotifikacjaOrchestrator
{
    private readonly IEnumerable<INotifikacja> _notifikacje;

    public NotifikacjaOrchestrator(IEnumerable<INotifikacja> notifikacje)
        => _notifikacje = notifikacje;

    public async Task WyslijWszystkimiKanalamiAsync(string wiadomosc)
    {
        foreach (var n in _notifikacje)
            await n.WyslijAsync(wiadomosc);
    }
}

// === TryAdd — rejestruj tylko jeśli NIE ma jeszcze rejestracji ===
builder.Services.TryAddScoped<IProduktRepo, ProduktRepo>();
// Użyteczne dla bibliotek — nie nadpisuj tego co użytkownik zarejestrował

builder.Services.TryAddEnumerable(new ServiceDescriptor(
    typeof(INotifikacja),
    typeof(EmailNotifikacja),
    ServiceLifetime.Scoped));
// TryAddEnumerable — dodaj do kolekcji tylko jeśli nie ma takiej implementacji

// === Replace — zamień istniejącą rejestrację ===
builder.Services.AddScoped<IEmailSerwis, SmtpEmailSerwis>();
builder.Services.Replace(ServiceDescriptor.Scoped<IEmailSerwis, SendGridEmailSerwis>());
// Teraz tylko SendGrid

// === ServiceDescriptor — niskopoziomowa rejestracja ===
var descriptor = new ServiceDescriptor(
    typeof(IProduktSerwis),
    typeof(ProduktSerwis),
    ServiceLifetime.Scoped);
builder.Services.Add(descriptor);

// === Extension Methods — organizacja kodu ===
// Zamiast pisać wszystko w Program.cs — grupuj w extension methods
public static class ServiceCollectionExtensions
{
    public static IServiceCollection DodajInfrastrukture(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(config.GetConnectionString("Default")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProduktRepo, ProduktRepo>();
        services.AddScoped<IKlientRepo, KlientRepo>();
        return services;
    }

    public static IServiceCollection DodajAplikacje(
        this IServiceCollection services)
    {
        services.AddScoped<IProduktSerwis, ProduktSerwis>();
        services.AddScoped<IZamowieniaSerwis, ZamowieniaSerwis>();
        services.AddScoped<IKlientSerwis, KlientSerwis>();
        return services;
    }

    public static IServiceCollection DodajEmail(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<SmtpOpcje>(config.GetSection("Smtp"));
        services.AddTransient<IEmailSerwis, SmtpEmailSerwis>();
        services.AddTransient<IEmailTemplateEngine, RazorEmailEngine>();
        return services;
    }
}

// Program.cs — czysto i przejrzyście
builder.Services
    .DodajInfrastrukture(builder.Configuration)
    .DodajAplikacje()
    .DodajEmail(builder.Configuration);
```

---

### 6. Pobieranie serwisów — różne sposoby

csharp

```csharp
// 1. Przez konstruktor — ZALECANE
public class MojSerwis
{
    private readonly IProduktRepo _repo;
    public MojSerwis(IProduktRepo repo) => _repo = repo;
}

// 2. IServiceProvider — gdy potrzebujesz runtime resolution
public class SerwisZDynamicznymTypem
{
    private readonly IServiceProvider _sp;

    public SerwisZDynamicznymTypem(IServiceProvider sp) => _sp = sp;

    public IRepository<T> PobierzRepo<T>() where T : class
        => _sp.GetRequiredService<IRepository<T>>();

    // GetService    — zwraca null gdy brak rejestracji
    // GetRequiredService — rzuca wyjątek gdy brak (zalecane!)
    public IEmailSerwis? Email => _sp.GetService<IEmailSerwis>();
    public ISmsSerwis   Sms   => _sp.GetRequiredService<ISmsSerwis>();
}

// 3. IServiceScopeFactory — twórz scope w Singleton lub Background Service
public class BackgroundJobService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BackgroundJobService(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Twórz nowy scope dla każdej iteracji
            using var scope = _scopeFactory.CreateScope();

            // Pobierz Scoped serwisy
            var ctx     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var serwis  = scope.ServiceProvider.GetRequiredService<IProduktSerwis>();

            await serwis.PrzetworzProduktyAsync(ct);

            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        } // scope.Dispose() — ctx zwolniony
    }
}

// 4. Keyed Services (.NET 8) — wiele implementacji przez klucz
builder.Services.AddKeyedScoped<IEmailSerwis, SmtpEmailSerwis>("smtp");
builder.Services.AddKeyedScoped<IEmailSerwis, SendGridEmailSerwis>("sendgrid");
builder.Services.AddKeyedScoped<IEmailSerwis, FakeEmailSerwis>("fake");

public class EmailOrchestrator
{
    private readonly IEmailSerwis _smtp;
    private readonly IEmailSerwis _sendgrid;

    public EmailOrchestrator(
        [FromKeyedServices("smtp")]     IEmailSerwis smtp,
        [FromKeyedServices("sendgrid")] IEmailSerwis sendgrid)
    {
        _smtp      = smtp;
        _sendgrid  = sendgrid;
    }

    public async Task WyslijAsync(string to, string body, bool uzyjSendgrid = false)
    {
        var serwis = uzyjSendgrid ? _sendgrid : _smtp;
        await serwis.WyslijAsync(to, body);
    }
}

// W Minimal API / Kontrolerze
app.MapGet("/test", (
    [FromKeyedServices("smtp")] IEmailSerwis email) =>
    email.WyslijAsync("test@test.pl", "Test"));
```

---

### 7. Options Pattern — typowana konfiguracja

csharp

```csharp
// Klasy opcji
public class DatabaseOpcje
{
    public const string Sekcja = "Database";

    [Required]
    public string ConnectionString { get; set; } = "";
    public int    CommandTimeout   { get; set; } = 30;
    public int    MaxPoolSize      { get; set; } = 100;
    public bool   EnableRetry      { get; set; } = true;
    public int    MaxRetryCount    { get; set; } = 3;
}

public class SmtpOpcje
{
    public const string Sekcja = "Smtp";

    [Required] public string Host     { get; set; } = "";
    [Range(1, 65535)] public int Port { get; set; } = 587;
    public string? Login              { get; set; }
    public string? Haslo              { get; set; }
    public bool    UzyjSSL            { get; set; } = true;
}

// appsettings.json:
// {
//   "Database": {
//     "ConnectionString": "Server=...;Database=Sklep;...",
//     "CommandTimeout": 60,
//     "MaxPoolSize": 200
//   },
//   "Smtp": {
//     "Host": "smtp.sendgrid.net",
//     "Port": 587,
//     "Login": "apikey",
//     "Haslo": "SG.xxx"
//   }
// }

// Rejestracja z walidacją
builder.Services
    .AddOptions<DatabaseOpcje>()
    .BindConfiguration(DatabaseOpcje.Sekcja)    // bind z sekcji
    .ValidateDataAnnotations()                   // waliduj [Required], [Range]...
    .ValidateOnStart();                          // waliduj przy starcie, nie przy pierwszym użyciu!

builder.Services
    .AddOptions<SmtpOpcje>()
    .BindConfiguration(SmtpOpcje.Sekcja)
    .Validate(opt =>                             // własna walidacja
    {
        if (opt.UzyjSSL && opt.Port == 25)
            return false;  // błąd — port 25 to niezaszyfrowany
        return true;
    }, "Port 25 nie obsługuje SSL")
    .ValidateOnStart();

// Trzy warianty IOptions
public class EmailSerwis
{
    // IOptions<T>       — zamrożona wartość, nie zmienia się
    // IOptionsSnapshot<T>— odświeżana na każdy request (Scoped)
    // IOptionsMonitor<T> — live reload, powiadamia o zmianach (Singleton-friendly)

    private readonly SmtpOpcje _opcjeZamrozone;

    public EmailSerwis(IOptions<SmtpOpcje> opcje)
        => _opcjeZamrozone = opcje.Value;

    // Lub dla live reload (np. zmiana config bez restartu):
    // public EmailSerwis(IOptionsMonitor<SmtpOpcje> monitor)
    // {
    //     _opcje = monitor.CurrentValue;
    //     monitor.OnChange(newOpcje => _opcje = newOpcje);
    // }
}

// Pobieranie opcji bez wstrzykiwania (w Program.cs)
var dbOpcje = builder.Configuration
    .GetSection(DatabaseOpcje.Sekcja)
    .Get<DatabaseOpcje>()!;

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(
        dbOpcje.ConnectionString,
        sql => sql.CommandTimeout(dbOpcje.CommandTimeout)));
```

---

### 8. Praktyczny przykład — pełna konfiguracja DI

csharp

```csharp
// Kompletna aplikacja e-commerce — organizacja DI

// === INTERFEJSY ===
public interface IProduktRepo      { Task<Produkt?> FindAsync(int id); }
public interface IKlientRepo       { Task<Klient?> FindAsync(int id);  }
public interface IUnitOfWork       { Task<int> SaveAsync();            }
public interface IEmailSerwis      { Task WyslijAsync(string to, string body); }
public interface ISmsSerwis        { Task WyslijAsync(string nr, string body); }
public interface IPasswordHasher   { string Hash(string pass); bool Verify(string pass, string hash); }
public interface ICacheService     { Task<T?> GetAsync<T>(string key); Task SetAsync<T>(string key, T val, TimeSpan? ttl); }

// === IMPLEMENTACJE ===
public class ProduktRepo : IProduktRepo
{
    private readonly AppDbContext _ctx;
    public ProduktRepo(AppDbContext ctx) => _ctx = ctx;
    public Task<Produkt?> FindAsync(int id) => _ctx.Produkty.FindAsync(id).AsTask();
}

public class SmtpEmailSerwis : IEmailSerwis
{
    private readonly SmtpOpcje _opcje;
    public SmtpEmailSerwis(IOptions<SmtpOpcje> opcje) => _opcje = opcje.Value;
    public Task WyslijAsync(string to, string body) => Task.CompletedTask; // stub
}

public class FakeEmailSerwis : IEmailSerwis
{
    private readonly ILogger<FakeEmailSerwis> _logger;
    public FakeEmailSerwis(ILogger<FakeEmailSerwis> logger) => _logger = logger;
    public Task WyslijAsync(string to, string body)
    {
        _logger.LogInformation("[FAKE EMAIL] Do: {To} | {Body}", to, body);
        return Task.CompletedTask;
    }
}

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string pass) => BCrypt.Net.BCrypt.HashPassword(pass);
    public bool Verify(string pass, string hash) => BCrypt.Net.BCrypt.Verify(pass, hash);
}

public class RedisCache : ICacheService, IDisposable
{
    private readonly StackExchange.Redis.IDatabase _db;
    public RedisCache(IOptions<RedisOpcje> opt)
    {
        var conn = StackExchange.Redis.ConnectionMultiplexer.Connect(opt.Value.ConnectionString);
        _db = conn.GetDatabase();
    }
    public async Task<T?> GetAsync<T>(string key)
    {
        var val = await _db.StringGetAsync(key);
        return val.HasValue ? System.Text.Json.JsonSerializer.Deserialize<T>(val!) : default;
    }
    public Task SetAsync<T>(string key, T val, TimeSpan? ttl = null)
        => _db.StringSetAsync(key, System.Text.Json.JsonSerializer.Serialize(val), ttl);
    public void Dispose() { }
}

// === EXTENSION METHODS — modułowa organizacja ===
public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DatabaseOpcje>()
            .BindConfiguration(DatabaseOpcje.Sekcja)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((sp, opt) =>
        {
            var dbOpt = sp.GetRequiredService<IOptions<DatabaseOpcje>>().Value;
            opt.UseSqlServer(dbOpt.ConnectionString, sql =>
                sql.CommandTimeout(dbOpt.CommandTimeout)
                   .EnableRetryOnFailure(dbOpt.MaxRetryCount));
        });

        services.AddScoped<IProduktRepo, ProduktRepo>();
        services.AddScoped<IKlientRepo, KlientRepo>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}

public static class EmailExtensions
{
    public static IServiceCollection AddEmail(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env)
    {
        services.AddOptions<SmtpOpcje>()
            .BindConfiguration(SmtpOpcje.Sekcja)
            .ValidateOnStart();

        if (env.IsDevelopment())
            services.AddTransient<IEmailSerwis, FakeEmailSerwis>();
        else
            services.AddTransient<IEmailSerwis, SmtpEmailSerwis>();

        return services;
    }
}

public static class CacheExtensions
{
    public static IServiceCollection AddCache(
        this IServiceCollection services, IConfiguration config)
    {
        var useRedis = config.GetValue<bool>("Cache:UseRedis");

        if (useRedis)
        {
            services.AddOptions<RedisOpcje>()
                .BindConfiguration("Redis")
                .ValidateOnStart();
            services.AddSingleton<ICacheService, RedisCache>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        return services;
    }
}

public static class SecurityExtensions
{
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        // Transient — bezstanowy, bezpieczny dla Singleton
        return services;
    }
}

// === Program.cs — czysto ===
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDatabase(builder.Configuration)
    .AddEmail(builder.Configuration, builder.Environment)
    .AddCache(builder.Configuration)
    .AddSecurity();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Weryfikacja DI przy budowaniu (dev)
builder.Host.UseDefaultServiceProvider(opt =>
{
    opt.ValidateScopes  = builder.Environment.IsDevelopment();
    opt.ValidateOnBuild = builder.Environment.IsDevelopment();
});

var app = builder.Build();

// Zastosuj migracje przy starcie
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await ctx.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();

// === TESTOWANIE — łatwe dzięki DI ===
public class ProduktSerwisTests
{
    [Fact]
    public async Task PobierzProdukt_Istnieje_ZwracaProdukt()
    {
        // Arrange — mock zamiast prawdziwych serwisów
        var repoMock  = new Mock<IProduktRepo>();
        var cacheMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<ProduktSerwis>>();

        repoMock.Setup(r => r.FindAsync(1))
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop" });

        cacheMock.Setup(c => c.GetAsync<Produkt>("produkt:1"))
            .ReturnsAsync((Produkt?)null);  // cache miss

        var serwis = new ProduktSerwis(repoMock.Object, cacheMock.Object,
            loggerMock.Object);

        // Act
        var wynik = await serwis.PobierzPoIdAsync(1);

        // Assert
        Assert.NotNull(wynik);
        Assert.Equal("Laptop", wynik.Nazwa);

        // Verify — czy zapisał do cache?
        cacheMock.Verify(c => c.SetAsync(
            "produkt:1",
            It.IsAny<Produkt>(),
            It.IsAny<TimeSpan?>()), Times.Once);
    }
}

// Klasy pomocnicze
public record Produkt { public int Id { get; set; } public string Nazwa { get; set; } = ""; }
public record Klient  { public int Id { get; set; } public string Email { get; set; } = ""; }
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> o) : base(o) { }
    public DbSet<Produkt> Produkty => Set<Produkt>();
}
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _ctx;
    public UnitOfWork(AppDbContext ctx) => _ctx = ctx;
    public Task<int> SaveAsync() => _ctx.SaveChangesAsync();
}
public class KlientRepo : IKlientRepo
{
    private readonly AppDbContext _ctx;
    public KlientRepo(AppDbContext ctx) => _ctx = ctx;
    public Task<Klient?> FindAsync(int id) => Task.FromResult<Klient?>(null);
}
public class MemoryCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T val, TimeSpan? ttl = null) => Task.CompletedTask;
}
public class ProduktSerwis
{
    private readonly IProduktRepo _repo;
    private readonly ICacheService _cache;
    private readonly ILogger<ProduktSerwis> _logger;
    public ProduktSerwis(IProduktRepo repo, ICacheService cache, ILogger<ProduktSerwis> logger)
    { _repo = repo; _cache = cache; _logger = logger; }
    public async Task<Produkt?> PobierzPoIdAsync(int id)
    {
        var cached = await _cache.GetAsync<Produkt>($"produkt:{id}");
        if (cached != null) return cached;
        var prod = await _repo.FindAsync(id);
        if (prod != null) await _cache.SetAsync($"produkt:{id}", prod, TimeSpan.FromMinutes(5));
        return prod;
    }
}
public class RedisOpcje { public string ConnectionString { get; set; } = ""; }
```

---

### Typowe pytania rekrutacyjne

**"Jakie są czasy życia serwisów i kiedy co używać?"** Transient — nowy obiekt za każdym pobraniem z kontenera. Używaj dla lekkich, bezstanowych serwisów (walidatory, mappery). Scoped — jeden obiekt na request HTTP. Używaj dla DbContext, Repository, UnitOfWork — wszystkiego co powinno dzielić stan w obrębie jednego requestu. Singleton — jeden przez całe życie aplikacji. Używaj dla cache, konfiguracji, klientów HTTP — ciężkich do inicjalizacji obiektów. Pamiętaj: Singleton nie może zależeć od Scoped (captive dependency)!

**"Co to captive dependency i jak uniknąć?"** Captive dependency = Singleton trzyma referencję do Scoped serwisu. Scoped powinien żyć jeden request, ale Singleton trzyma go przez całą aplikację — DbContext staje się współdzielony między requestami, co nie jest thread-safe. ASP.NET Core wykrywa to w Development i rzuca wyjątek. Rozwiązanie: wstrzyknij `IServiceScopeFactory` do Singletona i twórz scope w metodach gdy potrzebujesz Scoped serwisu.

**"Jaka różnica między `GetService<T>` a `GetRequiredService<T>`?"** `GetService<T>` zwraca `null` gdy nie ma rejestracji — musisz sprawdzać null. `GetRequiredService<T>` rzuca `InvalidOperationException` gdy brak rejestracji — fail fast, jasny komunikat błędu. W praktyce prawie zawsze `GetRequiredService<T>` — jeśli serwis jest potrzebny i nie jest zarejestrowany, to błąd konfiguracji który chcesz wykryć wcześnie, nie NullReferenceException gdzieś głębiej.

**"Po co Options Pattern zamiast bezpośredniego `IConfiguration`?"** `IConfiguration["klucz"]` — stringly typed, brak IntelliSense, brak walidacji, błędy w runtime. Options Pattern — silnie typowana klasa, IntelliSense, walidacja przez DataAnnotations lub własną logikę, `ValidateOnStart()` rzuca wyjątek przy starcie jeśli config jest nieprawidłowy. Trzy warianty: `IOptions<T>` zamrożona wartość (Singleton-friendly), `IOptionsSnapshot<T>` odświeżana na request, `IOptionsMonitor<T>` live reload z powiadomieniem o zmianach.