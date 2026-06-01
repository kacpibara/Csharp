### Repository i Unit of Work w C#

---

### 1. Po co te wzorce — problem który rozwiązują

csharp

```csharp
// BEZ wzorców — DbContext bezpośrednio w kontrolerze/serwisie
public class ZamowieniaController
{
    private readonly SklepContext _ctx;

    public async Task<IActionResult> ZlozZamowienie(ZamowienieDto dto)
    {
        // Logika biznesowa wymieszana z dostępem do danych
        var klient = await _ctx.Klienci.FindAsync(dto.KlientId);
        var produkt = await _ctx.Produkty.FindAsync(dto.ProduktId);

        // Walidacja, reguły biznesowe... wszystko tu
        if (produkt.StanMagazynu < dto.Ilosc)
            return BadRequest("Brak w magazynie");

        var zamowienie = new Zamowienie { /* ... */ };
        _ctx.Zamowienia.Add(zamowienie);
        produkt.StanMagazynu -= dto.Ilosc;
        await _ctx.SaveChangesAsync();

        return Ok(zamowienie.Id);
    }
    // Problemy:
    // ❌ Nie można testować bez prawdziwej bazy
    // ❌ Logika dostępu do danych rozproszona po całej aplikacji
    // ❌ Trudno podmienić ORM (np. EF → Dapper)
    // ❌ Brak centralnego miejsca na wspólne zapytania
}

// Z wzorcami — czysty podział odpowiedzialności
// Controller → Service → Repository → DbContext
// Testowanie: podmień Repository na Mock — zero bazy danych!
```

---

### 2. Repository — podstawowa implementacja

csharp

```csharp
// Interfejs generyczny — kontrakt dla każdego repozytorium
public interface IRepository<T> where T : class
{
    Task<T?> PobierzPoIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> PobierzWszystkieAsync(CancellationToken ct = default);
    Task DodajAsync(T encja, CancellationToken ct = default);
    Task DodajWieleAsync(IEnumerable<T> encje, CancellationToken ct = default);
    void Aktualizuj(T encja);
    void Usun(T encja);
    Task<bool> IstniejeAsync(int id, CancellationToken ct = default);
    Task<int> LiczAsync(CancellationToken ct = default);
}

// Generyczna implementacja — wspólna logika dla wszystkich encji
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly SklepContext _ctx;
    protected readonly DbSet<T> _dbSet;

    public Repository(SklepContext ctx)
    {
        _ctx   = ctx;
        _dbSet = ctx.Set<T>();
    }

    public virtual async Task<T?> PobierzPoIdAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct);

    public virtual async Task<IReadOnlyList<T>> PobierzWszystkieAsync(
        CancellationToken ct = default)
        => await _dbSet.AsNoTracking().ToListAsync(ct);

    public virtual async Task DodajAsync(T encja, CancellationToken ct = default)
        => await _dbSet.AddAsync(encja, ct);

    public virtual async Task DodajWieleAsync(IEnumerable<T> encje,
        CancellationToken ct = default)
        => await _dbSet.AddRangeAsync(encje, ct);

    public virtual void Aktualizuj(T encja)
        => _dbSet.Update(encja);

    public virtual void Usun(T encja)
        => _dbSet.Remove(encja);

    public virtual async Task<bool> IstniejeAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct) != null;

    public virtual async Task<int> LiczAsync(CancellationToken ct = default)
        => await _dbSet.CountAsync(ct);
}
```

---

### 3. Specjalizowane repozytoria

csharp

