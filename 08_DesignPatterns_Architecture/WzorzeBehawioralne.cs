using System.Collections.ObjectModel;

namespace _08_DesignPatterns_Architecture;

// ============================================================
// WZORCE BEHAWIORALNE — Observer, Strategy, Command, Iterator
// ============================================================

// ============================================================
// 1. OBSERVER — reaguj na zdarzenia
// ============================================================
// "Jeden emituje → wielu reaguje" — luźne powiązanie

public interface IObserwowalne<T>
{
    void Subskrybuj(IObserwator<T> obs);
    void Anuluj(IObserwator<T> obs);
    void Powiadom(T zdarzenie);
}

public interface IObserwator<T>
{
    void Aktualizuj(T zdarzenie);
}

// Zdarzenia domenowe (hierarchia)
public abstract record ZdarzenieSklepu
{
    public DateTime CzasZdarzenia { get; init; } = DateTime.UtcNow;
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
    int    ProduktId,
    string NazwaProduktu,
    int    AktualnyStan,
    int    MinimalnyProg) : ZdarzenieSklepu;

// Interfejsy serwisów obserwatorów
public interface IEmailSerwis { Task WyslijAsync(string to, string body); }
public interface IInwentarzSerwis { void ZmniejszStan(int id, int ilosc); }

// Stubs
public class MockEmailSerwis : IEmailSerwis
{
    public Task WyslijAsync(string to, string body)
    {
        Console.WriteLine($"  [Email→{to}] {body[..Math.Min(50, body.Length)]}");
        return Task.CompletedTask;
    }
}
public class MockInwentarzSerwis : IInwentarzSerwis
{
    public void ZmniejszStan(int id, int ilosc)
        => Console.WriteLine($"  [Inwentarz] #{id} -= {ilosc}");
}

// Hub (Subject) — emituje zdarzenia
public class SklepZdarzeniaHub : IObserwowalne<ZdarzenieSklepu>
{
    private readonly List<IObserwator<ZdarzenieSklepu>> _obs = new();

    public void Subskrybuj(IObserwator<ZdarzenieSklepu> obs)  { _obs.Add(obs);    Console.WriteLine($"  [Hub] +{obs.GetType().Name}"); }
    public void Anuluj(IObserwator<ZdarzenieSklepu> obs)      { _obs.Remove(obs); }
    public void Powiadom(ZdarzenieSklepu zdarzenie)
    {
        Console.WriteLine($"  [Hub] Powiadam o: {zdarzenie.GetType().Name}");
        foreach (var obs in _obs.ToList())
            try { obs.Aktualizuj(zdarzenie); }
            catch (Exception ex) { Console.WriteLine($"  [Hub] Błąd {obs.GetType().Name}: {ex.Message}"); }
    }

    public void Publikuj<T>(T zdarzenie) where T : ZdarzenieSklepu => Powiadom(zdarzenie);
}

// Obserwatory
public class EmailPowiadomieniaObserwator : IObserwator<ZdarzenieSklepu>
{
    private readonly IEmailSerwis _email;
    public EmailPowiadomieniaObserwator(IEmailSerwis email) => _email = email;

    public void Aktualizuj(ZdarzenieSklepu zdarzenie)
    {
        switch (zdarzenie)
        {
            case ZamowienieZlozone z:
                _email.WyslijAsync($"klient_{z.KlientId}@test.pl",
                    $"Zamówienie #{z.ZamowienieId} przyjęte — {z.Kwota:C}");
                break;
            case ZamowienieOplacone op:
                _email.WyslijAsync("klient@test.pl",
                    $"Płatność {op.Kwota:C} — transakcja {op.TransakcjaId}");
                break;
        }
    }
}

public class InwentarzObserwator : IObserwator<ZdarzenieSklepu>
{
    private readonly IInwentarzSerwis _inwentarz;
    public InwentarzObserwator(IInwentarzSerwis inwentarz) => _inwentarz = inwentarz;

    public void Aktualizuj(ZdarzenieSklepu zdarzenie)
    {
        if (zdarzenie is ZamowienieZlozone z)
            foreach (int pid in z.ProduktIds)
                _inwentarz.ZmniejszStan(pid, 1);
    }
}

public class AlertMagazynowyObserwator : IObserwator<ZdarzenieSklepu>
{
    private readonly IEmailSerwis _email;
    public AlertMagazynowyObserwator(IEmailSerwis email) => _email = email;

