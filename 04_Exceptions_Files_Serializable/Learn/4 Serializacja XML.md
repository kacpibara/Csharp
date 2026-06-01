### Serializacja XML w C#

---

### 1. XmlSerializer — klasyczna serializacja

csharp

```csharp
using System.Xml;
using System.Xml.Serialization;

// Modele z atrybutami XmlSerializer
[XmlRoot("produkt")]                    // nazwa elementu głównego
public class Produkt
{
    [XmlAttribute("id")]                // atrybut XML: <produkt id="1">
    public int Id { get; set; }

    [XmlElement("nazwa")]               // element: <nazwa>Laptop</nazwa>
    public string Nazwa { get; set; } = "";

    [XmlElement("cena")]
    public decimal Cena { get; set; }

    [XmlElement("kategoria")]
    public string Kategoria { get; set; } = "";

    [XmlIgnore]                         // pomijaj to pole!
    public string TajnePole { get; set; } = "";

    // XmlSerializer WYMAGA publicznego konstruktora bezparametrowego!
    public Produkt() { }

    public Produkt(int id, string nazwa, decimal cena, string kat)
    {
        Id = id; Nazwa = nazwa; Cena = cena; Kategoria = kat;
    }
}

// Serializacja — obiekt → XML
var produkt = new Produkt(1, "Laptop", 3500m, "IT");

var serializer = new XmlSerializer(typeof(Produkt));

// Do stringa
using var sw = new StringWriter();
using var xw = XmlWriter.Create(sw, new XmlWriterSettings
{
    Indent = true,
    IndentChars = "  ",
    Encoding = System.Text.Encoding.UTF8
});
serializer.Serialize(xw, produkt);
string xml = sw.ToString();
Console.WriteLine(xml);
// <?xml version="1.0" encoding="utf-16"?>
// <produkt id="1">
//   <nazwa>Laptop</nazwa>
//   <cena>3500</cena>
//   <kategoria>IT</kategoria>
// </produkt>

// Do pliku
using var fs = new FileStream("produkt.xml", FileMode.Create);
serializer.Serialize(fs, produkt);

// Deserializacja — XML → obiekt
using var sr = new StringReader(xml);
Produkt? odczytany = (Produkt?)serializer.Deserialize(sr);
Console.WriteLine($"{odczytany?.Nazwa}: {odczytany?.Cena}");
// Laptop: 3500

// Z pliku
using var fsOdczyt = new FileStream("produkt.xml", FileMode.Open);
Produkt? zPliku = (Produkt?)serializer.Deserialize(fsOdczyt);
```

---

### 2. Atrybuty XmlSerializer — szczegółowo

csharp

