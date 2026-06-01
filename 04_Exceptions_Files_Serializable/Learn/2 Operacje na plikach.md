### Operacje na plikach w C#

---

### 1. Klasa File — szybkie operacje

csharp

```csharp
// File — statyczna klasa do prostych operacji na plikach
// Idealna gdy plik jest mały i potrzebujesz szybko coś zrobić

// --- ZAPIS ---
// WriteAllText — nadpisuje cały plik
File.WriteAllText("notatka.txt", "Witaj, świecie!");

// WriteAllText z kodowaniem
File.WriteAllText("notatka_utf8.txt", "Zażółć gęślą jaźń",
    System.Text.Encoding.UTF8);

// AppendAllText — dopisuje do końca
File.AppendAllText("log.txt", $"[{DateTime.Now}] Aplikacja uruchomiona\n");

// WriteAllLines — każdy element tablicy jako osobna linia
string[] linie = { "Linia 1", "Linia 2", "Linia 3" };
File.WriteAllLines("lista.txt", linie);

// WriteAllBytes — zapis binarny
byte[] dane = { 0x48, 0x65, 0x6C, 0x6C, 0x6F };  // "Hello"
File.WriteAllBytes("binarny.bin", dane);

// --- ODCZYT ---
// ReadAllText — cały plik jako string
string tresc = File.ReadAllText("notatka.txt");
Console.WriteLine(tresc);  // Witaj, świecie!

// ReadAllLines — jako tablica stringów
string[] wiersze = File.ReadAllLines("lista.txt");
foreach (string w in wiersze)
    Console.WriteLine(w);

// ReadAllBytes — binarny odczyt
byte[] bajty = File.ReadAllBytes("binarny.bin");
Console.WriteLine(System.Text.Encoding.ASCII.GetString(bajty));  // Hello

// ReadLines — LAZY — ładuje po jednej linii (dobre dla dużych plików)
foreach (string linia in File.ReadLines("duzy_plik.txt"))
{
    if (linia.Contains("ERROR"))
        Console.WriteLine(linia);
    // Nie ładuje całego pliku do pamięci!
}

// --- ASYNC (zalecane dla I/O) ---
await File.WriteAllTextAsync("async.txt", "Zapisane asynchronicznie");
string trescAsync = await File.ReadAllTextAsync("async.txt");

// --- OPERACJE NA PLIKACH ---
// Istnienie
bool istnieje = File.Exists("notatka.txt");

// Kopiowanie
File.Copy("notatka.txt", "notatka_kopia.txt");
File.Copy("notatka.txt", "notatka_nadpisz.txt", overwrite: true);

// Przenoszenie / zmiana nazwy
File.Move("stara_nazwa.txt", "nowa_nazwa.txt");
File.Move("plik.txt", "archiwum/plik.txt", overwrite: true);  // .NET 3.0+

// Usuwanie
File.Delete("do_usuniecia.txt");

// Metadane pliku
FileInfo info = new FileInfo("notatka.txt");
Console.WriteLine($"Rozmiar:  {info.Length} bajtów");
Console.WriteLine($"Utworzony: {info.CreationTime}");
Console.WriteLine($"Zmodyfikowany: {info.LastWriteTime}");
Console.WriteLine($"Atrybuty: {info.Attributes}");
```

---

### 2. FileStream — niskopoziomowy dostęp

csharp

