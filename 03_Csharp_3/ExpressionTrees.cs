using System.Linq.Expressions;

namespace _03_Csharp_3;

// ── Klasy pomocnicze ──────────────────────────────────────────────────────────

public class OsobaET
{
    public string Imie { get; set; } = "";
    public int Wiek { get; set; }
    public string Miasto { get; set; } = "";
    public decimal Pensja { get; set; }
}

public static class ExpressionTrees
{
    static readonly List<OsobaET> Osoby = new()
    {
        new() { Imie="Anna",   Wiek=28, Miasto="Warszawa", Pensja=8500  },
        new() { Imie="Jan",    Wiek=35, Miasto="Kraków",   Pensja=6000  },
        new() { Imie="Marek",  Wiek=42, Miasto="Warszawa", Pensja=9200  },
        new() { Imie="Kasia",  Wiek=27, Miasto="Gdańsk",   Pensja=5800  },
        new() { Imie="Piotr",  Wiek=38, Miasto="Kraków",   Pensja=11000 },
        new() { Imie="Zofia",  Wiek=33, Miasto="Warszawa", Pensja=7500  },
    };

    // ── 1. Func vs Expression<Func> ───────────────────────────────────────────

    public static void FuncVsExpression()
    {
        Console.WriteLine("\n── FuncVsExpression ──");

        // Func<T,bool> — skompilowany kod (delegate), czarna skrzynka
        Func<OsobaET, bool> funcPredicate = o => o.Wiek > 30;

        // Expression<Func<T,bool>> — DRZEWO WYRAŻEŃ: dane opisujące kod, nie sam kod
        // Kompilator buduje obiektowy model AST (Abstract Syntax Tree)
        Expression<Func<OsobaET, bool>> exprPredicate = o => o.Wiek > 30;

        // Func: wywołaj bezpośrednio
        bool wynik = funcPredicate(Osoby[0]);
        Console.WriteLine($"Func wywołanie: {Osoby[0].Imie} wiek>30: {wynik}");

        // Expression: można analizować strukturę
        Console.WriteLine($"Expression.Body: {exprPredicate.Body}");
        Console.WriteLine($"Expression.Body.NodeType: {exprPredicate.Body.NodeType}");

        var binExpr = (BinaryExpression)exprPredicate.Body;
        Console.WriteLine($"Lewy: {binExpr.Left} (NodeType={binExpr.Left.NodeType})");
        Console.WriteLine($"Prawy: {binExpr.Right}");

        // Kompilacja Expression do Func — teraz można wywołać
        Func<OsobaET, bool> skompilowany = exprPredicate.Compile();
        Console.WriteLine($"Skompilowane i wywołane: {skompilowany(Osoby[0])}");

        // GDZIE to ma znaczenie:
        // IQueryable.Where(Expression<Func<T,bool>>) — provider tłumaczy na SQL
        // IEnumerable.Where(Func<T,bool>)            — C# delegate, wykon. in-process
        Console.WriteLine("\nFunc:       delegate → wykonanie w C#");
        Console.WriteLine("Expression: drzewo wyrażeń → provider może tłumaczyć (SQL, etc.)");
    }

    // ── 2. Anatomia drzewa wyrażeń ────────────────────────────────────────────

