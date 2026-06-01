### Wzorce Behawioralne w C#

Wzorce behawioralne rozwiązują **jak obiekty komunikują się i współpracują** — algorytmy, odpowiedzialności, przepływ sterowania.

---

### 1. Observer — reaguj na zdarzenia

csharp

```csharp
// Observer — definiuje zależność jeden-do-wielu
// gdy obiekt zmienia stan, wszyscy obserwatorzy są powiadamiani automatycznie

// === IMPLEMENTACJA Z INTERFEJSAMI ===

// Subject (Observable) — obiekt który emituje zdarzenia
public interface IObserwowalne<T>
{
    void Subskrybuj(IObserwator<T> obserwator);
    void Anuluj(IObserwator<T> obserwator);
    void Powiadom(T zdarzenie);
}

// Observer — reaguje na zdarzenia
public interface IObserwator<T>
{
    void Aktualizuj(T zdarzenie);
}

// === ZDARZENIA DOMENOWE ===
public abstract record ZdarzenieSklepu(DateTime CzasZdarzenia = default)
{
    public DateTime CzasZdarzenia { get; init; } =
        CzasZdarzenia == default ? DateTime.UtcNow : CzasZdarzenia;
}

public record ZamowienieZlozone(
    int    ZamowienieId,
    int    KlientId,
    decimal Kwota,
    List<int> ProduktIds) : ZdarzenieSklepu;

public record ZamowienieOplacone(
    int    ZamowienieId,
    string TransakcjaId,
    decimal Kwota) : ZdarzenieSklepu;

public record StanMagazynuNiski(
    int     ProduktId,
    string  NazwaProduktu,
    int     AktualnyStan,
    int     MinimalnyProg) : ZdarzenieSklepu;

// Sklep — Subject emitujący zdarzenia
public class SklepZdarzeniaHub : IObserwowalne<ZdarzenieSklepu>
{
    private readonly List<IObserwator<ZdarzenieSklepu>> _obserwatorzy = new();
    private readonly ILogger<SklepZdarzeniaHub> _logger;

    public SklepZdarzeniaHub(ILogger<SklepZdarzeniaHub> logger)
        => _logger = logger;

    public void Subskrybuj(IObserwator<ZdarzenieSklepu> obs)
    {
        _obserwatorzy.Add(obs);
        _logger.LogDebug("Subskrypcja: {Type}", obs.GetType().Name);
    }

    public void Anuluj(IObserwator<ZdarzenieSklepu> obs)
    {
        _obserwatorzy.Remove(obs);
        _logger.LogDebug("Anulowanie: {Type}", obs.GetType().Name);
    }

    public void Powiadom(ZdarzenieSklepu zdarzenie)
    {
        _logger.LogInformation("Zdarzenie: {Type}", zdarzenie.GetType().Name);

        // Kopia listy — obserwator może anulować podczas powiadomienia
        foreach (var obs in _obserwatorzy.ToList())
        {
            try
            {
                obs.Aktualizuj(zdarzenie);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Błąd obserwatora {Type}", obs.GetType().Name);
                // Jeden błąd nie zatrzymuje pozostałych!
            }
        }
    }

    // Wygodna metoda publikowania
    public void Publikuj<T>(T zdarzenie) where T : ZdarzenieSklepu
        => Powiadom(zdarzenie);
}

// Konkretne obserwatory
public class EmailPowiadomieniaObserwator : IObserwator<ZdarzenieSklepu>
{
    private readonly IEmailSerwis _email;

    public EmailPowiadomieniaObserwator(IEmailSerwis email)
        => _email = email;

    public void Aktualizuj(ZdarzenieSklepu zdarzenie)
    {
        switch (zdarzenie)
        {
            case ZamowienieZlozone z:
                _email.WyslijAsync(
                    $"klient_{z.KlientId}@test.pl",
                    $"Zamówienie #{z.ZamowienieId} przyjęte — {z.Kwota:C}");
                Console.WriteLine(
                    $"[Email] Potwierdzenie zamówienia #{z.ZamowienieId}");
                break;

            case ZamowienieOplacone op:
                _email.WyslijAsync(
                    "klient@test.pl",
                    $"Płatność {op.Kwota:C} potwierdzona — transakcja {op.TransakcjaId}");
                Console.WriteLine(
                    $"[Email] Potwierdzenie płatności #{op.ZamowienieId}");
                break;
        }
    }
}

public class InwentarzObserwator : IObserwator<ZdarzenieSklepu>
{
    private readonly IInwentarzSerwis _inwentarz;

    public InwentarzObserwator(IInwentarzSerwis inwentarz)
        => _inwentarz = inwentarz;

    public void Aktualizuj(ZdarzenieSklepu zdarzenie)
    {
        if (zdarzenie is ZamowienieZlozone z)
        {
            foreach (int prodId in z.ProduktIds)
            {
                _inwentarz.ZmniejszStan(prodId, 1);
                Console.WriteLine(
                    $"[Inwentarz] Zmniejszono stan produktu #{prodId}");
            }
        }
    }
}

public class AlertMagazynowyObserwator : IObserwator<ZdarzenieSklepu>
{
    private readonly IEmailSerwis _email;

    public AlertMagazynowyObserwator(IEmailSerwis email)
        => _email = email;

    public void Aktualizuj(ZdarzenieSklepu zdarzenie)
    {
        if (zdarzenie is StanMagazynuNiski alert)
        {
            Console.WriteLine(
                $"[ALERT] {alert.NazwaProduktu}: stan {alert.AktualnyStan}/" +
                $"{alert.MinimalnyProg}");
            _email.WyslijAsync("magazyn@sklep.pl",
                $"ALERT: Niski stan {alert.NazwaProduktu}!");
        }
    }
}

public class AnalitykiObserwator : IObserwator<ZdarzenieSklepu>
{
    private int _liczbaZamowien = 0;
    private decimal _sumaSprzedazy = 0;

    public void Aktualizuj(ZdarzenieSklepu zdarzenie)
    {
        switch (zdarzenie)
        {
            case ZamowienieZlozone z:
                _liczbaZamowien++;
                _sumaSprzedazy += z.Kwota;
                Console.WriteLine(
                    $"[Analityki] Zamówień: {_liczbaZamowien}, Suma: {_sumaSprzedazy:C}");
                break;
        }
    }
}

// === WERSJA Z C# EVENTS (alternatywa) ===
public class ZamowieniaSerwisZEventami
{
    // C# events — wbudowany Observer
    public event EventHandler<ZamowienieZlozone>?  OnZamowienieZlozone;
    public event EventHandler<ZamowienieOplacone>? OnZamowienieOplacone;

    public async Task<int> ZlozZamowienieAsync(
        int klientId, decimal kwota,
        List<int> produktIds)
    {
        int id = new Random().Next(1000, 9999);

        // Logika biznesowa...
        await Task.Delay(100);

        // Powiadom wszystkich subskrybentów
        OnZamowienieZlozone?.Invoke(this,
            new ZamowienieZlozone(id, klientId, kwota, produktIds));

        return id;
    }
}

// === UŻYCIE ===
var hub = new SklepZdarzeniaHub(
    Microsoft.Extensions.Logging.Abstractions
        .NullLogger<SklepZdarzeniaHub>.Instance);

var emailSerwis = new MockEmailSerwis();

hub.Subskrybuj(new EmailPowiadomieniaObserwator(emailSerwis));
hub.Subskrybuj(new InwentarzObserwator(new MockInwentarz()));
hub.Subskrybuj(new AnalitykiObserwator());
hub.Subskrybuj(new AlertMagazynowyObserwator(emailSerwis));

// Publikuj zdarzenia
hub.Publikuj(new ZamowienieZlozone(1001, 42, 3500m,
    new List<int> { 1, 2 }));
hub.Publikuj(new ZamowienieOplacone(1001, "TXN-ABC123", 3500m));
hub.Publikuj(new StanMagazynuNiski(1, "Laptop", 2, 5));

// Stubs
public interface IEmailSerwis { Task WyslijAsync(string to, string body); }
public interface IInwentarzSerwis { void ZmniejszStan(int id, int ilosc); }
public class MockEmailSerwis : IEmailSerwis
{
    public Task WyslijAsync(string to, string body)
    {
        Console.WriteLine($"[Mock Email → {to}]: {body[..Math.Min(40, body.Length)]}");
        return Task.CompletedTask;
    }
}
public class MockInwentarz : IInwentarzSerwis
{
    public void ZmniejszStan(int id, int ilosc)
        => Console.WriteLine($"[Mock Inwentarz] #{id} -= {ilosc}");
}
```

