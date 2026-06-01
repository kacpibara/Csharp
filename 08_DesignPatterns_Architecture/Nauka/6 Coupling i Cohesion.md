### Clean Architecture i DDD w C#

---

### 1. Fundamenty — dlaczego te wzorce

csharp

```csharp
// PROBLEM ze standardową architekturą warstwową:
//
// UI → Application → Domain → Infrastructure
//                         ↑
//             Domain zależy od Infrastructure!
// (np. IRepository z EF Core w domenie)
//
// Clean Architecture odwraca tę zależność:
//
//     ┌─────────────────────────────┐
//     │  Infrastructure / UI        │  ← zna wszystkich
//     │  ┌───────────────────────┐  │
//     │  │  Application          │  │  ← zna Domain
//     │  │  ┌─────────────────┐  │  │
//     │  │  │  Domain         │  │  │  ← nie zna nikogo!
//     │  │  │  (centrum)      │  │  │
//     │  │  └─────────────────┘  │  │
//     │  └───────────────────────┘  │
//     └─────────────────────────────┘
//
// Dependency Rule: zależności wskazują TYLKO do środka
// Domain nie wie o EF Core, ASP.NET Core, SQL Server, Redis itp.

// Struktura projektu:
// Sklep.Domain         — encje, agregaty, value objects, zdarzenia domenowe
// Sklep.Application    — use cases (commands/queries), interfejsy repozytoriów
// Sklep.Infrastructure — implementacje repozytoriów, EF Core, email, itp.
// Sklep.API            — kontrolery, middleware, DI setup
```

---

### 2. Value Objects — niezmienne obiekty wartości

csharp

```csharp
// Value Object — identyfikowany przez WARTOŚĆ nie tożsamość
// Niezmienne (immutable), brak ID, semantyczna równość

// === BAZOWA KLASA VALUE OBJECT ===
public abstract class ValueObject
{
    // Podklasy definiują które składniki decydują o równości
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
        => GetEqualityComponents()
            .Aggregate(1, (hash, obj) =>
                HashCode.Combine(hash, obj?.GetHashCode() ?? 0));

    public static bool operator ==(ValueObject? a, ValueObject? b)
        => a?.Equals(b) ?? b is null;

    public static bool operator !=(ValueObject? a, ValueObject? b)
        => !(a == b);
}

// === KONKRETNE VALUE OBJECTS ===

// Pieniądze — wartość + waluta
public class Pieniadze : ValueObject
{
    public decimal Kwota   { get; }
    public string  Waluta  { get; }

    private Pieniadze(decimal kwota, string waluta)
    {
        Kwota  = kwota;
        Waluta = waluta;
    }

    // Fabryka — walidacja przy tworzeniu
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

    // Operacje — zwracają nowy obiekt (immutable!)
    public Pieniadze Dodaj(Pieniadze inne)
    {
        if (Waluta != inne.Waluta)
            throw new InvalidOperationException(
                $"Nie można dodać {Waluta} i {inne.Waluta}");
        return new Pieniadze(Kwota + inne.Kwota, Waluta);
    }

    public Pieniadze Odejmij(Pieniadze inne)
    {
        if (Waluta != inne.Waluta)
            throw new InvalidOperationException(
                $"Nie można odjąć {Waluta} od {inne.Waluta}");
        if (Kwota < inne.Kwota)
            throw new InvalidOperationException("Niewystarczające środki");
        return new Pieniadze(Kwota - inne.Kwota, Waluta);
    }

    public Pieniadze Pomnoz(decimal wspolczynnik)
        => new(Math.Round(Kwota * wspolczynnik, 2), Waluta);

    public bool WiekszyNiz(Pieniadze inne) =>
        Waluta == inne.Waluta && Kwota > inne.Kwota;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kwota;
        yield return Waluta;
    }

    public override string ToString() => $"{Kwota:F2} {Waluta}";
}

// Adres
public class Adres : ValueObject
{
    public string Ulica   { get; }
    public string Miasto  { get; }
    public string KodPocz { get; }
    public string Kraj    { get; }

    private Adres(string ulica, string miasto, string kod, string kraj)
    {
        Ulica   = ulica;
        Miasto  = miasto;
        KodPocz = kod;
        Kraj    = kraj;
    }

    public static Adres Utworz(string ulica, string miasto, string kod,
        string kraj = "PL")
    {
        if (string.IsNullOrWhiteSpace(ulica))
            throw new ArgumentException("Ulica jest wymagana");
        if (string.IsNullOrWhiteSpace(miasto))
            throw new ArgumentException("Miasto jest wymagane");
        if (!System.Text.RegularExpressions.Regex.IsMatch(kod, @"^\d{2}-\d{3}$"))
            throw new ArgumentException("Kod pocztowy format: XX-XXX");

        return new Adres(ulica.Trim(), miasto.Trim(),
            kod.Trim(), kraj.Trim().ToUpper());
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

// Email
public class Email : ValueObject
{
    public string Wartosc { get; }

    private Email(string wartosc) => Wartosc = wartosc;

    public static Email Utworz(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email jest wymagany");

        var znormalizowany = email.Trim().ToLower();

        if (!znormalizowany.Contains('@') || !znormalizowany.Contains('.'))
            throw new ArgumentException($"Nieprawidłowy email: {email}");

        return new Email(znormalizowany);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Wartosc;
    }

    public override string ToString() => Wartosc;
    public static implicit operator string(Email e) => e.Wartosc;
}

// Demo równości Value Objects
var cena1 = Pieniadze.PLN(100m);
var cena2 = Pieniadze.PLN(100m);
var cena3 = Pieniadze.PLN(200m);

Console.WriteLine(cena1 == cena2);  // True — ta sama wartość!
Console.WriteLine(cena1 == cena3);  // False
Console.WriteLine(ReferenceEquals(cena1, cena2));  // False — różne obiekty

var suma = cena1.Dodaj(cena2);
Console.WriteLine(suma);  // 200.00 PLN
```

