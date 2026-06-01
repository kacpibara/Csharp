using _04_Exceptions_Files_Serializable;

Console.WriteLine("=== 04_Exceptions_Files_Serializable ===\n");

// ═══════════════════════════════════════════════════════════════════════
// 1. OBSLUGA WYJATKOW
// ═══════════════════════════════════════════════════════════════════════
Console.WriteLine("╔══════════════════════════════╗");
Console.WriteLine("║   1. Obsluga Wyjatkow        ║");
Console.WriteLine("╚══════════════════════════════╝");

ObslugaWyjatkow.TryCatchFinally();
ObslugaWyjatkow.ThrowVsThrowEx();
ObslugaWyjatkow.FilterryWyjatkow();
ObslugaWyjatkow.WzorzecGuard();
ObslugaWyjatkow.HierarchiaWyjatkow();
ObslugaWyjatkow.InnerExceptionChaining();
ObslugaWyjatkow.NajlepszyePraktyki();
await ObslugaWyjatkow.AsyncExceptions();
ObslugaWyjatkow.ResultPattern();
ObslugaWyjatkow.GlobalnyHandler();

// ═══════════════════════════════════════════════════════════════════════
// 2. OPERACJE NA PLIKACH
// ═══════════════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════╗");
Console.WriteLine("║   2. Operacje Na Plikach     ║");
Console.WriteLine("╚══════════════════════════════╝");

OperacjeNaPlikach.DemoFileStatic();
await OperacjeNaPlikach.DemoFileStaticAsync();
OperacjeNaPlikach.DemoFileStream();
await OperacjeNaPlikach.DemoStreamReaderWriter();
OperacjeNaPlikach.DemoPath();
OperacjeNaPlikach.DemoDirectory();
OperacjeNaPlikach.DemoFileSystemWatcher();
OperacjeNaPlikach.DemoAtomicWrite();
OperacjeNaPlikach.DemoCzytajZRetry();
await OperacjeNaPlikach.DemoIAsyncEnumerable();
OperacjeNaPlikach.DemoBezpiecznaSciezka();
await OperacjeNaPlikach.DemoPlikSerwis();

// ═══════════════════════════════════════════════════════════════════════
// 3. SERIALIZACJA JSON
// ═══════════════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════╗");
Console.WriteLine("║   3. Serializacja JSON       ║");
Console.WriteLine("╚══════════════════════════════╝");

SerializacjaJSON.DemoPodstawy();
SerializacjaJSON.DemoOpcje();
SerializacjaJSON.DemoAtrybuty();
SerializacjaJSON.DemoPolimorfizm();
SerializacjaJSON.DemoKonwertery();
SerializacjaJSON.DemoJsonDocument();
SerializacjaJSON.DemoJsonNode();
SerializacjaJSON.DemoNewtonsoft();
await SerializacjaJSON.DemoJsonApiSerwis();

// ═══════════════════════════════════════════════════════════════════════
// 4. SERIALIZACJA XML
// ═══════════════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════╗");
Console.WriteLine("║   4. Serializacja XML        ║");
Console.WriteLine("╚══════════════════════════════╝");

SerializacjaXML.DemoXmlSerializer();
SerializacjaXML.DemoXmlSerializerAtrybuty();
SerializacjaXML.DemoXDocument();
SerializacjaXML.DemoLinqToXml();
await SerializacjaXML.DemoXmlWriter();
await SerializacjaXML.DemoXmlReader();
SerializacjaXML.DemoXmlJsonKonwersja();
SerializacjaXML.DemoKonfiguracjaSerwis();

// ═══════════════════════════════════════════════════════════════════════
// 5. IDISPOSABLE I USING
// ═══════════════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════╗");
Console.WriteLine("║   5. IDisposable i Using     ║");
Console.WriteLine("╚══════════════════════════════╝");

IDisposableUsing.DemoUsingStatement();
IDisposableUsing.DemoLIFO();
IDisposableUsing.DemoFullDisposable();
IDisposableUsing.DemoUproszczonyDisposable();
await IDisposableUsing.DemoIAsyncDisposable();
IDisposableUsing.DemoFinalizatory();
IDisposableUsing.DemoRezurekcja();
IDisposableUsing.DemoPulapki();
await IDisposableUsing.DemoPulaPolaczen();

Console.WriteLine("\n=== 04_Exceptions_Files_Serializable KOMPLETNY ===");
