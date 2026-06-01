### MVVM w C#

MVVM (Model-View-ViewModel) to wzorzec architektoniczny który **separuje logikę UI od logiki biznesowej** przez warstwę ViewModel.

---

### 1. Fundamenty MVVM

csharp

```csharp
// MVVM — trzy warstwy z jasno określonymi odpowiedzialnościami
//
// MODEL      — dane i logika biznesowa, BRAK wiedzy o UI
// VIEW       — XAML/HTML, BRAK logiki biznesowej, wiąże się z ViewModel
// VIEWMODEL  — pośrednik, wystawia dane i komendy dla View
//
// Przepływ:
// View ←binding→ ViewModel ←→ Model
//
// Kluczowe cechy:
// ✅ ViewModel NIGDY nie importuje przestrzeni nazw UI (np. System.Windows)
// ✅ View NIGDY nie zawiera logiki biznesowej
// ✅ ViewModel testowalny bez instancji UI
// ✅ Model całkowicie niezależny

// === MODEL — czyste dane domenowe ===
public class Produkt
{
    public int      Id           { get; set; }
    public string   Nazwa        { get; set; } = "";
    public decimal  Cena         { get; set; }
    public int      StanMagazynu { get; set; }
    public string   Kategoria    { get; set; } = "";
    public bool     Aktywny      { get; set; } = true;
    public DateTime DataDodania  { get; set; } = DateTime.UtcNow;
}

public class ZamowienieModel
{
    public int         Id        { get; set; }
    public int         KlientId  { get; set; }
    public List<Produkt> Produkty { get; set; } = new();
    public decimal     Suma      => Produkty.Sum(p => p.Cena);
    public DateTime    Data      { get; set; } = DateTime.UtcNow;
    public string      Status    { get; set; } = "Nowe";
}

// === INTERFEJSY SERWISÓW — ViewModel zależy od abstrakcji ===
public interface IProduktSerwis
{
    Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default);
    Task<Produkt?>      PobierzPoIdAsync(int id, CancellationToken ct = default);
    Task<int>           DodajAsync(Produkt produkt, CancellationToken ct = default);
    Task<bool>          AktualizujAsync(Produkt produkt, CancellationToken ct = default);
    Task<bool>          UsunAsync(int id, CancellationToken ct = default);
}
```

---

### 2. INotifyPropertyChanged — fundament data binding

csharp

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

// INotifyPropertyChanged — interfejs który mówi View
// "ta właściwość się zmieniła, odśwież UI"

// === BAZOWA KLASA VIEWMODEL ===
public abstract class ViewModelBazowy : INotifyPropertyChanged
{
    // Zdarzenie wymagane przez interfejs
    public event PropertyChangedEventHandler? PropertyChanged;

    // Metoda powiadomienia — [CallerMemberName] automatycznie podaje nazwę właściwości!
    protected virtual void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Pomocnik ustawiania właściwości z automatycznym powiadomieniem
    protected bool UstawPole<T>(
        ref T pole,
        T wartosc,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(pole, wartosc))
            return false;  // brak zmiany — nie powiadamiaj!

        pole = wartosc;
        OnPropertyChanged(propertyName);
        return true;  // true = wartość się zmieniła
    }

    // Ustawienie z dodatkowymi powiadomieniami powiązanych właściwości
    protected bool UstawPoleIPowiazane<T>(
        ref T pole,
        T wartosc,
        [CallerMemberName] string? propertyName = null,
        params string[] powiazaneWlasciwosci)
    {
        if (!UstawPole(ref pole, wartosc, propertyName))
            return false;

        foreach (string powiazana in powiazaneWlasciwosci)
            OnPropertyChanged(powiazana);

        return true;
    }
}

// === PROSTY PRZYKŁAD — właściwości z INPC ===
public class ProstaProfil : ViewModelBazowy
{
    private string _imie  = "";
    private string _email = "";
    private int    _wiek  = 0;

