using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;

namespace _04_Exceptions_Files_Serializable;

// ===== MODELE DLA XmlSerializer - SX suffix, WYMAGANY bezparametrowy konstruktor =====

[XmlRoot("Produkt")]
public class ProduktSX
{
    [XmlAttribute("id")]
    public int Id { get; set; }

    [XmlElement("Nazwa")]
    public string Nazwa { get; set; } = "";

    [XmlElement("Cena")]
    public decimal Cena { get; set; }

    [XmlIgnore]                              // nie bierze udzialu w serializacji
    public string? HaszWewnetrzny { get; set; }

    public ProduktSX() { }                   // WYMAGANY przez XmlSerializer!
}

public class PozycjaXml
{
    [XmlAttribute("produkt")]
    public string NazwaProduktu { get; set; } = "";

    [XmlAttribute("cena")]
    public decimal Cena { get; set; }

    [XmlText]                                // tresc wewnetrzna elementu (<Pozycja>3</Pozycja>)
    public int Ilosc { get; set; }

    public PozycjaXml() { }
}

[XmlRoot("Zamowienie")]
public class ZamowienieSX
{
    [XmlAttribute("id")]
    public int Id { get; set; }

    [XmlElement("Klient")]
    public string Klient { get; set; } = "";

    [XmlArray("Pozycje")]
    [XmlArrayItem("Pozycja")]
    public List<PozycjaXml> Pozycje { get; set; } = new();

    public ZamowienieSX() { }
}

// Polimorfizm przez wiele [XmlElement] z typeof
public abstract class ZwierzeXml
{
    [XmlElement("Imie")]
    public string Imie { get; set; } = "";
}

public class PiesXml : ZwierzeXml
{
    [XmlElement("Rasa")]
    public string Rasa { get; set; } = "";
    public PiesXml() { }
}

public class KotXml : ZwierzeXml
{
    [XmlElement("CzyKolczasty")]
    public bool CzyKolczasty { get; set; }
    public KotXml() { }
}

[XmlRoot("Katalog")]
public class KatalogXml
{
    [XmlElement("Pies", typeof(PiesXml))]    // polimorfizm: rozne nazwy elementow → rozne typy
    [XmlElement("Kot",  typeof(KotXml))]
    public List<ZwierzeXml> Zwierzeta { get; set; } = new();

    public KatalogXml() { }
}

// ===== KONFIGURACJA APLIKACJI - hierarchia klas =====

[XmlRoot("Konfiguracja")]
public class KonfiguracjaAplikacjiSX
{
    [XmlElement("Baza")]
    public KonfiguracjaBazySX Baza { get; set; } = new();

    [XmlElement("Cache")]
    public KonfiguracjaCacheSX Cache { get; set; } = new();

    [XmlArray("Serwisy")]
    [XmlArrayItem("Serwis")]
    public List<KonfiguracjaSerwisuSX> Serwisy { get; set; } = new();

    [XmlElement("Logowanie")]
    public KonfiguracjaLogowaniaSX Logowanie { get; set; } = new();

    public KonfiguracjaAplikacjiSX() { }
}

public class KonfiguracjaBazySX
{
    [XmlAttribute("serwer")]  public string Serwer { get; set; } = "localhost";
    [XmlAttribute("baza")]    public string NazwaBazy { get; set; } = "";
    [XmlElement("Timeout")]   public int TimeoutSekund { get; set; } = 30;
    public KonfiguracjaBazySX() { }
}

public class KonfiguracjaCacheSX
{
    [XmlAttribute("wlaczony")] public bool Wlaczony { get; set; } = true;
    [XmlElement("TTL")]        public int TTLSekund { get; set; } = 300;
    public KonfiguracjaCacheSX() { }
}

public class NaglowekHttpSX
{
    [XmlAttribute("klucz")]
    public string Klucz { get; set; } = "";