```csharp
// Kolekcje i zagnieżdżone obiekty
[XmlRoot("zamowienie")]
public class Zamowienie
{
    [XmlAttribute("numer")]
    public string Numer { get; set; } = "";

    [XmlElement("data")]
    public DateTime Data { get; set; }

    [XmlElement("klient")]
    public KlientXml Klient { get; set; } = new();

    // Kolekcja — każdy element jako <pozycja>
    [XmlArray("pozycje")]              // element opakowujący
    [XmlArrayItem("pozycja")]          // nazwa każdego elementu
    public List<PozycjaXml> Pozycje { get; set; } = new();

    // Alternatywa — bez elementu opakowującego
    // (każda pozycja bezpośrednio w zamówieniu)
    [XmlElement("tag")]
    public List<string> Tagi { get; set; } = new();

    public Zamowienie() { }
}

public class KlientXml
{
    [XmlAttribute("id")]
    public int Id { get; set; }

    [XmlElement("imie")]
    public string Imie { get; set; } = "";

    [XmlElement("email")]
    public string Email { get; set; } = "";

    // Tekst wewnątrz elementu: <opis>treść</opis>
    [XmlText]
    public string Opis { get; set; } = "";

    public KlientXml() { }
}

public class PozycjaXml
{
    [XmlAttribute("produkt-id")]
    public int ProduktId { get; set; }

    [XmlElement("ilosc")]
    public int Ilosc { get; set; }

    [XmlElement("cena")]
    public decimal Cena { get; set; }

    [XmlIgnore]
    public decimal Wartosc => Cena * Ilosc;  // wyliczane — nie serializuj

    public PozycjaXml() { }
}

// Polimorfizm — różne typy w tej samej kolekcji
[XmlRoot("katalog")]
public class KatalogXml
{
    [XmlElement("laptop",    typeof(LaptopXml))]
    [XmlElement("smartfon",  typeof(SmartfonXml))]
    [XmlElement("akcesoria", typeof(AkcesoriaXml))]
    public List<SprzętXml> Sprzet { get; set; } = new();

    public KatalogXml() { }
}

public abstract class SprzętXml
{
    [XmlAttribute("id")]
    public int Id { get; set; }
    [XmlElement("nazwa")]
    public string Nazwa { get; set; } = "";
}

public class LaptopXml    : SprzętXml { [XmlElement("ram")] public int Ram { get; set; } }
public class SmartfonXml  : SprzętXml { [XmlElement("ekran")] public double Ekran { get; set; } }
public class AkcesoriaXml : SprzętXml { [XmlElement("kolor")] public string Kolor { get; set; } = ""; }

// Demonstracja pełnej serializacji
var zamowienie = new Zamowienie
{
    Numer = "ZAM-2024-001",
    Data  = DateTime.Now,
    Klient = new KlientXml { Id = 1, Imie = "Anna", Email = "anna@test.pl" },
    Pozycje = new List<PozycjaXml>
    {
        new() { ProduktId = 1, Ilosc = 2, Cena = 3500m },
        new() { ProduktId = 2, Ilosc = 1, Cena = 150m  }
    },
    Tagi = new List<string> { "pilne", "VIP" }
};

var serializerZam = new XmlSerializer(typeof(Zamowienie));
var xmlOutput = new System.Text.StringBuilder();
using (var writer = XmlWriter.Create(xmlOutput,
    new XmlWriterSettings { Indent = true }))
{
    serializerZam.Serialize(writer, zamowienie);
}
Console.WriteLine(xmlOutput);
// <?xml version="1.0" encoding="utf-16"?>
// <zamowienie numer="ZAM-2024-001">
//   <data>2024-03-15T10:30:00</data>
//   <klient id="1">
//     <imie>Anna</imie>
//     <email>anna@test.pl</email>
//   </klient>
//   <pozycje>
//     <pozycja produkt-id="1">
//       <ilosc>2</ilosc>
//       <cena>3500</cena>
//     </pozycja>
//     <pozycja produkt-id="2">
//       <ilosc>1</ilosc>
//       <cena>150</cena>
//     </pozycja>
//   </pozycje>
//   <tag>pilne</tag>
//   <tag>VIP</tag>
// </zamowienie>
```

---

### 3. XDocument — nowoczesne API do tworzenia XML

csharp

