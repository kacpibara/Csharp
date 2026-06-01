using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NJ = Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace _04_Exceptions_Files_Serializable;

// ===== MODELS - STJ (System.Text.Json) =====

public class UzytkownikSJ
{
    public int Id { get; set; }
    public string Imie { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime DataRejestracji { get; set; }
    public List<string> Role { get; set; } = new();
}

// Model z pelnym zestawem atrybutow STJ
public class ProduktDtoSJ
{
    [JsonPropertyName("product_id")]
    [JsonPropertyOrder(0)]
    public int Id { get; set; }

    [JsonPropertyName("product_name")]
    [JsonPropertyOrder(1)]
    [JsonRequired]                                                     // wyjatek jesli brak w JSON
    public string Nazwa { get; set; } = "";

    [JsonIgnore]                                                       // zawsze pomijany
    public string? HasloWewnetrzne { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]      // pomijaj gdy null
    public string? Opis { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]   // pomijaj gdy 0
    public decimal Cena { get; set; }

    [JsonInclude]                                                      // wymagane dla public field
    public string KodWewnetrzny = "WEW-001";

    [JsonPropertyOrder(2)]
    public string Kategoria { get; set; } = "";

    [JsonExtensionData]                                                // przechwytuje nieznane pola JSON
    public Dictionary<string, JsonElement>? DodatkowePola { get; set; }
}

// Niezmienne - deserializacja przez konstruktor
public class NiezmienniOsobaSJ
{
    public string Imie { get; }
    public string Nazwisko { get; }
    public int Wiek { get; }

    [JsonConstructor]                                                  // STJ uzyje tego konstruktora
    public NiezmienniOsobaSJ(string imie, string nazwisko, int wiek)
    {
        Imie = imie; Nazwisko = nazwisko; Wiek = wiek;
    }
}

// ===== POLIMORFIZM STJ (.NET 7+) =====

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$typ")]
[JsonDerivedType(typeof(PiesModelSJ), typeDiscriminator: "pies")]
[JsonDerivedType(typeof(KotModelSJ),  typeDiscriminator: "kot")]
public abstract class ZwierzeModelSJ
{
    public string Imie { get; set; } = "";
}

public class PiesModelSJ : ZwierzeModelSJ
{
    public string Rasa { get; set; } = "";
}

public class KotModelSJ : ZwierzeModelSJ
{
    public bool CzyKolczasty { get; set; }
}

// ===== ENUM + DTO =====

public enum StatusZamowieniaSJ { Nowe, WRealizacji, Wyslane, Dostarczone, Anulowane }

public class PozycjaDtoSJ
{
    public string NazwaProduktu { get; set; } = "";
    public int Ilosc { get; set; }
    public decimal Cena { get; set; }
}

public class ZamowienieDtoSJ
{
    [JsonPropertyOrder(0)] public int Id { get; set; }
    [JsonPropertyOrder(1)] public StatusZamowieniaSJ Status { get; set; }
    [JsonPropertyOrder(2)] public List<PozycjaDtoSJ> Pozycje { get; set; } = new();
}

// ===== MODELE DLA NEWTONSOFT =====

public class UzytkownikNwtSJ
{
    [NJ.JsonProperty("user_id")]
    public int Id { get; set; }

    [NJ.JsonProperty("full_name")]
    public string NazwaUzytkownika { get; set; } = "";

    [NJ.JsonProperty(NullValueHandling = NJ.NullValueHandling.Ignore)]
    public string? Email { get; set; }

    [NJ.JsonIgnore]
    public string? TokenWewnetrzny { get; set; }

    [NJ.JsonProperty(DefaultValueHandling = NJ.DefaultValueHandling.Ignore)]
    public int PunktLojalnosciowe { get; set; }
}

public abstract class ZwierzeNwtSJ { public string Imie { get; set; } = ""; }
public class PiesNwtSJ : ZwierzeNwtSJ { public string Rasa { get; set; } = ""; }
public class KotNwtSJ : ZwierzeNwtSJ { public bool CzyKolczasty { get; set; } }

// ===== KONWERTERY NIESTANDARDOWE =====

public class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateOnly.ParseExact(reader.GetString()!, "yyyy-MM-dd", null);

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
}

