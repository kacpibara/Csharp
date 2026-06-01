namespace _05_Databases;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

// ─── Encje (suffix RU) ───────────────────────────────────────────────────────

public class KategoriaRU
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";
    public ICollection<ProduktRU> Produkty { get; set; } = new List<ProduktRU>();
}

public class ProduktRU
{
    public int     Id           { get; set; }
    public string  Nazwa        { get; set; } = "";
    public decimal Cena         { get; set; }
    public int     StanMagazynu { get; set; }
    public bool    Aktywny      { get; set; } = true;

    public int        KategoriaId { get; set; }
    public KategoriaRU Kategoria  { get; set; } = null!;
    public ICollection<PozycjaZamowieniaRU> Pozycje { get; set; } = new List<PozycjaZamowieniaRU>();
}

public class KlientRU
{
    public int    Id       { get; set; }
    public string Imie     { get; set; } = "";
    public string Nazwisko { get; set; } = "";
    public string Email    { get; set; } = "";
    public bool   Aktywny  { get; set; } = true;

    public ICollection<ZamowienieRU> Zamowienia { get; set; } = new List<ZamowienieRU>();
}

public class ZamowienieRU
{
    public int      Id            { get; set; }
    public string   Status        { get; set; } = "Nowe";
    public decimal  SumaCalkowita { get; set; }
    public DateTime DataZlozenia  { get; set; } = DateTime.UtcNow;

    public int      KlientId { get; set; }
    public KlientRU Klient   { get; set; } = null!;

    public ICollection<PozycjaZamowieniaRU> Pozycje { get; set; } = new List<PozycjaZamowieniaRU>();
}

public class PozycjaZamowieniaRU
{
    public int     Id          { get; set; }
    public int     Ilosc       { get; set; }
    public decimal CenaWChwili { get; set; }

    public int          ZamowienieId { get; set; }
    public ZamowienieRU Zamowienie   { get; set; } = null!;

    public int       ProduktId { get; set; }
    public ProduktRU Produkt   { get; set; } = null!;
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record ZamowieniePodsumowanieDtoRU(int Id, string Klient,
    DateTime Data, decimal Suma, string Status, int LiczbaPozycji);
public record KlientStatystykiDtoRU(int Id, string Nazwa,
    int LiczbaZamowien, double SumaZakupow, DateTime? OstatniaData, int LiczbaAnulowanych);

// ─── DbContext ────────────────────────────────────────────────────────────────

public class SklepRUContext : DbContext
{
    public DbSet<KategoriaRU>        Kategorie   { get; set; }
    public DbSet<ProduktRU>          Produkty    { get; set; }
    public DbSet<KlientRU>           Klienci     { get; set; }
    public DbSet<ZamowienieRU>       Zamowienia  { get; set; }
    public DbSet<PozycjaZamowieniaRU> PozycjeZam { get; set; }

    public SklepRUContext(DbContextOptions<SklepRUContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<ProduktRU>()
          .HasOne(p => p.Kategoria).WithMany(k => k.Produkty)
          .HasForeignKey(p => p.KategoriaId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ZamowienieRU>()
          .HasOne(z => z.Klient).WithMany(k => k.Zamowienia)
          .HasForeignKey(z => z.KlientId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PozycjaZamowieniaRU>()
          .HasOne(p => p.Zamowienie).WithMany(z => z.Pozycje)
          .HasForeignKey(p => p.ZamowienieId);
        mb.Entity<PozycjaZamowieniaRU>()
          .HasOne(p => p.Produkt).WithMany(pr => pr.Pozycje)
          .HasForeignKey(p => p.ProduktId);
    }
}

// ─── Generic Repository ───────────────────────────────────────────────────────

public interface IRepositoryRU<T> where T : class
{
    Task<T?>                   PobierzPoIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>>     PobierzWszystkieAsync(CancellationToken ct = default);
    Task                       DodajAsync(T encja, CancellationToken ct = default);
    Task                       DodajWieleAsync(IEnumerable<T> encje, CancellationToken ct = default);
    void                       Aktualizuj(T encja);
    void                       Usun(T encja);
    Task<bool>                 IstniejeAsync(int id, CancellationToken ct = default);
    Task<int>                  LiczAsync(CancellationToken ct = default);
}

public class RepositoryRU<T> : IRepositoryRU<T> where T : class
{
    protected readonly SklepRUContext _ctx;
    protected readonly DbSet<T>       _dbSet;

    public RepositoryRU(SklepRUContext ctx)
    {
        _ctx   = ctx;
        _dbSet = ctx.Set<T>();
    }

    public virtual async Task<T?> PobierzPoIdAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct);

    public virtual async Task<IReadOnlyList<T>> PobierzWszystkieAsync(CancellationToken ct = default)
        => await _dbSet.AsNoTracking().ToListAsync(ct);

    public virtual async Task DodajAsync(T encja, CancellationToken ct = default)
        => await _dbSet.AddAsync(encja, ct);

    public virtual async Task DodajWieleAsync(IEnumerable<T> encje, CancellationToken ct = default)
        => await _dbSet.AddRangeAsync(encje, ct);

    public virtual void Aktualizuj(T encja) => _dbSet.Update(encja);

    public virtual void Usun(T encja) => _dbSet.Remove(encja);

    public virtual async Task<bool> IstniejeAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct) != null;

