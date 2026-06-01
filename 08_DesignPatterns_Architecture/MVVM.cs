using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace _08_DesignPatterns_Architecture;

// ============================================================
// MVVM — Model-View-ViewModel
// Model=dane, View=UI (XAML), ViewModel=pośrednik
// View ←binding→ ViewModel ←→ Model
// ViewModel NIGDY nie importuje przestrzeni nazw UI!
// ============================================================

// ============================================================
// 1. MODEL — czyste dane domenowe, zero wiedzy o UI
// ============================================================

public class ProduktModel
{
    public int      Id           { get; set; }
    public string   Nazwa        { get; set; } = "";
    public decimal  Cena         { get; set; }
    public int      StanMagazynu { get; set; }
    public string   Kategoria    { get; set; } = "";
    public bool     Aktywny      { get; set; } = true;
    public DateTime DataDodania  { get; set; } = DateTime.UtcNow;
}

// Interfejsy serwisów — ViewModel zależy od ABSTRAKCJI
public interface IProduktSerwis
{
    Task<List<ProduktModel>> PobierzWszystkieAsync(CancellationToken ct = default);
    Task<ProduktModel?>      PobierzPoIdAsync(int id, CancellationToken ct = default);
    Task<int>                DodajAsync(ProduktModel produkt, CancellationToken ct = default);
    Task<bool>               AktualizujAsync(ProduktModel produkt, CancellationToken ct = default);
    Task<bool>               UsunAsync(int id, CancellationToken ct = default);
}

// In-memory stub do demonstracji
public class InMemoryProduktSerwis : IProduktSerwis
{
    private readonly List<ProduktModel> _data = new()
    {
        new() { Id = 1, Nazwa = "Laptop",    Cena = 3500m, StanMagazynu = 10, Kategoria = "IT" },
        new() { Id = 2, Nazwa = "Mysz",       Cena =  150m, StanMagazynu = 50, Kategoria = "IT" },
        new() { Id = 3, Nazwa = "Klawiatura", Cena =  250m, StanMagazynu =  0, Kategoria = "IT" }
    };

    public Task<List<ProduktModel>> PobierzWszystkieAsync(CancellationToken ct = default)
        => Task.FromResult(_data.ToList());
    public Task<ProduktModel?> PobierzPoIdAsync(int id, CancellationToken ct = default)
        => Task.FromResult(_data.FirstOrDefault(p => p.Id == id));
    public Task<int> DodajAsync(ProduktModel p, CancellationToken ct = default)
    {
        p.Id = (_data.Max(x => x.Id)) + 1;
        _data.Add(p);
        return Task.FromResult(p.Id);
    }
    public Task<bool> AktualizujAsync(ProduktModel p, CancellationToken ct = default)
    {
        var idx = _data.FindIndex(x => x.Id == p.Id);
        if (idx < 0) return Task.FromResult(false);
        _data[idx] = p;
        return Task.FromResult(true);
    }
    public Task<bool> UsunAsync(int id, CancellationToken ct = default)
        => Task.FromResult(_data.RemoveAll(p => p.Id == id) > 0);
}

// ============================================================
// 2. INotifyPropertyChanged — fundament data binding
// ============================================================
// UI subskrybuje PropertyChanged → automatyczne odświeżanie

public abstract class ViewModelBazowy : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // [CallerMemberName] eliminuje magic strings — kompilator wstawia nazwę właściwości
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // Helper — ustaw pole i powiadom tylko gdy wartość się zmieniła
    protected bool UstawPole<T>(ref T pole, T wartosc,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(pole, wartosc)) return false;
        pole = wartosc;
        OnPropertyChanged(propertyName);
        return true;
    }

    // Ustaw pole i powiadom dodatkowo powiązane właściwości
    protected bool UstawPoleIPowiazane<T>(ref T pole, T wartosc,
        [CallerMemberName] string? propertyName = null,
        params string[] powiazane)
    {
        if (!UstawPole(ref pole, wartosc, propertyName)) return false;
        foreach (string p in powiazane) OnPropertyChanged(p);
        return true;
    }
}

// Przykład INPC z właściwościami obliczanymi
public class ProstaProfil : ViewModelBazowy
{
    private string _imie  = "";
    private string _email = "";
    private int    _wiek  = 0;

    public string Imie
    {
        get => _imie;
        set => UstawPole(ref _imie, value);
    }

    public string Email
    {
        get => _email;
        set => UstawPole(ref _email, value);
    }

    // Zmiana Wiek → powiadamia też CzyPelnoletni i KategoriaWieku
    public int Wiek
    {
        get => _wiek;
        set => UstawPoleIPowiazane(ref _wiek, value,
            powiazane: new[] { nameof(CzyPelnoletni), nameof(KategoriaWieku) });
    }