    public void Aktualizuj(ZdarzenieSklepu zdarzenie)
    {
        if (zdarzenie is StanMagazynuNiski a)
        {
            Console.WriteLine($"  [ALERT] {a.NazwaProduktu}: stan {a.AktualnyStan}/{a.MinimalnyProg}");
            _email.WyslijAsync("magazyn@sklep.pl", $"ALERT: Niski stan {a.NazwaProduktu}!");
        }
    }
}

public class AnalitykiObserwator : IObserwator<ZdarzenieSklepu>
{
    private int _liczba = 0;
    private decimal _suma = 0;

    public void Aktualizuj(ZdarzenieSklepu zdarzenie)
    {
        if (zdarzenie is ZamowienieZlozone z)
        {
            _liczba++;
            _suma += z.Kwota;
            Console.WriteLine($"  [Analityki] Zamówień: {_liczba}, Suma: {_suma:C}");
        }
    }
}

// Observer z C# events (alternatywa)
public class ZamowieniaSerwisZEventami
{
    public event EventHandler<ZamowienieZlozone>?  OnZamowienieZlozone;
    public event EventHandler<ZamowienieOplacone>? OnZamowienieOplacone;

    public async Task<int> ZlozZamowienieAsync(int klientId, decimal kwota, List<int> produktIds)
    {
        int id = new Random().Next(1000, 9999);
        await Task.Delay(10);
        OnZamowienieZlozone?.Invoke(this, new ZamowienieZlozone(id, klientId, kwota, produktIds));
        return id;
    }

    public async Task OplacZamowienieAsync(int zamId, decimal kwota)
    {
        await Task.Delay(10);
        OnZamowienieOplacone?.Invoke(this, new ZamowienieOplacone(zamId, "TXN-" + zamId, kwota));
    }
}

// ============================================================
// 2. STRATEGY — wymienne algorytmy
// ============================================================
// "Algorytm jako obiekt" — wymiana w runtime bez modyfikacji kontekstu

public interface IStrategiaRabatu
{
    decimal ObliczRabat(KoszykZakupow koszyk);
    string Nazwa { get; }
}

public class KoszykZakupow
{
    public List<ElementKoszyka> Elementy       { get; init; } = new();
    public int     KlientId                   { get; init; }
    public bool    JestCzlonkiem              { get; init; }
    public bool    PierwszeZamow              { get; init; }
    public string? KodPromocyjny              { get; init; }
    public DateTime DataZakupu                { get; init; } = DateTime.UtcNow;
    public decimal SumaCalkowita => Elementy.Sum(e => e.Cena * e.Ilosc);
}

public record ElementKoszyka(int ProduktId, string Nazwa, decimal Cena, int Ilosc, string Kategoria = "");

// Strategie rabatów
public class BrakRabatu : IStrategiaRabatu
{
    public string Nazwa => "Brak rabatu";
    public decimal ObliczRabat(KoszykZakupow k) => 0m;
}

public class RabatProcentowy : IStrategiaRabatu
{
    private readonly decimal _p;
    public RabatProcentowy(decimal p) => _p = p;
    public string Nazwa => $"Rabat {_p}%";
    public decimal ObliczRabat(KoszykZakupow k)
    {
        decimal r = k.SumaCalkowita * _p / 100;
        Console.WriteLine($"  [Rabat] {_p}% od {k.SumaCalkowita:C} = {r:C}");
        return r;
    }
}

public class RabatStalegoKlienta : IStrategiaRabatu
{
    public string Nazwa => "Rabat stałego klienta";
    public decimal ObliczRabat(KoszykZakupow k)
    {
        if (!k.JestCzlonkiem) return 0m;
        decimal r = k.SumaCalkowita switch
        {
            >= 1000m => k.SumaCalkowita * 0.15m,
            >= 500m  => k.SumaCalkowita * 0.10m,
            >= 200m  => k.SumaCalkowita * 0.05m,
            _        => 0m
        };
        Console.WriteLine($"  [Rabat] Stały klient: {r:C}");
        return r;
    }
}

public class RabatPierwszegoZamowienia : IStrategiaRabatu
{
    public string Nazwa => "Rabat pierwszego zamówienia (20%)";
    public decimal ObliczRabat(KoszykZakupow k)
    {
        if (!k.PierwszeZamow) return 0m;
        decimal r = k.SumaCalkowita * 0.20m;
        Console.WriteLine($"  [Rabat] Pierwsze zamówienie: {r:C}");
        return r;
    }
}

