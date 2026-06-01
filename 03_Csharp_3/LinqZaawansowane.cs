namespace _03_Csharp_3;

// ── Dane do demonstracji ──────────────────────────────────────────────────────

public class ProduktLZ
{
    public int Id { get; set; }
    public string Nazwa { get; set; } = "";
    public decimal Cena { get; set; }
    public string Kategoria { get; set; } = "";
    public List<string> Tagi { get; set; } = new();
}

public class KategoriaLZ
{
    public string Nazwa { get; set; } = "";
    public string Opis { get; set; } = "";
    public decimal Rabat { get; set; }
}

public static class LinqZaawansowane
{
    static readonly List<ProduktLZ> Produkty = new()
    {
        new() { Id=1, Nazwa="Laptop Pro",   Cena=4500, Kategoria="Elektronika", Tagi=new(){"nowy","bestseller","gaming"} },
        new() { Id=2, Nazwa="Mysz Gaming",  Cena=280,  Kategoria="Elektronika", Tagi=new(){"gaming","bestseller"} },
        new() { Id=3, Nazwa="Monitor 4K",   Cena=2200, Kategoria="Elektronika", Tagi=new(){"nowy","4k"} },
        new() { Id=4, Nazwa="Koszulka Polo",Cena=89,   Kategoria="Odzież",      Tagi=new(){"bawełna","casual"} },
        new() { Id=5, Nazwa="Spodnie Slim", Cena=180,  Kategoria="Odzież",      Tagi=new(){"slim","premium"} },
        new() { Id=6, Nazwa="Słuchawki BT", Cena=450,  Kategoria="Elektronika", Tagi=new(){"wireless","bestseller"} },
        new() { Id=7, Nazwa="Kurtka Zimowa",Cena=450,  Kategoria="Odzież",      Tagi=new(){"premium","outdoor"} },
        new() { Id=8, Nazwa="Klawiatura",   Cena=320,  Kategoria="Elektronika", Tagi=new(){"gaming","mechanical"} },
    };

    static readonly List<KategoriaLZ> Kategorie = new()
    {
        new() { Nazwa="Elektronika", Opis="Sprzęt elektroniczny", Rabat=0.10m },
        new() { Nazwa="Odzież",      Opis="Odzież i akcesoria",   Rabat=0.20m },
    };

    // ── 1. SelectMany zaawansowane ────────────────────────────────────────────

    public static void SelectManyZaawansowane()
    {
        Console.WriteLine("\n── SelectManyZaawansowane ──");

        // Spłaszczanie tagów ze wszystkich produktów
        var wszystkieTagi = Produkty.SelectMany(p => p.Tagi);
        Console.WriteLine($"Wszystkie tagi: {string.Join(", ", wszystkieTagi)}");

        // Unikalne tagi
        var unikalneTagi = Produkty.SelectMany(p => p.Tagi).Distinct().OrderBy(t => t);
        Console.WriteLine($"Unikalne tagi: {string.Join(", ", unikalneTagi)}");

        // SelectMany z zachowaniem kontekstu rodzica (produkt + tag)
        var produktTag = Produkty.SelectMany(
            p => p.Tagi,
            (p, tag) => new { p.Nazwa, Tag = tag });
        Console.WriteLine("\nProdukty z tagami:");
        foreach (var pt in produktTag.Where(x => x.Tag == "bestseller"))
            Console.WriteLine($"  bestseller: {pt.Nazwa}");

        // SelectMany w query syntax (from ... from ...)
        var queryFlat =
            from p in Produkty
            from tag in p.Tagi
            where tag.StartsWith("g")
            select new { p.Nazwa, Tag = tag };
        Console.WriteLine("\nQuery SelectMany (tagi na 'g'):");
        foreach (var x in queryFlat.Distinct())
            Console.WriteLine($"  {x.Nazwa}: {x.Tag}");

        // Kombinatoryka — iloczyn kartezjański
        var rozmiary = new[] { "S", "M", "L", "XL" };
        var kolory   = new[] { "Czerwony", "Niebieski" };
        var kombinacje = rozmiary.SelectMany(r => kolory, (r, k) => $"{r}/{k}");
        Console.WriteLine($"\nIloczyn kartezjański (rozmiar×kolor): {string.Join(", ", kombinacje)}");
    }