    public virtual async Task<int> LiczAsync(CancellationToken ct = default)
        => await _dbSet.CountAsync(ct);
}

// ─── Specjalizowane repozytoria ───────────────────────────────────────────────

public interface IProduktRepositoryRU : IRepositoryRU<ProduktRU>
{
    Task<IReadOnlyList<ProduktRU>> PobierzAktywneAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProduktRU>> SzukajAsync(string? fraza, decimal? minCena,
        decimal? maxCena, CancellationToken ct = default);
    Task<ProduktRU?>               PobierzZKategoriaAsync(int id, CancellationToken ct = default);
    Task<bool>                     AktualizujStanAsync(int id, int zmiana, CancellationToken ct = default);
    Task<IReadOnlyList<ProduktRU>> PobierzNiskiStanAsync(int prog, CancellationToken ct = default);
}

public class ProduktRepositoryRU : RepositoryRU<ProduktRU>, IProduktRepositoryRU
{
    public ProduktRepositoryRU(SklepRUContext ctx) : base(ctx) { }

    public async Task<IReadOnlyList<ProduktRU>> PobierzAktywneAsync(CancellationToken ct = default)
        => await _dbSet.AsNoTracking().Where(p => p.Aktywny).OrderBy(p => p.Nazwa).ToListAsync(ct);

    public async Task<IReadOnlyList<ProduktRU>> SzukajAsync(
        string? fraza, decimal? minCena, decimal? maxCena, CancellationToken ct = default)
    {
        var q = _dbSet.AsNoTracking().Where(p => p.Aktywny);
        if (!string.IsNullOrWhiteSpace(fraza)) q = q.Where(p => p.Nazwa.Contains(fraza));
        if (minCena.HasValue) q = q.Where(p => p.Cena >= minCena.Value);
        if (maxCena.HasValue) q = q.Where(p => p.Cena <= maxCena.Value);
        return await q.OrderBy(p => p.Nazwa).ToListAsync(ct);
    }

    public async Task<ProduktRU?> PobierzZKategoriaAsync(int id, CancellationToken ct = default)
        => await _dbSet.Include(p => p.Kategoria).AsNoTracking()
                       .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<bool> AktualizujStanAsync(int id, int zmiana, CancellationToken ct = default)
    {
        int rows = await _dbSet
            .Where(p => p.Id == id && p.StanMagazynu + zmiana >= 0)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StanMagazynu,
                p => p.StanMagazynu + zmiana), ct);
        return rows > 0;
    }

    public async Task<IReadOnlyList<ProduktRU>> PobierzNiskiStanAsync(
        int prog, CancellationToken ct = default)
        => await _dbSet.AsNoTracking()
                       .Where(p => p.Aktywny && p.StanMagazynu <= prog)
                       .OrderBy(p => p.StanMagazynu)
                       .ToListAsync(ct);
}

public interface IZamowienieRepositoryRU : IRepositoryRU<ZamowienieRU>
{
    Task<ZamowienieRU?>                     PobierzZPozycjamiAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ZamowienieRU>>       PobierzPoKliencieAsync(int klientId, CancellationToken ct = default);
    Task<decimal>                           SumaSprzedazyAsync(DateTime od, DateTime do_, CancellationToken ct = default);
    Task<IReadOnlyList<ZamowieniePodsumowanieDtoRU>> PobierzPodsumowaniaAsync(int strona, int rozmiar, CancellationToken ct = default);
}

public class ZamowienieRepositoryRU : RepositoryRU<ZamowienieRU>, IZamowienieRepositoryRU
{
    public ZamowienieRepositoryRU(SklepRUContext ctx) : base(ctx) { }

    public async Task<ZamowienieRU?> PobierzZPozycjamiAsync(int id, CancellationToken ct = default)
        => await _dbSet.Include(z => z.Klient)
                       .Include(z => z.Pozycje).ThenInclude(p => p.Produkt)
                       .FirstOrDefaultAsync(z => z.Id == id, ct);