public class FleksybilnyEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return (T)(object)reader.GetInt32();
        return Enum.Parse<T>(reader.GetString()!, ignoreCase: true);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

public class NormalizedStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString()?.Trim().ToLowerInvariant();

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

// ===== PRAKTYCZNY SERWIS =====

public class JsonApiSerwisSJ
{
    private readonly JsonSerializerOptions _opcje = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,        // polskie znaki bez escape
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string SerializujOdpowiedz<T>(T dane) =>
        JsonSerializer.Serialize(dane, _opcje);

    public T? DeserializujBezpiecznie<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, _opcje); }
        catch (JsonException ex)
        {
            Console.WriteLine($"  [ERROR] DeserializujBezpiecznie: {ex.Message}");
            return default;
        }
    }

    public async Task<T?> DeserializujStreamAsync<T>(Stream stream) =>
        await JsonSerializer.DeserializeAsync<T>(stream, _opcje);

    public async Task SerializujDoStreamAsync<T>(Stream stream, T dane) =>
        await JsonSerializer.SerializeAsync(stream, dane, _opcje);

    public string MergeJson(string podstawa, string nadpisanie)
    {
        using var doc1 = JsonDocument.Parse(podstawa);
        using var doc2 = JsonDocument.Parse(nadpisanie);
        var wynik = new Dictionary<string, JsonElement>();

        foreach (var prop in doc1.RootElement.EnumerateObject())
            wynik[prop.Name] = prop.Value;
        foreach (var prop in doc2.RootElement.EnumerateObject())
            wynik[prop.Name] = prop.Value; // nadpisuje istniejace klucze

        return JsonSerializer.Serialize(wynik, _opcje);
    }
}

// ===== GLOWNA KLASA Z DEMO =====

public static class SerializacjaJSON
{
    // 1. Podstawy - serialize/deserialize, kolekcje, slownik
    public static void DemoPodstawy()
    {
        Console.WriteLine("\n--- STJ: Podstawy ---");

        var uzytkownik = new UzytkownikSJ
        {
            Id = 1, Imie = "Jan Kowalski", Email = "jan@example.com",
            DataRejestracji = new DateTime(2024, 1, 15),
            Role = ["admin", "user"]
        };

        // Serialize (obiekt → JSON string)
        string json = JsonSerializer.Serialize(uzytkownik);
        Console.WriteLine($"Serialize: {json[..60]}...");

        // Serialize z wciecia
        string jsonFormatted = JsonSerializer.Serialize(uzytkownik, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"WriteIndented: {jsonFormatted.Split('\n').Length} linii");

        // Deserialize (JSON string → obiekt)
        var odtworzony = JsonSerializer.Deserialize<UzytkownikSJ>(json);
        Console.WriteLine($"Deserialize: Id={odtworzony?.Id}, Imie={odtworzony?.Imie}");

        // Kolekcje
        var lista = new List<UzytkownikSJ> { uzytkownik, new() { Id = 2, Imie = "Anna" } };
        string jsonListy = JsonSerializer.Serialize(lista);
        var odtworzonaLista = JsonSerializer.Deserialize<List<UzytkownikSJ>>(jsonListy);
        Console.WriteLine($"List<T>: {odtworzonaLista?.Count} elementow");

        // Slownik
        var slownik = new Dictionary<string, int> { ["jan"] = 95, ["anna"] = 87, ["piotr"] = 72 };
        string jsonSlownik = JsonSerializer.Serialize(slownik);
        var odtworzonySlownik = JsonSerializer.Deserialize<Dictionary<string, int>>(jsonSlownik);
        Console.WriteLine($"Dictionary: {odtworzonySlownik?.Count} kluczy, jan={odtworzonySlownik?["jan"]}");
    }