---

### 2. Strategy — wymienne algorytmy

csharp

```csharp
// Strategy — definiuje rodzinę algorytmów, enkapsuluje każdy z nich
// i umożliwia ich wymianę. Algorytm niezależny od klientów.

// === PROBLEM — wiele strategii sortowania/filtrowania ===

public interface IStrategiaRabatu
{
    decimal ObliczRabat(KoszykZakupow koszyk);
    string Nazwa { get; }
}

public class KoszykZakupow
{
    public List<ElementKoszyka> Elementy { get; init; } = new();
    public int     KlientId        { get; init; }
    public bool    JestCzlonkiem   { get; init; }
    public bool    PierwszeZamow   { get; init; }
    public string? KodPromocyjny   { get; init; }
    public DateTime DataZakupu     { get; init; } = DateTime.UtcNow;

    public decimal SumaCalkowita =>
        Elementy.Sum(e => e.Cena * e.Ilosc);
}

public record ElementKoszyka(
    int ProduktId, string Nazwa, decimal Cena, int Ilosc,
    string Kategoria = "");

// Konkretne strategie
public class BrakRabatu : IStrategiaRabatu
{
    public string Nazwa => "Brak rabatu";
    public decimal ObliczRabat(KoszykZakupow k) => 0m;
}

public class RabatProcentowy : IStrategiaRabatu
{
    private readonly decimal _procent;
    public RabatProcentowy(decimal procent) => _procent = procent;
    public string Nazwa => $"Rabat {_procent}%";

    public decimal ObliczRabat(KoszykZakupow k)
    {
        decimal rabat = k.SumaCalkowita * _procent / 100;
        Console.WriteLine($"[Rabat] {_procent}% od {k.SumaCalkowita:C} = {rabat:C}");
        return rabat;
    }
}

public class RabatStalegoKlienta : IStrategiaRabatu
{
    public string Nazwa => "Rabat stałego klienta (10%)";

    public decimal ObliczRabat(KoszykZakupow k)
    {
        if (!k.JestCzlonkiem) return 0m;

        // Progi — im więcej kupisz, tym wyższy rabat
        decimal rabat = k.SumaCalkowita switch
        {
            >= 1000m => k.SumaCalkowita * 0.15m,
            >= 500m  => k.SumaCalkowita * 0.10m,
            >= 200m  => k.SumaCalkowita * 0.05m,
            _        => 0m
        };
        Console.WriteLine($"[Rabat] Stały klient: {rabat:C}");
        return rabat;
    }
}

public class RabatPierwszegoZamowienia : IStrategiaRabatu
{
    public string Nazwa => "Rabat pierwszego zamówienia (20%)";

    public decimal ObliczRabat(KoszykZakupow k)
    {
        if (!k.PierwszeZamow) return 0m;
        decimal rabat = k.SumaCalkowita * 0.20m;
        Console.WriteLine($"[Rabat] Pierwsze zamówienie: {rabat:C}");
        return rabat;
    }
}

public class RabatSezonowy : IStrategiaRabatu
{
    public string Nazwa => "Rabat sezonowy";

    public decimal ObliczRabat(KoszykZakupow k)
    {
        // Rabat w określonym miesiącu (np. grudzień = święta)
        if (k.DataZakupu.Month == 12)
        {
            decimal rabat = k.SumaCalkowita * 0.12m;
            Console.WriteLine($"[Rabat] Świąteczny: {rabat:C}");
            return rabat;
        }
        return 0m;
    }
}

public class RabatKoduPromocyjnego : IStrategiaRabatu
{
    private readonly Dictionary<string, decimal> _kody = new()
    {
        ["SAVE10"] = 10m,
        ["SUMMER20"] = 20m,
        ["VIP50"] = 50m
    };

    public string Nazwa => "Kod promocyjny";

    public decimal ObliczRabat(KoszykZakupow k)
    {
        if (k.KodPromocyjny is null) return 0m;
        if (!_kody.TryGetValue(k.KodPromocyjny.ToUpper(), out decimal procent))
            return 0m;

        decimal rabat = k.SumaCalkowita * procent / 100;
        Console.WriteLine($"[Rabat] Kod '{k.KodPromocyjny}': {procent}% = {rabat:C}");
        return rabat;
    }
}

// Context — używa strategii
public class KalkulatorCeny
{
    // Strategy może być zmieniana w runtime!
    private IStrategiaRabatu _strategia;

    public KalkulatorCeny(IStrategiaRabatu? strategia = null)
        => _strategia = strategia ?? new BrakRabatu();

    public void ZmienStrategie(IStrategiaRabatu nowaStrategia)
    {
        Console.WriteLine($"Zmiana strategii: {_strategia.Nazwa} → {nowaStrategia.Nazwa}");
        _strategia = nowaStrategia;
    }

    public WynikKalkulacji Oblicz(KoszykZakupow koszyk)
    {
        decimal suma    = koszyk.SumaCalkowita;
        decimal rabat   = _strategia.ObliczRabat(koszyk);
        decimal doZapla = suma - rabat;

        return new WynikKalkulacji(
            Suma:            suma,
            Rabat:           rabat,
            DoZaplaty:       doZapla,
            ProcentRabatu:   suma > 0 ? rabat / suma * 100 : 0,
            NazwaStrategii:  _strategia.Nazwa);
    }
}

public record WynikKalkulacji(
    decimal Suma,
    decimal Rabat,
    decimal DoZaplaty,
    decimal ProcentRabatu,
    string NazwaStrategii);

// Selector strategii — wybiera najlepszą automatycznie
public class SelectorStrategii
{
    private readonly List<IStrategiaRabatu> _strategie;

    public SelectorStrategii()
    {
        _strategie = new List<IStrategiaRabatu>
        {
            new RabatPierwszegoZamowienia(),
            new RabatKoduPromocyjnego(),
            new RabatStalegoKlienta(),
            new RabatSezonowy(),
            new RabatProcentowy(5)
        };
    }

    // Wybierz strategię z największym rabatem
    public IStrategiaRabatu WybierzNajlepszaDla(KoszykZakupow koszyk)
    {
        var najlepsza = _strategie
            .Select(s => (Strategia: s, Rabat: s.ObliczRabat(koszyk)))
            .MaxBy(x => x.Rabat);

        return najlepsza?.Strategia ?? new BrakRabatu();
    }
}

// Użycie
var koszyk = new KoszykZakupow
{
    KlientId      = 42,
    JestCzlonkiem = true,
    PierwszeZamow = false,
    KodPromocyjny = "SAVE10",
    Elementy      = new()
    {
        new(1, "Laptop",   3500m, 1, "IT"),
        new(2, "Mysz",      150m, 2, "IT"),
        new(3, "Klawiatura",250m, 1, "IT")
    }
};

var kalkulator = new KalkulatorCeny();

// Ręczna zmiana strategii
kalkulator.ZmienStrategie(new RabatStalegoKlienta());
var wynik1 = kalkulator.Oblicz(koszyk);
Console.WriteLine($"Stały klient: {wynik1.DoZaplaty:C} (rabat: {wynik1.Rabat:C})");

kalkulator.ZmienStrategie(new RabatKoduPromocyjnego());
var wynik2 = kalkulator.Oblicz(koszyk);
Console.WriteLine($"Kod SAVE10: {wynik2.DoZaplaty:C} (rabat: {wynik2.Rabat:C})");

// Automatyczny wybór najlepszej strategii
var selector = new SelectorStrategii();
var najlepsza = selector.WybierzNajlepszaDla(koszyk);
kalkulator.ZmienStrategie(najlepsza);
var wynikFinalny = kalkulator.Oblicz(koszyk);
Console.WriteLine(
    $"Najlepsza strategia: {wynikFinalny.NazwaStrategii}: {wynikFinalny.DoZaplaty:C}");
```

