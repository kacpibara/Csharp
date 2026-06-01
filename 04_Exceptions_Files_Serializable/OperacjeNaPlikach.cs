using System.Runtime.CompilerServices;
using System.Text;

namespace _04_Exceptions_Files_Serializable;

// Helper classes - OP suffix to avoid naming conflicts across namespace
public class FolderRaportOP
{
    public string Sciezka { get; set; } = "";
    public int LiczbaPlikov { get; set; }
    public int LiczbaFolderow { get; set; }
    public long CalkowitaWielkosc { get; set; }
    public string? NajwiekszPlik { get; set; }
}

public class PlikSerwisDemoOP
{
    private readonly string _katalog;
    private readonly Dictionary<string, string> _cache = new();

    public PlikSerwisDemoOP(string katalog)
    {
        _katalog = katalog;
        Directory.CreateDirectory(katalog);
    }

    public async Task ZapiszAsync(string nazwaPliku, string zawartosc)
    {
        if (string.IsNullOrWhiteSpace(nazwaPliku))
            throw new ArgumentException("Nazwa pliku nie moze byc pusta", nameof(nazwaPliku));

        var sciezka = Path.Combine(_katalog, nazwaPliku);
        var tmpSciezka = sciezka + ".tmp";

        // atomowy zapis: najpierw .tmp, potem Move
        await File.WriteAllTextAsync(tmpSciezka, zawartosc, Encoding.UTF8);
        File.Move(tmpSciezka, sciezka, overwrite: true);
        _cache.Remove(nazwaPliku); // uniewnaznienie cache po zapisie
    }

    public async Task<string> CzytajTekstAsync(string nazwaPliku)
    {
        if (_cache.TryGetValue(nazwaPliku, out var cached))
        {
            Console.WriteLine($"  [cache hit] {nazwaPliku}");
            return cached;
        }

        var sciezka = Path.Combine(_katalog, nazwaPliku);
        if (!File.Exists(sciezka))
            throw new FileNotFoundException($"Plik {nazwaPliku} nie istnieje", sciezka);

        var zawartosc = await File.ReadAllTextAsync(sciezka, Encoding.UTF8);
        _cache[nazwaPliku] = zawartosc;
        return zawartosc;
    }

    public FolderRaportOP AnalizujFolder()
    {
        var info = new DirectoryInfo(_katalog);
        var pliki = info.GetFiles("*", SearchOption.AllDirectories);
        var foldery = info.GetDirectories("*", SearchOption.AllDirectories);

        return new FolderRaportOP
        {
            Sciezka = _katalog,
            LiczbaPlikov = pliki.Length,
            LiczbaFolderow = foldery.Length,
            CalkowitaWielkosc = pliki.Sum(f => f.Length),
            NajwiekszPlik = pliki.OrderByDescending(f => f.Length).FirstOrDefault()?.Name
        };
    }
}