    // 2. JsonSerializerOptions - wszystkie istotne ustawienia
    public static void DemoOpcje()
    {
        Console.WriteLine("\n--- STJ: JsonSerializerOptions ---");

        // PropertyNamingPolicy - camelCase
        var opcjeCamel = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var obj = new { NazwaWlasciwosci = "test", WiekUzytkownika = 30 };
        Console.WriteLine($"CamelCase:      {JsonSerializer.Serialize(obj, opcjeCamel)}");

        // SnakeCaseLower (.NET 8+)
        var opcjeSnake = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        Console.WriteLine($"SnakeCaseLower: {JsonSerializer.Serialize(obj, opcjeSnake)}");

        // PropertyNameCaseInsensitive - deserializacja bez uwzgledniania wielkosci liter
        var opcjeInsensitive = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var odtworzony = JsonSerializer.Deserialize<UzytkownikSJ>(
            """{"ID": 5, "IMIE": "Test"}""", opcjeInsensitive);
        Console.WriteLine($"CaseInsensitive: Id={odtworzony?.Id}, Imie={odtworzony?.Imie}");

        // UnsafeRelaxedJsonEscaping - polskie znaki bez \u escape
        var opcjePolskie = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string polskiTekst = JsonSerializer.Serialize("Żółw je języka"); // default: \u escape
        string polskiRelaxed = JsonSerializer.Serialize("Żółw je języka", opcjePolskie); // UTF-8 as-is
        Console.WriteLine($"Default encoding: {polskiTekst}");
        Console.WriteLine($"UnsafeRelaxed:    {polskiRelaxed}");

        // AllowTrailingCommas + ReadCommentHandling
        var opcjeRelaxed = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,  // ignoruj komentarze
            MaxDepth = 64
        };
        string jsonZKomentarzem = """{"Id": 1, /* komentarz */ "Imie": "Jan",}""";
        var wynik = JsonSerializer.Deserialize<UzytkownikSJ>(jsonZKomentarzem, opcjeRelaxed);
        Console.WriteLine($"AllowTrailingCommas + Skip Comments: Id={wynik?.Id}");

        // NumberHandling
        var opcjeNumery = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        var liczba = JsonSerializer.Deserialize<int>(""""5"""", opcjeNumery);
        Console.WriteLine($"NumberHandling AllowReadingFromString: {liczba}");

        // DefaultIgnoreCondition
        var opcjeIgnore = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var objZNull = new UzytkownikSJ { Id = 1 }; // Email = null
        Console.WriteLine($"WhenWritingNull: {JsonSerializer.Serialize(objZNull, opcjeIgnore)}");
    }

    // 3. Atrybuty STJ
    public static void DemoAtrybuty()
    {
        Console.WriteLine("\n--- STJ: Atrybuty ---");

        // ProduktDtoSJ z pelnym zestawem atrybutow
        var produkt = new ProduktDtoSJ
        {
            Id = 42,
            Nazwa = "Laptop",
            HasloWewnetrzne = "TAJNE",   // [JsonIgnore] - nie pojawi sie w JSON
            Opis = null,                  // [JsonIgnore(WhenWritingNull)] - nie pojawi sie
            Cena = 0,                     // [JsonIgnore(WhenWritingDefault)] - nie pojawi sie
            KodWewnetrzny = "LAP-001",    // [JsonInclude] - public field
            Kategoria = "Elektronika"
        };

        string json = JsonSerializer.Serialize(produkt, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"ProduktDtoSJ:\n{json}");

        // JsonExtensionData - nieznane pola zachowane w DodatkowePola
        string jsonZDodatkowymi = """
            {
                "product_id": 1,
                "product_name": "Telefon",
                "Kategoria": "Elektronika",
                "nieznane_pole": "wartosc",
                "extra": 123
            }
            """;
        var deserializowany = JsonSerializer.Deserialize<ProduktDtoSJ>(jsonZDodatkowymi);
        Console.WriteLine($"\nJsonExtensionData: {deserializowany?.DodatkowePola?.Count} dodatkowych pol");

        // NiezmienniOsobaSJ - [JsonConstructor] - STJ domyslnie case-sensitive, uzyj PascalCase
        string jsonOsoba = """{"Imie": "Anna", "Nazwisko": "Nowak", "Wiek": 28}""";
        var osoba = JsonSerializer.Deserialize<NiezmienniOsobaSJ>(jsonOsoba);
        Console.WriteLine($"[JsonConstructor]: {osoba?.Imie} {osoba?.Nazwisko}, {osoba?.Wiek} lat");
    }