```csharp
// Interfejsy specjalizowane — dodają metody specyficzne dla encji

public interface IProduktRepository : IRepository<Produkt>
{
    Task<IReadOnlyList<Produkt>> PobierzAktywneAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Produkt>> PobierzPoKategoriiAsync(int kategoriaId,
        CancellationToken ct = default);
    Task<IReadOnlyList<Produkt>> SzukajAsync(string? fraza, decimal? minCena,
        decimal? maxCena, CancellationToken ct = default);
    Task<Produkt?> PobierzZKategoriaPrzykladAsync(int id, CancellationToken ct = default);
    Task<bool> AktualizujStanAsync(int id, int zmiana, CancellationToken ct = default);
    Task<IReadOnlyList<Produkt>> PobierzNiskiStanAsync(int progStan,
        CancellationToken ct = default);
}

public interface IZamowienieRepository : IRepository<Zamowienie>
{
    Task<Zamowienie?> PobierzZPozycjamiAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Zamowienie>> PobierzPoKliencieAsync(int klientId,
        CancellationToken ct = default);
    Task<IReadOnlyList<Zamowienie>> PobierzPoStatusieAsync(string status,
        CancellationToken ct = default);
    Task<decimal> SumaSprzedazyAsync(DateTime od, DateTime do_,
        CancellationToken ct = default);
    Task<IReadOnlyList<ZamowieniePodsumowanieDto>> PobierzPodsumowaniaAsync(
        int strona, int rozmiar, CancellationToken ct = default);
}

public interface IKlientRepository : IRepository<Klient>
{
    Task<Klient?> PobierzPoEmailuAsync(string email, CancellationToken ct = default);
    Task<bool> EmailIstniejeAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<Klient>> PobierzAktywnychAsync(CancellationToken ct = default);
    Task<KlientStatystykiDto?> PobierzStatystykiAsync(int id,
        CancellationToken ct = default);
}

// Implementacje

public class ProduktRepository : Repository<Produkt>, IProduktRepository
{
    public ProduktRepository(SklepContext ctx) : base(ctx) { }

    public async Task<IReadOnlyList<Produkt>> PobierzAktywneAsync(
        CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(p => p.Aktywny)
            .OrderBy(p => p.Nazwa)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Produkt>> PobierzPoKategoriiAsync(
        int kategoriaId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(p => p.KategoriaId == kategoriaId && p.Aktywny)
            .OrderBy(p => p.Cena)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Produkt>> SzukajAsync(
        string? fraza, decimal? minCena, decimal? maxCena,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking().Where(p => p.Aktywny);

        if (!string.IsNullOrWhiteSpace(fraza))
            query = query.Where(p => p.Nazwa.Contains(fraza));

        if (minCena.HasValue)
            query = query.Where(p => p.Cena >= minCena.Value);

        if (maxCena.HasValue)
            query = query.Where(p => p.Cena <= maxCena.Value);

        return await query.OrderBy(p => p.Nazwa).ToListAsync(ct);
    }

    public async Task<Produkt?> PobierzZKategoriaPrzykladAsync(
        int id, CancellationToken ct = default)
        => await _dbSet
            .Include(p => p.Kategoria)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<bool> AktualizujStanAsync(
        int id, int zmiana, CancellationToken ct = default)
    {
        int zaktualizowanych = await _dbSet
            .Where(p => p.Id == id && p.StanMagazynu + zmiana >= 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StanMagazynu, p => p.StanMagazynu + zmiana), ct);

        return zaktualizowanych > 0;
    }

    public async Task<IReadOnlyList<Produkt>> PobierzNiskiStanAsync(
        int progStan, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(p => p.Aktywny && p.StanMagazynu <= progStan)
            .OrderBy(p => p.StanMagazynu)
            .ToListAsync(ct);
}

public class ZamowienieRepository : Repository<Zamowienie>, IZamowienieRepository
{
    public ZamowienieRepository(SklepContext ctx) : base(ctx) { }

    public async Task<Zamowienie?> PobierzZPozycjamiAsync(
        int id, CancellationToken ct = default)
        => await _dbSet
            .Include(z => z.Klient)
            .Include(z => z.Pozycje)
                .ThenInclude(p => p.Produkt)
            .FirstOrDefaultAsync(z => z.Id == id, ct);

    public async Task<IReadOnlyList<Zamowienie>> PobierzPoKliencieAsync(
        int klientId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(z => z.KlientId == klientId)
            .OrderByDescending(z => z.DataZlozenia)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Zamowienie>> PobierzPoStatusieAsync(
        string status, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(z => z.Status == status)
            .Include(z => z.Klient)
            .OrderByDescending(z => z.DataZlozenia)
            .ToListAsync(ct);

    public async Task<decimal> SumaSprzedazyAsync(
        DateTime od, DateTime do_, CancellationToken ct = default)
        => await _dbSet
            .Where(z => z.DataZlozenia >= od && z.DataZlozenia <= do_
                     && z.Status != "Anulowane")
            .SumAsync(z => z.SumaCalkowita, ct);

    public async Task<IReadOnlyList<ZamowieniePodsumowanieDto>> PobierzPodsumowaniaAsync(
        int strona, int rozmiar, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .OrderByDescending(z => z.DataZlozenia)
            .Skip((strona - 1) * rozmiar)
            .Take(rozmiar)
            .Select(z => new ZamowieniePodsumowanieDto(
                z.Id,
                $"{z.Klient.Imie} {z.Klient.Nazwisko}",
                z.DataZlozenia,
                z.SumaCalkowita,
                z.Status,
                z.Pozycje.Count))
            .ToListAsync(ct);
}

public class KlientRepository : Repository<Klient>, IKlientRepository
{
    public KlientRepository(SklepContext ctx) : base(ctx) { }

    public async Task<Klient?> PobierzPoEmailuAsync(
        string email, CancellationToken ct = default)
        => await _dbSet
            .FirstOrDefaultAsync(k => k.Email == email, ct);

    public async Task<bool> EmailIstniejeAsync(
        string email, CancellationToken ct = default)
        => await _dbSet
            .AnyAsync(k => k.Email == email, ct);

    public async Task<IReadOnlyList<Klient>> PobierzAktywnychAsync(
        CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(k => k.Aktywny)
            .OrderBy(k => k.Nazwisko)
            .ThenBy(k => k.Imie)
            .ToListAsync(ct);

    public async Task<KlientStatystykiDto?> PobierzStatystykiAsync(
        int id, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(k => k.Id == id)
            .Select(k => new KlientStatystykiDto(
                k.Id,
                $"{k.Imie} {k.Nazwisko}",
                k.Zamowienia.Count,
                k.Zamowienia.Sum(z => z.SumaCalkowita),
                k.Zamowienia.Max(z => (DateTime?)z.DataZlozenia),
                k.Zamowienia.Count(z => z.Status == "Anulowane")))
            .FirstOrDefaultAsync(ct);
}

// DTOs
public record ZamowieniePodsumowanieDto(int Id, string Klient, DateTime Data,
    decimal Suma, string Status, int LiczbaPozycji);
public record KlientStatystykiDto(int Id, string Nazwa, int LiczbaZamowien,
    decimal SumaZakupow, DateTime? OstatniaData, int LiczbaAnulowanych);
```