---

### 3. Entities i Aggregates

csharp

```csharp
// Entity — identyfikowany przez ID (tożsamość)
// Aggregate — klaster encji z jednym Aggregate Root
// Aggregate Root — jedyna "brama" do agregatu

// === BAZOWA ENCJA ===
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

    public override int GetHashCode()
        => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Encja<TId>? a, Encja<TId>? b)
        => a?.Equals(b) ?? b is null;

    public static bool operator !=(Encja<TId>? a, Encja<TId>? b)
        => !(a == b);
}

// === AGGREGATE ROOT ===
public abstract class AggregateRoot<TId> : Encja<TId>
{
    // Zdarzenia domenowe — zbierane i wysyłane po transakcji
    private readonly List<ZdarzenieDoменowe> _zdarzenia = new();

    public IReadOnlyList<ZdarzenieDoменowe> ZdarzeniaDomenowe
        => _zdarzenia.AsReadOnly();

    protected void DodajZdarzenie(ZdarzenieDoменowe zdarzenie)
        => _zdarzenia.Add(zdarzenie);

    public void CzyscZdarzenia()
        => _zdarzenia.Clear();

    protected AggregateRoot() { }
    protected AggregateRoot(TId id) : base(id) { }
}

// Marker dla zdarzeń domenowych
public abstract record ZdarzenieDoменowe(
    DateTime CzasWystapienia = default)
{
    public DateTime CzasWystapienia { get; init; } =
        CzasWystapienia == default ? DateTime.UtcNow : CzasWystapienia;

    public Guid Id { get; init; } = Guid.NewGuid();
}

// === AGGREGATE — Zamowienie ===

// Zdarzenia domenowe zamówienia
public record ZamowienieUtworzono(
    Guid    ZamowienieId,
    Guid    KlientId,
    string  KlientEmail) : ZdarzenieDoменowe;

public record PozycjaDodana(
    Guid    ZamowienieId,
    Guid    ProduktId,
    string  NazwaProduktu,
    Pieniadze Cena,
    int     Ilosc) : ZdarzenieDoменowe;

public record ZamowienieOplacono(
    Guid      ZamowienieId,
    Pieniadze Kwota,
    string    TransakcjaId) : ZdarzenieDoменowe;

public record ZamowienieAnulowano(
    Guid   ZamowienieId,
    string Powod) : ZdarzenieDoменowe;

// Enum statusu — wartość domenowa
public enum StatusZamowienia
{
    Szkic,
    Zlozone,
    Oplacone,
    WRealizacji,
    Wyslane,
    Dostarczone,
    Anulowane
}

// Entity wewnętrzna agregatu — dostępna tylko przez AggregateRoot!
public class PozycjaZamowienia : Encja<Guid>
{
    public Guid      ProduktId     { get; private set; }
    public string    NazwaProduktu { get; private set; }
    public Pieniadze CenaJednostk  { get; private set; }
    public int       Ilosc         { get; private set; }

    public Pieniadze Wartosc => CenaJednostk.Pomnoz(Ilosc);

    private PozycjaZamowienia() { }  // dla EF Core

    internal PozycjaZamowienia(
        Guid produktId, string nazwa,
        Pieniadze cena, int ilosc)
        : base(Guid.NewGuid())
    {
        if (ilosc <= 0)
            throw new ArgumentException("Ilość musi być > 0");

        ProduktId     = produktId;
        NazwaProduktu = nazwa;
        CenaJednostk  = cena;
        Ilosc         = ilosc;
    }

    internal void ZmienIlosc(int nowaIlosc)
    {
        if (nowaIlosc <= 0)
            throw new ArgumentException("Ilość musi być > 0");
        Ilosc = nowaIlosc;
    }
}

// AGGREGATE ROOT — Zamowienie
public class Zamowienie : AggregateRoot<Guid>
{
    // === PRYWATNY STAN ===
    private readonly List<PozycjaZamowienia> _pozycje = new();
    private StatusZamowienia _status = StatusZamowienia.Szkic;

    // === PUBLICZNE WŁAŚCIWOŚCI (read-only) ===
    public Guid              KlientId    { get; private set; }
    public Email             KlientEmail { get; private set; } = null!;
    public Adres             Adres       { get; private set; } = null!;
    public IReadOnlyList<PozycjaZamowienia> Pozycje
        => _pozycje.AsReadOnly();
    public StatusZamowienia  Status      => _status;
    public DateTime          DataZlozenia{ get; private set; }
    public string?           NumerSledzenia { get; private set; }

    // Obliczana właściwość
    public Pieniadze SumaCalkowita =>
        _pozycje.Aggregate(
            Pieniadze.PLN(0),
            (sum, p) => sum.Dodaj(p.Wartosc));

    // Konstruktor prywatny — tylko fabryka może tworzyć
    private Zamowienie() { }

    // === FABRYKA — jedyna metoda tworzenia ===
    public static Zamowienie Utworz(Guid klientId, string email, Adres adres)
    {
        var zamowienie = new Zamowienie
        {
            Id          = Guid.NewGuid(),
            KlientId    = klientId,
            KlientEmail = Email.Utworz(email),
            Adres       = adres,
            DataZlozenia = DateTime.UtcNow
        };

        // Emituj zdarzenie domenowe
        zamowienie.DodajZdarzenie(new ZamowienieUtworzono(
            zamowienie.Id,
            klientId,
            email));

        return zamowienie;
    }

    // === METODY DOMENOWE — logika biznesowa ===
    public void DodajPozycje(
        Guid produktId, string nazwa,
        Pieniadze cena, int ilosc)
    {
        SprawdzStatus(StatusZamowienia.Szkic, StatusZamowienia.Zlozone);

        // Sprawdź czy produkt już w zamówieniu
        var istniejaca = _pozycje
            .FirstOrDefault(p => p.ProduktId == produktId);

        if (istniejaca is not null)
        {
            istniejaca.ZmienIlosc(istniejaca.Ilosc + ilosc);
        }
        else
        {
            var pozycja = new PozycjaZamowienia(produktId, nazwa, cena, ilosc);
            _pozycje.Add(pozycja);
        }

        DodajZdarzenie(new PozycjaDodana(Id, produktId, nazwa, cena, ilosc));
    }

    public void UsunPozycje(Guid produktId)
    {
        SprawdzStatus(StatusZamowienia.Szkic);

        var pozycja = _pozycje.FirstOrDefault(p => p.ProduktId == produktId)
            ?? throw new InvalidOperationException(
                $"Produkt {produktId} nie jest w zamówieniu");

        _pozycje.Remove(pozycja);
    }

    public void Zloz()
    {
        SprawdzStatus(StatusZamowienia.Szkic);

        if (!_pozycje.Any())
            throw new InvalidOperationException(
                "Nie można złożyć pustego zamówienia");

        _status = StatusZamowienia.Zlozone;
    }

    public void Oplac(string transakcjaId)
    {
        SprawdzStatus(StatusZamowienia.Zlozone);

        _status = StatusZamowienia.Oplacone;

        DodajZdarzenie(new ZamowienieOplacono(
            Id, SumaCalkowita, transakcjaId));
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
        _status        = StatusZamowienia.Wyslane;
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

    // Pomocnik sprawdzania statusu
    private void SprawdzStatus(params StatusZamowienia[] dozwolone)
    {
        if (!dozwolone.Contains(_status))
            throw new InvalidOperationException(
                $"Operacja niedozwolona w statusie {_status}. " +
                $"Wymagany: {string.Join(" lub ", dozwolone)}");
    }
}
```

