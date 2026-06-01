namespace _03_Csharp_2;

// ── Hierarchia klas dla demonstracji wariancji ────────────────────────────────

public class ZwierzeKov
{
    public string Imie { get; set; } = "";
    public override string ToString() => $"Zwierze({Imie})";
}

public class PiesKov : ZwierzeKov
{
    public string Rasa { get; set; } = "";
    public override string ToString() => $"Pies({Imie},{Rasa})";
}

public class PudelKov : PiesKov
{
    public override string ToString() => $"Pudel({Imie})";
}

// ── Kowariantny interfejs (out T) — "producent" ───────────────────────────────

public interface IProducentKov<out T>   // out = kowariantny
{
    T Produkuj();
    IEnumerable<T> ProdukujWiele();
    // void Przyjmij(T e); // BŁĄD — out T nie może być parametrem wejściowym
}

public class HodowlaPsovKov : IProducentKov<PiesKov>
{
    private readonly PiesKov[] _psy =
    {
        new PiesKov { Imie = "Rex",   Rasa = "Labrador" },
        new PiesKov { Imie = "Burek", Rasa = "Owczarek" }
    };

    public PiesKov Produkuj() => _psy[0];
    public IEnumerable<PiesKov> ProdukujWiele() => _psy;
}

public class HodowlaPudliKov : IProducentKov<PudelKov>
{
    public PudelKov Produkuj() => new() { Imie = "Fifi" };
    public IEnumerable<PudelKov> ProdukujWiele() =>
        new[] { new PudelKov { Imie = "Fifi" }, new PudelKov { Imie = "Coco" } };
}

// ── Kontrawariantny interfejs (in T) — "konsument" ───────────────────────────

public interface IKonsumentKov<in T>    // in = kontrawariantny
{
    void Przyjmij(T element);
    // T Zwroc(); // BŁĄD — in T nie może być typem zwracanym
}

public class LecznicaZwierzatKov : IKonsumentKov<ZwierzeKov>
{
    public void Przyjmij(ZwierzeKov z)
        => Console.WriteLine($"  Leczę: {z}");
}

public class SchroniskoPsovKov : IKonsumentKov<PiesKov>
{
    public void Przyjmij(PiesKov p)
        => Console.WriteLine($"  Schronisko przyjmuje: {p}");
}

// ── Interfejs z oboma rodzajami wariancji ─────────────────────────────────────

public interface ITransformatorKov<in TZrodlo, out TDocel>
{
    TDocel Transformuj(TZrodlo zrodlo);
    IEnumerable<TDocel> TransformujWiele(IEnumerable<TZrodlo> zrodla);
}

public class PiesNaZwierzeKov : ITransformatorKov<PiesKov, ZwierzeKov>
{
    public ZwierzeKov Transformuj(PiesKov p) => p;
    public IEnumerable<ZwierzeKov> TransformujWiele(IEnumerable<PiesKov> psy) => psy;
}

public class PudelNaPiesKov : ITransformatorKov<PudelKov, PiesKov>
{
    public PiesKov Transformuj(PudelKov p) => p;
    public IEnumerable<PiesKov> TransformujWiele(IEnumerable<PudelKov> pudly) => pudly;
}

// ── Klasa demonstracyjna ──────────────────────────────────────────────────────

public static class KowariancjaIKontrawariancja
{
    public static void ProblemBezWariancji()
    {
        Console.WriteLine("\n── ProblemBezWariancji ──");

        // Podstawowe przypisanie (upcasting) — zawsze działa
        PiesKov pies = new PiesKov { Imie = "Rex", Rasa = "Labrador" };
        ZwierzeKov zwierze = pies; // OK — Pies JEST Zwierzęciem
        Console.WriteLine($"Upcasting OK: {zwierze}");

        // List<T> — INWARIANTNA (brak wariancji)
        var listaPsow = new List<PiesKov> { new() { Imie = "Max" } };
        // List<ZwierzeKov> listaZwierzat = listaPsow; // BŁĄD kompilacji!
        // Dlaczego? Gdyby działało: listaZwierzat.Add(new Kot()) → ListaPsow zawierałaby Kota!
        Console.WriteLine("List<Pies> → List<Zwierze>: BŁĄD (List jest inwariantna)");
        Console.WriteLine("Powód: List.Add(T) modyfikuje kolekcję — wstawiłbyś Kota do listy Psów");

        // Tablice — KOWARIANTNE (historyczny błąd C#!) — sprawdzanie w runtime
        ZwierzeKov[] tablicaZwierzat = new PiesKov[3];   // kompiluje się!
        tablicaZwierzat[0] = new PiesKov { Imie = "Azor" }; // OK
        Console.Write("Tablice kowariantne (historyczny błąd): przypisanie Pies[] do Zwierze[] działa");
        try
        {
            tablicaZwierzat[1] = new ZwierzeKov { Imie = "Kot" }; // ArrayTypeMismatchException!
        }
        catch (ArrayTypeMismatchException)
        {
            Console.WriteLine(" — ale wstawianie złego typu: ArrayTypeMismatchException w runtime!");
        }
        Console.WriteLine("Lepsza alternatywa: IReadOnlyList<T> (kowariantna i bezpieczna)");

        // IEnumerable<T> — kowariantna (tylko odczyt)
        IEnumerable<ZwierzeKov> enumZwierzat = listaPsow; // OK — kowariancja!
        Console.WriteLine($"IEnumerable<Pies> → IEnumerable<Zwierze>: OK — tylko odczyt");
        foreach (ZwierzeKov z in enumZwierzat)
            Console.WriteLine($"  {z}");
    }