    // ── 2. Aggregate zaawansowane ─────────────────────────────────────────────

    public static void AggregateZaawansowane()
    {
        Console.WriteLine("\n── AggregateZaawansowane ──");

        var liczby = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Aggregate bez seed — używa pierwszego elementu jako seed
        int sumaAgg = liczby.Aggregate((acc, n) => acc + n);
        Console.WriteLine($"Aggregate suma: {sumaAgg}"); // 55

        // Aggregate z seed
        int sumaZSeed = liczby.Aggregate(0, (acc, n) => acc + n);
        Console.WriteLine($"Aggregate z seed 0: {sumaZSeed}");

        // Aggregate z result selector (finalna transformacja)
        string result = liczby.Aggregate(
            seed: new System.Text.StringBuilder(),
            func: (sb, n) => sb.Append(n % 2 == 0 ? $"+{n}" : $"{n}"),
            resultSelector: sb => sb.ToString());
        Console.WriteLine($"Aggregate StringBuilder: {result}");

        // Aggregate jako Running Total
        var runningTotal = liczby
            .Aggregate(
                new List<int>(),
                (acc, n) => { acc.Add((acc.Count > 0 ? acc[^1] : 0) + n); return acc; });
        Console.WriteLine($"Running total: {string.Join(", ", runningTotal)}");

        // Aggregate do budowania słownika
        var dict = Produkty.Aggregate(
            new Dictionary<string, int>(),
            (acc, p) =>
            {
                acc[p.Kategoria] = acc.GetValueOrDefault(p.Kategoria) + 1;
                return acc;
            });
        Console.WriteLine("Aggregate → Dictionary[Kategoria:Liczba]:");
        foreach (var kv in dict) Console.WriteLine($"  {kv.Key}: {kv.Value}");

        // Scan (własna implementacja running aggregate)
        static IEnumerable<TAccum> Scan<T, TAccum>(
            IEnumerable<T> src, TAccum seed, Func<TAccum, T, TAccum> f)
        {
            TAccum acc = seed;
            foreach (T e in src) { acc = f(acc, e); yield return acc; }
        }

        var cumSum = Scan(liczby, 0, (a, n) => a + n);
        Console.WriteLine($"\nScan (cumulative sum): {string.Join(", ", cumSum)}");
    }

    // ── 3. IQueryable vs IEnumerable ─────────────────────────────────────────

    public static void IQueryableVsIEnumerable()
    {
        Console.WriteLine("\n── IQueryableVsIEnumerable ──");

        // IEnumerable<T> — wykonanie po stronie KLIENTA (in-process)
        // Predykat to delegate: Func<T, bool>
        // Każdy element trafia do pamięci i jest filtrowany przez delegate
        IEnumerable<ProduktLZ> asEnumerable = Produkty;
        var filtrowaneEnum = asEnumerable.Where(p => p.Cena > 300);
        Console.WriteLine($"IEnumerable.Where (Func<T,bool>): {filtrowaneEnum.Count()} produktów");

        // IQueryable<T> — wykonanie po stronie DOSTAWCY (SQL, web service)
        // Predykat to expression tree: Expression<Func<T, bool>>
        // Dostawca tłumaczy drzewo wyrażeń na SQL (lub inny język zapytań)
        IQueryable<ProduktLZ> asQueryable = Produkty.AsQueryable();
        var filtrowaneQuery = asQueryable.Where(p => p.Cena > 300);
        Console.WriteLine($"IQueryable.Where (Expression<Func<T,bool>>): {filtrowaneQuery.Count()} produktów");

        // Identyczny wynik, różny mechanizm wykonania
        // W EF Core: IQueryable → SQL "SELECT * FROM Produkty WHERE Cena > 300"
        // W IEnumerable: pobierz WSZYSTKO, filtruj w C#

        // AsEnumerable() — wymuszenie strony klienta (dla EF: reszta w C# po SELECT *)
        // AsQueryable() — owinięcie IEnumerable w IQueryable (dla dynamicznych zapytań)

        Console.WriteLine("\nKluczowe różnice:");
        Console.WriteLine("  IEnumerable: Func<T,bool>  — delegate, C#-side, in-process");
        Console.WriteLine("  IQueryable:  Expression<Func<T,bool>> — drzewo wyrażeń, provider-side");
        Console.WriteLine("  EF Core: ZAWSZE używaj IQueryable do filtrowania/sortowania/grupowania");
        Console.WriteLine("  AsEnumerable(): przełącz na C# side (np. dla operacji niemapowanych na SQL)");

        // Dynamiczne budowanie zapytania — tylko z IQueryable
        bool filtrujKategorie = true;
        bool filtrujCene      = true;
        IQueryable<ProduktLZ> query = Produkty.AsQueryable();
        if (filtrujKategorie) query = query.Where(p => p.Kategoria == "Elektronika");
        if (filtrujCene)      query = query.Where(p => p.Cena > 200);
        Console.WriteLine($"\nDynamiczne zapytanie: {query.Count()} produktów");
    }

