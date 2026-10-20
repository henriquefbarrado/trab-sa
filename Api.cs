using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SkiaSharp;

namespace PixelVault;

// Modelos (espelham as tabelas do banco SQLite)
public record Jogo(int id, string titulo, string plataforma, string genero, double preco, int estoque);
public record Cliente(int id, string nome, string? email, string? telefone);
public record ItemVenda(int qtd, double preco_unit, string titulo);
public record Venda(int id, double total, string data, string cliente, List<ItemVenda> itens);
public record Resumo(double faturamento, int vendas, int estoque, int clientes);

// Erro de regra de negócio (mostrado ao usuário numa caixa de mensagem)
public class ApiException : Exception
{
    public ApiException(string msg) : base(msg) { }
}

// Acesso ao banco SQLite local (arquivo pixelvault.db). Não precisa de servidor nem de Node.
public static class Api
{
    // O banco fica em %LocalAppData%\PixelVault\pixelvault.db (não se perde ao limpar o projeto)
    static readonly string Pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelVault");
    static readonly string ConnStr = $"Data Source={Path.Combine(Pasta, "pixelvault.db")}";
    static readonly Dictionary<string, Image?> Capas = new();

    static SqliteConnection Abrir()
    {
        var c = new SqliteConnection(ConnStr);
        c.Open();
        return c;
    }

