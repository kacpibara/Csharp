namespace _03_Csharp_4_5;

// ── Klasy pomocnicze ──────────────────────────────────────────────────────────

public class KonfiguracjaEmailON
{
    public string? SmtpHost    { get; set; }
    public int     Port        { get; set; }
    public bool    UseSsl      { get; set; }
    public int     Timeout     { get; set; }
    public int     MaxRetry    { get; set; }
}

public static class OptionalNamed
{
    // Pomocnicze — demonstrują rozwiązanie przeciążeń z opcjonalnymi
    private static string OpisInt(int x)              => $"int: {x}";
    private static string OpisIntStr(int x, string s = "def") => $"int+str: {x}, {s}";

    // ── 1. Parametry opcjonalne ───────────────────────────────────────────────

    public static void ParametryOpcjonalne()
    {
        Console.WriteLine("\n── ParametryOpcjonalne ──");

        // Parametry opcjonalne — muszą być COMPILE-TIME constants
        // Dozwolone: literały (0, "txt", true), const, enum, null, default(T)
        // NIEDOZWOLONE: new List<>(), DateTime.Now (nie są compile-time)

        static string FormatujWiadomość(
            string tekst,
            string prefix     = "[INFO]",   // literał string
            bool   uppercase  = false,       // literał bool
            int    maxLength  = 200,         // literał int
            char   separator  = ' ')         // literał char
        {
            string wynik = uppercase ? tekst.ToUpper() : tekst;
            if (wynik.Length > maxLength) wynik = wynik[..maxLength] + "...";
            return $"{prefix}{separator}{wynik}";
        }

        Console.WriteLine(FormatujWiadomość("Serwer uruchomiony"));
        Console.WriteLine(FormatujWiadomość("Błąd połączenia", "[ERROR]"));
        Console.WriteLine(FormatujWiadomość("debug info", "[DBG]", uppercase: true));
        Console.WriteLine(FormatujWiadomość("Krótki tekst", maxLength: 10));

        // Null jako sentinel dla wartości niedostępnych w compile-time
        // (DateTime.Now, new List<>() nie mogą być default — użyj null + sprawdzenie)
        static string PobierzDane(
            string  url,
            int?    timeoutMs    = null,   // null → użyj domyślnego
            string? nagłówek     = null,   // null → brak nagłówka
            DateTime? dataOd     = null)   // null → DateTime.Now
        {
            int    efTimeout = timeoutMs ?? 5000;
            string efNagł    = nagłówek  ?? "brak";
            DateTime efData  = dataOd    ?? DateTime.Now;
            return $"GET {url} timeout={efTimeout}ms nagł={efNagł} od={efData:yyyy-MM-dd}";
        }

        Console.WriteLine(PobierzDane("https://api.example.com"));
        Console.WriteLine(PobierzDane("https://api.example.com", timeoutMs: 1000));
        Console.WriteLine(PobierzDane("https://api.example.com", nagłówek: "Bearer TOKEN"));

        // Kolejność — opcjonalne MUSZĄ być po wymaganych
        // static void Zle(string? opt = null, string wymagany) { } // CS1737 BŁĄD!
        Console.WriteLine("\nReguła: opcjonalne zawsze po wymaganych (lub użyj named args)");
    }

    // ── 2. Nazwane argumenty ──────────────────────────────────────────────────

    public static void NazwaneArgumenty()
    {
        Console.WriteLine("\n── NazwaneArgumenty ──");

        // Nazwane argumenty — param: value
        // Zwiększają czytelność, pozwalają pominąć środkowe opcjonalne
        static void KonfigurujPolaczenie(
            string  host,
            int     port         = 5432,
            bool    useSsl       = true,
            int     poolMin      = 1,
            int     poolMax      = 10,
            string  charSet      = "UTF-8",
            int     timeoutSek   = 30)
        {
            Console.WriteLine($"  {host}:{port} ssl={useSsl} pool={poolMin}-{poolMax} " +
                              $"charset={charSet} timeout={timeoutSek}s");
        }

        // Bez named — trzeba pamiętać kolejność wszystkich parametrów
        KonfigurujPolaczenie("prod-db", 5432, true, 5, 20, "UTF-8", 60);

        // Z named — jasne, pomija środkowe jeśli chcemy tylko niektóre
        KonfigurujPolaczenie("prod-db",
            poolMin: 5,
            poolMax: 20,
            timeoutSek: 60);  // port, useSsl, charSet — domyślne

        KonfigurujPolaczenie("dev-db",
            port: 5433,
            useSsl: false,
            poolMax: 3);

        // Dowolna kolejność nazwanych (C# 7.2+)
        KonfigurujPolaczenie(
            timeoutSek: 10,
            host: "localhost",   // pozycjonalne mogą być za nazwanymi (C# 7.2+)
            port: 5432);

        // Named args z metodami przeciążonymi — wskazują konkretną sygnaturę
        static int Dodaj(int a, int b) => a + b;
        Console.WriteLine($"\nDodaj(b: 3, a: 7) = {Dodaj(b: 3, a: 7)}");  // kolejność odwrócona
    }