    public async Task<IReadOnlyList<ZamowienieRU>> PobierzPoKliencieAsync(
        int klientId, CancellationToken ct = default)
        => await _dbSet.AsNoTracking()
                       .Where(z => z.KlientId == klientId)
                       .OrderByDescending(z => z.DataZlozenia)
                       .ToListAsync(ct);

    public async Task<decimal> SumaSprzedazyAsync(
        DateTime od, DateTime do_, CancellationToken ct = default)
        => await _dbSet
            .Where(z => z.DataZlozenia >= od && z.DataZlozenia <= do_ && z.Status != "Anulowane")
            .SumAsync(z => z.SumaCalkowita, ct);

    public async Task<IReadOnlyList<ZamowieniePodsumowanieDtoRU>> PobierzPodsumowaniaAsync(
        int strona, int rozmiar, CancellationToken ct = default)
        => await _dbSet.AsNoTracking()
            .OrderByDescending(z => z.DataZlozenia)
            .Skip((strona - 1) * rozmiar)
            .Take(rozmiar)
            .Select(z => new ZamowieniePodsumowanieDtoRU(
                z.Id,
                z.Klient.Imie + " " + z.Klient.Nazwisko,
                z.DataZlozenia, z.SumaCalkowita, z.Status,
                z.Pozycje.Count))
            .ToListAsync(ct);
}

public interface IKlientRepositoryRU : IRepositoryRU<KlientRU>
{
    Task<KlientRU?>                   PobierzPoEmailuAsync(string email, CancellationToken ct = default);
    Task<bool>                        EmailIstniejeAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<KlientRU>>     PobierzAktywnychAsync(CancellationToken ct = default);
    Task<KlientStatystykiDtoRU?>      PobierzStatystykiAsync(int id, CancellationToken ct = default);
}

public class KlientRepositoryRU : RepositoryRU<KlientRU>, IKlientRepositoryRU
{
    public KlientRepositoryRU(SklepRUContext ctx) : base(ctx) { }

    public async Task<KlientRU?> PobierzPoEmailuAsync(string email, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(k => k.Email == email, ct);

    public async Task<bool> EmailIstniejeAsync(string email, CancellationToken ct = default)
        => await _dbSet.AnyAsync(k => k.Email == email, ct);

    public async Task<IReadOnlyList<KlientRU>> PobierzAktywnychAsync(CancellationToken ct = default)
        => await _dbSet.AsNoTracking().Where(k => k.Aktywny)
                       .OrderBy(k => k.Nazwisko).ThenBy(k => k.Imie)
                       .ToListAsync(ct);

    public async Task<KlientStatystykiDtoRU?> PobierzStatystykiAsync(
        int id, CancellationToken ct = default)
        => await _dbSet.AsNoTracking()
            .Where(k => k.Id == id)
            .Select(k => new KlientStatystykiDtoRU(
                k.Id,
                k.Imie + " " + k.Nazwisko,
                k.Zamowienia.Count,
                k.Zamowienia.Sum(z => (double)z.SumaCalkowita),
                k.Zamowienia.Max(z => (DateTime?)z.DataZlozenia),
                k.Zamowienia.Count(z => z.Status == "Anulowane")))
            .FirstOrDefaultAsync(ct);
}

// ─── Unit of Work ─────────────────────────────────────────────────────────────

public interface IUnitOfWorkRU : IDisposable, IAsyncDisposable
{
    IProduktRepositoryRU     Produkty   { get; }
    IZamowienieRepositoryRU  Zamowienia { get; }
    IKlientRepositoryRU      Klienci    { get; }

    Task<int> ZapiszAsync(CancellationToken ct = default);
    Task RozpocznijTransakcjeAsync(CancellationToken ct = default);
    Task ZatwierdzAsync(CancellationToken ct = default);
    Task CofnijAsync(CancellationToken ct = default);
}

public class UnitOfWorkRU : IUnitOfWorkRU
{
    private readonly SklepRUContext _ctx;
    private IDbContextTransaction? _trx;

    // Lazy initialization — repozytorium tworzone tylko gdy potrzebne
    private IProduktRepositoryRU?    _produkty;
    private IZamowienieRepositoryRU? _zamowienia;
    private IKlientRepositoryRU?     _klienci;

    public UnitOfWorkRU(SklepRUContext ctx) => _ctx = ctx;

    // WSZYSTKIE repozytoria dzielą TEN SAM _ctx — jedna baza, jedna transakcja
    public IProduktRepositoryRU    Produkty    => _produkty    ??= new ProduktRepositoryRU(_ctx);
    public IZamowienieRepositoryRU Zamowienia  => _zamowienia  ??= new ZamowienieRepositoryRU(_ctx);
    public IKlientRepositoryRU     Klienci     => _klienci     ??= new KlientRepositoryRU(_ctx);