```csharp
using System.Xml.Linq;

// XDocument — LINQ-friendly, czytelne API do tworzenia i czytania XML

// Tworzenie XML programowo
XDocument doc = new XDocument(
    new XDeclaration("1.0", "utf-8", "yes"),
    new XComment("Katalog produktów"),
    new XElement("katalog",
        new XAttribute("wersja", "2.0"),
        new XAttribute("data", DateTime.Now.ToString("yyyy-MM-dd")),

        new XElement("produkt",
            new XAttribute("id", "1"),
            new XElement("nazwa", "Laptop"),
            new XElement("cena", new XAttribute("waluta", "PLN"), 3500),
            new XElement("kategoria", "IT"),
            new XElement("specyfikacja",
                new XElement("ram", new XAttribute("jednostka", "GB"), 16),
                new XElement("dysk", "512 SSD"))),

        new XElement("produkt",
            new XAttribute("id", "2"),
            new XElement("nazwa", "Mysz"),
            new XElement("cena", new XAttribute("waluta", "PLN"), 150),
            new XElement("kategoria", "IT"))
    )
);

// Zapis do pliku
doc.Save("katalog.xml");

// Zapis do stringa
string xmlStr = doc.ToString();
Console.WriteLine(xmlStr);
// <katalog wersja="2.0" data="2024-03-15">
//   <!--Katalog produktów-->
//   <produkt id="1">
//     <nazwa>Laptop</nazwa>
//     <cena waluta="PLN">3500</cena>
//     ...

// Wczytanie z pliku lub stringa
XDocument wczytany = XDocument.Load("katalog.xml");
XDocument zStringa = XDocument.Parse(xmlStr);

// Dostęp do elementów
XElement? root = doc.Root;
Console.WriteLine(root?.Name);           // katalog
Console.WriteLine(root?.Attribute("wersja")?.Value);  // 2.0

// Odczyt pierwszego produktu
XElement? pierwszyProd = root?.Element("produkt");
Console.WriteLine(pierwszyProd?.Element("nazwa")?.Value);  // Laptop
Console.WriteLine(pierwszyProd?.Attribute("id")?.Value);   // 1

// Bezpieczne rzutowanie przez (typ?)
int? id = (int?)pierwszyProd?.Attribute("id");         // 1
decimal? cena = (decimal?)pierwszyProd?.Element("cena");  // 3500

// Modyfikowanie dokumentu
XElement? mysz = root?.Elements("produkt")
    .FirstOrDefault(e => e.Attribute("id")?.Value == "2");

mysz?.Add(new XElement("dostepnosc", "w magazynie"));  // dodaj element
mysz?.SetAttributeValue("aktywny", "true");            // dodaj/zmień atrybut
mysz?.Element("cena")?.SetValue(149);                  // zmień wartość
```

---

### 4. LINQ to XML — zapytania do dokumentów

csharp

```csharp
// LINQ to XML — potęga LINQ do pracy z XML

// Dane testowe — wczytaj XML
string xmlDane = """
<ksiegarnia>
  <ksiazka id="1" gatunek="programowanie">
    <tytul>C# in Depth</tytul>
    <autor>Jon Skeet</autor>
    <cena>89.99</cena>
    <rok>2019</rok>
    <ocena>5</ocena>
  </ksiazka>
  <ksiazka id="2" gatunek="architektura">
    <tytul>Clean Code</tytul>
    <autor>Robert Martin</autor>
    <cena>79.99</cena>
    <rok>2008</rok>
    <ocena>5</ocena>
  </ksiazka>
  <ksiazka id="3" gatunek="programowanie">
    <tytul>Design Patterns</tytul>
    <autor>Gang of Four</autor>
    <cena>119.99</cena>
    <rok>1994</rok>
    <ocena>4</ocena>
  </ksiazka>
  <ksiazka id="4" gatunek="algorytmy">
    <tytul>Introduction to Algorithms</tytul>
    <autor>Cormen</autor>
    <cena>149.99</cena>
    <rok>2009</rok>
    <ocena>4</ocena>
  </ksiazka>
</ksiegarnia>
""";

XDocument ks = XDocument.Parse(xmlDane);

// Pobierz wszystkie tytuły
var tytuly = ks.Descendants("tytul")
    .Select(t => t.Value);
tytuly.ToList().ForEach(Console.WriteLine);

// Filtrowanie — książki programistyczne
var programowanie = ks.Root!
    .Elements("ksiazka")
    .Where(k => k.Attribute("gatunek")?.Value == "programowanie")
    .Select(k => new
    {
        Tytul  = k.Element("tytul")?.Value,
        Cena   = (decimal?)k.Element("cena") ?? 0m,
        Rok    = (int?)k.Element("rok") ?? 0
    })
    .OrderBy(k => k.Cena);

Console.WriteLine("=== Książki programistyczne ===");
foreach (var k in programowanie)
    Console.WriteLine($"  {k.Tytul}: {k.Cena:C} ({k.Rok})");

// Grupowanie po gatunku
var wgGatunku = ks.Root!.Elements("ksiazka")
    .GroupBy(k => k.Attribute("gatunek")!.Value)
    .Select(g => new
    {
        Gatunek       = g.Key,
        Liczba        = g.Count(),
        SrednaCena    = g.Average(k => (double)(decimal)k.Element("cena")!),
        Najdrozszar   = g.Max(k => (decimal)k.Element("cena")!)
    });

Console.WriteLine("\n=== Statystyki gatunków ===");
foreach (var g in wgGatunku)
    Console.WriteLine($"  {g.Gatunek}: {g.Liczba} szt., " +
                      $"śr.cena: {g.SrednaCena:F2}PLN, max: {g.Najdrozszar:C}");

// XPath w LINQ to XML
var xpath = ks.XPathSelectElements("//ksiazka[@gatunek='programowanie']/tytul");
// Wymaga: using System.Xml.XPath;

// Transformacja — XML → nowy XML
XDocument katalogUproszczony = new XDocument(
    new XElement("katalog",
        from k in ks.Root!.Elements("ksiazka")
        where (int)k.Element("ocena")! >= 4
        orderby (decimal)k.Element("cena")!
        select new XElement("pozycja",
            new XAttribute("id", k.Attribute("id")!.Value),
            new XElement("info",
                $"{k.Element("tytul")!.Value} ({k.Element("rok")!.Value})"),
            new XElement("cena", k.Element("cena")!.Value)
        )
    )
);
Console.WriteLine("\n=== Uproszczony katalog ===");
Console.WriteLine(katalogUproszczony);

// Modyfikacja dokumentu — dodaj nową książkę
ks.Root!.Add(new XElement("ksiazka",
    new XAttribute("id", "5"),
    new XAttribute("gatunek", "programowanie"),
    new XElement("tytul", "Refactoring"),
    new XElement("autor", "Martin Fowler"),
    new XElement("cena", 94.99m),
    new XElement("rok", 2018),
    new XElement("ocena", 5)));

// Usuń tanie książki (< 80 PLN)
ks.Root.Elements("ksiazka")
    .Where(k => (decimal)k.Element("cena")! < 80m)
    .ToList()  // ToList() bo modyfikujemy podczas iteracji!
    .ForEach(k => k.Remove());

Console.WriteLine($"\nPo usunięciu: {ks.Root.Elements("ksiazka").Count()} książek");
```

