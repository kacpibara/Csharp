// 03_Csharp_4_5 — dynamic/DLR, Parametry opcjonalne, async/await, Zaawansowane Async, CPU vs I/O, TPL
using _03_Csharp_4_5;

// ── Dynamic i DLR ─────────────────────────────────────────────────────────────
DynamicDLR.VarVsDynamic();
DynamicDLR.DLRMechanizm();
DynamicDLR.ExpandoObjectDemo();
DynamicDLR.DynamicObjectDemo();
DynamicDLR.UzyciaIRyzyka();
DynamicDLR.SilnikRegulBiznesowych();

// ── Parametry opcjonalne i nazwane ────────────────────────────────────────────
OptionalNamed.ParametryOpcjonalne();
OptionalNamed.NazwaneArgumenty();
OptionalNamed.KombinowaneUzycie();
OptionalNamed.PulapkiOptional();
OptionalNamed.OverloadsVsOptional();

// ── async/await ───────────────────────────────────────────────────────────────
await AsyncAwait.ProblematykaITask();
await AsyncAwait.SkładniaAsyncAwait();
await AsyncAwait.SekwencyjneVsRownolegle();
await AsyncAwait.ConfigureAwaitDemo();
await AsyncAwait.AsyncVoidIPulapki();
await AsyncAwait.ObslugaWyjatkow();
await AsyncAwait.CancellationTokenDemo();
await AsyncAwait.ValueTaskIApiSerwis();

// ── Zaawansowane Async ────────────────────────────────────────────────────────
await ZaawansowaneAsync.CancellationTokenGłęboko();
await ZaawansowaneAsync.ProgressReporting();
await ZaawansowaneAsync.WhenAllSzczegółowo();
await ZaawansowaneAsync.WhenAnyWzorce();
await ZaawansowaneAsync.DeadlockiIPrzyczyny();
await ZaawansowaneAsync.ChannelProducentKonsument();
await ZaawansowaneAsync.AsyncLocalDemo();

// ── CPU-bound vs I/O-bound ────────────────────────────────────────────────────
CpuIoBound.FundamentalnaRoznica();
await CpuIoBound.WatkiPodMaska();
await CpuIoBound.AsyncAwaitDlaIO();
await CpuIoBound.TaskRunDlaCPU();
CpuIoBound.ParallelDlaCPU();
await CpuIoBound.BenchmarkIDecyzja();

// ── Task Parallel Library ─────────────────────────────────────────────────────
TaskParallelLibrary.ParallelForPodstawy();
TaskParallelLibrary.ParallelForEach();
TaskParallelLibrary.ObslugaWyjatkow();
await TaskParallelLibrary.ForEachAsync();
TaskParallelLibrary.PLINQDemo();
TaskParallelLibrary.ConcurrentCollections();
TaskParallelLibrary.PulapkiTPL();
await TaskParallelLibrary.AnalizatorLogowDemo();

Console.WriteLine("\n=== 03_Csharp_4_5 KOMPLETNY ===");