public class RabatSezonowy : IStrategiaRabatu
{
    public string Nazwa => "Rabat sezonowy";
    public decimal ObliczRabat(KoszykZakupow k)
    {
        if (k.DataZakupu.Month != 12) return 0m;
        decimal r = k.SumaCalkowita * 0.12m;
        Console.WriteLine($"  [Rabat] Świąteczny 12%: {r:C}");
        return r;
    }
}

public class RabatKoduPromocyjnego : IStrategiaRabatu
{
    private readonly Dictionary<string, decimal> _kody = new()
        { ["SAVE10"] = 10m, ["SUMMER20"] = 20m, ["VIP50"] = 50m };

    public string Nazwa => "Kod promocyjny";
    public decimal ObliczRabat(KoszykZakupow k)
    {
        if (k.KodPromocyjny is null) return 0m;
        if (!_kody.TryGetValue(k.KodPromocyjny.ToUpper(), out decimal p)) return 0m;
        decimal r = k.SumaCalkowita * p / 100;
        Console.WriteLine($"  [Rabat] Kod '{k.KodPromocyjny}' {p}% = {r:C}");
        return r;
    }
}

// Context
public class KalkulatorCeny
{
    private IStrategiaRabatu _strategia;
    public KalkulatorCeny(IStrategiaRabatu? s = null) => _strategia = s ?? new BrakRabatu();

    public void ZmienStrategie(IStrategiaRabatu s)
    {
        Console.WriteLine($"  [Strategia] {_strategia.Nazwa} → {s.Nazwa}");
        _strategia = s;
    }

    public WynikKalkulacji Oblicz(KoszykZakupow k)
    {
        decimal suma  = k.SumaCalkowita;
        decimal rabat = _strategia.ObliczRabat(k);
        return new(suma, rabat, suma - rabat, suma > 0 ? rabat / suma * 100 : 0, _strategia.Nazwa);
    }
}

public record WynikKalkulacji(decimal Suma, decimal Rabat, decimal DoZaplaty,
    decimal ProcentRabatu, string NazwaStrategii);

// Selector — najlepszy rabat automatycznie
public class SelectorStrategii
{
    private readonly List<IStrategiaRabatu> _strategie = new()
    {
        new RabatPierwszegoZamowienia(),
        new RabatKoduPromocyjnego(),
        new RabatStalegoKlienta(),
        new RabatSezonowy(),
        new RabatProcentowy(5)
    };

    public IStrategiaRabatu WybierzNajlepszaDla(KoszykZakupow k)
        => _strategie.MaxBy(s => s.ObliczRabat(k)) ?? new BrakRabatu();
}

// ============================================================
// 3. COMMAND — enkapsulacja operacji
// ============================================================
// "Operacja jako obiekt" — kolejkowanie, UNDO/REDO, audyt

public interface IKomenda
{
    Task WykonajAsync(CancellationToken ct = default);
    Task CofnijAsync(CancellationToken ct = default);
    string Opis { get; }
}

// Receiver — faktyczne operacje
public class EdytorDokumentu
{
    private string _tresc;
    public string Tresc => _tresc;
    public EdytorDokumentu(string tresc = "") => _tresc = tresc;

    public void WstawTekst(int poz, string tekst)
    {
        _tresc = _tresc.Insert(poz, tekst);
        Console.WriteLine($"  [Edytor] Wstawiono '{tekst}' na poz. {poz}");
    }

    public void UsunTekst(int poz, int len)
    {
        string usunieto = _tresc.Substring(poz, len);
        _tresc = _tresc.Remove(poz, len);
        Console.WriteLine($"  [Edytor] Usunięto '{usunieto}'");
    }
}

// Komendy
public class KomendaWstawTekst : IKomenda
{
    private readonly EdytorDokumentu _ed;
    private readonly int _poz;
    private readonly string _tekst;
    public string Opis => $"Wstaw '{_tekst}' na poz. {_poz}";

    public KomendaWstawTekst(EdytorDokumentu ed, int poz, string tekst)
    {
        _ed = ed; _poz = poz; _tekst = tekst;
    }

