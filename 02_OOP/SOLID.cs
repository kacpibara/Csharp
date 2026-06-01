namespace _02_OOP;

// ─────────────────────────────────────────────────────────────────────────────
// SOLID PRINCIPLES
// 5 zasad projektowania klas i modułów w OOP
// S — Single Responsibility, O — Open/Closed, L — Liskov Substitution
// I — Interface Segregation, D — Dependency Inversion
// ─────────────────────────────────────────────────────────────────────────────

public static class SOLIDPrinciples
{
    // S — SINGLE RESPONSIBILITY PRINCIPLE
    // "Klasa powinna mieć tylko jeden powód do zmiany."
    public static void SRP()
    {
        Console.WriteLine("\n=== S — SINGLE RESPONSIBILITY PRINCIPLE ===");
        Console.WriteLine("Klasa powinna mieć tylko jeden powód do zmiany.\n");

        Console.WriteLine("ŹLE — ZlyZarzadzaczUzytkownikow.Zarejestruj():");
        Console.WriteLine("  • waliduje email i hasło");
        Console.WriteLine("  • hashuje hasło (SHA256)");
        Console.WriteLine("  • zapisuje do bazy (INSERT)");
        Console.WriteLine("  • wysyła email powitalny");
        Console.WriteLine("  • loguje zdarzenie");
        Console.WriteLine("  → 5 powodów do zmiany = naruszenie SRP!\n");

        var walidator = new WalidatorUzyt();
        try { walidator.Waliduj("test@example.com", "haslo123"); Console.WriteLine("  WalidatorUzyt: OK"); }
        catch (ArgumentException ex) { Console.WriteLine($"  WalidatorUzyt: {ex.Message}"); }
        try { walidator.Waliduj("zly-email", "krótkie"); }
        catch (ArgumentException ex) { Console.WriteLine($"  WalidatorUzyt błąd: {ex.Message}"); }

        var hashing = new HashowanieSerwisSOLID();
        string hash = hashing.Hashuj("haslo123");
        Console.WriteLine($"  HashowanieSerwisSOLID: {hash[..16]}...");

        var repo = new InMemoryRepoUzytSOLID();
        repo.Dodaj("test@example.com", hash);
        Console.WriteLine("  InMemoryRepoUzytSOLID: dodano");

        Console.WriteLine("\nDOBRZE — RegistrationSerwis koordynuje bez implementowania szczegółów:");
        var serwisReg = new RegistrationSerwis(walidator, hashing, repo);
        serwisReg.Zarejestruj("user@domain.com", "silneHaslo1!");

        Console.WriteLine("\nTest SRP: ile powodów do zmiany ma klasa? > 1 → naruszasz SRP");
        Console.WriteLine("Symptomy: metody z setkami linii, klasy z dziesiątkami metod,");
        Console.WriteLine("  trudność w testowaniu bo trzeba mockować 5 zewnętrznych rzeczy");
    }

    // O — OPEN/CLOSED PRINCIPLE
    // "Klasa powinna być otwarta na rozszerzenia, zamknięta na modyfikacje."
    public static void OCP()
    {
        Console.WriteLine("\n=== O — OPEN/CLOSED PRINCIPLE ===");
        Console.WriteLine("Otwarta na rozszerzenia, zamknięta na modyfikacje.\n");

        Console.WriteLine("ŹLE — ZlyGeneratorRaportow:");
        Console.WriteLine("  if (format == \"PDF\") ... else if (format == \"CSV\") ...");
        Console.WriteLine("  Każdy nowy format = modyfikacja = ryzyko zepsucia istniejących\n");

        var formattery = new List<IFormatterSOLID>
        {
            new PdfFormatterSOLID(),
            new CsvFormatterSOLID(),
            new JsonFormatterSOLID(),
            new XmlFormatterSOLID()  // nowy format — zero zmian w GeneratorRaportowSOLID!
        };
        var generator = new GeneratorRaportowSOLID(formattery);

        string daneStr = "Laptop;3499.99";
        foreach (var f in formattery)
            Console.WriteLine($"  [{f.Format}] {generator.Generuj(daneStr, f.Format)}");

        Console.WriteLine("\nPolimorfizm jest MECHANIZMEM który umożliwia OCP.");
        Console.WriteLine("Nowy format = NOWA KLASA, zero zmian w GeneratorRaportowSOLID.");
        Console.WriteLine("OCP nie zabrania naprawy bugów — zabrania modyfikacji działającego kodu");
        Console.WriteLine("  tylko po to, żeby dodać nową funkcjonalność.");

        // Wzorzec z fabryki przez słownik (jak w PolymorphismAndSolid_OpenClosedPrinciple.cs)
        Console.WriteLine("\nEksporter z rejestrem (fabryka przez słownik):");
        var eksportery = new Dictionary<string, IFormatterSOLID>();
        foreach (var f in formattery) eksportery[f.Format.ToLower()] = f;
        Console.WriteLine($"  Dostępne formaty: {string.Join(", ", eksportery.Keys)}");
    }

