using System.Linq.Expressions;

namespace _08_DesignPatterns_Architecture;

// ============================================================
// EF Core stubs — symuluje Microsoft.EntityFrameworkCore
// W produkcji: pakiet Microsoft.EntityFrameworkCore
// ============================================================

public abstract class EfSaveChangesInterceptor
{
    public virtual ValueTask<int> SavedChangesAsync(
        EfSaveChangesEventData data, int result, CancellationToken ct = default)
        => ValueTask.FromResult(result);
}

public class EfSaveChangesEventData
{
    public EfDbContext? Context { get; init; }
}

public abstract class EfDbContext
{
    public EfChangeTracker ChangeTracker { get; } = new();
}

public class EfChangeTracker
{
    private readonly List<object> _tracked = new();
    public void Track<T>(T entity) where T : class => _tracked.Add(entity!);
    public IEnumerable<EfEntityEntry<T>> Entries<T>() where T : class
        => _tracked.OfType<T>().Select(e => new EfEntityEntry<T> { Entity = e });
}

public class EfEntityEntry<T> where T : class
{
    public T Entity { get; init; } = default!;
}

public class EfEntityTypeBuilder<T>
{
    public EfEntityTypeBuilder<T> HasKey(Expression<Func<T, object?>> key) => this;
    public EfPropertyBuilder EfProperty<TProp>(Expression<Func<T, TProp>> prop) => new();
    public EfEntityTypeBuilder<T> OwnsOne<TOwned>(
        Expression<Func<T, TOwned?>> nav,
        Action<EfOwnedBuilder<TOwned>> cfg) where TOwned : class
    { cfg(new()); return this; }
    public EfRelationshipBuilder HasMany<TRelated>(string navigationName) where TRelated : class => new();
    public EfNavigationBuilder Navigation(string navigationName) => new();
    public EfEntityTypeBuilder<T> HasConversion<TConvert>() => this;
}

public class EfOwnedBuilder<TOwned>
{
    public EfPropertyBuilder EfProperty<TProp>(Expression<Func<TOwned, TProp>> prop) => new();
}

public class EfPropertyBuilder
{
    public EfPropertyBuilder HasColumnName(string name) => this;
    public EfPropertyBuilder HasMaxLength(int len) => this;
    public EfPropertyBuilder HasConversion<T>() => this;
}

public class EfRelationshipBuilder
{
    public EfRelationshipBuilder WithOne() => this;
    public EfRelationshipBuilder HasForeignKey(string fk) => this;
}

public class EfNavigationBuilder
{
    public EfNavigationBuilder UsePropertyAccessMode(int mode) => this;
}

// ============================================================
// 1. VALUE OBJECTS — identyfikowane przez wartość, nie ID
// ============================================================

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        return GetEqualityComponents()
            .SequenceEqual(((ValueObject)obj).GetEqualityComponents());
    }

    public override int GetHashCode()
        => GetEqualityComponents()
            .Aggregate(1, (hash, obj) => HashCode.Combine(hash, obj?.GetHashCode() ?? 0));

    public static bool operator ==(ValueObject? a, ValueObject? b)
        => a?.Equals(b) ?? b is null;

    public static bool operator !=(ValueObject? a, ValueObject? b)
        => !(a == b);
}

public class Pieniadze : ValueObject
{
    public decimal Kwota  { get; }
    public string  Waluta { get; }

    private Pieniadze(decimal kwota, string waluta) { Kwota = kwota; Waluta = waluta; }

    public static Pieniadze Utworz(decimal kwota, string waluta)
    {
        if (kwota < 0)
            throw new ArgumentException("Kwota nie może być ujemna");
        if (string.IsNullOrWhiteSpace(waluta) || waluta.Length != 3)
            throw new ArgumentException("Waluta musi być 3-literowym kodem ISO");
        return new Pieniadze(kwota, waluta.ToUpper());
    }