---

### 3. Command — enkapsulacja operacji

csharp

```csharp
// Command — enkapsuluje żądanie jako obiekt
// Pozwala: kolejkowanie, logowanie operacji, UNDO/REDO

public interface IKomenda
{
    Task WykonajAsync(CancellationToken ct = default);
    Task CofnijAsync(CancellationToken ct = default);
    string Opis { get; }
}

// Receiver — faktyczne operacje na encjach
public class EdytorDokumentu
{
    private string _tresc;
    public string Tresc => _tresc;

    public EdytorDokumentu(string tresc = "") => _tresc = tresc;

    public void WstawTekst(int pozycja, string tekst)
    {
        _tresc = _tresc.Insert(pozycja, tekst);
        Console.WriteLine($"[Edytor] Wstawiono '{tekst}' na poz. {pozycja}");
    }

    public void UsunTekst(int pozycja, int dlugosc)
    {
        string usunieto = _tresc.Substring(pozycja, dlugosc);
        _tresc = _tresc.Remove(pozycja, dlugosc);
        Console.WriteLine($"[Edytor] Usunięto '{usunieto}'");
    }

    public void ZamienTekst(string stary, string nowy)
    {
        _tresc = _tresc.Replace(stary, nowy);
        Console.WriteLine($"[Edytor] Zamieniono '{stary}' → '{nowy}'");
    }
}

// Konkretne komendy
public class KomendaWstawTekst : IKomenda
{
    private readonly EdytorDokumentu _edytor;
    private readonly int    _pozycja;
    private readonly string _tekst;

    public string Opis => $"Wstaw '{_tekst}' na poz. {_pozycja}";

    public KomendaWstawTekst(EdytorDokumentu edytor, int pozycja, string tekst)
    {
        _edytor  = edytor;
        _pozycja = pozycja;
        _tekst   = tekst;
    }

    public Task WykonajAsync(CancellationToken ct = default)
    {
        _edytor.WstawTekst(_pozycja, _tekst);
        return Task.CompletedTask;
    }

    public Task CofnijAsync(CancellationToken ct = default)
    {
        _edytor.UsunTekst(_pozycja, _tekst.Length);
        return Task.CompletedTask;
    }
}

public class KomendaUsunTekst : IKomenda
{
    private readonly EdytorDokumentu _edytor;
    private readonly int    _pozycja;
    private readonly int    _dlugosc;
    private string _usunieto = "";  // dla undo!

    public string Opis => $"Usuń {_dlugosc} znaków z poz. {_pozycja}";

    public KomendaUsunTekst(EdytorDokumentu edytor, int pozycja, int dlugosc)
    {
        _edytor  = edytor;
        _pozycja = pozycja;
        _dlugosc = dlugosc;
    }

    public Task WykonajAsync(CancellationToken ct = default)
    {
        _usunieto = _edytor.Tresc.Substring(_pozycja, _dlugosc);
        _edytor.UsunTekst(_pozycja, _dlugosc);
        return Task.CompletedTask;
    }

    public Task CofnijAsync(CancellationToken ct = default)
    {
        // Przywróć usunięty tekst!
        _edytor.WstawTekst(_pozycja, _usunieto);
        return Task.CompletedTask;
    }
}

// Invoker — zarządza historią komend (Undo/Redo)
public class HistoriaKomend
{
    private readonly Stack<IKomenda> _wykonane = new();
    private readonly Stack<IKomenda> _cofniete = new();
    private readonly int _maxHistoria;

    public HistoriaKomend(int maxHistoria = 50)
        => _maxHistoria = maxHistoria;

    public bool MoznaUndo => _wykonane.Any();
    public bool MoznaRedo => _cofniete.Any();

    public IReadOnlyList<string> HistoriaOpisy =>
        _wykonane.Select(k => k.Opis).Reverse().ToList();

    public async Task WykonajAsync(
        IKomenda komenda, CancellationToken ct = default)
    {
        await komenda.WykonajAsync(ct);
        _wykonane.Push(komenda);
        _cofniete.Clear();  // nowa komenda kasuje redo history

        // Ogranicz historię
        while (_wykonane.Count > _maxHistoria)
        {
            // Stack nie ma metody RemoveBottom — użyj listy
            var temp = _wykonane.ToArray().Reverse().ToArray();
            _wykonane.Clear();
            foreach (var k in temp.Skip(1))
                _wykonane.Push(k);
        }
    }

    public async Task<bool> UndoAsync(CancellationToken ct = default)
    {
        if (!MoznaUndo) return false;

        var komenda = _wykonane.Pop();
        await komenda.CofnijAsync(ct);
        _cofniete.Push(komenda);
        Console.WriteLine($"[Undo] {komenda.Opis}");
        return true;
    }

    public async Task<bool> RedoAsync(CancellationToken ct = default)
    {
        if (!MoznaRedo) return false;

        var komenda = _cofniete.Pop();
        await komenda.WykonajAsync(ct);
        _wykonane.Push(komenda);
        Console.WriteLine($"[Redo] {komenda.Opis}");
        return true;
    }
}

// Makro komenda — kompozyt komend
public class MacroKomenda : IKomenda
{
    private readonly List<IKomenda> _komendy;
    public string Opis => $"Makro [{string.Join(", ", _komendy.Select(k => k.Opis))}]";

    public MacroKomenda(params IKomenda[] komendy)
        => _komendy = komendy.ToList();

    public async Task WykonajAsync(CancellationToken ct = default)
    {
        foreach (var k in _komendy)
            await k.WykonajAsync(ct);
    }

    public async Task CofnijAsync(CancellationToken ct = default)
    {
        // Cofaj w odwrotnej kolejności!
        foreach (var k in Enumerable.Reverse(_komendy))
            await k.CofnijAsync(ct);
    }
}

// Użycie
var edytor  = new EdytorDokumentu("Witaj świecie!");
var historia = new HistoriaKomend();

Console.WriteLine($"Start: '{edytor.Tresc}'");

// Wykonaj komendy
await historia.WykonajAsync(new KomendaWstawTekst(edytor, 6, " piękny"));
Console.WriteLine($"Po wstawieniu: '{edytor.Tresc}'");

await historia.WykonajAsync(new KomendaUsunTekst(edytor, 0, 5));
Console.WriteLine($"Po usunięciu: '{edytor.Tresc}'");

// Makro
var makro = new MacroKomenda(
    new KomendaWstawTekst(edytor, 0, ">>> "),
    new KomendaWstawTekst(edytor, edytor.Tresc.Length + 4, " <<<"));
await historia.WykonajAsync(makro);
Console.WriteLine($"Po makro: '{edytor.Tresc}'");

// UNDO!
Console.WriteLine("\n--- UNDO ---");
while (historia.MoznaUndo)
{
    await historia.UndoAsync();
    Console.WriteLine($"Stan: '{edytor.Tresc}'");
}

// REDO!
Console.WriteLine("\n--- REDO ---");
await historia.RedoAsync();
Console.WriteLine($"Stan: '{edytor.Tresc}'");
```

