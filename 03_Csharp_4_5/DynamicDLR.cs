using System.Dynamic;

namespace _03_Csharp_4_5;

// ── Klasy pomocnicze ──────────────────────────────────────────────────────────

// Własna implementacja DynamicObject — niestandardowe zachowanie
public class DynamicznyObiektDLR : DynamicObject
{
    private readonly Dictionary<string, object?> _dane = new();
    private int _licznikOdczytow = 0;

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        _licznikOdczytow++;
        if (!_dane.TryGetValue(binder.Name, out result))
            result = $"[brak: {binder.Name}]";
        return true;  // zwróć placeholder, nie rzucaj wyjątku
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        _dane[binder.Name] = value;
        return true;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder,
        object?[]? args, out object? result)
    {
        if (binder.Name == "Info")
        {
            result = $"Obiekt: {_dane.Count} właściwości, odczytano {_licznikOdczytow}×";
            return true;
        }
        result = null;
        return false;  // metoda nieznana → RuntimeBinderException
    }

    public override IEnumerable<string> GetDynamicMemberNames() => _dane.Keys;
}

public static class DynamicDLR
{
    // ── 1. var vs dynamic ─────────────────────────────────────────────────────

    public static void VarVsDynamic()
    {
        Console.WriteLine("\n── VarVsDynamic ──");

        // var — wnioskowanie COMPILE-TIME — pełne IntelliSense, zero narzutu
        var liczba = 42;      // int — znany w compile-time
        var tekst  = "hello"; // string
        Console.WriteLine($"var: {liczba.GetType().Name}, {tekst.GetType().Name}");
        // liczba = "napis"; // CS0029 — typ jest zablokowany przez kompilator

        // dynamic — sprawdzanie RUNTIME — brak IntelliSense, brak bezpieczeństwa
        dynamic dyn = 42;
        Console.WriteLine($"dynamic int: {dyn}");
        dyn = "teraz string!";                     // OK — typ może się zmienić
        Console.WriteLine($"dynamic string: {dyn.ToUpper()}");

        // Błąd compile-time vs runtime
        // var x = "abc"; x.Brak(); // BŁĄD kompilacji — natychmiastowa informacja
        try
        {
            dynamic d = "abc";
            d.Brak();  // RuntimeBinderException — RUNTIME, nie kompilacja
        }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
        {
            Console.WriteLine($"RuntimeBinderException: {ex.Message[..Math.Min(55, ex.Message.Length)]}...");
        }

        // Wydajność — dynamic 10-50× wolniejszy w pętlach (DLR binding)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long sumaVar = 0;
        for (int i = 0; i < 500_000; i++) sumaVar += i;
        long msVar = sw.ElapsedMilliseconds;

        sw.Restart();
        dynamic sumaDyn = 0L;
        for (int i = 0; i < 500_000; i++) sumaDyn += i;
        long msDyn = sw.ElapsedMilliseconds;

        Console.WriteLine($"var pętla:     {msVar}ms (wynik: {sumaVar})");
        Console.WriteLine($"dynamic pętla: {msDyn}ms (wynik: {sumaDyn}) — wolniejszy przez DLR");

        Console.WriteLine("\nTABELA var vs dynamic:");
        Console.WriteLine("  var:     compile-time, IntelliSense, zero narzut, bezpieczny");
        Console.WriteLine("  dynamic: runtime, brak IntelliSense, 10-50x wolniejszy, RuntimeBinderException");
    }

    // ── 2. DLR — Dynamic Language Runtime ────────────────────────────────────

    public static void DLRMechanizm()
    {
        Console.WriteLine("\n── DLRMechanizm ──");

        // DLR — warstwa między C# a CLR dla dynamicznych operacji
        // Kluczowe komponenty:
        // 1. CallSite — punkt wywołania dynamicznego (generowany przez kompilator)
        // 2. Binder   — logika wiązania (CSharp RuntimeBinder dla C#)
        // 3. Cache    — zapamiętanie reguł wiązania dla tego samego CallSite
        //
        // Pierwsze wywołanie: DLR bada typ, kompiluje regułę, zapisuje do cache
        // Kolejne wywołania: szybkie sprawdzenie cache → brak refleksji

        dynamic obj = "hello";
        Console.WriteLine("DLR CallSite caching:");
        Console.WriteLine($"  1. wywołanie .ToUpper(): {obj.ToUpper()} ← binding + zapis do cache");
        Console.WriteLine($"  2. wywołanie .ToUpper(): {obj.ToUpper()} ← z cache (szybciej)");

        // Propagacja dynamic — ZARAŻA otaczający kod
        dynamic wejscie = 42;
        var wynik = wejscie + 1;  // wynik jest dynamic, nie int!
        Console.WriteLine($"\nPropagacja: (dynamic 42) + 1 = {wynik}, typ: {wynik.GetType().Name}");
        Console.WriteLine("  UWAGA: operacja na dynamic zwraca dynamic — ogranicz zasięg do minimum");

        // Duck typing — wywołaj metodę jeśli obiekt ją posiada
        object[] obiekty = { "tekst", 42, 3.14, true };
        Console.Write("Duck typing ToString: ");
        foreach (dynamic d in obiekty)
            Console.Write($"[{d}] ");
        Console.WriteLine();

        // DLR obsługuje wiele języków: IronPython, IronRuby, F# — wspólna infrastruktura
        Console.WriteLine("DLR języki: C#, VB.NET, IronPython, IronRuby — wspólna warstwa wiązania");
    }