    // ── 4. Specification Pattern ──────────────────────────────────────────────

    public static void SpecificationPattern()
    {
        Console.WriteLine("\n── SpecificationPattern ──");

        // Specification: hermetyzuje regułę filtrowania jako obiekt
        // Pozwala na: kompozycję (AND/OR/NOT), wielokrotne użycie, testowanie
        var elektronika   = new KategoriaSpec("Elektronika");
        var drogie        = new CenaMinSpec(400);
        var tanieLubOdzież = drogie.Negate().Or(new KategoriaSpec("Odzież"));

        Console.WriteLine("Elektronika i droga (>400):");
        foreach (var p in Produkty.Where(elektronika.And(drogie).IsSatisfiedBy))
            Console.WriteLine($"  {p.Nazwa}: {p.Cena:C}");

        Console.WriteLine("\nNie-drogie (<400) LUB Odzież:");
        foreach (var p in Produkty.Where(tanieLubOdzież.IsSatisfiedBy))
            Console.WriteLine($"  {p.Nazwa}: {p.Cena:C} [{p.Kategoria}]");

        // Specyfikacja z tagiem
        var bestseller = new TagSpec("bestseller");
        Console.WriteLine("\nBestseller i Elektronika:");
        foreach (var p in Produkty.Where(bestseller.And(elektronika).IsSatisfiedBy))
            Console.WriteLine($"  {p.Nazwa}");
    }

    // ── 5. Anti-patterns LINQ ─────────────────────────────────────────────────

    public static void LinqAntiPatterns()
    {
        Console.WriteLine("\n── LinqAntiPatterns ──");

        // Anti-pattern 1: Count() zamiast Any()
        // Count() musi zliczyć WSZYSTKIE elementy, Any() zatrzymuje się przy pierwszym
        bool jestIT_zle   = Produkty.Count(p => p.Kategoria == "Elektronika") > 0; // ZŁE
        bool jestIT_dobrze = Produkty.Any(p => p.Kategoria == "Elektronika");      // DOBRE
        Console.WriteLine($"Any zamiast Count>0: {jestIT_dobrze} (poprawnie)");

        // Anti-pattern 2: ToList() przed filtrowaniem — pobiera WSZYSTKO potem filtruje
        var zle   = Produkty.ToList().Where(p => p.Cena > 200).ToList(); // ToList() za wcześnie
        var dobrze = Produkty.Where(p => p.Cena > 200).ToList();          // filtruj PRZED ToList
        Console.WriteLine($"Filtrowanie przed ToList: {dobrze.Count} produktów");

        // Anti-pattern 3: wielokrotna re-enumeracja IEnumerable
        IEnumerable<ProduktLZ> drogi = Produkty.Where(p => p.Cena > 300);
        // Zła praktyka: każde Count(), Max() etc. iteruje od nowa
        // int c1 = drogi.Count(); int c2 = drogi.Max(...); — 2 pełne iteracje
        // Poprawka: zmaterializuj raz
        var drogiList = drogi.ToList(); // jedna iteracja
        int c1 = drogiList.Count;       // bez iteracji
        decimal cMax = drogiList.Max(p => p.Cena);
        Console.WriteLine($"Re-enumeracja fix: Count={c1}, Max={cMax:C}");

        // Anti-pattern 4: First() bez sprawdzenia — rzuca InvalidOperationException
        try { Produkty.First(p => p.Kategoria == "Robotyka"); }
        catch (InvalidOperationException) { Console.WriteLine("First bez elementu: wyjątek — użyj FirstOrDefault"); }

        // Anti-pattern 5: OrderBy().Where() zamiast Where().OrderBy()
        // Sortuj PO filtrowaniu (mniej elementów do sortowania)
        var wynikZle    = Produkty.OrderBy(p => p.Cena).Where(p => p.Cena > 200); // najpierw sortuj wszystkie
        var wynikDobrze = Produkty.Where(p => p.Cena > 200).OrderBy(p => p.Cena); // filtruj, potem sortuj

        // Anti-pattern 6: LINQ w pętli O(n²)
        var drogi2 = Produkty.Where(p => p.Cena > 300).ToList();
        foreach (var p in drogi2)
        {
            // ZŁE: Kategorie.FirstOrDefault() w pętli = O(n*m)
            var kat = Kategorie.FirstOrDefault(k => k.Nazwa == p.Kategoria);
        }
        // DOBRE: zbuduj słownik raz, potem lookup O(1)
        var katDict = Kategorie.ToDictionary(k => k.Nazwa);
        foreach (var p in drogi2)
        {
            var kat = katDict.GetValueOrDefault(p.Kategoria);
        }
        Console.WriteLine("LINQ w pętli: użyj ToDictionary zamiast FirstOrDefault w pętli");

        Console.WriteLine("\nPodsumowanie anti-patterns:");
        Console.WriteLine("  1. Count()>0 → Any()");
        Console.WriteLine("  2. ToList() za wcześnie przed filtrowaniem");
        Console.WriteLine("  3. Wielokrotna re-enumeracja IEnumerable — zmaterializuj ToList()");
        Console.WriteLine("  4. First() → FirstOrDefault() z null check");
        Console.WriteLine("  5. OrderBy przed Where — sortuj PO filtrowaniu");
        Console.WriteLine("  6. LINQ w pętli O(n²) — ToDictionary + lookup O(1)");
    }

