// 02_OOP — Programowanie Obiektowe w C#
// Uruchamia wszystkie demo metody z każdego tematu
using _02_OOP;

// ── Hermetyzacja ──────────────────────────────────────────────────────────────
Hermetyzacja.ModyfikatoryDostepu();
Hermetyzacja.PrzykladHermetyzacji();
Hermetyzacja.WlasciwosciIPolaDemo();
Hermetyzacja.KonstruktoryDemo();
Hermetyzacja.SingletonDemo();
Hermetyzacja.IDisposableDemo();
Hermetyzacja.BuilderPattern();

// ── Dziedziczenie ─────────────────────────────────────────────────────────────
Dziedziczenie.PodstawyDziedziczenia();
Dziedziczenie.KlasyAbstrakcyjne();
Dziedziczenie.VirtualVsNew();
Dziedziczenie.SealedDemo();
Dziedziczenie.KolejnoscKonstruktorow();
Dziedziczenie.UpcastingIDowncasting();
Dziedziczenie.GetTypeVsTypeof();
Dziedziczenie.KompozycjaVsDziedziczenie();
Dziedziczenie.SzablonMetody();

// ── Interfejsy ────────────────────────────────────────────────────────────────
Interfejsy.PodstawyInterfejsu();
Interfejsy.ExplicitImplementacja();
Interfejsy.InterfejsyNET();
Interfejsy.DomyslneImplementacje();
Interfejsy.InterfejsVsAbstrakcyjna();
Interfejsy.StrategiaWysylki();

// ── Polimorfizm ───────────────────────────────────────────────────────────────
Polimorfizm.VtableModel();
Polimorfizm.StrategyRabatu();
Polimorfizm.PulapePolimorfizmu();
Polimorfizm.AdhocPolimorfizm();

// ── Enumy i Struktury ─────────────────────────────────────────────────────────
EnumyIStruktury.PodstawyEnum();
EnumyIStruktury.FlagiEnum();
EnumyIStruktury.MetodyEnum();
EnumyIStruktury.RozszerzeniaEnum();
EnumyIStruktury.StructVsClass();
EnumyIStruktury.ReadonlyStructISpan();
EnumyIStruktury.BoxingIUnboxing();
EnumyIStruktury.RecordStruct();

// ── SOLID Principles ──────────────────────────────────────────────────────────
SOLIDPrinciples.SRP();
SOLIDPrinciples.OCP();
SOLIDPrinciples.LSP();
SOLIDPrinciples.ISP();
SOLIDPrinciples.DIP();

// ── Wyjątki ───────────────────────────────────────────────────────────────────
WyjatkiOOP.TryCatchFinally();
WyjatkiOOP.ThrowVsThrowEx();
WyjatkiOOP.ExceptionFilters();
WyjatkiOOP.WlasneWyjatki();
WyjatkiOOP.ResultPattern();
WyjatkiOOP.CircuitBreakerDemo();
WyjatkiOOP.UsingDemo();
WyjatkiOOP.NullableReferenceTypesDemo();

Console.WriteLine("\n=== 02_OOP KOMPLETNY ===");
