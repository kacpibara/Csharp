### Walidacja w ASP.NET Core

Walidacja to **pierwsza linia obrony** — sprawdza poprawność danych wejściowych zanim trafią do logiki biznesowej.

---

### 1. DataAnnotations — atrybuty walidacyjne

csharp

```csharp
using System.ComponentModel.DataAnnotations;

// Podstawowe atrybuty walidacyjne
public class RejestrujKlientaDto
{
    // Wymagane
    [Required(ErrorMessage = "Imię jest wymagane")]
    public string Imie { get; set; } = "";

    [Required(ErrorMessage = "Nazwisko jest wymagane")]
    [StringLength(100, MinimumLength = 2,
        ErrorMessage = "Nazwisko musi mieć od 2 do 100 znaków")]
    public string Nazwisko { get; set; } = "";

    // Email
    [Required]
    [EmailAddress(ErrorMessage = "Nieprawidłowy format email")]
    [StringLength(200)]
    public string Email { get; set; } = "";

    // Telefon
    [Phone(ErrorMessage = "Nieprawidłowy numer telefonu")]
    [RegularExpression(@"^\+?[0-9\s\-]{9,15}$",
        ErrorMessage = "Format: +48 123 456 789")]
    public string? Telefon { get; set; }

    // Hasło
    [Required]
    [MinLength(8, ErrorMessage = "Hasło min. 8 znaków")]
    [MaxLength(100)]
    public string Haslo { get; set; } = "";

    // Porównanie pól
    [Required]
    [Compare(nameof(Haslo), ErrorMessage = "Hasła muszą być takie same")]
    public string PowtorzHaslo { get; set; } = "";

    // Zakres liczbowy
    [Range(18, 120, ErrorMessage = "Wiek musi być między 18 a 120")]
    public int Wiek { get; set; }

    // URL
    [Url(ErrorMessage = "Nieprawidłowy URL")]
    public string? StronaWww { get; set; }

    // Karta kredytowa
    [CreditCard(ErrorMessage = "Nieprawidłowy numer karty")]
    public string? NumerKarty { get; set; }

    // Zakres dat
    [DataType(DataType.Date)]
    public DateTime? DataUrodzenia { get; set; }

    // Enum
    [EnumDataType(typeof(TypKlienta))]
    public TypKlienta Typ { get; set; } = TypKlienta.Indywidualny;
}

public enum TypKlienta { Indywidualny, Firmowy, Premium }

// Atrybuty informacyjne (nie walidują — tylko formatują w UI)
public class ProduktDisplayDto
{
    [Display(Name = "Cena brutto")]
    [DataType(DataType.Currency)]
    public decimal Cena { get; set; }

    [Display(Name = "Data dodania")]
    [DataType(DataType.DateTime)]
    [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy}")]
    public DateTime DataDodania { get; set; }

    [Display(Name = "Opis produktu")]
    [DataType(DataType.MultilineText)]
    public string? Opis { get; set; }

    [Display(Name = "Aktywny")]
    public bool Aktywny { get; set; }
}
```

---

### 2. IValidatableObject — walidacja między polami

csharp

```csharp
// IValidatableObject — gdy potrzebujesz walidacji zależnej od innych pól
public class RezerwacjaDto : IValidatableObject
{
    [Required]
    public int HotelId { get; set; }

    [Required]
    public DateTime DataOd { get; set; }

    [Required]
    public DateTime DataDo { get; set; }

    [Range(1, 10)]
    public int LiczbaGosci { get; set; } = 1;

    [Range(1, 5)]
    public int LiczbaPokoiJednoosobowych { get; set; } = 0;

    [Range(0, 5)]
    public int LiczbaPokoiDwuosobowych { get; set; } = 0;

    public bool CzySniadanie { get; set; } = false;

    [StringLength(500)]
    public string? UwagisSpecjalne { get; set; }

    // Walidacja między polami
    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        // Walidacja dat
        if (DataDo <= DataOd)
            yield return new ValidationResult(
                "Data wymeldowania musi być po dacie zameldowania",
                new[] { nameof(DataDo) });

        if (DataOd < DateTime.Today)
            yield return new ValidationResult(
                "Data zameldowania nie może być w przeszłości",
                new[] { nameof(DataOd) });

        if ((DataDo - DataOd).TotalDays > 90)
            yield return new ValidationResult(
                "Rezerwacja nie może przekraczać 90 dni",
                new[] { nameof(DataDo), nameof(DataOd) });

        // Walidacja logiki biznesowej
        int calkowitaPojemnosc =
            LiczbaPokoiJednoosobowych + LiczbaPokoiDwuosobowych * 2;

        if (calkowitaPojemnosc < LiczbaGosci)
            yield return new ValidationResult(
                $"Wybrane pokoje mieszczą {calkowitaPojemnosc} osób, " +
                $"ale masz {LiczbaGosci} gości",
                new[] { nameof(LiczbaGosci),
                        nameof(LiczbaPokoiJednoosobowych),
                        nameof(LiczbaPokoiDwuosobowych) });

        if (LiczbaPokoiJednoosobowych == 0 && LiczbaPokoiDwuosobowych == 0)
            yield return new ValidationResult(
                "Wybierz przynajmniej jeden pokój",
                new[] { nameof(LiczbaPokoiJednoosobowych) });

        // Wielokrotne błędy — możesz dodawać wiele
        if (LiczbaGosci > 8 && !UwagisSpecjalne?.Contains("VIP", StringComparison.OrdinalIgnoreCase) == true)
            yield return new ValidationResult(
                "Duże grupy (>8 osób) wymagają specjalnych uwag",
                new[] { nameof(UwagisSpecjalne) });
    }
}
```