    // ── 3. Kombinowane użycie ─────────────────────────────────────────────────

    public static void KombinowaneUzycie()
    {
        Console.WriteLine("\n── KombinowaneUzycie ──");

        // Optional + named — idealny do konfiguracji z wieloma opcjami
        static KonfiguracjaEmailON UtwórzKonfigEmail(
            string smtpHost,
            int    port     = 587,
            bool   useSsl   = true,
            int    timeout  = 30_000,
            int    maxRetry = 3)
        {
            return new KonfiguracjaEmailON
            {
                SmtpHost = smtpHost,
                Port     = port,
                UseSsl   = useSsl,
                Timeout  = timeout,
                MaxRetry = maxRetry
            };
        }

        // Różne konfiguracje — tylko zmienione parametry
        var gmail    = UtwórzKonfigEmail("smtp.gmail.com");
        var office   = UtwórzKonfigEmail("smtp.office365.com", port: 25, useSsl: false);
        var lokalny  = UtwórzKonfigEmail("localhost", port: 25, useSsl: false, maxRetry: 1);
        var szybki   = UtwórzKonfigEmail("smtp.fast.com", timeout: 5_000);

        foreach (var cfg in new[] { gmail, office, lokalny, szybki })
            Console.WriteLine($"  {cfg.SmtpHost}:{cfg.Port} ssl={cfg.UseSsl} " +
                              $"timeout={cfg.Timeout}ms retry={cfg.MaxRetry}");

        // Named args dla czytelności przy logicznych bool-ach
        static void UruchomSerwis(
            string  nazwa,
            bool    autoRestart    = false,
            bool    logowanieDebug = false,
            bool    metrykaWlaczona = true)
        {
            Console.WriteLine($"  Serwis {nazwa}: autoRestart={autoRestart} " +
                              $"debug={logowanieDebug} metryki={metrykaWlaczona}");
        }

        Console.WriteLine("\nUruchamianie serwisów:");
        // Bez named — sekwencja bool-i jest nieczytelna
        UruchomSerwis("ApiGateway", true, false, true);

        // Z named — od razu wiadomo co znaczy każdy parametr
        UruchomSerwis("ApiGateway",
            autoRestart:    true,
            logowanieDebug: false,
            metrykaWlaczona: true);
    }

    // ── 4. Pułapki parametrów opcjonalnych ────────────────────────────────────

    public static void PulapkiOptional()
    {
        Console.WriteLine("\n── PulapkiOptional ──");

        // PUŁAPKA 1 — wartość domyślna wkompilowana w CALLER IL
        // Zmiana domyślnej wartości w bibliotece NIE wpływa na istniejące kallers
        // bez rekompilacji callera!
        //
        // Biblioteka v1.0: void Foo(int x = 10)
        // Biblioteka v2.0: void Foo(int x = 20) ← zmiana!
        // Caller skompilowany z v1.0: nadal wywołuje Foo(10) ← NIE widzi zmiany!
        //
        // Rozwiązanie: jeśli to publiczne API → użyj przeciążeń zamiast optional
        Console.WriteLine("Pułapka 1: default baked into caller IL");
        Console.WriteLine("  Jeśli zmieniasz default w public API → musisz rekompilować wszystkich callerów");

        // PUŁAPKA 2 — zmiana NAZWY parametru to breaking change przy named args
        // void Foo(int count = 0) → void Foo(int ile = 0)  // zmiana nazwy
        // Caller: Foo(count: 5) // CS1739 BŁĄD po zmianie nazwy!
        Console.WriteLine("\nPułapka 2: zmiana nazwy parametru = breaking change (named args)");
        Console.WriteLine("  Foo(count: 5) przestaje kompilować gdy parametr nazwiesz 'ile'");

        // PUŁAPKA 3 — rozwiązanie przeciążeń z opcjonalnymi
        // Metoda bardziej specyficzna wygrywa nad opcjonalną (local functions nie obsługują overloads)
        // Demonstrujemy jako osobne metody klasy (patrz OpisInt / OpisIntStr poniżej):
        Console.WriteLine("\nPułapka 3: rozwiązanie przeciążeń");
        Console.WriteLine($"  OpisInt(5)            = {OpisInt(5)}");   // int — bardziej specyficzne!
        Console.WriteLine($"  OpisIntStr(5, \"abc\") = {OpisIntStr(5, "abc")}");
        Console.WriteLine("  Wywołanie Opisz(5) z prawdziwymi przeciążeniami → wersja bez optional wins");

        // PUŁAPKA 4 — params vs optional — nie mieszaj dla tych samych typów
        // void Log(string msg, params string[] tags) — params
        // void Log(string msg, string tag1 = "", string tag2 = "") — optional
        // Wywołanie Log("msg", "tag") — które wywoła? Niejednoznaczność!
        Console.WriteLine("\nPułapka 4: params vs optional — niejednoznaczność");
        Console.WriteLine("  Nie definiuj obu wariantów dla tych samych typów argumentów");

        // PUŁAPKA 5 — optional + przeciążenia w public API → użyj przeciążeń
        Console.WriteLine("\nPułapka 5: public API → preferuj przeciążenia");
        Console.WriteLine("  Optional params: wartość wkompilowana u kallera");
        Console.WriteLine("  Przeciążenia:    można zmienić logikę bez rekompilacji kallerów");
    }