---

### 4. Unit of Work — koordynator

csharp

```csharp
// UoW gromadzi wszystkie repozytoria i zarządza jedną transakcją
// Jeden SaveChanges = atomowe zatwierdzenie WSZYSTKICH zmian

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    IProduktRepository   Produkty   { get; }
    IZamowienieRepository Zamowienia { get; }
    IKlientRepository    Klienci    { get; }

    Task<int> ZapiszAsync(CancellationToken ct = default);
    Task RozpocznijTransakcjeAsync(CancellationToken ct = default);
    Task ZatwierdzAsync(CancellationToken ct = default);
    Task CofnijAsync(CancellationToken ct = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly SklepContext _ctx;
    private IDbContextTransaction? _trx;

    // Lazy initialization — twórz repozytorium tylko gdy potrzebne
    private IProduktRepository?    _produkty;
    private IZamowienieRepository? _zamowienia;
    private IKlientRepository?     _klienci;

    public UnitOfWork(SklepContext ctx) => _ctx = ctx;

    // Wszystkie repozytoria dzielą TEN SAM kontekst!
    public IProduktRepository Produkty
        => _produkty ??= new ProduktRepository(_ctx);

    public IZamowienieRepository Zamowienia
        => _zamowienia ??= new ZamowienieRepository(_ctx);

    public IKlientRepository Klienci
        => _klienci ??= new KlientRepository(_ctx);

    // Jeden SaveChanges — atomowe zatwierdzenie wszystkich zmian
    public async Task<int> ZapiszAsync(CancellationToken ct = default)
        => await _ctx.SaveChangesAsync(ct);

    public async Task RozpocznijTransakcjeAsync(CancellationToken ct = default)
    {
        if (_trx != null)
            throw new InvalidOperationException("Transakcja już otwarta");
        _trx = await _ctx.Database.BeginTransactionAsync(ct);
    }

    public async Task ZatwierdzAsync(CancellationToken ct = default)
    {
        if (_trx == null)
            throw new InvalidOperationException("Brak otwartej transakcji");

        await _ctx.SaveChangesAsync(ct);
        await _trx.CommitAsync(ct);
        await _trx.DisposeAsync();
        _trx = null;
    }

    public async Task CofnijAsync(CancellationToken ct = default)
    {
        if (_trx == null) return;
        await _trx.RollbackAsync(ct);
        await _trx.DisposeAsync();
        _trx = null;
    }

    public void Dispose()
    {
        _trx?.Dispose();
        _ctx.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_trx != null) await _trx.DisposeAsync();
        await _ctx.DisposeAsync();
    }
}
```