---

### 3. Własne atrybuty walidacyjne

csharp

```csharp
// Własny atrybut — NIP
public class NipAttribute : ValidationAttribute
{
    public NipAttribute()
        : base("Nieprawidłowy numer NIP") { }

    protected override ValidationResult? IsValid(
        object? value, ValidationContext ctx)
    {
        if (value is null) return ValidationResult.Success;  // null OK — [Required] to sprawdzi

        string nip = value.ToString()!.Replace("-", "").Replace(" ", "");

        if (nip.Length != 10 || !nip.All(char.IsDigit))
            return new ValidationResult(
                "NIP musi składać się z 10 cyfr",
                new[] { ctx.MemberName! });

        // Algorytm kontrolny NIP
        int[] wagi = { 6, 5, 7, 2, 3, 4, 5, 6, 7 };
        int suma = nip.Take(9).Select((c, i) => (c - '0') * wagi[i]).Sum();
        int cyfraKontrolna = suma % 11;

        if (cyfraKontrolna == 10 || cyfraKontrolna != (nip[9] - '0'))
            return new ValidationResult(
                GetErrorMessage(ctx),
                new[] { ctx.MemberName! });

        return ValidationResult.Success;
    }

    private string GetErrorMessage(ValidationContext ctx)
        => ErrorMessage ?? $"Pole '{ctx.DisplayName}' zawiera nieprawidłowy NIP";
}

// Własny atrybut — walidacja rozszerzenia pliku
public class DozwoloneRozszerzeniaAttribute : ValidationAttribute
{
    private readonly string[] _rozszerzenia;

    public DozwoloneRozszerzeniaAttribute(params string[] rozszerzenia)
    {
        _rozszerzenia = rozszerzenia.Select(r => r.ToLower()).ToArray();
    }

    protected override ValidationResult? IsValid(
        object? value, ValidationContext ctx)
    {
        if (value is not IFormFile plik)
            return ValidationResult.Success;

        var rozszerzenie = Path.GetExtension(plik.FileName)?.ToLower();

        if (!_rozszerzenia.Contains(rozszerzenie))
            return new ValidationResult(
                $"Dozwolone rozszerzenia: {string.Join(", ", _rozszerzenia)}",
                new[] { ctx.MemberName! });

        return ValidationResult.Success;
    }
}

// Własny atrybut — maksymalny rozmiar pliku
public class MaxRozmiarPlikuAttribute : ValidationAttribute
{
    private readonly int _maxMB;

    public MaxRozmiarPlikuAttribute(int maxMB) => _maxMB = maxMB;

    protected override ValidationResult? IsValid(
        object? value, ValidationContext ctx)
    {
        if (value is not IFormFile plik) return ValidationResult.Success;

        if (plik.Length > _maxMB * 1024 * 1024)
            return new ValidationResult(
                $"Plik nie może być większy niż {_maxMB}MB " +
                $"(rozmiar: {plik.Length / 1024 / 1024:F1}MB)",
                new[] { ctx.MemberName! });

        return ValidationResult.Success;
    }
}

// Asynchroniczny atrybut walidacyjny
// DataAnnotations nie wspiera async — obejście przez synchroniczny wrapper
// Dla async walidacji użyj FluentValidation!

// Użycie własnych atrybutów
public class FirmaDto
{
    [Required]
    [StringLength(200)]
    public string Nazwa { get; set; } = "";

    [Required]
    [Nip]                              // własny atrybut NIP
    public string Nip { get; set; } = "";
}

public class UploadZdjeciaDto
{
    [Required(ErrorMessage = "Wybierz plik")]
    [DozwoloneRozszerzenia(".jpg", ".jpeg", ".png", ".webp")]
    [MaxRozmiarPliku(5)]               // max 5MB
    public IFormFile Zdjecie { get; set; } = null!;

    [StringLength(100)]
    public string? Opis { get; set; }
}
```