---

### 4. Domain Services i Repository

csharp

```csharp
// Domain Service — logika która nie pasuje do jednej encji

public class SerwisWycenyZamowienia
{
    // Obliczenie rabatu — wymaga wielu agregatów lub zewnętrznych zasad
    public Pieniadze ObliczRabat(
        Zamowienie zamowienie,
        Klient klient,
        IReadOnlyList<KodPromocyjny> aktywneKody)
    {
        var suma = zamowienie.SumaCalkowita;

        // Reguła 1 — stały klient
        if (klient.LiczbaZamowien > 10)
        {
            var rabat = suma.Pomnoz(0.1m);
            Console.WriteLine($"Rabat stały klient 10%: {rabat}");
            return rabat;
        }

        // Reguła 2 — kod promocyjny
        foreach (var kod in aktywneKody)
        {
            if (kod.CzyAktywny && zamowienie.SumaCalkowita.WiekszyNiz(kod.MinimalnaSuma))
            {
                var rabat = suma.Pomnoz(kod.ProcentRabatu / 100);
                Console.WriteLine($"Rabat kod {kod.Wartosc}: {rabat}");
                return rabat;
            }
        }

        return Pieniadze.PLN(0);
    }
}

// === INTERFEJSY REPOZYTORIÓW — w Application layer ===
// (implementacje w Infrastructure layer)

public interface IZamowienieRepository
{
    Task<Zamowienie?> PobierzPoIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Zamowienie>> PobierzPoKliencieAsync(
        Guid klientId, CancellationToken ct = default);
    Task DodajAsync(Zamowienie zamowienie, CancellationToken ct = default);
    Task AktualizujAsync(Zamowienie zamowienie, CancellationToken ct = default);
    Task<bool> IstniejeAsync(Guid id, CancellationToken ct = default);
}

public interface IKlientRepository
{
    Task<Klient?> PobierzPoIdAsync(Guid id, CancellationToken ct = default);
    Task<Klient?> PobierzPoEmailuAsync(Email email, CancellationToken ct = default);
    Task DodajAsync(Klient klient, CancellationToken ct = default);
}

// Uproszczone modele dla kompilacji
public class Klient : AggregateRoot<Guid>
{
    public Email Email           { get; private set; } = null!;
    public int   LiczbaZamowien { get; private set; } = 0;

    public static Klient Utworz(string email)
        => new() { Id = Guid.NewGuid(), Email = Email.Utworz(email) };
}

public class KodPromocyjny : ValueObject
{
    public string   Wartosc       { get; }
    public decimal  ProcentRabatu { get; }
    public Pieniadze MinimalnaSuma { get; }
    public bool     CzyAktywny    { get; }

    public KodPromocyjny(string wartosc, decimal procent,
        Pieniadze min, bool aktywny)
    {
        Wartosc       = wartosc;
        ProcentRabatu = procent;
        MinimalnaSuma = min;
        CzyAktywny    = aktywny;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Wartosc.ToUpper();
    }
}
```

