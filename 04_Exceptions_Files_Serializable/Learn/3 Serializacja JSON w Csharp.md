### Serializacja JSON w C#

---

### 1. System.Text.Json — wbudowany, nowoczesny

csharp

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

// Podstawowe modele
public class Uzytkownik
{
    public int    Id       { get; set; }
    public string Imie     { get; set; } = "";
    public string Email    { get; set; } = "";
    public int    Wiek     { get; set; }
    public bool   Aktywny  { get; set; }
}

// --- SERIALIZACJA (obiekt → JSON) ---
var user = new Uzytkownik
{
    Id = 1, Imie = "Anna", Email = "anna@test.pl", Wiek = 25, Aktywny = true
};

// Minifikowany JSON (domyślny)
string json = JsonSerializer.Serialize(user);
Console.WriteLine(json);
// {"Id":1,"Imie":"Anna","Email":"anna@test.pl","Wiek":25,"Aktywny":true}

// Czytelny (indented)
string jsonPiekny = JsonSerializer.Serialize(user,
    new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(jsonPiekny);
// {
//   "Id": 1,
//   "Imie": "Anna",
//   ...
// }

// --- DESERIALIZACJA (JSON → obiekt) ---
string jsonWejscie = """{"Id":2,"Imie":"Bartek","Email":"b@test.pl","Wiek":30,"Aktywny":false}""";
Uzytkownik? user2 = JsonSerializer.Deserialize<Uzytkownik>(jsonWejscie);
Console.WriteLine($"{user2?.Imie}, {user2?.Wiek}");  // Bartek, 30

// Kolekcje
var lista = new List<Uzytkownik> { user, user2! };
string jsonListy = JsonSerializer.Serialize(lista);
List<Uzytkownik>? odczytana = JsonSerializer.Deserialize<List<Uzytkownik>>(jsonListy);
Console.WriteLine($"Odczytano: {odczytana?.Count} użytkowników");

// Słownik
var dict = new Dictionary<string, int> { ["ania"] = 25, ["bartek"] = 30 };
string jsonDict = JsonSerializer.Serialize(dict);
// {"ania":25,"bartek":30}
```

---

### 2. JsonSerializerOptions — konfiguracja

csharp

```csharp
// Globalne opcje — utwórz raz, używaj wielokrotnie (są thread-safe)
var opcje = new JsonSerializerOptions
{
    WriteIndented              = true,            // czytelne formatowanie
    PropertyNamingPolicy       = JsonNamingPolicy.CamelCase,  // camelCase nazwy
    PropertyNameCaseInsensitive= true,            // ignoruj wielkość liter przy odczycie
    DefaultIgnoreCondition     = JsonIgnoreCondition.WhenWritingNull,  // pomijaj null
    NumberHandling             = JsonNumberHandling.AllowReadingFromString,
    AllowTrailingCommas        = true,            // tolleruj przecinki na końcu
    ReadCommentHandling        = JsonCommentHandling.Skip,  // ignoruj komentarze //
    MaxDepth                   = 32,              // max głębokość zagnieżdżenia
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    // UnsafeRelaxedJsonEscaping — nie escape'uje polskich znaków!
};

// WAŻNE — bez UnsafeRelaxedJsonEscaping polskie znaki są escape'owane!
var testPolski = new { Tekst = "Zażółć gęślą jaźń" };
Console.WriteLine(JsonSerializer.Serialize(testPolski));
// {"Tekst":"Za\u017C\u00F3\u0142\u0107 g\u0119\u015Bl\u0105 ja\u017A\u0144"}

Console.WriteLine(JsonSerializer.Serialize(testPolski, opcje));
// {"tekst":"Zażółć gęślą jaźń"}  ← czytelne!

// CamelCase demo
var produkt = new { NazwaProduktu = "Laptop", CenaGross = 3500m };
Console.WriteLine(JsonSerializer.Serialize(produkt, opcje));
// {"nazwaProduktu":"Laptop","cenaGross":3500}  ← camelCase!

// Rejestracja własnych konwerterów
var opcjeZKonwerterami = new JsonSerializerOptions
{
    Converters =
    {
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),  // enum jako string
        new DateOnlyJsonConverter(),  // własny konwerter
    }
};
```

---

### 3. Atrybuty System.Text.Json

csharp

```csharp
public class ProduktDto
{
    // JsonPropertyName — własna nazwa w JSON
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("product_name")]
    public string Nazwa { get; set; } = "";

    // JsonIgnore — pomijaj to pole w JSON
    [JsonIgnore]
    public string HasloWewnetrzne { get; set; } = "";

    // JsonIgnore warunkowo
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OpcjonalnyOpis { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LiczbaRecenzji { get; set; }  // pomiń gdy 0

    // JsonInclude — włącz pole/właściwość które normalnie byłoby pominięte
    [JsonInclude]
    public decimal CenaWewnetrzna;  // publiczne POLE (nie property) — domyślnie pomijane!

    // JsonRequired — wymagany przy deserializacji (C# 11 / .NET 7+)
    [JsonRequired]
    public decimal Cena { get; set; }

    // JsonPropertyOrder — kolejność w JSON
    [JsonPropertyOrder(-1)]  // przed wszystkimi innymi
    public string Typ { get; set; } = "Produkt";

    [JsonPropertyOrder(100)]  // na końcu
    public DateTime DataDodania { get; set; } = DateTime.Now;

    // JsonExtensionData — przechwytuj nieznane pola
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DodatkowePola { get; set; }
}