    [XmlText]                                // wartosc jako tresc elementu
    public string Wartosc { get; set; } = "";

    public NaglowekHttpSX() { }
}

public class KonfiguracjaSerwisuSX
{
    [XmlAttribute("nazwa")]  public string Nazwa { get; set; } = "";
    [XmlAttribute("url")]    public string Url   { get; set; } = "";

    [XmlArray("Naglowki")]
    [XmlArrayItem("Naglowek")]
    public List<NaglowekHttpSX> Naglowki { get; set; } = new();

    public KonfiguracjaSerwisuSX() { }
}

public class KonfiguracjaLogowaniaSX
{
    [XmlAttribute("poziom")] public string Poziom { get; set; } = "Info";
    [XmlElement("PlikLogow")] public string PlikLogow { get; set; } = "app.log";
    public KonfiguracjaLogowaniaSX() { }
}

// ===== PRAKTYCZNY SERWIS KONFIGURACJI =====

public class KonfiguracjaSerwisSX
{
    private static readonly XmlSerializer _serializer = new(typeof(KonfiguracjaAplikacjiSX));

    public KonfiguracjaAplikacjiSX Zaladuj(string sciezka)
    {
        using var fs = new FileStream(sciezka, FileMode.Open, FileAccess.Read);
        return (KonfiguracjaAplikacjiSX)_serializer.Deserialize(fs)!;
    }

    public void Zapisz(string sciezka, KonfiguracjaAplikacjiSX konfiguracja)
    {
        var ustawienia = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };
        using var writer = XmlWriter.Create(sciezka, ustawienia);
        _serializer.Serialize(writer, konfiguracja);
    }

    // Walidacja przez LINQ to XML
    public List<string> Waliduj(KonfiguracjaAplikacjiSX konfiguracja)
    {
        var bledy = new List<string>();

        // Serializuj do pamieci zeby przetworzyc LINQ to XML
        var sb = new StringBuilder();
        using (var w = XmlWriter.Create(sb, new XmlWriterSettings { Indent = false }))
            _serializer.Serialize(w, konfiguracja);

        var doc = XDocument.Parse(sb.ToString());

        // Sprawdz serwisy bez nazwy
        var bezNazwy = doc.Descendants("Serwis")
            .Where(s => string.IsNullOrWhiteSpace((string?)s.Attribute("nazwa")))
            .ToList();
        if (bezNazwy.Count > 0)
            bledy.Add($"{bezNazwy.Count} serwisow bez atrybutu 'nazwa'");

        // Sprawdz timeout > 0
        foreach (var t in doc.Descendants("Timeout"))
            if (int.TryParse(t.Value, out int v) && v <= 0)
                bledy.Add($"Timeout musi byc > 0, znaleziono: {v}");

        return bledy;
    }
}

// ===== GLOWNA KLASA DEMO =====

public static class SerializacjaXML
{
    // 1. XmlSerializer - podstawy (atrybuty XmlRoot/Attribute/Element/Ignore)
    public static void DemoXmlSerializer()
    {
        Console.WriteLine("\n--- XmlSerializer: podstawy ---");

        var produkt = new ProduktSX
        {
            Id = 42, Nazwa = "Laptop", Cena = 3499.99m,
            HaszWewnetrzny = "TAJNE" // [XmlIgnore] - nie pojawi sie
        };

        var serializer = new XmlSerializer(typeof(ProduktSX));

        // Serializacja do StringWriter + XmlWriter (z wciecia)
        var sb = new StringBuilder();
        var xmlSettings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };
        using (var writer = XmlWriter.Create(sb, xmlSettings))
            serializer.Serialize(writer, produkt);

        string xml = sb.ToString();
        Console.WriteLine($"Serialize:\n{xml}");