public static class OperacjeNaPlikach
{
    // 1. File static class - wszystkie podstawowe operacje
    public static void DemoFileStatic()
    {
        Console.WriteLine("\n--- File static class ---");
        var tmp = Path.GetTempPath();
        var plik = Path.Combine(tmp, "demo_plik.txt");
        var plikKopia = Path.Combine(tmp, "demo_plik_kopia.txt");
        var plikPrzeniesiony = Path.Combine(tmp, "demo_plik_przeniesiony.txt");
        var plikBinarny = Path.Combine(tmp, "demo_binarny.bin");
        var plikLinijki = Path.Combine(tmp, "demo_linijki.txt");

        try
        {
            // WriteAllText / ReadAllText
            File.WriteAllText(plik, "Pierwsza linia\nDruga linia\nTrzecia linia", Encoding.UTF8);
            string zawartosc = File.ReadAllText(plik, Encoding.UTF8);
            Console.WriteLine($"ReadAllText: {zawartosc.Split('\n').Length} linii");

            // AppendAllText - dopisuje na koniec
            File.AppendAllText(plik, "\nDodana linia");

            // WriteAllLines / ReadAllLines
            File.WriteAllLines(plikLinijki, ["Linia 1", "Linia 2", "Linia 3"], Encoding.UTF8);
            string[] linijki = File.ReadAllLines(plikLinijki, Encoding.UTF8);
            Console.WriteLine($"ReadAllLines: {linijki.Length} linii");

            // ReadLines - leniwe, IEnumerable<string>, nie wczytuje wszystkiego do pamieci
            foreach (var linijka in File.ReadLines(plikLinijki, Encoding.UTF8))
            {
                _ = linijka; // przetwarza linia po linii
                break;
            }
            Console.WriteLine("ReadLines (lazy IEnumerable): pierwsze wywolanie");

            // WriteAllBytes / ReadAllBytes
            byte[] bajty = [0x48, 0x65, 0x6C, 0x6C, 0x6F]; // "Hello"
            File.WriteAllBytes(plikBinarny, bajty);
            byte[] odczytane = File.ReadAllBytes(plikBinarny);
            Console.WriteLine($"ReadAllBytes: {odczytane.Length} bajtow = '{Encoding.ASCII.GetString(odczytane)}'");

            // Exists / Copy / Move / Delete
            Console.WriteLine($"Exists: {File.Exists(plik)}");
            File.Copy(plik, plikKopia, overwrite: true);
            Console.WriteLine($"Copy ok, kopia istnieje: {File.Exists(plikKopia)}");
            File.Move(plikKopia, plikPrzeniesiony, overwrite: true);
            Console.WriteLine($"Move ok, przeniesiony istnieje: {File.Exists(plikPrzeniesiony)}");

            // FileInfo - metadane pliku
            var info = new FileInfo(plik);
            Console.WriteLine($"FileInfo: Name={info.Name}, Length={info.Length}B, " +
                              $"LastWriteTime={info.LastWriteTime:HH:mm:ss}, " +
                              $"Extension={info.Extension}");
        }
        finally
        {
            foreach (var f in new[] { plik, plikKopia, plikPrzeniesiony, plikBinarny, plikLinijki })
                if (File.Exists(f)) File.Delete(f);
        }
    }

    // 2. File static async - asynchroniczne odpowiedniki
    public static async Task DemoFileStaticAsync()
    {
        Console.WriteLine("\n--- File static async ---");
        var tmp = Path.GetTempPath();
        var plik = Path.Combine(tmp, "demo_async.txt");
        var plikLinijki = Path.Combine(tmp, "demo_async_linijki.txt");
        var plikBinarny = Path.Combine(tmp, "demo_async_binarny.bin");

        try
        {
            // WriteAllTextAsync / ReadAllTextAsync
            await File.WriteAllTextAsync(plik, "Tresc pliku\nLinia 2\nLinia 3", Encoding.UTF8);
            string tekst = await File.ReadAllTextAsync(plik, Encoding.UTF8);
            Console.WriteLine($"WriteAllTextAsync/ReadAllTextAsync: {tekst.Length} znakow");

            // AppendAllTextAsync
            await File.AppendAllTextAsync(plik, "\nDolaczona linia");

            // WriteAllLinesAsync / ReadAllLinesAsync
            await File.WriteAllLinesAsync(plikLinijki, ["Async 1", "Async 2", "Async 3"], Encoding.UTF8);
            string[] linijki = await File.ReadAllLinesAsync(plikLinijki, Encoding.UTF8);
            Console.WriteLine($"WriteAllLinesAsync/ReadAllLinesAsync: {linijki.Length} linii");

            // WriteAllBytesAsync / ReadAllBytesAsync
            byte[] bajty = Encoding.UTF8.GetBytes("Dane binarne async");
            await File.WriteAllBytesAsync(plikBinarny, bajty);
            byte[] odczytane = await File.ReadAllBytesAsync(plikBinarny);
            Console.WriteLine($"WriteAllBytesAsync/ReadAllBytesAsync: {odczytane.Length} bajtow");
        }
        finally
        {
            foreach (var f in new[] { plik, plikLinijki, plikBinarny })
                if (File.Exists(f)) File.Delete(f);
        }
    }