    public static Pieniadze PLN(decimal kwota) => Utworz(kwota, "PLN");
    public static Pieniadze USD(decimal kwota) => Utworz(kwota, "USD");
    public static Pieniadze Zero(string waluta) => Utworz(0, waluta);

    public Pieniadze Dodaj(Pieniadze inne)
    {
        if (Waluta != inne.Waluta)
            throw new InvalidOperationException($"Nie można dodać {Waluta} i {inne.Waluta}");
        return new Pieniadze(Kwota + inne.Kwota, Waluta);
    }

    public Pieniadze Odejmij(Pieniadze inne)
    {
        if (Waluta != inne.Waluta)
            throw new InvalidOperationException($"Nie można odjąć {Waluta} od {inne.Waluta}");
        if (Kwota < inne.Kwota)
            throw new InvalidOperationException("Niewystarczające środki");
        return new Pieniadze(Kwota - inne.Kwota, Waluta);
    }

    public Pieniadze Pomnoz(decimal wspolczynnik)
        => new(Math.Round(Kwota * wspolczynnik, 2), Waluta);

    public bool WiekszyNiz(Pieniadze inne) => Waluta == inne.Waluta && Kwota > inne.Kwota;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kwota;
        yield return Waluta;
    }

    public override string ToString() => $"{Kwota:F2} {Waluta}";
}

public class Adres : ValueObject
{
    public string Ulica   { get; }
    public string Miasto  { get; }
    public string KodPocz { get; }
    public string Kraj    { get; }

    private Adres(string ulica, string miasto, string kod, string kraj)
    {
        Ulica = ulica; Miasto = miasto; KodPocz = kod; Kraj = kraj;
    }

    public static Adres Utworz(string ulica, string miasto, string kod, string kraj = "PL")
    {
        if (string.IsNullOrWhiteSpace(ulica))
            throw new ArgumentException("Ulica jest wymagana");
        if (string.IsNullOrWhiteSpace(miasto))
            throw new ArgumentException("Miasto jest wymagane");
        if (!System.Text.RegularExpressions.Regex.IsMatch(kod, @"^\d{2}-\d{3}$"))
            throw new ArgumentException("Kod pocztowy format: XX-XXX");
        return new Adres(ulica.Trim(), miasto.Trim(), kod.Trim(), kraj.Trim().ToUpper());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Ulica.ToLower();
        yield return Miasto.ToLower();
        yield return KodPocz;
        yield return Kraj;
    }

    public override string ToString() => $"{Ulica}, {KodPocz} {Miasto}, {Kraj}";
}

public class Email : ValueObject
{
    public string Wartosc { get; }

    private Email(string wartosc) => Wartosc = wartosc;

    public static Email Utworz(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email jest wymagany");
        var znorm = email.Trim().ToLower();
        if (!znorm.Contains('@') || !znorm.Contains('.'))
            throw new ArgumentException($"Nieprawidłowy email: {email}");
        return new Email(znorm);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Wartosc;
    }

    public override string ToString() => Wartosc;
    public static implicit operator string(Email e) => e.Wartosc;
}

// ============================================================
// 2. DOMAIN: ENTITY + AGGREGATE ROOT
// ============================================================

public abstract class Encja<TId>
{
    public TId Id { get; protected set; } = default!;

    protected Encja() { }
    protected Encja(TId id) => Id = id;