    public static void Kowariancja()
    {
        Console.WriteLine("\n── Kowariancja (out T) ──");

        // Intuicja: jeśli coś PRODUKUJE Psy, to produkuje też Zwierzęta
        // out T — T pojawia się tylko na wyjściu (return type)
        // Kierunek: Pochodna → Bazowa (zgodny z kierunkiem dziedziczenia)

        var hodowlaPsow  = new HodowlaPsovKov();
        var hodowlaPudli = new HodowlaPudliKov();

        // IProducentKov<Pudel> → IProducentKov<Pies> (kowariancja)
        IProducentKov<PiesKov> producent = hodowlaPudli;  // OK! Pudel : Pies
        PiesKov p = producent.Produkuj(); // dostajemy Pudla, ale jako Psa — bezpieczne
        Console.WriteLine($"Kowariancja (Pudel→Pies): {p}");

        // I wyżej w hierarchii
        IProducentKov<ZwierzeKov> producentZ = hodowlaPudli; // OK! Pudel : Zwierze
        IProducentKov<ZwierzeKov> producentZ2 = hodowlaPsow; // OK! Pies : Zwierze
        Console.WriteLine($"Kowariancja (Pudel→Zwierze): {producentZ.Produkuj()}");
        Console.WriteLine($"Kowariancja (Pies→Zwierze): {producentZ2.Produkuj()}");

        // Lista producentów różnych typów — traktujemy jako producentów Zwierząt
        var hodowle = new List<IProducentKov<ZwierzeKov>>
        {
            hodowlaPsow,   // IProducentKov<Pies> → IProducentKov<Zwierze> ✓
            hodowlaPudli   // IProducentKov<Pudel> → IProducentKov<Zwierze> ✓
        };

        Console.WriteLine("Polimorfizm przez kowariancję:");
        foreach (var h in hodowle)
        {
            foreach (var z in h.ProdukujWiele())
                Console.WriteLine($"  {z}");
        }
    }

    public static void Kontrawariancja()
    {
        Console.WriteLine("\n── Kontrawariancja (in T) ──");

        // Intuicja: jeśli coś AKCEPTUJE Zwierzęta, to akceptuje też Psy
        // in T — T pojawia się tylko na wejściu (parametry metod)
        // Kierunek: Bazowa → Pochodna (ODWRÓCONY względem dziedziczenia!)

        var lecznica   = new LecznicaZwierzatKov();
        var schronisko = new SchroniskoPsovKov();

        // IKonsumentKov<Zwierze> → IKonsumentKov<Pies> (kontrawariancja)
        IKonsumentKov<PiesKov> lecznicaJakoKonsPsa = lecznica; // OK! kontrawariancja
        // Dlaczego bezpieczne? Wywołamy Przyjmij(pies). Lecznica obsługuje Zwierze.
        // Pies : Zwierze — więc lecznica poradzi sobie z Psem.
        lecznicaJakoKonsPsa.Przyjmij(new PiesKov { Imie = "Rex", Rasa = "Lab" });

        // Jeszcze niżej w hierarchii
        IKonsumentKov<PudelKov> lecznicaJakoKonsPudla = lecznica; // OK!
        lecznicaJakoKonsPudla.Przyjmij(new PudelKov { Imie = "Fifi" });

        // Nie można odwrotnie:
        // IKonsumentKov<ZwierzeKov> lecznica2 = schronisko; // BŁĄD!
        // Dlaczego? Wywołalibyśmy Przyjmij(zwierze). Ale schronisko obsługuje tylko Psy.
        // Gdybyśmy przekazali Kota → SchroniskoPsow.Przyjmij(Pies) — Kot nie jest Psem!
        Console.WriteLine("IKonsument<Zwierze> → IKonsument<Pies>: OK (kontrawariancja)");
        Console.WriteLine("IKonsument<Pies> → IKonsument<Zwierze>: BŁĄD (odwrotna hierarchia!)");
    }