    // ── 6. Operacje na zbiorach i kombinatoryka ───────────────────────────────

    public static void OperacjeNaZbiorach()
    {
        Console.WriteLine("\n── OperacjeNaZbiorach ──");

        var setA = new[] { 1, 2, 3, 4, 5 };
        var setB = new[] { 3, 4, 5, 6, 7 };

        // Union — suma zbiorów (uniq)
        Console.WriteLine($"Union:    {string.Join(", ", setA.Union(setB))}");

        // Intersect — część wspólna
        Console.WriteLine($"Intersect: {string.Join(", ", setA.Intersect(setB))}");

        // Except — różnica (A - B)
        Console.WriteLine($"Except(A-B): {string.Join(", ", setA.Except(setB))}");
        Console.WriteLine($"Except(B-A): {string.Join(", ", setB.Except(setA))}");

        // Distinct — usunięcie duplikatów
        var zduplikowane = new[] { 1, 2, 2, 3, 3, 3, 4 };
        Console.WriteLine($"Distinct:  {string.Join(", ", zduplikowane.Distinct())}");

        // DistinctBy (C# 6 / .NET 6+) — distinct wg klucza
        var distinctWgKat = Produkty.DistinctBy(p => p.Kategoria).Select(p => p.Kategoria);
        Console.WriteLine($"DistinctBy Kategoria: {string.Join(", ", distinctWgKat)}");

        // Zip — łączenie parami (2 lub 3 sekwencje)
        var a = new[] { "A", "B", "C" };
        var b = new[] { 1, 2, 3 };
        var c = new[] { true, false, true };
        var zip2 = a.Zip(b, (l, n) => $"{l}{n}");
        var zip3 = a.Zip(b, c).Select(t => $"{t.First}{t.Second}={t.Third}");
        Console.WriteLine($"\nZip(a,b): {string.Join(", ", zip2)}");
        Console.WriteLine($"Zip(a,b,c): {string.Join(", ", zip3)}");

        // Chunk — podział na porcje o stałym rozmiarze (.NET 6+)
        var liczby = Enumerable.Range(1, 10);
        Console.WriteLine("\nChunk(3):");
        foreach (var chunk in liczby.Chunk(3))
            Console.WriteLine($"  [{string.Join(", ", chunk)}]");

        // SequenceEqual — porównanie sekwencji element po elemencie
        var s1 = new[] { 1, 2, 3 };
        var s2 = new[] { 1, 2, 3 };
        var s3 = new[] { 1, 2, 4 };
        Console.WriteLine($"\nSequenceEqual(s1,s2): {s1.SequenceEqual(s2)}");
        Console.WriteLine($"SequenceEqual(s1,s3): {s1.SequenceEqual(s3)}");

        // Concat — łączenie sekwencji (bez deduplikacji)
        var concat = setA.Concat(setB);
        Console.WriteLine($"Concat: {string.Join(", ", concat)}");

        // Prepend / Append — dodanie elementu na początku/końcu
        var lista = new[] { 2, 3, 4 };
        Console.WriteLine($"Prepend(1): {string.Join(", ", lista.Prepend(1))}");
        Console.WriteLine($"Append(5):  {string.Join(", ", lista.Append(5))}");
    }