```csharp
// FileStream — pełna kontrola nad odczytem i zapisem
// Strumień bajtów — podstawa dla wszystkich innych operacji na plikach

// Tworzenie FileStream
using var fs = new FileStream(
    path:   "dane.bin",
    mode:   FileMode.Create,     // Utwórz nowy lub nadpisz
    access: FileAccess.Write,    // Tylko zapis
    share:  FileShare.None,      // Nikt inny nie może otworzyć
    bufferSize: 4096,            // Rozmiar bufora
    useAsync: true);             // Asynchroniczne I/O

// FileMode:
// Create      — nowy lub nadpisz istniejący
// CreateNew   — nowy, błąd gdy istnieje
// Open        — otwórz istniejący, błąd gdy brak
// OpenOrCreate— otwórz lub utwórz
// Append      — dopisz na koniec (lub utwórz)
// Truncate    — otwórz i wyczyść

// Zapis bajtów
byte[] bufFor = System.Text.Encoding.UTF8.GetBytes("Hello, FileStream!");
await fs.WriteAsync(bufFor, 0, bufFor.Length);
await fs.FlushAsync();  // Wymuś zapis na dysk

// Odczyt z pozycją
using var fsOdczyt = new FileStream("dane.bin", FileMode.Open, FileAccess.Read);

// Pozycja w strumieniu
Console.WriteLine($"Pozycja: {fsOdczyt.Position}");  // 0
Console.WriteLine($"Długość: {fsOdczyt.Length}");     // rozmiar pliku

// Seek — przesuń pozycję
fsOdczyt.Seek(7, SeekOrigin.Begin);    // od początku
fsOdczyt.Seek(-5, SeekOrigin.End);     // od końca
fsOdczyt.Seek(3, SeekOrigin.Current);  // od aktualnej pozycji

// Odczyt do bufora
byte[] bufor = new byte[1024];
int przeczytano = await fsOdczyt.ReadAsync(bufor, 0, bufor.Length);
string tekst = System.Text.Encoding.UTF8.GetString(bufor, 0, przeczytano);
Console.WriteLine(tekst);

// Odczyt dużego pliku w kawałkach — efektywne zarządzanie pamięcią
public async Task KopiujDuzyPlikAsync(
    string zrodlo,
    string cel,
    IProgress<long>? postep = null,
    CancellationToken ct = default)
{
    const int rozmiarBufora = 81920;  // 80KB — optymalny rozmiar
    byte[] bufor2 = new byte[rozmiarBufora];
    long skopiowano = 0;

    using var wejscie  = new FileStream(zrodlo, FileMode.Open,
        FileAccess.Read, FileShare.Read, rozmiarBufora, useAsync: true);
    using var wyjscie  = new FileStream(cel, FileMode.Create,
        FileAccess.Write, FileShare.None, rozmiarBufora, useAsync: true);

    int przeczytano2;
    while ((przeczytano2 = await wejscie.ReadAsync(bufor2, ct)) > 0)
    {
        await wyjscie.WriteAsync(bufor2.AsMemory(0, przeczytano2), ct);
        skopiowano += przeczytano2;
        postep?.Report(skopiowano);
    }

    Console.WriteLine($"Skopiowano {skopiowano:N0} bajtów");
}
```

---

### 3. StreamReader i StreamWriter — tekst

csharp

```csharp
// StreamReader — odczyt tekstu ze strumienia
// StreamWriter — zapis tekstu do strumienia

// --- StreamWriter ---
// Podstawowe użycie
using var writer = new StreamWriter("tekst.txt", append: false,
    encoding: System.Text.Encoding.UTF8);

writer.WriteLine("Pierwsza linia");
writer.WriteLine("Druga linia");
writer.Write("Bez nowej linii");
writer.WriteLine(" — kontynuacja");

// AutoFlush — automatyczny flush po każdym zapisie (wolniejsze)
writer.AutoFlush = true;

// Formatowanie — StreamWriter implementuje TextWriter
writer.WriteLine($"Data: {DateTime.Now:yyyy-MM-dd}");
writer.WriteLine("Liczba: {0:F2}", 3.14159);

// StreamWriter na FileStream — pełna kontrola
using var fs2 = new FileStream("log.txt", FileMode.Append, FileAccess.Write,
    FileShare.Read);  // inne procesy mogą CZYTAĆ podczas zapisu
using var logWriter = new StreamWriter(fs2, System.Text.Encoding.UTF8);
logWriter.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Zdarzenie zalogowane");

// --- StreamReader ---
// Podstawowe użycie
using var reader = new StreamReader("tekst.txt",
    encoding: System.Text.Encoding.UTF8,
    detectEncodingFromByteOrderMarks: true);

// Linia po linii
string? linia;
int numerLinii = 0;
while ((linia = reader.ReadLine()) != null)
{
    numerLinii++;
    Console.WriteLine($"{numerLinii:D3}: {linia}");
}

// Cały plik naraz
using var reader2 = new StreamReader("tekst.txt");
string calyTekst = reader2.ReadToEnd();

// Odczyt do końca — sprawdzenie
Console.WriteLine($"EOF: {reader2.EndOfStream}");
Console.WriteLine($"Pozycja znaku: {reader2.BaseStream.Position}");

// Async StreamReader
using var asyncReader = new StreamReader("duzy_plik.txt");
while (!asyncReader.EndOfStream)
{
    string? liniaAsync = await asyncReader.ReadLineAsync();
    // Przetwórz linię
}

// Wykrywanie kodowania
using var autoReader = new StreamReader("nieznany.txt", detectEncodingFromByteOrderMarks: true);
string _ = autoReader.ReadToEnd();
Console.WriteLine($"Wykryte kodowanie: {autoReader.CurrentEncoding.EncodingName}");
```