---

### 5. Rejestracja w DI

csharp

```csharp
// Program.cs — rejestracja wszystkich komponentów

var builder = WebApplication.CreateBuilder(args);

// DbContext — Scoped (jeden na request HTTP)
builder.Services.AddDbContext<SklepContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Sklep")));

// Unit of Work — Scoped (jeden na request, ten sam co DbContext)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Repozytoria osobno — opcjonalne, gdy nie używasz UoW
// builder.Services.AddScoped<IProduktRepository, ProduktRepository>();
// builder.Services.AddScoped<IZamowienieRepository, ZamowienieRepository>();
// builder.Services.AddScoped<IKlientRepository, KlientRepository>();

// Serwisy aplikacyjne
builder.Services.AddScoped<ZamowieniaApplicationService>();
builder.Services.AddScoped<ProduktApplicationService>();
```

---

### 6. Serwisy aplikacyjne używające UoW

csharp

```csharp
// Serwis orkiestruje repozytoria przez UoW — logika biznesowa

public class ZamowieniaApplicationService
{
    private readonly IUnitOfWork _uow;

    public ZamowieniaApplicationService(IUnitOfWork uow) => _uow = uow;

    // Złożenie zamówienia — wiele repozytoriów, jedna transakcja
    public async Task<int> ZlozZamowienieAsync(
        int klientId,
        IEnumerable<(int ProduktId, int Ilosc)> pozycje,
        CancellationToken ct = default)
    {
        // Walidacja klienta
        var klient = await _uow.Klienci.PobierzPoIdAsync(klientId, ct)
            ?? throw new DomainException($"Klient #{klientId} nie istnieje");

        if (!klient.Aktywny)
            throw new DomainException("Klient jest nieaktywny");

        var pozycjeList = pozycje.ToList();
        var prodIds = pozycjeList.Select(p => p.ProduktId).ToArray();

        // Pobierz wszystkie produkty naraz
        var produkty = await _uow.Produkty.SzukajAsync(null, null, null, ct);
        var produktyDict = produkty
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionary(p => p.Id);

        // Walidacja produktów i stanów
        foreach (var (prodId, ilosc) in pozycjeList)
        {
            if (!produktyDict.TryGetValue(prodId, out var prod))
                throw new DomainException($"Produkt #{prodId} nie istnieje");
            if (!prod.Aktywny)
                throw new DomainException($"Produkt '{prod.Nazwa}' jest nieaktywny");
            if (prod.StanMagazynu < ilosc)
                throw new DomainException(
                    $"'{prod.Nazwa}': stan {prod.StanMagazynu}, potrzeba {ilosc}");
        }

        // Wszystko OK — wykonaj w transakcji
        await _uow.RozpocznijTransakcjeAsync(ct);
        try
        {
            decimal suma = 0;
            var zamowienie = new Zamowienie
            {
                KlientId = klientId,
                Status   = "Nowe"
            };
            await _uow.Zamowienia.DodajAsync(zamowienie, ct);
            await _uow.ZapiszAsync(ct);  // potrzebujemy Id zamówienia

            var pozycjeEncje = new List<PozycjaZamowienia>();
            foreach (var (prodId, ilosc) in pozycjeList)
            {
                var prod = produktyDict[prodId];
                decimal cena = prod.Cena;

                pozycjeEncje.Add(new PozycjaZamowienia
                {
                    ZamowienieId  = zamowienie.Id,
                    ProduktId     = prodId,
                    Ilosc         = ilosc,
                    CenaW_Chwili  = cena
                });

                // Zmniejsz stan przez specjalizowaną metodę
                bool ok = await _uow.Produkty.AktualizujStanAsync(prodId, -ilosc, ct);
                if (!ok) throw new DomainException($"Nie udało się zarezerwować produktu #{prodId}");

                suma += cena * ilosc;
            }

            // Dodaj pozycje
            await _uow.Zamowienia.DodajAsync(zamowienie, ct);  // jeśli mamy repozytorium pozycji
            zamowienie.SumaCalkowita = suma;

            await _uow.ZatwierdzAsync(ct);
            return zamowienie.Id;
        }
        catch
        {
            await _uow.CofnijAsync(ct);
            throw;
        }
    }

    public async Task<bool> AnulujZamowienieAsync(
        int zamowienieId, string powod, CancellationToken ct = default)
    {
        var zamowienie = await _uow.Zamowienia.PobierzZPozycjamiAsync(zamowienieId, ct)
            ?? throw new DomainException($"Zamówienie #{zamowienieId} nie istnieje");

        if (zamowienie.Status == "Anulowane")
            throw new DomainException("Zamówienie już anulowane");

        if (zamowienie.Status == "Dostarczone")
            throw new DomainException("Nie można anulować dostarczonego zamówienia");

        await _uow.RozpocznijTransakcjeAsync(ct);
        try
        {
            // Przywróć stany magazynowe
            foreach (var pozycja in zamowienie.Pozycje)
            {
                await _uow.Produkty.AktualizujStanAsync(
                    pozycja.ProduktId, pozycja.Ilosc, ct);  // +ilosc (przywróć)
            }

            zamowienie.Status = "Anulowane";
            _uow.Zamowienia.Aktualizuj(zamowienie);

            await _uow.ZatwierdzAsync(ct);
            return true;
        }
        catch
        {
            await _uow.CofnijAsync(ct);
            throw;
        }
    }
}

public class ProduktApplicationService
{
    private readonly IUnitOfWork _uow;

    public ProduktApplicationService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<Produkt>> PobierzKatalogAsync(
        string? kategoria = null,
        decimal? minCena = null,
        decimal? maxCena = null,
        CancellationToken ct = default)
        => await _uow.Produkty.SzukajAsync(kategoria, minCena, maxCena, ct);

    public async Task<int> DodajProduktAsync(
        string nazwa, decimal cena, int kategoriaId,
        int stan = 0, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nazwa))
            throw new DomainException("Nazwa produktu jest wymagana");
        if (cena <= 0)
            throw new DomainException("Cena musi być dodatnia");

        var produkt = new Produkt
        {
            Nazwa       = nazwa,
            Cena        = cena,
            KategoriaId = kategoriaId,
            StanMagazynu = stan
        };

        await _uow.Produkty.DodajAsync(produkt, ct);
        await _uow.ZapiszAsync(ct);
        return produkt.Id;
    }

    public async Task<IReadOnlyList<Produkt>> PobierzAlertyStanuAsync(
        int prog = 5, CancellationToken ct = default)
        => await _uow.Produkty.PobierzNiskiStanAsync(prog, ct);
}

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
```