    // Jeden SaveChanges = atomowe zatwierdzenie WSZYSTKICH zmian ze wszystkich repozytoriów
    public async Task<int> ZapiszAsync(CancellationToken ct = default)
        => await _ctx.SaveChangesAsync(ct);

    public async Task RozpocznijTransakcjeAsync(CancellationToken ct = default)
    {
        if (_trx != null) throw new InvalidOperationException("Transakcja już otwarta");
        _trx = await _ctx.Database.BeginTransactionAsync(ct);
    }

    public async Task ZatwierdzAsync(CancellationToken ct = default)
    {
        if (_trx == null) throw new InvalidOperationException("Brak transakcji");
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

// ─── Domain Exception ─────────────────────────────────────────────────────────

public class DomainExceptionRU : Exception
{
    public DomainExceptionRU(string message) : base(message) { }
}

// ─── Application Services ─────────────────────────────────────────────────────

public class ZamowieniaApplicationServiceRU
{
    private readonly IUnitOfWorkRU _uow;
    public ZamowieniaApplicationServiceRU(IUnitOfWorkRU uow) => _uow = uow;

    public async Task<int> ZlozZamowienieAsync(
        int klientId,
        IEnumerable<(int ProduktId, int Ilosc)> pozycje,
        CancellationToken ct = default)
    {
        // Walidacja PRZED transakcją (bez blokad)
        var klient = await _uow.Klienci.PobierzPoIdAsync(klientId, ct)
            ?? throw new DomainExceptionRU($"Klient #{klientId} nie istnieje");
        if (!klient.Aktywny)
            throw new DomainExceptionRU("Klient jest nieaktywny");

        var pozycjeList = pozycje.ToList();
        var produktyAll = await _uow.Produkty.SzukajAsync(null, null, null, ct);
        var produktyDict = produktyAll.ToDictionary(p => p.Id);

        decimal suma = 0;
        var encjePozycji = new List<PozycjaZamowieniaRU>();

        foreach (var (prodId, ilosc) in pozycjeList)
        {
            if (!produktyDict.TryGetValue(prodId, out var prod))
                throw new DomainExceptionRU($"Produkt #{prodId} nie istnieje");
            if (!prod.Aktywny)
                throw new DomainExceptionRU($"Produkt '{prod.Nazwa}' nieaktywny");
            if (prod.StanMagazynu < ilosc)
                throw new DomainExceptionRU(
                    $"'{prod.Nazwa}': stan={prod.StanMagazynu}, potrzeba={ilosc}");

            encjePozycji.Add(new PozycjaZamowieniaRU
                { ProduktId = prodId, Ilosc = ilosc, CenaWChwili = prod.Cena });
            suma += prod.Cena * ilosc;
        }

        // Transakcja dla właściwych operacji
        await _uow.RozpocznijTransakcjeAsync(ct);
        try
        {
            var zamowienie = new ZamowienieRU
            {
                KlientId = klientId, Status = "Nowe",
                SumaCalkowita = suma, DataZlozenia = DateTime.UtcNow,
                Pozycje = encjePozycji
            };
            await _uow.Zamowienia.DodajAsync(zamowienie, ct);

            // Zmniejsz stany magazynowe przez ExecuteUpdateAsync
            foreach (var (prodId, ilosc) in pozycjeList)
            {
                bool ok = await _uow.Produkty.AktualizujStanAsync(prodId, -ilosc, ct);
                if (!ok) throw new DomainExceptionRU($"Nie udało się zarezerwować produktu #{prodId}");
            }

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
        int zamowienieId, CancellationToken ct = default)
    {
        var zam = await _uow.Zamowienia.PobierzZPozycjamiAsync(zamowienieId, ct)
            ?? throw new DomainExceptionRU($"Zamówienie #{zamowienieId} nie istnieje");

        if (zam.Status == "Anulowane")  throw new DomainExceptionRU("Już anulowane");
        if (zam.Status == "Dostarczone") throw new DomainExceptionRU("Nie można anulować dostarczonego");

        await _uow.RozpocznijTransakcjeAsync(ct);
        try
        {
            // Przywróć stany magazynowe
            foreach (var poz in zam.Pozycje)
                await _uow.Produkty.AktualizujStanAsync(poz.ProduktId, poz.Ilosc, ct);

            zam.Status = "Anulowane";
            _uow.Zamowienia.Aktualizuj(zam);

            await _uow.ZatwierdzAsync(ct);
            return true;
        }
        catch { await _uow.CofnijAsync(ct); throw; }
    }
}

public class ProduktApplicationServiceRU
{
    private readonly IUnitOfWorkRU _uow;
    public ProduktApplicationServiceRU(IUnitOfWorkRU uow) => _uow = uow;

    public async Task<IReadOnlyList<ProduktRU>> PobierzKatalogAsync(
        string? fraza = null, decimal? minCena = null, decimal? maxCena = null,
        CancellationToken ct = default)
        => await _uow.Produkty.SzukajAsync(fraza, minCena, maxCena, ct);

    public async Task<int> DodajProduktAsync(
        string nazwa, decimal cena, int kategoriaId, int stan = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nazwa)) throw new DomainExceptionRU("Nazwa wymagana");
        if (cena <= 0)                        throw new DomainExceptionRU("Cena musi być > 0");

        var produkt = new ProduktRU { Nazwa = nazwa, Cena = cena,
            KategoriaId = kategoriaId, StanMagazynu = stan };
        await _uow.Produkty.DodajAsync(produkt, ct);
        await _uow.ZapiszAsync(ct);
        return produkt.Id;
    }

    public async Task<IReadOnlyList<ProduktRU>> PobierzAlertyStanuAsync(
        int prog = 5, CancellationToken ct = default)
        => await _uow.Produkty.PobierzNiskiStanAsync(prog, ct);
}

// ─── Specification Pattern ────────────────────────────────────────────────────

// EF-kompatybilna implementacja — bez Expression.Invoke (które EF nie obsługuje)
// Używa ZamienParametr zamiast Expression.Invoke dla poprawnego tłumaczenia na SQL

public abstract class SpecyfikacjaRU<T>
{
    public abstract Expression<Func<T, bool>> Wyrazenie { get; }