    public Task WykonajAsync(CancellationToken ct = default) { _ed.WstawTekst(_poz, _tekst); return Task.CompletedTask; }
    public Task CofnijAsync(CancellationToken ct = default)  { _ed.UsunTekst(_poz, _tekst.Length); return Task.CompletedTask; }
}

public class KomendaUsunTekst : IKomenda
{
    private readonly EdytorDokumentu _ed;
    private readonly int _poz, _len;
    private string _usunieto = "";
    public string Opis => $"Usuń {_len} znaków z poz. {_poz}";

    public KomendaUsunTekst(EdytorDokumentu ed, int poz, int len) { _ed = ed; _poz = poz; _len = len; }

    public Task WykonajAsync(CancellationToken ct = default)
    {
        _usunieto = _ed.Tresc.Substring(_poz, Math.Min(_len, _ed.Tresc.Length - _poz));
        _ed.UsunTekst(_poz, _usunieto.Length);
        return Task.CompletedTask;
    }
    public Task CofnijAsync(CancellationToken ct = default) { _ed.WstawTekst(_poz, _usunieto); return Task.CompletedTask; }
}

// Invoker — zarządza historią (UNDO/REDO)
public class HistoriaKomend
{
    private readonly Stack<IKomenda> _wykonane = new();
    private readonly Stack<IKomenda> _cofniete = new();

    public bool MoznaUndo => _wykonane.Any();
    public bool MoznaRedo => _cofniete.Any();
    public IReadOnlyList<string> HistoriaOpisy => _wykonane.Select(k => k.Opis).Reverse().ToList();

    public async Task WykonajAsync(IKomenda komenda, CancellationToken ct = default)
    {
        await komenda.WykonajAsync(ct);
        _wykonane.Push(komenda);
        _cofniete.Clear();
    }

    public async Task<bool> UndoAsync(CancellationToken ct = default)
    {
        if (!MoznaUndo) return false;
        var k = _wykonane.Pop();
        await k.CofnijAsync(ct);
        _cofniete.Push(k);
        Console.WriteLine($"  [Undo] {k.Opis}");
        return true;
    }

    public async Task<bool> RedoAsync(CancellationToken ct = default)
    {
        if (!MoznaRedo) return false;
        var k = _cofniete.Pop();
        await k.WykonajAsync(ct);
        _wykonane.Push(k);
        Console.WriteLine($"  [Redo] {k.Opis}");
        return true;
    }
}

// Makro — kompozyt komend
public class MacroKomenda : IKomenda
{
    private readonly List<IKomenda> _komendy;
    public string Opis => $"Makro [{string.Join(", ", _komendy.Select(k => k.Opis))}]";
    public MacroKomenda(params IKomenda[] komendy) => _komendy = komendy.ToList();

    public async Task WykonajAsync(CancellationToken ct = default)
    {
        foreach (var k in _komendy) await k.WykonajAsync(ct);
    }
    public async Task CofnijAsync(CancellationToken ct = default)
    {
        foreach (var k in Enumerable.Reverse(_komendy)) await k.CofnijAsync(ct);
    }
}

// ============================================================
// 4. ITERATOR — sekwencyjny dostęp
// ============================================================
// Ujawnia elementy sekwencji bez odkrywania wewnętrznej struktury

// Drzewo binarne z różnymi strategiami przechodzenia
public class WezelDrzewa<T>
{
    public T           Wartosc { get; init; }
    public WezelDrzewa<T>? Lewy  { get; set; }
    public WezelDrzewa<T>? Prawy { get; set; }
    public WezelDrzewa(T wartosc) => Wartosc = wartosc;
}

public class DrzewoBinarne<T> : IEnumerable<T>
{
    private readonly WezelDrzewa<T>? _korzen;
    public DrzewoBinarne(WezelDrzewa<T>? korzen = null) => _korzen = korzen;

    // InOrder — domyślny iterator (lewy, root, prawy)
    public IEnumerator<T> GetEnumerator() => PrzejdzInOrder(_korzen).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<T> PrzejdzInOrder(WezelDrzewa<T>? w)
    {
        if (w is null) yield break;
        foreach (var el in PrzejdzInOrder(w.Lewy))  yield return el;
        yield return w.Wartosc;
        foreach (var el in PrzejdzInOrder(w.Prawy)) yield return el;
    }