    // ── 3. ExpandoObject ──────────────────────────────────────────────────────

    public static void ExpandoObjectDemo()
    {
        Console.WriteLine("\n── ExpandoObjectDemo ──");

        // ExpandoObject — gotowy obiekt rozszerzalny w runtime
        dynamic expando = new ExpandoObject();

        // Dodawanie właściwości jak zwykłe przypisania
        expando.Imie   = "Anna";
        expando.Wiek   = 30;
        expando.Miasto = "Warszawa";
        Console.WriteLine($"Pola: {expando.Imie}, {expando.Wiek}, {expando.Miasto}");

        // Dodawanie metody jako Func
        expando.Powitaj = (Func<string>)(() => $"Cześć, jestem {expando.Imie}!");
        Console.WriteLine($"Metoda: {expando.Powitaj()}");

        // IDictionary<string, object?> — iteracja właściwości, usuwanie
        var dict = (IDictionary<string, object?>)expando;
        Console.WriteLine($"\nWłaściwości ({dict.Count}):");
        foreach (var (klucz, wartosc) in dict)
            Console.WriteLine($"  {klucz} = {wartosc}");

        dict.Remove("Miasto");
        Console.WriteLine($"Po dict.Remove(\"Miasto\"): {dict.Count} właściwości");

        // Zagnieżdżone ExpandoObject
        dynamic firma = new ExpandoObject();
        firma.Nazwa  = "TechCorp";
        firma.Adres  = new ExpandoObject();
        firma.Adres.Miasto = "Kraków";
        firma.Adres.Kod    = "30-001";
        Console.WriteLine($"\nZagnieżdżone: firma.Adres.Miasto = {firma.Adres.Miasto}");

        // INotifyPropertyChanged — wbudowane w ExpandoObject!
        dynamic obserwowany = new ExpandoObject();
        ((System.ComponentModel.INotifyPropertyChanged)obserwowany).PropertyChanged +=
            (s, e) => Console.WriteLine($"  PropertyChanged: {e.PropertyName}");

        Console.WriteLine("INotifyPropertyChanged:");
        obserwowany.X = 1;   // wywołuje zdarzenie
        obserwowany.X = 99;  // wywołuje zdarzenie ponownie
    }

    // ── 4. DynamicObject ──────────────────────────────────────────────────────

    public static void DynamicObjectDemo()
    {
        Console.WriteLine("\n── DynamicObjectDemo ──");

        // DynamicObject — klasa bazowa do własnej implementacji dynamiki
        // Nadpisujesz tylko te metody Try*, które potrzebujesz
        dynamic obj = new DynamicznyObiektDLR();

        // TrySetMember
        obj.Imie   = "Piotr";
        obj.Wiek   = 25;
        obj.Miasto = "Gdańsk";

        // TryGetMember — istniejące i nieistniejące pola
        Console.WriteLine($"Imie:        {obj.Imie}");
        Console.WriteLine($"Wiek:        {obj.Wiek}");
        Console.WriteLine($"NieMaBraku:  {obj.NieMaBraku}");  // placeholder zamiast wyjątku

        // TryInvokeMember
        Console.WriteLine($"Info():      {obj.Info()}");

        // GetDynamicMemberNames — lista znanych właściwości
        Console.Write("Właściwości: ");
        foreach (string name in ((DynamicznyObiektDLR)obj).GetDynamicMemberNames())
            Console.Write($"{name} ");
        Console.WriteLine();

        // Porównanie DynamicObject vs ExpandoObject:
        Console.WriteLine("\nExpandoObject: szybki start, IDictionary, INotifyPropertyChanged");
        Console.WriteLine("DynamicObject: pełna kontrola — walidacja, logowanie, lazy loading, fallback");

        // Inne metody TryXxx które możesz nadpisać:
        Console.WriteLine("\nTryXxx do nadpisania: TryGetMember, TrySetMember, TryInvokeMember,");
        Console.WriteLine("  TryBinaryOperation, TryUnaryOperation, TryGetIndex, TrySetIndex,");
        Console.WriteLine("  TryConvert, TryCreateInstance, TryDeleteMember");
    }

    // ── 5. Użycia i ryzyka dynamic ────────────────────────────────────────────