---

### 5. Application Layer — Use Cases (CQRS)

csharp

```csharp
// CQRS — Command Query Responsibility Segregation
// Commands — zmieniają stan, nie zwracają danych (lub tylko ID)
// Queries  — odczytują dane, nie zmieniają stanu

// === COMMAND ===
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

// Wynik komendy
public record UtworzZamowienieResult(
    Guid   ZamowienieId,
    string Numer);

// Handler komendy
public class UtworzZamowienieHandler
{
    private readonly IZamowienieRepository _zamRepo;
    private readonly IKlientRepository     _kliRepo;
    private readonly IZdarzenieDoменoweBus _bus;
    private readonly IUnitOfWork           _uow;

    public UtworzZamowienieHandler(
        IZamowienieRepository zamRepo,
        IKlientRepository kliRepo,
        IZdarzenieDoменoweBus bus,
        IUnitOfWork uow)
    {
        _zamRepo = zamRepo;
        _kliRepo = kliRepo;
        _bus     = bus;
        _uow     = uow;
    }

    public async Task<UtworzZamowienieResult> HandleAsync(
        UtworzZamowienieCommand cmd,
        CancellationToken ct = default)
    {
        // 1. Walidacja
        if (!cmd.Pozycje.Any())
            throw new ArgumentException("Zamówienie musi mieć pozycje");

        // 2. Pobierz klienta
        var klient = await _kliRepo.PobierzPoIdAsync(cmd.KlientId, ct)
            ?? throw new InvalidOperationException(
                $"Klient {cmd.KlientId} nie istnieje");

        // 3. Utwórz agregat
        var adres = Adres.Utworz(cmd.Ulica, cmd.Miasto, cmd.KodPocztowy);
        var zamowienie = Zamowienie.Utworz(klient.Id, cmd.Email, adres);

        // 4. Dodaj pozycje
        foreach (var poz in cmd.Pozycje)
        {
            zamowienie.DodajPozycje(
                poz.ProduktId,
                poz.NazwaProduktu,
                Pieniadze.PLN(poz.Cena),
                poz.Ilosc);
        }

        // 5. Złóż zamówienie
        zamowienie.Zloz();

        // 6. Zapisz
        await _zamRepo.DodajAsync(zamowienie, ct);
        await _uow.SaveChangesAsync(ct);

        // 7. Opublikuj zdarzenia domenowe (po commicie!)
        foreach (var zdarzenie in zamowienie.ZdarzeniaDomenowe)
            await _bus.PublikujAsync(zdarzenie, ct);

        zamowienie.CzyscZdarzenia();

        return new UtworzZamowienieResult(
            zamowienie.Id,
            $"ZAM-{DateTime.Now:yyyyMMdd}-{zamowienie.Id.ToString("N")[..6].ToUpper()}");
    }
}

// === QUERY ===
public record PobierzZamowieniaKlientaQuery(
    Guid KlientId,
    int  Strona   = 1,
    int  Rozmiar  = 20);

public record ZamowienieListDto(
    Guid     Id,
    string   Status,
    decimal  Suma,
    int      LiczbaPozycji,
    DateTime Data,
    string?  NumerSledzenia);

public class PobierzZamowieniaKlientaHandler
{
    private readonly IZamowienieReadRepository _repo;

    public PobierzZamowieniaKlientaHandler(IZamowienieReadRepository repo)
        => _repo = repo;

    public async Task<StronicowanaLista<ZamowienieListDto>> HandleAsync(
        PobierzZamowieniaKlientaQuery query,
        CancellationToken ct = default)
    {
        // Queries mogą iść bezpośrednio do bazy (Dapper, EF AsNoTracking)
        // bez przechodzenia przez agregaty!
        return await _repo.PobierzDlaKlientaAsync(
            query.KlientId,
            query.Strona,
            query.Rozmiar,
            ct);
    }
}

public record StronicowanaLista<T>(
    IReadOnlyList<T> Dane,
    int LacznaIlosc,
    int Strona,
    int Rozmiar);

// Interfejsy dla kompilacji
public interface IZdarzenieDoменoweBus
{
    Task PublikujAsync(ZdarzenieDoменowe zdarzenie, CancellationToken ct);
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
```