---

### 5. XmlReader i XmlWriter — wydajne strumieniowe API

csharp

```csharp
// XmlReader/Writer — dla DUŻYCH plików (strumieniowe, mała pamięć)
// Zamiast ładować cały XML do pamięci — czytasz/piszesz element po elemencie

// XmlWriter — tworzenie XML bez budowania drzewa w pamięci
public static async Task GenerujDuzyXmlAsync(
    string sciezka,
    IEnumerable<Produkt> produkty)
{
    var settings = new XmlWriterSettings
    {
        Indent         = true,
        Async          = true,
        Encoding       = System.Text.Encoding.UTF8,
        CloseOutput    = true
    };

    await using var writer = XmlWriter.Create(sciezka, settings);

    await writer.WriteStartDocumentAsync();
    await writer.WriteStartElementAsync(null, "produkty", null);

    foreach (var p in produkty)
    {
        await writer.WriteStartElementAsync(null, "produkt", null);
        await writer.WriteAttributeStringAsync(null, "id", null, p.Id.ToString());
        await writer.WriteElementStringAsync(null, "nazwa", null, p.Nazwa);
        await writer.WriteElementStringAsync(null, "cena", null, p.Cena.ToString());
        await writer.WriteEndElementAsync();  // </produkt>
    }

    await writer.WriteEndElementAsync();  // </produkty>
    await writer.WriteEndDocumentAsync();
}

// XmlReader — czytanie dużego XML bez ładowania do pamięci
public static IEnumerable<Produkt> CzytajDuzyXml(string sciezka)
{
    var settings = new XmlReaderSettings
    {
        IgnoreWhitespace = true,
        IgnoreComments   = true
    };

    using var reader = XmlReader.Create(sciezka, settings);

    while (reader.Read())
    {
        // Przesuń do elementu <produkt>
        if (reader.NodeType != XmlNodeType.Element || reader.Name != "produkt")
            continue;

        // Odczytaj atrybuty
        int id = int.Parse(reader.GetAttribute("id") ?? "0");

        // Odczytaj podelemnty
        string nazwa = "";
        decimal cena = 0;

        using XmlReader subtree = reader.ReadSubtree();
        while (subtree.Read())
        {
            if (subtree.NodeType != XmlNodeType.Element) continue;

            switch (subtree.Name)
            {
                case "nazwa":
                    nazwa = subtree.ReadElementContentAsString();
                    break;
                case "cena":
                    cena = subtree.ReadElementContentAsDecimal();
                    break;
            }
        }

        yield return new Produkt(id, nazwa, cena, "");
    }
}

// Demonstracja — generuj 100k produktów, czytaj strumieniowo
var duzeListy = Enumerable.Range(1, 100_000)
    .Select(i => new Produkt(i, $"Produkt_{i}", i * 10.5m, "Test"));

await GenerujDuzyXmlAsync("duzy_katalog.xml", duzeListy);

// Czytaj strumieniowo — mała pamięć!
int licznik = 0;
decimal suma = 0;
foreach (var p in CzytajDuzyXml("duzy_katalog.xml"))
{
    suma += p.Cena;
    licznik++;
}
Console.WriteLine($"Produktów: {licznik:N0}, Suma cen: {suma:N2}");
```