    public static void AnatomiaDrzewa()
    {
        Console.WriteLine("\n── AnatomiaDrzewa ──");

        // Przykładowe wyrażenie: o => o.Imie == "Anna" && o.Wiek > 25
        Expression<Func<OsobaET, bool>> expr = o => o.Imie == "Anna" && o.Wiek > 25;

        Console.WriteLine($"Pełne wyrażenie: {expr}");
        Console.WriteLine($"Parametry: {string.Join(", ", expr.Parameters.Select(p => $"{p.Name}:{p.Type.Name}"))}");
        Console.WriteLine($"Body.NodeType: {expr.Body.NodeType}"); // AndAlso

        var andExpr = (BinaryExpression)expr.Body;
        Console.WriteLine($"\nLewa (imie=Anna):");
        Console.WriteLine($"  NodeType: {andExpr.Left.NodeType}"); // Equal
        var leftBin = (BinaryExpression)andExpr.Left;
        Console.WriteLine($"  Left: {leftBin.Left} (MemberAccess)");
        Console.WriteLine($"  Right: {leftBin.Right} (Constant)");

        Console.WriteLine($"\nPrawa (wiek>25):");
        Console.WriteLine($"  NodeType: {andExpr.Right.NodeType}"); // GreaterThan
        var rightBin = (BinaryExpression)andExpr.Right;
        Console.WriteLine($"  Left: {rightBin.Left}");
        Console.WriteLine($"  Right: {rightBin.Right}");

        // Węzły Expression
        Console.WriteLine("\nGłówne węzły Expression:");
        Console.WriteLine("  ParameterExpression — parametr lambdy (o)");
        Console.WriteLine("  MemberExpression    — dostęp do właściwości/pola (o.Imie)");
        Console.WriteLine("  ConstantExpression  — stała ('Anna', 25)");
        Console.WriteLine("  BinaryExpression    — operator binarny (==, >, &&, +)");
        Console.WriteLine("  UnaryExpression     — operator unarny (!, -)");
        Console.WriteLine("  MethodCallExpression — wywołanie metody (.Contains(), .ToString())");
        Console.WriteLine("  LambdaExpression    — cała lambda (o => ...)");
    }

    // ── 3. Ręczne budowanie drzewa wyrażeń ───────────────────────────────────

    public static void ReczneBudowanie()
    {
        Console.WriteLine("\n── ReczneBudowanie ──");

        // Ręczne budowanie odpowiednika: o => o.Wiek > 30
        ParameterExpression param = Expression.Parameter(typeof(OsobaET), "o");
        MemberExpression wiekszosc = Expression.Property(param, nameof(OsobaET.Wiek));
        ConstantExpression stala = Expression.Constant(30);
        BinaryExpression cialo = Expression.GreaterThan(wiekszosc, stala);
        Expression<Func<OsobaET, bool>> lambdaExpr =
            Expression.Lambda<Func<OsobaET, bool>>(cialo, param);

        Console.WriteLine($"Ręcznie zbudowane: {lambdaExpr}");
        var skompilowane = lambdaExpr.Compile();
        var wynikList = Osoby.Where(skompilowane).Select(o => o.Imie).ToList();
        Console.WriteLine($"Osoby wiek>30: {string.Join(", ", wynikList)}");

        // Ręczne budowanie: o => o.Miasto == "Warszawa"
        var paramMiasto = Expression.Parameter(typeof(OsobaET), "o");
        var propertyMiasto = Expression.Property(paramMiasto, nameof(OsobaET.Miasto));
        var stalaMiasto = Expression.Constant("Warszawa");
        var rowne = Expression.Equal(propertyMiasto, stalaMiasto);
        var lambdaMiasto = Expression.Lambda<Func<OsobaET, bool>>(rowne, paramMiasto);

        Console.WriteLine($"Ręcznie Miasto==Warszawa: {lambdaMiasto}");
        var warszawa = Osoby.Where(lambdaMiasto.Compile()).Select(o => o.Imie);
        Console.WriteLine($"Wynik: {string.Join(", ", warszawa)}");

        // Kombinowanie wyrażeń AND
        var combined = Expression.Lambda<Func<OsobaET, bool>>(
            Expression.AndAlso(cialo, rowne),
            param); // UWAGA: oba wyrażenia muszą używać tego SAMEGO ParameterExpression!
        // Powyżej param z pierwszego wyrażenia, ale rowne używa paramMiasto — inny obiekt!
        // Poprawka: podmień parametr w rowne za pomocą ExpressionVisitor

        Console.WriteLine("\nKombinowanie wyrażeń wymaga ExpressionVisitor do podmiany ParameterExpression");
    }

    // ── 4. ExpressionVisitor ──────────────────────────────────────────────────