    // PreOrder — root, lewy, prawy
    public IEnumerable<T> PreOrder() => PrzejdzPreOrder(_korzen);
    private IEnumerable<T> PrzejdzPreOrder(WezelDrzewa<T>? w)
    {
        if (w is null) yield break;
        yield return w.Wartosc;
        foreach (var el in PrzejdzPreOrder(w.Lewy))  yield return el;
        foreach (var el in PrzejdzPreOrder(w.Prawy)) yield return el;
    }

    // PostOrder — lewy, prawy, root
    public IEnumerable<T> PostOrder() => PrzejdzPostOrder(_korzen);
    private IEnumerable<T> PrzejdzPostOrder(WezelDrzewa<T>? w)
    {
        if (w is null) yield break;
        foreach (var el in PrzejdzPostOrder(w.Lewy))  yield return el;
        foreach (var el in PrzejdzPostOrder(w.Prawy)) yield return el;
        yield return w.Wartosc;
    }

    // BFS — poziomami (Breadth-First Search)
    public IEnumerable<T> BFS()
    {
        if (_korzen is null) yield break;
        var kolejka = new Queue<WezelDrzewa<T>>();
        kolejka.Enqueue(_korzen);
        while (kolejka.Any())
        {
            var w = kolejka.Dequeue();
            yield return w.Wartosc;
            if (w.Lewy  is not null) kolejka.Enqueue(w.Lewy);
            if (w.Prawy is not null) kolejka.Enqueue(w.Prawy);
        }
    }
}

// Generatory z yield return — lazy evaluation
public static class GeneratorDanych
{
    // Nieskończona sekwencja Fibonacci — lazy!
    public static IEnumerable<long> Fibonacci()
    {
        long a = 0, b = 1;
        while (true)
        {
            yield return a;
            (a, b) = (b, a + b);
        }
    }

    // Chunki — podziel kolekcję na kawałki
    public static IEnumerable<IEnumerable<T>> Chunki<T>(IEnumerable<T> kolekcja, int rozmiar)
    {
        var bufor = new T[rozmiar];
        int count = 0;
        foreach (var el in kolekcja)
        {
            bufor[count++] = el;
            if (count == rozmiar) { yield return bufor.Take(count).ToArray(); count = 0; }
        }
        if (count > 0) yield return bufor.Take(count).ToArray();
    }

    // IAsyncEnumerable — async streaming
    public static async IAsyncEnumerable<int> GenerujLiczbLazy(
        int ile,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 1; i <= ile; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(1, ct);
            yield return i;
        }
    }
}

// ============================================================
// RUNNER
// ============================================================