    // L — LISKOV SUBSTITUTION PRINCIPLE
    // "Obiekty klas pochodnych muszą być wymienne z obiektami klas bazowych
    //  bez zepsucia programu."
    public static void LSP()
    {
        Console.WriteLine("\n=== L — LISKOV SUBSTITUTION PRINCIPLE ===");
        Console.WriteLine("Klasa pochodna zastępuje bazową bez zepsucia programu.\n");

        Console.WriteLine("ŹLE — klasyczny błąd: Prostokąt vs Kwadrat:");
        var zlyP = new ZlyProstokatSOLID();
        zlyP.Szerokosc = 4;
        zlyP.Wysokosc = 5;
        Console.WriteLine($"  ZlyProstokąt: {zlyP.Szerokosc}x{zlyP.Wysokosc} = pole {zlyP.Pole} (OK)");

        var zlyK = new ZlyKwadratSOLID();
        zlyK.Szerokosc = 4;
        zlyK.Wysokosc = 5;
        Console.WriteLine($"  ZlyKwadrat:   {zlyK.Szerokosc}x{zlyK.Wysokosc} = pole {zlyK.Pole} (oczekiwano 20 → BŁĄD!)");
        Console.WriteLine("  void TestujProstokat(ZlyProstokatSOLID p) {{ p.Szer=4; p.Wys=5; p.Pole==20; }}");
        Console.WriteLine("  TestujProstokat(new ZlyProstokatSOLID()) → True");
        Console.WriteLine("  TestujProstokat(new ZlyKwadratSOLID())   → False! NARUSZENIE LSP!\n");

        Console.WriteLine("DOBRZE — osobne klasy bez wadliwej hierarchii:");
        WyswietlKsztaltSOLID(new ProstokatSOLID(4, 5));
        WyswietlKsztaltSOLID(new KwadratSOLID(5));

        Console.WriteLine("\nNaruszenie LSP przez wyjątki:");
        Console.WriteLine("  RepozyBazowe.PobierzDane(id): rzuca dla id <= 0");
        Console.WriteLine("  ZleRepozytorium.PobierzDane(id): rzuca też dla id > 1000");
        Console.WriteLine("  → Wzmocnienie warunku wstępnego = naruszenie LSP!");

        Console.WriteLine("\nZasada LSP — klasa pochodna:");
        Console.WriteLine("  ✓ Nie wzmacnia warunków wstępnych (preconditions)");
        Console.WriteLine("  ✓ Nie osłabia warunków końcowych (postconditions)");
        Console.WriteLine("  ✓ Nie rzuca nowych wyjątków których bazowa nie rzuca");
        Console.WriteLine("  ✓ Zachowuje niezmienniki (invariants) klasy bazowej");
        Console.WriteLine("\nTest LSP: jeśli test przechodzi dla bazowej ale nie dla pochodnej → LSP złamane");
    }

