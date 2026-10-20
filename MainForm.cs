using System.Globalization;

namespace PixelVault;

public class MainForm : Form
{
    static readonly CultureInfo PtBR = new("pt-BR");
    static readonly Color Bg = Color.FromArgb(10, 10, 10), Pn = Color.FromArgb(20, 20, 20), Ln = Color.FromArgb(40, 40, 40),
        Fg = Color.FromArgb(245, 245, 245), Mut = Color.FromArgb(140, 140, 140), Red = Color.FromArgb(225, 29, 46), Red2 = Color.FromArgb(255, 59, 74);
    static readonly string[] Plats = { "PS5", "Xbox", "Switch", "PC", "PS4" };

    record CartLine(int Id, string Text) { public override string ToString() => Text; }

    List<Jogo> _todos = new(), _lista = new();
    List<Cliente> _clientes = new();
    readonly Dictionary<int, int> _cart = new();   // jogoId -> quantidade
    readonly List<Panel> _views = new();
    readonly List<Button> _nav = new();
    readonly System.Windows.Forms.Timer _deb = new() { Interval = 250 }, _msg = new() { Interval = 3500 };

    Label _status = null!, _lojaSub = null!, _vendasSub = null!, _total = null!;
    FlowLayoutPanel _grid = null!;
    TextBox _busca = null!, _fTitulo = null!, _fGenero = null!, _fPreco = null!, _fEstoque = null!, _cNome = null!, _cEmail = null!, _cTel = null!;
    ComboBox _platFiltro = null!, _fPlat = null!, _cliente = null!;
    ListBox _cartList = null!;
    DataGridView _gJogos = null!, _gClientes = null!, _gVendas = null!;

    public MainForm()
    {
        Text = "PixelVault - Loja de Jogos";
        Size = new Size(1320, 820); MinimumSize = new Size(1040, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg; ForeColor = Fg; Font = new Font("Segoe UI", 9.5f); DoubleBuffered = true;

        var content = new Panel { Dock = DockStyle.Fill };
        _status = new Label { Dock = DockStyle.Bottom, Height = 30, ForeColor = Mut, BackColor = Pn, TextAlign = ContentAlignment.MiddleLeft };
        var top = BuildTop();
        _views.Add(BuildLoja()); _views.Add(BuildJogos()); _views.Add(BuildClientes()); _views.Add(BuildVendas());
        foreach (var v in _views) content.Controls.Add(v);
        Controls.Add(content); Controls.Add(top); Controls.Add(_status);

        _deb.Tick += (_, _) => { _deb.Stop(); Guard(ReloadGrid); };
        _msg.Tick += (_, _) => { _msg.Stop(); _status.Text = ""; };
        Shown += (_, _) => Guard(RefreshAll);
        ShowView(0);
    }

    // ---------- helpers de interface ----------
    static Label Lbl(string t, float size = 10, bool bold = false, Color? c = null) => new()
    {
        Text = t, AutoSize = true, ForeColor = c ?? Fg, BackColor = Color.Transparent,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular)
    };

    static Button Btn(string t, Action a, bool ghost = false)
    {
        var b = new Button
        {
            Text = t, FlatStyle = FlatStyle.Flat, BackColor = ghost ? Pn : Red, ForeColor = Color.White, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Height = 36, AutoSize = true, Padding = new Padding(8, 0, 8, 0)
        };
        b.FlatAppearance.BorderSize = ghost ? 1 : 0;
        b.FlatAppearance.BorderColor = Ln;
        b.FlatAppearance.MouseOverBackColor = ghost ? Color.FromArgb(36, 36, 36) : Red2;
        b.Click += (_, _) => a();
        return b;
    }