    // 4. Polimorfizm - JsonPolymorphic + JsonDerivedType
    public static void DemoPolimorfizm()
    {
        Console.WriteLine("\n--- STJ: Polimorfizm (.NET 7+) ---");

        // Serializacja polimorficzna - dodaje "$typ" discriminator
        List<ZwierzeModelSJ> zwierzeta = [
            new PiesModelSJ { Imie = "Rex", Rasa = "Owczarek" },
            new KotModelSJ  { Imie = "Mruczek", CzyKolczasty = false }
        ];

        string json = JsonSerializer.Serialize(zwierzeta, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"Polimorficzny JSON:\n{json}");

        // Deserializacja - odtwarza wlasciwy typ na podstawie "$typ"
        var odtworzone = JsonSerializer.Deserialize<List<ZwierzeModelSJ>>(json);
        foreach (var z in odtworzone ?? [])
        {
            string typ = z switch
            {
                PiesModelSJ p => $"Pies (rasa: {p.Rasa})",
                KotModelSJ  k => $"Kot (kolczasty: {k.CzyKolczasty})",
                _              => "Nieznany"
            };
            Console.WriteLine($"  {z.Imie}: {typ}");
        }
    }

    // 5. Konwertery niestandardowe
    public static void DemoKonwertery()
    {
        Console.WriteLine("\n--- STJ: Konwertery niestandardowe ---");

        // DateOnlyJsonConverter
        var opcjeDateOnly = new JsonSerializerOptions();
        opcjeDateOnly.Converters.Add(new DateOnlyJsonConverter());

        var data = new DateOnly(2024, 3, 15);
        string jsonData = JsonSerializer.Serialize(data, opcjeDateOnly);
        Console.WriteLine($"DateOnly → JSON: {jsonData}");
        var odtworzonaData = JsonSerializer.Deserialize<DateOnly>(jsonData, opcjeDateOnly);
        Console.WriteLine($"JSON → DateOnly: {odtworzonaData}");

        // FleksybilnyEnumConverter<T> - akceptuje liczbe i string
        var opcjeEnum = new JsonSerializerOptions();
        opcjeEnum.Converters.Add(new FleksybilnyEnumConverter<StatusZamowieniaSJ>());

        // Liczba jako string
        var statusZLiczby = JsonSerializer.Deserialize<StatusZamowieniaSJ>("2", opcjeEnum);
        Console.WriteLine($"Enum z liczby 2: {statusZLiczby}");

        // String case-insensitive
        var statusZStringa = JsonSerializer.Deserialize<StatusZamowieniaSJ>(
            "\"wyslane\"", opcjeEnum);
        Console.WriteLine($"Enum z 'wyslane': {statusZStringa}");

        // Serializacja enum → string
        string jsonEnum = JsonSerializer.Serialize(StatusZamowieniaSJ.Dostarczone, opcjeEnum);
        Console.WriteLine($"Enum → JSON string: {jsonEnum}");

        // NormalizedStringConverter - trim + toLower przy deserializacji
        var opcjeNorm = new JsonSerializerOptions();
        opcjeNorm.Converters.Add(new NormalizedStringConverter());

        string znormalizowany = JsonSerializer.Deserialize<string>(
            "\"  JAN.KOWALSKI@EMAIL.COM  \"", opcjeNorm)!;  // JSON string = musi miec cudzyslowy
        Console.WriteLine($"NormalizedString: '{znormalizowany}'");
    }