    public static void ExpressionVisitorDemo()
    {
        Console.WriteLine("\n── ExpressionVisitorDemo ──");

        // ExpressionVisitor — wzorzec Visitor dla drzewa wyrażeń
        // Umożliwia: transformację, analizę, podmianę węzłów

        Expression<Func<OsobaET, bool>> exprA = o => o.Wiek > 30;
        Expression<Func<OsobaET, bool>> exprB = o => o.Miasto == "Warszawa";

        // Łączenie AND dwóch Expression<Func<T,bool>>
        // Trzeba podmienić parametr z exprB na ten z exprA
        var combined = CombineAnd(exprA, exprB);
        Console.WriteLine($"Połączone AND: {combined}");

        var wynikAnd = Osoby.AsQueryable().Where(combined).Select(o => o.Imie).ToList();
        Console.WriteLine($"Warszawa i wiek>30: {string.Join(", ", wynikAnd)}");

        // Łączenie OR
        var combinedOr = CombineOr(exprA, exprB);
        var wynikOr = Osoby.AsQueryable().Where(combinedOr).Select(o => o.Imie).ToList();
        Console.WriteLine($"Wiek>30 LUB Warszawa: {string.Join(", ", wynikOr)}");

        // Analiza wyrażenia — wyciąganie nazw właściwości
        Expression<Func<OsobaET, int>> wiekExpr = o => o.Wiek;
        string nazwaWlasciw = WyciagnijNazweWlasciwosci(wiekExpr);
        Console.WriteLine($"\nNazwa właściwości z Expression: '{nazwaWlasciw}'");
    }