---

### 4. Path — operacje na ścieżkach

csharp

```csharp
// Path — statyczna klasa do operacji na ścieżkach plików
// Działa cross-platform! (Windows używa \, Linux /  — Path abstrahuje to)

string pelnaSciezka = @"C:\Projekty\MojaApp\src\Program.cs";

// Dekompozycja ścieżki
Console.WriteLine(Path.GetFileName(pelnaSciezka));      // Program.cs
Console.WriteLine(Path.GetFileNameWithoutExtension(pelnaSciezka)); // Program
Console.WriteLine(Path.GetExtension(pelnaSciezka));     // .cs
Console.WriteLine(Path.GetDirectoryName(pelnaSciezka)); // C:\Projekty\MojaApp\src
Console.WriteLine(Path.GetPathRoot(pelnaSciezka));      // C:\

// Łączenie ścieżek — Path.Combine jest BEZPIECZNIEJSZE niż string concatenation!
string folder  = @"C:\Projekty";
string podfolder = "MojaApp";
string plik    = "config.json";

string polaczona = Path.Combine(folder, podfolder, plik);
Console.WriteLine(polaczona);  // C:\Projekty\MojaApp\config.json

// Cross-platform — automatycznie używa / lub \ w zależności od systemu
string crossPlatform = Path.Combine("src", "Models", "User.cs");
Console.WriteLine(crossPlatform);  // src\Models\User.cs (Windows) lub src/Models/User.cs (Linux)

// Normalizacja ścieżek
string brzydka = Path.GetFullPath(@"C:\Projekty\.\MojaApp\..\Inne\plik.txt");
Console.WriteLine(brzydka);  // C:\Projekty\Inne\plik.txt

// Czy ścieżka jest absolutna
Console.WriteLine(Path.IsPathRooted(@"C:\plik.txt")); // True
Console.WriteLine(Path.IsPathRooted("relative/path")); // False

// Rozszerzenia
Console.WriteLine(Path.ChangeExtension("plik.txt", ".bak")); // plik.bak
Console.WriteLine(Path.ChangeExtension("plik.txt", null));   // plik (bez rozszerzenia)

// Pliki tymczasowe
string tmpPlik   = Path.GetTempFileName();    // tworzy pusty plik tymczasowy!
string tmpFolder = Path.GetTempPath();        // folder temp systemu
string losowaNazwa = Path.GetRandomFileName(); // losowa nazwa (nie tworzy pliku)
string tmpBezTworzenia = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

Console.WriteLine($"Temp plik:   {tmpPlik}");     // C:\Users\...\AppData\Local\Temp\tmp1234.tmp
Console.WriteLine($"Temp folder: {tmpFolder}");   // C:\Users\...\AppData\Local\Temp\
Console.WriteLine($"Losowa:      {losowaNazwa}"); // np. abc123de.fgh

// Znaki niedozwolone w ścieżce
char[] niedozwolone = Path.GetInvalidPathChars();
char[] niedozwoloneNazwa = Path.GetInvalidFileNameChars();

// Bezpieczna nazwa pliku
string bezpieczna = new string(
    "Raport Q1/2024 <specjalny>.xlsx"
        .Select(c => niedozwoloneNazwa.Contains(c) ? '_' : c)
        .ToArray());
Console.WriteLine(bezpieczna);  // Raport Q1_2024 _specjalny_.xlsx

// Separator ścieżek — cross-platform
Console.WriteLine(Path.DirectorySeparatorChar); // \ (Windows) lub / (Linux)
Console.WriteLine(Path.AltDirectorySeparatorChar); // / (zawsze)
Console.WriteLine(Path.PathSeparator);          // ; (Windows) lub : (Linux)
```