---

### 4. ModelState — ręczna obsługa

csharp

```csharp
// ModelState — słownik błędów walidacji

[ApiController]
[Route("api/walidacja")]
public class WalidacjaController : ControllerBase
{
    private readonly IKlientSerwis _serwis;

    public WalidacjaController(IKlientSerwis serwis) => _serwis = serwis;

    // Z [ApiController] — auto-walidacja, 400 przed wejściem do akcji
    [HttpPost("auto")]
    public async Task<IActionResult> AutoWalidacja(
        [FromBody] RejestrujKlientaDto dto)
    {
        // Tu już wiemy że ModelState.IsValid == true!
        // [ApiController] zwrócił 400 jeśli było inaczej
        int id = await _serwis.ZarejestujAsync(dto);
        return CreatedAtAction(nameof(PobierzKlienta), new { id }, new { id });
    }

    // Bez [ApiController] lub po wyłączeniu auto-walidacji
    [HttpPost("manualna")]
    public async Task<IActionResult> ManualnaWalidacja(
        [FromBody] RejestrujKlientaDto dto)
    {
        // Sprawdź ModelState
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
            // Zwraca: 400 + słownik błędów
        }

        int id = await _serwis.ZarejestujAsync(dto);
        return Created($"/api/klienci/{id}", new { id });
    }

    // Ręczne dodawanie błędów po sprawdzeniu logiki biznesowej
    [HttpPost("biznesowa")]
    public async Task<IActionResult> WalidacjaBiznesowa(
        [FromBody] RejestrujKlientaDto dto)
    {
        // DataAnnotations przeszły — sprawdź logikę biznesową
        bool emailZajety = await _serwis.CzyEmailIstniejeAsync(dto.Email);
        if (emailZajety)
            ModelState.AddModelError(nameof(dto.Email),
                $"Email '{dto.Email}' jest już zajęty");

        bool telefonZajety = dto.Telefon != null
            && await _serwis.CzyTelefonIstniejeAsync(dto.Telefon);
        if (telefonZajety)
            ModelState.AddModelError(nameof(dto.Telefon),
                "Numer telefonu jest już przypisany do innego konta");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);  // RFC 7807 format

        int id = await _serwis.ZarejestujAsync(dto);
        return Created($"/api/klienci/{id}", new { id });
    }

    // Odczyt błędów z ModelState — różne sposoby
    [HttpPost("analiza")]
    public IActionResult AnalizaBledow([FromBody] RejestrujKlientaDto dto)
    {
        if (!ModelState.IsValid)
        {
            // 1. Wszystkie błędy jako słownik
            var slownik = ModelState
                .Where(m => m.Value!.Errors.Any())
                .ToDictionary(
                    m => m.Key,
                    m => m.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            // 2. Płaska lista błędów
            var lista = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            // 3. Pierwszy błąd
            string? pierwszyBlad = ModelState.Values
                .SelectMany(v => v.Errors)
                .FirstOrDefault()?.ErrorMessage;

            // 4. Błędy konkretnego pola
            var bladyEmaila = ModelState["Email"]?.Errors
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new
            {
                wiadomosc = "Błędy walidacji",
                bledy     = slownik,
                liczba    = lista.Count
            });
        }

        return Ok("Walidacja OK");
    }

    [HttpGet("{id:int}", Name = "PobierzKlienta")]
    public IActionResult PobierzKlienta(int id) => Ok(new { id });
}

// Konfiguracja formatu odpowiedzi błędów — Program.cs
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(opt =>
{
    // Własny format odpowiedzi 400
    opt.InvalidModelStateResponseFactory = ctx =>
    {
        var bledy = ctx.ModelState
            .Where(m => m.Value!.Errors.Any())
            .ToDictionary(
                m => m.Key,
                m => m.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        var odpowiedz = new
        {
            tytul    = "Błędy walidacji danych wejściowych",
            status   = 400,
            bledy,
            traceId  = ctx.HttpContext.TraceIdentifier,
            czas     = DateTime.UtcNow
        };

        return new BadRequestObjectResult(odpowiedz);
    };

    // Wyłącz auto-walidację gdy chcesz ręcznie
    // opt.SuppressModelStateInvalidFilter = true;
});
```