---

### 7. Testowanie z mockami

csharp

```csharp
// Wielka zaleta wzorców — łatwe testowanie!

using Moq;
using Xunit;

public class ZamowieniaServiceTests
{
    private readonly Mock<IUnitOfWork>             _uowMock;
    private readonly Mock<IProduktRepository>      _produktyMock;
    private readonly Mock<IZamowienieRepository>   _zamowieniaMock;
    private readonly Mock<IKlientRepository>       _klienciMock;
    private readonly ZamowieniaApplicationService  _sut;

    public ZamowieniaServiceTests()
    {
        _uowMock        = new Mock<IUnitOfWork>();
        _produktyMock   = new Mock<IProduktRepository>();
        _zamowieniaMock = new Mock<IZamowienieRepository>();
        _klienciMock    = new Mock<IKlientRepository>();

        // Podłącz mockowane repozytoria do UoW
        _uowMock.Setup(u => u.Produkty).Returns(_produktyMock.Object);
        _uowMock.Setup(u => u.Zamowienia).Returns(_zamowieniaMock.Object);
        _uowMock.Setup(u => u.Klienci).Returns(_klienciMock.Object);

        _sut = new ZamowieniaApplicationService(_uowMock.Object);
    }

    [Fact]
    public async Task ZlozZamowienie_NieistniejacyKlient_RzucaDomainException()
    {
        // Arrange
        _klienciMock
            .Setup(r => r.PobierzPoIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Klient?)null);  // klient nie istnieje

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ZlozZamowienieAsync(99, new[] { (1, 2) }));
    }

    [Fact]
    public async Task ZlozZamowienie_NiedostatecznyStanMagazynowy_RzucaDomainException()
    {
        // Arrange
        var klient = new Klient { Id = 1, Imie = "Test", Aktywny = true };
        var produkt = new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m,
            StanMagazynu = 1, Aktywny = true };

        _klienciMock
            .Setup(r => r.PobierzPoIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(klient);

        _produktyMock
            .Setup(r => r.SzukajAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Produkt> { produkt });

        // Act & Assert — chcemy 5 ale mamy tylko 1
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ZlozZamowienieAsync(1, new[] { (ProduktId: 1, Ilosc: 5) }));

        Assert.Contains("stan 1, potrzeba 5", ex.Message);
    }

    [Fact]
    public async Task ZlozZamowienie_PoprawneZamowienie_ZwracaId()
    {
        // Arrange
        var klient  = new Klient { Id = 1, Imie = "Anna", Aktywny = true };
        var produkt = new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m,
            StanMagazynu = 10, Aktywny = true };

        _klienciMock
            .Setup(r => r.PobierzPoIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(klient);

        _produktyMock
            .Setup(r => r.SzukajAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Produkt> { produkt });

        _produktyMock
            .Setup(r => r.AktualizujStanAsync(1, -2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _uowMock.Setup(u => u.ZapiszAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Symuluj nadanie Id przez bazę
        _zamowieniaMock
            .Setup(r => r.DodajAsync(It.IsAny<Zamowienie>(), It.IsAny<CancellationToken>()))
            .Callback<Zamowienie, CancellationToken>((z, _) => z.Id = 42);

        // Act
        int id = await _sut.ZlozZamowienieAsync(1, new[] { (ProduktId: 1, Ilosc: 2) });

        // Assert
        Assert.Equal(42, id);
        _uowMock.Verify(u => u.RozpocznijTransakcjeAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.ZatwierdzAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CofnijAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ZlozZamowienie_BladPodczasBazy_WywolujeRollback()
    {
        // Arrange
        var klient  = new Klient { Id = 1, Aktywny = true };
        var produkt = new Produkt { Id = 1, Cena = 100m, StanMagazynu = 5, Aktywny = true };

        _klienciMock.Setup(r => r.PobierzPoIdAsync(1, default)).ReturnsAsync(klient);
        _produktyMock.Setup(r => r.SzukajAsync(null, null, null, default))
            .ReturnsAsync(new List<Produkt> { produkt });
        _produktyMock.Setup(r => r.AktualizujStanAsync(1, -1, default)).ReturnsAsync(true);
        _uowMock.Setup(u => u.ZapiszAsync(default))
            .ThrowsAsync(new Exception("Błąd bazy danych"));  // symuluj błąd

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _sut.ZlozZamowienieAsync(1, new[] { (ProduktId: 1, Ilosc: 1) }));

        // Verify rollback wywołany!
        _uowMock.Verify(u => u.CofnijAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

### 8. Specification Pattern — zaawansowane zapytania

csharp

```csharp
// Specyfikacja hermetyzuje logikę filtrowania — wielokrotnego użytku