// JsonConstructor — wskaż konstruktor do deserializacji
public class NiezmiennaOsoba
{
    public string Imie { get; }
    public int    Wiek { get; }

    [JsonConstructor]
    public NiezmiennaOsoba(string imie, int wiek)
    {
        Imie = imie;
        Wiek = wiek;
    }
}

// JsonDerivedType — polimorfizm (C# / .NET 7+)
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$typ")]
[JsonDerivedType(typeof(PiesModel), "pies")]
[JsonDerivedType(typeof(KotModel),  "kot")]
public abstract class ZwierzeModel
{
    public string Imie { get; set; } = "";
}

public class PiesModel : ZwierzeModel
{
    public string Rasa { get; set; } = "";
}

public class KotModel : ZwierzeModel
{
    public bool CzyDomowy { get; set; }
}

// Polimorfizm w akcji
ZwierzeModel[] zwierzeta =
{
    new PiesModel { Imie = "Rex",     Rasa = "Labrador"  },
    new KotModel  { Imie = "Mruczek", CzyDomowy = true   },
};

string jsonZwierzat = JsonSerializer.Serialize(zwierzeta);
Console.WriteLine(jsonZwierzat);
// [{"$typ":"pies","Rasa":"Labrador","Imie":"Rex"},
//  {"$typ":"kot","CzyDomowy":true,"Imie":"Mruczek"}]

ZwierzeModel[]? odczytaneZwierzeta = JsonSerializer.Deserialize<ZwierzeModel[]>(jsonZwierzat);
Console.WriteLine(odczytaneZwierzeta?[0].GetType().Name);  // PiesModel
```

---

### 4. Własne konwertery

csharp

```csharp
// JsonConverter<T> — własna logika serializacji dla danego typu

// Konwerter dla DateOnly (nie obsługiwany natywnie przed .NET 6)
public class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return DateOnly.ParseExact(reader.GetString()!, Format);
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateOnly value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format));
    }
}