    public bool Spelnia(T encja) => Wyrazenie.Compile()(encja);

    public SpecyfikacjaRU<T> And(SpecyfikacjaRU<T> inna) =>
        new ZlozonaSp<T>(this, inna, Expression.AndAlso);

    public SpecyfikacjaRU<T> Or(SpecyfikacjaRU<T> inna) =>
        new ZlozonaSp<T>(this, inna, Expression.OrElse);

    public SpecyfikacjaRU<T> Not() => new NegacjaSp<T>(this);
}

// Łączy dwie specyfikacje przez And/Or z prawidłową podmianą parametru (EF-safe)
internal sealed class ZlozonaSp<T> : SpecyfikacjaRU<T>
{
    private readonly SpecyfikacjaRU<T> _a, _b;
    private readonly Func<Expression, Expression, BinaryExpression> _lacz;

    public ZlozonaSp(SpecyfikacjaRU<T> a, SpecyfikacjaRU<T> b,
        Func<Expression, Expression, BinaryExpression> lacz) => (_a, _b, _lacz) = (a, b, lacz);

    public override Expression<Func<T, bool>> Wyrazenie
    {
        get
        {
            var lewExpr  = _a.Wyrazenie;
            var prawExpr = _b.Wyrazenie;
            var param    = lewExpr.Parameters[0];
            // Podmień parametr prawego wyrażenia na ten sam co lewy — EF SQL-safe
            var prawCialo = new ZamienParametrSp(param).Visit(prawExpr.Body);
            return Expression.Lambda<Func<T, bool>>(_lacz(lewExpr.Body, prawCialo), param);
        }
    }
}

internal sealed class NegacjaSp<T> : SpecyfikacjaRU<T>
{
    private readonly SpecyfikacjaRU<T> _wew;
    public NegacjaSp(SpecyfikacjaRU<T> wew) => _wew = wew;

    public override Expression<Func<T, bool>> Wyrazenie
    {
        get
        {
            var wewExpr = _wew.Wyrazenie;
            return Expression.Lambda<Func<T, bool>>(
                Expression.Not(wewExpr.Body), wewExpr.Parameters[0]);
        }
    }
}

// Podmienia jeden parametr wyrażenia na inny (by połączyć dwa Lambda.Body w jeden)
internal sealed class ZamienParametrSp : ExpressionVisitor
{
    private readonly ParameterExpression _nowy;
    public ZamienParametrSp(ParameterExpression nowy) => _nowy = nowy;
    protected override Expression VisitParameter(ParameterExpression node) => _nowy;
}

// Konkretne specyfikacje
public class ProduktAktywnySpec : SpecyfikacjaRU<ProduktRU>
{
    public override Expression<Func<ProduktRU, bool>> Wyrazenie => p => p.Aktywny;
}

public class ProduktWKategoriiSpec : SpecyfikacjaRU<ProduktRU>
{
    private readonly int _katId;
    public ProduktWKategoriiSpec(int katId) => _katId = katId;
    public override Expression<Func<ProduktRU, bool>> Wyrazenie => p => p.KategoriaId == _katId;
}

public class ProduktWCenieSpec : SpecyfikacjaRU<ProduktRU>
{
    private readonly decimal _min, _max;
    public ProduktWCenieSpec(decimal min, decimal max) { _min = min; _max = max; }
    public override Expression<Func<ProduktRU, bool>> Wyrazenie =>
        p => p.Cena >= _min && p.Cena <= _max;
}

public class NiskiStanSpec : SpecyfikacjaRU<ProduktRU>
{
    private readonly int _prog;
    public NiskiStanSpec(int prog) => _prog = prog;
    public override Expression<Func<ProduktRU, bool>> Wyrazenie => p => p.StanMagazynu <= _prog;
}

// Repository V2 z obsługą specyfikacji
public interface IProduktRepositoryV2RU
{
    Task<IReadOnlyList<ProduktRU>> ZnajdzAsync(SpecyfikacjaRU<ProduktRU> spec, CancellationToken ct = default);
    Task<int>                      LiczAsync(SpecyfikacjaRU<ProduktRU> spec, CancellationToken ct = default);
}

public class ProduktRepositoryV2RU : RepositoryRU<ProduktRU>, IProduktRepositoryV2RU
{
    public ProduktRepositoryV2RU(SklepRUContext ctx) : base(ctx) { }