    public override bool Equals(object? obj)
    {
        if (obj is not Encja<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        if (Id is null || other.Id is null) return false;
        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Encja<TId>? a, Encja<TId>? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(Encja<TId>? a, Encja<TId>? b) => !(a == b);
}

public abstract class AggregateRoot<TId> : Encja<TId>
{
    private readonly List<ZdarzenieDomenowe> _zdarzenia = new();

    public IReadOnlyList<ZdarzenieDomenowe> ZdarzeniaDomenowe => _zdarzenia.AsReadOnly();

    protected void DodajZdarzenie(ZdarzenieDomenowe zdarzenie) => _zdarzenia.Add(zdarzenie);

    public void CzyscZdarzenia() => _zdarzenia.Clear();

    protected AggregateRoot() { }
    protected AggregateRoot(TId id) : base(id) { }
}

public abstract record ZdarzenieDomenowe
{
    public Guid     Id              { get; init; } = Guid.NewGuid();
    public DateTime CzasWystapienia { get; init; } = DateTime.UtcNow;
}

// ============================================================
// 3. AGGREGATE — Zamowienie (domain events + state machine)
// ============================================================

public record ZamowienieUtworzono(
    Guid   ZamowienieId,
    Guid   KlientId,
    string KlientEmail) : ZdarzenieDomenowe;

public record PozycjaDodana(
    Guid      ZamowienieId,
    Guid      ProduktId,
    string    NazwaProduktu,
    Pieniadze Cena,
    int       Ilosc) : ZdarzenieDomenowe;

public record ZamowienieOplacono(
    Guid      ZamowienieId,
    Pieniadze Kwota,
    string    TransakcjaId) : ZdarzenieDomenowe;

public record ZamowienieAnulowano(
    Guid   ZamowienieId,
    string Powod) : ZdarzenieDomenowe;

public enum StatusZamowienia
{
    Szkic, Zlozone, Oplacone, WRealizacji, Wyslane, Dostarczone, Anulowane
}

public class PozycjaZamowienia : Encja<Guid>
{
    public Guid      ProduktId     { get; private set; }
    public string    NazwaProduktu { get; private set; } = default!;
    public Pieniadze CenaJednostk  { get; private set; } = default!;
    public int       Ilosc         { get; private set; }

    public Pieniadze Wartosc => CenaJednostk.Pomnoz(Ilosc);

    private PozycjaZamowienia() { }

    internal PozycjaZamowienia(Guid produktId, string nazwa, Pieniadze cena, int ilosc)
        : base(Guid.NewGuid())
    {
        if (ilosc <= 0) throw new ArgumentException("Ilość musi być > 0");
        ProduktId = produktId; NazwaProduktu = nazwa; CenaJednostk = cena; Ilosc = ilosc;
    }

    internal void ZmienIlosc(int nowaIlosc)
    {
        if (nowaIlosc <= 0) throw new ArgumentException("Ilość musi być > 0");
        Ilosc = nowaIlosc;
    }
}

public class Zamowienie : AggregateRoot<Guid>
{
    private readonly List<PozycjaZamowienia> _pozycje = new();
    private StatusZamowienia _status = StatusZamowienia.Szkic;

    public Guid                          KlientId       { get; private set; }
    public Email                         KlientEmail    { get; private set; } = null!;
    public Adres                         Adres          { get; private set; } = null!;
    public StatusZamowienia              Status         => _status;
    public DateTime                      DataZlozenia   { get; private set; }
    public string?                       NumerSledzenia { get; private set; }
    public IReadOnlyList<PozycjaZamowienia> Pozycje    => _pozycje.AsReadOnly();

    public Pieniadze SumaCalkowita =>
        _pozycje.Aggregate(Pieniadze.PLN(0), (sum, p) => sum.Dodaj(p.Wartosc));

    private Zamowienie() { }

    public static Zamowienie Utworz(Guid klientId, string email, Adres adres)
    {
        var z = new Zamowienie
        {
            Id          = Guid.NewGuid(),
            KlientId    = klientId,
            KlientEmail = Email.Utworz(email),
            Adres       = adres,
            DataZlozenia = DateTime.UtcNow
        };
        z.DodajZdarzenie(new ZamowienieUtworzono(z.Id, klientId, email));
        return z;
    }

    public void DodajPozycje(Guid produktId, string nazwa, Pieniadze cena, int ilosc)
    {
        SprawdzStatus(StatusZamowienia.Szkic, StatusZamowienia.Zlozone);
        var istniejaca = _pozycje.FirstOrDefault(p => p.ProduktId == produktId);
        if (istniejaca is not null)
            istniejaca.ZmienIlosc(istniejaca.Ilosc + ilosc);
        else
            _pozycje.Add(new PozycjaZamowienia(produktId, nazwa, cena, ilosc));
        DodajZdarzenie(new PozycjaDodana(Id, produktId, nazwa, cena, ilosc));
    }

    public void UsunPozycje(Guid produktId)
    {
        SprawdzStatus(StatusZamowienia.Szkic);
        var pozycja = _pozycje.FirstOrDefault(p => p.ProduktId == produktId)
            ?? throw new InvalidOperationException($"Produkt {produktId} nie jest w zamówieniu");
        _pozycje.Remove(pozycja);
    }

    public void Zloz()
    {
        SprawdzStatus(StatusZamowienia.Szkic);
        if (!_pozycje.Any())
            throw new InvalidOperationException("Nie można złożyć pustego zamówienia");
        _status = StatusZamowienia.Zlozone;
    }

    public void Oplac(string transakcjaId)
    {
        SprawdzStatus(StatusZamowienia.Zlozone);
        _status = StatusZamowienia.Oplacone;
        DodajZdarzenie(new ZamowienieOplacono(Id, SumaCalkowita, transakcjaId));
    }

    public void RozpoczniRealizacje()
    {
        SprawdzStatus(StatusZamowienia.Oplacone);
        _status = StatusZamowienia.WRealizacji;
    }

    public void WyslijZNumeremSledzenia(string numer)
    {
        SprawdzStatus(StatusZamowienia.WRealizacji);
        NumerSledzenia = numer;
        _status = StatusZamowienia.Wyslane;
    }

    public void Anuluj(string powod)
    {
        if (_status is StatusZamowienia.Wyslane
                    or StatusZamowienia.Dostarczone
                    or StatusZamowienia.Anulowane)
            throw new InvalidOperationException(
                $"Nie można anulować zamówienia w statusie {_status}");
        _status = StatusZamowienia.Anulowane;
        DodajZdarzenie(new ZamowienieAnulowano(Id, powod));
    }

    private void SprawdzStatus(params StatusZamowienia[] dozwolone)
    {
        if (!dozwolone.Contains(_status))
            throw new InvalidOperationException(
                $"Operacja niedozwolona w statusie {_status}. " +
                $"Wymagany: {string.Join(" lub ", dozwolone)}");
    }
}

// ============================================================
// 4. SUPPORTING DOMAIN TYPES
// ============================================================

public class Klient : AggregateRoot<Guid>
{
    public Email Email           { get; private set; } = null!;
    public int   LiczbaZamowien  { get; private set; } = 0;

    public static Klient Utworz(string email)
        => new() { Id = Guid.NewGuid(), Email = Email.Utworz(email) };
}

public class KodPromocyjny : ValueObject
{
    public string    Wartosc       { get; }
    public decimal   ProcentRabatu { get; }
    public Pieniadze MinimalnaSuma { get; }
    public bool      CzyAktywny   { get; }

    public KodPromocyjny(string wartosc, decimal procent, Pieniadze min, bool aktywny)
    {
        Wartosc = wartosc; ProcentRabatu = procent; MinimalnaSuma = min; CzyAktywny = aktywny;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Wartosc.ToUpper();
    }
}

// ============================================================
// 5. DOMAIN SERVICE — logika niepasująca do jednej encji
// ============================================================

public class SerwisWycenyZamowienia
{
    public Pieniadze ObliczRabat(
        Zamowienie zamowienie,
        Klient klient,
        IReadOnlyList<KodPromocyjny> aktywneKody)
    {
        var suma = zamowienie.SumaCalkowita;

        if (klient.LiczbaZamowien > 10)
        {
            var rabat = suma.Pomnoz(0.1m);
            Console.WriteLine($"  Rabat stały klient 10%: {rabat}");
            return rabat;
        }

        foreach (var kod in aktywneKody)
        {
            if (kod.CzyAktywny && zamowienie.SumaCalkowita.WiekszyNiz(kod.MinimalnaSuma))
            {
                var rabat = suma.Pomnoz(kod.ProcentRabatu / 100);
                Console.WriteLine($"  Rabat kod {kod.Wartosc}: {rabat}");
                return rabat;
            }
        }

        return Pieniadze.PLN(0);
    }
}

// ============================================================
// 6. APPLICATION LAYER — interfejsy repozytoriów
// ============================================================

public interface IZamowienieRepository
{
    Task<Zamowienie?>       PobierzPoIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Zamowienie>>  PobierzPoKliencieAsync(Guid klientId, CancellationToken ct = default);
    Task                    DodajAsync(Zamowienie zamowienie, CancellationToken ct = default);
    Task                    AktualizujAsync(Zamowienie zamowienie, CancellationToken ct = default);
    Task<bool>              IstniejeAsync(Guid id, CancellationToken ct = default);
}

public interface IKlientRepository
{
    Task<Klient?> PobierzPoIdAsync(Guid id, CancellationToken ct = default);
    Task<Klient?> PobierzPoEmailuAsync(Email email, CancellationToken ct = default);
    Task          DodajAsync(Klient klient, CancellationToken ct = default);
}

public interface IZdarzenieDomenoweBus
{
    Task PublikujAsync(ZdarzenieDomenowe zdarzenie, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IZamowienieReadRepository
{
    Task<StronicowanaLista<ZamowienieListDto>> PobierzDlaKlientaAsync(
        Guid id, int strona, int rozmiar, CancellationToken ct);
}

public interface IInwentarzAsyncSerwis
{
    Task ZarezerwujTowarAsync(Guid zamId, CancellationToken ct);
}

// ============================================================
// 7. APPLICATION LAYER — CQRS: Commands + Queries
// ============================================================

public record UtworzZamowienieCommand(
    Guid   KlientId,
    string Email,
    string Ulica,
    string Miasto,
    string KodPocztowy,
    IReadOnlyList<PozycjaCommand> Pozycje);

public record PozycjaCommand(
    Guid    ProduktId,
    string  NazwaProduktu,
    decimal Cena,
    int     Ilosc);

public record UtworzZamowienieResult(Guid ZamowienieId, string Numer);

public class UtworzZamowienieHandler
{
    private readonly IZamowienieRepository  _zamRepo;
    private readonly IKlientRepository      _kliRepo;
    private readonly IZdarzenieDomenoweBus  _bus;
    private readonly IUnitOfWork            _uow;

    public UtworzZamowienieHandler(
        IZamowienieRepository zamRepo,
        IKlientRepository kliRepo,
        IZdarzenieDomenoweBus bus,
        IUnitOfWork uow)
    {
        _zamRepo = zamRepo; _kliRepo = kliRepo; _bus = bus; _uow = uow;
    }

    public async Task<UtworzZamowienieResult> HandleAsync(
        UtworzZamowienieCommand cmd, CancellationToken ct = default)
    {
        if (!cmd.Pozycje.Any())
            throw new ArgumentException("Zamówienie musi mieć pozycje");

        var klient = await _kliRepo.PobierzPoIdAsync(cmd.KlientId, ct)
            ?? throw new InvalidOperationException($"Klient {cmd.KlientId} nie istnieje");

        var adres = Adres.Utworz(cmd.Ulica, cmd.Miasto, cmd.KodPocztowy);
        var zamowienie = Zamowienie.Utworz(klient.Id, cmd.Email, adres);

        foreach (var poz in cmd.Pozycje)
            zamowienie.DodajPozycje(poz.ProduktId, poz.NazwaProduktu, Pieniadze.PLN(poz.Cena), poz.Ilosc);

        zamowienie.Zloz();

        await _zamRepo.DodajAsync(zamowienie, ct);
        await _uow.SaveChangesAsync(ct);

        foreach (var zdarzenie in zamowienie.ZdarzeniaDomenowe)
            await _bus.PublikujAsync(zdarzenie, ct);

        zamowienie.CzyscZdarzenia();

        return new UtworzZamowienieResult(
            zamowienie.Id,
            $"ZAM-{DateTime.Now:yyyyMMdd}-{zamowienie.Id.ToString("N")[..6].ToUpper()}");
    }
}

public record PobierzZamowieniaKlientaQuery(Guid KlientId, int Strona = 1, int Rozmiar = 20);

public record ZamowienieListDto(
    Guid     Id,
    string   Status,
    decimal  Suma,
    int      LiczbaPozycji,
    DateTime Data,
    string?  NumerSledzenia);

public record StronicowanaLista<T>(
    IReadOnlyList<T> Dane,
    int LacznaIlosc,
    int Strona,
    int Rozmiar);

public class PobierzZamowieniaKlientaHandler
{
    private readonly IZamowienieReadRepository _repo;

    public PobierzZamowieniaKlientaHandler(IZamowienieReadRepository repo) => _repo = repo;

    public async Task<StronicowanaLista<ZamowienieListDto>> HandleAsync(
        PobierzZamowieniaKlientaQuery query, CancellationToken ct = default)
        => await _repo.PobierzDlaKlientaAsync(query.KlientId, query.Strona, query.Rozmiar, ct);
}

// ============================================================
// 8. INFRASTRUCTURE LAYER — EF Core konfiguracja (z stubami)
// ============================================================

// W produkcji: implements IEntityTypeConfiguration<Zamowienie>
public class ZamowienieConfiguration
{
    public void Configure(EfEntityTypeBuilder<Zamowienie> builder)
    {
        builder.HasKey(z => (object?)z.Id);

        // Value Object Email jako owned entity
        builder.OwnsOne(z => z.KlientEmail, e =>
        {
            e.EfProperty(x => x.Wartosc)
             .HasColumnName("KlientEmail")
             .HasMaxLength(200);
        });

        // Value Object Adres jako owned entity
        builder.OwnsOne(z => z.Adres, a =>
        {
            a.EfProperty(x => x.Ulica).HasMaxLength(200);
            a.EfProperty(x => x.Miasto).HasMaxLength(100);
            a.EfProperty(x => x.KodPocz).HasMaxLength(10);
            a.EfProperty(x => x.Kraj).HasMaxLength(2);
        });

        // Enum jako string
        builder.EfProperty<StatusZamowienia>(z => z.Status)
               .HasConversion<string>()
               .HasMaxLength(50);

        // Kolekcja wewnętrzna — navigation przez backing field
        builder.HasMany<PozycjaZamowienia>("_pozycje")
               .WithOne()
               .HasForeignKey("ZamowienieId");

        // PropertyAccessMode.Field = 4 (EF Core enum)
        builder.Navigation("_pozycje").UsePropertyAccessMode(4);
    }
}

// W produkcji: extends SaveChangesInterceptor (Microsoft.EntityFrameworkCore.Diagnostics)
public class ZdarzeniaDomenoweInterceptor : EfSaveChangesInterceptor
{
    private readonly IZdarzenieDomenoweBus _bus;

    public ZdarzeniaDomenoweInterceptor(IZdarzenieDomenoweBus bus) => _bus = bus;

    public override async ValueTask<int> SavedChangesAsync(
        EfSaveChangesEventData data, int result, CancellationToken ct = default)
    {
        if (data.Context is null) return result;

        var agregaty = data.Context.ChangeTracker
            .Entries<AggregateRoot<Guid>>()
            .Where(e => e.Entity.ZdarzeniaDomenowe.Any())
            .Select(e => e.Entity)
            .ToList();

        foreach (var agregat in agregaty)
        {
            foreach (var zdarzenie in agregat.ZdarzeniaDomenowe)
                await _bus.PublikujAsync(zdarzenie, ct);
            agregat.CzyscZdarzenia();
        }

        return result;
    }
}

public class ZamowienieOplaconoHandler
{
    private readonly IEmailSerwis          _email;
    private readonly IInwentarzAsyncSerwis _inwentarz;

    public ZamowienieOplaconoHandler(IEmailSerwis email, IInwentarzAsyncSerwis inwentarz)
    {
        _email = email; _inwentarz = inwentarz;
    }

    public async Task HandleAsync(ZamowienieOplacono zdarzenie, CancellationToken ct)
    {
        Console.WriteLine($"  [Handler] ZamowienieOplacono: #{zdarzenie.ZamowienieId}");
        await _email.WyslijAsync("klient@test.pl", $"Płatność {zdarzenie.Kwota} potwierdzona");
        await _inwentarz.ZarezerwujTowarAsync(zdarzenie.ZamowienieId, ct);
    }
}

// ============================================================
// 9. DEMO
// ============================================================

public static class CleanArchitectureDemo
{
    public static void Uruchom()
    {
        Console.WriteLine("\n=== Clean Architecture / DDD ===");

        DemoValueObjects();
        DemoAgregat();
        DemoDomainService();
        DemoCqrsStruktury();
        DemoInfrastrukturaStub();
    }

    private static void DemoValueObjects()
    {
        Console.WriteLine("\n-- Value Objects --");

        var cena1 = Pieniadze.PLN(100m);
        var cena2 = Pieniadze.PLN(100m);
        var cena3 = Pieniadze.PLN(200m);

        Console.WriteLine($"cena1 == cena2: {cena1 == cena2}");            // True
        Console.WriteLine($"cena1 == cena3: {cena1 == cena3}");            // False
        Console.WriteLine($"ReferenceEquals: {ReferenceEquals(cena1, cena2)}"); // False

        var suma = cena1.Dodaj(cena2);
        Console.WriteLine($"Suma: {suma}");                                 // 200.00 PLN

        var email1 = Email.Utworz("jan@test.pl");
        var email2 = Email.Utworz("JAN@TEST.PL");
        Console.WriteLine($"Email równość (case-insensitive): {email1 == email2}"); // True

        var adres = Adres.Utworz("ul. Marszałkowska 1", "Warszawa", "00-001");
        Console.WriteLine($"Adres: {adres}");
    }

    private static void DemoAgregat()
    {
        Console.WriteLine("\n-- Aggregate Root — Zamowienie (state machine) --");

        var adres = Adres.Utworz("ul. Marszałkowska 1", "Warszawa", "00-001");
        var klient = Klient.Utworz("jan@test.pl");

        var zamowienie = Zamowienie.Utworz(klient.Id, "jan@test.pl", adres);
        zamowienie.DodajPozycje(Guid.NewGuid(), "Laptop", Pieniadze.PLN(3500m), 1);
        zamowienie.DodajPozycje(Guid.NewGuid(), "Mysz",   Pieniadze.PLN(150m),  2);
        zamowienie.DodajPozycje(Guid.NewGuid(), "Kabel",  Pieniadze.PLN(49m),   3);

        Console.WriteLine($"Pozycji: {zamowienie.Pozycje.Count}");
        Console.WriteLine($"Suma: {zamowienie.SumaCalkowita}");

        zamowienie.Zloz();
        Console.WriteLine($"Po Zloz(): {zamowienie.Status}");

        zamowienie.Oplac("TXN-ABC123");
        Console.WriteLine($"Po Oplac(): {zamowienie.Status}");

        zamowienie.RozpoczniRealizacje();
        Console.WriteLine($"Po RozpoczniRealizacje(): {zamowienie.Status}");

        zamowienie.WyslijZNumeremSledzenia("PL-9999-2024");
        Console.WriteLine($"Po Wysylce: {zamowienie.Status}, numer: {zamowienie.NumerSledzenia}");

        Console.WriteLine($"Zdarzeń domenowych: {zamowienie.ZdarzeniaDomenowe.Count}");
        foreach (var z in zamowienie.ZdarzeniaDomenowe)
            Console.WriteLine($"  - {z.GetType().Name}");

        // Guard — operacja niedozwolona w bieżącym statusie
        try
        {
            zamowienie.DodajPozycje(Guid.NewGuid(), "Klawiatura", Pieniadze.PLN(250m), 1);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Guard (oczekiwany błąd): {ex.Message}");
        }

        // Anulowanie z dopuszczalnego statusu
        var inne = Zamowienie.Utworz(klient.Id, "jan@test.pl", adres);
        inne.DodajPozycje(Guid.NewGuid(), "Test", Pieniadze.PLN(100m), 1);
        inne.Zloz();
        inne.Anuluj("Zmiana decyzji");
        Console.WriteLine($"Anulowane: {inne.Status}");
    }

    private static void DemoDomainService()
    {
        Console.WriteLine("\n-- Domain Service — SerwisWycenyZamowienia --");

        var adres   = Adres.Utworz("ul. Testowa 1", "Kraków", "30-001");
        var klient  = Klient.Utworz("vip@test.pl");
        var zam     = Zamowienie.Utworz(klient.Id, "vip@test.pl", adres);
        zam.DodajPozycje(Guid.NewGuid(), "Monitor", Pieniadze.PLN(1200m), 1);
        zam.Zloz();

        var kody = new List<KodPromocyjny>
        {
            new("LATO24", 15m, Pieniadze.PLN(500m), true)
        };

        var serwis = new SerwisWycenyZamowienia();
        var rabat  = serwis.ObliczRabat(zam, klient, kody);
        Console.WriteLine($"Rabat: {rabat}");
    }

    private static void DemoCqrsStruktury()
    {
        Console.WriteLine("\n-- CQRS — struktury Command/Query/Handler --");

        // Pokaż strukturę Command
        var cmd = new UtworzZamowienieCommand(
            Guid.NewGuid(), "jan@test.pl",
            "ul. Testowa 5", "Gdańsk", "80-001",
            new List<PozycjaCommand>
            {
                new(Guid.NewGuid(), "Laptop", 3500m, 1),
                new(Guid.NewGuid(), "Mysz",   150m,  2)
            });

        Console.WriteLine($"Command: KlientId={cmd.KlientId}, Pozycji={cmd.Pozycje.Count}");
        Console.WriteLine($"Query:   PobierzZamowieniaKlientaQuery(strona=1, rozmiar=20)");
        Console.WriteLine("Handler: UtworzZamowienieHandler.HandleAsync() — walidacja → agregat → zapis → zdarzenia");
        Console.WriteLine("Query handler: PobierzZamowieniaKlientaHandler.HandleAsync() — bezpośredni odczyt przez IZamowienieReadRepository");

        // StronicowanaLista demo
        var dto = new ZamowienieListDto(Guid.NewGuid(), "Zlozone", 3800m, 2, DateTime.Now, null);
        var lista = new StronicowanaLista<ZamowienieListDto>(
            new[] { dto }, 1, 1, 20);
        Console.WriteLine($"StronicowanaLista: {lista.LacznaIlosc} rekordów, strona {lista.Strona}/{lista.Rozmiar}");
    }

    private static void DemoInfrastrukturaStub()
    {
        Console.WriteLine("\n-- Infrastructure — EF Core konfiguracja (stub) --");

        var config = new ZamowienieConfiguration();
        var builder = new EfEntityTypeBuilder<Zamowienie>();
        config.Configure(builder);
        Console.WriteLine("ZamowienieConfiguration.Configure() — HasKey, OwnsOne(Email/Adres), HasMany(_pozycje)");

        Console.WriteLine("ZdarzeniaDomenoweInterceptor — po SaveChanges publikuje ZdarzeniaDomenowe agregatów");
        Console.WriteLine("ZamowienieOplaconoHandler    — wysyła email + rezerwuje towar po opłaceniu");
    }
}