---

### 5. Directory — operacje na folderach

csharp

```csharp
// Directory — statyczna klasa do operacji na katalogach

// Tworzenie
Directory.CreateDirectory(@"C:\MojaApp\logs\2024");  // tworzy całą hierarchię!
// Nie rzuca wyjątku gdy folder już istnieje

// Istnienie
bool istnieje2 = Directory.Exists(@"C:\MojaApp");

// Usuwanie
Directory.Delete("pusty_folder");                   // błąd gdy nie pusty
Directory.Delete("niepusty_folder", recursive: true); // usuwa całą zawartość!

// Przenoszenie
Directory.Move("stary_folder", "nowy_folder");

// Listowanie zawartości
string sciezkaFolderuX = @"C:\Projekty";

// Pliki w folderze
string[] pliki = Directory.GetFiles(sciezkaFolderuX);
string[] csPliki = Directory.GetFiles(sciezkaFolderuX, "*.cs");
string[] wszystkieCs = Directory.GetFiles(sciezkaFolderuX, "*.cs",
    SearchOption.AllDirectories);  // rekurencyjnie!

// Podfoldery
string[] foldery = Directory.GetDirectories(sciezkaFolderuX);

// EnumerateFiles — LAZY (lepsze dla dużych struktur)
foreach (string plikCS in Directory.EnumerateFiles(".", "*.cs",
    SearchOption.AllDirectories))
{
    Console.WriteLine(plikCS);
    // Pliki ładowane po jednym — nie ładuje wszystkich do pamięci!
}

// Aktualna lokalizacja
Console.WriteLine(Directory.GetCurrentDirectory());
Directory.SetCurrentDirectory(@"C:\MojaApp");

// Foldery systemowe
Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

// DirectoryInfo — obiektowa wersja (więcej możliwości)
var dirInfo = new DirectoryInfo(@"C:\Projekty");

Console.WriteLine($"Nazwa:    {dirInfo.Name}");
Console.WriteLine($"Pełna:    {dirInfo.FullName}");
Console.WriteLine($"Rodzic:   {dirInfo.Parent?.Name}");
Console.WriteLine($"Istnieje: {dirInfo.Exists}");

// FileSystemInfo — pliki i foldery razem
foreach (FileSystemInfo element in dirInfo.EnumerateFileSystemInfos("*",
    SearchOption.TopDirectoryOnly))
{
    string typ = element is DirectoryInfo ? "[DIR]" : "[FILE]";
    Console.WriteLine($"{typ} {element.Name} ({element.LastWriteTime:dd.MM.yyyy})");
}
```

---

### 6. FileSystemWatcher — monitorowanie zmian

csharp

```csharp
// FileSystemWatcher — reaguj na zmiany w systemie plików

using var watcher = new FileSystemWatcher(@"C:\MojaApp\config")
{
    Filter                 = "*.json",         // tylko pliki .json
    NotifyFilter           =                   // co śledzić
        NotifyFilters.LastWrite |
        NotifyFilters.FileName  |
        NotifyFilters.DirectoryName,
    IncludeSubdirectories  = false,
    EnableRaisingEvents    = true              // aktywuj nasłuchiwanie
};

// Zdarzenia
watcher.Changed += (sender, e) =>
    Console.WriteLine($"Zmieniony: {e.FullPath}");

watcher.Created += (sender, e) =>
    Console.WriteLine($"Utworzony: {e.FullPath}");

watcher.Deleted += (sender, e) =>
    Console.WriteLine($"Usunięty: {e.FullPath}");

watcher.Renamed += (sender, e) =>
    Console.WriteLine($"Przemianowany: {e.OldFullPath} → {e.FullPath}");

watcher.Error += (sender, e) =>
    Console.WriteLine($"Błąd watchera: {e.GetException().Message}");

Console.WriteLine("Nasłuchuję zmian... (Enter = zakończ)");
Console.ReadLine();
```