    public async Task<IReadOnlyList<ProduktRU>> ZnajdzAsync(
        SpecyfikacjaRU<ProduktRU> spec, CancellationToken ct = default)
        => await _dbSet.AsNoTracking()
                       .Where(spec.Wyrazenie) // EF tłumaczy wyrażenie na SQL WHERE
                       .OrderBy(p => p.Nazwa)
                       .ToListAsync(ct);

    public async Task<int> LiczAsync(
        SpecyfikacjaRU<ProduktRU> spec, CancellationToken ct = default)
        => await _dbSet.CountAsync(spec.Wyrazenie, ct);
}

// ─── Klasa Demo ───────────────────────────────────────────────────────────────

public static class RepositoryUoW
{
    private static readonly SqliteConnection _conn =
        new SqliteConnection("Data Source=:memory:");
    private static bool _init;

    private static SklepRUContext NowCtx()
    {
        if (!_init)
        {
            _conn.Open();
            using var seed = new SklepRUContext(
                new DbContextOptionsBuilder<SklepRUContext>().UseSqlite(_conn).Options);
            seed.Database.EnsureCreated();
            seed.Kategorie.AddRange(
                new KategoriaRU { Id = 1, Nazwa = "Elektronika" },
                new KategoriaRU { Id = 2, Nazwa = "Odzież" });
            seed.Produkty.AddRange(
                new ProduktRU { Id = 1, Nazwa = "Laptop",    Cena = 3499m, StanMagazynu = 10, KategoriaId = 1 },
                new ProduktRU { Id = 2, Nazwa = "Słuchawki", Cena =  299m, StanMagazynu = 25, KategoriaId = 1 },
                new ProduktRU { Id = 3, Nazwa = "Kurtka",    Cena =  199m, StanMagazynu = 15, KategoriaId = 2 },
                new ProduktRU { Id = 4, Nazwa = "Monitor",   Cena = 1299m, StanMagazynu =  3, KategoriaId = 1 });
            seed.Klienci.AddRange(
                new KlientRU { Id = 1, Imie = "Anna", Nazwisko = "Kowalska", Email = "anna@ru.pl" },
                new KlientRU { Id = 2, Imie = "Jan",  Nazwisko = "Nowak",    Email = "jan@ru.pl" });
            seed.SaveChanges();
            _init = true;
        }
        return new SklepRUContext(
            new DbContextOptionsBuilder<SklepRUContext>().UseSqlite(_conn).Options);
    }