public abstract class Specyfikacja<T>
{
    public abstract Expression<Func<T, bool>> Wyrazenie { get; }

    public bool Spelnia(T encja) => Wyrazenie.Compile()(encja);

    public Specyfikacja<T> And(Specyfikacja<T> inna) =>
        new AndSpecyfikacja<T>(this, inna);

    public Specyfikacja<T> Or(Specyfikacja<T> inna) =>
        new OrSpecyfikacja<T>(this, inna);

    public Specyfikacja<T> Not() =>
        new NotSpecyfikacja<T>(this);
}

public class AndSpecyfikacja<T> : Specyfikacja<T>
{
    private readonly Specyfikacja<T> _a, _b;

    public AndSpecyfikacja(Specyfikacja<T> a, Specyfikacja<T> b)
    {
        _a = a; _b = b;
    }

    public override Expression<Func<T, bool>> Wyrazenie
    {
        get
        {
            var param = Expression.Parameter(typeof(T));
            var body = Expression.AndAlso(
                Expression.Invoke(_a.Wyrazenie, param),
                Expression.Invoke(_b.Wyrazenie, param));
            return Expression.Lambda<Func<T, bool>>(body, param);
        }
    }
}

public class OrSpecyfikacja<T> : Specyfikacja<T>
{
    private readonly Specyfikacja<T> _a, _b;