---

### 5. FluentValidation — zaawansowana walidacja

csharp

```csharp
// dotnet add package FluentValidation.AspNetCore

using FluentValidation;

// Walidator dla DTO rejestracji
public class RejestrujKlientaDtoValidator
    : AbstractValidator<RejestrujKlientaDto>
{
    private readonly IKlientSerwis _serwis;

    public RejestrujKlientaDtoValidator(IKlientSerwis serwis)
    {
        _serwis = serwis;

        // Podstawowe reguły — odpowiednik DataAnnotations
        RuleFor(x => x.Imie)
            .NotEmpty().WithMessage("Imię jest wymagane")
            .MinimumLength(2).WithMessage("Imię min. 2 znaki")
            .MaximumLength(100).WithMessage("Imię max. 100 znaków")
            .Matches(@"^[a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ\s\-]+$")
                .WithMessage("Imię może zawierać tylko litery");

        RuleFor(x => x.Nazwisko)
            .NotEmpty().WithMessage("Nazwisko jest wymagane")
            .Length(2, 150);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("Nieprawidłowy email")
            .MaximumLength(200)
            // Async walidacja! — DataAnnotations tego nie może
            .MustAsync(async (email, ct) =>
                !await _serwis.CzyEmailIstniejeAsync(email))
            .WithMessage("Email jest już zajęty")
            .WithErrorCode("EMAIL_TAKEN");  // własny kod błędu

        RuleFor(x => x.Haslo)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Hasło min. 8 znaków")
            .Matches("[A-Z]").WithMessage("Hasło musi zawierać wielką literę")
            .Matches("[a-z]").WithMessage("Hasło musi zawierać małą literę")
            .Matches("[0-9]").WithMessage("Hasło musi zawierać cyfrę")
            .Matches("[^a-zA-Z0-9]").WithMessage("Hasło musi zawierać znak specjalny");

        RuleFor(x => x.PowtorzHaslo)
            .NotEmpty()
            .Equal(x => x.Haslo).WithMessage("Hasła muszą być takie same");

        RuleFor(x => x.Wiek)
            .InclusiveBetween(18, 120)
            .WithMessage("Wiek musi być między 18 a 120");

        // Walidacja warunkowa
        When(x => x.Telefon != null, () =>
        {
            RuleFor(x => x.Telefon)
                .Matches(@"^\+?[0-9\s\-]{9,15}$")
                .WithMessage("Nieprawidłowy numer telefonu");
        });

        // Walidacja zależna od innego pola
        RuleFor(x => x.PowtorzHaslo)
            .NotEmpty()
            .When(x => !string.IsNullOrEmpty(x.Haslo));
    }
}

// Złożony walidator z regułami grupowymi
public class NoweZamowienieValidator : AbstractValidator<NoweZamowienieDto>
{
    public NoweZamowienieValidator(IProduktSerwis produktSerwis)
    {
        // === KLIENT ===
        RuleFor(x => x.KlientId)
            .GreaterThan(0).WithMessage("Nieprawidłowy ID klienta");

        // === ADRES DOSTAWY — RuleSet dla grupy pól ===
        RuleFor(x => x.AdresDostawy).NotNull()
            .WithMessage("Adres dostawy jest wymagany");

        RuleFor(x => x.AdresDostawy.Ulica)
            .NotEmpty().MaximumLength(200)
            .When(x => x.AdresDostawy != null);

        RuleFor(x => x.AdresDostawy.Kod)
            .Matches(@"^\d{2}-\d{3}$")
            .WithMessage("Kod pocztowy format: 00-000")
            .When(x => x.AdresDostawy != null);

        // === POZYCJE — walidacja kolekcji ===
        RuleFor(x => x.Pozycje)
            .NotEmpty().WithMessage("Zamówienie musi mieć przynajmniej jedną pozycję")
            .Must(p => p.Count <= 50).WithMessage("Max 50 pozycji");

        // Walidacja każdego elementu kolekcji
        RuleForEach(x => x.Pozycje).SetValidator(
            new PozycjaZamowieniaValidator(produktSerwis));

        // === DATA DOSTAWY ===
        RuleFor(x => x.DataDostawy)
            .GreaterThan(DateTime.Today)
                .WithMessage("Data dostawy musi być przyszłością")
            .LessThanOrEqualTo(DateTime.Today.AddDays(30))
                .WithMessage("Max 30 dni do dostawy")
            .When(x => x.DataDostawy.HasValue);

        // === REGUŁY ZŁOŻONE ===
        RuleFor(x => x)
            .Must(z => z.Pozycje.Sum(p => p.Ilosc * p.CenaJednostkowa) <= 100_000)
            .WithMessage("Wartość zamówienia przekracza limit 100 000 PLN")
            .WithName("WartoscZamowienia");

        // Cascade mode — zatrzymaj przy pierwszym błędzie
        RuleFor(x => x.KlientId)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0)
            .MustAsync(async (id, ct) =>
                await produktSerwis.KlientIstniejeAsync(id))
            .WithMessage("Klient nie istnieje");
    }
}

// Zagnieżdżony walidator
public class PozycjaZamowieniaValidator
    : AbstractValidator<PozycjaZamowieniaDto>
{
    public PozycjaZamowieniaValidator(IProduktSerwis serwis)
    {
        RuleFor(x => x.ProduktId)
            .GreaterThan(0)
            .MustAsync(async (id, ct) =>
                await serwis.ProduktIstniejeAsync(id))
            .WithMessage(p => $"Produkt #{p.ProduktId} nie istnieje");

        RuleFor(x => x.Ilosc)
            .GreaterThan(0).WithMessage("Ilość musi być > 0")
            .LessThanOrEqualTo(999).WithMessage("Max 999 sztuk");

        RuleFor(x => x.CenaJednostkowa)
            .GreaterThan(0).WithMessage("Cena musi być > 0")
            .ScalePrecision(2, 18).WithMessage("Max 2 miejsca po przecinku");
    }
}

// DTOs
public class NoweZamowienieDto
{
    public int           KlientId     { get; set; }
    public AdresDto      AdresDostawy { get; set; } = null!;
    public List<PozycjaZamowieniaDto> Pozycje { get; set; } = new();
    public DateTime?     DataDostawy  { get; set; }
}
public class AdresDto
{
    public string Ulica  { get; set; } = "";
    public string Miasto { get; set; } = "";
    public string Kod    { get; set; } = "";
}
public class PozycjaZamowieniaDto
{
    public int     ProduktId       { get; set; }
    public int     Ilosc           { get; set; }
    public decimal CenaJednostkowa { get; set; }
}
```