    // I — INTERFACE SEGREGATION PRINCIPLE
    // "Klasy nie powinny być zmuszane do implementowania interfejsów których nie używają."
    public static void ISP()
    {
        Console.WriteLine("\n=== I — INTERFACE SEGREGATION PRINCIPLE ===");
        Console.WriteLine("Wiele małych interfejsów zamiast jednego grubego.\n");

        Console.WriteLine("ŹLE — IZlyPracownikSOLID (gruby interfejs):");
        Console.WriteLine("  void Pracuj(), Jedz(), Spi(), Zarzadzaj(), Programuje(), SprzedajeKlientom()");
        Console.WriteLine("  ZlyProgramistaSOLID.Zarzadzaj() → throw new NotImplementedException()");
        Console.WriteLine("  → Wymusza implementację metod których klasa nie potrzebuje!\n");

        Console.WriteLine("DOBRZE — małe, spójne interfejsy:");
        Console.WriteLine("  IPracownikSOLID: Imie, Pracuj()");
        Console.WriteLine("  IZarzadzajacySOLID: ProwadzSpotkanie(), OceniPracownika(), Zespol");
        Console.WriteLine("  IProgramistaSOLID: Programuje(jezyk), RobisCodeReview(kod), ZnaneJezyki");
        Console.WriteLine("  IKontaktZKlientemSOLID: SprzedajeKlientom(), ObslugaReklamacji()\n");

        var anna   = new ProgramistaSOLID("Anna");
        var bartek = new TeamLeadSOLID("Bartek");
        var celina = new HandlowiecSOLID("Celina");

        Console.WriteLine("IPracownikSOLID.Pracuj():");
        ZaprezentujZespolSOLID([anna, bartek, celina]);

        Console.WriteLine("\nIProgramistaSOLID.RobisCodeReview():");
        CodeReviewSOLID([anna, bartek], "var x = 42;");

        Console.WriteLine("\nIKontaktZKlientemSOLID.ObslugaReklamacji():");
        celina.ObslugaReklamacji("Zepsuty produkt");

        Console.WriteLine("\nKlasy implementują TYLKO to czego faktycznie potrzebują.");
        Console.WriteLine("Zmiana IProgramistaSOLID nie wpływa na HandlowiecSOLID — izolacja!");
    }

    // D — DEPENDENCY INVERSION PRINCIPLE
    // "Moduły wysokiego poziomu nie powinny zależeć od modułów niskiego poziomu.
    //  Oba powinny zależeć od abstrakcji."
    public static void DIP()
    {
        Console.WriteLine("\n=== D — DEPENDENCY INVERSION PRINCIPLE ===");
        Console.WriteLine("Zależności od interfejsów, wstrzykiwane z zewnątrz.\n");

        Console.WriteLine("ŹLE — ZlySerwisZamowienSOLID:");
        Console.WriteLine("  private readonly SqlServerBazaSOLID _baza = new SqlServerBazaSOLID();");
        Console.WriteLine("  private readonly SmtpEmailSOLID _email = new SmtpEmailSOLID();");
        Console.WriteLine("  → Tight coupling! Nie można przetestować bez prawdziwej bazy/emaila.");
        Console.WriteLine("  → Nie można zamienić SQL na MongoDB bez modyfikacji serwisu.\n");

        Console.WriteLine("DOBRZE — Dependency Injection przez konstruktor:");
        var serwis = new SerwisZamowienSOLID(
            repo:   new SqlRepoZamowienSOLID("Server=localhost"),
            email:  new SendGridSOLID("sg-key-xxx"),
            logger: new ConsoleLoggerSOLID());
        serwis.ZlozZamowienie("Laptop", 1);

        Console.WriteLine("\nNa potrzeby testów — proste mock obiekty bez frameworku:");
        var fakeEmail = new FakeEmailSOLID();
        var testSerwis = new SerwisZamowienSOLID(
            repo:   new InMemoryRepoZamowienSOLID(),
            email:  fakeEmail,
            logger: new ConsoleLoggerSOLID());
        testSerwis.ZlozZamowienie("Klawiatura", 2);
        Console.WriteLine($"  fakeEmail.WyslaneDo.Count = {fakeEmail.WyslaneDo.Count} — email bez SMTP");

        Console.WriteLine("\nSkład zależności (Composition Root) — tylko tutaj wiesz jakich konkretnych klas użyć.");
        Console.WriteLine("DIP a Dependency Injection:");
        Console.WriteLine("  DIP = zasada (abstrakcje zamiast konkretów)");
        Console.WriteLine("  DI  = wzorzec implementujący DIP (wstrzykujesz z zewnątrz)");
        Console.WriteLine("  IoC Container (np. wbudowany w ASP.NET Core) = framework automatyzujący DI");
    }