    // ── 5. Kiedy przeciążenia, kiedy opcjonalne ───────────────────────────────

    public static void OverloadsVsOptional()
    {
        Console.WriteLine("\n── OverloadsVsOptional ──");

        // PRZECIĄŻENIA — kiedy lepsze:
        // ✅ Różna logika dla różnych zestawów argumentów
        // ✅ Stabilne publiczne API (callerzy nie muszą rekompilować)
        // ✅ Czytelna sygnatura bez zbędnych parametrów
        // ✅ Typy zamiast bool-flag (Foo(bool szczegółowy) → FooSzybki() + FooSzczegółowy())

        // Przykład: zapisz dane — różna logika per format
        static void ZapiszJSON(string dane)   => Console.WriteLine($"  JSON: {dane}");
        static void ZapiszXML(string dane)    => Console.WriteLine($"  XML:  {dane}");
        static void ZapiszCSV(string dane, char sep = ',') =>
            Console.WriteLine($"  CSV(sep='{sep}'): {dane}");

        Console.WriteLine("Przeciążenia — różna logika:");
        ZapiszJSON("{\"key\":1}");
        ZapiszXML("<root>1</root>");
        ZapiszCSV("a,b,c");

        // OPCJONALNE + NAMED — kiedy lepsze:
        // ✅ Ta sama logika, wiele niezależnych opcji konfiguracyjnych
        // ✅ Wewnętrzne API (nie public library)
        // ✅ Wzorzec "fluent options" gdzie 90% wywołań używa domyślnych

        static string WyslijEmail(
            string  odbiorca,
            string  temat,
            string  treść,
            bool    ważny    = false,
            bool    bcc      = false,
            string? replyTo  = null,
            int     prioryta = 3)
        {
            var flagi = new List<string>();
            if (ważny) flagi.Add("WAŻNY");
            if (bcc)   flagi.Add("BCC");
            if (replyTo != null) flagi.Add($"reply={replyTo}");
            string f = flagi.Any() ? $" [{string.Join(",", flagi)}]" : "";
            return $"Email → {odbiorca} '{temat}' prio={prioryta}{f}";
        }

        Console.WriteLine("\nOptional+named — ta sama logika, wiele opcji:");
        Console.WriteLine("  " + WyslijEmail("jan@test.pl", "Witaj", "Treść"));
        Console.WriteLine("  " + WyslijEmail("ann@test.pl", "Pilne!", "Treść",
            ważny: true, prioryta: 1));
        Console.WriteLine("  " + WyslijEmail("bob@test.pl", "Fwd", "Treść",
            bcc: true, replyTo: "org@test.pl"));

        // PODSUMOWANIE
        Console.WriteLine("\nPodsumowanie:");
        Console.WriteLine("  Przeciążenia:   różna logika, stabilne public API, różne typy");
        Console.WriteLine("  Optional+named: ta sama logika, wiele konfiguracji, internal API");
        Console.WriteLine("  Reguła kciuka:  public library → przeciążenia; internal → optional+named");
    }
}