    public bool   CzyPelnoletni  => Wiek >= 18;
    public string KategoriaWieku => Wiek switch
    {
        < 18 => "Nieletni",
        < 30 => "Młody dorosły",
        < 60 => "Dorosły",
        _    => "Senior"
    };
}

// ============================================================
// 3. ICommand — enkapsulacja akcji UI
// ============================================================
// NIE używamy System.Windows.Input.ICommand (wymaga WPF)
// Definiujemy własny interfejs (identyczny kontrakt)

public interface IMvvmCommand
{
    event EventHandler? CanExecuteChanged;
    bool CanExecute(object? parameter);
    void Execute(object? parameter);
}

// RelayCommand — synchroniczny
public class RelayCommand : IMvvmCommand
{
    private readonly Action<object?>      _wykonaj;
    private readonly Func<object?, bool>? _moznaWykonac;

    public RelayCommand(Action<object?> wykonaj, Func<object?, bool>? moznaWykonac = null)
    {
        _wykonaj      = wykonaj;
        _moznaWykonac = moznaWykonac;
    }

    // Przeciążenie bez parametru
    public RelayCommand(Action wykonaj, Func<bool>? moznaWykonac = null)
        : this(_ => wykonaj(), moznaWykonac is null ? null : _ => moznaWykonac()) { }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => _moznaWykonac?.Invoke(p) ?? true;
    public void Execute(object? p)    => _wykonaj(p);

    public void PowiadomOMozliwosci() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

// AsyncRelayCommand — asynchroniczny
// Execute jest async void (wymagane przez ICommand)
public class AsyncRelayCommand : IMvvmCommand
{
    private readonly Func<object?, CancellationToken, Task> _wykonaj;
    private readonly Func<object?, bool>?                   _moznaWykonac;
    private CancellationTokenSource? _cts;
    private bool _wykonywany;

