# 03_Csharp_4_5 — dynamic, Parametry opcjonalne, async/await, TPL

## 1. Dynamic i DLR

### var vs dynamic

| Cecha | var | dynamic |
|---|---|---|
| Typ znany | compile-time | runtime |
| IntelliSense | pełny | brak |
| Wydajność | natywna | 10-50× wolniej |
| Błędy | kompilacja | RuntimeBinderException |
| Zmiana typu | niemożliwa | możliwa |

```csharp
var liczba = 42;       // int — znany w compile-time, typ zablokowany
dynamic dyn = 42;      // może zmienić typ
dyn = "teraz string";  // OK — runtime binding
dyn.Brak();            // RuntimeBinderException — dopiero w runtime!
```

### DLR — Dynamic Language Runtime

DLR to warstwa między C# a CLR dla dynamicznych operacji. Komponenty:
1. **CallSite** — punkt wywołania generowany przez kompilator
2. **Binder** — logika wiązania (C# RuntimeBinder)
3. **Cache** — zapamiętanie reguł wiązania — pierwsze wywołanie wolne, kolejne szybsze

**Propagacja dynamic** — operacja na `dynamic` zwraca `dynamic`:
```csharp
dynamic x = 5;
var y = x + 1;  // y jest dynamic, nie int!
```
Ogranicz zasięg `dynamic` do minimum.

### ExpandoObject

Gotowy obiekt rozszerzalny w runtime. Implementuje `IDictionary<string, object?>` i `INotifyPropertyChanged`.

```csharp
dynamic expando = new ExpandoObject();
expando.Imie = "Anna";
expando.Powitaj = (Func<string>)(() => $"Cześć, {expando.Imie}!");
Console.WriteLine(expando.Powitaj());

// Iteracja właściwości przez IDictionary
var dict = (IDictionary<string, object?>)expando;
foreach (var (k, v) in dict) Console.WriteLine($"{k} = {v}");

// INotifyPropertyChanged — wbudowane
((INotifyPropertyChanged)expando).PropertyChanged +=
    (s, e) => Console.WriteLine($"Zmieniono: {e.PropertyName}");
```

### DynamicObject

Klasa bazowa do własnej implementacji dynamiki. Nadpisujesz wybrane metody `TryXxx`:
- `TryGetMember` — odczyt właściwości
- `TrySetMember` — zapis właściwości
- `TryInvokeMember` — wywołanie metody
- `TryBinaryOperation`, `TryUnaryOperation`, `TryGetIndex`, `TrySetIndex`, `TryConvert`

```csharp
public class MójDynamic : DynamicObject
{
    private Dictionary<string, object?> _dane = new();

    public override bool TryGetMember(GetMemberBinder b, out object? result)
    {
        if (!_dane.TryGetValue(b.Name, out result))
            result = $"[brak: {b.Name}]";
        return true;
    }

    public override bool TrySetMember(SetMemberBinder b, object? value)
    {
        _dane[b.Name] = value; return true;
    }
}
```

**ExpandoObject vs DynamicObject:**
- `ExpandoObject`: szybki start, gotowe `IDictionary`, `INotifyPropertyChanged`
- `DynamicObject`: pełna kontrola — walidacja, logowanie, lazy loading, fallback

### Użycia i ryzyka dynamic

**Kiedy dynamic ma sens:**
1. COM Interop (Excel/Word) — API bez typowanych wrapperów
2. Duck typing — wywołaj metodę niezależnie od hierarchii typów
3. JSON z nieznaną strukturą — `ExpandoObject` + JsonConverter
4. Plugin system — dynamiczne ładowanie assembly
5. Prototypowanie — przed ustaleniem ostatecznych typów

**Ryzyka:**
1. `RuntimeBinderException` — błąd wykrywany dopiero w runtime
2. Brak IntelliSense — refactoring niedziałający
3. Wydajność — 10-50× wolniej przez DLR binding
4. Propagacja — wynik operacji na `dynamic` jest też `dynamic`
5. Nieoczekiwane konwersje — `"5" + 3 = "53"` (konkatenacja!)

**Alternatywy:** `JsonElement`, interfejsy, strongly-typed models, `Action`/`Func`.

---

## 2. Parametry opcjonalne i nazwane argumenty

### Parametry opcjonalne

Muszą być **compile-time constants** i **na końcu listy parametrów**:
```csharp
void FormatujWiadomość(
    string tekst,
    string prefix    = "[INFO]",   // literał — OK
    bool   uppercase = false,       // literał — OK
    int    maxLength = 200)         // literał — OK
```

**Niedozwolone jako default:** `new List<>()`, `DateTime.Now` — użyj `null` jako sentinel:
```csharp
void PobierzDane(string url, int? timeoutMs = null, DateTime? dataOd = null)
{
    int  efTimeout = timeoutMs ?? 5000;
    var  efData    = dataOd    ?? DateTime.Now;
}
```

### Nazwane argumenty

```csharp
void KonfigurujPolaczenie(string host, int port = 5432, bool useSsl = true,
    int poolMax = 10, int timeoutSek = 30)
{
    // ...
}

// Tylko zmienione parametry — reszta domyślna
KonfigurujPolaczenie("prod-db",
    poolMax:    20,
    timeoutSek: 60);  // port, useSsl — domyślne

// Dowolna kolejność (C# 7.2+)
KonfigurujPolaczenie(timeoutSek: 10, host: "localhost", port: 5432);
```

### Pułapki

**Pułapka 1 — wartość wkompilowana w caller IL:**
Zmiana domyślnej wartości w bibliotece NIE wpływa na callerów bez ich rekompilacji.

**Pułapka 2 — zmiana nazwy parametru = breaking change:**
```csharp
// Biblioteka v1: void Foo(int count = 0)
// Biblioteka v2: void Foo(int ile = 0)  ← zmiana nazwy
Foo(count: 5);  // CS1739 — błąd kompilacji callera!
```

**Pułapka 3 — overload resolution:**
Metoda bardziej specyficzna (bez optional) wygrywa nad wersją z optional.

**Pułapka 4 — params vs optional:**
Nie definiuj obu wariantów dla tych samych typów — niejednoznaczność.

### Kiedy przeciążenia, kiedy optional+named

| Kryterium | Przeciążenia | Optional+Named |
|---|---|---|
| Logika | Różna per wariant | Ta sama, różne opcje |
| Public API | Preferowane | Ryzyko breaking change |
| Czytelność | Jasna sygnatura | `param: value` przy wywołaniu |
| Reguła kciuka | Public library | Internal/config API |

---

## 3. async/await — programowanie asynchroniczne

### Problem — dlaczego async?

**Synchroniczne:** wątek blokuje się czekając na I/O — bezczynny przez np. 500ms.  
**Asynchroniczne:** wątek "wypożyczony" do puli podczas oczekiwania — obsługuje inne zadania.

```
Synchroniczne: dzwonisz i trzymasz słuchawkę 20 minut
Asynchroniczne: zostawiasz numer, robisz inne rzeczy, oddzwaniają
```

Dwa typy operacji:
- **I/O-bound** (sieć, dysk, DB) → `await` — wątek zwolniony
- **CPU-bound** (obliczenia) → `Task.Run` — osobny wątek

### Task i Task\<T\>

`Task` — reprezentacja operacji która MOŻE nie być skończona ("obietnica"):
```csharp
Task        pusty   = Task.CompletedTask;    // gotowy void
Task<int>   gotowy  = Task.FromResult(42);   // gotowy wynik
Task.Delay(1000);                            // async odpowiednik Thread.Sleep

// Stany: WaitingForActivation → Running → RanToCompletion / Faulted / Canceled
Console.WriteLine(task.IsCompleted);         // bool
Console.WriteLine(task.IsCompletedSuccessfully);
Console.WriteLine(task.IsFaulted);
Console.WriteLine(task.IsCanceled);
```

### Składnia async/await — state machine

```csharp
// async — modyfikator: metoda może używać await
// await — "poczekaj na Task, nie blokuj wątku"
public async Task<string> PobierzAsync(string url)
{
    Console.WriteLine("Przed await");
    string dane = await httpClient.GetStringAsync(url);  // wątek zwolniony
    Console.WriteLine("Po await");                        // może być inny wątek
    return dane.ToUpper();
}
```

Kompilator generuje **state machine** z polami dla każdego punktu `await`.  
Jeśli `Task` jest już skończony — `await` kontynuuje **synchronicznie** (zero narzutu).

Reguły:
1. `async` bez `await` → ostrzeżenie, metoda synchroniczna
2. `await` tylko w metodach `async`
3. `async void` — TYLKO dla event handlerów
4. Typ zwracany: `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`

### Sekwencyjne vs równoległe

```csharp
// SEKWENCYJNE — łącznie ~3s
string w1 = await PobierzAsync("API1");  // czekaj 1s
string w2 = await PobierzAsync("API2");  // potem 1s
string w3 = await PobierzAsync("API3");  // potem 1s

// RÓWNOLEGŁE — łącznie ~1s (tyle co najdłuższy)
Task<string> t1 = PobierzAsync("API1");  // uruchom od razu
Task<string> t2 = PobierzAsync("API2");
Task<string> t3 = PobierzAsync("API3");
string[] wyniki = await Task.WhenAll(t1, t2, t3);  // czekaj na wszystkie
```

**Task.WhenAny — timeout pattern:**
```csharp
async Task<T?> ZTimeoutemAsync<T>(Task<T> operacja, TimeSpan timeout) where T : class
{
    Task timeoutTask = Task.Delay(timeout);
    Task pierwsza    = await Task.WhenAny(operacja, timeoutTask);
    return pierwsza == operacja ? await operacja : null;
}
```

### ConfigureAwait

`SynchronizationContext` — mechanizm decydujący gdzie wznowić kod po `await`:
- WPF/WinForms: wznowienie na wątku UI
- ASP.NET Core + Console: brak kontekstu (dowolny wątek z puli)

```csharp
// W bibliotece — ZAWSZE ConfigureAwait(false)
public async Task<string> MetodaBiblioteczna()
{
    string wynik = await httpClient.GetStringAsync(url)
        .ConfigureAwait(false);  // nie przechwytuj UI context
    return wynik;
}

// W UI — domyślne ConfigureAwait(true) gdy aktualizujesz UI
private async void KlikniecieAsync()
{
    var dane = await PobierzAsync();  // powrót na UI thread
    etykieta.Text = dane;             // bezpieczne
}
```

**Klasyczny deadlock** (w aplikacjach z SynchronizationContext):
```
.Result blokuje wątek UI
→ await chce wrócić na wątek UI
→ wątek UI zablokowany przez .Result
→ DEADLOCK
```

Rozwiązania: (1) zawsze `await`, (2) `ConfigureAwait(false)` w bibliotece, (3) `Task.Run(...).Result`.

### async void — niebezpieczne

```csharp
// ŹLE — wyjątki crashują aplikację, nie możesz await'ować
public async void ZlaMetoda() { await Task.Delay(1000); throw new Exception(); }

// DOBRZE — async Task
public async Task DobraMetoda() { await Task.Delay(1000); throw new Exception(); }
try { await DobraMetoda(); } catch (Exception ex) { /* działa! */ }

// JEDYNY uzasadniony przypadek — event handler (ZAWSZE z try-catch!)
private async void PrzyciskClick(object sender, EventArgs e)
{
    try { await ZalogujAsync(); }
    catch (Exception ex) { Console.WriteLine($"Błąd: {ex.Message}"); }
}
```

### Obsługa wyjątków

```csharp
// Podstawowe try-catch
try { string wynik = await BezpiecznaOperacjaAsync(); }
catch (HttpRequestException ex) { Console.WriteLine($"Błąd HTTP: {ex.Message}"); }

// Task.WhenAll — rzuca PIERWSZY wyjątek przy await
// Aby dostać WSZYSTKIE — sprawdź task.Exception
Task<string[]> wszystkie = Task.WhenAll(tasks);
try { await wszystkie; }
catch
{
    foreach (var ex in wszystkie.Exception!.InnerExceptions)
        Console.WriteLine(ex.Message);
}

// Retry z exponential backoff
async Task<T> RetryAsync<T>(Func<Task<T>> op, int maxProb = 3)
{
    for (int p = 1; p <= maxProb; p++)
    {
        try { return await op(); }
        catch when (p < maxProb)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, p)));
        }
    }
    return await op();
}
```

### CancellationToken

Kooperatywne anulowanie — kod MUSI sprawdzać token:
```csharp
// CancellationTokenSource — źródło anulowania
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(5));  // po czasie
cts.Cancel();                               // natychmiastowo

// Przekazuj token przez cały stos wywołań
async Task PobierzAsync(IEnumerable<string> urls, CancellationToken ct = default)
{
    foreach (string url in urls)
    {
        ct.ThrowIfCancellationRequested();       // rzuca OperationCanceledException
        await httpClient.GetStringAsync(url, ct);  // Task.Delay, HttpClient respektują token
    }
}

// Linked tokens — anuluj gdy KTÓRYKOLWIEK token anulowany
using var linked = CancellationTokenSource
    .CreateLinkedTokenSource(zewnetrzny, lokalnyTimeout.Token);

// Callback przy anulowaniu
using CancellationTokenRegistration reg =
    ct.Register(() => Console.WriteLine("Anulowano — sprzątam"));
```

### ValueTask\<T\>

`ValueTask<T>` — struct zamiast class — **zero alokacji** gdy wynik synchroniczny:
```csharp
public ValueTask<string?> PobierzAsync(string klucz)
{
    // Cache trafiony — synchronicznie, zero alokacji
    if (_cache.TryGetValue(klucz, out string? val))
        return ValueTask.FromResult<string?>(val);

    // Cache pudło — pełny Task
    return new ValueTask<string?>(PobierzZBazyAsync(klucz));
}
```

**Ograniczenia ValueTask:**
- Nie awaituj dwa razy (undefined behavior)
- Nie `Task.WhenAll` bezpośrednio — użyj `.AsTask()`
- Używaj TYLKO gdy cache-hit jest często mierzony

---

## 4. Zaawansowane Async

### CancellationToken — głęboko

```csharp
// Trzy sposoby anulowania
cts.Cancel();                          // natychmiastowe
cts.CancelAfter(TimeSpan.FromSeconds(5)); // po czasie
cts.CancelAfter(5000);                 // ms

// ThrowIfCancellationRequested vs IsCancellationRequested
ct.ThrowIfCancellationRequested();     // rzuca OperationCanceledException
if (ct.IsCancellationRequested) { /* sprzątaj i return */ }

// Callback przy anulowaniu
using CancellationTokenRegistration reg = ct.Register(() =>
    Console.WriteLine("Callback: token anulowany"));

// Linked tokens
using var linked = CancellationTokenSource
    .CreateLinkedTokenSource(zewnetrzny, lokalny.Token);
try { await DlugaOperacjaAsync(linked.Token); }
catch (OperationCanceledException) when (zewnetrzny.IsCancellationRequested)
    { Console.WriteLine("Klient rozłączył się"); }
catch (OperationCanceledException) when (lokalny.Token.IsCancellationRequested)
    { Console.WriteLine("Lokalny timeout"); }

// WithCancellationAsync — polyfill dla starych bibliotek bez CT
static async Task<T> WithCancellationAsync<T>(Task<T> task, CancellationToken ct)
{
    var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    await using var reg = ct.UnsafeRegister(
        s => ((TaskCompletionSource<T>)s!).TrySetCanceled(), tcs);
    try   { return await task.ConfigureAwait(false); }
    finally { tcs.TrySetResult(default!); }
}
```

### IProgress\<T\> — raportowanie postępu

```csharp
// Progress<T> — przechwytuje SynchronizationContext → callback na UI thread!
var postep = new Progress<double>(p => progressBar.Value = (int)p);

// Użycie w metodzie
async Task PrzetworzAsync(IProgress<double>? postep = null, CancellationToken ct = default)
{
    for (int i = 0; i < 100; i++)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Delay(10, ct);
        postep?.Report((i + 1) * 1.0);  // null-safe — Report tylko gdy ktoś słucha
    }
}

// Progress<T> vs Action<T>:
// Progress<T> — marshalling na UI thread (WPF/WinForms bezpieczne)
// Action<T>   — wywołany na bieżącym wątku (może być wątek puli → wyjątek w UI)
```

### Task.WhenAll — wzorce

```csharp
// Limit równoległości — SemaphoreSlim
using var semafor = new SemaphoreSlim(5, 5);

var tasks = urls.Select(async url =>
{
    await semafor.WaitAsync(ct);
    try   { return await httpClient.GetStringAsync(url, ct); }
    finally { semafor.Release(); }
});

string[] wyniki = await Task.WhenAll(tasks);

// Wszystkie wyjątki (nie tylko pierwszy)
Task allTask = Task.WhenAll(tasks);
try { await allTask; }
catch { foreach (var ex in allTask.Exception!.InnerExceptions) ... }

// Batch processing — paczki równolegle, paczki sekwencyjnie
for (int i = 0; i < taskList.Count; i += batchSize)
{
    var paczka = taskList.Skip(i).Take(batchSize);
    var wyniki = await Task.WhenAll(paczka);
}
```

### Task.WhenAny — wzorce

```csharp
// 1. Timeout
Task pierwsza = await Task.WhenAny(operacja, Task.Delay(timeout));
return pierwsza == operacja ? await operacja : null;

// 2. Wyścig (Race) — pierwszy poprawny wynik
Task<string> pierwszy = await Task.WhenAny(pozostale);
if (!pierwszy.IsFaulted) return await pierwszy;

// 3. Kolejność ukończenia
while (pozostale.Any())
{
    var ukonczony = await Task.WhenAny(pozostale);
    pozostale.Remove(ukonczony);
    Console.WriteLine(await ukonczony);
}

// 4. Heartbeat — puls podczas długiej operacji
while (true)
{
    if (await Task.WhenAny(glownaOp, Task.Delay(interwal)) == glownaOp)
        return await glownaOp;
    heartbeat();
}
```

### Deadlocks — przyczyny i rozwiązania

**Mechanizm:** wątek UI blokuje się przez `.Result` → `await` chce wrócić na UI thread → DEADLOCK.

**Rozwiązania (w kolejności preferencji):**
1. **Zawsze `await`** — nigdy `.Result`/`.Wait()` w async kontekście
2. **`ConfigureAwait(false)`** w metodzie async — kontynuacja na wątku puli
3. **`Task.Run(...).Result`** — wątek puli nie ma `SynchronizationContext`

```csharp
// WaitAsync — limit czasowy bez deadlocka
try { return await operacja.WaitAsync(TimeSpan.FromSeconds(5)); }
catch (TimeoutException) { /* timeout */ }
```

### Channel\<T\> — producent/konsument

```csharp
using System.Threading.Channels;

// Ograniczony channel — backpressure gdy pełny
var kanal = Channel.CreateBounded<int>(new BoundedChannelOptions(100)
{
    FullMode = BoundedChannelFullMode.Wait,  // czekaj gdy pełny
});

// Producent
async Task ProducentAsync(ChannelWriter<int> writer, CancellationToken ct)
{
    try
    {
        for (int i = 0; i < 100; i++)
            await writer.WriteAsync(i, ct);
    }
    finally { writer.Complete(); }  // sygnał końca
}

// Konsument — ReadAllAsync kończy się gdy Complete()
async Task KonsumentAsync(ChannelReader<int> reader, CancellationToken ct)
{
    await foreach (int el in reader.ReadAllAsync(ct))
        await PrzetworzAsync(el, ct);
}
```

**Channel vs ConcurrentQueue:**
- `Channel` — async z backpressure i CT, `ReadAllAsync` = `IAsyncEnumerable`
- `ConcurrentQueue` — synchroniczna, wymaga busy-waiting lub `SemaphoreSlim`

### AsyncLocal\<T\>

```csharp
// Dane "podążające" za async flow — każde Task dziedziczy kopię
private static readonly AsyncLocal<string?> _traceId = new();
public static string? TraceId { get => _traceId.Value; set => _traceId.Value = value; }

TraceId = "TRACE-MAIN";
Task.Run(() =>
{
    // Dziedziczy "TRACE-MAIN"
    TraceId = "TRACE-CHILD";  // zmiana tylko w tym flow!
});
// TraceId tu nadal "TRACE-MAIN"
```

Praktyczne zastosowanie: Request ID w ASP.NET Core middleware (jedno ustawienie, wszędzie dostępne).

---

## 5. CPU-bound vs I/O-bound

### Fundamentalna różnica

| Typ | Czas spędzony na | CPU | Rozwiązanie |
|---|---|---|---|
| I/O-bound | Czekaniu na zewnętrzny zasób | Bezczynny | `async/await` |
| CPU-bound | Obliczeniach | Zajęty | `Task.Run` / `Parallel` |

### ThreadPool

```csharp
ThreadPool.GetAvailableThreads(out int dostepne, out int io);
ThreadPool.GetMaxThreads(out int max, out int maxIO);
// Domyślnie: procesory × 2 wątki bazowo, rośnie dynamicznie
```

**I/O-bound z async:** wątek zwolniony → jeden wątek = tysiące operacji I/O.  
**CPU-bound Task.Run:** wątek zajęty → dedykowany wątek z puli.

### Task.Run dla CPU

```csharp
// KIEDY Task.Run:
// ✅ Długie obliczenia (>50ms) na wątku UI
// ✅ Długie obliczenia w ASP.NET Core (odciąż request thread)
// ❌ Krótkie operacje (<1ms) — narzut > zysk
// ❌ I/O operacje — użyj await bezpośrednio

long wynik = await Task.Run(() => ObliczFibonacci(45));

// Z CancellationToken — sprawdzaj co N iteracji
return await Task.Run(() =>
{
    for (int i = 0; i < dane.Length; i++)
    {
        if (i % 10_000 == 0) ct.ThrowIfCancellationRequested();
        // ...
    }
}, ct);
```

**ANTYPATTERN w ASP.NET Core dla I/O:**
```csharp
// ŹLE — przenosi I/O z jednego wątku puli na inny — zero korzyści
await Task.Run(async () => await IoOperacjaAsync());

// DOBRZE — bezpośredni await
await IoOperacjaAsync();
```

### Parallel dla CPU

```csharp
// Parallel.For — iteracja z Interlocked (thread-safe)
int count = 0;
Parallel.For(0, n, i =>
{
    if (IsPrime(i)) Interlocked.Increment(ref count);
});

// Parallel.ForEach — kolekcje
Parallel.ForEach(kolekcja,
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    item => { /* przetwórz item */ });

// Parallel.ForEachAsync — async I/O z limitem (.NET 6+)
await Parallel.ForEachAsync(urls,
    new ParallelOptions { MaxDegreeOfParallelism = 10 },
    async (url, ct) => await httpClient.GetStringAsync(url, ct));

// PLINQ — deklaratywny
int ilosc = duzaDane.AsParallel().Count(IsPrime);
```

### ThreadPool Starvation

Gdy wszystkie wątki puli zablokowane przez `.Result`/`.Wait()` na async metodach:
- Nowe wątki dodawane wolno (1 co 500ms)
- Aplikacja przestaje odpowiadać pod obciążeniem przy niskim CPU
- Rozwiązanie: zawsze `await`, nigdy `.Result` w async kontekście

---

## 6. Task Parallel Library (TPL)

### Parallel.For

```csharp
// Podstawowe — wyniki w losowej kolejności
Parallel.For(0, n, i => { /* równolegle */ });

// Z opcjami
Parallel.For(0, n,
    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
    i => { /* ... */ });

// Break vs Stop
Parallel.For(0, 100, (i, stan) =>
{
    if (warunek) stan.Break();  // przetwórz <= indeks, nie zacznij nowych wyższych
    if (znaleziono) stan.Stop(); // zakończ jak najszybciej
});

// ParallelLoopResult
ParallelLoopResult wynik = Parallel.For(0, 100, ...);
Console.WriteLine($"IsCompleted={wynik.IsCompleted}, LowestBreak={wynik.LowestBreakIteration}");
```

### Thread-local state — unikaj locków

```csharp
long suma = 0;
Parallel.For(0, 1_000_000,
    () => 0L,                                        // localInit — per wątek
    (i, _, lokalny) => lokalny + (long)i * i,        // body — akumuluj lokalnie
    lokalny => Interlocked.Add(ref suma, lokalny)    // localFinally — scal raz
);
// 10-50× szybsze niż lock w każdej iteracji!
```

### Partycjonowanie

```csharp
var partycjoner = Partitioner.Create(0, dane.Length, 10_000);  // paczki po 10k
Parallel.ForEach(partycjoner, zakres =>
{
    for (int i = zakres.Item1; i < zakres.Item2; i++)
        { /* przetwórz dane[i] */ }
});
```

### Obsługa wyjątków w Parallel

```csharp
try { Parallel.For(0, 10, i => { throw new Exception($"Błąd {i}"); }); }
catch (AggregateException ae)
{
    // Flatten — rozkłada zagnieżdżone AggregateException
    foreach (var ex in ae.Flatten().InnerExceptions)
        Console.WriteLine(ex.Message);

    // Handle — obsłuż wybrane, rethrow reszty
    ae.Handle(ex =>
    {
        if (ex is ArgumentException) { /* obsłuż */ return true; }
        return false;  // rethrow
    });
}
```

### PLINQ

```csharp
int[] liczby = Enumerable.Range(1, 10_000_000).ToArray();

// Sekwencyjny → PLINQ: dodaj tylko AsParallel()
var wynik = liczby
    .AsParallel()
    .WithDegreeOfParallelism(4)
    .WithCancellation(ct)
    .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
    .WithMergeOptions(ParallelMergeOptions.NotBuffered)
    .Where(IsPrime)
    .ToList();

// AsOrdered — zachowaj kolejność (kosztem wydajności)
liczby.AsParallel().AsOrdered().Where(IsPrime).Take(100).ToList();

// AsSequential — powrót do sekwencyjnego
liczby.AsParallel().Where(IsPrime).AsSequential().Take(10).ToList();

// ForAll — najszybsze, bez buforowania
liczby.AsParallel().Where(IsPrime).ForAll(p => bag.Add(p));
```

**Kiedy PLINQ pomaga:**
- ✅ Duże kolekcje (>10k elementów)
- ✅ Każdy element: ciężkie obliczenia CPU
- ✅ Niezależne operacje (bez shared state)
- ❌ Małe kolekcje (narzut partycjonowania > zysk)
- ❌ Szybkie operacje (`.Select(x => x * 2)`)
- ❌ I/O operacje

### ConcurrentCollections

```csharp
// ConcurrentBag<T> — nieuporządkowana, najszybsza dla Add (thread-local per wątek)
var bag = new ConcurrentBag<int>();
Parallel.For(0, 1000, i => bag.Add(i));

// ConcurrentQueue<T> — FIFO
var queue = new ConcurrentQueue<string>();
queue.Enqueue("element");
if (queue.TryDequeue(out string? el)) { /* ... */ }

// ConcurrentDictionary<K,V> — atomowe operacje
dict.AddOrUpdate(key, addValue: 1, updateValueFactory: (k, v) => v + 1);
int val = dict.GetOrAdd(key, k => ObligatedValue(k));

// Interlocked — atomowe dla liczb (NAJSZYBSZE — bez locka)
Interlocked.Increment(ref licznik);          // ++
Interlocked.Decrement(ref licznik);          // --
Interlocked.Add(ref licznik, 5);             // += 5
Interlocked.Exchange(ref licznik, 0);        // = 0 (atomowo, zwraca starą)
Interlocked.CompareExchange(ref val, nowy, oczekiwany);  // CAS
```

### Pułapki TPL

```csharp
// Pułapka 1 — za mała praca
Parallel.For(0, 1000, i => { suma += i; });  // WOLNIEJSZE — narzut > zysk

// Pułapka 2 — data race (brak synchronizacji)
int zly = 0;
Parallel.For(0, 100_000, _ => zly++);  // wynik losowy!
// Poprawka: Interlocked.Increment(ref dobry)

// Pułapka 3 — lock w hot path eliminuje równoległość
// Poprawka: thread-local state (10-50× szybciej)

// Pułapka 4 — I/O w Parallel.For blokuje ThreadPool
Parallel.For(0, 100, i => Thread.Sleep(100));  // 100 zablokowanych wątków!
// Poprawka: Parallel.ForEachAsync

// Pułapka 5 — ForAll bez materializacji
query.ForAll(p => bag.Add(p));  // fire-and-forget — może nie skończyć przed return
// Poprawka: query.ToList()
```

### Parallel.For vs PLINQ — kiedy co

| | Parallel.For/ForEach | PLINQ |
|---|---|---|
| Efekty uboczne | ✅ Obsługa | ❌ Unikaj |
| Break/Stop | ✅ Dostępne | ❌ Brak |
| Thread-local | ✅ Wbudowane | ❌ Brak |
| Styl | Imperatywny | Deklaratywny LINQ |
| LINQ pipeline | ❌ Nieodpowiedni | ✅ Idealny |

---

## Typowe pytania rekrutacyjne

**"Co się dzieje gdy napiszesz await?"**  
Kompilator tworzy state machine. Przy `await`: sprawdza czy Task skończony (jeśli tak — synchronicznie), jeśli nie — rejestruje kontynuację i zwraca kontrolę. Wątek zwolniony do puli. Gdy Task skończy — kontynuacja zaplanowana na odpowiednim kontekście.

**"Dlaczego async void jest złe?"**  
(1) Wyjątki crashują aplikację — nie trafiają do callera, (2) nie można await'ować — nie wiesz kiedy skończyła, (3) niemożliwe testowanie. Jedyny dozwolony przypadek: event handlers. Zawsze z `try-catch`.

**"Co to ConfigureAwait(false)?"**  
Bez niego — po `await` kod wznawia na oryginalnym `SynchronizationContext` (np. UI thread). `ConfigureAwait(false)` — wznowienie na dowolnym wątku puli. W bibliotekach ZAWSZE używaj — zapobiega deadlockom, szybsze.

**"Kiedy async/await a kiedy Task.Run?"**  
`async/await` — I/O bound: sieć, DB, dysk. `Task.Run` — CPU bound gdy trwa >50ms i nie chcesz blokować wywołującego wątku. W ASP.NET Core `Task.Run` dla I/O = antypattern.

**"Co to ThreadPool Starvation?"**  
Gdy wszystkie wątki puli blokują się przez `.Result`/`.Wait()` na async metodach. Nowe dodawane wolno (1 co 500ms). Aplikacja przestaje odpowiadać. Rozwiązanie: zawsze `await`.

**"Parallel.For a Task.WhenAll — różnica?"**  
`Parallel.For` — synchroniczny, blokuje caller, CPU-bound (wiele rdzeni). `Task.WhenAll` — asynchroniczny, nie blokuje, I/O-bound (skalowalność). Parallel = szybkość CPU. WhenAll = skalowalność I/O.

**"Thread-local state w Parallel.For?"**  
Lokalny akumulator per wątek — akumuluje bez synchronizacji. `localFinally` scala przez jeden `Interlocked.Add`. Vs. `lock` w hot path: 10-50× szybsze bo wątki nie czekają w kolejce.

**"Channel vs ConcurrentQueue?"**  
`Channel` — async z backpressure i CT, `ReadAllAsync()` = `IAsyncEnumerable` (elegancki koniec gdy `Complete()`). `ConcurrentQueue` — synchroniczna, wymaga busy-waiting lub `SemaphoreSlim`. Channel preferuj dla async pipeline'ów.