    // ── 7. Paginacja i stronicowanie ──────────────────────────────────────────

    public static void Paginacja()
    {
        Console.WriteLine("\n── Paginacja ──");

        // Klasyczna paginacja: Skip + Take
        int rozmiarStrony = 3;
        int calkowitaLiczba = Produkty.Count;

        for (int strona = 0; strona * rozmiarStrony < calkowitaLiczba; strona++)
        {
            var dane = Produkty
                .OrderBy(p => p.Id)
                .Skip(strona * rozmiarStrony)
                .Take(rozmiarStrony)
                .Select(p => p.Nazwa);
            Console.WriteLine($"Strona {strona + 1}: {string.Join(", ", dane)}");
        }

        // Cursor-based paginacja (bardziej wydajna przy dużych zbiorach)
        // Zamiast OFFSET (Skip), pamiętamy ostatni ID i filtrujemy dalej
        int lastSeenId = 0;
        Console.WriteLine("\nCursor-based (po 3):");
        while (true)
        {
            var strona = Produkty
                .Where(p => p.Id > lastSeenId)
                .OrderBy(p => p.Id)
                .Take(rozmiarStrony)
                .ToList();
            if (strona.Count == 0) break;
            Console.WriteLine($"  [{string.Join(", ", strona.Select(p => $"id={p.Id}"))}]");
            lastSeenId = strona[^1].Id;
        }

        // Take z Range (.NET 6+)
        var srodkowe = Produkty.OrderBy(p => p.Id).Take(2..5).Select(p => p.Nazwa);
        Console.WriteLine($"\nTake(2..5): {string.Join(", ", srodkowe)}");
    }
}

// ── Specification Pattern — implementacja ─────────────────────────────────────

public abstract class Specyfikacja<T>
{
    public abstract bool IsSatisfiedBy(T element);

    public Specyfikacja<T> And(Specyfikacja<T> other) => new AndSpec<T>(this, other);
    public Specyfikacja<T> Or(Specyfikacja<T> other) => new OrSpec<T>(this, other);
    public Specyfikacja<T> Negate() => new NotSpec<T>(this);
}

public class AndSpec<T>(Specyfikacja<T> left, Specyfikacja<T> right) : Specyfikacja<T>
{
    public override bool IsSatisfiedBy(T e) => left.IsSatisfiedBy(e) && right.IsSatisfiedBy(e);
}

public class OrSpec<T>(Specyfikacja<T> left, Specyfikacja<T> right) : Specyfikacja<T>
{
    public override bool IsSatisfiedBy(T e) => left.IsSatisfiedBy(e) || right.IsSatisfiedBy(e);
}

public class NotSpec<T>(Specyfikacja<T> inner) : Specyfikacja<T>
{
    public override bool IsSatisfiedBy(T e) => !inner.IsSatisfiedBy(e);
}

public class KategoriaSpec(string kategoria) : Specyfikacja<ProduktLZ>
{
    public override bool IsSatisfiedBy(ProduktLZ p) => p.Kategoria == kategoria;
}

public class CenaMinSpec(decimal min) : Specyfikacja<ProduktLZ>
{
    public override bool IsSatisfiedBy(ProduktLZ p) => p.Cena >= min;
}

public class TagSpec(string tag) : Specyfikacja<ProduktLZ>
{
    public override bool IsSatisfiedBy(ProduktLZ p) => p.Tagi.Contains(tag);
}
