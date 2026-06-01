using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace _06_ASPNetCore_RestAPI;

// ============================================================
// DATA ANNOTATIONS — ATRYBUTY
// ============================================================

public class RejestrujKlientaDto : IValidatableObject
{
    [Required(ErrorMessage = "Imię jest wymagane")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Imię: 2-50 znaków")]
    public string Imie { get; set; } = "";

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string Nazwisko { get; set; } = "";

    [Required]
    [EmailAddress(ErrorMessage = "Nieprawidłowy adres e-mail")]
    public string Email { get; set; } = "";

    [Phone(ErrorMessage = "Nieprawidłowy numer telefonu")]
    public string? Telefon { get; set; }

    [Required]
    [Nip(ErrorMessage = "Nieprawidłowy NIP")]
    public string Nip { get; set; } = "";

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Haslo { get; set; } = "";

    [Compare(nameof(Haslo), ErrorMessage = "Hasła muszą być identyczne")]
    public string PotwierdzHaslo { get; set; } = "";

    [Range(18, 120, ErrorMessage = "Wiek musi być między 18 a 120")]
    public int Wiek { get; set; }

    [Url(ErrorMessage = "Nieprawidłowy URL")]
    public string? StronaWww { get; set; }

    [RegularExpression(@"^\d{2}-\d{3}$", ErrorMessage = "Kod pocztowy: XX-XXX")]
    public string? KodPocztowy { get; set; }

    [MinLength(3, ErrorMessage = "Minimum 3 elementy")]
    [MaxLength(10, ErrorMessage = "Maksymalnie 10 elementów")]
    public List<string> Role { get; set; } = [];

    // IValidatableObject — walidacja przekrojowa
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Imie == Nazwisko)
            yield return new ValidationResult("Imię i nazwisko nie mogą być identyczne",
                [nameof(Imie), nameof(Nazwisko)]);

        if (Wiek < 18 && Role.Contains("admin"))
            yield return new ValidationResult("Administrator musi mieć ukończone 18 lat",
                [nameof(Wiek), nameof(Role)]);
    }
}

public class NoweZamowienieDto
{
    [Required]
    public int KlientId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Zamówienie musi zawierać przynajmniej jedną pozycję")]
    public List<PozycjaZamowieniaDto> Pozycje { get; set; } = [];

    [Required]
    public DateTime DataDostawy { get; set; }

    public DateTime DataZlozenia { get; set; } = DateTime.UtcNow;
}

public class PozycjaZamowieniaDto
{
    [Required]
    public int ProduktId { get; set; }

    [Range(1, 9999, ErrorMessage = "Ilość: 1-9999")]
    public int Ilosc { get; set; }

    [Range(0.01, 1_000_000, ErrorMessage = "Cena musi być dodatnia")]
    public decimal CenaJednostkowa { get; set; }
}

public class ZaladujPlikDto
{
    [Required]
    public string Nazwa { get; set; } = "";

    [DozwoloneRozszerzenia(".jpg", ".jpeg", ".png", ".pdf")]
    [MaxRozmiarPliku(5 * 1024 * 1024)] // 5 MB
    public IFormFile? Plik { get; set; }
}

// ============================================================
// NIESTANDARDOWE ATRYBUTY WALIDACJI
// ============================================================

[AttributeUsage(AttributeTargets.Property)]
public class NipAttribute : ValidationAttribute
{
    // Algorytm kontrolny NIP: cyfry pomnożone przez wagi, suma mod 11
    private static readonly int[] Wagi = [6, 5, 7, 2, 3, 4, 5, 6, 7];

    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        if (value is not string nip)
            return ValidationResult.Success;

        // Usuń myślniki i spacje
        nip = nip.Replace("-", "").Replace(" ", "");

        if (nip.Length != 10 || !nip.All(char.IsDigit))
            return new ValidationResult(ErrorMessage ?? "NIP musi składać się z 10 cyfr");

        var cyfry = nip.Select(c => c - '0').ToArray();
        var suma = cyfry.Take(9).Select((c, i) => c * Wagi[i]).Sum();
        var cyfraCyfrowa = suma % 11;

        if (cyfraCyfrowa == 10 || cyfraCyfrowa != cyfry[9])
            return new ValidationResult(ErrorMessage ?? "Nieprawidłowy NIP (błędna suma kontrolna)");