    public OrSpecyfikacja(Specyfikacja<T> a, Specyfikacja<T> b)
    {
        _a = a; _b = b;
    }

    public override Expression<Func<T, bool>> Wyrazenie
    {
        get
        {
            var param = Expression.Parameter(typeof(T));
            var body = Expression.OrElse(
                Expression.Invoke(_a.Wyrazenie, param),
                Expression.Invoke(_b.Wyrazenie, param));
            return Expression.Lambda<Func<T, bool>>(body, param);
        }
    }
}

public class NotSpecyfikacja<T> : Specyfikacja<T>
{
    private readonly Specyfikacja<T> _wew;
    public NotSpecyfikacja(Specyfikacja<T> wew) => _wew = wew;

    public override Expression<Func<T, bool>> Wyrazenie
    {
        get
        {
            var param = Expression.Parameter(typeof(T));
            var body = Expression.Not(Expression.Invoke(_wew.Wyrazenie, param));
            return Expression.Lambda<Func<T, bool>>(body, param);
        }
    }
}

// Konkretne specyfikacje
public class ProduktAktywnySpec : Specyfikacja<Produkt>
{
    public override Expression<Func<Produkt, bool>> Wyrazenie =>
        p => p.Aktywny;
}

public class ProduktWKategoriiSpec : Specyfikacja<Produkt>
{
    private readonly int _kategoriaId;
    public ProduktWKategoriiSpec(int kategoriaId) => _kategoriaId = kategoriaId;

    public override Expression<Func<Produkt, bool>> Wyrazenie =>
        p => p.KategoriaId == _kategoriaId;
}

public class ProduktWCenieSpec : Specyfikacja<Produkt>
{
    private readonly decimal _min, _max;
    public ProduktWCenieSpec(decimal min, decimal max) { _min = min; _max = max; }

    public override Expression<Func<Produkt, bool>> Wyrazenie =>
        p => p.Cena >= _min && p.Cena <= _max;
}

public class NiskiStanSpec : Specyfikacja<Produkt>
{
    private readonly int _prog;
    public NiskiStanSpec(int prog) => _prog = prog;

    public override Expression<Func<Produkt, bool>> Wyrazenie =>
        p => p.StanMagazynu <= _prog;
}