    // 6. JsonDocument + JsonElement - read-only parsing bez deserializacji
    public static void DemoJsonDocument()
    {
        Console.WriteLine("\n--- STJ: JsonDocument / JsonElement ---");

        string json = """
            {
                "imie": "Jan",
                "wiek": 30,
                "aktywny": true,
                "wynagrodzenie": 8500.50,
                "role": ["admin", "user", "editor"],
                "adres": { "miasto": "Warszawa", "kod": "00-001" }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        // GetProperty + typy
        Console.WriteLine($"GetString:  {root.GetProperty("imie").GetString()}");
        Console.WriteLine($"GetInt32:   {root.GetProperty("wiek").GetInt32()}");
        Console.WriteLine($"GetBoolean: {root.GetProperty("aktywny").GetBoolean()}");
        Console.WriteLine($"GetDecimal: {root.GetProperty("wynagrodzenie").GetDecimal()}");

        // TryGetProperty - bezpieczny dostep
        if (root.TryGetProperty("email", out JsonElement emailEl))
            Console.WriteLine($"email: {emailEl.GetString()}");
        else
            Console.WriteLine("TryGetProperty('email'): nie znaleziono");

        // EnumerateArray - iteracja tablicy
        Console.Write("EnumerateArray role: ");
        foreach (var rola in root.GetProperty("role").EnumerateArray())
            Console.Write($"{rola.GetString()} ");
        Console.WriteLine();

        // EnumerateObject - iteracja obiektu
        Console.WriteLine("EnumerateObject adres:");
        foreach (var pole in root.GetProperty("adres").EnumerateObject())
            Console.WriteLine($"  {pole.Name} = {pole.Value.GetString()}");

        // ValueKind - sprawdzenie typu
        Console.WriteLine($"ValueKind imie:   {root.GetProperty("imie").ValueKind}");
        Console.WriteLine($"ValueKind wiek:   {root.GetProperty("wiek").ValueKind}");
        Console.WriteLine($"ValueKind role:   {root.GetProperty("role").ValueKind}");
        Console.WriteLine($"ValueKind aktywny:{root.GetProperty("aktywny").ValueKind}");
    }

    // 7. JsonNode - mutowalne drzewo JSON
    public static void DemoJsonNode()
    {
        Console.WriteLine("\n--- STJ: JsonNode (mutable) ---");

        string json = """{"imie": "Jan", "wiek": 30, "role": ["admin"]}""";

        // Parse
        var node = JsonNode.Parse(json)!;

        // Odczyt
        string? imie = node["imie"]?.GetValue<string>();
        int wiek = node["wiek"]!.GetValue<int>();
        Console.WriteLine($"Parse: imie={imie}, wiek={wiek}");

        // Modyfikacja istniejacych pol
        node["wiek"] = 31;
        node["email"] = "jan@example.com";

        // Dodanie zagniezzdzonego obiektu
        node["adres"] = new JsonObject
        {
            ["ulica"] = "Kwiatowa 5",
            ["miasto"] = "Warszawa",
            ["kod"] = "00-001"
        };

        // Dodanie do tablicy
        node["role"]!.AsArray().Add("editor");

        // Tworzenie JsonNode od zera
        var nowyNode = new JsonObject
        {
            ["id"] = 99,
            ["aktywny"] = true,
            ["tagi"] = new JsonArray("c#", "dotnet", "json")
        };

        // NET 8: JsonSerializerOptions przekazane do ToJsonString musi miec TypeInfoResolver
        // Rozwiazanie: dziedziczymy z JsonSerializerOptions.Default (ma resolver) + dodajemy WriteIndented
        var optsWrit = new JsonSerializerOptions(JsonSerializerOptions.Default) { WriteIndented = true };
        string wynik = node.ToJsonString(optsWrit);
        Console.WriteLine($"Zmodyfikowany:\n{wynik}");

        Console.WriteLine($"Od zera: {nowyNode.ToJsonString()}");
    }

    // 8. Newtonsoft.Json - alternatywna biblioteka
    public static void DemoNewtonsoft()
    {
        Console.WriteLine("\n--- Newtonsoft.Json ---");

        // Podstawowe serializacja/deserializacja
        var uzytkownik = new UzytkownikNwtSJ
        {
            Id = 1, NazwaUzytkownika = "Jan Kowalski",
            Email = "jan@example.com", TokenWewnetrzny = "SECRET"
        };

        string json = NJ.JsonConvert.SerializeObject(uzytkownik, NJ.Formatting.Indented);
        Console.WriteLine($"SerializeObject:\n{json}");

        var odtworzony = NJ.JsonConvert.DeserializeObject<UzytkownikNwtSJ>(json);
        Console.WriteLine($"DeserializeObject: Id={odtworzony?.Id}, Token={odtworzony?.TokenWewnetrzny ?? "null (JsonIgnore)"}");

        // JsonSerializerSettings - opcje globalne
        var settings = new NJ.JsonSerializerSettings
        {
            ContractResolver = new NJ.Serialization.CamelCasePropertyNamesContractResolver(),
            ReferenceLoopHandling = NJ.ReferenceLoopHandling.Ignore, // wazne dla EF Core!
            NullValueHandling = NJ.NullValueHandling.Ignore,
            Formatting = NJ.Formatting.Indented
        };
        var obj = new { NazwaWlasciwosci = "test", WartoscCalkowita = 42 };
        string jsonSettings = NJ.JsonConvert.SerializeObject(obj, settings);
        Console.WriteLine($"Settings (camelCase): {jsonSettings}");

        // JObject - dynamiczne manipulacje
        Console.WriteLine("\nJObject (dynamiczny):");
        var jobj = new JObject
        {
            ["id"] = 5,
            ["imie"] = "Anna",
            ["aktywna"] = true,
            ["wynik"] = 98.5
        };
        jobj["nowe_pole"] = "dodane dynamicznie";
        Console.WriteLine($"  JObject: {jobj.ToString(NJ.Formatting.None)}");

        // JObject.Parse + dostep
        var sparsowany = JObject.Parse("""{"produkt": "Laptop", "cena": 3499.99, "tagi": ["tech","sale"]}""");
        Console.WriteLine($"  Parse: produkt={sparsowany["produkt"]}, cena={sparsowany["cena"]}");

        // JArray
        var jarray = JArray.Parse("""[1, 2, 3, "cztery", true]""");
        Console.WriteLine($"  JArray: {jarray.Count} elementow, pierwszy={jarray[0]}");

        // LINQ to JSON
        Console.WriteLine("\nLINQ to JSON:");
        string jsonDanych = """
            [
                {"imie": "Jan", "wiek": 30, "wynik": 85},
                {"imie": "Anna", "wiek": 25, "wynik": 92},
                {"imie": "Piotr", "wiek": 35, "wynik": 78}
            ]
            """;
        var osoby = JArray.Parse(jsonDanych);

        // Filtrowanie i projekcja
        var powyzej80 = osoby
            .Where(o => o["wynik"]!.Value<int>() > 80)
            .Select(o => $"{o["imie"]} ({o["wynik"]})")
            .ToList();
        Console.WriteLine($"  Wynik > 80: {string.Join(", ", powyzej80)}");

        double srednia = osoby.Average(o => o["wynik"]!.Value<double>());
        Console.WriteLine($"  Srednia: {srednia:F1}");

        // TypeNameHandling.Objects - polimorfizm Newtonsoft
        // TypeNameHandling.Auto: dodaje $type gdy runtime != declared (nie dziala dla root)
        // TypeNameHandling.Objects: zawsze dodaje $type dla obiektow - bezpieczne dla polimorfizmu
        Console.WriteLine("\nTypeNameHandling.Objects (polimorfizm):");
        var settingsTypy = new NJ.JsonSerializerSettings
        {
            TypeNameHandling = NJ.TypeNameHandling.Objects, // $type dla kazdego obiektu
            Formatting = NJ.Formatting.None
        };

        ZwierzeNwtSJ pies = new PiesNwtSJ { Imie = "Rex", Rasa = "Owczarek" };
        string jsonPolimorficzny = NJ.JsonConvert.SerializeObject(pies, settingsTypy);
        Console.WriteLine($"  JSON z $type: {jsonPolimorficzny[..Math.Min(80, jsonPolimorficzny.Length)]}");

        ZwierzeNwtSJ odtworzonyTyp = NJ.JsonConvert.DeserializeObject<ZwierzeNwtSJ>(
            jsonPolimorficzny, settingsTypy)!;
        Console.WriteLine($"  Odtworzony typ: {odtworzonyTyp.GetType().Name}");

        // STJ vs Newtonsoft - podsumowanie roznic
        Console.WriteLine("\nSTJ vs Newtonsoft - kluczowe roznice:");
        Console.WriteLine("  STJ: wbudowany w .NET, szybszy, mniej pamieci");
        Console.WriteLine("  NWT: JObject/JArray, LINQ to JSON, ReferenceLoopHandling (EF Core!)");
        Console.WriteLine("  STJ: wymaga UnsafeRelaxedJsonEscaping dla polskich znakow");
        Console.WriteLine("  NWT: TypeNameHandling.Auto dla polimorfizmu");
        Console.WriteLine("  STJ: public fields wymagaja [JsonInclude]");
        Console.WriteLine("  NWT: serializuje public fields automatycznie");
    }

    // 9. Praktyczny JsonApiSerwisSJ
    public static async Task DemoJsonApiSerwis()
    {
        Console.WriteLine("\n--- JsonApiSerwisSJ ---");

        var serwis = new JsonApiSerwisSJ();

        var dane = new ZamowienieDtoSJ
        {
            Id = 100,
            Status = StatusZamowieniaSJ.WRealizacji,
            Pozycje =
            [
                new() { NazwaProduktu = "Laptop", Ilosc = 1, Cena = 3499.99m },
                new() { NazwaProduktu = "Mysz", Ilosc = 2, Cena = 89.99m }
            ]
        };

        // SerializujOdpowiedz
        string json = serwis.SerializujOdpowiedz(dane);
        Console.WriteLine($"SerializujOdpowiedz:\n{json}");

        // DeserializujBezpiecznie - poprawny JSON
        var odtworzony = serwis.DeserializujBezpiecznie<ZamowienieDtoSJ>(json);
        Console.WriteLine($"DeserializujBezpiecznie: Id={odtworzony?.Id}, " +
                          $"Status={odtworzony?.Status}, Pozycje={odtworzony?.Pozycje.Count}");

        // DeserializujBezpiecznie - niepoprawny JSON
        var blad = serwis.DeserializujBezpiecznie<ZamowienieDtoSJ>("{niepoprawny json}");
        Console.WriteLine($"Niepoprawny JSON: wynik={blad?.Id.ToString() ?? "null"}");

        // Stream - serialize/deserialize
        using var strumien = new MemoryStream();
        await serwis.SerializujDoStreamAsync(strumien, dane);
        strumien.Position = 0;
        var zeSrumienia = await serwis.DeserializujStreamAsync<ZamowienieDtoSJ>(strumien);
        Console.WriteLine($"Stream round-trip: {strumien.Length}B, Id={zeSrumienia?.Id}");

        // MergeJson - laczenie dwoch dokumentow JSON
        string podstawa  = """{"id": 1, "status": "stary", "priorytet": 5}""";
        string nadpisanie = """{"status": "nowy", "wersja": 2}""";
        string scalony = serwis.MergeJson(podstawa, nadpisanie);
        Console.WriteLine($"MergeJson: {scalony}");
    }
}