    public string Imie
    {
        get => _imie;
        set => UstawPole(ref _imie, value);
        // CallerMemberName = "Imie" — automatycznie!
    }

    public string Email
    {
        get => _email;
        set => UstawPole(ref _email, value);
    }

    public int Wiek
    {
        get => _wiek;
        set => UstawPoleIPowiazane(
            ref _wiek, value,
            powiazaneWlasciwosci: new[] { nameof(CzyPelnoletni), nameof(KategoriaWieku) });
        // Zmiana Wiek → powiadamia też CzyPelnoletni i KategoriaWieku
    }

    // Właściwości wyliczane — nie mają backing field
    public bool   CzyPelnoletni => Wiek >= 18;
    public string KategoriaWieku => Wiek switch
    {
        < 18  => "Nieletni",
        < 30  => "Młody dorosły",
        < 60  => "Dorosły",
        _     => "Senior"
    };

    public string PelneNazwisko => $"{Imie} ({Email})";
}

// Demo INPC
var profil = new ProstaProfil();
profil.PropertyChanged += (sender, e) =>
    Console.WriteLine($"Zmieniono: {e.PropertyName}");

profil.Imie  = "Anna";      // → "Zmieniono: Imie"
profil.Wiek  = 17;          // → "Zmieniono: Wiek"
                             // → "Zmieniono: CzyPelnoletni"
                             // → "Zmieniono: KategoriaWieku"
profil.Wiek  = 17;          // → brak powiadomienia (ta sama wartość!)
profil.Email = "a@test.pl"; // → "Zmieniono: Email"
```

---

### 3. RelayCommand — enkapsulacja akcji

csharp

```csharp
using System.Windows.Input;

// ICommand — interfejs dla przycisków i akcji w UI
public interface ICommand
{
    event EventHandler? CanExecuteChanged;
    bool CanExecute(object? parameter);
    void Execute(object? parameter);
}

// === RelayCommand — synchroniczny ===
public class RelayCommand : ICommand
{
    private readonly Action<object?> _wykonaj;
    private readonly Func<object?, bool>? _moznaWykonac;

    public RelayCommand(
        Action<object?> wykonaj,
        Func<object?, bool>? moznaWykonac = null)
    {
        _wykonaj      = wykonaj;
        _moznaWykonac = moznaWykonac;
    }

    // Wygodne przeciążenia bez parametru
    public RelayCommand(Action wykonaj, Func<bool>? moznaWykonac = null)
        : this(_ => wykonaj(), moznaWykonac is null ? null : _ => moznaWykonac()) { }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
        => _moznaWykonac?.Invoke(parameter) ?? true;

    public void Execute(object? parameter)
        => _wykonaj(parameter);

    // Wymuś re-ewaluację CanExecute
    public void PowiadomOMozliwosciWykonania()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

// === AsyncRelayCommand — asynchroniczny ===
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, CancellationToken, Task> _wykonaj;
    private readonly Func<object?, bool>? _moznaWykonac;
    private CancellationTokenSource? _cts;
    private bool _wykonywany = false;

