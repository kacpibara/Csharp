namespace _03_Csharp_2;

// ── Typy pomocnicze dla DelegatyIZdarzenia ───────────────────────────────────

// Własny delegat — typ wskaźnika na metodę
public delegate double OperacjaMatDel(double a, double b);
public delegate void ObslugaZdarzeniaDel(string komunikat);
public delegate bool PredykatDel<T>(T element);

// Klasa z zdarzeniami (event keyword — hermetyzacja delegata)
public class MinutnikDel
{
    // event — zewnętrzny kod może tylko += i -=, nie może = ani bezpośrednio wywołać
    public event Action<int>? Tyk;
    public event Action? Zatrzymany;

    private bool _dziala = false;

    public void Uruchom(int sekundy)
    {
        _dziala = true;
        for (int i = 1; i <= sekundy && _dziala; i++)
        {
            Tyk?.Invoke(i);
        }
        _dziala = false;
        Zatrzymany?.Invoke();
    }

    public void Zatrzymaj() => _dziala = false;
}

// EventArgs dla zdarzenia zmiany temperatury
public class ZmianaTemperaturyArgs : EventArgs
{
    public double Stara { get; }
    public double Nowa { get; }
    public double Roznica => Nowa - Stara;

    public ZmianaTemperaturyArgs(double stara, double nowa)
    {
        Stara = stara;
        Nowa = nowa;
    }
}

// Klasa z EventHandler<T> — wzorzec .NET
public class TermometrDel
{
    private double _temperatura;

    // EventHandler<TEventArgs> — standardowy delegat .NET
    public event EventHandler<ZmianaTemperaturyArgs>? ZmianaTemperatury;

    public double Temperatura
    {
        get => _temperatura;
        set
        {
            if (Math.Abs(value - _temperatura) > 0.01)
            {
                var args = new ZmianaTemperaturyArgs(_temperatura, value);
                _temperatura = value;
                OnZmianaTemperatury(args); // protected virtual — wzorzec .NET
            }
        }
    }

    protected virtual void OnZmianaTemperatury(ZmianaTemperaturyArgs args)
        => ZmianaTemperatury?.Invoke(this, args);
}

// Subskrybent — implementuje IDisposable, żeby wyrejestrować się ze zdarzenia
public class LoggerTemperaturyDel : IDisposable
{
    private readonly TermometrDel _termometr;

    public LoggerTemperaturyDel(TermometrDel t)
    {
        _termometr = t;
        _termometr.ZmianaTemperatury += OnZmiana;
    }

    private void OnZmiana(object? sender, ZmianaTemperaturyArgs e)
        => Console.WriteLine($"  [Logger] {e.Stara:F1}°C → {e.Nowa:F1}°C (Δ={e.Roznica:+0.0;-0.0}°C)");

    // Wyrejestrowanie — zapobiega wyciekowi pamięci
    public void Dispose() => _termometr.ZmianaTemperatury -= OnZmiana;
}

// Dla demonstracji domknięcia i pułapki pętli
public class PrzyciskDel
{
    private readonly string _nazwa;
    public event Action<string>? Kliknieto;

    public PrzyciskDel(string nazwa) => _nazwa = nazwa;
    public void Kliknij() => Kliknieto?.Invoke(_nazwa);
}

// ── Klasa demonstracyjna ──────────────────────────────────────────────────────

public static class DelegatyIZdarzenia
{
    public static void PodstawyDelegatow()
    {
        Console.WriteLine("\n── PodstawyDelegatow ──");

        // Własny delegat — deklaracja, tworzenie instancji, wywołanie
        OperacjaMatDel dodaj = (a, b) => a + b;
        OperacjaMatDel odejmij = (a, b) => a - b;
        OperacjaMatDel potega = Math.Pow; // group conversion — metoda pasuje do sygnatury

        Console.WriteLine($"dodaj(10,3)={dodaj(10, 3)}");
        Console.WriteLine($"odejmij(10,3)={odejmij(10, 3)}");
        Console.WriteLine($"potega(2,10)={potega(2, 10)}");

        // Delegat jako parametr — higher-order function
        double[] liczby = { 1, 4, 9, 16, 25 };
        double[] wyniki = Transformuj(liczby, Math.Sqrt);
        Console.WriteLine($"Sqrt: [{string.Join(", ", wyniki.Select(v => $"{v:F1}"))}]");

        // Delegat jako zmienna — przechowywanie różnych zachowań
        OperacjaMatDel operacja = dodaj;
        Console.WriteLine($"Dynamiczna operacja: {operacja(5, 3)}");
        operacja = odejmij;
        Console.WriteLine($"Po zmianie: {operacja(5, 3)}");
    }