    private static void WyswietlKsztaltSOLID(KsztaltSOLID k)
        => Console.WriteLine($"  {k.GetType().Name}: pole={k.Pole}, obwód={k.Obwod}");

    private static void ZaprezentujZespolSOLID(IEnumerable<IPracownikSOLID> zespol)
    {
        foreach (var p in zespol) p.Pracuj();
    }

    private static void CodeReviewSOLID(IEnumerable<IProgramistaSOLID> programisci, string kod)
    {
        foreach (var p in programisci) p.RobisCodeReview(kod);
    }
}

// ─── S — SRP HELPERS ─────────────────────────────────────────────────────────

internal class WalidatorUzyt
{
    public void Waliduj(string email, string haslo)
    {
        if (!email.Contains('@'))
            throw new ArgumentException("Nieprawidłowy email");
        if (haslo.Length < 8)
            throw new ArgumentException("Hasło musi mieć min. 8 znaków");
    }
}

internal class HashowanieSerwisSOLID
{
    // Pełna kwalifikacja — System.Security.Cryptography nie jest w ImplicitUsings
    public string Hashuj(string haslo) =>
        Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(haslo)));
}

internal interface IRepoUzytSOLID
{
    void Dodaj(string email, string hashHasla);
}

internal class InMemoryRepoUzytSOLID : IRepoUzytSOLID
{
    private readonly List<(string Email, string Hash)> _dane = new();
    public void Dodaj(string email, string hash) => _dane.Add((email, hash));
}

internal class RegistrationSerwis
{
    private readonly WalidatorUzyt _walidator;
    private readonly HashowanieSerwisSOLID _hashing;
    private readonly IRepoUzytSOLID _repo;

    public RegistrationSerwis(WalidatorUzyt walidator, HashowanieSerwisSOLID hashing, IRepoUzytSOLID repo)
    {
        _walidator = walidator;
        _hashing = hashing;
        _repo = repo;
    }

    public void Zarejestruj(string emailAdres, string haslo)
    {
        _walidator.Waliduj(emailAdres, haslo);
        string hash = _hashing.Hashuj(haslo);
        _repo.Dodaj(emailAdres, hash);
        Console.WriteLine($"  RegistrationSerwis: zarejestrowano {emailAdres}");
    }
}

// ─── O — OCP HELPERS ─────────────────────────────────────────────────────────

internal interface IFormatterSOLID
{
    string Format { get; }
    string Formatuj(object dane);
}

internal class PdfFormatterSOLID : IFormatterSOLID
{
    public string Format => "PDF";
    public string Formatuj(object dane) => $"<PDF>{dane}</PDF>";
}

internal class CsvFormatterSOLID : IFormatterSOLID
{
    public string Format => "CSV";
    public string Formatuj(object dane) => $"col1,col2\n{dane}";
}

internal class JsonFormatterSOLID : IFormatterSOLID
{
    public string Format => "JSON";
    // Pełna kwalifikacja — System.Text.Json nie jest w ImplicitUsings
    public string Formatuj(object dane) =>
        System.Text.Json.JsonSerializer.Serialize(dane);
}

internal class XmlFormatterSOLID : IFormatterSOLID
{
    public string Format => "XML";
    public string Formatuj(object dane) => $"<root><dane>{dane}</dane></root>";
}

internal class GeneratorRaportowSOLID
{
    private readonly Dictionary<string, IFormatterSOLID> _formattery;

    public GeneratorRaportowSOLID(IEnumerable<IFormatterSOLID> formattery)
        => _formattery = formattery.ToDictionary(f => f.Format);

    public string Generuj(object dane, string format)
    {
        if (!_formattery.TryGetValue(format, out var formatter))
            throw new NotSupportedException($"Format {format} nieobsługiwany");
        return formatter.Formatuj(dane);
    }
}

// ─── L — LSP HELPERS ─────────────────────────────────────────────────────────