        // Deserializacja ze StringReader
        using var reader = new System.IO.StringReader(xml);
        var odtworzony = (ProduktSX)serializer.Deserialize(reader)!;
        Console.WriteLine($"Deserialize: Id={odtworzony.Id}, Nazwa={odtworzony.Nazwa}, " +
                          $"HaszWewnetrzny={odtworzony.HaszWewnetrzny ?? "null (XmlIgnore)"}");

        // Serializacja do/z FileStream
        var tmpFile = Path.Combine(Path.GetTempPath(), "demo_produkt.xml");
        try
        {
            using (var fs = new FileStream(tmpFile, FileMode.Create))
                serializer.Serialize(fs, produkt);

            using (var fs = new FileStream(tmpFile, FileMode.Open))
            {
                var zPliku = (ProduktSX)serializer.Deserialize(fs)!;
                Console.WriteLine($"FileStream: Id={zPliku.Id} (plik: {new FileInfo(tmpFile).Length}B)");
            }
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }
    }

    // 2. XmlSerializer - zaawansowane atrybuty (XmlArray, XmlArrayItem, XmlText, polimorfizm)
    public static void DemoXmlSerializerAtrybuty()
    {
        Console.WriteLine("\n--- XmlSerializer: atrybuty XmlArray/XmlArrayItem/XmlText/Polimorfizm ---");

        // ZamowienieSX - XmlArray + XmlArrayItem + XmlText
        var zamowienie = new ZamowienieSX
        {
            Id = 100, Klient = "Jan Kowalski",
            Pozycje =
            [
                new() { NazwaProduktu = "Laptop", Cena = 3499.99m, Ilosc = 1 },
                new() { NazwaProduktu = "Mysz",   Cena = 89.99m,   Ilosc = 2 }
            ]
        };

        var serializerZ = new XmlSerializer(typeof(ZamowienieSX));
        var sb = new StringBuilder();
        using (var w = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true }))
            serializerZ.Serialize(w, zamowienie);
        Console.WriteLine($"ZamowienieSX (XmlArray/XmlText):\n{sb}");

        // KatalogXml - polimorfizm przez wiele [XmlElement]
        var katalog = new KatalogXml
        {
            Zwierzeta = [
                new PiesXml { Imie = "Rex",     Rasa = "Owczarek" },
                new KotXml  { Imie = "Mruczek", CzyKolczasty = false },
                new PiesXml { Imie = "Burek",   Rasa = "Labrador" }
            ]
        };

        var serializerK = new XmlSerializer(typeof(KatalogXml));
        var sbK = new StringBuilder();
        using (var w = XmlWriter.Create(sbK, new XmlWriterSettings { Indent = true }))
            serializerK.Serialize(w, katalog);
        Console.WriteLine($"KatalogXml (polimorfizm):\n{sbK}");

        using var r = new System.IO.StringReader(sbK.ToString());
        var odtworzony = (KatalogXml)serializerK.Deserialize(r)!;
        Console.WriteLine($"Deserializacja: {odtworzony.Zwierzeta.Count} zwierzat, " +
                          $"typy: {string.Join(", ", odtworzony.Zwierzeta.Select(z => z.GetType().Name))}");
    }

    // 3. XDocument - tworzenie, zapis, odczyt, modyfikacja
    public static void DemoXDocument()
    {
        Console.WriteLine("\n--- XDocument API ---");

        // Tworzenie od zera: XDeclaration + XElement + XAttribute + XComment
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("Sklep",
                new XAttribute("wersja", "2.0"),
                new XComment("Lista produktow - automatycznie generowana"),
                new XElement("Produkt",
                    new XAttribute("id", 1),
                    new XAttribute("kategoria", "elektronika"),
                    new XElement("Nazwa", "Laptop"),
                    new XElement("Cena", 3499.99m)),
                new XElement("Produkt",
                    new XAttribute("id", 2),
                    new XAttribute("kategoria", "akcesoria"),
                    new XElement("Nazwa", "Mysz"),
                    new XElement("Cena", 89.99m))
            )
        );

        Console.WriteLine($"Utworzony XDocument: {doc.Root?.Elements().Count()} produktow");
        Console.WriteLine($"ToString():\n{doc}");

        // Save i Load
        var tmpFile = Path.Combine(Path.GetTempPath(), "demo_sklep.xml");
        try
        {
            doc.Save(tmpFile);
            var zaladowany = XDocument.Load(tmpFile);
            Console.WriteLine($"Load: {zaladowany.Root?.Elements("Produkt").Count()} produktow");

            // Parse - z stringa
            var parsed = XDocument.Parse("<root><child id='1'>tekst</child></root>");
            Console.WriteLine($"Parse: root.Child = {parsed.Root?.Element("child")?.Value}");

            // Dostep do elementow i atrybutow
            var pierwszyProdukt = doc.Root!.Element("Produkt");
            Console.WriteLine($"Root.Element: {pierwszyProdukt?.Element("Nazwa")?.Value}");

            // null-safe casting - (int?) zwraca null gdy brak atrybutu (nie rzuca wyjatku!)
            int? id = (int?)pierwszyProdukt?.Attribute("id");
            decimal? cena = (decimal?)pierwszyProdukt?.Element("Cena");
            Console.WriteLine($"Null-safe cast: id={(id.HasValue ? id.ToString() : "null")}, " +
                              $"cena={(cena.HasValue ? cena.ToString() : "null")}");

            string? brakAtr = (string?)doc.Root.Attribute("nieistnieje");
            Console.WriteLine($"Brakujacy atrybut (string?): {brakAtr ?? "null"}");

            // Modyfikacja - Add, SetAttributeValue, SetValue
            doc.Root.Add(new XElement("Produkt",
                new XAttribute("id", 3),
                new XAttribute("kategoria", "akcesoria"),
                new XElement("Nazwa", "Klawiatura"),
                new XElement("Cena", 199.99m)));
            Console.WriteLine($"Po Add: {doc.Root.Elements("Produkt").Count()} produktow");

            // SetAttributeValue - zmiana wartosci atrybutu
            pierwszyProdukt!.SetAttributeValue("kategoria", "profesjonalne");
            Console.WriteLine($"SetAttributeValue: {pierwszyProdukt.Attribute("kategoria")?.Value}");

            // SetValue - zmiana wartosci elementu
            pierwszyProdukt.Element("Cena")?.SetValue(2999.99m);
            Console.WriteLine($"SetValue Cena: {(decimal?)pierwszyProdukt.Element("Cena")}");
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }
    }

    // 4. LINQ to XML - filtrowanie, grupowanie, transformacja, modyfikacja
    public static void DemoLinqToXml()
    {
        Console.WriteLine("\n--- LINQ to XML ---");

        string xml = """
            <?xml version="1.0"?>
            <Magazyn data="2024-01-15">
                <Produkt id="1" kategoria="elektronika"><Nazwa>Laptop</Nazwa><Cena>3499</Cena></Produkt>
                <Produkt id="2" kategoria="elektronika"><Nazwa>Telefon</Nazwa><Cena>1999</Cena></Produkt>
                <Produkt id="3" kategoria="akcesoria"><Nazwa>Mysz</Nazwa><Cena>89</Cena></Produkt>
                <Produkt id="4" kategoria="akcesoria"><Nazwa>Klawiatura</Nazwa><Cena>199</Cena></Produkt>
                <Produkt id="5" kategoria="oprogramowanie"><Nazwa>System</Nazwa><Cena>899</Cena></Produkt>
            </Magazyn>
            """;

        var doc = XDocument.Parse(xml);

        // Descendants - elementy Produkt na dowolnym poziomie
        int ile = doc.Descendants("Produkt").Count();
        Console.WriteLine($"Descendants('Produkt'): {ile} elementow");

        // Elements - bezposrednie dzieci
        int bezposrednie = doc.Root!.Elements("Produkt").Count();
        Console.WriteLine($"Root.Elements('Produkt'): {bezposrednie} bezposrednich dzieci");

        // Filtrowanie + projekcja
        var drogie = doc.Descendants("Produkt")
            .Where(p => (decimal?)p.Element("Cena") > 1000)
            .Select(p => p.Element("Nazwa")!.Value)
            .ToList();
        Console.WriteLine($"Cena > 1000: {string.Join(", ", drogie)}");

        // GroupBy po atrybucie
        Console.WriteLine("GroupBy kategoria:");
        var grupy = doc.Descendants("Produkt")
            .GroupBy(p => (string?)p.Attribute("kategoria") ?? "brak")
            .Select(g => $"{g.Key}({g.Count()})");
        Console.WriteLine($"  {string.Join(", ", grupy)}");

        // Agregacja
        double srednia = doc.Descendants("Produkt")
            .Average(p => (double?)p.Element("Cena") ?? 0);
        decimal max = doc.Descendants("Produkt")
            .Max(p => (decimal?)p.Element("Cena") ?? 0);
        Console.WriteLine($"Srednia: {srednia:F0}, Max: {max}");

        // Transformacja XML → nowy XDocument (query expression)
        var raport = new XDocument(
            new XElement("Raport",
                new XAttribute("wygenerowano", DateTime.Now.ToString("yyyy-MM-dd")),
                from p in doc.Descendants("Produkt")
                where (decimal?)p.Element("Cena") > 100
                orderby (decimal?)p.Element("Cena") descending
                select new XElement("Pozycja",
                    new XAttribute("id", (int?)p.Attribute("id") ?? 0),
                    new XElement("ProduktNazwa", p.Element("Nazwa")!.Value),
                    new XElement("Cena", $"{(decimal?)p.Element("Cena"):F2} PLN")
                )
            )
        );
        Console.WriteLine($"Transformacja: raport zawiera {raport.Root?.Elements().Count()} pozycji");

        // Modyfikacja - ZAWSZE ToList() przed Remove()!
        // BEZ ToList() = InvalidOperationException (modyfikacja kolekcji podczas iteracji)
        var tanie = doc.Descendants("Produkt")
            .Where(p => (decimal?)p.Element("Cena") < 200)
            .ToList(); // KRYTYCZNE: zmaterializuj przed iteracja
        foreach (var t in tanie) t.Remove();
        Console.WriteLine($"Po usunieciu Cena < 200: {doc.Descendants("Produkt").Count()} produktow");

        // XPath z using System.Xml.XPath
        var xpathWynik = doc.XPathSelectElements("//Produkt[@kategoria='elektronika']").ToList();
        Console.WriteLine($"XPath [@kategoria='elektronika']: {xpathWynik.Count} elementow");

        // GetAttribute przez XPath
        string? dataMagazynu = doc.XPathSelectElement("/Magazyn")?.Attribute("data")?.Value;
        Console.WriteLine($"XPath atrybut data: {dataMagazynu}");
    }

    // 5. XmlWriter - strumieniowy zapis (duze pliki, async)
    public static async Task DemoXmlWriter()
    {
        Console.WriteLine("\n--- XmlWriter (streaming, async) ---");
        var tmpFile = Path.Combine(Path.GetTempPath(), "demo_writer.xml");

        try
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                Async = true   // wlacz async API
            };

            // Strumieniowy zapis - nie tworzy calego drzewa w pamieci
            await using var writer = XmlWriter.Create(tmpFile, settings);

            await writer.WriteStartDocumentAsync();
            await writer.WriteStartElementAsync(null, "Produkty", null);
            await writer.WriteAttributeStringAsync(null, "data", null, DateTime.Now.ToString("yyyy-MM-dd"));

            // Generuj 5 produktow strumieniowo
            for (int i = 1; i <= 5; i++)
            {
                await writer.WriteStartElementAsync(null, "Produkt", null);
                await writer.WriteAttributeStringAsync(null, "id", null, i.ToString());
                await writer.WriteElementStringAsync(null, "Nazwa", null, $"Produkt #{i}");
                await writer.WriteElementStringAsync(null, "Cena", null, (i * 100m).ToString());
                await writer.WriteEndElementAsync(); // </Produkt>
            }

            await writer.WriteEndElementAsync(); // </Produkty>
            await writer.WriteEndDocumentAsync();
            await writer.FlushAsync();

            Console.WriteLine($"XmlWriter async: zapisano {new FileInfo(tmpFile).Length}B");
            Console.WriteLine("Wzorzec: StartElement/Attribute/ElementString/EndElement");
            Console.WriteLine("Zaletą: stały zuzycie pamieci niezaleznie od ilosci rekordow");
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }
    }

    // 6. XmlReader - strumieniowy odczyt (duze pliki)
    public static async Task DemoXmlReader()
    {
        Console.WriteLine("\n--- XmlReader (streaming) ---");

        // Przygotuj XML do odczytu
        string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <!-- Plik produktow -->
            <Produkty>
                <Produkt id="1"><Nazwa>Laptop</Nazwa><Cena>3499.99</Cena></Produkt>
                <Produkt id="2"><Nazwa>Telefon</Nazwa><Cena>1999.99</Cena></Produkt>
                <Produkt id="3"><Nazwa>Mysz</Nazwa><Cena>89.99</Cena></Produkt>
            </Produkty>
            """;

        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,   // ignoruj biale znaki miedzy elementami
            IgnoreComments  = true,    // ignoruj komentarze
            Async = true
        };

        var odczytane = new List<(int Id, string Nazwa, decimal Cena)>();

        using var stringReader = new System.IO.StringReader(xml);
        using var reader = XmlReader.Create(stringReader, settings);

        // Podejscie stanowe: sledz biezacy element przez currentElement
        // ReadElementContentAsString* przesuwa reader za zamykajacy tag i
        // nastepny ReadAsync() pominalby nastepny element - uzyj Value zamiast.
        int id = 0;
        string nazwa = "";
        decimal cena = 0;
        string currentElement = "";

        while (await reader.ReadAsync())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    currentElement = reader.Name;
                    if (currentElement == "Produkt")
                    {
                        id = int.Parse(reader.GetAttribute("id")!);
                        nazwa = ""; cena = 0;
                    }
                    break;

                case XmlNodeType.Text:
                    // ReadSubtree() nie jest potrzebne - wystarczy stan currentElement
                    switch (currentElement)
                    {
                        case "Nazwa": nazwa = reader.Value; break;
                        // InvariantCulture: XML zawiera "." jako separator dziesietny
                        case "Cena":  cena = decimal.Parse(
                            reader.Value, System.Globalization.CultureInfo.InvariantCulture); break;
                    }
                    break;

                case XmlNodeType.EndElement:
                    if (reader.Name == "Produkt")
                        odczytane.Add((id, nazwa, cena));
                    break;
            }
        }

        Console.WriteLine($"XmlReader async: odczytano {odczytane.Count} produktow:");
        foreach (var (rid, rnazwa, rcena) in odczytane)
            Console.WriteLine($"  id={rid}, nazwa={rnazwa}, cena={rcena:F2}");
        Console.WriteLine("Wzorzec: ReadAsync() + NodeType switch + stan currentElement + InvariantCulture");
    }

    // 7. XML <-> JSON konwersja
    public static void DemoXmlJsonKonwersja()
    {
        Console.WriteLine("\n--- XML <-> JSON konwersja ---");

        string xml = """
            <Osoba id="1">
                <Imie>Jan</Imie>
                <Wiek>30</Wiek>
                <Email>jan@example.com</Email>
            </Osoba>
            """;

        // Metoda 1: Reczna konwersja XML → Dictionary → JSON (przez STJ)
        Console.WriteLine("Metoda 1: XElement → Dictionary → STJ:");
        var element = XElement.Parse(xml);
        var slownik = new Dictionary<string, object?>
        {
            ["@id"] = element.Attribute("id")?.Value,
            ["Imie"] = element.Element("Imie")?.Value,
            ["Wiek"] = int.TryParse(element.Element("Wiek")?.Value, out int w) ? w : 0,
            ["Email"] = element.Element("Email")?.Value
        };
        string json = JsonSerializer.Serialize(slownik, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);

        // Metoda 2: Newtonsoft.Json.JsonConvert.SerializeXmlNode (wbudowana konwersja)
        Console.WriteLine("Metoda 2: Newtonsoft SerializeXmlNode:");
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);
        string jsonNewtonsoft = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);
        Console.WriteLine(jsonNewtonsoft);

        // JSON → XML przez Newtonsoft
        Console.WriteLine("JSON → XML przez Newtonsoft DeserializeXmlNode:");
        string jsonDoXml = """{"Produkty": {"Produkt": [{"@id": "1", "Nazwa": "Laptop"}, {"@id": "2", "Nazwa": "Mysz"}]}}""";
        XmlDocument? spowrotem = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(jsonDoXml);
        Console.WriteLine($"  Root: {spowrotem?.DocumentElement?.Name}, " +
                          $"dzieci: {spowrotem?.DocumentElement?.ChildNodes[0]?.ChildNodes.Count}");
    }

    // 8. Praktyczny serwis konfiguracji
    public static void DemoKonfiguracjaSerwis()
    {
        Console.WriteLine("\n--- KonfiguracjaSerwisSX ---");

        var konfiguracja = new KonfiguracjaAplikacjiSX
        {
            Baza = new() { Serwer = "db.example.com", NazwaBazy = "AppDb", TimeoutSekund = 30 },
            Cache = new() { Wlaczony = true, TTLSekund = 600 },
            Serwisy =
            [
                new()
                {
                    Nazwa = "PlatnosciAPI", Url = "https://payments.example.com",
                    Naglowki = [new() { Klucz = "X-API-Key", Wartosc = "secret123" }]
                },
                new()
                {
                    Nazwa = "EmailAPI", Url = "https://mail.example.com"
                }
            ],
            Logowanie = new() { Poziom = "Warning", PlikLogow = "logs/app.log" }
        };

        var serwis = new KonfiguracjaSerwisSX();
        var tmpFile = Path.Combine(Path.GetTempPath(), "konfiguracja_demo.xml");

        try
        {
            // Zapisz
            serwis.Zapisz(tmpFile, konfiguracja);
            Console.WriteLine($"Zapisz: plik {new FileInfo(tmpFile).Length}B");

            // Zaladuj
            var zaladowana = serwis.Zaladuj(tmpFile);
            Console.WriteLine($"Zaladuj: Serwer={zaladowana.Baza.Serwer}, " +
                              $"Cache.TTL={zaladowana.Cache.TTLSekund}s, " +
                              $"Serwisy={zaladowana.Serwisy.Count}");

            // Walidacja - poprawna konfiguracja
            var bledy = serwis.Waliduj(zaladowana);
            Console.WriteLine($"Waliduj (poprawna): {bledy.Count} bledow");

            // Walidacja - konfiguracja z bledami
            var zBledem = new KonfiguracjaAplikacjiSX
            {
                Baza = new() { TimeoutSekund = -5 }, // nieprawidlowy timeout
                Serwisy = [new() { Url = "http://test.com" }] // serwis bez nazwy
            };
            var bledy2 = serwis.Waliduj(zBledem);
            Console.WriteLine($"Waliduj (z bledami): {bledy2.Count} bledow: {string.Join("; ", bledy2)}");
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }
    }
}