    // 3. FileStream - tryby, dostep, Seek, chunked reading
    public static void DemoFileStream()
    {
        Console.WriteLine("\n--- FileStream ---");
        var plik = Path.Combine(Path.GetTempPath(), "demo_filestream.bin");

        try
        {
            // FileMode, FileAccess, FileShare, bufferSize, useAsync
            using (var fs = new FileStream(
                plik,
                FileMode.Create,        // Utwórz lub nadpisz istniejacy
                FileAccess.ReadWrite,   // Odczyt i zapis
                FileShare.None,         // Blokada - zadne inne procesy
                bufferSize: 4096,
                useAsync: false))
            {
                // Zapis - Write
                byte[] dane = Encoding.UTF8.GetBytes("ABCDEFGHIJKLMNOP");
                fs.Write(dane, 0, dane.Length);
                Console.WriteLine($"Write: {dane.Length}B, pozycja: {fs.Position}");

                // Seek - SeekOrigin.Begin
                fs.Seek(0, SeekOrigin.Begin);
                Console.WriteLine($"Seek(0, Begin) → pozycja: {fs.Position}");

                // Seek - SeekOrigin.Current
                fs.Seek(5, SeekOrigin.Current);
                Console.WriteLine($"Seek(5, Current) → pozycja: {fs.Position}");

                // Seek - SeekOrigin.End
                fs.Seek(-4, SeekOrigin.End);
                Console.WriteLine($"Seek(-4, End) → pozycja: {fs.Position}");

                // Read - odczyt fragmentu
                byte[] bufor = new byte[4];
                int odczytano = fs.Read(bufor, 0, bufor.Length);
                Console.WriteLine($"Read: {odczytano}B = '{Encoding.UTF8.GetString(bufor, 0, odczytano)}'");
            }

            // Chunked reading - czytaj duze pliki porcjami bez ladowania do pamieci
            Console.WriteLine("Chunked reading (porcje po 4B):");
            using (var fs = new FileStream(plik, FileMode.Open, FileAccess.Read))
            {
                byte[] bufor = new byte[4];
                int odczytano;
                int numer = 0;
                while ((odczytano = fs.Read(bufor, 0, bufor.Length)) > 0)
                {
                    string fragment = Encoding.UTF8.GetString(bufor, 0, odczytano);
                    Console.WriteLine($"  Porcja {++numer}: '{fragment}'");
                }
            }
        }
        finally
        {
            if (File.Exists(plik)) File.Delete(plik);
        }
    }

    // 4. StreamReader / StreamWriter - encoding, AutoFlush, EndOfStream, async
    public static async Task DemoStreamReaderWriter()
    {
        Console.WriteLine("\n--- StreamReader / StreamWriter ---");
        var plik = Path.Combine(Path.GetTempPath(), "demo_stream_rw.txt");

        try
        {
            // StreamWriter - encoding, AutoFlush
            using (var sw = new StreamWriter(plik, append: false, encoding: Encoding.UTF8))
            {
                sw.AutoFlush = false; // buforowanie - flush przy zamknieciu lub recznie
                await sw.WriteLineAsync("Linia pierwsza");
                await sw.WriteLineAsync("Linia druga");
                await sw.WriteLineAsync("Linia trzecia");
                await sw.FlushAsync(); // reczne wymusze zapisu bufora na dysk
                Console.WriteLine("StreamWriter: zapisano z AutoFlush=false + reczny FlushAsync");
            }

            // StreamReader - EndOfStream, async ReadLineAsync
            using (var sr = new StreamReader(plik, Encoding.UTF8))
            {
                Console.WriteLine("StreamReader ReadLineAsync:");
                int nr = 1;
                while (!sr.EndOfStream)
                {
                    string? linia = await sr.ReadLineAsync();
                    Console.WriteLine($"  {nr++}: {linia}");
                }
            }

            // StreamWriter - tryb append
            using (var sw = new StreamWriter(plik, append: true, encoding: Encoding.UTF8))
            {
                await sw.WriteLineAsync("Dolaczona linia (append mode)");
            }

            // ReadToEndAsync - caly plik naraz
            using (var sr = new StreamReader(plik, Encoding.UTF8))
            {
                string caly = await sr.ReadToEndAsync();
                Console.WriteLine($"ReadToEndAsync: {caly.Split('\n').Length} linii lacznie");
            }
        }
        finally
        {
            if (File.Exists(plik)) File.Delete(plik);
        }
    }