// Złe — naruszenie LSP
internal class ZlyProstokatSOLID
{
    public virtual int Szerokosc { get; set; }
    public virtual int Wysokosc  { get; set; }
    public int Pole => Szerokosc * Wysokosc;
}

internal class ZlyKwadratSOLID : ZlyProstokatSOLID
{
    // Kwadrat ustawia OBE wymiary — łamie kontrakt Prostokąta!
    public override int Szerokosc
    {
        get => base.Szerokosc;
        set { base.Szerokosc = value; base.Wysokosc = value; }
    }
    public override int Wysokosc
    {
        get => base.Wysokosc;
        set { base.Szerokosc = value; base.Wysokosc = value; }
    }
}

// Dobre — osobne klasy bez wadliwej hierarchii
internal abstract class KsztaltSOLID
{
    public abstract int Pole   { get; }
    public abstract int Obwod  { get; }
}

internal class ProstokatSOLID : KsztaltSOLID
{
    public int Szerokosc { get; }
    public int Wysokosc  { get; }
    public ProstokatSOLID(int szer, int wys) => (Szerokosc, Wysokosc) = (szer, wys);
    public override int Pole  => Szerokosc * Wysokosc;
    public override int Obwod => 2 * (Szerokosc + Wysokosc);
}

internal class KwadratSOLID : KsztaltSOLID
{
    public int Bok { get; }
    public KwadratSOLID(int bok) => Bok = bok;
    public override int Pole  => Bok * Bok;
    public override int Obwod => 4 * Bok;
}

// ─── I — ISP HELPERS ─────────────────────────────────────────────────────────

// Małe, spójne interfejsy
internal interface IPracownikSOLID
{
    string Imie { get; }
    void Pracuj();
}

internal interface IZarzadzajacySOLID
{
    void ProwadzSpotkanie();
    void OceniPracownika(IPracownikSOLID pracownik);
    IReadOnlyList<IPracownikSOLID> Zespol { get; }
}

internal interface IProgramistaSOLID
{
    void Programuje(string jezyk);
    void RobisCodeReview(string kod);
    IReadOnlyList<string> ZnaneJezyki { get; }
}

internal interface IKontaktZKlientemSOLID
{
    void SprzedajeKlientom();
    void ObslugaReklamacji(string reklamacja);
}

internal class ProgramistaSOLID : IPracownikSOLID, IProgramistaSOLID
{
    public string Imie { get; }
    public IReadOnlyList<string> ZnaneJezyki { get; } = ["C#", "Python", "TypeScript"];

    public ProgramistaSOLID(string imie) => Imie = imie;

    public void Pracuj() => Console.WriteLine($"  {Imie}: programuje");
    public void Programuje(string jezyk) => Console.WriteLine($"  {Imie}: koduje w {jezyk}");
    public void RobisCodeReview(string kod) => Console.WriteLine($"  {Imie}: reviewuje '{kod}'");
}

internal class TeamLeadSOLID : IPracownikSOLID, IProgramistaSOLID, IZarzadzajacySOLID
{
    private readonly List<IPracownikSOLID> _zespol = new();
    public string Imie { get; }
    public IReadOnlyList<string> ZnaneJezyki { get; } = ["C#", "Java"];
    public IReadOnlyList<IPracownikSOLID> Zespol => _zespol.AsReadOnly();

    public TeamLeadSOLID(string imie) => Imie = imie;

    public void Pracuj()  => Console.WriteLine($"  {Imie}: zarządza i koduje");
    public void Programuje(string jezyk) => Console.WriteLine($"  {Imie}: koduje w {jezyk}");
    public void RobisCodeReview(string kod) => Console.WriteLine($"  {Imie}: robi szczegółowy review '{kod}'");
    public void ProwadzSpotkanie() => Console.WriteLine($"  {Imie}: prowadzi daily");
    public void OceniPracownika(IPracownikSOLID p) => Console.WriteLine($"  {Imie}: ocenia {p.Imie}");
    public void DodajDoZespolu(IPracownikSOLID p) => _zespol.Add(p);
}

internal class HandlowiecSOLID : IPracownikSOLID, IKontaktZKlientemSOLID
{
    public string Imie { get; }

    public HandlowiecSOLID(string imie) => Imie = imie;