    public static void WariancjaDelegatow()
    {
        Console.WriteLine("\n── WariancjaDelegatow ──");

        // Func<out TResult> — TResult jest kowariantny
        Func<PiesKov> produkujPsa = () => new PiesKov { Imie = "Rex", Rasa = "Lab" };
        Func<ZwierzeKov> produkujZwierze = produkujPsa; // OK! Func<Pies> → Func<Zwierze>
        Console.WriteLine($"Func kowariancja (Pies→Zwierze): {produkujZwierze()}");

        Func<PudelKov> produkujPudla = () => new PudelKov { Imie = "Coco" };
        Func<ZwierzeKov> produkujZ2 = produkujPudla; // Func<Pudel> → Func<Zwierze>
        Console.WriteLine($"Func kowariancja (Pudel→Zwierze): {produkujZ2()}");

        // Action<in T> — T jest kontrawariantny
        Action<ZwierzeKov> lecz = z => Console.WriteLine($"  Leczę: {z}");
        Action<PiesKov> leczPsa = lecz;           // Action<Zwierze> → Action<Pies>
        Action<PudelKov> leczPudla = lecz;         // Action<Zwierze> → Action<Pudel>

        leczPsa(new PiesKov { Imie = "Max", Rasa = "Golden" });
        leczPudla(new PudelKov { Imie = "Lulu" });

        // Func<in T, out TResult> — kombinacja: kontrawariantny wejście + kowariantny wyjście
        Func<ZwierzeKov, PiesKov> transformuj = z => new PiesKov { Imie = z.Imie + "_pies" };

        // Func<Zwierze, Pies> → Func<Pies, Zwierze>
        // kontrawariancja wejścia: Pies→Zwierze ✓
        // kowariancja wyjścia: Pies→Zwierze ✓
        Func<PiesKov, ZwierzeKov> transformuj2 = transformuj;
        Console.WriteLine($"Func<Zwierze,Pies>→Func<Pies,Zwierze>: {transformuj2(new PiesKov { Imie = "Azor" })}");
    }

    public static void WbudowaneInterfejsy()
    {
        Console.WriteLine("\n── WbudowaneInterfejsy ──");

        var listaPsow = new List<PiesKov>
        {
            new() { Imie = "Zorro", Rasa = "Husky" },
            new() { Imie = "Ares",  Rasa = "Owczarek" },
            new() { Imie = "Max",   Rasa = "Lab" }
        };

        // IEnumerable<out T> — kowariantny (najczęściej używany)
        IEnumerable<ZwierzeKov> zwierzeta = listaPsow;   // OK! IEnumerable<Pies>→IEnumerable<Zwierze>
        Console.Write("IEnumerable<Pies>→IEnumerable<Zwierze>: ");
        foreach (ZwierzeKov z in zwierzeta) Console.Write($"{z.Imie} ");
        Console.WriteLine();

        // IReadOnlyList<out T> — kowariantny (bezpieczna alternatywa dla tablic)
        IReadOnlyList<PiesKov> roPsy = listaPsow;
        IReadOnlyList<ZwierzeKov> roZwierzeta = roPsy; // OK! IReadOnlyList<Pies>→IReadOnlyList<Zwierze>
        Console.WriteLine($"IReadOnlyList<Pies>→IReadOnlyList<Zwierze>: Count={roZwierzeta.Count}");

        // IReadOnlyCollection<out T> — kowariantny
        IReadOnlyCollection<ZwierzeKov> kolekcja = listaPsow;
        Console.WriteLine($"IReadOnlyCollection kowariantna: Count={kolekcja.Count}");

        // IList<T> — inwariantna (bo ma Add i indekser do zapisu)
        // IList<ZwierzeKov> lista = listaPsow; // BŁĄD! IList nie jest kowariantna

        // IComparer<in T> — kontrawariantny
        IComparer<ZwierzeKov> porownywacz = Comparer<ZwierzeKov>.Create(
            (a, b) => string.Compare(a.Imie, b.Imie, StringComparison.Ordinal));

        // IComparer<Zwierze> → IComparer<Pies> (kontrawariancja!)
        IComparer<PiesKov> porownywaczPsow = porownywacz;
        listaPsow.Sort(porownywaczPsow);
        Console.Write("IComparer<Zwierze>→IComparer<Pies> (kontrawariancja), posortowane: ");
        foreach (var p in listaPsow) Console.Write($"{p.Imie} ");
        Console.WriteLine();

        Console.WriteLine("\nKowariantne .NET: IEnumerable<out T>, IEnumerator<out T>,");
        Console.WriteLine("  IReadOnlyList<out T>, IReadOnlyCollection<out T>, IQueryable<out T>");
        Console.WriteLine("Kontrawariantne .NET: IComparer<in T>, IComparable<in T>, Action<in T>, IObserver<in T>");
    }