    static double[] Transformuj(double[] src, Func<double, double> f)
        => src.Select(f).ToArray();

    public static void FuncActionPredicate()
    {
        Console.WriteLine("\n── FuncActionPredicate ──");

        // Func<T..., TResult> — ostatni typ to TResult (funkcja z wynikiem)
        Func<int, int, int> suma = (a, b) => a + b;
        Func<string, int> dlugoscSłowa = s => s.Length;
        Func<int, bool> CzyParzyste = n => n % 2 == 0;
        Func<double> pi = () => Math.PI; // zero argumentów

        Console.WriteLine($"Func suma(3,4)={suma(3, 4)}");
        Console.WriteLine($"Func dlugoscSłowa(\"hello\")={dlugoscSłowa("hello")}");
        Console.WriteLine($"Func pi()={pi():F4}");

        // Action<T...> — brak wyniku (void)
        Action<string> wypisz = s => Console.WriteLine($"  [Action] {s}");
        Action<int, int> wypisz2 = (a, b) => Console.WriteLine($"  [Action2] {a}+{b}={a + b}");
        Action loguj = () => Console.WriteLine("  [Action] log");

        wypisz("Hello z Action!");
        wypisz2(3, 4);
        loguj();

        // Predicate<T> — specjalny Func<T, bool>
        Predicate<int> CzyDodatnia = n => n > 0;
        Predicate<string> CzyNiePusta = s => !string.IsNullOrWhiteSpace(s);

        Console.WriteLine($"Predicate CzyDodatnia(-5)={CzyDodatnia(-5)}");
        Console.WriteLine($"Predicate CzyNiePusta(\"\")={CzyNiePusta("")}");

        // Currying — przekształcenie f(a,b) w f(a)(b)
        Func<int, Func<int, int>> mnoz = x => y => x * y;
        var podwoj = mnoz(2);
        var potraj = mnoz(3);
        Console.WriteLine($"Currying: podwoj(7)={podwoj(7)}, potraj(7)={potraj(7)}");

        // Kompozycja funkcji
        Func<int, int> razy2 = x => x * 2;
        Func<int, int> plus10 = x => x + 10;
        Func<int, int> razy2Plus10 = x => plus10(razy2(x));
        Console.WriteLine($"Kompozycja razy2+plus10: f(5)={razy2Plus10(5)}");

        // Partial application
        Func<int, int, int> dodajDwa = (a, b) => a + b;
        Func<int, int> dodaj5 = PartialApply(dodajDwa, 5);
        Console.WriteLine($"PartialApply dodaj5(7)={dodaj5(7)}");
    }

    static Func<T2, TResult> PartialApply<T1, T2, TResult>(Func<T1, T2, TResult> f, T1 arg)
        => x => f(arg, x);

    public static void MulticastDelegaty()
    {
        Console.WriteLine("\n── MulticastDelegaty ──");

        // Multicast — jeden delegat, wiele metod (invocation list)
        Action<string> handler = null!;
        handler += s => Console.WriteLine($"  [H1] {s}");
        handler += s => Console.WriteLine($"  [H2] {s.ToUpper()}");
        handler += s => Console.WriteLine($"  [H3] długość={s.Length}");

        Console.WriteLine("Invoke multicast:");
        handler("test");

        // -= usuwa ostatnie dodane pasujące wyrażenie
        Action<string> h3 = s => Console.WriteLine($"  [H3] długość={s.Length}");
        // (lambdy zarejestrowane inline nie mogą być usunięte przez !=)
        Console.WriteLine("Delegates po -= usunięcia h3 (zarejestrowanego inline: nie działa)");

        // GetInvocationList — iteracja przez handlery
        Action<string> multi = s => Console.WriteLine($"  [A] {s}");
        multi += s => Console.WriteLine($"  [B] {s}");
        Console.WriteLine("InvocationList count: " + multi.GetInvocationList().Length);

        // Func multicast — TYLKO ostatnia wartość jest zwracana!
        Func<int> fn = () => 1;
        fn += () => 2;
        fn += () => 3;
        Console.WriteLine($"Func multicast — zwraca tylko ostatnią: {fn()}"); // 3

        // Bezpieczne wywołanie przez GetInvocationList
        Console.WriteLine("Zbieranie wyników wszystkich handlerów:");
        foreach (Func<int> del in fn.GetInvocationList())
            Console.Write($"{del()} ");
        Console.WriteLine();
    }