    // 5. Path static class - wszystkie metody
    public static void DemoPath()
    {
        Console.WriteLine("\n--- Path static class ---");
        string sciezka = Path.Combine("C:", "Users", "kacper", "Desktop", "projekt", "dane.json");

        // Rozkladanie sciezki na czesci
        Console.WriteLine($"GetFileName:                  {Path.GetFileName(sciezka)}");
        Console.WriteLine($"GetFileNameWithoutExtension:  {Path.GetFileNameWithoutExtension(sciezka)}");
        Console.WriteLine($"GetExtension:                 {Path.GetExtension(sciezka)}");
        Console.WriteLine($"GetDirectoryName:             {Path.GetDirectoryName(sciezka)}");
        Console.WriteLine($"GetPathRoot:                  {Path.GetPathRoot(sciezka)}");

        // Laczenie i normalizacja
        string polaczona = Path.Combine("katalog1", "podkatalog", "plik.txt");
        Console.WriteLine($"Combine (cross-platform):     {polaczona}");

        string pelna = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "..", "tmp_test"));
        Console.WriteLine($"GetFullPath (normalizacja):   {pelna}");

        Console.WriteLine($"IsPathRooted (absolutna):     {Path.IsPathRooted(sciezka)}");
        Console.WriteLine($"IsPathRooted (wzgledna):      {Path.IsPathRooted("relative/path")}");

        // Zmiana rozszerzenia
        string noweRozszerzenie = Path.ChangeExtension(sciezka, ".xml");
        Console.WriteLine($"ChangeExtension:              {Path.GetFileName(noweRozszerzenie)}");

        // Sciezki tymczasowe
        Console.WriteLine($"GetTempPath:                  {Path.GetTempPath()}");
        string tmpPlik = Path.GetTempFileName(); // tworzy pusty plik!
        Console.WriteLine($"GetTempFileName:              {Path.GetFileName(tmpPlik)}");
        File.Delete(tmpPlik); // GetTempFileName tworzy plik - trzeba posprzatac
        Console.WriteLine($"GetRandomFileName:            {Path.GetRandomFileName()} (nie tworzy pliku)");

        // Nieprawidlowe znaki
        char[] zleSciezka = Path.GetInvalidPathChars();
        char[] zlePlik = Path.GetInvalidFileNameChars();
        Console.WriteLine($"GetInvalidPathChars:          {zleSciezka.Length} znakow");
        Console.WriteLine($"GetInvalidFileNameChars:      {zlePlik.Length} znakow");

        // Bezpieczna nazwa pliku - usun nieprawidlowe znaki
        string niebezpieczna = "raport:z<niedozwolonymi>znakami?.csv";
        var invalidChars = Path.GetInvalidFileNameChars();
        string bezpieczna = string.Concat(niebezpieczna.Select(c => invalidChars.Contains(c) ? '_' : c));
        Console.WriteLine($"Bezpieczna nazwa:             '{bezpieczna}'");

        // Separatory
        Console.WriteLine($"DirectorySeparatorChar:       '{Path.DirectorySeparatorChar}'");
        Console.WriteLine($"PathSeparator:                '{Path.PathSeparator}'");
    }

    // 6. Directory - operacje na katalogach
    public static void DemoDirectory()
    {
        Console.WriteLine("\n--- Directory ---");
        var tmpDir = Path.GetTempPath();
        var testDir = Path.Combine(tmpDir, "demo_katalog_test");
        var podKatalog = Path.Combine(testDir, "podkatalog", "glebszy");
        var przeniesCel = Path.Combine(tmpDir, "demo_katalog_przeniesiony");

        try
        {
            // CreateDirectory - tworzy caly lancuch, idempotentne (nie rzuca wyjatku jesli istnieje)
            Directory.CreateDirectory(podKatalog);
            Console.WriteLine($"CreateDirectory (caly lancuch, idempotentne): ok");
            Console.WriteLine($"Exists: {Directory.Exists(testDir)}");

            // Przygotuj pliki testowe
            File.WriteAllText(Path.Combine(testDir, "plik1.txt"), "Tresc 1");
            File.WriteAllText(Path.Combine(testDir, "plik2.csv"), "Tresc 2");
            File.WriteAllText(Path.Combine(podKatalog, "plik3.txt"), "Tresc 3");

            // GetFiles - z wzorcem i SearchOption
            string[] plikiTxt = Directory.GetFiles(testDir, "*.txt", SearchOption.AllDirectories);
            string[] plikiWszystkie = Directory.GetFiles(testDir, "*", SearchOption.AllDirectories);
            Console.WriteLine($"GetFiles *.txt AllDirectories: {plikiTxt.Length} plikow");
            Console.WriteLine($"GetFiles * AllDirectories:     {plikiWszystkie.Length} plikow");

            // EnumerateFiles - leniwe, nie wczytuje listy do pamieci
            int licz = 0;
            foreach (var plik in Directory.EnumerateFiles(testDir, "*", SearchOption.AllDirectories))
            {
                licz++;
                _ = plik;
            }
            Console.WriteLine($"EnumerateFiles (lazy): {licz} plikow");

            // GetDirectories / EnumerateDirectories
            string[] podkatalogi = Directory.GetDirectories(testDir, "*", SearchOption.AllDirectories);
            Console.WriteLine($"GetDirectories AllDirectories: {podkatalogi.Length} katalogow");

            // DirectoryInfo - metadane katalogu
            var info = new DirectoryInfo(testDir);
            Console.WriteLine($"DirectoryInfo: Name={info.Name}, Parent={info.Parent?.Name}, " +
                              $"Created={info.CreationTime:HH:mm:ss}");

            // FileSystemInfo - wspolna baza FileInfo i DirectoryInfo
            Console.WriteLine("FileSystemInfo (GetFileSystemInfos):");
            foreach (FileSystemInfo fsi in info.GetFileSystemInfos())
            {
                string typ = fsi is DirectoryInfo ? "[DIR] " : "[FILE]";
                Console.WriteLine($"  {typ} {fsi.Name}");
            }

            // Directory.Move - przeniesienie calego katalogu
            if (Directory.Exists(przeniesCel)) Directory.Delete(przeniesCel, recursive: true);
            Directory.Move(testDir, przeniesCel);
            Console.WriteLine($"Directory.Move ok, istnieje: {Directory.Exists(przeniesCel)}");
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, recursive: true);
            if (Directory.Exists(przeniesCel)) Directory.Delete(przeniesCel, recursive: true);
        }
    }

    // 7. FileSystemWatcher - monitorowanie zmian w systemie plikow
    public static void DemoFileSystemWatcher()
    {
        Console.WriteLine("\n--- FileSystemWatcher ---");
        var watchDir = Path.Combine(Path.GetTempPath(), "demo_watcher");
        Directory.CreateDirectory(watchDir);
        var zdarzenia = new List<string>();

        try
        {
            using var watcher = new FileSystemWatcher(watchDir)
            {
                Filter = "*.txt",                     // obserwuj tylko pliki .txt
                NotifyFilter = NotifyFilters.FileName  // zmiana nazwy / nowy / usuniety
                             | NotifyFilters.LastWrite  // modyfikacja zawartosci
                             | NotifyFilters.Size,
                EnableRaisingEvents = false             // wlacz po rejestracji handlerow
            };

            // Rejestracja zdarzen
            watcher.Created += (_, e) => zdarzenia.Add($"CREATED: {e.Name}");
            watcher.Changed += (_, e) => zdarzenia.Add($"CHANGED: {e.Name}");
            watcher.Deleted += (_, e) => zdarzenia.Add($"DELETED: {e.Name}");
            watcher.Renamed += (_, e) => zdarzenia.Add($"RENAMED: {e.OldName} -> {e.Name}");
            watcher.Error   += (_, e) => zdarzenia.Add($"ERROR: {e.GetException().Message}");

            watcher.EnableRaisingEvents = true; // start obserwacji

            // Wygeneruj zdarzenia
            var plik = Path.Combine(watchDir, "test.txt");
            File.WriteAllText(plik, "tresc");
            File.AppendAllText(plik, " dopisana");
            var nowy = Path.Combine(watchDir, "po_zmianie.txt");
            File.Move(plik, nowy);
            File.Delete(nowy);

            System.Threading.Thread.Sleep(300); // daj watkom zdarzen czas na wykonanie

            Console.WriteLine($"FileSystemWatcher zarejestrowal {zdarzenia.Count} zdarzen:");
            foreach (var z in zdarzenia)
                Console.WriteLine($"  {z}");
        }
        finally
        {
            if (Directory.Exists(watchDir)) Directory.Delete(watchDir, recursive: true);
        }
    }

    // 8. Wzorzec atomowego zapisu - .tmp + Move
    public static void DemoAtomicWrite()
    {
        Console.WriteLine("\n--- Atomowy zapis (.tmp + Move) ---");
        var docelowy = Path.Combine(Path.GetTempPath(), "dane_produkcyjne.json");

        static void ZapiszAtomowo(string sciezka, string zawartosc)
        {
            var tmp = sciezka + ".tmp";
            try
            {
                // 1. Zapisz do pliku tymczasowego
                File.WriteAllText(tmp, zawartosc, Encoding.UTF8);
                // 2. Atomowe zastapienie - Move jest atomowy na tym samym woluminie
                //    Czytelnicy nigdy nie widza niepelnego pliku
                File.Move(tmp, sciezka, overwrite: true);
            }
            catch
            {
                if (File.Exists(tmp)) File.Delete(tmp);
                throw; // bare throw - zachowuje stack trace
            }
        }

        try
        {
            ZapiszAtomowo(docelowy, """{"wersja": 1, "dane": "oryginalne"}""");
            Console.WriteLine($"Atomowy zapis v1: {File.ReadAllText(docelowy)}");

            ZapiszAtomowo(docelowy, """{"wersja": 2, "dane": "zaktualizowane"}""");
            Console.WriteLine($"Atomowy zapis v2: {File.ReadAllText(docelowy)}");

            Console.WriteLine("Wzorzec: Write(.tmp) + Move = czytelnicy zawsze widza kompletna wersje");
        }
        finally
        {
            if (File.Exists(docelowy)) File.Delete(docelowy);
        }
    }

    // 9. CzytajZRetry - odczyt zablokowanych plikow z exponential backoff
    public static void DemoCzytajZRetry()
    {
        Console.WriteLine("\n--- CzytajZRetry (zablokowane pliki) ---");
        var plik = Path.Combine(Path.GetTempPath(), "demo_retry.txt");

        static string CzytajZRetry(string sciezka, int maxProby = 3, int opoznienieMs = 100)
        {
            for (int proba = 1; proba <= maxProby; proba++)
            {
                try
                {
                    return File.ReadAllText(sciezka, Encoding.UTF8);
                }
                catch (IOException) when (proba < maxProby)
                {
                    // IOException filter - nie odwija stosu jesli warunek false
                    Console.WriteLine($"  Proba {proba} nieudana, czekam {opoznienieMs}ms...");
                    System.Threading.Thread.Sleep(opoznienieMs);
                    opoznienieMs *= 2; // exponential backoff
                }
            }
            throw new IOException($"Nie udalo sie odczytac po {maxProby} probach: {sciezka}");
        }

        try
        {
            File.WriteAllText(plik, "Tresc do odczytu z mechanizmem retry");
            string wynik = CzytajZRetry(plik);
            Console.WriteLine($"CzytajZRetry: '{wynik}'");
            Console.WriteLine("Wzorzec: IOException filter + exponential backoff");
        }
        finally
        {
            if (File.Exists(plik)) File.Delete(plik);
        }
    }

    // 10. IAsyncEnumerable streaming - strumieniowe czytanie linii
    public static async Task DemoIAsyncEnumerable()
    {
        Console.WriteLine("\n--- IAsyncEnumerable streaming ---");
        var plik = Path.Combine(Path.GetTempPath(), "demo_async_enum.txt");

        try
        {
            // Przygotuj plik z wieloma liniami
            var linijki = Enumerable.Range(1, 10).Select(i => $"Linia {i}: wartosc={i * 100}");
            await File.WriteAllLinesAsync(plik, linijki, Encoding.UTF8);

            // Generowanie strumienia linii - IAsyncEnumerable<string>
            static async IAsyncEnumerable<string> CzytajLinieAsync(
                string sciezka,
                [EnumeratorCancellation] CancellationToken token = default)
            {
                using var sr = new StreamReader(sciezka, Encoding.UTF8);
                while (!sr.EndOfStream)
                {
                    token.ThrowIfCancellationRequested();
                    string? linia = await sr.ReadLineAsync(token);
                    if (linia != null) yield return linia;
                }
            }

            // await foreach - konsumuje strumien linia po linii bez ladowania calego pliku
            int przetworzone = 0;
            await foreach (var linia in CzytajLinieAsync(plik))
            {
                przetworzone++;
                if (przetworzone <= 3)
                    Console.WriteLine($"  Stream: {linia}");
            }
            Console.WriteLine($"  ... lacznie {przetworzone} linii przetworzone strumieniowo");
            Console.WriteLine("Wzorzec: yield return + await foreach = brak Buffer.All w pamieci");
        }
        finally
        {
            if (File.Exists(plik)) File.Delete(plik);
        }
    }

    // 11. BezpiecznaSciezka - ochrona przed path traversal attack
    public static void DemoBezpiecznaSciezka()
    {
        Console.WriteLine("\n--- BezpiecznaSciezka (path traversal protection) ---");

        static string BezpiecznaSciezka(string katalogBazowy, string nazwaPliku)
        {
            // Normalizuj sciezke bazowa (usuwa .., symlinki, normalizuje separatory)
            string baza = Path.GetFullPath(katalogBazowy)
                          + Path.DirectorySeparatorChar;

            // Polacz i normalizuj zadana sciezke
            string zadana = Path.GetFullPath(Path.Combine(katalogBazowy, nazwaPliku));

            // Path traversal: ../../etc/passwd po normalizacji wychodzi poza baze
            if (!zadana.StartsWith(baza, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException(
                    $"Proba dostepu poza dozwolonym katalogiem: '{nazwaPliku}'");

            return zadana;
        }

        static void ZapewnijFolder(string sciezka)
        {
            if (!Directory.Exists(sciezka))
                Directory.CreateDirectory(sciezka);
        }

        var bazowy = Path.Combine(Path.GetTempPath(), "bezpieczny_katalog");
        ZapewnijFolder(bazowy);

        try
        {
            // Poprawna sciezka - wewnaz katalogu bazowego
            string ok = BezpiecznaSciezka(bazowy, "raport.txt");
            Console.WriteLine($"OK - zwykla sciezka: {Path.GetFileName(ok)}");

            string okPodkat = BezpiecznaSciezka(bazowy, Path.Combine("podkatalog", "plik.txt"));
            Console.WriteLine($"OK - podkatalog: ...{okPodkat[bazowy.Length..]}");

            // Atak path traversal - proba wyjscia poza katalog bazowy
            try
            {
                BezpiecznaSciezka(bazowy, "../../etc/passwd");
                Console.WriteLine("BLAD: powinno rzucic wyjatek!");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Path traversal ZABLOKOWANY: {ex.Message}");
            }

            // Absolutna sciezka jako atak
            try
            {
                BezpiecznaSciezka(bazowy, @"C:\Windows\System32\drivers\etc\hosts");
                Console.WriteLine("BLAD: powinno rzucic wyjatek!");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Absolutna sciezka ZABLOKOWANA: {ex.Message}");
            }
        }
        finally
        {
            if (Directory.Exists(bazowy)) Directory.Delete(bazowy, recursive: true);
        }
    }

    // 12. PlikSerwisDemoOP - praktyczny serwis plikow
    public static async Task DemoPlikSerwis()
    {
        Console.WriteLine("\n--- PlikSerwisDemoOP ---");
        var katalog = Path.Combine(Path.GetTempPath(), "demo_plik_serwis");

        try
        {
            var serwis = new PlikSerwisDemoOP(katalog);

            // ZapiszAsync - walidacja + atomowy zapis (.tmp + Move)
            await serwis.ZapiszAsync("config.json", """{"srodowisko": "dev", "debug": true}""");
            await serwis.ZapiszAsync("log.txt", "Start aplikacji\nLogowanie wlaczone\nGotowy");
            Console.WriteLine("ZapiszAsync: 2 pliki zapisane (atomowo przez .tmp + Move)");

            // CzytajTekstAsync - pierwsze wywolanie: czyta z dysku, ustawia cache
            string config = await serwis.CzytajTekstAsync("config.json");
            Console.WriteLine($"CzytajTekstAsync (dysk):  {config.Length} znakow");

            // CzytajTekstAsync - drugie wywolanie: serwuje z cache
            string config2 = await serwis.CzytajTekstAsync("config.json");
            Console.WriteLine($"CzytajTekstAsync (cache): {config2.Length} znakow (bez I/O)");

            // Nadpisanie uniewnaznia cache
            await serwis.ZapiszAsync("config.json", """{"srodowisko": "prod", "debug": false}""");
            string config3 = await serwis.CzytajTekstAsync("config.json");
            Console.WriteLine($"Po nadpisaniu (dysk):     {config3.Length} znakow");

            // AnalizujFolder - statystyki katalogu
            var raport = serwis.AnalizujFolder();
            Console.WriteLine($"AnalizujFolder: {raport.LiczbaPlikov} plikow, " +
                              $"lacznie {raport.CalkowitaWielkosc}B, " +
                              $"najw.: {raport.NajwiekszPlik}");
        }
        finally
        {
            if (Directory.Exists(katalog)) Directory.Delete(katalog, recursive: true);
        }
    }
}