---

### 6. Infrastructure — EF Core + zdarzenia

csharp

```csharp
// Infrastructure layer — implementacje interfejsów z Application/Domain

public class ZamowienieConfiguration
    : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Zamowienie>
{
    public void Configure(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Zamowienie> builder)
    {
        builder.HasKey(z => z.Id);

        // Value Object jako owned entity
        builder.OwnsOne(z => z.KlientEmail, e =>
        {
            e.Property(x => x.Wartosc)
             .HasColumnName("KlientEmail")
             .HasMaxLength(200);
        });

        builder.OwnsOne(z => z.Adres, a =>
        {
            a.Property(x => x.Ulica).HasMaxLength(200);
            a.Property(x => x.Miasto).HasMaxLength(100);
            a.Property(x => x.KodPocz).HasMaxLength(10);
            a.Property(x => x.Kraj).HasMaxLength(2);
        });

        // Value Object jako conversion
        builder.Property(z => z.Status)
               .HasConversion<string>()
               .HasMaxLength(50);

        // Kolekcja wewnętrzna — navigation przez backing field
        builder.HasMany<PozycjaZamowienia>("_pozycje")
               .WithOne()
               .HasForeignKey("ZamowienieId");

        builder.Navigation("_pozycje").UsePropertyAccessMode(
            Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
    }
}

// Interceptor dla zdarzeń domenowych
public class ZdarzeniaDomenoweInterceptor
    : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
{
    private readonly IZdarzenieDoменoweBus _bus;

    public ZdarzeniaDomenoweInterceptor(IZdarzenieDoменoweBus bus)
        => _bus = bus;

    public override async ValueTask<int> SavedChangesAsync(
        Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData data,
        int result,
        CancellationToken ct = default)
    {
        // Po zapisaniu do bazy — opublikuj zdarzenia domenowe
        var agregaty = data.Context!.ChangeTracker
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

// Handlery zdarzeń domenowych
public class ZamowienieOplaconoHandler
{
    private readonly IEmailSerwis _email;
    private readonly IInwentarzSerwis _inwentarz;

    public ZamowienieOplaconoHandler(
        IEmailSerwis email, IInwentarzSerwis inwentarz)
    {
        _email     = email;
        _inwentarz = inwentarz;
    }

    public async Task HandleAsync(
        ZamowienieOplacono zdarzenie, CancellationToken ct)
    {
        Console.WriteLine(
            $"[Handler] ZamowienieOplacono: #{zdarzenie.ZamowienieId}");

        // Email do klienta
        await _email.WyslijAsync(
            "klient@test.pl",
            $"Płatność {zdarzenie.Kwota} potwierdzona");

        // Zmniejsz stan magazynu
        await _inwentarz.ZarezerwujTowarAsync(
            zdarzenie.ZamowienieId, ct);
    }
}
```

