// ============================================================
// 01_Fundamentals — Fundamenty C# i .NET
// ============================================================
// Tematy: środowisko .NET, typy danych, zmienne, operatory,
//         stringi, instrukcje warunkowe, pętle, metody,
//         tablice, kolekcje, klasy i obiekty
// ============================================================

using _01_Fundamentals;

PrintHeader("TYPY DANYCH I ZMIENNE");
TypyDanychIZmienne.WartoscioweVsReferencyjne();
TypyDanychIZmienne.TypyLiczboweCalkowite();
TypyDanychIZmienne.TypyZmiennoprzecinkowe();
TypyDanychIZmienne.BoolIChar();
TypyDanychIZmienne.VarWnioskowanieTypow();
TypyDanychIZmienne.ConstIReadonly();
TypyDanychIZmienne.KonwersjeTypow();
TypyDanychIZmienne.NullableReferenceTypes();

PrintHeader("STRINGI");
Stringi.TworzeniaStringow();
Stringi.MetodyString();
Stringi.PorownywanieStringow();
Stringi.ImmutabilityIStringBuilder();
Stringi.FormatowanieStringow();

PrintHeader("INSTRUKCJE WARUNKOWE");
InstrukcjeWarunkowe.IfElse();
InstrukcjeWarunkowe.PatternMatchingIsOperator();
InstrukcjeWarunkowe.SwitchKlasyczny();
InstrukcjeWarunkowe.SwitchExpression();
InstrukcjeWarunkowe.PropertyPattern();
InstrukcjeWarunkowe.FizzBuzz();

PrintHeader("PĘTLE");
Petle.PetlaFor();
Petle.PetlaWhile();
Petle.PetlaDoWhile();
Petle.PetlaForeach();
Petle.BreakIContinue();
Petle.ZakresByIIndeksy();

PrintHeader("METODY");
Metody.AnatomiaMetody();
Metody.TypyZwracane();
Metody.PrzekazywaniePrzezWartosc();
Metody.ParametrRef();
Metody.ParametrOut();
Metody.ParametrIn();
Metody.DomyslneINazwane();
Metody.ParametrParams();
Metody.PrzeciazanieMetod();
Metody.MetodyLokalne();
Metody.Rekurencja();

PrintHeader("TABLICE I KOLEKCJE");
TableiceIKolekcje.TablicePodstawy();
TableiceIKolekcje.TabliceOperacje();
TableiceIKolekcje.TabliceWielowymiarowe();
TableiceIKolekcje.SpanT();
TableiceIKolekcje.ListaT();
TableiceIKolekcje.SlownikDictionary();
TableiceIKolekcje.HashSetT();
TableiceIKolekcje.QueueIStack();
TableiceIKolekcje.PraktycznyPrzykladBiblioteka();

PrintHeader("KLASY I OBIEKTY");
KlasyIObiekty.AnatomiaKlasy();
KlasyIObiekty.Konstruktory();
KlasyIObiekty.WlasciwostiProperties();
KlasyIObiekty.StatyczneVsInstancyjne();
KlasyIObiekty.Rekordy();
KlasyIObiekty.ObjectBaza();
KlasyIObiekty.KontoBankoweDemo();

Console.WriteLine("\n\n=== KONIEC 01_Fundamentals ===");

// ─────────────────────────────────────────────────────────────────────────────
static void PrintHeader(string title)
{
    Console.WriteLine($"\n\n{'='.ToString().PadRight(60, '=')}");
    Console.WriteLine($"  {title}");
    Console.WriteLine('='.ToString().PadRight(60, '='));
}