    public static void UzyciaIRyzyka()
    {
        Console.WriteLine("\n── UzyciaIRyzyka ──");

        Console.WriteLine("KIEDY dynamic ma sens:");
        Console.WriteLine("  1. COM Interop (Excel/Word) — API bez typowanych wrapperów");
        Console.WriteLine("     excel.Cells[1,1].Value = 42; // bez dynamic — 50 linii castów");

        // Duck typing — wywołaj metodę niezależnie od hierarchii typów
        static string WywołajToString(dynamic d) => d.ToString()!;
        Console.WriteLine($"  2. Duck typing: {WywołajToString(DateTime.Now):HH:mm:ss}");

        Console.WriteLine("  3. JSON z nieznaną strukturą — ExpandoObject + JsonConverter");
        Console.WriteLine("  4. Plugin system — ładowanie assembly + wywołanie metod");
        Console.WriteLine("  5. Prototypowanie — przed ustaleniem ostatecznych typów");

        Console.WriteLine("\nRYZYKA dynamic:");

        // Ryzyko 1 — RuntimeBinderException zamiast błędu kompilacji
        try
        {
            dynamic d = 42;
            _ = d.NieIstniejacaWlasc;
        }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            Console.WriteLine("  1. RuntimeBinderException — wykrywasz błąd dopiero w runtime");
        }

        // Ryzyko 2 — brak IntelliSense i bezpiecznego refaktoringu
        Console.WriteLine("  2. Brak IntelliSense — tytujesz w ciemno, refactoring nie działa");
        Console.WriteLine("  3. Wydajność — 10-50× wolniej, DLR binding przy każdym wywołaniu");

        // Ryzyko 4 — propagacja
        dynamic x = 5;
        var y = x * 2;  // y jest dynamic, nie int!
        Console.WriteLine($"  4. Propagacja: dynamic * 2 → wynik też dynamic ({y.GetType().Name})");

        // Ryzyko 5 — niespodziewane konwersje
        dynamic str = "5";
        dynamic num = 3;
        Console.WriteLine($"  5. Niespodz. konwersja: \"5\" + 3 = \"{str + num}\" (konkatenacja, nie suma!)");

        Console.WriteLine("\nALTERNATYWY zamiast dynamic:");
        Console.WriteLine("  JsonElement   — type-safe odczyt JSON");
        Console.WriteLine("  Interfejsy    — duck typing bez dynamic");
        Console.WriteLine("  Strongly-typed models — gdy znasz strukturę");
        Console.WriteLine("  generics + Func/Action — zamiast dynamicznych wywołań");
    }

    // ── 6. Silnik reguł biznesowych ───────────────────────────────────────────

    public static void SilnikRegulBiznesowych()
    {
        Console.WriteLine("\n── SilnikRegulBiznesowych ──");

        // ExpandoObject jako dynamiczny kontekst reguł biznesowych
        // Reguły = Func<dynamic, bool> — mogą odczytywać dowolne pola kontekstu
        // Dodawanie nowych reguł bez rekompilacji silnika

        var reguly = new List<(string Nazwa, Func<dynamic, bool> Regula)>
        {
            ("Wiek >= 18",      ctx => ctx.Wiek >= 18),
            ("Dochód > 3000",   ctx => ctx.Dochod > 3000m),
            ("Konto > 0 dni",   ctx => ctx.DniKonta > 0),
            ("Brak długów",     ctx => !ctx.MaDlugi),
        };

        // Konfiguracje klientów
        var klienci = new[]
        {
            ("Jan Kowalski", (Action<dynamic>)(ctx =>
            {
                ctx.Wiek = 35; ctx.Dochod = 5000m; ctx.DniKonta = 365; ctx.MaDlugi = false;
            })),
            ("Anna Młoda", (Action<dynamic>)(ctx =>
            {
                ctx.Wiek = 17; ctx.Dochod = 2000m; ctx.DniKonta = 30; ctx.MaDlugi = false;
            })),
            ("Piotr Zadłużony", (Action<dynamic>)(ctx =>
            {
                ctx.Wiek = 40; ctx.Dochod = 1500m; ctx.DniKonta = 200; ctx.MaDlugi = true;
            })),
        };

        Console.WriteLine("Ocena wniosków kredytowych:");
        foreach (var (imie, konfiguracja) in klienci)
        {
            dynamic kontekst = new ExpandoObject();
            konfiguracja(kontekst);

            var wyniki = reguly.Select(r => (r.Nazwa, Ok: r.Regula(kontekst))).ToList();
            bool zatwierdzony = wyniki.All(w => w.Ok);

            Console.WriteLine($"\n  {imie} → {(zatwierdzony ? "ZATWIERDZONY" : "ODRZUCONY")}");
            foreach (var (nazwa, ok) in wyniki)
                Console.WriteLine($"    {(ok ? "✓" : "✗")} {nazwa}");
        }

        // Dodawanie reguł w runtime — bez rekompilacji silnika
        reguly.Add(("Scoring >= 600", ctx =>
        {
            var d = (IDictionary<string, object?>)ctx;
            return d.TryGetValue("Scoring", out var s) && Convert.ToInt32(s) >= 600;
        }));
        Console.WriteLine($"\nDodano regułę w runtime — łącznie: {reguly.Count} reguł");
    }
}