    public bool Wykonywany
    {
        get => _wykonywany;
        private set
        {
            _wykonywany = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public AsyncRelayCommand(
        Func<CancellationToken, Task> wykonaj,
        Func<bool>? moznaWykonac = null)
        : this((_, ct) => wykonaj(ct),
               moznaWykonac is null ? null : _ => moznaWykonac()) { }

    public AsyncRelayCommand(
        Func<object?, CancellationToken, Task> wykonaj,
        Func<object?, bool>? moznaWykonac = null)
    {
        _wykonaj      = wykonaj;
        _moznaWykonac = moznaWykonac;
    }

    public event EventHandler? CanExecuteChanged;

    // Nie wykonuj gdy już trwa
    public bool CanExecute(object? parameter)
        => !_wykonywany && (_moznaWykonac?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _cts = new CancellationTokenSource();
        Wykonywany = true;

        try
        {
            await _wykonaj(parameter, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Anulowanie — OK
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd komendy: {ex.Message}");
        }
        finally
        {
            Wykonywany = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    public void Anuluj() => _cts?.Cancel();
}

// Generyczne wersje dla type safety
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?>      _wykonaj;
    private readonly Func<T?, bool>? _moznaWykonac;

    public RelayCommand(Action<T?> wykonaj, Func<T?, bool>? moznaWykonac = null)
    {
        _wykonaj      = wykonaj;
        _moznaWykonac = moznaWykonac;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (parameter is T typowany || parameter is null)
            return _moznaWykonac?.Invoke((T?)parameter) ?? true;
        return false;
    }

    public void Execute(object? parameter)
    {
        if (parameter is T typowany || parameter is null)
            _wykonaj((T?)parameter);
    }

    public void PowiadomOMozliwosciWykonania()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

---

### 4. Kompletny ViewModel — lista produktów

csharp

```csharp
using System.Collections.ObjectModel;

// ObservableCollection<T> — kolekcja która powiadamia UI o zmianach
// (dodanie/usunięcie elementów automatycznie odświeża listę w UI)

public class ProduktyListaViewModel : ViewModelBazowy
{
    private readonly IProduktSerwis _serwis;

    // === WŁAŚCIWOŚCI ===
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
                // Powiadom komendy że wybranie się zmieniło
                (_edytujKomenda as RelayCommand)?.PowiadomOMozliwosciWykonania();
                (_usunKomenda as RelayCommand)?.PowiadomOMozliwosciWykonania();
            }
        }
    }

    private string _filtrtekst = "";
    public string FiltrTekst
    {
        get => _filtrtekst;
        set
        {
            if (UstawPole(ref _filtrtekst, value))
                ZastosujFiltr();
        }
    }

    private bool _laduje = false;
    public bool Laduje
    {
        get => _laduje;
        private set => UstawPoleIPowiazane(
            ref _laduje, value,
            powiazaneWlasciwosci: new[] { nameof(NieLaduje) });
    }

    public bool NieLaduje => !Laduje;

    private string _komunikat = "";
    public string Komunikat
    {
        get => _komunikat;
        private set => UstawPole(ref _komunikat, value);
    }

    private bool _byledSukces;
    public bool BledSukces
    {
        get => _byledSukces;
        private set => UstawPole(ref _byledSukces, value);
    }

    // Statystyki — właściwości wyliczane z kolekcji
    public int    LiczbaProduktow => Produkty.Count;
    public decimal SredniaCena    => Produkty.Any()
        ? Produkty.Average(p => p.Cena) : 0;
    public int    LacznyStanMag   => Produkty.Sum(p => p.Stan);

    // === KOMENDY ===
    private readonly ICommand _zaladujKomenda;
    private readonly ICommand _dodajKomenda;
    private readonly ICommand _edytujKomenda;
    private readonly ICommand _usunKomenda;
    private readonly ICommand _odswiezKomenda;

    public ICommand ZaladujKomenda   => _zaladujKomenda;
    public ICommand DodajKomenda     => _dodajKomenda;
    public ICommand EdytujKomenda    => _edytujKomenda;
    public ICommand UsunKomenda      => _usunKomenda;
    public ICommand OdswiezKomenda   => _odswiezKomenda;

    // === ZDARZENIA dla View ===
    public event Action<ProduktViewModel>? OtworzEdycjeZadane;
    public event Action<string, bool>?     PokazKomunikatZadane;

    public ProduktyListaViewModel(IProduktSerwis serwis)
    {
        _serwis = serwis;

        // Inicjalizuj komendy
        _zaladujKomenda = new AsyncRelayCommand(
            ZaladujProduktyAsync);

        _dodajKomenda = new RelayCommand(
            () => OtworzEdycjeZadane?.Invoke(new ProduktViewModel()),
            () => NieLaduje);

        _edytujKomenda = new RelayCommand(
            () => { if (WybranyProdukt != null) OtworzEdycjeZadane?.Invoke(WybranyProdukt); },
            () => WybranyProdukt != null && NieLaduje);

        _usunKomenda = new AsyncRelayCommand(
            ct => UsunWybranyAsync(ct),
            () => WybranyProdukt != null && NieLaduje);

        _odswiezKomenda = new AsyncRelayCommand(
            ZaladujProduktyAsync);

        // Subskrybuj zmiany kolekcji dla statystyk
        Produkty.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(LiczbaProduktow));
            OnPropertyChanged(nameof(SredniaCena));
            OnPropertyChanged(nameof(LacznyStanMag));
        };
    }

    // === METODY PRYWATNE ===
    private async Task ZaladujProduktyAsync(CancellationToken ct)
    {
        Laduje    = true;
        Komunikat = "";

        try
        {
            var lista = await _serwis.PobierzWszystkieAsync(ct);

            Produkty.Clear();
            foreach (var p in lista)
                Produkty.Add(new ProduktViewModel(p));

            Komunikat  = $"Załadowano {lista.Count} produktów";
            BledSukces = true;
        }
        catch (OperationCanceledException)
        {
            Komunikat = "Anulowano ładowanie";
        }
        catch (Exception ex)
        {
            Komunikat  = $"Błąd: {ex.Message}";
            BledSukces = false;
        }
        finally
        {
            Laduje = false;
        }
    }

    private async Task UsunWybranyAsync(CancellationToken ct)
    {
        if (WybranyProdukt is null) return;

        string nazwaBackup = WybranyProdukt.Nazwa;
        Laduje = true;

        try
        {
            bool ok = await _serwis.UsunAsync(WybranyProdukt.Id, ct);

            if (ok)
            {
                Produkty.Remove(WybranyProdukt);
                WybranyProdukt = null;
                Komunikat      = $"Usunięto: {nazwaBackup}";
                BledSukces     = true;
                PokazKomunikatZadane?.Invoke(Komunikat, true);
            }
        }
        catch (Exception ex)
        {
            Komunikat  = $"Błąd usuwania: {ex.Message}";
            BledSukces = false;
        }
        finally
        {
            Laduje = false;
        }
    }

    private void ZastosujFiltr()
    {
        // W prawdziwej aplikacji filtruj ObservableCollection lub użyj CollectionView
        // Tu demonstracja konceptu
        Console.WriteLine($"Filtr: '{FiltrTekst}'");
    }
}
```

---

### 5. ProduktViewModel — edycja z walidacją

csharp

```csharp
// ViewModel dla pojedynczego produktu — edycja + walidacja

public class ProduktViewModel : ViewModelBazowy, IDataErrorInfo
{
    // === BACKING FIELDS ===
    private int     _id;
    private string  _nazwa    = "";
    private decimal _cena;
    private int     _stan;
    private string  _kategoria = "";
    private bool    _aktywny   = true;
    private bool    _zmieniony = false;

    // === KONSTRUKTORY ===
    public ProduktViewModel() { }  // Nowy produkt

    public ProduktViewModel(Produkt model)
    {
        _id        = model.Id;
        _nazwa     = model.Nazwa;
        _cena      = model.Cena;
        _stan      = model.StanMagazynu;
        _kategoria = model.Kategoria;
        _aktywny   = model.Aktywny;
    }

    // === WŁAŚCIWOŚCI Z WALIDACJĄ ===
    public int Id
    {
        get => _id;
        set => UstawPole(ref _id, value);
    }

    public string Nazwa
    {
        get => _nazwa;
        set
        {
            if (UstawPole(ref _nazwa, value))
            {
                Zmieniony = true;
                OnPropertyChanged(nameof(this["Nazwa"]));
                OnPropertyChanged(nameof(CzyPoprawny));
            }
        }
    }

    public decimal Cena
    {
        get => _cena;
        set
        {
            if (UstawPole(ref _cena, value))
            {
                Zmieniony = true;
                OnPropertyChanged(nameof(this["Cena"]));
                OnPropertyChanged(nameof(CzyPoprawny));
            }
        }
    }

    public int Stan
    {
        get => _stan;
        set
        {
            if (UstawPole(ref _stan, value))
            {
                Zmieniony  = true;
                OnPropertyChanged(nameof(StatusMagazynu));
            }
        }
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
        private set => UstawPoleIPowiazane(
            ref _zmieniony, value,
            powiazaneWlasciwosci: new[] { nameof(TytulOkna) });
    }

    // === WŁAŚCIWOŚCI WYLICZANE ===
    public string StatusMagazynu => Stan switch
    {
        0      => "Brak w magazynie",
        <= 5   => "Niski stan",
        <= 20  => "Dostępny",
        _      => "Dobrze zaopatrzony"
    };

    public string TytulOkna => Id == 0
        ? "Nowy produkt"
        : $"Edycja: {Nazwa}{(Zmieniony ? " *" : "")}";

    public bool CzyNowy     => Id == 0;
    public bool CzyPoprawny => string.IsNullOrEmpty(Error);

    // === IDataErrorInfo — walidacja per pole ===
    public string Error
    {
        get
        {
            var bledy = new[]
            {
                this["Nazwa"],
                this["Cena"],
                this["Kategoria"]
            }.Where(e => !string.IsNullOrEmpty(e));

            return string.Join("; ", bledy);
        }
    }

    // Indekser — walidacja konkretnego pola
    public string this[string columnName] => columnName switch
    {
        nameof(Nazwa)    => WalidujNazwe(),
        nameof(Cena)     => WalidujCene(),
        nameof(Kategoria)=> WalidujKategorie(),
        _                => ""
    };

    private string WalidujNazwe()
    {
        if (string.IsNullOrWhiteSpace(Nazwa))
            return "Nazwa jest wymagana";
        if (Nazwa.Length < 2)
            return "Nazwa musi mieć min. 2 znaki";
        if (Nazwa.Length > 200)
            return "Nazwa max. 200 znaków";
        return "";
    }

    private string WalidujCene()
    {
        if (Cena <= 0)
            return "Cena musi być większa od 0";
        if (Cena > 999_999)
            return "Cena nie może przekraczać 999 999";
        return "";
    }

    private string WalidujKategorie()
    {
        if (string.IsNullOrWhiteSpace(Kategoria))
            return "Kategoria jest wymagana";
        return "";
    }

    // === MAPOWANIE DO MODELU ===
    public Produkt ToModel() => new Produkt
    {
        Id           = Id,
        Nazwa        = Nazwa,
        Cena         = Cena,
        StanMagazynu = Stan,
        Kategoria    = Kategoria,
        Aktywny      = Aktywny
    };

    public void Reset()
    {
        Zmieniony = false;
    }
}
```

---

### 6. XAML — View z data binding

xml

```xml
<!-- WPF — ProduktyView.xaml -->
<!-- DataContext = ProduktyListaViewModel (ustawiony w code-behind lub DI) -->

<Window x:Class="Sklep.ProduktyView"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="{Binding TytulOkna}"
        Width="900" Height="600">

    <Window.Resources>
        <!-- Konwerter bool → Visibility -->
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- Toolbar -->
            <RowDefinition Height="Auto"/>  <!-- Filtr -->
            <RowDefinition Height="*"/>     <!-- Lista -->
            <RowDefinition Height="Auto"/>  <!-- Status -->
        </Grid.RowDefinitions>

        <!-- === TOOLBAR === -->
        <ToolBar Grid.Row="0">
            <!-- Command binding — CanExecute automatycznie wyłącza przycisk! -->
            <Button Command="{Binding ZaladujKomenda}" Content="Załaduj"/>
            <Button Command="{Binding DodajKomenda}"   Content="Dodaj"/>
            <Button Command="{Binding EdytujKomenda}"  Content="Edytuj"/>
            <Button Command="{Binding UsunKomenda}"    Content="Usuń"/>
            <Separator/>
            <Button Command="{Binding OdswiezKomenda}" Content="Odśwież"/>
        </ToolBar>

        <!-- === FILTR === -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="5">
            <Label Content="Szukaj:"/>
            <!-- TwoWay + UpdateSourceTrigger = natychmiastowe filtrowanie -->
            <TextBox
                Width="200"
                Text="{Binding FiltrTekst, Mode=TwoWay,
                               UpdateSourceTrigger=PropertyChanged}"/>
        </StackPanel>

        <!-- === ŁADOWANIE === -->
        <Grid Grid.Row="2">
            <!-- Indicator ładowania -->
            <ProgressBar
                IsIndeterminate="True"
                Height="4"
                VerticalAlignment="Top"
                Visibility="{Binding Laduje, Converter={StaticResource BoolToVis}}"/>

            <!-- Lista produktów -->
            <DataGrid
                ItemsSource="{Binding Produkty}"
                SelectedItem="{Binding WybranyProdukt, Mode=TwoWay}"
                AutoGenerateColumns="False"
                IsReadOnly="True"
                Margin="0,4,0,0">

                <DataGrid.Columns>
                    <DataGridTextColumn
                        Header="ID"
                        Binding="{Binding Id}"
                        Width="50"/>
                    <DataGridTextColumn
                        Header="Nazwa"
                        Binding="{Binding Nazwa}"
                        Width="*"/>
                    <DataGridTextColumn
                        Header="Cena"
                        Binding="{Binding Cena, StringFormat=C}"
                        Width="100"/>
                    <DataGridTextColumn
                        Header="Stan"
                        Binding="{Binding Stan}"
                        Width="80"/>
                    <!-- Kolumna wyliczana z ViewModel -->
                    <DataGridTextColumn
                        Header="Status"
                        Binding="{Binding StatusMagazynu}"
                        Width="150"/>
                    <DataGridCheckBoxColumn
                        Header="Aktywny"
                        Binding="{Binding Aktywny}"
                        Width="70"/>
                </DataGrid.Columns>
            </DataGrid>
        </Grid>

        <!-- === STATUS BAR === -->
        <StatusBar Grid.Row="3">
            <StatusBarItem>
                <TextBlock Text="{Binding Komunikat}"/>
            </StatusBarItem>
            <Separator/>
            <StatusBarItem>
                <TextBlock>
                    <Run Text="Produktów: "/>
                    <Run Text="{Binding LiczbaProduktow}"/>
                </TextBlock>
            </StatusBarItem>
            <Separator/>
            <StatusBarItem>
                <TextBlock>
                    <Run Text="Śr. cena: "/>
                    <Run Text="{Binding SredniaCena, StringFormat=C}"/>
                </TextBlock>
            </StatusBarItem>
        </StatusBar>
    </Grid>
</Window>
```

---

### 7. Code-behind — minimum kodu

csharp

```csharp
// ProduktyView.xaml.cs — tylko "plumbing", zero logiki biznesowej!
public partial class ProduktyView : Window
{
    private readonly ProduktyListaViewModel _vm;

    public ProduktyView(ProduktyListaViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        // Ustaw DataContext — wszystkie binding zaczną działać
        DataContext = _vm;

        // Subskrybuj zdarzenia ViewModel → akcje UI
        _vm.OtworzEdycjeZadane    += OtworzEdycje;
        _vm.PokazKomunikatZadane  += PokazKomunikat;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Załaduj dane przy starcie
        await (_vm.ZaladujKomenda as AsyncRelayCommand)!.WykonajZewnAsync();
    }

    private void OtworzEdycje(ProduktViewModel produkt)
    {
        // ViewModel mówi "otwórz edycję" — View decyduje JAK
        var dialog = new ProduktEdycjaView(
            new ProduktEdycjaViewModel(produkt, _serwis));
        dialog.Owner = this;

        if (dialog.ShowDialog() == true)
            _ = _vm.ZaladujKomenda.ExecuteAsync();
    }

    private void PokazKomunikat(string tekst, bool sukces)
    {
        System.Windows.MessageBox.Show(
            tekst,
            sukces ? "Sukces" : "Błąd",
            System.Windows.MessageBoxButton.OK,
            sukces ? System.Windows.MessageBoxImage.Information
                   : System.Windows.MessageBoxImage.Error);
    }

    private IProduktSerwis _serwis = null!;  // wstrzykiwane przez DI
}
```

---

### 8. Testowanie ViewModelu

csharp

```csharp
// Testowanie ViewModel jest proste — zero UI!
using Moq;
using Xunit;

public class ProduktyListaViewModelTests
{
    private readonly Mock<IProduktSerwis> _serwis = new();

    private static List<Produkt> PrzykladoweProdukt() => new()
    {
        new() { Id = 1, Nazwa = "Laptop",   Cena = 3500m, StanMagazynu = 10 },
        new() { Id = 2, Nazwa = "Mysz",      Cena =  150m, StanMagazynu = 50 },
        new() { Id = 3, Nazwa = "Klawiatura",Cena =  250m, StanMagazynu = 0  }
    };

    [Fact]
    public async Task ZaladujKomenda_WywolujeSerwisDodajeDoKolekcji()
    {
        // Arrange
        _serwis.Setup(s => s.PobierzWszystkieAsync(default))
            .ReturnsAsync(PrzykladoweProdukt());

        var vm = new ProduktyListaViewModel(_serwis.Object);

        // Act
        vm.ZaladujKomenda.Execute(null);
        await Task.Delay(100);  // poczekaj na async

        // Assert
        Assert.Equal(3, vm.Produkty.Count);
        Assert.Equal("Laptop", vm.Produkty[0].Nazwa);
        Assert.Contains("3", vm.Komunikat);
    }

    [Fact]
    public async Task UsunKomenda_UsuwaWybranyProdukt()
    {
        // Arrange
        _serwis.Setup(s => s.PobierzWszystkieAsync(default))
            .ReturnsAsync(PrzykladoweProdukt());
        _serwis.Setup(s => s.UsunAsync(1, default))
            .ReturnsAsync(true);

        var vm = new ProduktyListaViewModel(_serwis.Object);
        vm.ZaladujKomenda.Execute(null);
        await Task.Delay(100);

        // Act
        vm.WybranyProdukt = vm.Produkty.First();
        vm.UsunKomenda.Execute(null);
        await Task.Delay(100);

        // Assert
        Assert.Equal(2, vm.Produkty.Count);
        Assert.Null(vm.WybranyProdukt);
        _serwis.Verify(s => s.UsunAsync(1, default), Times.Once);
    }

    [Fact]
    public void WybranyProdukt_Zmiana_AktualizujeCanExecute()
    {
        // Arrange
        _serwis.Setup(s => s.PobierzWszystkieAsync(default))
            .ReturnsAsync(PrzykladoweProdukt());

        var vm = new ProduktyListaViewModel(_serwis.Object);

        // Komendy niedostępne bez wyboru
        Assert.False(vm.EdytujKomenda.CanExecute(null));
        Assert.False(vm.UsunKomenda.CanExecute(null));

        // Act — wybierz produkt
        vm.WybranyProdukt = new ProduktViewModel(
            new Produkt { Id = 1, Nazwa = "Test" });

        // Assert — komendy dostępne
        Assert.True(vm.EdytujKomenda.CanExecute(null));
        Assert.True(vm.UsunKomenda.CanExecute(null));
    }

    [Fact]
    public void ProduktViewModel_WalidacjaBledy()
    {
        var vm = new ProduktViewModel();

        // Pusta nazwa — błąd
        Assert.NotEmpty(vm["Nazwa"]);

        // Ustaw nazwę
        vm.Nazwa = "A";  // za krótka
        Assert.NotEmpty(vm["Nazwa"]);

        vm.Nazwa = "Laptop";  // OK
        Assert.Empty(vm["Nazwa"]);

        // Cena ujemna
        vm.Cena = -100m;
        Assert.NotEmpty(vm["Cena"]);

        vm.Cena = 3500m;
        Assert.Empty(vm["Cena"]);
    }

    [Fact]
    public void ProduktViewModel_ZmianWlasciwosci_PowiadamiaINPC()
    {
        var vm      = new ProduktViewModel();
        var zdarzenia = new List<string>();

        vm.PropertyChanged += (_, e) =>
            zdarzenia.Add(e.PropertyName!);

        vm.Nazwa = "Test";
        vm.Cena  = 100m;
        vm.Stan  = 5;

        Assert.Contains(nameof(ProduktViewModel.Nazwa),   zdarzenia);
        Assert.Contains(nameof(ProduktViewModel.Cena),    zdarzenia);
        Assert.Contains(nameof(ProduktViewModel.Stan),    zdarzenia);
        Assert.Contains(nameof(ProduktViewModel.StatusMagazynu), zdarzenia);
    }
}
```

---

### Typowe pytania rekrutacyjne

**"Co to INotifyPropertyChanged i dlaczego jest kluczowy dla MVVM?"** `INotifyPropertyChanged` to interfejs z jednym zdarzeniem `PropertyChanged`. Gdy właściwość ViewModel się zmienia, wywołujesz `PropertyChanged` z nazwą właściwości. Data binding w WPF/MAUI/Blazor subskrybuje to zdarzenie — automatycznie odświeża UI. Bez INPC UI nigdy nie wie o zmianach w ViewModel. `[CallerMemberName]` eliminuje magic strings — kompilator wstawia nazwę właściwości automatycznie.

**"Jaka różnica między ObservableCollection a List?"** `List<T>` nie powiadamia o zmianach — po `Add`/`Remove` UI nie wie że kolekcja się zmieniła. `ObservableCollection<T>` implementuje `INotifyCollectionChanged` — każde `Add`, `Remove`, `Clear` emituje zdarzenie `CollectionChanged`. Bindowana `DataGrid`/`ListView` automatycznie odświeża wyświetlane wiersze. Używaj `ObservableCollection` dla kolekcji wyświetlanych w UI, `List` dla danych wewnętrznych bez bindowania.

**"Jak ViewModel komunikuje się z View bez referencji?"** Trzy podejścia: (1) Zdarzenia — ViewModel wystawia `event Action<string> OtworzDialogZadane`, View subskrybuje i obsługuje (jak w przykładzie). (2) Messenger/EventAggregator — luźne powiązanie przez centralny hub wiadomości (CommunityToolkit.Mvvm `WeakReferenceMessenger`). (3) Dialogowy serwis — `IDialogSerwis` wstrzykiwany do ViewModel, View implementuje i rejestruje przez DI. Najczystsza opcja: serwisy przez DI — ViewModel testuje przez mock.

**"Kiedy MVVM jest przesadą?"** MVVM ma sens dla: aplikacji desktop (WPF, MAUI, UWP) z dwukierunkowym bindowaniem, złożonych formularzy z walidacją, aplikacji z dużą logiką UI (filtrowanie, sortowanie, undo/redo). Przesada dla: prostych stron CRUD bez logiki UI, małych aplikacji gdzie warstwa ViewModel kopiuje tylko dane z serwisu. W ASP.NET Core MVC MVVM nie ma zastosowania — tam działa wzorzec MVC z ViewModelami jako DTO dla widoków (bez bindowania dwukierunkowego).