---

### 6. Rejestracja FluentValidation

csharp

```csharp
// Program.cs

// Opcja 1 — auto-rejestracja wszystkich walidatorów z assembly
builder.Services.AddFluentValidationAutoValidation()
    .AddValidatorsFromAssemblyContaining<RejestrujKlientaDtoValidator>();

// Opcja 2 — ręczna rejestracja
builder.Services.AddScoped<IValidator<RejestrujKlientaDto>,
    RejestrujKlientaDtoValidator>();
builder.Services.AddScoped<IValidator<NoweZamowienieDto>,
    NoweZamowienieValidator>();

// Opcja 3 — FluentValidation zastępuje DataAnnotations
builder.Services.AddFluentValidationAutoValidation(opt =>
{
    opt.DisableDataAnnotationsValidation = true;  // wyłącz DataAnnotations!
});

// Użycie w kontrolerze — automatyczne (przez middleware)
[ApiController]
[Route("api/zamowienia")]
public class ZamowieniaController : ControllerBase
{
    private readonly IZamowieniaSerwis _serwis;

    public ZamowieniaController(IZamowieniaSerwis serwis)
        => _serwis = serwis;

    [HttpPost]
    public async Task<IActionResult> Zloz(
        [FromBody] NoweZamowienieDto dto)
    {
        // FluentValidation uruchamia się automatycznie
        // [ApiController] zwraca 400 gdy błędy
        int id = await _serwis.ZlozAsync(dto);
        return CreatedAtAction(nameof(Pobierz), new { id }, new { id });
    }

    // Manualne użycie walidatora
    [HttpPost("manual")]
    public async Task<IActionResult> ZlozManualnie(
        [FromBody] NoweZamowienieDto dto,
        [FromServices] IValidator<NoweZamowienieDto> validator)
    {
        // Ręczna walidacja — więcej kontroli
        ValidationResult wynik = await validator.ValidateAsync(dto);

        if (!wynik.IsValid)
        {
            // FluentValidation → ModelState
            wynik.AddToModelState(ModelState);
            return ValidationProblem(ModelState);

            // Lub własny format
            var bledy = wynik.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new
                    {
                        e.ErrorMessage,
                        e.ErrorCode,
                        e.AttemptedValue
                    }).ToList());

            return BadRequest(new { bledy });
        }

        int id = await _serwis.ZlozAsync(dto);
        return Created($"/api/zamowienia/{id}", new { id });
    }

    [HttpGet("{id:int}", Name = "Pobierz")]
    public IActionResult Pobierz(int id) => Ok(new { id });
}
```