    // 1. Repository Pattern — pełna implementacja CRUD + metody specjalizowane
    public static async Task DemoRepositoryAsync()
    {
        Console.WriteLine("\n--- Repository/UoW: Repository Pattern ---");
        // Zalety Repository: testowalność (mock), separacja, wymienność ORM
        // BEZ Repository: logika biznesowa zna DbContext → trudne do testowania
        // Z Repository:   serwis zna interfejs IRepository → łatwy Mock.Setup(...)

        using var ctx = NowCtx();
        IProduktRepositoryRU repo = new ProduktRepositoryRU(ctx);

        // PobierzWszystkieAsync — AsNoTracking, wszystkie
        var wszystkie = await repo.PobierzWszystkieAsync();
        Console.WriteLine($"PobierzWszystkie: {wszystkie.Count} produktów");

        // PobierzPoIdAsync — FindAsync (sprawdza tracker, potem DB)
        var laptop = await repo.PobierzPoIdAsync(1);
        Console.WriteLine($"PobierzPoId(1): {laptop?.Nazwa} {laptop?.Cena:C}");

        // PobierzZKategoriaAsync — Include(Kategoria)
        var zKat = await repo.PobierzZKategoriaAsync(2);
        Console.WriteLine($"PobierzZKategoria(2): {zKat?.Nazwa} → {zKat?.Kategoria.Nazwa}");

        // SzukajAsync — dynamiczne WHERE
        var tanie = await repo.SzukajAsync(null, null, 400m);
        Console.WriteLine($"SzukajAsync (max 400 PLN): {tanie.Count} produktów");

        var elek = await repo.SzukajAsync("a", null, null);
        Console.WriteLine($"SzukajAsync (fraza='a'): {string.Join(", ", elek.Select(p => p.Nazwa))}");

        // DodajAsync + ZapiszAsync (przez ctx bezpośrednio w tym demo)
        var nowy = new ProduktRU { Nazwa = "Klawiatura", Cena = 149m, StanMagazynu = 20, KategoriaId = 1 };
        await repo.DodajAsync(nowy);
        await ctx.SaveChangesAsync();
        Console.WriteLine($"DodajAsync: Klawiatura ID={nowy.Id}");

        // IstniejeAsync
        bool jest = await repo.IstniejeAsync(nowy.Id);
        Console.WriteLine($"IstniejeAsync({nowy.Id}): {jest}");

        // AktualizujStanAsync — ExecuteUpdateAsync (EF7+, bez ładowania encji)
        bool stanOK = await repo.AktualizujStanAsync(nowy.Id, -5);
        Console.WriteLine($"AktualizujStan(-5): {stanOK}");

        // PobierzNiskiStanAsync — stan <= prog
        var niskiStan = await repo.PobierzNiskiStanAsync(5);
        Console.WriteLine($"PobierzNiskiStan(prog=5): {string.Join(", ", niskiStan.Select(p => $"{p.Nazwa}(={p.StanMagazynu})"))}");

        // UsunAsync
        var doUsuniecia = await repo.PobierzPoIdAsync(nowy.Id);
        repo.Usun(doUsuniecia!);
        await ctx.SaveChangesAsync();
        Console.WriteLine($"Usun: Klawiatura usunięta");

        // LiczAsync
        int total = await repo.LiczAsync();
        Console.WriteLine($"LiczAsync: {total} produktów");
    }

    // 2. Unit of Work — koordynacja wielu repozytoriów, jedna transakcja
    public static async Task DemoUnitOfWorkAsync()
    {
        Console.WriteLine("\n--- Repository/UoW: Unit of Work ---");
        // UoW = jeden SaveChanges/Commit dla WSZYSTKICH repozytoriów naraz
        // Bez UoW: każde repo ma własny kontekst → osobne transakcje → brak atomowości

        // ZamowieniaApplicationServiceRU — orkiestrator używający UoW
        await using var ctx = NowCtx();
        await using var uow = new UnitOfWorkRU(ctx);
        var serwis = new ZamowieniaApplicationServiceRU(uow);

        // Złożenie zamówienia — wiele repozytoriów, jedna transakcja
        int zamId = await serwis.ZlozZamowienieAsync(1, new[]
        {
            (ProduktId: 2, Ilosc: 2), // 2× Słuchawki
            (ProduktId: 3, Ilosc: 1)  // 1× Kurtka
        });
        Console.WriteLine($"ZlozZamowienie: ID={zamId}");

        // Weryfikacja przez repozytoria
        var zamowienie = await uow.Zamowienia.PobierzZPozycjamiAsync(zamId);
        Console.WriteLine($"Zamówienie #{zamId}: {zamowienie?.SumaCalkowita:C} | {zamowienie?.Pozycje.Count} poz.");
        foreach (var p in zamowienie?.Pozycje ?? [])
            Console.WriteLine($"  {p.Produkt.Nazwa}: {p.Ilosc}× {p.CenaWChwili:C}");

        var stat = await uow.Klienci.PobierzStatystykiAsync(1);
        Console.WriteLine($"Statystyki klienta 1: {stat?.LiczbaZamowien} zam., {stat?.SumaZakupow:C}");

        // Stronicowanie
        var strona1 = await uow.Zamowienia.PobierzPodsumowaniaAsync(1, 10);
        Console.WriteLine($"Podsumowania (str.1): {strona1.Count}");

        // ProduktApplicationServiceRU
        await using var ctx2 = NowCtx();
        await using var uow2 = new UnitOfWorkRU(ctx2);
        var prodSerwis = new ProduktApplicationServiceRU(uow2);

        int nowyProdId = await prodSerwis.DodajProduktAsync("Webcam", 249m, 1, 8);
        Console.WriteLine($"DodajProdukt: Webcam ID={nowyProdId}");

        var alerty = await prodSerwis.PobierzAlertyStanuAsync(5);
        Console.WriteLine($"Alerty niski stan: {string.Join(", ", alerty.Select(p => $"{p.Nazwa}({p.StanMagazynu})"))}");

        var katalog = await prodSerwis.PobierzKatalogAsync(maxCena: 500m);
        Console.WriteLine($"Katalog (<500 PLN): {katalog.Count} produktów");

        // DI rejestracja (Web API):
        // builder.Services.AddDbContext<SklepRUContext>(opt => opt.UseSqlite(connStr));
        // builder.Services.AddScoped<IUnitOfWorkRU, UnitOfWorkRU>();
        // builder.Services.AddScoped<ZamowieniaApplicationServiceRU>();
        Console.WriteLine("DI: AddScoped<IUnitOfWork> + AddScoped<ApplicationService> = jeden kontekst na request");
    }

