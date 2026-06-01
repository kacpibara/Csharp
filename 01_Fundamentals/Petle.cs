namespace _01_Fundamentals;

public static class Petle
{
    // ─────────────────────────────────────────────────────────────────────────
    // FOR — gdy znasz liczbę iteracji lub potrzebujesz indeksu
    // ─────────────────────────────────────────────────────────────────────────

    public static void PetlaFor()
    {
        Console.WriteLine("\n=== PĘTLA FOR ===");

        // Klasyczna pętla — for (inicjalizacja; warunek; krok)
        Console.Write("0-4: ");
        for (int i = 0; i < 5; i++)
            Console.Write($"{i} ");
        Console.WriteLine();

        // Iteracja wsteczna
        Console.Write("10→0: ");
        for (int i = 10; i >= 0; i--)
            Console.Write($"{i} ");
        Console.WriteLine();

        // Krok co 2
        Console.Write("Parzyste: ");
        for (int i = 0; i < 20; i += 2)
            Console.Write($"{i} ");
        Console.WriteLine();

        // Tablice z indeksem
        string[] imiona = { "Kacper", "Ania", "Michał", "Zosia" };
        for (int i = 0; i < imiona.Length; i++)
            Console.WriteLine($"  [{i}] = {imiona[i]}");

        // Pętla zagnieżdżona — tabliczka mnożenia
        Console.WriteLine("Tabliczka 3x3:");
        for (int i = 1; i <= 3; i++)
        {
            for (int j = 1; j <= 3; j++)
                Console.Write($"{i * j,3}");
            Console.WriteLine();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WHILE — gdy nie wiesz z góry ile iteracji (może się nie wykonać ani razu)
    // ─────────────────────────────────────────────────────────────────────────

    public static void PetlaWhile()
    {
        Console.WriteLine("\n=== PĘTLA WHILE ===");

        // Klasyczne while — sprawdza warunek PRZED każdą iteracją
        int n = 1;
        Console.Write("1-5: ");
        while (n <= 5)
        {
            Console.Write($"{n} ");
            n++;   // pamiętaj o zmianie warunku — inaczej pętla nieskończona!
        }
        Console.WriteLine();

        // Wzorzec: pętla nieskończona z break
        int krok = 0;
        while (true)
        {
            krok++;
            if (krok >= 5) break;
        }
        Console.WriteLine($"Wyszedłem po {krok} krokach");

        // Zliczanie cyfr w liczbie — nie wiadomo z góry ile iteracji
        int liczba = 123456;
        int cyfry  = 0;
        int temp   = liczba;
        while (temp > 0)
        {
            cyfry++;
            temp /= 10;
        }
        Console.WriteLine($"Liczba {liczba} ma {cyfry} cyfr");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DO-WHILE — wykonuje się co najmniej RAZ (warunek po)
    // ─────────────────────────────────────────────────────────────────────────

    public static void PetlaDoWhile()
    {
        Console.WriteLine("\n=== PĘTLA DO-WHILE ===");

        // Warunek sprawdzany PO wykonaniu — minimum jedna iteracja
        int x = 10;
        Console.Write("do-while (x=10, warunek x<5): ");
        do
        {
            Console.Write($"{x} ");  // wypisze 10 mimo że warunek od razu false!
            x++;
        } while (x < 5);
        Console.WriteLine();

        // Klasyczne zastosowanie — menu, walidacja (zawsze pokaż opcje przynajmniej raz)
        Console.WriteLine("Symulacja menu (wybory: 1,2,3):");
        int[] symulowaneWybory = { 5, 2 };  // zły wybór, potem dobry
        int indeks = 0;
        int wybor;
        do
        {
            wybor = symulowaneWybory[indeks++];
            if (wybor < 1 || wybor > 3)
                Console.WriteLine($"  Nieprawidłowy wybór: {wybor}. Zakres 1-3.");
        } while ((wybor < 1 || wybor > 3) && indeks < symulowaneWybory.Length);

        Console.WriteLine($"  Wybrano: {wybor}");

        // Porównanie while vs do-while
        int y = 100;
        Console.Write("while (y=100, warunek y<5): ");
        while (y < 5) Console.Write("NIE WYKONA SIĘ ");
        Console.WriteLine("(pominięte)");

        Console.Write("do-while (y=100, warunek y<5): ");
        do { Console.Write("wykona się raz! "); } while (y < 5);
        Console.WriteLine();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FOREACH — iteracja po kolekcjach (najczęściej używana)
    // ─────────────────────────────────────────────────────────────────────────

    public static void PetlaForeach()
    {
        Console.WriteLine("\n=== PĘTLA FOREACH ===");

        // Tablice
        int[] liczby = { 1, 2, 3, 4, 5 };
        Console.Write("Tablica: ");
        foreach (int l in liczby)
            Console.Write($"{l} ");
        Console.WriteLine();

        // Lista
        var imiona = new List<string> { "Ania", "Bartek", "Celina" };
        foreach (string imie in imiona)
            Console.Write($"{imie.ToUpper()} ");
        Console.WriteLine();

        // Słownik — każdy element to para klucz-wartość
        var stolice = new Dictionary<string, string>
        {
            { "Polska",  "Warszawa" },
            { "Niemcy",  "Berlin" },
            { "Francja", "Paryż" }
        };

        foreach (KeyValuePair<string, string> para in stolice)
            Console.WriteLine($"  {para.Key} → {para.Value}");

        // Dekonstrukcja pary (C# 7+) — czystszy zapis
        foreach (var (kraj, stolica) in stolice)
            Console.WriteLine($"  {kraj} → {stolica}");

        // PUŁAPKA: nie modyfikuj kolekcji podczas foreach!
        var lista = new List<int> { 1, 2, 3, 4, 5 };
        // foreach (var item in lista) { lista.Remove(item); } ← InvalidOperationException!

        // Prawidłowo — usuń parzyste iterując od końca z for
        for (int i = lista.Count - 1; i >= 0; i--)
            if (lista[i] % 2 == 0) lista.RemoveAt(i);
        Console.Write("Nieparzyste: ");
        foreach (int n in lista) Console.Write($"{n} ");
        Console.WriteLine();

        // Alternatywa — RemoveAll
        var lista2 = new List<int> { 1, 2, 3, 4, 5 };
        lista2.RemoveAll(el => el % 2 == 0);
        Console.Write("RemoveAll: ");
        foreach (int n in lista2) Console.Write($"{n} ");
        Console.WriteLine();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BREAK, CONTINUE — kontrola przepływu
    // ─────────────────────────────────────────────────────────────────────────

    public static void BreakIContinue()
    {
        Console.WriteLine("\n=== BREAK I CONTINUE ===");

        // break — natychmiastowe wyjście z pętli
        Console.Write("break przy i==5: ");
        for (int i = 0; i < 100; i++)
        {
            if (i == 5) break;
            Console.Write($"{i} ");
        }
        Console.WriteLine();  // 0 1 2 3 4

        // Szukanie pierwszego elementu > 8
        int[] dane = { 3, 7, 2, 9, 1, 5 };
        foreach (int n in dane)
        {
            if (n > 8)
            {
                Console.WriteLine($"Pierwsza liczba >8: {n}");
                break;
            }
        }

        // continue — pomiń resztę iteracji, idź do następnej
        Console.Write("Nieparzyste (continue): ");
        for (int i = 0; i < 10; i++)
        {
            if (i % 2 == 0) continue;   // pomiń parzyste
            Console.Write($"{i} ");
        }
        Console.WriteLine();  // 1 3 5 7 9

        // Break w zagnieżdżonych pętlach — wychodzi tylko z wewnętrznej!
        Console.Write("Zagnieżdżone (break z wewnętrznej): ");
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (j == 1) break;           // wychodzi tylko z j-pętli
                Console.Write($"({i},{j}) ");
            }
        }
        Console.WriteLine();  // (0,0) (1,0) (2,0)

        // Wyjście z obu pętli — flaga bool
        bool znaleziono = false;
        int si = -1, sj = -1;
        for (int i = 0; i < 5 && !znaleziono; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (i == 2 && j == 3) { znaleziono = true; si = i; sj = j; break; }
            }
        }
        Console.WriteLine($"Znaleziono (2,3) = {znaleziono} na [{si},{sj}]");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ZAKRESY I INDEKSY (C# 8+) — operator ^ i ..
    // ─────────────────────────────────────────────────────────────────────────

    public static void ZakresByIIndeksy()
    {
        Console.WriteLine("\n=== ZAKRESY I INDEKSY (C# 8+) ===");

        int[] liczby = { 10, 20, 30, 40, 50 };

        // Indeks od końca — operator ^
        Console.WriteLine($"liczby[^1] = {liczby[^1]}");  // 50 — ostatni
        Console.WriteLine($"liczby[^2] = {liczby[^2]}");  // 40 — przedostatni

        // Range — operator ..
        int[] srodek    = liczby[1..4];   // { 20, 30, 40 } — 4 jest excluded
        int[] odDrugiego = liczby[2..];   // { 30, 40, 50 }
        int[] doTrzeciego = liczby[..3];  // { 10, 20, 30 }
        int[] ostatnie2  = liczby[^2..];  // { 40, 50 }

        Console.WriteLine($"[1..4]:  {string.Join(",", srodek)}");
        Console.WriteLine($"[2..]:   {string.Join(",", odDrugiego)}");
        Console.WriteLine($"[..3]:   {string.Join(",", doTrzeciego)}");
        Console.WriteLine($"[^2..]:  {string.Join(",", ostatnie2)}");

        // WAŻNE: range tworzy NOWĄ tablicę, nie widok
        srodek[0] = 999;
        Console.WriteLine($"Oryginał niezmieniony: liczby[1]={liczby[1]}");  // 20

        // foreach z indeksem — użyj Select z LINQ
        Console.WriteLine("foreach z indeksem (Select):");
        foreach (var (indeks, wartosc) in liczby.Select((v, i) => (i, v)))
            Console.WriteLine($"  [{indeks}] = {wartosc}");
    }
}