    public static void WlasnyTransformator()
    {
        Console.WriteLine("\n── WlasnyTransformator ──");

        // Interfejs z oboma rodzajami wariancji: in TZrodlo, out TDocel
        var piesNaZwierze = new PiesNaZwierzeKov();
        var pudelNaPies   = new PudelNaPiesKov();

        // Kowariancja TDocel: PudelNaPies : ITransformator<Pudel,Pies> → ITransformator<Pudel,Zwierze>
        // — TZrodlo=Pudel→Pudel (bez zmiany ✓)
        // — TDocel=Pies→Zwierze (Pies bardziej szczegółowy niż Zwierze → out ✓)
        ITransformatorKov<PudelKov, ZwierzeKov> t1 = pudelNaPies;
        ZwierzeKov wynik1 = t1.Transformuj(new PudelKov { Imie = "Fifi" });
        Console.WriteLine($"ITransformator<Pudel,Pies>→ITransformator<Pudel,Zwierze> (out TDocel): {wynik1}");

        // Kontrawariancja TZrodlo: PiesNaZwierze : ITransformator<Pies,Zwierze> → ITransformator<Pudel,Zwierze>
        // — TZrodlo=Pies→Pudel (Pies bardziej ogólny niż Pudel → in ✓)
        // — TDocel=Zwierze→Zwierze (bez zmiany ✓)
        ITransformatorKov<PudelKov, ZwierzeKov> t2 = piesNaZwierze;
        ZwierzeKov wynik2 = t2.Transformuj(new PudelKov { Imie = "Rex" });
        Console.WriteLine($"ITransformator<Pies,Zwierze>→ITransformator<Pudel,Zwierze> (in TZrodlo): {wynik2}");

        // Potok przetwarzania — różne transformatory traktowane jednolicie
        var potok = new List<ITransformatorKov<PudelKov, ZwierzeKov>>
        {
            piesNaZwierze, // ITransformator<Pies, Zwierze> → przez kontrawariancję
            pudelNaPies    // ITransformator<Pudel, Pies>  → przez kowariancję
        };

        var pudly = new[] { new PudelKov { Imie = "Coco" }, new PudelKov { Imie = "Kiki" } };
        Console.WriteLine("Potok transformacji:");
        foreach (var t in potok)
        {
            Console.Write($"  ");
            foreach (var w in t.TransformujWiele(pudly))
                Console.Write($"{w} ");
            Console.WriteLine();
        }

        Console.WriteLine("\nMnemonic:");
        Console.WriteLine("  out (kowariant): producent — WYDAJESZ T → możesz wyjść jako ogólniejszy");
        Console.WriteLine("  in (kontrawariant): konsument — PRZYJMUJESZ T → możesz wejść jako szczegółowszy");
        Console.WriteLine("  Prawo: T tylko na wyjściu = out, T tylko na wejściu = in, oba = inwariancja");
    }

    public static void PytaniaRekrutacyjne()
    {
        Console.WriteLine("\n── PytaniaRekrutacyjne (Wariancja) ──");

        Console.WriteLine("Q: Wyjaśnij kowariancję i kontrawariancję prostymi słowami:");
        Console.WriteLine("   Kowariancja (out T): możesz użyć bardziej szczegółowego tam gdzie ogólniejszy.");
        Console.WriteLine("   IEnumerable<Pies> pasuje gdzie IEnumerable<Zwierze> — bo tylko czytasz.");
        Console.WriteLine("   Kontrawariancja (in T): możesz użyć ogólniejszego tam gdzie szczegółowszy.");
        Console.WriteLine("   Action<Zwierze> pasuje gdzie Action<Pies> — bo obsługuje każde Zwierze, też Psa.");

        Console.WriteLine("\nQ: Dlaczego List<T> jest inwariantna a IEnumerable<T> kowariantna?");
        Console.WriteLine("   List<T> ma Add(T) — gdyby była kowariantna, mógłbyś wstawić Kota do List<Pies>.");
        Console.WriteLine("   IEnumerable<T> tylko GetEnumerator() zwracający T — wyłącznie odczyt.");
        Console.WriteLine("   Reguła: T tylko na wyjściu = out, T tylko na wejściu = in.");

        Console.WriteLine("\nQ: Dlaczego tablice w C# są kowariantne i dlaczego to błąd?");
        Console.WriteLine("   Historyczna decyzja dla kompatybilności z Javą.");
        Console.WriteLine("   string[] → object[] kompiluje się, ale object[0] = 42 rzuca ArrayTypeMismatchException.");
        Console.WriteLine("   Sprawdzanie typów przesunięte z compile-time do runtime — narusza bezpieczeństwo.");
        Console.WriteLine("   Bezpieczna alternatywa: IReadOnlyList<T> (kowariantna + bezpieczna).");
    }
}