---

### 7. Wzorce i best practices

csharp

```csharp
// WZORZEC 1 — Bezpieczny zapis (zapis do temp, potem rename)
// Zapobiega uszkodzeniu pliku przy awarii podczas zapisu!
public static async Task BezpiecznyZapisAsync(string docelowy, string tresc)
{
    string tymczasowy = docelowy + ".tmp";
    try
    {
        await File.WriteAllTextAsync(tymczasowy, tresc);
        File.Move(tymczasowy, docelowy, overwrite: true);
        // Move jest atomowy na tym samym dysku — albo stary albo nowy!
    }
    catch
    {
        // Wyczyść plik tymczasowy przy błędzie
        if (File.Exists(tymczasowy))
            File.Delete(tymczasowy);
        throw;
    }
}

// WZORZEC 2 — Retry dla zablokowanych plików
public static async Task<string> CzytajZRetryAsync(
    string sciezka,
    int maxProb = 3,
    int opoznienieMs = 200)
{
    for (int proba = 1; proba <= maxProb; proba++)
    {
        try
        {
            return await File.ReadAllTextAsync(sciezka);
        }
        catch (IOException ex) when (proba < maxProb)
        {
            Console.WriteLine($"Plik zajęty (próba {proba}): {ex.Message}");
            await Task.Delay(opoznienieMs * proba);
        }
    }
    return await File.ReadAllTextAsync(sciezka);  // ostatnia próba, rzuć jeśli błąd
}

// WZORZEC 3 — Streaming JSON dla dużych plików
public static async IAsyncEnumerable<string> CzytajLinieAsync(
    string sciezka,
    [System.Runtime.CompilerServices.EnumeratorCancellation]
    CancellationToken ct = default)
{
    using var stream = new FileStream(sciezka, FileMode.Open,
        FileAccess.Read, FileShare.Read, 4096, useAsync: true);
    using var reader3 = new StreamReader(stream);

    string? linia;
    while ((linia = await reader3.ReadLineAsync(ct)) != null)
    {
        ct.ThrowIfCancellationRequested();
        yield return linia;
    }
}

// Użycie
await foreach (string l in CzytajLinieAsync("wielki.csv"))
{
    if (l.StartsWith("ERROR"))
        Console.WriteLine(l);
}

// WZORZEC 4 — Folder zapewniający istnienie
public static string ZapewnijFolder(string sciezka)
{
    Directory.CreateDirectory(sciezka);  // nie rzuca gdy istnieje
    return sciezka;
}

public static string SciezkaLogu(string nazwaAplikacji) =>
    ZapewnijFolder(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        nazwaAplikacji,
        "logs",
        DateTime.Now.ToString("yyyy-MM")));

// WZORZEC 5 — Ochrona przed path traversal
public static string BezpiecznaSciezka(string folder, string nazwaPliku)
{
    // Atakujący może podać: "../../etc/passwd" lub "..\Windows\System32\hosts"
    string pelna = Path.GetFullPath(Path.Combine(folder, nazwaPliku));

    if (!pelna.StartsWith(Path.GetFullPath(folder)))
        throw new UnauthorizedAccessException(
            $"Próba dostępu poza dozwolonym folderem: {nazwaPliku}");

    return pelna;
}

// Użycie
string dozwolonyFolder = @"C:\MojaApp\uploads";
try
{
    string bezpieczna2 = BezpiecznaSciezka(dozwolonyFolder, "dokument.pdf");     // OK
    string atak = BezpiecznaSciezka(dozwolonyFolder, "../../Windows/hosts");      // UnauthorizedAccess!
}
catch (UnauthorizedAccessException ex)
{
    Console.WriteLine($"Atak path traversal: {ex.Message}");
}
```

---

### 8. Praktyczny przykład — system zarządzania plikami

csharp