---

### 7. Praktyczny przykład — cały flow

csharp

```csharp
// === DEMONSTRACJA KOMPLETNEGO FLOW ===

// 1. Tworzenie agregatów przez fabryki
var adres = Adres.Utworz("ul. Marszałkowska 1", "Warszawa", "00-001");
var klient = Klient.Utworz("jan@test.pl");

Console.WriteLine($"Klient: {klient.Email}");
Console.WriteLine($"Adres: {adres}");

// 2. Tworzenie zamówienia
var zamowienie = Zamowienie.Utworz(klient.Id, "jan@test.pl", adres);

// 3. Dodawanie pozycji przez metody domenowe
zamowienie.DodajPozycje(Guid.NewGuid(), "Laptop", Pieniadze.PLN(3500m), 1);
zamowienie.DodajPozycje(Guid.NewGuid(), "Mysz",   Pieniadze.PLN(150m),  2);
zamowienie.DodajPozycje(Guid.NewGuid(), "Kabel",  Pieniadze.PLN(49m),   3);

Console.WriteLine($"Pozycji: {zamowienie.Pozycje.Count}");
Console.WriteLine($"Suma: {zamowienie.SumaCalkowita}");

// 4. Przejście przez stany
zamowienie.Zloz();
Console.WriteLine($"Status: {zamowienie.Status}");

zamowienie.Oplac("TXN-ABC123");
Console.WriteLine($"Status: {zamowienie.Status}");

// 5. Sprawdź zdarzenia domenowe
Console.WriteLine($"\nZdarzenia domenowe ({zamowienie.ZdarzeniaDomenowe.Count}):");
foreach (var z in zamowienie.ZdarzeniaDomenowe)
    Console.WriteLine($"  - {z.GetType().Name}");

// 6. Niedozwolona operacja rzuca wyjątek
try
{
    zamowienie.DodajPozycje(Guid.NewGuid(), "Klawiatura",
        Pieniadze.PLN(250m), 1);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"\nBłąd (oczekiwany): {ex.Message}");
}

// 7. Value Objects — równość przez wartość
var cena1 = Pieniadze.PLN(3500m);
var cena2 = Pieniadze.PLN(3500m);
Console.WriteLine($"\nValue Object równość: {cena1 == cena2}");  // True

var email1 = Email.Utworz("jan@test.pl");
var email2 = Email.Utworz("JAN@TEST.PL");  // case insensitive!
Console.WriteLine($"Email równość: {email1 == email2}");  // True

// 8. Anulowanie
var inne = Zamowienie.Utworz(klient.Id, "jan@test.pl", adres);
inne.DodajPozycje(Guid.NewGuid(), "Test", Pieniadze.PLN(100m), 1);
inne.Zloz();
inne.Anuluj("Zmiana decyzji");
Console.WriteLine($"Anulowane: {inne.Status}");

// Stubs
public interface IEmailSerwis
{
    Task WyslijAsync(string to, string body);
}
public interface IInwentarzSerwis
{
    Task ZarezerwujTowarAsync(Guid zamId, CancellationToken ct);
}
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między Entity a Value Object?"** Entity ma tożsamość — dwa obiekty z tym samym ID to ten sam byt nawet jeśli inne pola się różnią. Porównujemy przez ID. Value Object ma tylko wartość — dwa obiekty z identycznymi danymi są sobie równe, brak ID, są niezmienne. `Pieniadze(100, "PLN") == Pieniadze(100, "PLN")` — true. Zmiana Value Object = stworzenie nowego obiektu, nie modyfikacja. Przykłady VO: Pieniądze, Adres, Email, Kolor, DateRange.

**"Co to Aggregate Root i dlaczego ważny?"** Aggregate Root to jedyna "brama" do agregatu — wszystkie operacje na wewnętrznych encjach przechodzą przez Root. Gwarantuje niezmienniki (invariants) — reguły biznesowe które muszą być spełnione zawsze. `Zamowienie.DodajPozycje()` sprawdza status, `Zamowienie.Oplac()` weryfikuje że było złożone. Nikt z zewnątrz nie może bezpośrednio modyfikować `PozycjaZamowienia` — tylko przez `Zamowienie`. Repository zawsze zapisuje cały agregat atomowo.

**"Jak Domain Events pomagają w Clean Architecture?"** Domain Events oddzielają skutki uboczne od logiki biznesowej. Agregat emituje zdarzenia (np. `ZamowienieOplacono`) nie wiedząc kto zareaguje. Handler może wysłać email, zmniejszyć stan magazynu, powiadomić zewnętrzne systemy. Korzyści: (1) Single Responsibility — agregat robi tylko swoje, efekty w handlerach, (2) testowanie agregatu bez emaili/magazynu, (3) łatwe dodawanie nowych efektów bez modyfikacji domeny. Zdarzenia publikowane PO commicie — dane zapisane, nie można cofnąć.

**"Jaka różnica między Command a Query w CQRS?"** Command zmienia stan aplikacji — nie zwraca danych (lub tylko ID nowego zasobu). Query odczytuje dane — nie zmienia stanu, można go cachować, powtarzać. Korzyść: Query może iść bezpośrednio przez Dapper/SQL bez ładowania agregatów — szybsze read modele. Command przechodzi przez pełną domenę — walidacja, logika, zdarzenia. Fizyczny podział: osobna baza read/write (eventual consistency) lub logiczny podział w tej samej bazie (prostszy start).