    static Expression<Func<T, bool>> CombineAnd<T>(
        Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var visitor = new ParameterReplacerET(right.Parameters[0], left.Parameters[0]);
        var rightBody = visitor.Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(left.Body, rightBody), left.Parameters[0]);
    }

    static Expression<Func<T, bool>> CombineOr<T>(
        Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var visitor = new ParameterReplacerET(right.Parameters[0], left.Parameters[0]);
        var rightBody = visitor.Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(left.Body, rightBody), left.Parameters[0]);
    }

    static string WyciagnijNazweWlasciwosci<T, TProp>(Expression<Func<T, TProp>> expr) =>
        ((MemberExpression)expr.Body).Member.Name;

    // ── 5. Dynamiczny filtr ───────────────────────────────────────────────────

    public static void DynamicznyFiltr()
    {
        Console.WriteLine("\n── DynamicznyFiltr ──");

        // Budowanie filtra w runtime na podstawie wejścia użytkownika
        // Wejście: nazwa właściwości + operator + wartość
        var filtry = new[]
        {
            ("Wiek", ">", (object)30),
            ("Miasto", "==", (object)"Warszawa"),
        };

        IQueryable<OsobaET> query = Osoby.AsQueryable();
        foreach (var (prop, op, value) in filtry)
            query = ZastosujFiltr(query, prop, op, value);

        Console.WriteLine("Dynamiczny filtr (Wiek>30 AND Miasto==Warszawa):");
        foreach (var o in query)
            Console.WriteLine($"  {o.Imie}, {o.Wiek}, {o.Miasto}");

        // Generowanie sortera po nazwie właściwości (string → Expression)
        var posortowane = ZastosujSortowanie(Osoby.AsQueryable(), "Pensja", descending: true);
        Console.WriteLine("\nDynamiczne sortowanie po Pensja malejąco:");
        foreach (var o in posortowane)
            Console.WriteLine($"  {o.Imie}: {o.Pensja:C}");
    }

    static IQueryable<OsobaET> ZastosujFiltr(IQueryable<OsobaET> query, string prop, string op, object value)
    {
        var param = Expression.Parameter(typeof(OsobaET), "o");
        var member = Expression.Property(param, prop);
        // Konwersja wartości do typu właściwości (np. int dla Wiek)
        var stala = Expression.Constant(Convert.ChangeType(value, member.Type));
        Expression cialo = op switch
        {
            "==" => Expression.Equal(member, stala),
            "!=" => Expression.NotEqual(member, stala),
            ">"  => Expression.GreaterThan(member, stala),
            ">=" => Expression.GreaterThanOrEqual(member, stala),
            "<"  => Expression.LessThan(member, stala),
            "<=" => Expression.LessThanOrEqual(member, stala),
            _    => throw new ArgumentException($"Nieznany operator: {op}")
        };
        var lambda = Expression.Lambda<Func<OsobaET, bool>>(cialo, param);
        return query.Where(lambda);
    }

    static IQueryable<OsobaET> ZastosujSortowanie(IQueryable<OsobaET> query, string prop, bool descending)
    {
        var param = Expression.Parameter(typeof(OsobaET), "o");
        var member = Expression.Property(param, prop);
        var keySelector = Expression.Lambda(member, param);
        var method = descending ? "OrderByDescending" : "OrderBy";
        var call = Expression.Call(
            typeof(Queryable),
            method,
            new[] { typeof(OsobaET), member.Type },
            query.Expression,
            Expression.Quote(keySelector));
        return query.Provider.CreateQuery<OsobaET>(call);
    }

    // ── 6. Mini-translator SQL ────────────────────────────────────────────────

    public static void MiniTranslatorSQL()
    {
        Console.WriteLine("\n── MiniTranslatorSQL ──");

        // Demonstracja idei: Expression → SQL string
        // To co robi EF Core (znacznie bardziej rozbudowane)
        Expression<Func<OsobaET, bool>> expr1 = o => o.Wiek > 30;
        Expression<Func<OsobaET, bool>> expr2 = o => o.Miasto == "Warszawa";
        Expression<Func<OsobaET, bool>> expr3 = o => o.Imie.StartsWith("A");

        Console.WriteLine($"Expr → SQL: '{TlumaczNaSQL(expr1)}'");
        Console.WriteLine($"Expr → SQL: '{TlumaczNaSQL(expr2)}'");
        Console.WriteLine($"Expr → SQL: '{TlumaczNaSQL(expr3)}'");

        Console.WriteLine("\nEF Core robi to samo (dużo bardziej kompletnie):");
        Console.WriteLine("  dbContext.Osoby.Where(o => o.Wiek > 30)");
        Console.WriteLine("  → SELECT * FROM Osoby WHERE Wiek > 30");
    }

    static string TlumaczNaSQL<T>(Expression<Func<T, bool>> expr)
    {
        return TlumaczWyrażenie(expr.Body);
    }

    static string TlumaczWyrażenie(Expression expr) => expr switch
    {
        BinaryExpression bin => $"{TlumaczWyrażenie(bin.Left)} {TlumaczOperator(bin.NodeType)} {TlumaczWyrażenie(bin.Right)}",
        MemberExpression mem => mem.Member.Name,
        ConstantExpression con => con.Value is string s ? $"'{s}'" : con.Value?.ToString() ?? "NULL",
        MethodCallExpression mc when mc.Method.Name == "StartsWith" =>
            $"{TlumaczWyrażenie(mc.Object!)} LIKE '{((ConstantExpression)mc.Arguments[0]).Value}%'",
        UnaryExpression u when u.NodeType == ExpressionType.Convert => TlumaczWyrażenie(u.Operand),
        _ => expr.ToString()
    };

    static string TlumaczOperator(ExpressionType t) => t switch
    {
        ExpressionType.Equal              => "=",
        ExpressionType.NotEqual           => "<>",
        ExpressionType.GreaterThan        => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThan           => "<",
        ExpressionType.LessThanOrEqual    => "<=",
        ExpressionType.AndAlso            => "AND",
        ExpressionType.OrElse             => "OR",
        _                                 => t.ToString()
    };
}

// ── ExpressionVisitor — podmiana ParameterExpression ─────────────────────────

public class ParameterReplacerET(ParameterExpression toReplace, ParameterExpression replacement)
    : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node)
        => node == toReplace ? replacement : base.VisitParameter(node);
}