---

### 7. DataAnnotations vs FluentValidation

csharp

```csharp
// Ten sam DTO — dwa podejścia

// === DataAnnotations ===
public class ProduktDtoDA
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Nazwa { get; set; } = "";

    [Range(0.01, 999999)]
    public decimal Cena { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int KategoriaId { get; set; }

    // Problem: brak async, ograniczone komunikaty, trudna walidacja między polami
}

// === FluentValidation — ten sam efekt ===
public class ProduktDtoFV
{
    public string  Nazwa       { get; set; } = "";
    public decimal Cena        { get; set; }
    public int     KategoriaId { get; set; }
}

public class ProduktDtoFVValidator : AbstractValidator<ProduktDtoFV>
{
    public ProduktDtoFVValidator(IKategoriaRepo repo)
    {
        RuleFor(x => x.Nazwa)
            .NotEmpty()
            .Length(2, 200);

        RuleFor(x => x.Cena)
            .GreaterThan(0.01m)
            .LessThanOrEqualTo(999999m);

        RuleFor(x => x.KategoriaId)
            .GreaterThan(0)
            // BONUS: async walidacja — DataAnnotations tego nie umie!
            .MustAsync(async (id, ct) => await repo.IstniejeAsync(id))
            .WithMessage("Kategoria nie istnieje");
    }
}

// KIEDY CO UŻYWAĆ:
// DataAnnotations:
// ✅ Proste modele CRUD
// ✅ Szybkie prototypy
// ✅ Współpraca z Entity Framework (atrybuty na encjach)
// ❌ Brak async walidacji
// ❌ Trudna walidacja warunkowa i między polami
// ❌ Logika w modelach (naruszenie SRP)

// FluentValidation:
// ✅ Złożona logika biznesowa
// ✅ Async walidacja (sprawdzanie w bazie)
// ✅ Walidacja warunkowa (When/Unless)
// ✅ Separacja odpowiedzialności — walidator osobno
// ✅ Testowalność — walidator to zwykła klasa
// ❌ Dodatkowa zależność
// ❌ Więcej kodu dla prostych przypadków
```

---

### 8. Praktyczny przykład — kompletna walidacja

csharp

