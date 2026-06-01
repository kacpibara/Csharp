namespace _01_Fundamentals;

public static class TableiceIKolekcje
{
    // ─────────────────────────────────────────────────────────────────────────
    // TABLICE — stały rozmiar, ciągły blok pamięci, O(1) dostęp
    // Elementy leżą OBOK SIEBIE w pamięci (cache-friendly)
    // Tablica to REFERENCE TYPE — zmienna przechowuje adres do stercie
    // ─────────────────────────────────────────────────────────────────────────

    public static void TablicePodstawy()
    {
        Console.WriteLine("\n=== TABLICE — PODSTAWY ===");

        // Sposoby deklaracji i inicjalizacji
        int[] tab1 = new int[5];                    // 5 zer
        int[] tab2 = new int[] { 1, 2, 3, 4, 5 };
        int[] tab3 = { 10, 20, 30, 40, 50 };        // skrót
        int[] tab4 = [1, 2, 3];                     // collection expression (C# 12)
        var   tab5 = new[] { 7, 8, 9 };             // kompilator wywnioskuje int[]

        // Dostęp — indeksowanie od 0, O(1)
        Console.WriteLine($"tab3[0]={tab3[0]}, tab3[^1]={tab3[^1]}, tab3[^2]={tab3[^2]}");

        // Właściwości
        Console.WriteLine($"Length={tab3.Length}, Rank={tab3.Rank}");

        // tab3[5] ← IndexOutOfRangeException w runtime!
        // Zawsze waliduj: if (i >= 0 && i < tab.Length)

        // Iteracja
        Console.Write("for:     ");
        for (int i = 0; i < tab3.Length; i++) Console.Write($"[{i}]={tab3[i]} ");
        Console.WriteLine();

        Console.Write("foreach: ");
        foreach (int n in tab3) Console.Write($"{n} ");
        Console.WriteLine();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ZAKRESY, KOPIOWANIE I METODY KLASY ARRAY
    // ─────────────────────────────────────────────────────────────────────────

    public static void TabliceOperacje()
    {
        Console.WriteLine("\n=== TABLICE — OPERACJE ===");

        int[] dane = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        // Ranges (C# 8+) — tworzą NOWĄ tablicę (kopię)
        int[] srodek    = dane[1..4];    // { 20, 30, 40 } — 4 jest excluded
        int[] odTrzeciego = dane[3..];   // { 40, 50, ..., 100 }
        int[] pierwsze3 = dane[..3];     // { 10, 20, 30 }
        int[] ostatnie2 = dane[^2..];    // { 90, 100 }
        Console.WriteLine($"[1..4]:  {string.Join(",", srodek)}");
        Console.WriteLine($"[^2..]:  {string.Join(",", ostatnie2)}");

        // Kopiowanie
        int[] zrodlo = { 1, 2, 3, 4, 5 };
        int[] kopia1 = (int[])zrodlo.Clone();        // Clone() → rzutowanie potrzebne
        int[] kopia2 = zrodlo.ToArray();             // LINQ — czytelniejsze
        int[] kopia3 = new int[zrodlo.Length];
        Array.Copy(zrodlo, kopia3, zrodlo.Length);  // precyzyjna kontrola

        kopia1[0] = 999;
        Console.WriteLine($"Oryginał po zmianie kopii: {zrodlo[0]}");  // 1 — niezmieniony

        // Sort, Reverse
        int[] doSort = { 5, 2, 8, 1, 9, 3 };
        Array.Sort(doSort);    // IN PLACE, modyfikuje oryginał!
        Console.WriteLine($"Sort: {string.Join(", ", doSort)}");    // 1,2,3,5,8,9
        Array.Reverse(doSort);
        Console.WriteLine($"Reverse: {string.Join(", ", doSort)}"); // 9,8,5,3,2,1

        // Sort z komparatorem
        string[] imiona = { "Zosia", "Ania", "Michał", "Bartek" };
        Array.Sort(imiona, (a, b) => a.Length.CompareTo(b.Length));
        Console.WriteLine($"Sort wg długości: {string.Join(", ", imiona)}");

        // BinarySearch — O(log n), tablica MUSI być posortowana!
        int[] pos = { 1, 3, 5, 7, 9, 11 };
        int idx = Array.BinarySearch(pos, 7);
        Console.WriteLine($"BinarySearch(7): idx={idx}");  // 3

        // Fill, Clear
        int[] tabl = new int[5];
        Array.Fill(tabl, 42);         // { 42, 42, 42, 42, 42 }
        Array.Clear(tabl, 1, 3);      // zeruj indeksy 1,2,3 → { 42, 0, 0, 0, 42 }
        Console.WriteLine($"Fill+Clear: {string.Join(",", tabl)}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TABLICE WIELOWYMIAROWE — rectangular (int[,]) i jagged (int[][])
    // ─────────────────────────────────────────────────────────────────────────

    public static void TabliceWielowymiarowe()
    {
        Console.WriteLine("\n=== TABLICE WIELOWYMIAROWE ===");

        // Rectangular — JEDEN ciągły blok pamięci, stałe długości wierszy
        int[,] szachownica = {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 }
        };
        Console.WriteLine($"Rectangular [1,2]={szachownica[1, 2]}");    // 6
        Console.WriteLine($"Wiersze={szachownica.GetLength(0)}, Kolumny={szachownica.GetLength(1)}");

        Console.WriteLine("Iteracja rectangular:");
        for (int i = 0; i < szachownica.GetLength(0); i++)
        {
            for (int j = 0; j < szachownica.GetLength(1); j++)
                Console.Write($"{szachownica[i, j],3}");
            Console.WriteLine();
        }

        // Jagged — tablica tablic, każdy wiersz może mieć INNĄ długość
        // Lepszy cache locality przy iteracji po wierszu, elastyczny rozmiar
        int[][] jagged = new int[3][];
        jagged[0] = new[] { 1, 2, 3 };
        jagged[1] = new[] { 4, 5 };          // różna długość!
        jagged[2] = new[] { 6, 7, 8, 9, 10 };

        Console.WriteLine($"Jagged [0][2]={jagged[0][2]}, [2].Length={jagged[2].Length}");

        Console.WriteLine("Iteracja jagged:");
        foreach (int[] wiersz in jagged)
        {
            foreach (int el in wiersz) Console.Write($"{el} ");
            Console.WriteLine();
        }

        // Kiedy co:
        // Rectangular: macierze matematyczne, dane tabelaryczne stałego rozmiaru
        // Jagged:      dane o zmiennym rozmiarze, LINQ działa naturalnie (int[][] to IEnumerable<int[]>)
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SPAN<T> — okno na pamięć BEZ kopiowania
    // Stack-only (ref struct), działa na tablicach, stringach, stackalloc
    // ─────────────────────────────────────────────────────────────────────────

    public static void SpanT()
    {
        Console.WriteLine("\n=== SPAN<T> — ZERO KOPIOWANIA ===");

        int[] dane = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        // Span jako WIDOK na fragment tablicy — zero alokacji
        Span<int> srodek = dane.AsSpan(2, 5);  // elementy [2..7)
        srodek[0] = 999;                         // modyfikuje ORYGINAŁ — to widok!
        Console.WriteLine($"Oryginał dane[2] po zmianie przez Span: {dane[2]}");  // 999

        // ReadOnlySpan — tylko odczyt, bezpieczniejsze
        PrzetworzDane(dane.AsSpan(1, 3));

        // Span na stringu — wydajne parsowanie bez alokacji
        string csv = "Kacper,25,Warszawa";
        ReadOnlySpan<char> tekst = csv.AsSpan();
        int przecinek = tekst.IndexOf(',');
        ReadOnlySpan<char> imie = tekst[..przecinek]; // "Kacper" — zero alokacji!
        Console.WriteLine($"Imię z Span: {imie.ToString()}");

        // stackalloc — dane na stosie, zero heap allocation
        PrzetworzDane(stackalloc int[] { 1, 2, 3 }); // zero heap!
    }

    private static void PrzetworzDane(ReadOnlySpan<int> dane)
    {
        int suma = 0;
        foreach (int n in dane) suma += n;
        Console.WriteLine($"Suma przez Span: {suma}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LIST<T> — dynamiczna lista (najczęściej używana kolekcja)
    // Pod maską: tablica która automatycznie rośnie (x2 gdy pełna)
    // Add = O(1) amortyzowane, Remove/Contains = O(n)
    // ─────────────────────────────────────────────────────────────────────────

    public static void ListaT()
    {
        Console.WriteLine("\n=== LIST<T> ===");

        var lista = new List<int> { 1, 2, 3, 4, 5 };

        // Capacity — warto ustawiać z góry gdy znasz rozmiar
        var zKapacita = new List<int>(capacity: 1000);  // bez resizowania

        // Dodawanie
        lista.Add(6);
        lista.AddRange(new[] { 7, 8, 9 });
        lista.Insert(0, 0);            // wstaw na pozycję 0
        Console.WriteLine($"Po Add/Insert: {string.Join(",", lista)}");

        // Usuwanie
        lista.Remove(9);               // usuń PIERWSZE wystąpienie wartości 9
        lista.RemoveAt(0);             // usuń element na indeksie 0
        lista.RemoveAll(x => x % 2 == 0);  // usuń wszystkie parzyste
        Console.WriteLine($"Po Remove: {string.Join(",", lista)}");

        // Wyszukiwanie i dostęp
        var lst = new List<string> { "Ania", "Bartek", "Celina", "Ania" };
        Console.WriteLine($"Count={lst.Count}, [0]={lst[0]}");
        Console.WriteLine($"Contains('Bartek')={lst.Contains("Bartek")}");
        Console.WriteLine($"IndexOf('Ania')={lst.IndexOf("Ania")}");        // pierwsze
        Console.WriteLine($"LastIndexOf('Ania')={lst.LastIndexOf("Ania")}"); // ostatnie
        Console.WriteLine($"Find (startsWith C): {lst.Find(s => s.StartsWith("C"))}");

        string? znaleziony = lst.Find(s => s.StartsWith("C"));
        Console.WriteLine($"Find('C...'): {znaleziony}");

        // Sortowanie
        lst.Sort();                                       // domyślne
        lst.Sort((a, b) => a.Length.CompareTo(b.Length)); // po długości
        Console.WriteLine($"Sort wg długości: {string.Join(",", lst)}");

        // Konwersje
        int[] tablica = lista.ToArray();
        var zTablicy  = new List<int>(tablica);

        // Jak działa pod maską — capacity
        var rosnaca = new List<int>();
        Console.WriteLine($"Capacity startowy: {rosnaca.Capacity}");  // 0
        rosnaca.Add(1);
        Console.WriteLine($"Capacity po 1 Add: {rosnaca.Capacity}");  // 4
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DICTIONARY<TKey, TValue> — hash table, O(1) dostęp
    // Wymaga GetHashCode() i Equals() na kluczu
    // ─────────────────────────────────────────────────────────────────────────

    public static void SlownikDictionary()
    {
        Console.WriteLine("\n=== DICTIONARY<K,V> ===");

        // Dwa style inicjalizatora — nie można ich MIESZAĆ w jednym bloku!
        var oceny = new Dictionary<string, int>
        {
            { "Matematyka", 5 },    // collection initializer syntax
            { "Polski",     4 },
            { "Angielski",  5 }
        };
        // Index initializer syntax (alternatywa, nie do mieszania z powyższym):
        // var oceny2 = new Dictionary<string, int> { ["Matematyka"] = 5, ["Polski"] = 4 };

        // Dodawanie i modyfikacja
        oceny.Add("Fizyka", 3);       // rzuca wyjątek jeśli klucz istnieje!
        oceny["Chemia"] = 4;          // dodaje lub NADPISUJE — bezpieczniejsze
        oceny["Matematyka"] = 6;      // nadpisanie

        // Odczyt — PUŁAPKA: oceny["Biologia"] ← KeyNotFoundException!
        // Bezpieczny odczyt — TryGetValue (ZALECANE)
        if (oceny.TryGetValue("Matematyka", out int mat))
            Console.WriteLine($"Matematyka: {mat}");

        if (!oceny.TryGetValue("Biologia", out int bio))
            Console.WriteLine("Brak biologii");

        // GetValueOrDefault (C# 8+)
        int ang = oceny.GetValueOrDefault("Angielski", 0);  // 5
        int ger = oceny.GetValueOrDefault("Niemiecki", 0);  // 0 — domyślna
        Console.WriteLine($"Angielski={ang}, Niemiecki={ger}");

        // Usuwanie
        oceny.Remove("Fizyka");

        // Iteracja przez dekonstrukcję
        Console.WriteLine("Oceny:");
        foreach (var (przedmiot, ocena) in oceny)
            Console.WriteLine($"  {przedmiot}: {ocena}");

        // Tylko klucze / tylko wartości
        Console.WriteLine($"Klucze:    {string.Join(", ", oceny.Keys)}");
        Console.WriteLine($"Wartości:  {string.Join(", ", oceny.Values)}");
        Console.WriteLine($"Count={oceny.Count}, ContainsKey={oceny.ContainsKey("Polski")}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HASHSET<T> — zbiór unikalnych wartości, O(1) dla Add/Contains/Remove
    // Jak Dictionary ale tylko klucze, bez wartości
    // ─────────────────────────────────────────────────────────────────────────

    public static void HashSetT()
    {
        Console.WriteLine("\n=== HASHSET<T> ===");

        var zbior = new HashSet<int> { 1, 2, 3, 4, 5 };
        zbior.Add(3);   // ignorowane — 3 już jest!
        zbior.Add(6);
        Console.WriteLine($"Count={zbior.Count}, Contains(3)={zbior.Contains(3)}");

        // Operacje zbiorowe — MODYFIKUJĄ oryginał!
        var a = new HashSet<int> { 1, 2, 3, 4, 5 };
        var b = new HashSet<int> { 4, 5, 6, 7, 8 };

        var czescWspolna = new HashSet<int>(a);
        czescWspolna.IntersectWith(b);       // część wspólna: { 4, 5 }
        Console.WriteLine($"Intersection: {string.Join(",", czescWspolna)}");

        var suma = new HashSet<int>(a);
        suma.UnionWith(b);                   // suma: { 1,2,3,4,5,6,7,8 }
        Console.WriteLine($"Union:        {string.Join(",", suma)}");

        var roznica = new HashSet<int>(a);
        roznica.ExceptWith(b);               // a - b: { 1,2,3 }
        Console.WriteLine($"Except:       {string.Join(",", roznica)}");

        // Klasyczne zastosowanie — usunięcie duplikatów z listy
        var lista = new List<int> { 1, 2, 2, 3, 3, 3, 4 };
        var unikalne = new HashSet<int>(lista);
        Console.WriteLine($"Bez duplikatów: {string.Join(",", unikalne)}");

        // HashSet vs List — kiedy co?
        // HashSet: Contains = O(1), chcesz unikalnych, operacje zbiorowe
        // List: Contains = O(n), potrzebujesz indeksu, chcesz duplikatów
    }

    // ─────────────────────────────────────────────────────────────────────────
    // QUEUE<T> — FIFO (First In, First Out) | STACK<T> — LIFO (Last In, First Out)
    // ─────────────────────────────────────────────────────────────────────────

    public static void QueueIStack()
    {
        Console.WriteLine("\n=== QUEUE<T> i STACK<T> ===");

        // QUEUE — kolejka FIFO (jak kolejka w sklepie)
        var kolejka = new Queue<string>();
        kolejka.Enqueue("Ania");    // dodaj na koniec
        kolejka.Enqueue("Bartek");
        kolejka.Enqueue("Celina");

        Console.WriteLine($"Peek (nie usuwa): {kolejka.Peek()}");     // "Ania"
        Console.WriteLine($"Dequeue: {kolejka.Dequeue()}");           // "Ania"
        Console.WriteLine($"Dequeue: {kolejka.Dequeue()}");           // "Bartek"
        Console.WriteLine($"Count po 2x Dequeue: {kolejka.Count}");  // 1

        if (kolejka.TryDequeue(out string? osoba))
            Console.WriteLine($"TryDequeue: {osoba}");                // "Celina"

        // STACK — stos LIFO (jak stos talerzy — ostatni → pierwszy)
        var stos = new Stack<int>();
        stos.Push(1);   // połóż na stos
        stos.Push(2);
        stos.Push(3);

        Console.WriteLine($"Peek (szczyt): {stos.Peek()}");  // 3
        Console.WriteLine($"Pop: {stos.Pop()}");              // 3
        Console.WriteLine($"Pop: {stos.Pop()}");              // 2
        Console.WriteLine($"Count: {stos.Count}");            // 1

        // Praktyczne zastosowanie stosu — sprawdzanie nawiasów
        string test1 = "({[]})";
        string test2 = "({[})";
        Console.WriteLine($"{test1} poprawne: {CzyNawiasyPoprawne(test1)}");  // True
        Console.WriteLine($"{test2} błędne:   {CzyNawiasyPoprawne(test2)}");  // False

        // Zastosowania:
        // Queue: przetwarzanie zadań, BFS w grafach, bufory asynchroniczne
        // Stack: cofanie operacji (Ctrl+Z), DFS w grafach, parsowanie wyrażeń, historia nawigacji
    }

    private static bool CzyNawiasyPoprawne(string tekst)
    {
        var stos = new Stack<char>();
        foreach (char c in tekst)
        {
            if (c == '(' || c == '[' || c == '{')
                stos.Push(c);
            else if (c == ')' || c == ']' || c == '}')
            {
                if (stos.Count == 0) return false;
                char otw = stos.Pop();
                if ((c == ')' && otw != '(') ||
                    (c == ']' && otw != '[') ||
                    (c == '}' && otw != '{')) return false;
            }
        }
        return stos.Count == 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRAKTYCZNY PRZYKŁAD — system biblioteki łączący wiele kolekcji
    // ─────────────────────────────────────────────────────────────────────────

    public static void PraktycznyPrzykladBiblioteka()
    {
        Console.WriteLine("\n=== PRAKTYCZNY PRZYKŁAD: BIBLIOTEKA ===");

        var lib = new Biblioteka();
        lib.DodajKsiazke("C# in Depth");
        lib.DodajKsiazke("Clean Code");
        lib.DodajKsiazke("Design Patterns");

        Console.WriteLine($"Wypożycz 'C# in Depth' (Ania):   {lib.Wypozycz("C# in Depth", "Ania")}");     // True
        Console.WriteLine($"Wypożycz 'C# in Depth' (Bartek): {lib.Wypozycz("C# in Depth", "Bartek")}");   // False — zajęta

        lib.Zwroc("C# in Depth", "Ania");
        Console.WriteLine($"Wypożycz 'C# in Depth' (Bartek): {lib.Wypozycz("C# in Depth", "Bartek")}");   // True

        Console.WriteLine($"Wypożyczone przez Bartek: {string.Join(", ", lib.GetWypozyczone("Bartek"))}");
    }
}

// Klasa pomocnicza — poza statyczną klasą TableiceIKolekcje
internal class Biblioteka
{
    private readonly List<string>                   _ksiazki      = new();
    private readonly Dictionary<string, List<string>> _wypozyczenia = new();
    private readonly HashSet<string>                _dostepne     = new();

    public void DodajKsiazke(string tytul)
    {
        _ksiazki.Add(tytul);
        _dostepne.Add(tytul);
    }

    public bool Wypozycz(string tytul, string czytelnik)
    {
        if (!_dostepne.Contains(tytul)) return false;
        _dostepne.Remove(tytul);
        if (!_wypozyczenia.ContainsKey(czytelnik))
            _wypozyczenia[czytelnik] = new List<string>();
        _wypozyczenia[czytelnik].Add(tytul);
        return true;
    }

    public void Zwroc(string tytul, string czytelnik)
    {
        if (_wypozyczenia.TryGetValue(czytelnik, out var lista))
            lista.Remove(tytul);
        _dostepne.Add(tytul);
    }

    public List<string> GetWypozyczone(string czytelnik) =>
        _wypozyczenia.GetValueOrDefault(czytelnik, new List<string>());
}