    public bool Wykonywany
    {
        get => _wykonywany;
        private set { _wykonywany = value; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }

    public AsyncRelayCommand(Func<CancellationToken, Task> wykonaj, Func<bool>? moznaWykonac = null)
        : this((_, ct) => wykonaj(ct), moznaWykonac is null ? null : _ => moznaWykonac()) { }

    public AsyncRelayCommand(Func<object?, CancellationToken, Task> wykonaj,
        Func<object?, bool>? moznaWykonac = null)
    {
        _wykonaj      = wykonaj;
        _moznaWykonac = moznaWykonac;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => !_wykonywany && (_moznaWykonac?.Invoke(p) ?? true);

    public async void Execute(object? p)
    {
        if (!CanExecute(p)) return;
        _cts = new CancellationTokenSource();
        Wykonywany = true;
        try   { await _wykonaj(p, _cts.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine($"  [AsyncCmd] Błąd: {ex.Message}"); }
        finally { Wykonywany = false; _cts.Dispose(); _cts = null; }
    }

    // Metoda pomocnicza — wykonaj i poczekaj (do testów/demo)
    public Task WykonajAsync(CancellationToken ct = default)
        => _wykonaj(null, ct);

    public void Anuluj() => _cts?.Cancel();
}

// RelayCommand<T> — typowany parametr
public class RelayCommand<T> : IMvvmCommand
{
    private readonly Action<T?>      _wykonaj;
    private readonly Func<T?, bool>? _moznaWykonac;

    public RelayCommand(Action<T?> wykonaj, Func<T?, bool>? moznaWykonac = null)
    {
        _wykonaj = wykonaj; _moznaWykonac = moznaWykonac;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p)
        => (p is T t || p is null) && (_moznaWykonac?.Invoke(p is T x ? x : default) ?? true);
    public void Execute(object? p)
    {
        if (p is T t || p is null) _wykonaj(p is T x ? x : default);
    }
    public void PowiadomOMozliwosci() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

// ============================================================
// 4. ProduktViewModel — edycja z walidacją (IDataErrorInfo)
// ============================================================

public class ProduktViewModel : ViewModelBazowy, IDataErrorInfo
{
    private int     _id;
    private string  _nazwa     = "";
    private decimal _cena;
    private int     _stan;
    private string  _kategoria = "";
    private bool    _aktywny   = true;
    private bool    _zmieniony = false;

    public ProduktViewModel() { }
    public ProduktViewModel(ProduktModel m)
    {
        _id        = m.Id;
        _nazwa     = m.Nazwa;
        _cena      = m.Cena;
        _stan      = m.StanMagazynu;
        _kategoria = m.Kategoria;
        _aktywny   = m.Aktywny;
    }

    public int Id
    {
        get => _id;
        set => UstawPole(ref _id, value);
    }

    public string Nazwa
    {
        get => _nazwa;
        set { if (UstawPole(ref _nazwa, value)) { Zmieniony = true; OnPropertyChanged(nameof(CzyPoprawny)); } }
    }

    public decimal Cena
    {
        get => _cena;
        set { if (UstawPole(ref _cena, value)) { Zmieniony = true; OnPropertyChanged(nameof(CzyPoprawny)); } }
    }

    public int Stan
    {
        get => _stan;
        set { if (UstawPole(ref _stan, value)) { Zmieniony = true; OnPropertyChanged(nameof(StatusMagazynu)); } }
    }

    public string Kategoria
    {
        get => _kategoria;
        set => UstawPole(ref _kategoria, value);
    }

    public bool Aktywny
    {
        get => _aktywny;
        set => UstawPole(ref _aktywny, value);
    }

    public bool Zmieniony
    {
        get => _zmieniony;
        private set => UstawPoleIPowiazane(ref _zmieniony, value, powiazane: new[] { nameof(TytulOkna) });
    }

    // Właściwości obliczane
    public string StatusMagazynu => Stan switch
    {
        0     => "Brak w magazynie",
        <= 5  => "Niski stan",
        <= 20 => "Dostępny",
        _     => "Dobrze zaopatrzony"
    };

    public string TytulOkna => Id == 0 ? "Nowy produkt" : $"Edycja: {Nazwa}{(Zmieniony ? " *" : "")}";
    public bool   CzyNowy   => Id == 0;
    public bool   CzyPoprawny => string.IsNullOrEmpty(Error);

    // IDataErrorInfo — walidacja per pole
    public string Error
    {
        get
        {
            var bledy = new[] { this["Nazwa"], this["Cena"], this["Kategoria"] }
                .Where(e => !string.IsNullOrEmpty(e));
            return string.Join("; ", bledy);
        }
    }

    public string this[string col] => col switch
    {
        nameof(Nazwa)    => WalidujNazwe(),
        nameof(Cena)     => WalidujCene(),
        nameof(Kategoria)=> WalidujKategorie(),
        _                => ""
    };

    private string WalidujNazwe()
    {
        if (string.IsNullOrWhiteSpace(Nazwa)) return "Nazwa jest wymagana";
        if (Nazwa.Length < 2)  return "Nazwa musi mieć min. 2 znaki";
        if (Nazwa.Length > 200) return "Nazwa max. 200 znaków";
        return "";
    }
    private string WalidujCene()
    {
        if (Cena <= 0)       return "Cena musi być > 0";
        if (Cena > 999_999)  return "Cena max. 999 999";
        return "";
    }
    private string WalidujKategorie()
        => string.IsNullOrWhiteSpace(Kategoria) ? "Kategoria jest wymagana" : "";

    public ProduktModel ToModel() => new()
    {
        Id = Id, Nazwa = Nazwa, Cena = Cena,
        StanMagazynu = Stan, Kategoria = Kategoria, Aktywny = Aktywny
    };

    public void Reset() => Zmieniony = false;
}

// ============================================================
// 5. ProduktyListaViewModel — lista z komendami i statystykami
// ============================================================
// ObservableCollection<T> — powiadamia UI o zmianach kolekcji

public class ProduktyListaViewModel : ViewModelBazowy
{
    private readonly IProduktSerwis _serwis;

    private ObservableCollection<ProduktViewModel> _produkty = new();
    public ObservableCollection<ProduktViewModel> Produkty
    {
        get => _produkty;
        private set => UstawPole(ref _produkty, value);
    }

    private ProduktViewModel? _wybranyProdukt;
    public ProduktViewModel? WybranyProdukt
    {
        get => _wybranyProdukt;
        set
        {
            if (UstawPole(ref _wybranyProdukt, value))
            {
                _edytujCmd.PowiadomOMozliwosci();
                _usunCmd.PowiadomOMozliwosci();
            }
        }
    }

    private string _filtrTekst = "";
    public string FiltrTekst
    {
        get => _filtrTekst;
        set { if (UstawPole(ref _filtrTekst, value)) ZastosujFiltr(); }
    }

    private bool _laduje;
    public bool Laduje
    {
        get => _laduje;
        private set => UstawPoleIPowiazane(ref _laduje, value, powiazane: new[] { nameof(NieLaduje) });
    }
    public bool NieLaduje => !Laduje;

    private string _komunikat = "";
    public string Komunikat
    {
        get => _komunikat;
        private set => UstawPole(ref _komunikat, value);
    }

    // Statystyki — właściwości obliczane z kolekcji
    public int     LiczbaProduktow => Produkty.Count;
    public decimal SredniaCena     => Produkty.Any() ? Produkty.Average(p => p.Cena) : 0;
    public int     LacznyStanMag   => Produkty.Sum(p => p.Stan);

    // Komendy
    private readonly RelayCommand      _edytujCmd;
    private readonly RelayCommand      _usunCmd;

    public AsyncRelayCommand ZaladujKomenda  { get; }
    public RelayCommand      DodajKomenda    { get; }
    public RelayCommand      EdytujKomenda   => _edytujCmd;
    public RelayCommand      UsunKomenda     => _usunCmd;
    public AsyncRelayCommand OdswiezKomenda  { get; }

    // Zdarzenia dla View — bez referencji do konkretnego UI!
    public event Action<ProduktViewModel>? OtworzEdycjeZadane;
    public event Action<string, bool>?     PokazKomunikatZadane;

    public ProduktyListaViewModel(IProduktSerwis serwis)
    {
        _serwis = serwis;

        ZaladujKomenda = new AsyncRelayCommand(ZaladujProduktyAsync);
        OdswiezKomenda = new AsyncRelayCommand(ZaladujProduktyAsync);

        DodajKomenda = new RelayCommand(
            () => OtworzEdycjeZadane?.Invoke(new ProduktViewModel()),
            () => NieLaduje);

        _edytujCmd = new RelayCommand(
            () => { if (WybranyProdukt != null) OtworzEdycjeZadane?.Invoke(WybranyProdukt); },
            () => WybranyProdukt != null && NieLaduje);

        _usunCmd = new RelayCommand(
            () => UsunWybrany(),
            () => WybranyProdukt != null && NieLaduje);

        // Powiadamiaj o statystykach przy każdej zmianie kolekcji
        Produkty.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(LiczbaProduktow));
            OnPropertyChanged(nameof(SredniaCena));
            OnPropertyChanged(nameof(LacznyStanMag));
        };
    }

    private async Task ZaladujProduktyAsync(CancellationToken ct)
    {
        Laduje    = true;
        Komunikat = "";
        try
        {
            var lista = await _serwis.PobierzWszystkieAsync(ct);
            Produkty.Clear();
            foreach (var p in lista) Produkty.Add(new ProduktViewModel(p));
            Komunikat = $"Załadowano {lista.Count} produktów";
        }
        catch (OperationCanceledException) { Komunikat = "Anulowano ładowanie"; }
        catch (Exception ex)              { Komunikat = $"Błąd: {ex.Message}"; }
        finally                           { Laduje = false; }
    }

    private async void UsunWybrany()
    {
        if (WybranyProdukt is null) return;
        string nazwa = WybranyProdukt.Nazwa;
        Laduje = true;
        try
        {
            bool ok = await _serwis.UsunAsync(WybranyProdukt.Id);
            if (ok)
            {
                Produkty.Remove(WybranyProdukt);
                WybranyProdukt = null;
                Komunikat = $"Usunięto: {nazwa}";
                PokazKomunikatZadane?.Invoke(Komunikat, true);
            }
        }
        catch (Exception ex) { Komunikat = $"Błąd: {ex.Message}"; }
        finally              { Laduje = false; }
    }

    private void ZastosujFiltr()
        => Console.WriteLine($"  [VM] Filtr zastosowany: '{FiltrTekst}'");
}

// ============================================================
// RUNNER
// ============================================================

public static class MVVMDemo
{
    public static async Task Uruchom()
    {
        // --- INotifyPropertyChanged ---
        Console.WriteLine("  [MVVM] Model-View-ViewModel: View←binding→ViewModel←→Model");
        Console.WriteLine("  [INPC] INotifyPropertyChanged — fundament data binding");

        var profil = new ProstaProfil();
        var zmiany = new List<string>();
        profil.PropertyChanged += (_, e) => zmiany.Add(e.PropertyName!);

        profil.Imie  = "Anna";
        profil.Wiek  = 17;
        profil.Wiek  = 17;   // brak powiadomienia — ta sama wartość
        profil.Email = "a@test.pl";

        Console.WriteLine($"  [INPC] Zdarzenia: {string.Join(", ", zmiany)}");
        Console.WriteLine($"  [INPC] CzyPelnoletni(17): {profil.CzyPelnoletni}, kategoria: {profil.KategoriaWieku}");

        profil.Wiek = 25;
        Console.WriteLine($"  [INPC] Po zmianie(25): {profil.KategoriaWieku}");

        // --- RelayCommand ---
        Console.WriteLine("  [Command] RelayCommand — CanExecute/Execute");
        bool moznaWykonac = true;
        var cmd = new RelayCommand(
            () => Console.WriteLine("  [Command] Wykonano!"),
            () => moznaWykonac);

        Console.WriteLine($"  [Command] CanExecute=true: {cmd.CanExecute(null)}");
        cmd.Execute(null);
        moznaWykonac = false;
        Console.WriteLine($"  [Command] CanExecute=false: {cmd.CanExecute(null)}");

        // --- AsyncRelayCommand ---
        Console.WriteLine("  [AsyncCmd] AsyncRelayCommand — async operacje");
        var asyncCmd = new AsyncRelayCommand(async ct =>
        {
            Console.WriteLine("  [AsyncCmd] Start async...");
            await Task.Delay(20, ct);
            Console.WriteLine("  [AsyncCmd] Koniec async!");
        });
        await asyncCmd.WykonajAsync();

        // --- RelayCommand<T> ---
        var typedCmd = new RelayCommand<int>(
            id => Console.WriteLine($"  [TypedCmd] Edytuję produkt #{id}"));
        typedCmd.Execute(42);

        // --- ProduktViewModel (walidacja) ---
        Console.WriteLine("  [ProduktVM] IDataErrorInfo — walidacja per pole");
        var pvm = new ProduktViewModel();
        Console.WriteLine($"  [ProduktVM] Błąd 'Nazwa' (puste): '{pvm["Nazwa"]}'");
        Console.WriteLine($"  [ProduktVM] CzyPoprawny: {pvm.CzyPoprawny}");

        pvm.Nazwa    = "Laptop";
        pvm.Cena     = 3500m;
        pvm.Kategoria = "IT";
        Console.WriteLine($"  [ProduktVM] Po wypełnieniu: CzyPoprawny={pvm.CzyPoprawny}, Zmieniony={pvm.Zmieniony}");
        Console.WriteLine($"  [ProduktVM] Status: {pvm.StatusMagazynu}, Tytuł: {pvm.TytulOkna}");

        pvm.Stan = 3;
        Console.WriteLine($"  [ProduktVM] Stan=3: {pvm.StatusMagazynu}");

        // --- ProduktyListaViewModel (lista z komendami) ---
        Console.WriteLine("  [ListaVM] ProduktyListaViewModel z ObservableCollection");
        var vm = new ProduktyListaViewModel(new InMemoryProduktSerwis());

        vm.PropertyChanged     += (_, e) => Console.WriteLine($"  [ListaVM] PropertyChanged: {e.PropertyName}");
        vm.OtworzEdycjeZadane  += p => Console.WriteLine($"  [ListaVM] Otwórz edycję: {p.TytulOkna}");
        vm.PokazKomunikatZadane += (msg, ok) => Console.WriteLine($"  [ListaVM] Komunikat [{(ok?"OK":"ERR")}]: {msg}");

        await vm.ZaladujKomenda.WykonajAsync();
        Console.WriteLine($"  [ListaVM] Załadowano: {vm.LiczbaProduktow} prod., śr. cena: {vm.SredniaCena:C}");
        Console.WriteLine($"  [ListaVM] Stan magazynu: {vm.LacznyStanMag}");
        Console.WriteLine($"  [ListaVM] Komunikat: {vm.Komunikat}");

        // Wybierz produkt — sprawdź CanExecute komend
        Console.WriteLine($"  [ListaVM] EdytujCmd.CanExecute (bez wyboru): {vm.EdytujKomenda.CanExecute(null)}");
        vm.WybranyProdukt = vm.Produkty[0];
        Console.WriteLine($"  [ListaVM] EdytujCmd.CanExecute (z wyborem): {vm.EdytujKomenda.CanExecute(null)}");
        vm.DodajKomenda.Execute(null);
        vm.EdytujKomenda.Execute(null);

        // Filtrowanie
        vm.FiltrTekst = "Laptop";

        // XAML — DataContext = ViewModel, <TextBox Text="{Binding FiltrTekst, Mode=TwoWay}"/>
        Console.WriteLine("  [XAML] DataContext=ViewModel, {Binding Nazwa, Mode=TwoWay}");
        Console.WriteLine("  [XAML] <Button Command=\"{Binding EdytujKomenda}\"/> — CanExecute wyłącza przycisk!");
        Console.WriteLine("  [XAML] ObservableCollection → DataGrid auto-odświeżenie przy Add/Remove");
        Console.WriteLine("  [MVVM] Testowanie VM: zero UI, serwis podmieniony na mock");
    }
}