    public static void ZdarzeniaEvent()
    {
        Console.WriteLine("\n── ZdarzeniaEvent ──");

        // event keyword — hermetyzacja delegata
        // Zewnętrzny kod: tylko += i -=
        // Klasa właściciel: może wywołać i sprawdzić null

        var minutnik = new MinutnikDel();

        minutnik.Tyk += sekundy => Console.Write($"Tyk{sekundy} ");
        minutnik.Tyk += sekundy =>
        {
            if (sekundy % 2 == 0) Console.Write("[parzysty] ");
        };
        minutnik.Zatrzymany += () => Console.WriteLine("\nMinutnik zatrzymany!");

        // minutnik.Tyk = null; // BŁĄD — event nie pozwala na = z zewnątrz
        // minutnik.Tyk("hack"); // BŁĄD — event nie można wywołać z zewnątrz

        minutnik.Uruchom(4);

        // Wyrejestrowanie handlera
        Action<int> tykHandler = s => Console.Write($"[T{s}]");
        minutnik.Tyk += tykHandler;
        minutnik.Tyk -= tykHandler; // wyrejestrowanie
        Console.WriteLine("Wyrejestrowany handler — nie zostanie wywołany");
        minutnik.Uruchom(2);
        Console.WriteLine();
    }

    public static void EventHandlerT()
    {
        Console.WriteLine("\n── EventHandlerT ──");

        // EventHandler<TEventArgs> — standardowy wzorzec .NET
        // signature: void Handler(object? sender, TEventArgs e)

        var termometr = new TermometrDel();

        // Subskrypcja z IDisposable — zapobiega wyciekowi pamięci
        using var logger = new LoggerTemperaturyDel(termometr);

        // Anonim subskrybent — trudny do wyrejestrowania
        termometr.ZmianaTemperatury += (sender, e) =>
            Console.WriteLine($"  [Alert] Temperatura: {e.Nowa:F1}°C");

        termometr.Temperatura = 20.0;
        termometr.Temperatura = 25.5;
        termometr.Temperatura = 19.0;

        Console.WriteLine("Logger wyrejestrowany po using — następna zmiana tylko do alertu:");
        // logger jest Disposed po bloku using, ale zawijamy ręcznie
        logger.Dispose();
        termometr.Temperatura = 30.0;

        // protected virtual OnXxx — wzorzec pozwalający klasom pochodnym przechwycić zdarzenie
        Console.WriteLine("\nWzorzec: protected virtual OnXxx — klasy pochodne mogą override");
    }

    public static void Lambdy()
    {
        Console.WriteLine("\n── Lambdy ──");

        // Składnia lambda: parametry => ciało
        Func<int, int> kw1 = x => x * x;               // wyrażenie
        Func<int, int> kw2 = x => { return x * x; };   // blok
        Func<int, int, int> dodaj = (a, b) => a + b;
        Func<string> powitanie = () => "Cześć!";        // zero parametrów

        Console.WriteLine($"kw1(5)={kw1(5)}, kw2(5)={kw2(5)}");
        Console.WriteLine($"dodaj(3,4)={dodaj(3, 4)}");
        Console.WriteLine($"powitanie()={powitanie()}");

        // Domknięcie (closure) — lambda przechwytuje zmienne z zewnętrznego scope
        int mnoznik = 3;
        Func<int, int> mnoz = x => x * mnoznik; // przechwytuje referencję do mnoznik!
        Console.WriteLine($"mnoz(5) gdzie mnoznik=3: {mnoz(5)}");
        mnoznik = 10; // zmiana mnoznika...
        Console.WriteLine($"mnoz(5) gdzie mnoznik=10 teraz: {mnoz(5)}"); // 50 — domknięcie!

        // Static lambda (C# 9+) — nie może przechwytywać zmiennych z zewnątrz
        // static lambdy są bezpieczne (brak niezamierzonych domknięć) i nie alokują obiektu domknięcia
        Func<int, int> staticLambda = static x => x * 2; // OK — nie przechwytuje niczego
        // Func<int, int> staticZlaPrzyk = static x => x * mnoznik; // BŁĄD kompilacji
        Console.WriteLine($"static lambda(7)={staticLambda(7)}");

        // Lambda jako argument metody
        int[] liczby = { 5, 3, 8, 1, 9, 2, 7 };
        Array.Sort(liczby, (a, b) => a - b); // lambda jako IComparer inline
        Console.WriteLine($"Posortowane: [{string.Join(", ", liczby)}]");

        var powyzej5 = liczby.Where(n => n > 5);
        Console.WriteLine($"Powyżej 5: [{string.Join(", ", powyzej5)}]");
    }