```csharp
// Kompletny przykład dla rejestracji użytkownika

// DTO
public class RejestrujUzytkownikaDto
{
    public string   Imie         { get; set; } = "";
    public string   Nazwisko     { get; set; } = "";
    public string   Email        { get; set; } = "";
    public string   Haslo        { get; set; } = "";
    public string   PowtorzHaslo { get; set; } = "";
    public DateTime DataUrodzenia{ get; set; }
    public string?  Telefon      { get; set; }
    public bool     AcceptTerms  { get; set; }
}

// Walidator
public class RejestrujUzytkownikaDtoValidator
    : AbstractValidator<RejestrujUzytkownikaDto>
{
    public RejestrujUzytkownikaDtoValidator(IUzytkownikRepo repo)
    {
        // Imię i Nazwisko
        RuleFor(x => x.Imie)
            .NotEmpty().WithMessage("Imię jest wymagane")
            .Length(2, 50)
            .Matches(@"^[\p{L}\s\-]+$")
                .WithMessage("Imię może zawierać tylko litery");

        RuleFor(x => x.Nazwisko)
            .NotEmpty().WithMessage("Nazwisko jest wymagane")
            .Length(2, 100)
            .Matches(@"^[\p{L}\s\-]+$")
                .WithMessage("Nazwisko może zawierać tylko litery");

        // Email z async sprawdzeniem unikalności
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(200)
            .MustAsync(async (email, ct) =>
                !await repo.EmailIstniejeAsync(email))
            .WithMessage("Ten email jest już zarejestrowany");

        // Hasło — złożone reguły
        RuleFor(x => x.Haslo)
            .NotEmpty()
            .MinimumLength(8)
            .Must(HasloMaSilneMoznicy)
                .WithMessage("Hasło musi zawierać: " +
                    "wielką literę, małą literę, cyfrę i znak specjalny");

        RuleFor(x => x.PowtorzHaslo)
            .NotEmpty()
            .Equal(x => x.Haslo)
                .WithMessage("Hasła muszą być identyczne");

        // Data urodzenia — 18+
        RuleFor(x => x.DataUrodzenia)
            .NotEmpty()
            .Must(data => data <= DateTime.Today.AddYears(-18))
                .WithMessage("Musisz mieć ukończone 18 lat")
            .Must(data => data >= DateTime.Today.AddYears(-120))
                .WithMessage("Nieprawidłowa data urodzenia");

        // Telefon — opcjonalny ale gdy podany musi być poprawny
        When(x => !string.IsNullOrEmpty(x.Telefon), () =>
            RuleFor(x => x.Telefon)
                .Matches(@"^\+?[0-9\s\-\(\)]{9,20}$")
                .WithMessage("Nieprawidłowy format telefonu"));

        // Zgoda na warunki — musi być zaznaczona
        RuleFor(x => x.AcceptTerms)
            .Must(a => a).WithMessage("Musisz zaakceptować regulamin");
    }

    private static bool HasloMaSilneMoznicy(string haslo)
    {
        return haslo.Any(char.IsUpper)
            && haslo.Any(char.IsLower)
            && haslo.Any(char.IsDigit)
            && haslo.Any(c => !char.IsLetterOrDigit(c));
    }
}

// Kontroler
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUzytkownikSerwis _serwis;
    private readonly IValidator<RejestrujUzytkownikaDto> _validator;

    public AuthController(
        IUzytkownikSerwis serwis,
        IValidator<RejestrujUzytkownikaDto> validator)
    {
        _serwis    = serwis;
        _validator = validator;
    }

    [HttpPost("rejestracja")]
    public async Task<IActionResult> Rejestruj(
        [FromBody] RejestrujUzytkownikaDto dto,
        CancellationToken ct)
    {
        // Manualna walidacja — pełna kontrola nad odpowiedzią
        var wynik = await _validator.ValidateAsync(dto, ct);

        if (!wynik.IsValid)
        {
            var bledy = wynik.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            return UnprocessableEntity(new
            {
                tytul  = "Błędy walidacji",
                status = 422,
                bledy
            });
        }

        try
        {
            int id = await _serwis.ZarejestujAsync(dto, ct);
            return CreatedAtAction(
                nameof(PobierzProfil),
                new { id },
                new { id, wiadomosc = "Rejestracja pomyślna" });
        }
        catch (EmailZajetyException ex)
        {
            ModelState.AddModelError(nameof(dto.Email), ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpGet("profil/{id:int}", Name = "PobierzProfil")]
    public IActionResult PobierzProfil(int id) => Ok(new { id });
}

// Testy walidatora — łatwe dzięki FluentValidation!
public class RejestrujUzytkownikaDtoValidatorTests
{
    private readonly Mock<IUzytkownikRepo> _repoMock = new();
    private readonly RejestrujUzytkownikaDtoValidator _sut;

    public RejestrujUzytkownikaDtoValidatorTests()
    {
        _repoMock.Setup(r => r.EmailIstniejeAsync(It.IsAny<string>()))
            .ReturnsAsync(false);  // email wolny domyślnie

        _sut = new RejestrujUzytkownikaDtoValidator(_repoMock.Object);
    }

    [Fact]
    public async Task PoprawneDto_BezBledow()
    {
        var dto = PoprawneDto();
        var wynik = await _sut.ValidateAsync(dto);
        Assert.True(wynik.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("123")]           // tylko cyfry
    public async Task ZleImie_MaBledyWalidacji(string imie)
    {
        var dto = PoprawneDto() with { Imie = imie };
        var wynik = await _sut.ValidateAsync(dto);

        Assert.False(wynik.IsValid);
        Assert.Contains(wynik.Errors, e => e.PropertyName == nameof(dto.Imie));
    }

    [Fact]
    public async Task HaslaBezZnakowSpecjalnych_MaBladHasla()
    {
        var dto = PoprawneDto() with
        {
            Haslo        = "Haslo123",  // brak znaku specjalnego
            PowtorzHaslo = "Haslo123"
        };
        var wynik = await _sut.ValidateAsync(dto);

        Assert.False(wynik.IsValid);
        Assert.Contains(wynik.Errors,
            e => e.PropertyName == nameof(dto.Haslo));
    }

    [Fact]
    public async Task EmailZajety_MaBladEmaila()
    {
        _repoMock.Setup(r => r.EmailIstniejeAsync("zajety@test.pl"))
            .ReturnsAsync(true);  // email zajęty

        var dto = PoprawneDto() with { Email = "zajety@test.pl" };
        var wynik = await _sut.ValidateAsync(dto);

        Assert.False(wynik.IsValid);
        Assert.Contains(wynik.Errors,
            e => e.PropertyName == nameof(dto.Email)
              && e.ErrorMessage.Contains("już zarejestrowany"));
    }

    private static RejestrujUzytkownikaDto PoprawneDto() => new()
    {
        Imie          = "Jan",
        Nazwisko      = "Kowalski",
        Email         = "jan@test.pl",
        Haslo         = "Haslo123!",
        PowtorzHaslo  = "Haslo123!",
        DataUrodzenia = DateTime.Today.AddYears(-25),
        AcceptTerms   = true
    };
}

// Stub interfaces
public interface IKlientSerwis
{
    Task<int> ZarejestujAsync(RejestrujKlientaDto dto);
    Task<bool> CzyEmailIstniejeAsync(string email);
    Task<bool> CzyTelefonIstniejeAsync(string telefon);
    Task<bool> KlientIstniejeAsync(int id);
}
public interface IProduktSerwis
{
    Task<bool> ProduktIstniejeAsync(int id);
    Task<bool> KlientIstniejeAsync(int id);
}
public interface IKategoriaRepo { Task<bool> IstniejeAsync(int id); }
public interface IUzytkownikRepo { Task<bool> EmailIstniejeAsync(string email); }
public interface IUzytkownikSerwis { Task<int> ZarejestujAsync(RejestrujUzytkownikaDto dto, CancellationToken ct); }
public interface IZamowieniaSerwis { Task<int> ZlozAsync(NoweZamowienieDto dto); }
public class EmailZajetyException : Exception { public EmailZajetyException(string m) : base(m) { } }
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między DataAnnotations a FluentValidation?"** DataAnnotations to atrybuty na właściwościach modelu — proste, wbudowane, ale ograniczone. Brak async walidacji, trudna walidacja warunkowa i między polami, logika miesza się z modelem. FluentValidation to osobna klasa walidatora — separacja odpowiedzialności, pełna async walidacja, warunkowe reguły (`When`/`Unless`), walidacja kolekcji (`RuleForEach`), łatwe testowanie jako zwykła klasa. W praktyce: DataAnnotations dla prostych DTO, FluentValidation dla złożonej logiki biznesowej.

**"Co to ModelState i jak działa?"** ModelState to słownik błędów walidacji — klucz to nazwa pola, wartość to lista błędów. Wypełniany automatycznie przez model binding (gdy parsowanie się nie uda) i przez atrybuty DataAnnotations. Z `[ApiController]` — automatycznie zwraca 400 gdy `ModelState.IsValid == false`. Możesz ręcznie dodawać błędy przez `ModelState.AddModelError("pole", "komunikat")` dla logiki biznesowej. `ValidationProblem(ModelState)` zwraca format RFC 7807.

**"Kiedy `ValidationResult.Success` a kiedy `null`?"** Oba oznaczają sukces walidacji — `ValidationResult.Success` to singleton `new ValidationResult(null)`, a `null` też oznacza brak błędów. Framework akceptuje oba. Konwencja: zwracaj `ValidationResult.Success` dla jasności kodu, `null` też działa. Błąd zwracasz przez `new ValidationResult("komunikat", new[] { "NazwaPola" })`.

**"Jak testować FluentValidation?"** Walidator to zwykła klasa — tworzysz instancję ręcznie i wywołujesz `ValidateAsync(dto)`. Wstrzykujesz mocki zależności (np. `IKlientRepo`). Sprawdzasz `wynik.IsValid`, `wynik.Errors` przez asercje. Możesz testować konkretne pola: `Assert.Contains(wynik.Errors, e => e.PropertyName == "Email")`. FluentValidation ma też własne extension methods do testów: `wynik.ShouldHaveValidationErrorFor(x => x.Email)`.