    static SqliteCommand Cmd(SqliteConnection c, string sql, params (string, object?)[] ps)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd;
    }

    static T Escalar<T>(SqliteConnection c, string sql, params (string, object?)[] ps)
    {
        var r = Cmd(c, sql, ps).ExecuteScalar();
        return r is null or DBNull ? default! : (T)Convert.ChangeType(r, typeof(T), CultureInfo.InvariantCulture);
    }

    // Cria o banco e as tabelas na primeira execução e insere dados de exemplo
    public static void Iniciar()
    {
        Directory.CreateDirectory(Pasta);
        using var c = Abrir();
        Cmd(c, @"
            CREATE TABLE IF NOT EXISTS jogos (
                id INTEGER PRIMARY KEY AUTOINCREMENT, titulo TEXT NOT NULL, plataforma TEXT NOT NULL,
                genero TEXT NOT NULL DEFAULT '', preco REAL NOT NULL CHECK (preco >= 0), estoque INTEGER NOT NULL DEFAULT 0 CHECK (estoque >= 0));
            CREATE TABLE IF NOT EXISTS clientes (
                id INTEGER PRIMARY KEY AUTOINCREMENT, nome TEXT NOT NULL, email TEXT, telefone TEXT);
            CREATE TABLE IF NOT EXISTS vendas (
                id INTEGER PRIMARY KEY AUTOINCREMENT, cliente TEXT NOT NULL, total REAL NOT NULL, data TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS itens_venda (
                id INTEGER PRIMARY KEY AUTOINCREMENT, venda_id INTEGER NOT NULL, titulo TEXT NOT NULL, qtd INTEGER NOT NULL, preco_unit REAL NOT NULL);
        ").ExecuteNonQuery();

        if (Escalar<long>(c, "SELECT COUNT(*) FROM jogos") == 0)
        {
            Cmd(c, "BEGIN").ExecuteNonQuery();
            (string, string, string, double, int)[] jogos =
            {
                ("The Last of Us Part II", "PS5", "Ação", 199.90, 8),
                ("God of War Ragnarök", "PS5", "Aventura", 249.90, 5),
                ("Marvel's Spider-Man 2", "PS5", "Ação", 279.90, 6),
                ("Forza Horizon 5", "Xbox", "Corrida", 179.90, 7),
                ("Zelda: Tears of the Kingdom", "Switch", "Aventura", 349.90, 10),
                ("Mario Kart 8 Deluxe", "Switch", "Corrida", 289.90, 0),
                ("Cyberpunk 2077", "PC", "RPG", 129.90, 12),
                ("Elden Ring", "PC", "RPG", 229.90, 9),
                ("Red Dead Redemption 2", "PS4", "Aventura", 119.90, 6),
            };
            foreach (var (t, p, g, pr, e) in jogos)
                Cmd(c, "INSERT INTO jogos (titulo, plataforma, genero, preco, estoque) VALUES ($t,$p,$g,$pr,$e)",
                    ("$t", t), ("$p", p), ("$g", g), ("$pr", pr), ("$e", e)).ExecuteNonQuery();
            (string, string, string)[] clientes =
            {
                ("Ana Souza", "ana@email.com", "(31) 99999-1111"),
                ("Bruno Lima", "bruno@email.com", "(31) 98888-2222"),
                ("Carla Mendes", "carla@email.com", "(31) 97777-3333"),
            };
            foreach (var (n, e, t) in clientes)
                Cmd(c, "INSERT INTO clientes (nome, email, telefone) VALUES ($n,$e,$t)", ("$n", n), ("$e", e), ("$t", t)).ExecuteNonQuery();
            Cmd(c, "COMMIT").ExecuteNonQuery();
        }

        LimparSemCapa(c);
    }

    static readonly string[] ExtCapas = { "jpg", "jpeg", "png", "webp" };
    static string[] PastasCapas() => new[] { Path.Combine(AppContext.BaseDirectory, "capas"), Path.Combine(Pasta, "capas") };

    static bool TemCapa(string titulo)
    {
        var s = Slug(titulo);
        return PastasCapas().Any(d => ExtCapas.Any(e => File.Exists(Path.Combine(d, s + "." + e))));
    }

    // Limpeza feita UMA vez: remove do banco os jogos que não têm capa na pasta "capas".
    // (Se a pasta de capas não for encontrada, não apaga nada.)
    static void LimparSemCapa(SqliteConnection c)
    {
        if (Escalar<long>(c, "PRAGMA user_version") >= 1) return;
        bool achouCapas = PastasCapas().Any(d => Directory.Exists(d) && Directory.EnumerateFiles(d).Any(f => ExtCapas.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant())));
        if (!achouCapas) return;

        // "Spider-Man 2" tem capa (marvel-s-spider-man-2.jpg): só o nome estava diferente
        Cmd(c, "UPDATE jogos SET titulo = $n WHERE titulo = $o", ("$n", "Marvel's Spider-Man 2"), ("$o", "Spider-Man 2")).ExecuteNonQuery();

        var lista = new List<(long Id, string Titulo)>();
        using (var r = Cmd(c, "SELECT id, titulo FROM jogos").ExecuteReader())
            while (r.Read()) lista.Add((r.GetInt64(0), r.GetString(1)));
        foreach (var (id, titulo) in lista)
            if (!TemCapa(titulo)) Cmd(c, "DELETE FROM jogos WHERE id = $id", ("$id", id)).ExecuteNonQuery();

        Cmd(c, "PRAGMA user_version = 1").ExecuteNonQuery();
    }

    // ---------- jogos ----------
    public static List<Jogo> Jogos(string q = "", string plataforma = "")
    {
        using var c = Abrir();
        using var r = Cmd(c, @"SELECT id, titulo, plataforma, genero, preco, estoque FROM jogos
            WHERE ($q = '' OR titulo LIKE '%' || $q || '%' OR genero LIKE '%' || $q || '%')
              AND ($p = '' OR plataforma = $p)
            ORDER BY titulo COLLATE NOCASE", ("$q", q.Trim()), ("$p", plataforma)).ExecuteReader();
        var l = new List<Jogo>();
        while (r.Read()) l.Add(new Jogo(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetDouble(4), r.GetInt32(5)));
        return l;
    }

    public static void NovoJogo(string titulo, string plataforma, string genero, double preco, int estoque)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ApiException("Informe o título do jogo.");
        if (preco < 0) throw new ApiException("O preço não pode ser negativo.");
        if (estoque < 0) throw new ApiException("O estoque não pode ser negativo.");
        using var c = Abrir();
        Cmd(c, "INSERT INTO jogos (titulo, plataforma, genero, preco, estoque) VALUES ($t,$p,$g,$pr,$e)",
            ("$t", titulo.Trim()), ("$p", plataforma), ("$g", genero.Trim()), ("$pr", preco), ("$e", estoque)).ExecuteNonQuery();
    }

    public static void AjustaEstoque(int id, int delta)
    {
        using var c = Abrir();
        int atual = Escalar<int>(c, "SELECT estoque FROM jogos WHERE id=$id", ("$id", id));
        if (atual + delta < 0) throw new ApiException("O estoque não pode ficar negativo.");
        Cmd(c, "UPDATE jogos SET estoque = estoque + $d WHERE id=$id", ("$d", delta), ("$id", id)).ExecuteNonQuery();
    }

    public static void ExcluiJogo(int id)
    {
        using var c = Abrir();
        Cmd(c, "DELETE FROM jogos WHERE id=$id", ("$id", id)).ExecuteNonQuery();
    }

    // ---------- clientes ----------
    public static List<Cliente> Clientes()
    {
        using var c = Abrir();
        using var r = Cmd(c, "SELECT id, nome, email, telefone FROM clientes ORDER BY nome COLLATE NOCASE").ExecuteReader();
        var l = new List<Cliente>();
        while (r.Read()) l.Add(new Cliente(r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3)));
        return l;
    }

    public static void NovoCliente(string nome, string email, string telefone)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ApiException("Informe o nome do cliente.");
        using var c = Abrir();
        Cmd(c, "INSERT INTO clientes (nome, email, telefone) VALUES ($n,$e,$t)",
            ("$n", nome.Trim()), ("$e", email.Trim()), ("$t", telefone.Trim())).ExecuteNonQuery();
    }

    public static void ExcluiCliente(int id)
    {
        using var c = Abrir();
        Cmd(c, "DELETE FROM clientes WHERE id=$id", ("$id", id)).ExecuteNonQuery();
    }

    // ---------- vendas ----------
    // Regra de negócio: não vende com estoque zero nem acima do estoque disponível.
    // Tudo dentro de uma transação: ou grava a venda inteira ou nada.
    public static double Vender(int clienteId, Dictionary<int, int> carrinho)
    {
        if (carrinho.Count == 0) throw new ApiException("O carrinho está vazio.");
        using var c = Abrir();
        Cmd(c, "BEGIN").ExecuteNonQuery();
        try { return VenderTx(c, clienteId, carrinho); }
        catch { Cmd(c, "ROLLBACK").ExecuteNonQuery(); throw; }
    }

    static double VenderTx(SqliteConnection c, int clienteId, Dictionary<int, int> carrinho)
    {
        string cliente = Escalar<string>(c, "SELECT nome FROM clientes WHERE id=$id", ("$id", clienteId));
        if (cliente == null) throw new ApiException("Cliente não encontrado.");

        double total = 0;
        var itens = new List<(string titulo, int qtd, double preco)>();
        foreach (var (jogoId, qtd) in carrinho)
        {
            using var r = Cmd(c, "SELECT titulo, preco, estoque FROM jogos WHERE id=$id", ("$id", jogoId)).ExecuteReader();
            if (!r.Read()) throw new ApiException("Um dos jogos do carrinho não existe mais.");
            string titulo = r.GetString(0); double preco = r.GetDouble(1); int estoque = r.GetInt32(2);
            r.Close();
            if (estoque <= 0) throw new ApiException($"Estoque zerado: {titulo} não pode ser vendido.");
            if (qtd > estoque) throw new ApiException($"Quantidade maior que o estoque de {titulo} (disponível: {estoque}).");
            total += preco * qtd;
            itens.Add((titulo, qtd, preco));
            Cmd(c, "UPDATE jogos SET estoque = estoque - $q WHERE id=$id", ("$q", qtd), ("$id", jogoId)).ExecuteNonQuery();
        }

        Cmd(c, "INSERT INTO vendas (cliente, total, data) VALUES ($c,$t,$d)",
            ("$c", cliente), ("$t", total), ("$d", DateTime.Now.ToString("s"))).ExecuteNonQuery();
        long vendaId = Escalar<long>(c, "SELECT last_insert_rowid()");
        foreach (var (titulo, qtd, preco) in itens)
            Cmd(c, "INSERT INTO itens_venda (venda_id, titulo, qtd, preco_unit) VALUES ($v,$t,$q,$p)",
                ("$v", vendaId), ("$t", titulo), ("$q", qtd), ("$p", preco)).ExecuteNonQuery();
        Cmd(c, "COMMIT").ExecuteNonQuery();
        return total;
    }

    public static List<Venda> Vendas()
    {
        using var c = Abrir();
        var itens = new Dictionary<int, List<ItemVenda>>();
        using (var r = Cmd(c, "SELECT venda_id, qtd, preco_unit, titulo FROM itens_venda ORDER BY id").ExecuteReader())
            while (r.Read())
            {
                int vid = r.GetInt32(0);
                if (!itens.TryGetValue(vid, out var li)) itens[vid] = li = new();
                li.Add(new ItemVenda(r.GetInt32(1), r.GetDouble(2), r.GetString(3)));
            }
        var l = new List<Venda>();
        using var rv = Cmd(c, "SELECT id, total, data, cliente FROM vendas ORDER BY id DESC").ExecuteReader();
        while (rv.Read())
        {
            int id = rv.GetInt32(0);
            var data = DateTime.TryParse(rv.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd/MM/yyyy HH:mm") : rv.GetString(2);
            l.Add(new Venda(id, rv.GetDouble(1), data, rv.GetString(3), itens.GetValueOrDefault(id) ?? new()));
        }
        return l;
    }

    public static Resumo Resumo()
    {
        using var c = Abrir();
        return new Resumo(
            Escalar<double>(c, "SELECT COALESCE(SUM(total),0) FROM vendas"),
            Escalar<int>(c, "SELECT COUNT(*) FROM vendas"),
            Escalar<int>(c, "SELECT COALESCE(SUM(estoque),0) FROM jogos"),
            Escalar<int>(c, "SELECT COUNT(*) FROM clientes"));
    }

    // ---------- capas ----------
    public static string Slug(string t)
    {
        var n = new string(t.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        return Regex.Replace(n, "[^a-z0-9]+", "-").Trim('-');
    }

    // Lê a capa da pasta "capas" (ao lado do programa). Ex.: capas\elden-ring.jpg
    // Aceita jpg, png e webp. Sem imagem, o card mostra o título do jogo.
    public static Image? Capa(string titulo)
    {
        var s = Slug(titulo);
        if (Capas.TryGetValue(s, out var cache)) return cache;
        Image? img = null;
        foreach (var dir in new[] { Path.Combine(AppContext.BaseDirectory, "capas"), Path.Combine(Pasta, "capas") })
            foreach (var ext in new[] { "jpg", "jpeg", "png", "webp" })
            {
                var f = Path.Combine(dir, $"{s}.{ext}");
                if (!File.Exists(f)) continue;
                try { img = Decodificar(File.ReadAllBytes(f)); } catch { }
                if (img != null) break;
            }
        Capas[s] = img;
        return img;
    }

    static Image? Decodificar(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            using var tmp = Image.FromStream(ms);
            return new Bitmap(tmp);
        }
        catch { }
        try // webp (ou extensão trocada): o SkiaSharp lê pelo conteúdo
        {
            using var sk = SKBitmap.Decode(bytes);
            if (sk == null) return null;
            using var im = SKImage.FromBitmap(sk);
            using var data = im.Encode(SKEncodedImageFormat.Png, 90);
            using var ms2 = new MemoryStream(data.ToArray());
            using var tmp2 = Image.FromStream(ms2);
            return new Bitmap(tmp2);
        }
        catch { return null; }
    }
}