    public static void PulapkaDomkniec()
    {
        Console.WriteLine("\n── PulapkaDomkniec ──");

        // Klasyczna pułapka: capture zmiennej pętli (zmienna, nie wartość!)
        var akcjeZle = new List<Action>();
        for (int i = 0; i < 5; i++)
            akcjeZle.Add(() => Console.Write($"{i} ")); // przechwytuje referencję do i!

        Console.Write("Błędne: ");
        foreach (var a in akcjeZle) a(); // 5 5 5 5 5 — i=5 po pętli!
        Console.WriteLine();

        // Naprawka 1: kopia zmiennej w ciele pętli
        var akcjeOk1 = new List<Action>();
        for (int i = 0; i < 5; i++)
        {
            int kopia = i; // nowa zmienna dla każdej iteracji
            akcjeOk1.Add(() => Console.Write($"{kopia} "));
        }
        Console.Write("Naprawione (kopia): ");
        foreach (var a in akcjeOk1) a(); // 0 1 2 3 4
        Console.WriteLine();

        // Naprawka 2: foreach nie ma tego problemu w C# 5+ (każda iteracja ma własną zmienną)
        var elementy = new[] { "A", "B", "C" };
        var akcjeForEach = new List<Action>();
        foreach (var e in elementy)
            akcjeForEach.Add(() => Console.Write($"{e} ")); // OK w C# 5+
        Console.Write("Foreach (OK w C# 5+): ");
        foreach (var a in akcjeForEach) a(); // A B C
        Console.WriteLine();

        // Domknięcie z mutowalnymi danymi — zamierzony efekt
        int licznik = 0;
        Action inkrementuj = () => licznik++;
        Action[] tabs = { inkrementuj, inkrementuj, inkrementuj };
        foreach (var t in tabs) t();
        Console.WriteLine($"Zamierzony shared state: licznik={licznik}"); // 3
    }

    public static void WyciekPamieci()
    {
        Console.WriteLine("\n── WyciekPamieci ──");

        // Wyciek przez event — wydawca trzyma referencję do subskrybenta
        // Subskrybent nie może być zebrany przez GC dopóki jest zarejestrowany
        var termometr = new TermometrDel();

        // Wyciek: lambda zarejestrowana bez możliwości wyrejestrowania
        termometr.ZmianaTemperatury += (_, e) =>
            Console.WriteLine($"  [Wyciek?] Nowa: {e.Nowa}");

        // Rozwiązanie 1: IDisposable — wyrejestruj w Dispose
        using (var logger = new LoggerTemperaturyDel(termometr))
        {
            termometr.Temperatura = 25.0;
            Console.WriteLine("Logger aktywny");
        } // Dispose() wywołane — -= zarejestrowane, GC może zebrać logger
        Console.WriteLine("Logger wyrejestrowany (Dispose)");
        termometr.Temperatura = 30.0; // logger nie reaguje

        // Rozwiązanie 2: WeakReference<T> — słaba referencja (nie blokuje GC)
        var handler = new ZdarzenieHandlerWeak();
        var weakRef = new WeakReference<ZdarzenieHandlerWeak>(handler);
        Console.WriteLine("WeakReference: subskrybent może być zebrany przez GC");

        // Zawsze wyrejestruj zdarzenie gdy obiekt jest niszczony!
        Console.WriteLine("Zasada: jeśli subskrybujesz zdarzenie — zawsze wyrejestruj w Dispose");
    }
}

// Klasa demonstrujący WeakReference
public class ZdarzenieHandlerWeak
{
    public void Obsłuż(string msg) => Console.WriteLine($"  [Weak] {msg}");
}