---

### 6. Konwersja XML ↔ JSON

csharp

```csharp
using System.Text.Json;
using System.Xml.Linq;

// XML → JSON (ręczna konwersja przez LINQ to XML)
public static JsonElement XmlToJson(XElement element)
{
    var dict = new Dictionary<string, object?>();

    // Atrybuty jako właściwości z prefiksem @
    foreach (var attr in element.Attributes())
        dict[$"@{attr.Name.LocalName}"] = attr.Value;

    // Elementy potomne
    foreach (var child in element.Elements())
    {
        string name = child.Name.LocalName;
        bool maDzieci = child.HasElements;
        bool wielokrotny = element.Elements(name).Count() > 1;

        if (wielokrotny)
        {
            if (!dict.ContainsKey(name))
                dict[name] = new List<object?>();
            ((List<object?>)dict[name]!).Add(
                maDzieci ? (object)XmlToJson(child) : child.Value);
        }
        else
        {
            dict[name] = maDzieci ? (object)XmlToJson(child) : child.Value;
        }
    }

    // Tekst bez dzieci
    if (!element.HasElements && !element.HasAttributes)
        return JsonSerializer.SerializeToElement(element.Value);

    return JsonSerializer.SerializeToElement(dict);
}

// Newtonsoft.Json ma wbudowaną konwersję XML ↔ JSON:
// string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc);
// XmlDocument xmlDoc2 = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(json);

// Demonstracja
string xmlProsty = """
<ksiazka id="1">
  <tytul>C# in Depth</tytul>
  <autor>Jon Skeet</autor>
  <cena>89.99</cena>
</ksiazka>
""";

XElement xmlElem = XElement.Parse(xmlProsty);
JsonElement jsonResult = XmlToJson(xmlElem);
Console.WriteLine(JsonSerializer.Serialize(jsonResult,
    new JsonSerializerOptions { WriteIndented = true }));
// {
//   "@id": "1",
//   "tytul": "C# in Depth",
//   "autor": "Jon Skeet",
//   "cena": "89.99"
// }
```

---

### 7. Praktyczny przykład — system konfiguracji XML

csharp