    // 3. Specification Pattern — kompozycja warunków, wielokrotny użytek
    public static async Task DemoSpecyfikacjeAsync()
    {
        Console.WriteLine("\n--- Repository/UoW: Specification Pattern ---");
        // Specification = enkapsulacja logiki filtrowania jako obiekt wielokrotnego użytku
        // Zalety: SOLID (Open/Closed), testowalność, czytelność złożonych warunków

        using var ctx = NowCtx();
        var repo = new ProduktRepositoryV2RU(ctx);

        // Konkretne specyfikacje
        var aktywny      = new ProduktAktywnySpec();
        var elektronika  = new ProduktWKategoriiSpec(1);
        var taniaPolka   = new ProduktWCenieSpec(100m, 500m);
        var niskiStan    = new NiskiStanSpec(5);

        // Sprawdzenie in-memory (Spelnia = Compile + Invoke)
        var laptopRU = new ProduktRU { Aktywny = true, KategoriaId = 1, Cena = 3499m, StanMagazynu = 10 };
        Console.WriteLine($"aktywny.Spelnia(laptop): {aktywny.Spelnia(laptopRU)}");
        Console.WriteLine($"taniaPolka.Spelnia(laptop): {taniaPolka.Spelnia(laptopRU)}");

        // Kompozycja specyfikacji — And / Or / Not
        var elektroTania  = aktywny.And(elektronika).And(taniaPolka);
        var alarm         = aktywny.And(niskiStan);
        var nieElektronika = aktywny.And(elektronika.Not());

        // ZnajdzAsync — EF Core tłumaczy spec.Wyrazenie na SQL WHERE
        var elektroTanieList = await repo.ZnajdzAsync(elektroTania);
        Console.WriteLine($"And(aktywny, elektronika, taniaPolka): {elektroTanieList.Count} produktów");
        foreach (var p in elektroTanieList)
            Console.WriteLine($"  {p.Nazwa} {p.Cena:C} stan={p.StanMagazynu}");

        var alarmList = await repo.ZnajdzAsync(alarm);
        Console.WriteLine($"Alarm niski stan (≤5): {string.Join(", ", alarmList.Select(p => $"{p.Nazwa}({p.StanMagazynu})"))}");

        var nieElektroList = await repo.ZnajdzAsync(nieElektronika);
        Console.WriteLine($"Not(elektronika): {string.Join(", ", nieElektroList.Select(p => p.Nazwa))}");

        // LiczAsync — SELECT COUNT(*) WHERE spec.Wyrazenie
        int ile = await repo.LiczAsync(alarm);
        Console.WriteLine($"LiczAsync(alarm): {ile} produktów z niskim stanem");

        // Or — alternatywa warunków
        var tanieAlboNiskiStan = aktywny.And(taniaPolka.Or(niskiStan));
        var orList = await repo.ZnajdzAsync(tanieAlboNiskiStan);
        Console.WriteLine($"Or(tania ALBO niskiStan): {orList.Count} produktów");

        // Wielokrotny użytek: te same specyfikacje w różnych serwisach/kontrolerach
        // ProduktController.Index → elektroTania
        // StockAlert.Check     → alarm
        // Report.NonElectro    → nieElektronika
        Console.WriteLine("Specification: enkapsulacja logiki filtrowania — reusable, testable, composable");

        // Kontrowersja Repository nad EF Core:
        // DbSet = Repository, DbContext = UoW — EF już implementuje oba wzorce
        // BEZ Repository: DbContext bezpośrednio w serwisie, trudniejszy mock
        // Z REPOSITORY:   interfejs + mock → czyste testy jednostkowe (ms zamiast sekund)
        // KOMPROMIS: użyj Repository gdy testowalność priorytetem; dla CRUD może być zbędne
        Console.WriteLine("Czy Repository nad EF ma sens? Tak gdy: testy jednostkowe > integracyjne");
    }
}