        return ValidationResult.Success;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class DozwoloneRozszerzeniaAttribute : ValidationAttribute
{
    private readonly string[] _dozwolone;

    public DozwoloneRozszerzeniaAttribute(params string[] dozwolone)
    {
        _dozwolone = dozwolone.Select(e => e.ToLowerInvariant()).ToArray();
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        if (value is not IFormFile plik)
            return ValidationResult.Success;

        var rozszerzenie = Path.GetExtension(plik.FileName).ToLowerInvariant();
        if (!_dozwolone.Contains(rozszerzenie))
            return new ValidationResult(
                $"Dozwolone rozszerzenia: {string.Join(", ", _dozwolone)}");

        return ValidationResult.Success;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class MaxRozmiarPlikuAttribute : ValidationAttribute
{
    private readonly long _maxBajtow;

    public MaxRozmiarPlikuAttribute(long maxBajtow)
    {
        _maxBajtow = maxBajtow;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        if (value is not IFormFile plik)
            return ValidationResult.Success;

        if (plik.Length > _maxBajtow)
            return new ValidationResult(
                $"Plik przekracza maksymalny rozmiar {_maxBajtow / 1024 / 1024} MB");

        return ValidationResult.Success;
    }
}

// ============================================================
// FLUENT VALIDATION
// ============================================================

public class RejestrujKlientaDtoValidator : AbstractValidator<RejestrujKlientaDto>
{
    private readonly IEmailSerwis _emailSerwis;

    public RejestrujKlientaDtoValidator(IEmailSerwis emailSerwis)
    {
        _emailSerwis = emailSerwis;

        // CascadeMode.Stop — zatrzymuje po pierwszym błędzie w regule
        RuleFor(x => x.Imie)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Imię jest wymagane")
            .MinimumLength(2).WithMessage("Imię min. 2 znaki")
            .MaximumLength(50).WithMessage("Imię max. 50 znaków")
            .Matches(@"^[\p{L}\s-]+$").WithMessage("Imię może zawierać tylko litery");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            // MustAsync — asynchroniczna walidacja (np. sprawdzenie unikalności w bazie)
            .MustAsync(async (email, ct) =>
            {
                await Task.Delay(1, ct); // symulacja zapytania do bazy
                return !email.StartsWith("banned@");
            }).WithMessage("Ten adres e-mail jest zablokowany");

        RuleFor(x => x.Haslo)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Hasło musi zawierać wielką literę")
            .Matches(@"[0-9]").WithMessage("Hasło musi zawierać cyfrę")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Hasło musi zawierać znak specjalny");

        RuleFor(x => x.PotwierdzHaslo)
            .Equal(x => x.Haslo).WithMessage("Hasła muszą być identyczne");

        RuleFor(x => x.Wiek)
            .InclusiveBetween(18, 120);

        // When — warunkowa reguła
        When(x => x.StronaWww is not null, () =>
        {
            RuleFor(x => x.StronaWww!)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("Nieprawidłowy URL strony WWW");
        });

        // RuleForEach — walidacja każdego elementu kolekcji
        RuleForEach(x => x.Role)
            .NotEmpty()
            .MaximumLength(50);
    }
}

public class NoweZamowienieValidator : AbstractValidator<NoweZamowienieDto>
{
    public NoweZamowienieValidator()
    {
        RuleFor(x => x.KlientId).GreaterThan(0);

        RuleFor(x => x.Pozycje)
            .NotEmpty().WithMessage("Zamówienie wymaga przynajmniej 1 pozycji")
            .Must(p => p.Count <= 100).WithMessage("Max 100 pozycji w zamówieniu");

        RuleForEach(x => x.Pozycje).SetValidator(new PozycjaValidator());

        RuleFor(x => x.DataDostawy)
            .GreaterThan(x => x.DataZlozenia)
            .WithMessage("Data dostawy musi być późniejsza niż data złożenia");
    }
}

public class PozycjaValidator : AbstractValidator<PozycjaZamowieniaDto>
{
    public PozycjaValidator()
    {
        RuleFor(x => x.ProduktId).GreaterThan(0);
        RuleFor(x => x.Ilosc).InclusiveBetween(1, 9999);
        RuleFor(x => x.CenaJednostkowa).GreaterThan(0);
    }
}

// ============================================================
// DATA ANNOTATIONS vs FLUENT VALIDATION — PORÓWNANIE
// ============================================================

public static class WalidacjaPorownanie
{
    public static void Opisz()
    {
        Console.WriteLine("DataAnnotations vs FluentValidation:");
        Console.WriteLine("  DA  + Proste reguły (required, range, string length)");
        Console.WriteLine("  DA  + Deklaratywne (atrybuty bezpośrednio na klasie)");
        Console.WriteLine("  DA  + Wsparcie EF Core (Required -> NOT NULL)");
        Console.WriteLine("  DA  - Trudne do testowania (unit test wymaga ValidationContext)");
        Console.WriteLine("  DA  - Brak async, brak DI, brak złożonej logiki");
        Console.WriteLine("  FV  + Łatwe testy jednostkowe (validator.TestValidate())");
        Console.WriteLine("  FV  + DI w konstruktorze walidatora");
        Console.WriteLine("  FV  + MustAsync — async walidacja (unikatowość w bazie)");
        Console.WriteLine("  FV  + Złożone reguły: When, Unless, RuleForEach, DependentRules");
        Console.WriteLine("  FV  + CascadeMode.Stop — zatrzymanie po pierwszym błędzie");
        Console.WriteLine("  FV  - Oddzielna klasa walidatora (więcej plików)");
    }
}