```csharp
// Kompletny przykład — aplikacja z konfiguracją w XML

[XmlRoot("konfiguracja")]
public class KonfiguracjaAplikacji
{
    [XmlElement("baza-danych")]
    public KonfiguracjaBazy Baza { get; set; } = new();

    [XmlElement("cache")]
    public KonfiguracjaCache Cache { get; set; } = new();

    [XmlArray("serwisy")]
    [XmlArrayItem("serwis")]
    public List<KonfiguracjaSerwisu> Serwisy { get; set; } = new();

    [XmlElement("logging")]
    public KonfiguracjaLogowania Logging { get; set; } = new();

    public KonfiguracjaAplikacji() { }
}

public class KonfiguracjaBazy
{
    [XmlAttribute("provider")]
    public string Provider { get; set; } = "SqlServer";

    [XmlElement("server")]
    public string Server { get; set; } = "localhost";

    [XmlElement("database")]
    public string Database { get; set; } = "";

    [XmlElement("port")]
    public int Port { get; set; } = 1433;

    [XmlElement("timeout")]
    public int Timeout { get; set; } = 30;

    [XmlElement("pool-min")]
    public int PoolMin { get; set; } = 5;

    [XmlElement("pool-max")]
    public int PoolMax { get; set; } = 100;

    public KonfiguracjaBazy() { }

    [XmlIgnore]
    public string ConnectionString =>
        $"Server={Server},{Port};Database={Database};Min Pool Size={PoolMin};Max Pool Size={PoolMax}";
}

public class KonfiguracjaCache
{
    [XmlAttribute("enabled")]
    public bool Enabled { get; set; } = true;

    [XmlElement("ttl-minutes")]
    public int TtlMinutes { get; set; } = 60;

    [XmlElement("max-items")]
    public int MaxItems { get; set; } = 10000;

    public KonfiguracjaCache() { }
}

public class KonfiguracjaSerwisu
{
    [XmlAttribute("nazwa")]
    public string Nazwa { get; set; } = "";

    [XmlAttribute("enabled")]
    public bool Enabled { get; set; } = true;

    [XmlElement("url")]
    public string Url { get; set; } = "";

    [XmlElement("timeout")]
    public int Timeout { get; set; } = 30;

    [XmlArray("headers")]
    [XmlArrayItem("header")]
    public List<NaglowekHttp> Headers { get; set; } = new();

    public KonfiguracjaSerwisu() { }
}

public class NaglowekHttp
{
    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    [XmlText]
    public string Value { get; set; } = "";

    public NaglowekHttp() { }
}

public class KonfiguracjaLogowania
{
    [XmlAttribute("poziom")]
    public string Poziom { get; set; } = "Info";

    [XmlElement("do-pliku")]
    public bool DoPliku { get; set; } = true;

    [XmlElement("folder")]
    public string Folder { get; set; } = "logs";

    public KonfiguracjaLogowania() { }
}

// Serwis konfiguracji
public class KonfiguracjaSerwis
{
    private KonfiguracjaAplikacji _config = new();
    private readonly string _sciezka;
    private readonly XmlSerializer _serializer = new(typeof(KonfiguracjaAplikacji));

    public KonfiguracjaSerwis(string sciezka)
    {
        _sciezka = sciezka;
    }

    public KonfiguracjaAplikacji Zaladuj()
    {
        if (!File.Exists(_sciezka))
        {
            _config = TworzDomyslna();
            Zapisz();
            return _config;
        }

        using var reader = new StreamReader(_sciezka);
        _config = (_serializer.Deserialize(reader) as KonfiguracjaAplikacji)!;
        return _config;
    }

    public void Zapisz()
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = System.Text.Encoding.UTF8 };
        using var writer = XmlWriter.Create(_sciezka, settings);
        _serializer.Serialize(writer, _config);
        Console.WriteLine($"Konfiguracja zapisana: {_sciezka}");
    }

    private KonfiguracjaAplikacji TworzDomyslna() => new()
    {
        Baza = new KonfiguracjaBazy
        {
            Server   = "localhost",
            Database = "MojaAplikacja",
            Port     = 1433,
            Timeout  = 30
        },
        Cache = new KonfiguracjaCache
        {
            Enabled    = true,
            TtlMinutes = 60,
            MaxItems   = 5000
        },
        Serwisy = new List<KonfiguracjaSerwisu>
        {
            new()
            {
                Nazwa   = "PaymentAPI",
                Url     = "https://api.payment.pl",
                Timeout = 10,
                Headers = new List<NaglowekHttp>
                {
                    new() { Name = "X-API-Version", Value = "v2" },
                    new() { Name = "Accept",         Value = "application/json" }
                }
            },
            new()
            {
                Nazwa   = "EmailSerwis",
                Url     = "https://smtp.example.com",
                Enabled = false
            }
        },
        Logging = new KonfiguracjaLogowania
        {
            Poziom = "Warning",
            DoPliku = true,
            Folder  = "logs"
        }
    };

    // LINQ to XML — walidacja konfiguracji
    public List<string> Waliduj()
    {
        var bledy = new List<string>();

        if (!File.Exists(_sciezka)) return bledy;

        XDocument doc = XDocument.Load(_sciezka);

        // Sprawdź wymagane elementy
        if (doc.Root?.Element("baza-danych")?.Element("database")?.Value == "")
            bledy.Add("Brak nazwy bazy danych");

        // Sprawdź serwisy
        var serwisynienaladowane = doc.Root?.Element("serwisy")?
            .Elements("serwis")
            .Where(s => s.Attribute("enabled")?.Value == "true"
                     && string.IsNullOrEmpty(s.Element("url")?.Value))
            .Select(s => s.Attribute("nazwa")?.Value)
            .ToList();

        serwisynienaladowane?.ForEach(n =>
            bledy.Add($"Serwis '{n}' jest aktywny ale nie ma URL"));

        return bledy;
    }
}

// Uruchomienie
var konfSerwis = new KonfiguracjaSerwis("app.config.xml");
var konf = konfSerwis.Zaladuj();

Console.WriteLine($"Baza: {konf.Baza.ConnectionString}");
Console.WriteLine($"Cache TTL: {konf.Cache.TtlMinutes} min");
Console.WriteLine($"Serwisy: {konf.Serwisy.Count}");

foreach (var s in konf.Serwisy.Where(s => s.Enabled))
    Console.WriteLine($"  {s.Nazwa}: {s.Url} (headers: {s.Headers.Count})");

// Walidacja
var bledy2 = konfSerwis.Waliduj();
if (bledy2.Any())
{
    Console.WriteLine("\nBłędy konfiguracji:");
    bledy2.ForEach(b => Console.WriteLine($"  ⚠ {b}"));
}
else
    Console.WriteLine("\nKonfiguracja poprawna ✓");
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między XmlSerializer a XDocument?"** `XmlSerializer` — serializuje/deserializuje obiekty C# do/z XML. Wymaga konstruktora bezparametrowego, konfigurowany przez atrybuty (`[XmlElement]`, `[XmlAttribute]`). Idealny gdy masz modele C# i chcesz zapisać/wczytać je jako XML. `XDocument` — reprezentuje dokument XML jako drzewo obiektów (LINQ-friendly). Idealny do odczytu, transformacji i generowania XML bez mapowania na klasy. Możesz łączyć oba: `XDocument` do odczytu, `XmlSerializer` do mapowania.

**"Kiedy XmlReader zamiast XDocument?"** `XDocument` ładuje cały XML do pamięci — dla pliku 100MB to 100MB+ RAM. `XmlReader` jest strumieniowy — czyta element po elemencie, zużywa stałą ilość pamięci niezależnie od rozmiaru pliku. Używaj `XmlReader` gdy: plik XML jest duży (>10MB), potrzebujesz tylko części danych, piszesz parser który musi być wydajny. `XDocument` gdy: plik jest mały, potrzebujesz LINQ queries, modyfikujesz dokument.

**"Jak działa LINQ to XML i czym różni się od XPath?"** LINQ to XML to integracja LINQ z modelem obiektowym XML (XDocument/XElement). Używasz tych samych operatorów co dla kolekcji: `Where`, `Select`, `GroupBy`. Zalety: type-safe, IntelliSense, łatwa transformacja wyników do obiektów C#. XPath to oddzielny język zapytań (string) — bardziej zwięzły dla prostych przypadków, ale brak type safety i trudniejszy do debugowania. W C# preferuj LINQ to XML — jest bardziej naturalny i czytelny.

**"Jakie są ograniczenia XmlSerializer?"** Wymaga publicznego konstruktora bezparametrowego (inaczej wyjątek w runtime). Nie serializuje prywatnych właściwości i pól. Nie obsługuje słowników `Dictionary<K,V>` (trzeba własną implementację). Interfejsy i typy abstrakcyjne wymagają `[XmlInclude]` lub `[XmlElement(typeof(...))]`. Generics są trudne — `XmlSerializer(typeof(List<MyClass>))` działa, ale złożone hierarchie mogą sprawiać problemy.