// Konwerter — enum jako liczba LUB string
public class FleksybilnyEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            int wartosc = reader.GetInt32();
            return (T)Enum.ToObject(typeof(T), wartosc);
        }

        string? nazwa = reader.GetString();
        return Enum.Parse<T>(nazwa!, ignoreCase: true);
    }

    public override void Write(Utf8JsonWriter writer, T value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

// Konwerter dla stringów — trim i uppercase
public class NormalizedStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.GetString()?.Trim().ToUpper() ?? string.Empty;

    public override void Write(Utf8JsonWriter writer, string value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

// Użycie konwerterów
public enum StatusZamowienia { Nowe, WRealizacji, Dostarczone, Anulowane }

public class ZamowienieDto
{
    public int Id { get; set; }

    [JsonConverter(typeof(FleksybilnyEnumConverter<StatusZamowienia>))]
    public StatusZamowienia Status { get; set; }

    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateOnly DataZlozenia { get; set; }
}

var opcjeKonwertery = new JsonSerializerOptions
{
    Converters = { new DateOnlyJsonConverter() }
};

var zamowienie = new ZamowienieDto
{
    Id = 1,
    Status = StatusZamowienia.WRealizacji,
    DataZlozenia = new DateOnly(2024, 3, 15)
};

string json2 = JsonSerializer.Serialize(zamowienie, opcjeKonwertery);
Console.WriteLine(json2);
// {"Id":1,"Status":"WRealizacji","DataZlozenia":"2024-03-15"}

// Deserializacja z enum jako liczba (API zwraca 1 zamiast "WRealizacji")
string jsonZLiczba = """{"Id":2,"Status":2,"DataZlozenia":"2024-03-16"}""";
ZamowienieDto? z = JsonSerializer.Deserialize<ZamowienieDto>(jsonZLiczba, opcjeKonwertery);
Console.WriteLine($"{z?.Id}: {z?.Status}");  // 2: WRealizacji
```

---

### 5. JsonDocument i JsonElement — parsowanie bez typów

csharp

```csharp
// JsonDocument — parsuj JSON bez z góry znanych typów
// Przydatne gdy struktura JSON jest nieznana lub dynamiczna

string jsonNieznany = """
{
    "user": {
        "name": "Anna",
        "age": 25,
        "scores": [95, 87, 92],
        "address": {
            "city": "Warszawa",
            "zip": "00-001"
        }
    },
    "version": "1.0",
    "timestamp": 1709827200
}
""";

using JsonDocument doc = JsonDocument.Parse(jsonNieznany);
JsonElement root = doc.RootElement;

// Bezpieczny odczyt
string? imie = root
    .GetProperty("user")
    .GetProperty("name")
    .GetString();
Console.WriteLine(imie);  // Anna

int wiek = root.GetProperty("user").GetProperty("age").GetInt32();
Console.WriteLine(wiek);  // 25

// Tablica
JsonElement wyniki = root.GetProperty("user").GetProperty("scores");
foreach (JsonElement wynik in wyniki.EnumerateArray())
    Console.Write($"{wynik.GetInt32()} ");  // 95 87 92
Console.WriteLine();

// TryGetProperty — bezpieczny odczyt (nie rzuca gdy brak)
if (root.TryGetProperty("version", out JsonElement ver))
    Console.WriteLine($"Wersja: {ver.GetString()}");

// Iteracja po wszystkich polach
JsonElement user = root.GetProperty("user");
foreach (JsonProperty pole in user.EnumerateObject())
    Console.WriteLine($"  {pole.Name}: {pole.Value.ValueKind}");
// name: String
// age: Number
// scores: Array
// address: Object

// JsonElement.ValueKind — typ wartości
JsonElement adres = root.GetProperty("user").GetProperty("address");
Console.WriteLine(adres.ValueKind);  // Object

// Modyfikacja JSON — przez JsonNode (mutowalny odpowiednik JsonDocument)
using System.Text.Json.Nodes;

JsonNode? node = JsonNode.Parse(jsonNieznany);
node!["user"]!["name"] = "Bartek";        // zmiana wartości
node["user"]!["phone"] = "123-456-789";   // dodanie nowego pola
node["user"]!["age"]!.AsValue();          // dostęp do wartości

string zmodyfikowany = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(zmodyfikowany);
```

---

### 6. Newtonsoft.Json — porównanie

csharp

```csharp
// Newtonsoft.Json (Json.NET) — starsza biblioteka, bogatsza w funkcje
// Instalacja: dotnet add package Newtonsoft.Json

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

// Podstawowe użycie — bardzo podobne do STJ
var user3 = new Uzytkownik { Id = 1, Imie = "Anna", Wiek = 25 };

string json3 = JsonConvert.SerializeObject(user3);
string json4 = JsonConvert.SerializeObject(user3, Formatting.Indented);

Uzytkownik? user4 = JsonConvert.DeserializeObject<Uzytkownik>(json3);

// Atrybuty Newtonsoft.Json
public class ProduktNJ
{
    [JsonProperty("id")]                    // własna nazwa
    public int Id { get; set; }

    [JsonProperty("name", Required = Required.Always)]  // wymagane
    public string Nazwa { get; set; } = "";

    [JsonIgnore]                            // pomijaj
    public string Tajne { get; set; } = "";

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Opis { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    [System.ComponentModel.DefaultValue(0)]
    public int StanMagazynu { get; set; }

    // Własny konwerter przez atrybut
    [JsonConverter(typeof(NJDateConverter))]
    public DateTime DataDodania { get; set; }
}

// Konwerter Newtonsoft.Json
public class NJDateConverter : Newtonsoft.Json.Converters.DateTimeConverterBase
{
    private const string Format = "dd.MM.yyyy";

    public override object? ReadJson(JsonReader reader, Type objectType,
        object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
        string? s = reader.Value?.ToString();
        return s != null ? DateTime.ParseExact(s, Format, null) : DateTime.MinValue;
    }

    public override void WriteJson(JsonWriter writer, object? value,
        Newtonsoft.Json.JsonSerializer serializer)
    {
        writer.WriteValue(((DateTime)value!).ToString(Format));
    }
}

// Globalne ustawienia Newtonsoft
var settings = new JsonSerializerSettings
{
    Formatting             = Formatting.Indented,
    ContractResolver       = new CamelCasePropertyNamesContractResolver(),
    NullValueHandling      = NullValueHandling.Ignore,
    DateFormatString       = "yyyy-MM-dd",
    ReferenceLoopHandling  = ReferenceLoopHandling.Ignore,  // Kluczowe dla EF Core!
    Converters             = { new Newtonsoft.Json.Converters.StringEnumConverter() }
};

string jsonNJ = JsonConvert.SerializeObject(user3, settings);

// JObject — dynamiczne parsowanie (odpowiednik JsonDocument)
JObject jobj = JObject.Parse("""{"name":"Anna","age":25,"tags":["c#","dotnet"]}""");

string? name = jobj["name"]?.Value<string>();
int age = jobj["age"]!.Value<int>();
JArray tags = (JArray)jobj["tags"]!;
foreach (var tag in tags)
    Console.Write($"{tag} ");  // c# dotnet

// LINQ to JSON — Newtonsoft specjalność
var uzytkownicy = JArray.Parse("""
[
    {"id": 1, "name": "Anna", "active": true},
    {"id": 2, "name": "Bartek", "active": false},
    {"id": 3, "name": "Celina", "active": true}
]
""");

var aktywni = uzytkownicy
    .Where(u => u["active"]!.Value<bool>())
    .Select(u => u["name"]!.Value<string>());

Console.WriteLine(string.Join(", ", aktywni));  // Anna, Celina

// Polimorfizm — Newtonsoft ma wbudowane wsparcie
var nsSettings = new JsonSerializerSettings
{
    TypeNameHandling = TypeNameHandling.Auto  // zapisuje typ w JSON
};
```

---

### 7. System.Text.Json vs Newtonsoft.Json — kiedy co

csharp

```csharp
// PORÓWNANIE FUNKCJI:
// ┌─────────────────────────────┬──────────┬─────────────┐
// │ Funkcja                     │ STJ      │ Newtonsoft  │
// ├─────────────────────────────┼──────────┼─────────────┤
// │ Wydajność                   │ ✅ 2-3x   │ OK          │
// │ Wbudowany w .NET            │ ✅        │ NuGet       │
// │ Polskie znaki bez escape    │ ⚠️ opcja  │ ✅ domyślnie │
// │ JObject/dynamic parsing     │ JsonNode │ ✅ JObject   │
// │ Polimorfizm                 │ ✅ .NET 7 │ ✅ łatwy     │
// │ Pętle referencji (EF Core)  │ ❌ brak  │ ✅ opcja     │
// │ Publiczne pola              │ ⚠️ opt    │ ✅           │
// │ Konstruktor bez parametrów  │ ⚠️ wymaga│ ✅ nie wymaga│
// │ LINQ to JSON                │ ❌        │ ✅           │
// └─────────────────────────────┴──────────┴─────────────┘

// STJ — idealne dla: ASP.NET Core API, nowe projekty, wydajność
// Newtonsoft — idealne dla: stare projekty, EF Core lazy loading,
//              złożona serializacja, dynamiczne JSON

// Referencje okrężne — problem z EF Core
// Z EF Core używaj:
// dotnet add package Microsoft.AspNetCore.Mvc.NewtonsoftJson
// builder.Services.AddControllers().AddNewtonsoftJson(opt =>
//     opt.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore);
```

---

### 8. Praktyczny przykład — API z serializacją

csharp

```csharp
// Kompletny przykład — serwis z pełną serializacją JSON

// Modele z atrybutami
public record KlientDto(
    [property: JsonPropertyName("client_id")]
    int Id,

    [property: JsonPropertyName("full_name")]
    string PelneNazwisko,

    [property: JsonPropertyName("email")]
    string Email,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Telefon = null);

public record ZamowienieDto(
    [property: JsonPropertyName("order_id")]  int Id,
    [property: JsonPropertyName("client_id")] int KlientId,
    [property: JsonPropertyName("items")]     List<PozycjaDto> Pozycje,
    [property: JsonPropertyName("status")]
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    StatusZamowienia2 Status = StatusZamowienia2.Nowe);

public record PozycjaDto(
    [property: JsonPropertyName("product")] string Produkt,
    [property: JsonPropertyName("qty")]     int Ilosc,
    [property: JsonPropertyName("price")]   decimal Cena);

public enum StatusZamowienia2 { Nowe, Potwierdzone, Wyslane, Dostarczone }

// Serwis do obsługi JSON
public class JsonApiSerwis
{
    private static readonly JsonSerializerOptions _opcje = new()
    {
        WriteIndented              = true,
        PropertyNamingPolicy       = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive= true,
        DefaultIgnoreCondition     = JsonIgnoreCondition.WhenWritingNull,
        Converters                 = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Serializacja odpowiedzi API
    public string SerializujOdpowiedz<T>(T dane, bool sukces = true)
    {
        var odpowiedz = new
        {
            success   = sukces,
            timestamp = DateTime.UtcNow,
            data      = dane
        };
        return JsonSerializer.Serialize(odpowiedz, _opcje);
    }

    // Deserializacja z walidacją
    public T DeserializujBezpiecznie<T>(string json)
    {
        try
        {
            T? wynik = JsonSerializer.Deserialize<T>(json, _opcje);
            return wynik ?? throw new JsonException($"Deserializacja zwróciła null dla {typeof(T).Name}");
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"Nieprawidłowy JSON dla {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    // Przetwarzanie streamu (dla dużych danych)
    public async Task<T?> DeserializujStreamAsync<T>(Stream stream,
        CancellationToken ct = default) =>
        await JsonSerializer.DeserializeAsync<T>(stream, _opcje, ct);

    public async Task SerializujDoStreamAsync<T>(T dane, Stream stream,
        CancellationToken ct = default) =>
        await JsonSerializer.SerializeAsync(stream, dane, _opcje, ct);

    // Merge dwóch JSON (prosta implementacja)
    public string MergeJson(string bazowy, string nakladka)
    {
        using var docBaz = JsonDocument.Parse(bazowy);
        using var docNak = JsonDocument.Parse(nakladka);

        var node = JsonNode.Parse(bazowy)!;

        foreach (JsonProperty pole in docNak.RootElement.EnumerateObject())
        {
            node[pole.Name] = JsonNode.Parse(pole.Value.GetRawText());
        }

        return node.ToJsonString(_opcje);
    }
}

// Demonstracja
var serwis3 = new JsonApiSerwis();

var klient = new KlientDto(1, "Anna Kowalska", "anna@example.com", "+48123456789");
var zamowienie = new ZamowienieDto(
    Id: 1001,
    KlientId: 1,
    Pozycje: new List<PozycjaDto>
    {
        new("Laptop",     1, 3500m),
        new("Mysz",       2,  150m),
        new("Klawiatura", 1,  250m),
    },
    Status: StatusZamowienia2.Potwierdzone);

// Serializacja
Console.WriteLine("=== Klient ===");
Console.WriteLine(serwis3.SerializujOdpowiedz(klient));

Console.WriteLine("\n=== Zamówienie ===");
Console.WriteLine(serwis3.SerializujOdpowiedz(zamowienie));

// Deserializacja
string jsonZewnetrzny = """
{
    "client_id": 2,
    "full_name": "Bartek Nowak",
    "email": "bartek@example.com"
}
""";

KlientDto klient2 = serwis3.DeserializujBezpiecznie<KlientDto>(jsonZewnetrzny);
Console.WriteLine($"\nZdeserializowany: {klient2.PelneNazwisko}");

// Streaming dla dużych danych
using var ms = new MemoryStream();
await serwis3.SerializujDoStreamAsync(zamowienie, ms);
ms.Position = 0;
ZamowienieDto? z2 = await serwis3.DeserializujStreamAsync<ZamowienieDto>(ms);
Console.WriteLine($"Ze streamu: #{z2?.Id}, {z2?.Status}");

// Merge JSON
string bazowy2 = """{"name": "Anna", "age": 25}""";
string nakladka = """{"age": 26, "city": "Warszawa"}""";
string merged = serwis3.MergeJson(bazowy2, nakladka);
Console.WriteLine($"\nMerged: {merged}");
// {"name": "Anna", "age": 26, "city": "Warszawa"}
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między System.Text.Json a Newtonsoft.Json?"** STJ jest wbudowany w .NET, szybszy (2-3x) i bardziej restrykcyjny. Newtonsoft jest bardziej elastyczny: obsługuje pętle referencji (kluczowe dla EF Core lazy loading), nie wymaga konstruktora bez parametrów, ma LINQ to JSON (JObject). W nowych projektach używaj STJ, dla EF Core lub gdy potrzebujesz zaawansowanej serializacji — Newtonsoft. W ASP.NET Core domyślnie STJ, można zamienić na Newtonsoft przez `AddNewtonsoftJson()`.

**"Jak obsłużyć polimorfizm w JSON?"** STJ .NET 7+: atrybut `[JsonPolymorphic]` + `[JsonDerivedType]` na klasie bazowej — automatycznie dodaje discriminator `$type`. Newtonsoft: `TypeNameHandling.Auto` w settings. Manualnie (oba): własny `JsonConverter<BaseClass>` który w `Read` sprawdza discriminator i deserializuje do właściwego typu.

**"Co to `JsonExtensionData` i kiedy używać?"** `[JsonExtensionData]` na `Dictionary<string, JsonElement>` przechwytuje wszystkie pola JSON które nie mają odpowiadającego propertisa w klasie. Przydatne gdy API zwraca dodatkowe pola których nie znasz, chcesz je zachować i np. re-serializować. Bez tego atrybutu nieznane pola są po prostu ignorowane przy deserializacji.

**"Jak serializować do streamu zamiast do stringa?"** `JsonSerializer.SerializeAsync(stream, obiekt)` i `DeserializeAsync<T>(stream)`. Kluczowe dla wydajności gdy dane są duże — unikasz alokacji dużego stringa w pamięci. W ASP.NET Core response body jest streamem — kontroler zwracający `Ok(obiekt)` wewnętrznie serializuje bezpośrednio do response stream.