---

### 4. Iterator — sekwencyjny dostęp

csharp

```csharp
// Iterator — dostarcza sposób sekwencyjnego przechodzenia przez elementy
// bez ujawniania wewnętrznej struktury

// === WŁASNY ITERATOR — drzewo binarne ===
public class WezelDrzewa<T>
{
    public T           Wartosc { get; init; }
    public WezelDrzewa<T>? Lewy  { get; set; }
    public WezelDrzewa<T>? Prawy { get; set; }

    public WezelDrzewa(T wartosc) => Wartosc = wartosc;
}

// Drzewo binarne z różnymi strategiami przechodzenia
public class DrzewoBinarne<T> : IEnumerable<T>
{
    private WezelDrzewa<T>? _korzen;

    public DrzewoBinarne(WezelDrzewa<T>? korzen = null)
        => _korzen = korzen;

    public void Wstaw(T wartosc) where T : IComparable<T>
    {
        // uproszczone — w prawdziwym BST porównanie
        if (_korzen is null)
            _korzen = new WezelDrzewa<T>(wartosc);
    }

    // Domyślny iterator — InOrder (lewy, root, prawy)
    public IEnumerator<T> GetEnumerator()
        => PrzejdzInOrder(_korzen).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => GetEnumerator();

    // InOrder — sorted order for BST
    private IEnumerable<T> PrzejdzInOrder(WezelDrzewa<T>? wezel)
    {
        if (wezel is null) yield break;
        foreach (var el in PrzejdzInOrder(wezel.Lewy))  yield return el;
        yield return wezel.Wartosc;
        foreach (var el in PrzejdzInOrder(wezel.Prawy)) yield return el;
    }

    // PreOrder — root, lewy, prawy
    public IEnumerable<T> PreOrder()
        => PrzejdzPreOrder(_korzen);

    private IEnumerable<T> PrzejdzPreOrder(WezelDrzewa<T>? wezel)
    {
        if (wezel is null) yield break;
        yield return wezel.Wartosc;
        foreach (var el in PrzejdzPreOrder(wezel.Lewy))  yield return el;
        foreach (var el in PrzejdzPreOrder(wezel.Prawy)) yield return el;
    }

    // PostOrder — lewy, prawy, root
    public IEnumerable<T> PostOrder()
        => PrzejdzPostOrder(_korzen);

    private IEnumerable<T> PrzejdzPostOrder(WezelDrzewa<T>? wezel)
    {
        if (wezel is null) yield break;
        foreach (var el in PrzejdzPostOrder(wezel.Lewy))  yield return el;
        foreach (var el in PrzejdzPostOrder(wezel.Prawy)) yield return el;
        yield return wezel.Wartosc;
    }

    // BFS — poziomami (Breadth-First)
    public IEnumerable<T> BFS()
    {
        if (_korzen is null) yield break;

        var kolejka = new Queue<WezelDrzewa<T>>();
        kolejka.Enqueue(_korzen);

        while (kolejka.Any())
        {
            var wezel = kolejka.Dequeue();
            yield return wezel.Wartosc;

            if (wezel.Lewy  is not null) kolejka.Enqueue(wezel.Lewy);
            if (wezel.Prawy is not null) kolejka.Enqueue(wezel.Prawy);
        }
    }
}

// === YIELD RETURN — lazy collections ===
public class GeneratorDanych
{
    // Nieskończona sekwencja Fibonacci
    public static IEnumerable<long> Fibonacci()
    {
        long a = 0, b = 1;
        while (true)
        {
            yield return a;
            (a, b) = (b, a + b);
        }
    }

    // Stronicowanie z bazy — lazy loading
    public static async IAsyncEnumerable<List<Produkt>> StronicujZBazy(
        IProduktRepo repo,
        int rozmiarStrony = 100,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        int strona = 1;
        while (true)
        {
            var dane = await repo.PobierzStroneAsync(strona, rozmiarStrony, ct);
            if (!dane.Any()) yield break;

            yield return dane;
            if (dane.Count < rozmiarStrony) yield break;
            strona++;
        }
    }

    // Chunking — podziel kolekcję na kawałki
    public static IEnumerable<IEnumerable<T>> Chunki<T>(
        IEnumerable<T> kolekcja, int rozmiar)
    {
        T[] bufor = new T[rozmiar];
        int count = 0;

        foreach (var element in kolekcja)
        {
            bufor[count++] = element;
            if (count == rozmiar)
            {
                yield return bufor.Take(count).ToArray();
                count = 0;
            }
        }

        if (count > 0)
            yield return bufor.Take(count).ToArray();
    }
}

// === IAsyncEnumerable — async iterator ===
public class LogReader
{
    private readonly string _sciezka;

    public LogReader(string sciezka) => _sciezka = sciezka;

    public async IAsyncEnumerable<WpisLogu> CzytajLogi(
        DateTime? od = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        using var reader = new System.IO.StreamReader(_sciezka);

        string? linia;
        while ((linia = await reader.ReadLineAsync(ct)) is not null)
        {
            ct.ThrowIfCancellationRequested();

            var wpis = ParseujLinie(linia);
            if (wpis is null) continue;
            if (od.HasValue && wpis.Czas < od) continue;

            yield return wpis;
        }
    }

    private static WpisLogu? ParseujLinie(string linia)
    {
        // Format: [2024-03-15 10:30:00] [INFO] Komunikat
        if (!linia.StartsWith('[')) return null;

        try
        {
            var endBracket = linia.IndexOf(']');
            var dataStr    = linia[1..endBracket];
            var reszta     = linia[(endBracket + 3)..];  // pomiń "] ["
            var levelEnd   = reszta.IndexOf(']');
            var poziom     = reszta[..levelEnd];
            var komunikat  = reszta[(levelEnd + 2)..];

            return new WpisLogu(
                DateTime.Parse(dataStr),
                poziom,
                komunikat);
        }
        catch { return null; }
    }
}

public record WpisLogu(DateTime Czas, string Poziom, string Komunikat);

// === UŻYCIE ITERATORÓW ===

// Fibonacci — weź pierwsze 10
var fib = GeneratorDanych.Fibonacci()
    .Take(10)
    .ToList();
Console.WriteLine($"Fibonacci: {string.Join(", ", fib)}");
// 0, 1, 1, 2, 3, 5, 8, 13, 21, 34

// Chunki
int[] liczby = Enumerable.Range(1, 10).ToArray();
foreach (var chunk in GeneratorDanych.Chunki(liczby, 3))
    Console.WriteLine($"Chunk: [{string.Join(", ", chunk)}]");
// [1, 2, 3], [4, 5, 6], [7, 8, 9], [10]

// Drzewo
var drzewo = new DrzewoBinarne<int>(
    new WezelDrzewa<int>(4)
    {
        Lewy  = new(2) { Lewy = new(1), Prawy = new(3) },
        Prawy = new(6) { Lewy = new(5), Prawy = new(7) }
    });

Console.WriteLine($"InOrder:   {string.Join(" ", drzewo)}");
Console.WriteLine($"PreOrder:  {string.Join(" ", drzewo.PreOrder())}");
Console.WriteLine($"PostOrder: {string.Join(" ", drzewo.PostOrder())}");
Console.WriteLine($"BFS:       {string.Join(" ", drzewo.BFS())}");
// InOrder:   1 2 3 4 5 6 7
// PreOrder:  4 2 1 3 6 5 7
// PostOrder: 1 3 2 5 7 6 4
// BFS:       4 2 6 1 3 5 7

// Async iterator — logi
var reader = new LogReader("app.log");
await foreach (var wpis in reader.CzytajLogi(od: DateTime.Today))
    Console.WriteLine($"[{wpis.Poziom}] {wpis.Komunikat}");

// Stubs
public interface IProduktRepo
{
    Task<List<Produkt>> PobierzStroneAsync(int strona, int rozmiar, CancellationToken ct);
    Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default);
}
public record Produkt(int Id, string Nazwa, decimal Cena);
```

