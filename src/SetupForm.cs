using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CmdCrew
{
    // Assistente de instalação: boas-vindas → escolher a equipe → instalando → pronto.
    public class SetupForm : Form
    {
        readonly List<Character> chars;
        readonly float k;
        readonly CrewBanner banner;
        readonly Panel body;
        Character chosen;
        ComboBox cbSkin;
        NumericUpDown numCount;
        ComboBox cbMode;
        Label lblDesc;
        readonly List<CharTile> tiles = new List<CharTile>();

        int S(int v) { return (int)Math.Round(v * k); }

        // page: só para revisar o visual ("escolher", "pronto", "erro"), sem instalar nada.
        public SetupForm(string page = null)
        {
            chars = Character.LoadEmbedded(null);
            if (chars.Count == 0) chars = Character.LoadAll(null);
            // destaques primeiro, como no site
            var order = new[] { "anoes", "gnomos-jardim", "oompa-loompas", "programadores", "clawd", "claude", "dragao", "elfos", "gatos", "pinguins", "formigas", "robos", "gnomos" };
            chars = chars.OrderBy(c => Array.IndexOf(order, c.Id) < 0 ? 99 : Array.IndexOf(order, c.Id)).ThenBy(c => c.Name).ToList();
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            AutoScaleMode = AutoScaleMode.None;
            Text = "CmdCrew: instalação";
            ClientSize = new Size(S(760), S(560));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Font = Theme.Body(10f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            banner = new CrewBanner { Location = new Point(0, 0), Size = new Size(ClientSize.Width, S(150)), BackColor = Theme.Bg2, Unit = 4 };
            Controls.Add(banner);
            body = new Panel { Location = new Point(0, S(150)), Size = new Size(ClientSize.Width, ClientSize.Height - S(150)), BackColor = Theme.Bg };
            Controls.Add(body);
            Shown += delegate
            {
                // na abertura, um pouco de todo mundo (na tela de escolha, a faixa mostra a equipe escolhida)
                if (tiles.Count == 0) banner.SetCrew(chars.OrderBy(c => c.Width > 20 ? 0 : 1).ToList(), "*", Math.Min(6, Math.Max(3, chars.Count)));
            };
            chosen = Character.Find(chars, "oompa-loompas") ?? chars.FirstOrDefault();
            if (page == "escolher") ShowChoose();
            else if (page == "pronto") ShowDone();
            else if (page == "erro") ShowError("Exemplo de erro: não consegui gravar no settings.json do Claude Code.");
            else ShowWelcome();
        }

        // ---------- utilidades de layout ----------

        Label Text_(string text, int x, int y, int w, int h, Font font, Color color)
        {
            var l = new Label { Text = text, Location = new Point(S(x), S(y)), Size = new Size(S(w), S(h)), Font = font, ForeColor = color, BackColor = Color.Transparent };
            body.Controls.Add(l);
            return l;
        }

        PixelButton Button_(string text, int x, int y, int w, bool primary, Action onClick)
        {
            var b = new PixelButton { Text = text, Primary = primary, Location = new Point(S(x), S(y)), Size = new Size(S(w), S(46)) };
            b.Click += delegate { onClick(); };
            body.Controls.Add(b);
            return b;
        }

        void Clear()
        {
            foreach (Control c in body.Controls.Cast<Control>().ToList()) c.Dispose();
            body.Controls.Clear();
            tiles.Clear();
        }

        // ---------- 1. boas-vindas ----------

        void ShowWelcome()
        {
            Clear();
            Text_("Contrate a equipe!", 40, 24, 680, 52, Theme.Pixel(28f), Theme.Ink);
            Text_("Enquanto o Claude Code trabalha, uma equipe de bichinhos em pixel art trabalha junto: em cima do terminal " +
                  "ou numa salinha no canto da tela, uma por terminal. Quando o Claude termina, eles comemoram e avisam.",
                  42, 84, 670, 70, Theme.Body(11f), Theme.Ink2);

            bool found = Installer.ClaudeCodeFound;
            var status = new StatusBadge
            {
                Ok = found,
                Text = found ? "Claude Code encontrado neste computador." :
                    "Não encontrei o Claude Code. Dá para instalar mesmo assim: a equipe aparece quando ele estiver instalado.",
                Location = new Point(S(42), S(166)), Size = new Size(S(670), S(44)), Font = Theme.Body(10f, FontStyle.Bold)
            };
            body.Controls.Add(status);
            if (Installer.IsInstalled && !Installer.RunningInstalled)
                Text_("Já existe uma instalação: ela será atualizada e suas frases e configurações continuam.", 42, 218, 670, 22, Theme.Body(9.5f), Theme.Accent2);

            Text_("Instala em " + Installer.InstallDir + "\r\nNão precisa de administrador · nada sai do seu computador",
                  42, 330, 410, 44, Theme.Body(8.5f), Theme.Ink2);
            Button_("Cancelar", 470, 330, 110, false, Close);
            Button_(Installer.IsInstalled ? "Atualizar  ▸" : "Começar  ▸", 592, 330, 130, true, ShowChoose);
        }

        // ---------- 2. escolher a equipe ----------

        void ShowChoose()
        {
            Clear();
            Text_("Quem vai trabalhar pra você?", 40, 16, 680, 40, Theme.Pixel(20f), Theme.Ink);

            var grid = new FlowLayoutPanel
            {
                Location = new Point(S(36), S(58)), Size = new Size(S(446), S(262)), AutoScroll = true,
                BackColor = Theme.Bg, Padding = new Padding(0)
            };
            body.Controls.Add(grid);
            foreach (var ch in chars)
            {
                var t = new CharTile(ch, k) { Size = new Size(S(102), S(116)), Margin = new Padding(S(3)) };
                t.Selected = ch == chosen;
                t.Click += delegate { Choose(ch); };
                tiles.Add(t);
                grid.Controls.Add(t);
            }

            lblDesc = Text_("", 500, 60, 224, 110, Theme.Body(9.5f), Theme.Ink2);
            Text_("Cores", 500, 176, 220, 20, Theme.Body(9.5f, FontStyle.Bold), Theme.Ink);
            cbSkin = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(S(500), S(198)), Size = new Size(S(222), S(26)), BackColor = Theme.Card, ForeColor = Theme.Ink, Font = Theme.Body(9.5f) };
            cbSkin.SelectedIndexChanged += delegate { RefreshBanner(); };
            body.Controls.Add(cbSkin);
            Text_("Quantos na equipe", 500, 236, 220, 20, Theme.Body(9.5f, FontStyle.Bold), Theme.Ink);
            numCount = new NumericUpDown { Minimum = 1, Maximum = 12, Value = 4, Location = new Point(S(500), S(258)), Size = new Size(S(80), S(26)), BackColor = Theme.Card, Font = Theme.Body(9.5f) };
            numCount.ValueChanged += delegate { RefreshBanner(); };
            body.Controls.Add(numCount);
            Text_("Onde eles aparecem", 500, 290, 220, 20, Theme.Body(9.5f, FontStyle.Bold), Theme.Ink);
            cbMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(S(500), S(310)), Size = new Size(S(222), S(26)), BackColor = Theme.Card, ForeColor = Theme.Ink, Font = Theme.Body(9.5f) };
            cbMode.Items.AddRange(new object[] { "Salas no canto da tela", "Em cima do terminal", "Os dois" });
            cbMode.SelectedIndex = 0;
            body.Controls.Add(cbMode);

            Button_("◂  Voltar", 36, 336, 120, false, ShowWelcome);
            Button_("Instalar  ▸", 592, 352, 130, true, ShowInstalling);
            Choose(chosen);
        }

        void Choose(Character ch)
        {
            chosen = ch;
            foreach (var t in tiles) { t.Selected = t.Character == ch; t.Invalidate(); }
            lblDesc.Text = ch.Name + "\r\n" + ch.Description;
            cbSkin.Items.Clear();
            cbSkin.Items.Add("Misturar todas as cores");
            foreach (var s in ch.Skins) cbSkin.Items.Add(s.Name);
            cbSkin.SelectedIndex = 0;
            RefreshBanner();
        }

        string SkinId() { return cbSkin == null || cbSkin.SelectedIndex <= 0 ? "*" : chosen.Skins[cbSkin.SelectedIndex - 1].Id; }

        void RefreshBanner()
        {
            if (chosen == null || numCount == null) return;
            banner.SetCrew(new List<Character> { chosen }, SkinId(), (int)Math.Min(8, numCount.Value));
        }

        // ---------- 3. instalando ----------

        void ShowInstalling()
        {
            var cfg = new Config { Character = chosen.Id, Skin = SkinId(), Count = (int)numCount.Value,
                Mode = cbMode == null ? "salas" : new[] { "salas", "equipe", "ambos" }[Math.Max(0, cbMode.SelectedIndex)] };
            Clear();
            Text_("Contratando a equipe...", 40, 40, 680, 44, Theme.Pixel(22f), Theme.Ink);
            var step = Text_("", 42, 96, 670, 24, Theme.Body(11f), Theme.Ink2);
            var bar = new BusyBar { Location = new Point(S(42), S(134)), Size = new Size(S(676), S(34)) };
            body.Controls.Add(bar);
            var t = new Thread(() =>
            {
                try
                {
                    Installer.Install(cfg, s => BeginInvoke((Action)(() => step.Text = s)));
                    Thread.Sleep(600);
                    BeginInvoke((Action)ShowDone);
                }
                catch (Exception ex)
                {
                    Paths.Log("setup: " + ex);
                    BeginInvoke((Action)(() => ShowError(ex.Message)));
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- 4. pronto ----------

        void ShowDone()
        {
            Clear();
            Text_("Equipe contratada!", 40, 24, 680, 52, Theme.Pixel(28f), Theme.Ink);
            Text_("Na próxima vez que você mandar um prompt no Claude Code, eles aparecem. " +
                  "Se o Claude Code já estiver aberto, feche e abra de novo para ele perceber a equipe nova.",
                  42, 84, 670, 64, Theme.Body(11f), Theme.Ink2);
            Text_("Para mudar personagem, cores, frases e tamanho, procure \"CmdCrew\" no menu Iniciar. " +
                  "Para desinstalar, use \"Aplicativos instalados\" do Windows.",
                  42, 160, 670, 48, Theme.Body(9.5f), Theme.Ink2);
            Button_("▶  Testar agora", 42, 330, 170, false, () => Run("demo-salas 3 10"));
            Button_("Configurar", 224, 330, 130, false, () => { Run("config"); Close(); });
            Button_("Concluir", 592, 330, 130, true, Close);
        }

        void ShowError(string msg)
        {
            Clear();
            Text_("Ops, a equipe tropeçou.", 40, 24, 680, 52, Theme.Pixel(24f), Theme.Ink);
            Text_(msg, 42, 84, 670, 90, Theme.Body(10.5f), Color.Firebrick);
            Text_("Os detalhes ficam em " + Paths.LogFile, 42, 180, 670, 22, Theme.Body(9f), Theme.Ink2);
            Button_("Fechar", 470, 330, 110, false, Close);
            Button_("Tentar de novo", 592, 330, 130, true, ShowChoose);
        }

        static void Run(string args)
        {
            try { Process.Start(new ProcessStartInfo(Installer.InstalledExe, args) { UseShellExecute = true }); }
            catch (Exception ex) { Paths.Log("setup run: " + ex.Message); }
        }
    }

    // Cartão de personagem da grade de escolha.
    class CharTile : Control
    {
        public readonly Character Character;
        public bool Selected;
        readonly Bitmap img;
        readonly float k;
        bool hover;

        public CharTile(Character ch, float k)
        {
            Character = ch; this.k = k;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            int rows = ch.BottomRow - ch.TopRow + 1;
            int px = Math.Max(1, (int)Math.Floor(64 * k / Math.Max(rows, ch.Width * 0.8)));
            img = ch.Render(ch.Skins[0], "parado", px, false)[0];
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.DrawCard(g, ClientRectangle, Selected ? Theme.Sun : hover ? Theme.Bg2 : Theme.Card, k, Selected ? 4 : 3, 10);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            int top = Character.TopRow * (img.Height / Character.Height);
            int visH = img.Height - top;
            int area = Height - (int)(38 * k);
            g.DrawImage(img, new Rectangle((Width - (int)(4 * k) - img.Width) / 2, (int)(8 * k) + Math.Max(0, area - visH - (int)(8 * k)), img.Width, visH),
                new Rectangle(0, top, img.Width, visH), GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, Character.Name, Theme.Body(8.5f, FontStyle.Bold),
                new Rectangle((int)(4 * k), Height - (int)(36 * k), Width - (int)(10 * k), (int)(30 * k)), Theme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        protected override void Dispose(bool disposing) { if (disposing) img.Dispose(); base.Dispose(disposing); }
    }

    // Aviso com ícone de "ok" (verde) ou atenção (amarelo).
    class StatusBadge : Control
    {
        public bool Ok;
        public StatusBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float k; using (var dg = CreateGraphics()) k = dg.DpiX / 96f;
            using (var p = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), 10 * k))
            {
                using (var b = new SolidBrush(Ok ? Color.FromArgb(221, 244, 233) : Color.FromArgb(255, 241, 204))) g.FillPath(b, p);
                using (var pen = new Pen(Theme.Ink, 2 * k)) g.DrawPath(pen, p);
            }
            int d = (int)(22 * k), y = (Height - d) / 2;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Ok ? Theme.Mint : Theme.Sun)) g.FillEllipse(b, 12 * k, y, d, d);
            TextRenderer.DrawText(g, Ok ? "✓" : "!", Theme.Body(10f, FontStyle.Bold), new Rectangle((int)(12 * k), y, d, d), Theme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(44 * k), 0, Width - (int)(52 * k), Height), Theme.Ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
    }

    // Barra de "trabalhando" com listras andando.
    class BusyBar : Control
    {
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 40 };
        int offset;
        public BusyBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            timer.Tick += delegate { offset = (offset + 2) % 40; Invalidate(); };
            timer.Start();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float k; using (var dg = CreateGraphics()) k = dg.DpiX / 96f;
            g.Clear(Theme.Bg);
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Theme.Round(r, 8 * k))
            {
                g.SetClip(p);
                using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, r);
                using (var b = new SolidBrush(Theme.Accent2))
                    for (int x = -40 + offset; x < Width + 40; x += 40)
                        g.FillPolygon(b, new[] { new Point(x, Height), new Point(x + 20, Height), new Point(x + 20 + Height, 0), new Point(x + Height, 0) });
                g.ResetClip();
                using (var pen = new Pen(Theme.Ink, 3 * k)) g.DrawPath(pen, p);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