    public void Pracuj() => Console.WriteLine($"  {Imie}: sprzedaje");
    public void SprzedajeKlientom() => Console.WriteLine($"  {Imie}: pozyskuje klientów");
    public void ObslugaReklamacji(string reklamacja) => Console.WriteLine($"  {Imie}: obsługuje: {reklamacja}");
}

// ─── D — DIP HELPERS ─────────────────────────────────────────────────────────

internal record ZamowienieSOLID(int Id, string Produkt, int Ilosc, DateTime Data);

internal interface IRepozytoriumZamowienSOLID
{
    void Zapisz(ZamowienieSOLID zamowienie);
    IReadOnlyList<ZamowienieSOLID> PobierzWszystkie();
}

internal interface ISerwisEmailowySOLID
{
    void Wyslij(string do_, string temat, string tresc);
}

internal interface ILoggerSOLID
{
    void Info(string msg);
    void Blad(string msg, Exception? ex = null);
}

// Implementacje niskiego poziomu
internal class SqlRepoZamowienSOLID : IRepozytoriumZamowienSOLID
{
    private readonly string _connStr;
    public SqlRepoZamowienSOLID(string connStr) => _connStr = connStr;
    public void Zapisz(ZamowienieSOLID z) => Console.WriteLine($"  [SQL] INSERT zamowienie: {z.Produkt} x{z.Ilosc}");
    public IReadOnlyList<ZamowienieSOLID> PobierzWszystkie() => Array.Empty<ZamowienieSOLID>();
}

internal class SendGridSOLID : ISerwisEmailowySOLID
{
    private readonly string _apiKey;
    public SendGridSOLID(string apiKey) => _apiKey = apiKey;
    public void Wyslij(string do_, string temat, string tresc)
        => Console.WriteLine($"  [SendGrid] Email do {do_}: {temat}");
}

internal class ConsoleLoggerSOLID : ILoggerSOLID
{
    public void Info(string msg) => Console.WriteLine($"  [INFO]  {msg}");
    public void Blad(string msg, Exception? ex = null)
        => Console.WriteLine($"  [ERROR] {msg} {ex?.Message}");
}

// Moduł wysokiego poziomu — zależy TYLKO od interfejsów
internal class SerwisZamowienSOLID
{
    private readonly IRepozytoriumZamowienSOLID _repo;
    private readonly ISerwisEmailowySOLID _email;
    private readonly ILoggerSOLID _logger;

    public SerwisZamowienSOLID(
        IRepozytoriumZamowienSOLID repo,
        ISerwisEmailowySOLID email,
        ILoggerSOLID logger)
    {
        _repo = repo;
        _email = email;
        _logger = logger;
    }

    public void ZlozZamowienie(string produkt, int ilosc)
    {
        _logger.Info($"Składanie zamówienia: {produkt} x{ilosc}");
        var zam = new ZamowienieSOLID(
            Id: new Random().Next(1000, 9999),
            Produkt: produkt,
            Ilosc: ilosc,
            Data: DateTime.Now);
        _repo.Zapisz(zam);
        _email.Wyslij("klient@example.com", "Potwierdzenie zamówienia", $"Zamówiono: {produkt} x{ilosc}");
        _logger.Info($"Zamówienie #{zam.Id} złożone pomyślnie");
    }
}

// Fake/mock na potrzeby testów — bez frameworku
internal class InMemoryRepoZamowienSOLID : IRepozytoriumZamowienSOLID
{
    private readonly List<ZamowienieSOLID> _dane = new();
    public void Zapisz(ZamowienieSOLID z)
    {
        _dane.Add(z);
        Console.WriteLine($"  [InMemory] zamówienie: {z.Produkt} x{z.Ilosc}");
    }
    public IReadOnlyList<ZamowienieSOLID> PobierzWszystkie() => _dane.AsReadOnly();
}

internal class FakeEmailSOLID : ISerwisEmailowySOLID
{
    public List<string> WyslaneDo { get; } = new();
    public void Wyslij(string do_, string temat, string tresc)
    {
        WyslaneDo.Add(do_);
        Console.WriteLine($"  [FakeEmail] 'wysłano' do {do_}");
    }
}