---

### 5. Porównanie wzorców behawioralnych

csharp

```csharp
// OBSERVER — reaktywna komunikacja
// Problem: "gdy X zmienia się, wielu musi wiedzieć"
// Przykłady: events, eventsourcing, real-time UI
// Kluczowe: nadawca NIE zna odbiorców — luźne powiązanie

// STRATEGY — wymienne algorytmy
// Problem: "wiele sposobów zrobienia tego samego"
// Przykłady: sortowanie, rabaty, walidacja, routing
// Kluczowe: algorytm wymieniany w runtime, klient nie zmienia się

// COMMAND — enkapsulacja operacji
// Problem: "operacja jako obiekt — kolejkuj, loguj, cofaj"
// Przykłady: UNDO/REDO, task queue, transakcje, batch
// Kluczowe: oddziela wywołującego od wykonywanego

// ITERATOR — sekwencyjny dostęp
// Problem: "przejdź przez kolekcję bez znajomości jej struktury"
// Przykłady: drzewa, grafy, lazy loading, streaming
// Kluczowe: yield return dla lazy evaluation

// .NET ma wbudowane implementacje:
// Observer  → IObservable<T>/IObserver<T>, events, Rx.NET
// Strategy  → Func<T>, delegates (strategie jako lambdy!)
// Command   → ICommand (WPF/MAUI), MediatR IRequest
// Iterator  → IEnumerable<T>, IAsyncEnumerable<T>, yield return
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między Observer a Event w C#?"** C# events to wbudowana implementacja Observer Pattern. `event EventHandler<T>` jest syntactic sugar nad delegatami: automatyczna enkapsulacja (nikt spoza klasy nie może wywołać eventu ani wyczyścić subskrybentów), thread-safe add/remove przez `+=`/`-=`. Observer z interfejsem daje więcej kontroli: można przekazywać kontekst do obserwatora przez DI, obserwator może być Scoped (jeden na request), łatwiej mockować w testach. W ASP.NET Core domenowe zdarzenia — interfejsy. W UI (WPF, MAUI) — events.

**"Kiedy Strategy zamiast if-else lub switch?"** Switch gdy: mała liczba stałych przypadków, rzadko dodawane nowe. Strategy gdy: wiele wariantów algorytmu (>3-4), często dodawane nowe bez modyfikacji kodu (OCP), algorytm zmienia się w runtime (kalkulator cen wybiera strategię per klient), każdy wariant ma nietrywialną logikę. Lambdy w C# to lekkie strategie — `Func<Koszyk, decimal>` zamiast interfejsu gdy logika prosta. Pełne klasy gdy strategie mają zależności i stan.

**"Co daje Command pattern — kiedy warto?"** Trzy główne zastosowania: (1) Undo/Redo — każda komenda wie jak się cofnąć, `IKomenda.CofnijAsync()`. (2) Kolejkowanie i harmonogramowanie — komenda to obiekt, można ją serializować, opóźniać, ponawiać. (3) Logowanie audytowe — każda zmiana to komenda z opisem, timestampem, userId. W ASP.NET Core MediatR używa Command pattern — `IRequest<T>` to komenda, `IRequestHandler<T>` to receiver. CQRS opiera się na Command i Query jako pierwszoklasowych obiektach.

**"Jaka różnica między IEnumerable a IAsyncEnumerable?"** `IEnumerable<T>` — synchroniczny iterator, blokuje wątek przy operacjach I/O. `IAsyncEnumerable<T>` — asynchroniczny, `await foreach`, każdy element może być awaitable. Idealny dla streamingu danych z bazy (EF Core `AsAsyncEnumerable()`), czytania pliku linia po linii, Kafka consumer, gRPC server streaming. `yield return` działa w obu — kompilator generuje state machine. `await foreach` + `IAsyncEnumerable` + `CancellationToken` = wzorzec backpressure.