```csharp
// Kompletny serwis do zarządzania plikami aplikacji

public class PlikSerwis
{
    private readonly string _glownyFolder;
    private readonly long _maxRozmiarBytes;

    public PlikSerwis(string glownyFolder, long maxRozmiarMB = 10)
    {
        _glownyFolder    = glownyFolder;
        _maxRozmiarBytes = maxRozmiarMB * 1024 * 1024;
        Directory.CreateDirectory(glownyFolder);
    }

    // Zapisz plik z walidacją
    public async Task<string> ZapiszAsync(
        string nazwaPliku,
        Stream zawartosc,
        string[] dozwoloneRozszerzenia,
        CancellationToken ct = default)
    {
        // Walidacja rozszerzenia
        string rozszerzenie = Path.GetExtension(nazwaPliku).ToLower();
        if (!dozwoloneRozszerzenia.Contains(rozszerzenie))
            throw new ArgumentException(
                $"Niedozwolone rozszerzenie: {rozszerzenie}. " +
                $"Dozwolone: {string.Join(", ", dozwoloneRozszerzenia)}");

        // Walidacja rozmiaru
        if (zawartosc.CanSeek && zawartosc.Length > _maxRozmiarBytes)
            throw new ArgumentException(
                $"Plik za duży: {zawartosc.Length / 1024 / 1024}MB " +
                $"(max {_maxRozmiarBytes / 1024 / 1024}MB)");

        // Bezpieczna nazwa — unikaj path traversal
        string bezpiecznaNazwa = Path.GetFileName(nazwaPliku);
        string unikalnaSciezka = Path.Combine(_glownyFolder,
            $"{Guid.NewGuid():N}_{bezpiecznaNazwa}");

        // Zapis przez temp (atomowość)
        string tmpSciezka = unikalnaSciezka + ".tmp";
        try
        {
            await using var fileStream = new FileStream(
                tmpSciezka, FileMode.Create, FileAccess.Write,
                FileShare.None, 81920, useAsync: true);

            await zawartosc.CopyToAsync(fileStream, ct);
            await fileStream.FlushAsync(ct);
        }
        catch
        {
            File.Delete(tmpSciezka);
            throw;
        }

        File.Move(tmpSciezka, unikalnaSciezka);
        Console.WriteLine($"Zapisano: {Path.GetFileName(unikalnaSciezka)}");
        return unikalnaSciezka;
    }

    // Odczyt z cache
    private readonly Dictionary<string, (string Tresc, DateTime CzasCache)> _cache = new();

    public async Task<string> CzytajTekstAsync(string sciezka, int cacheSekundy = 60)
    {
        // Sprawdź cache
        if (_cache.TryGetValue(sciezka, out var wpis))
        {
            if ((DateTime.Now - wpis.CzasCache).TotalSeconds < cacheSekundy)
            {
                Console.WriteLine($"[CACHE] {Path.GetFileName(sciezka)}");
                return wpis.Tresc;
            }
        }

        if (!File.Exists(sciezka))
            throw new FileNotFoundException("Plik nie istnieje", sciezka);

        string tresc2 = await File.ReadAllTextAsync(sciezka);
        _cache[sciezka] = (tresc2, DateTime.Now);
        return tresc2;
    }

    // Raport o folderze
    public FolderRaport AnalizujFolder()
    {
        var pliki2 = new DirectoryInfo(_glownyFolder)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .ToList();

        var wgRozszerzenia = pliki2
            .GroupBy(f => f.Extension.ToLower())
            .ToDictionary(
                g => g.Key,
                g => new { Ilosc = g.Count(), Rozmiar = g.Sum(f => f.Length) });

        return new FolderRaport(
            LiczbaPllikow:        pliki2.Count,
            CalkowityRozmiar:     pliki2.Sum(f => f.Length),
            NajwiekszPlik:        pliki2.MaxBy(f => f.Length)?.Name ?? "-",
            StatystykiRozszerzen: wgRozszerzenia.ToDictionary(
                kv => kv.Key,
                kv => $"{kv.Value.Ilosc} plik(i), {kv.Value.Rozmiar / 1024:N0}KB"));
    }

    // Archiwizacja starych plików
    public async Task ArchiwizujStareAsync(int starszeniDni = 30,
        CancellationToken ct = default)
    {
        string folderArchiwum = Path.Combine(_glownyFolder, "archiwum",
            DateTime.Now.ToString("yyyy-MM"));
        Directory.CreateDirectory(folderArchiwum);

        var stare = Directory.EnumerateFiles(_glownyFolder)
            .Select(f => new FileInfo(f))
            .Where(f => (DateTime.Now - f.LastWriteTime).TotalDays > starszeniDni)
            .ToList();

        Console.WriteLine($"Archiwizuję {stare.Count} plików...");

        foreach (FileInfo plikInfo in stare)
        {
            ct.ThrowIfCancellationRequested();
            string cel2 = Path.Combine(folderArchiwum, plikInfo.Name);
            plikInfo.MoveTo(cel2, overwrite: true);
            Console.WriteLine($"  ↩ {plikInfo.Name}");
        }
    }
}

record FolderRaport(
    int LiczbaPllikow,
    long CalkowityRozmiar,
    string NajwiekszPlik,
    Dictionary<string, string> StatystykiRozszerzen);

// Demonstracja
var serwis2 = new PlikSerwis(@"C:\Temp\PlikSerwisDemo");

// Zapisz plik
var tresc3 = System.Text.Encoding.UTF8.GetBytes("Witaj, PlikSerwis!");
using var strumien = new MemoryStream(tresc3);
string sciezkaZapisana = await serwis2.ZapiszAsync(
    "test.txt",
    strumien,
    new[] { ".txt", ".pdf", ".json" });

// Odczytaj
string odczytana = await serwis2.CzytajTekstAsync(sciezkaZapisana);
Console.WriteLine($"Odczytano: {odczytana}");

// Raport
var raport2 = serwis2.AnalizujFolder();
Console.WriteLine($"\nRaport folderu:");
Console.WriteLine($"  Pliki:   {raport2.LiczbaPllikow}");
Console.WriteLine($"  Rozmiar: {raport2.CalkowityRozmiar:N0} bajtów");
foreach (var (ext, info2) in raport2.StatystykiRozszerzen)
    Console.WriteLine($"  {ext}: {info2}");
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między `File.ReadAllText` a `StreamReader`?"** `File.ReadAllText` — wczytuje cały plik do pamięci jako jeden string. Proste, ale nieefektywne dla dużych plików. `StreamReader` — daje kontrolę nad odczytem: linia po linii, buforowanie, wykrywanie kodowania. Dla pliku >100MB użyj `File.ReadLines()` (lazy IEnumerable) lub `StreamReader` z `ReadLine()` w pętli — nie ładujesz wszystkiego do RAM.

**"Dlaczego używamy `Path.Combine` zamiast konkatenacji stringów?"** Konkatenacja może dawać błędne wyniki: `folder + "\\" + plik` daje podwójny backslash gdy folder kończy się na `\`. `Path.Combine` obsługuje separatory automatycznie i działa cross-platform — na Linuxie używa `/`, na Windows `\`. Obsługuje też absolutne ścieżki: `Path.Combine("C:\foo", "D:\bar")` zwraca `D:\bar` (absolutna ścieżka "wygrywa").

**"Co to path traversal i jak się przed tym bronić?"** Atakujący podaje ścieżkę jak `../../etc/passwd` jako nazwę pliku, żeby wyjść poza dozwolony folder. Ochrona: `Path.GetFullPath(Path.Combine(folder, wejscie))` normalizuje ścieżkę (rozwiązuje `..`), potem sprawdź czy wynik zaczyna się od dozwolonego folderu. Zawsze używaj `Path.GetFileName()` gdy chcesz tylko nazwę pliku bez ścieżki.

**"Dlaczego bezpieczny zapis idzie przez plik tymczasowy?"** Gdy piszesz bezpośrednio do docelowego pliku i aplikacja crasha w połowie zapisu — plik jest uszkodzony (ma część starych danych, część nowych). Zapis do `.tmp` + `File.Move` jest bezpieczny: `Move` na tym samym dysku jest operacją atomową (rename) — albo masz stary plik, albo nowy, nigdy uszkodzony. To standardowy wzorzec dla krytycznych danych.