    static TextBox Txt(string ph, int w) => new()
    {
        PlaceholderText = ph, Width = w, BackColor = Pn, ForeColor = Fg, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10.5f)
    };

    static ComboBox Combo(string[] items, int w)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Pn, ForeColor = Fg, Width = w, Font = new Font("Segoe UI", 10.5f) };
        c.Items.AddRange(items);
        if (items.Length > 0) c.SelectedIndex = 0;
        return c;
    }

    static DataGridView Grid(params string[] cols)
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            BackgroundColor = Bg, BorderStyle = BorderStyle.None, GridColor = Ln, EnableHeadersVisualStyles = false,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, ColumnHeadersHeight = 40,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        g.RowTemplate.Height = 38;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Pn, ForeColor = Mut, SelectionBackColor = Pn, SelectionForeColor = Mut,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Padding = new Padding(8, 0, 0, 0)
        };
        g.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Bg, ForeColor = Fg, SelectionBackColor = Color.FromArgb(70, 12, 18), SelectionForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5f), Padding = new Padding(8, 0, 0, 0)
        };
        foreach (var c in cols) g.Columns.Add(c, c);
        return g;
    }

    static Panel Header(string title, out Label sub)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 84 };
        p.Controls.Add(Lbl(title, 24, true));
        sub = Lbl("", 10, false, Mut);
        sub.Location = new Point(2, 50);
        p.Controls.Add(sub);
        return p;
    }

    Action Safe(Action f) => () => { try { f(); } catch (Exception ex) { Erro(ex.Message); } };
    void Guard(Action f) { try { f(); } catch (Exception ex) { Erro(ex.Message); } }
    void Erro(string m) => MessageBox.Show(this, m, "PixelVault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    void Aviso(string m) { _status.Text = "   " + m; _msg.Stop(); _msg.Start(); }

    // ---------- barra superior ----------
    Panel BuildTop()
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Bg };
        var logo = new FlowLayoutPanel { AutoSize = true, Location = new Point(24, 16), BackColor = Color.Transparent };
        logo.Controls.Add(Lbl("PIXEL", 16, true)); logo.Controls.Add(Lbl("VAULT", 16, true, Red));
        foreach (Control c in logo.Controls) c.Margin = Padding.Empty;
        var nav = new FlowLayoutPanel { AutoSize = true, Location = new Point(240, 14), BackColor = Color.Transparent };
        string[] names = { "Loja", "Jogos", "Clientes", "Vendas" };
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var b = new Button { Text = names[i], FlatStyle = FlatStyle.Flat, Height = 36, Width = 100, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, _) => ShowView(idx);
            _nav.Add(b); nav.Controls.Add(b);
        }
        p.Controls.Add(logo); p.Controls.Add(nav);
        p.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Ln });
        return p;
    }

    void ShowView(int i)
    {
        for (int k = 0; k < _views.Count; k++)
        {
            bool on = k == i;
            _views[k].Visible = on;
            _nav[k].BackColor = on ? Red : Bg;
            _nav[k].ForeColor = on ? Color.White : Mut;
            _nav[k].FlatAppearance.MouseOverBackColor = on ? Red2 : Color.FromArgb(30, 30, 30);
        }
    }

    // ---------- Loja (catálogo + carrinho) ----------
    Panel BuildLoja()
    {
        var v = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 8, 24, 16) };
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 16, 0) };
        _grid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg };
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56 };
        _busca = Txt("Pesquisar por título ou gênero", 420);
        _busca.TextChanged += (_, _) => { _deb.Stop(); _deb.Start(); };
        _platFiltro = Combo(new[] { "Todas as plataformas" }.Concat(Plats).ToArray(), 220);
        _platFiltro.SelectedIndexChanged += (_, _) => Guard(ReloadGrid);
        tools.Controls.Add(_busca); tools.Controls.Add(_platFiltro);
        Label sub;
        host.Controls.Add(_grid); host.Controls.Add(tools); host.Controls.Add(Header("Catálogo", out sub));
        _lojaSub = sub;

        var cart = new Panel { Dock = DockStyle.Right, Width = 340, BackColor = Pn, Padding = new Padding(16) };
        _cartList = new ListBox
        {
            Dock = DockStyle.Fill, BackColor = Pn, ForeColor = Fg, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10.5f),
            ItemHeight = 30, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed
        };
        _cartList.DrawItem += (s, e) =>
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using var br = new SolidBrush(sel ? Color.FromArgb(70, 12, 18) : Pn);
            e.Graphics.FillRectangle(br, e.Bounds);
            TextRenderer.DrawText(e.Graphics, _cartList.Items[e.Index].ToString(), _cartList.Font, e.Bounds, Fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
        var q = new FlowLayoutPanel { AutoSize = true };
        q.Controls.Add(Btn("−", () => Qty(-1), true)); q.Controls.Add(Btn("+", () => Qty(1), true)); q.Controls.Add(Btn("Remover", RemoveLine, true));
        _total = Lbl("Total: R$ 0,00", 15, true); _total.Margin = new Padding(3, 12, 3, 8);
        var reg = Btn("Registrar venda", Safe(Vender)); reg.AutoSize = false; reg.Size = new Size(300, 44);
        bottom.Controls.Add(q); bottom.Controls.Add(_total); bottom.Controls.Add(reg);
        _cliente = Combo(Array.Empty<string>(), 300); _cliente.DisplayMember = "nome"; _cliente.Dock = DockStyle.Top;
        var lc = Lbl("Cliente", 9, false, Mut); lc.AutoSize = false; lc.Height = 24; lc.Dock = DockStyle.Top;
        var title = Lbl("Carrinho", 16, true); title.AutoSize = false; title.Height = 38; title.Dock = DockStyle.Top;
        cart.Controls.Add(_cartList); cart.Controls.Add(bottom); cart.Controls.Add(_cliente); cart.Controls.Add(lc); cart.Controls.Add(title);

        v.Controls.Add(host); v.Controls.Add(cart);
        return v;
    }

    Panel Card(Jogo j)
    {
        var p = new Panel { Width = 210, Height = 384, BackColor = Pn, Margin = new Padding(0, 0, 16, 16) };
        var pic = new PictureBox { Dock = DockStyle.Top, Height = 280, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(58, 10, 15) };
        var fb = new Label { Text = j.titulo, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Color.Transparent, Font = new Font("Segoe UI", 15f, FontStyle.Bold) };
        pic.Controls.Add(fb);
        if (j.estoque <= 0)
        {
            var sold = new Label { Text = "ESGOTADO", AutoSize = true, BackColor = Red, ForeColor = Color.White, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Padding = new Padding(6, 2, 6, 2), Location = new Point(8, 8) };
            pic.Controls.Add(sold); sold.BringToFront();
        }
        CarregaCapa(pic, fb, j.titulo);
        p.Controls.Add(pic);

        var title = Lbl(j.titulo, 10.5f, true); title.AutoSize = false; title.AutoEllipsis = true; title.Size = new Size(190, 22); title.Location = new Point(12, 288);
        var sub = Lbl($"{j.plataforma} · {j.genero}", 9, false, Mut); sub.Location = new Point(12, 312);
        var price = Lbl(j.preco.ToString("C", PtBR), 12.5f, true); price.Location = new Point(12, 338);
        var stock = Lbl(j.estoque > 0 ? $"{j.estoque} em estoque" : "Sem estoque", 8.5f, false, j.estoque > 0 ? Mut : Red2); stock.Location = new Point(12, 362);
        var add = Btn(j.estoque > 0 ? "Adicionar" : "Esgotado", () => AddCart(j));
        add.AutoSize = false; add.Size = new Size(92, 32); add.Location = new Point(108, 332); add.Enabled = j.estoque > 0;
        if (j.estoque <= 0) add.BackColor = Color.FromArgb(50, 50, 50);
        p.Controls.AddRange(new Control[] { title, sub, price, stock, add });
        return p;
    }

    // Ajusta a capa para preencher todo o espaço (recorta o excesso, sem esticar nem deixar faixas)
    static Bitmap Cobrir(Image src, int w, int h)
    {
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        float scale = Math.Max((float)w / src.Width, (float)h / src.Height);
        float sw = w / scale, sh = h / scale;
        var origem = new RectangleF((src.Width - sw) / 2, (src.Height - sh) / 2, sw, sh);
        g.DrawImage(src, new Rectangle(0, 0, w, h), origem, GraphicsUnit.Pixel);
        return bmp;
    }

    void CarregaCapa(PictureBox pic, Label fb, string titulo)
    {
        var img = Api.Capa(titulo);
        if (img != null && !pic.IsDisposed) { pic.Image = Cobrir(img, 210, 280); fb.Visible = false; }
    }

    void ReloadGrid()
    {
        var plat = _platFiltro.SelectedIndex > 0 ? (string)_platFiltro.SelectedItem! : "";
        _lista = Api.Jogos(_busca.Text, plat);
        _grid.SuspendLayout();
        while (_grid.Controls.Count > 0) _grid.Controls[0].Dispose();
        foreach (var j in _lista) _grid.Controls.Add(Card(j));
        _grid.ResumeLayout();
    }

    // ---------- carrinho ----------
    void AddCart(Jogo j)
    {
        _cart.TryGetValue(j.id, out var q);
        if (j.estoque <= 0) { Erro("Estoque zerado: venda não permitida."); return; }
        if (q + 1 > j.estoque) { Erro("Quantidade maior que o estoque disponível."); return; }
        _cart[j.id] = q + 1;
        RefreshCart();
        Aviso($"{j.titulo} adicionado ao carrinho");
    }

    void RefreshCart()
    {
        int sel = _cartList.SelectedIndex; double tot = 0;
        _cartList.Items.Clear();
        foreach (var (id, q) in _cart.ToList())
        {
            var j = _todos.FirstOrDefault(x => x.id == id);
            if (j == null) { _cart.Remove(id); continue; }
            tot += j.preco * q;
            _cartList.Items.Add(new CartLine(id, $"{q}×  {j.titulo}  -  {(j.preco * q).ToString("C", PtBR)}"));
        }
        if (_cartList.Items.Count > 0) _cartList.SelectedIndex = Math.Clamp(sel, 0, _cartList.Items.Count - 1);
        _total.Text = $"Total: {tot.ToString("C", PtBR)}";
    }

    void Qty(int d)
    {
        if (_cartList.SelectedItem is not CartLine l) return;
        var j = _todos.First(x => x.id == l.Id);
        int q = _cart[l.Id] + d;
        if (q > j.estoque) { Erro("Quantidade maior que o estoque disponível."); return; }
        if (q <= 0) _cart.Remove(l.Id); else _cart[l.Id] = q;
        RefreshCart();
    }

    void RemoveLine() { if (_cartList.SelectedItem is CartLine l) { _cart.Remove(l.Id); RefreshCart(); } }

    void Vender()
    {
        if (_cliente.SelectedItem is not Cliente c) throw new ApiException("Selecione um cliente.");
        if (_cart.Count == 0) throw new ApiException("O carrinho está vazio.");
        var total = Api.Vender(c.id, _cart);
        _cart.Clear();
        RefreshAll();
        Aviso($"Venda registrada: {total.ToString("C", PtBR)}");
    }

    // ---------- Jogos ----------
    Panel BuildJogos()
    {
        var v = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 8, 24, 16) };
        _gJogos = Grid("Título", "Plataforma", "Gênero", "Preço", "Estoque");
        var acts = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(0, 10, 0, 0) };
        acts.Controls.Add(Btn("+5 estoque", Safe(() => Ajuste(5)), true));
        acts.Controls.Add(Btn("−1 estoque", Safe(() => Ajuste(-1)), true));
        acts.Controls.Add(Btn("Excluir jogo", Safe(DelJogo), true));
        var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60 };
        _fTitulo = Txt("Título", 260); _fPlat = Combo(Plats, 110); _fGenero = Txt("Gênero", 150); _fPreco = Txt("Preço (ex: 249,90)", 130); _fEstoque = Txt("Estoque", 80);
        form.Controls.AddRange(new Control[] { _fTitulo, _fPlat, _fGenero, _fPreco, _fEstoque, Btn("Cadastrar", Safe(NovoJogo)) });
        Label sub;
        v.Controls.Add(_gJogos); v.Controls.Add(acts); v.Controls.Add(form); v.Controls.Add(Header("Jogos", out sub));
        sub.Text = "Cadastro e controle de estoque";
        return v;
    }

    void FillJogos()
    {
        _gJogos.Rows.Clear();
        foreach (var j in _todos)
        {
            int i = _gJogos.Rows.Add(j.titulo, j.plataforma, j.genero, j.preco.ToString("C", PtBR), j.estoque);
            _gJogos.Rows[i].Tag = j.id;
            if (j.estoque == 0) _gJogos.Rows[i].Cells[4].Style.ForeColor = Red2;
        }
    }

    static int? SelId(DataGridView g) => g.SelectedRows.Count > 0 ? (int?)g.SelectedRows[0].Tag : null;

    void NovoJogo()
    {
        if (!decimal.TryParse(_fPreco.Text, NumberStyles.Number, PtBR, out var preco)) throw new ApiException("Informe um preço válido (ex: 249,90).");
        int est = 0;
        if (!string.IsNullOrWhiteSpace(_fEstoque.Text) && !int.TryParse(_fEstoque.Text, out est)) throw new ApiException("Informe um estoque válido (número inteiro).");
        Api.NovoJogo(_fTitulo.Text, _fPlat.Text, _fGenero.Text, (double)preco, est);
        _fTitulo.Clear(); _fGenero.Clear(); _fPreco.Clear(); _fEstoque.Clear();
        RefreshAll();
        Aviso("Jogo cadastrado");
    }

    void Ajuste(int d)
    {
        var id = SelId(_gJogos) ?? throw new ApiException("Selecione um jogo na lista.");
        Api.AjustaEstoque(id, d);
        RefreshAll();
        Aviso("Estoque atualizado");
    }

    void DelJogo()
    {
        var id = SelId(_gJogos) ?? throw new ApiException("Selecione um jogo na lista.");
        if (MessageBox.Show(this, "Excluir este jogo?", "PixelVault", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Api.ExcluiJogo(id);
        RefreshAll();
    }

    // ---------- Clientes ----------
    Panel BuildClientes()
    {
        var v = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 8, 24, 16) };
        _gClientes = Grid("Nome", "E-mail", "Telefone");
        var acts = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(0, 10, 0, 0) };
        acts.Controls.Add(Btn("Excluir cliente", Safe(DelCliente), true));
        var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60 };
        _cNome = Txt("Nome", 260); _cEmail = Txt("E-mail", 260); _cTel = Txt("Telefone", 170);
        form.Controls.AddRange(new Control[] { _cNome, _cEmail, _cTel, Btn("Cadastrar", Safe(NovoCliente)) });
        Label sub;
        v.Controls.Add(_gClientes); v.Controls.Add(acts); v.Controls.Add(form); v.Controls.Add(Header("Clientes", out sub));
        sub.Text = "Cadastro de clientes";
        return v;
    }

    void FillClientes()
    {
        _gClientes.Rows.Clear();
        foreach (var c in _clientes) { int i = _gClientes.Rows.Add(c.nome, c.email, c.telefone); _gClientes.Rows[i].Tag = c.id; }
    }

    void NovoCliente()
    {
        Api.NovoCliente(_cNome.Text, _cEmail.Text, _cTel.Text);
        _cNome.Clear(); _cEmail.Clear(); _cTel.Clear();
        RefreshAll();
        Aviso("Cliente cadastrado");
    }

    void DelCliente()
    {
        var id = SelId(_gClientes) ?? throw new ApiException("Selecione um cliente na lista.");
        Api.ExcluiCliente(id);
        RefreshAll();
    }

    // ---------- Vendas ----------
    Panel BuildVendas()
    {
        var v = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 8, 24, 16) };
        _gVendas = Grid("#", "Cliente", "Itens", "Data", "Total");
        _gVendas.Columns[0].FillWeight = 20; _gVendas.Columns[2].FillWeight = 160;
        v.Controls.Add(_gVendas); v.Controls.Add(Header("Vendas", out _vendasSub));
        return v;
    }

    void FillVendas(List<Venda> vendas, Resumo r)
    {
        _vendasSub.Text = $"Faturamento: {r.faturamento.ToString("C", PtBR)}   ·   {r.vendas} vendas   ·   {r.clientes} clientes";
        _gVendas.Rows.Clear();
        foreach (var x in vendas)
            _gVendas.Rows.Add(x.id, x.cliente, string.Join(" · ", x.itens.Select(i => $"{i.qtd}× {i.titulo}")), x.data, x.total.ToString("C", PtBR));
    }

    // ---------- carrega tudo do servidor ----------
    void RefreshAll()
    {
        _todos = Api.Jogos();
        _clientes = Api.Clientes();
        var vendas = Api.Vendas();
        var r = Api.Resumo();
        FillJogos(); FillClientes(); FillVendas(vendas, r);

        int sel = (_cliente.SelectedItem as Cliente)?.id ?? -1;
        _cliente.DataSource = null;
        _cliente.DataSource = _clientes;
        _cliente.DisplayMember = "nome";
        int ix = _clientes.FindIndex(c => c.id == sel);
        _cliente.SelectedIndex = ix >= 0 ? ix : (_clientes.Count > 0 ? 0 : -1);

        _lojaSub.Text = $"{_todos.Count} jogos · {r.estoque} unidades em estoque";
        RefreshCart();
        ReloadGrid();
    }
}