public static class WzorzeBehawioralneDemo
{
    public static async Task Uruchom()
    {
        // --- OBSERVER ---
        Console.WriteLine("  [Observer] Jeden emituje → wielu reaguje (luźne powiązanie)");
        var hub      = new SklepZdarzeniaHub();
        var emailSrv = new MockEmailSerwis();

        hub.Subskrybuj(new EmailPowiadomieniaObserwator(emailSrv));
        hub.Subskrybuj(new InwentarzObserwator(new MockInwentarzSerwis()));
        hub.Subskrybuj(new AnalitykiObserwator());
        hub.Subskrybuj(new AlertMagazynowyObserwator(emailSrv));

        hub.Publikuj(new ZamowienieZlozone(1001, 42, 3500m, new() { 1, 2 }));
        hub.Publikuj(new ZamowienieOplacone(1001, "TXN-ABC123", 3500m));
        hub.Publikuj(new StanMagazynuNiski(1, "Laptop", 2, 5));

        // C# events jako alternatywa Observer
        var serwisEvents = new ZamowieniaSerwisZEventami();
        serwisEvents.OnZamowienieZlozone += (_, e) =>
            Console.WriteLine($"  [Event] ZamowienieZlozone #{e.ZamowienieId} — {e.Kwota:C}");
        await serwisEvents.ZlozZamowienieAsync(42, 1200m, new() { 1, 2, 3 });

        // --- STRATEGY ---
        Console.WriteLine("  [Strategy] Algorytm wymienialny w runtime");
        var koszyk = new KoszykZakupow
        {
            KlientId = 42, JestCzlonkiem = true, PierwszeZamow = false,
            KodPromocyjny = "SAVE10",
            Elementy = new()
            {
                new(1, "Laptop",    3500m, 1, "IT"),
                new(2, "Mysz",       150m, 2, "IT"),
                new(3, "Klawiatura", 250m, 1, "IT")
            }
        };

        var kalk = new KalkulatorCeny();
        kalk.ZmienStrategie(new RabatStalegoKlienta());
        var w1 = kalk.Oblicz(koszyk);
        Console.WriteLine($"  [Strategy] Stały klient: {w1.DoZaplaty:C} (rabat {w1.Rabat:C})");

        kalk.ZmienStrategie(new RabatKoduPromocyjnego());
        var w2 = kalk.Oblicz(koszyk);
        Console.WriteLine($"  [Strategy] Kod SAVE10: {w2.DoZaplaty:C} (rabat {w2.Rabat:C})");

        var selector   = new SelectorStrategii();
        var najlepsza  = selector.WybierzNajlepszaDla(koszyk);
        kalk.ZmienStrategie(najlepsza);
        var wFin = kalk.Oblicz(koszyk);
        Console.WriteLine($"  [Strategy] Najlepsza '{wFin.NazwaStrategii}': {wFin.DoZaplaty:C}");

        // --- COMMAND ---
        Console.WriteLine("  [Command] Operacja jako obiekt — UNDO/REDO");
        var edytor   = new EdytorDokumentu("Witaj świecie!");
        var historia = new HistoriaKomend();

        Console.WriteLine($"  [Command] Start: '{edytor.Tresc}'");
        await historia.WykonajAsync(new KomendaWstawTekst(edytor, 6, " piękny"));
        Console.WriteLine($"  [Command] Po wstawieniu: '{edytor.Tresc}'");

        await historia.WykonajAsync(new KomendaUsunTekst(edytor, 0, 5));
        Console.WriteLine($"  [Command] Po usunięciu: '{edytor.Tresc}'");

        // Makro komenda
        var makro = new MacroKomenda(
            new KomendaWstawTekst(edytor, 0, ">>> "),
            new KomendaWstawTekst(edytor, edytor.Tresc.Length + 4, " <<<"));
        await historia.WykonajAsync(makro);
        Console.WriteLine($"  [Command] Po makro: '{edytor.Tresc}'");

        Console.WriteLine("  [Command] UNDO:");
        while (historia.MoznaUndo) { await historia.UndoAsync(); Console.WriteLine($"    Stan: '{edytor.Tresc}'"); }

        Console.WriteLine("  [Command] REDO:");
        await historia.RedoAsync();
        Console.WriteLine($"    Stan: '{edytor.Tresc}'");

        // --- ITERATOR ---
        Console.WriteLine("  [Iterator] Drzewo binarne — InOrder/PreOrder/PostOrder/BFS");
        var drzewo = new DrzewoBinarne<int>(
            new WezelDrzewa<int>(4)
            {
                Lewy  = new(2) { Lewy = new(1), Prawy = new(3) },
                Prawy = new(6) { Lewy = new(5), Prawy = new(7) }
            });

        Console.WriteLine($"  [Iterator] InOrder:   {string.Join(" ", drzewo)}");
        Console.WriteLine($"  [Iterator] PreOrder:  {string.Join(" ", drzewo.PreOrder())}");
        Console.WriteLine($"  [Iterator] PostOrder: {string.Join(" ", drzewo.PostOrder())}");
        Console.WriteLine($"  [Iterator] BFS:       {string.Join(" ", drzewo.BFS())}");

        // Fibonacci — nieskończona sekwencja, weź 10
        var fib = GeneratorDanych.Fibonacci().Take(10).ToList();
        Console.WriteLine($"  [Iterator] Fibonacci: {string.Join(", ", fib)}");

        // Chunki
        foreach (var chunk in GeneratorDanych.Chunki(Enumerable.Range(1, 10), 3))
            Console.WriteLine($"  [Iterator] Chunk: [{string.Join(", ", chunk)}]");

        // IAsyncEnumerable — async streaming
        Console.WriteLine("  [Iterator] IAsyncEnumerable (async foreach):");
        var sumaAsync = 0;
        await foreach (var n in GeneratorDanych.GenerujLiczbLazy(5))
            sumaAsync += n;
        Console.WriteLine($"  [Iterator] Suma 1..5 (async): {sumaAsync}");

        Console.WriteLine("  [Porównanie] Observer=reaktywny | Strategy=algorytm | Command=UNDO | Iterator=yield");
    }
}