// Repozytorium ze specyfikacjami
public interface IProduktRepositoryV2 : IRepository<Produkt>
{
    Task<IReadOnlyList<Produkt>> ZnajdzAsync(Specyfikacja<Produkt> spec,
        CancellationToken ct = default);
    Task<int> LiczAsync(Specyfikacja<Produkt> spec, CancellationToken ct = default);
}

public class ProduktRepositoryV2 : Repository<Produkt>, IProduktRepositoryV2
{
    public ProduktRepositoryV2(SklepContext ctx) : base(ctx) { }

    public async Task<IReadOnlyList<Produkt>> ZnajdzAsync(
        Specyfikacja<Produkt> spec, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(spec.Wyrazenie)
            .OrderBy(p => p.Nazwa)
            .ToListAsync(ct);

    public async Task<int> LiczAsync(
        Specyfikacja<Produkt> spec, CancellationToken ct = default)
        => await _dbSet.CountAsync(spec.Wyrazenie, ct);
}

// Użycie specyfikacji — czytelny, wielokrotnego użytku kod
var aktywne     = new ProduktAktywnySpec();
var it          = new ProduktWKategoriiSpec(1);
var sredniPolka = new ProduktWCenieSpec(200m, 2000m);
var niskiStan   = new NiskiStanSpec(5);

// Składaj specyfikacje dowolnie
var alertITMalyStan   = aktywne.And(it).And(niskiStan);
var tanielubMaleIT    = aktywne.And(it.And(sredniPolka).Or(niskiStan));
var nieIT             = aktywne.And(it.Not());

var produktyRepo = new ProduktRepositoryV2(ctx);

var produktyAlert = await produktyRepo.ZnajdzAsync(alertITMalyStan);
Console.WriteLine($"IT z niskim stanem: {produktyAlert.Count}");

var liczbaNieIT = await produktyRepo.LiczAsync(nieIT);
Console.WriteLine($"Nie-IT aktywnych: {liczbaNieIT}");
```

---

### Typowe pytania rekrutacyjne

**"Co to Repository Pattern i jakie problemy rozwiązuje?"** Repository to abstrakcja warstwy dostępu do danych — ukrywa szczegóły jak (EF Core, Dapper, ADO.NET) za interfejsem opisującym CO. Rozwiązuje: (1) testowalność — można podmienić na mock bez bazy, (2) separację — logika biznesowa nie zna szczegółów ORM, (3) centralność — wspólne zapytania w jednym miejscu, (4) wymienność — można zmienić ORM bez dotykania serwisów.

**"Co to Unit of Work i czym różni się od repozytorium?"** Repository zarządza dostępem do JEDNEGO typu encji. Unit of Work grupuje WIELE repozytoriów i zarządza jedną transakcją dla wszystkich operacji. `SaveChanges` raz — atomowy commit dla zmian z wielu repozytoriów. UoW zapewnia że zapis klienta i zamówienia albo się udaje w całości, albo jest wycofany.

**"Czy Repository nad EF Core ma sens — EF już implementuje Repository i UoW?"** Kontrowersyjne pytanie! DbSet to Repository, DbContext to UoW. Dodawanie kolejnej warstwy to overhead. ALE: bez Repository mockowanie wymaga InMemory database lub specjalnych bibliotek. Z Repository — prosty Mock.Setup. Kompromis: używaj Repository gdy testowalność jest priorytetem i logika biznesowa jest złożona. Dla CRUD-only aplikacji bezpośredni DbContext w serwisach może być OK.

**"Jak przetestować serwis bez bazy danych?"** Z Repository + UoW — Moq.Setup zwraca dane testowe, weryfikujesz czy SaveChanges wywołany. Bez Repository — EF Core InMemoryDatabase lub SQLite in-memory, ale to wolniejsze i bardziej skomplikowane. Najlepszy test: jednostkowy z mockami (ms), akceptowalny: integracyjny z InMemory (sekundy). Wzorzec Repository czyni testy jednostkowe trywialnie łatwymi.