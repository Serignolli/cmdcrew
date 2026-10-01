using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

namespace CmdCrew
{
    // Tela de configuração: o modo (salas, equipe ou os dois), as opções de cada um, os sons e os pacotes.
    // Tudo é salvo na hora; as janelas abertas leem o config.json de novo sozinhas.
    public class ConfigForm : Form
    {
        static readonly string[] ModeIds = { "salas", "equipe", "ambos" };
        static readonly string[] PosIds = { "terminal", "rodape", "topo" };

        readonly Config cfg;
        readonly List<Character> chars;
        readonly List<RoomPreview> previews = new List<RoomPreview>();
        readonly RadioButton[] modeRadios = new RadioButton[3];
        readonly float k;
        CheckBox enabled, mix, showName, permissions, bubbles, terminalPhrases, soundDone, soundAttention, soundFailure;
        ComboBox corner, position, cbChar, cbSkin;
        TrackBar roomSize, speed;
        NumericUpDown doneSecs, count, crewSize;
        PreviewBox crewPreview;
        Label hookStatus, packStatus, status;
        PixelButton hookBtn;
        Panel roomsPanel, crewPanel;
        bool loading = true;

        int S(int v) { return (int)Math.Round(v * k); }

        public ConfigForm()
        {
            cfg = Config.Load();
            chars = Character.LoadAll(null);
            using (var g = CreateGraphics()) k = g.DpiX / 96f;

            AutoScaleMode = AutoScaleMode.None;
            Text = "CmdCrew: configuração";
            Font = Theme.Body(9.5f);
            BackColor = Theme.Bg;
            ForeColor = Theme.Ink;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildUi();
            Theme.Apply(this);
            LoadValues();
            loading = false;
        }

        // ---------- montagem ----------

        T Add<T>(Control parent, T c, int x, int y, int w, int h) where T : Control
        {
            c.Location = new Point(S(x), S(y));
            c.Size = new Size(S(w), S(h));
            parent.Controls.Add(c);
            return c;
        }

        Label Lbl(Control parent, string text, int x, int y, int w, int h = 20, bool bold = false)
        {
            var l = Add(parent, new Label { Text = text }, x, y, w, h);
            if (bold) l.Font = Theme.Body(10f, FontStyle.Bold);
            return l;
        }

        CheckBox Chk(Control parent, string text, int x, int y, int w)
        {
            var c = Add(parent, new CheckBox { Text = text }, x, y, w, 24);
            c.CheckedChanged += delegate { Changed(); };
            return c;
        }

        void BuildUi()
        {
            const int W = 1010;
            Add(this, new Label { Text = "CmdCrew", Font = Theme.Pixel(22f), AutoSize = true }, 18, 10, 200, 44);
            var sub = Lbl(this, "Uma equipe de bichinhos em pixel art que trabalha junto com o Claude Code e avisa quando ele termina.", 20, 52, 760);
            sub.ForeColor = Theme.Ink2;

            // modo
            Lbl(this, "Onde eles aparecem", 20, 84, 160, 22, true);
            string[] modeNames = { "Salas no canto da tela", "Em cima do terminal", "Os dois" };
            for (int i = 0; i < 3; i++)
            {
                var r = Add(this, new RadioButton { Text = modeNames[i] }, 186 + i * 190, 82, 186, 26);
                int idx = i;
                r.CheckedChanged += delegate { if (r.Checked && !loading) { cfg.Mode = ModeIds[idx]; Changed(); } };
                modeRadios[i] = r;
            }
            enabled = Chk(this, "Ativo", 900, 84, 90);

            // ----- salas -----
            roomsPanel = Add(this, new Panel { BackColor = Theme.Bg2 }, 14, 118, 600, 470);
            Lbl(roomsPanel, "Salas no canto da tela", 12, 10, 400, 22, true);
            var hint = Lbl(roomsPanel, "Uma sala por terminal trabalhando. Duplo clique na sala leva ao terminal dela.", 12, 32, 576);
            hint.ForeColor = Theme.Ink2;
            float unit = Math.Max(2f, 2.6f * k);
            int x = 6;
            foreach (var t in RoomTheme.All)
            {
                var pv = new RoomPreview(t, unit) { Location = new Point(S(x), S(56)) };
                pv.Click += (s, e) => { mix.Checked = false; cfg.Theme = ((RoomPreview)s).RoomTheme.Id; Changed(); };
                roomsPanel.Controls.Add(pv);
                previews.Add(pv);
                bool premium = pv.UsesPremium;
                var desc = Lbl(roomsPanel, t.Name + (premium ? "  ★ Supporters Pack" : "") + "\n" + t.Description, x + 6, 56 + (int)(pv.Height / k) + 2, (int)(pv.Width / k) - 8, 52);
                desc.ForeColor = Theme.Ink2;
                desc.Font = Theme.Body(8.5f);
                x += (int)(pv.Width / k) + 10;
            }
            int y = 56 + (int)(previews[0].Height / k) + 58;
            mix = Chk(roomsPanel, "Alternar os temas (cada terminal novo ganha o próximo)", 12, y, 560);
            y += 32;
            Lbl(roomsPanel, "Tamanho", 12, y + 8, 70);
            roomSize = Add(roomsPanel, new TrackBar { Minimum = 1, Maximum = 4, TickStyle = TickStyle.Both, AutoSize = false }, 80, y, 150, 36);
            roomSize.ValueChanged += delegate { Changed(); };
            Lbl(roomsPanel, "Canto", 256, y + 8, 50);
            corner = Add(roomsPanel, new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 306, y + 4, 170, 26);
            corner.Items.AddRange(new object[] { "Direita, embaixo", "Esquerda, embaixo" });
            corner.SelectedIndexChanged += delegate { Changed(); };
            y += 44;
            Lbl(roomsPanel, "Depois de terminar, a sala fica", 12, y + 3, 200);
            doneSecs = Add(roomsPanel, new NumericUpDown { Minimum = 2, Maximum = 120 }, 212, y, 60, 26);
            doneSecs.ValueChanged += delegate { Changed(); };
            Lbl(roomsPanel, "segundos na tela", 280, y + 3, 150);
            y += 34;
            showName = Chk(roomsPanel, "Nome da pasta do projeto na sala", 12, y, 280);
            permissions = Chk(roomsPanel, "Permitir ou negar permissões direto na sala", 296, y, 300);

            // ----- equipe -----
            crewPanel = Add(this, new Panel { BackColor = Theme.Bg2 }, 626, 118, 370, 470);
            Lbl(crewPanel, "Equipe em cima do terminal", 12, 10, 340, 22, true);
            cbChar = Add(crewPanel, new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 12, 38, 346, 26);
            foreach (var c in chars) cbChar.Items.Add(c);
            cbChar.Format += (s, e) => { var c = e.ListItem as Character; if (c != null) e.Value = c.Name + (c.Premium ? "  ★" : ""); };
            cbChar.SelectedIndexChanged += delegate { OnCharacterChanged(); };
            cbSkin = Add(crewPanel, new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 12, 70, 346, 26);
            cbSkin.SelectedIndexChanged += delegate { if (!loading) { cfg.Skin = SelectedSkin(); Changed(); } };
            crewPreview = Add(crewPanel, new PreviewBox(), 12, 102, 346, 130);
            Lbl(crewPanel, "Quantos", 12, 246, 70);
            count = Add(crewPanel, new NumericUpDown { Minimum = 1, Maximum = 15 }, 82, 243, 56, 26);
            count.ValueChanged += delegate { Changed(); };
            Lbl(crewPanel, "Tamanho", 160, 246, 70);
            crewSize = Add(crewPanel, new NumericUpDown { Minimum = 1, Maximum = 8 }, 230, 243, 56, 26);
            crewSize.ValueChanged += delegate { Changed(); };
            Lbl(crewPanel, "Velocidade", 12, 286, 80);
            speed = Add(crewPanel, new TrackBar { Minimum = 3, Maximum = 30, TickFrequency = 3, AutoSize = false }, 92, 278, 200, 36);
            speed.ValueChanged += delegate { Changed(); };
            position = Add(crewPanel, new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 12, 324, 346, 26);
            position.Items.AddRange(new object[] { "Em cima da janela do terminal", "No rodapé da tela", "No topo da tela" });
            position.SelectedIndexChanged += delegate { Changed(); };
            bubbles = Chk(crewPanel, "Balões de fala", 12, 362, 160);
            terminalPhrases = Chk(crewPanel, "Frases no spinner do terminal", 172, 362, 196);
            var edit = Add(crewPanel, new Button { Text = "Editar frases…" }, 12, 398, 150, 30);
            edit.Click += delegate
            {
                var c = cbChar.SelectedItem as Character;
                if (c == null) return;
                using (var f = new PhrasesForm(cfg, c)) if (f.ShowDialog(this) == DialogResult.OK) Changed();
            };
            var demo = Add(crewPanel, new Button { Text = "▶ Ver na tela" }, 172, 398, 150, 30);
            demo.Click += delegate { TestCrew(); };

            // ----- sons -----
            int by = 600;
            Lbl(this, "Sons", 20, by + 3, 50, 22, true);
            soundDone = Chk(this, "Quando termina", 76, by, 128);
            AddPlay(204, by, "pronto");
            soundAttention = Chk(this, "Quando precisa de você", 250, by, 176);
            AddPlay(426, by, "atencao");
            soundFailure = Chk(this, "Quando falha", 472, by, 110);
            AddPlay(582, by, "falha");
            var credit = Lbl(this, "feitos no SFX Forge", 630, by + 4, 180);
            credit.ForeColor = Theme.Ink2;
            credit.Font = Theme.Body(8.5f);

            // ----- pacotes -----
            by += 36;
            Lbl(this, "Pacotes", 20, by + 6, 70, 22, true);
            packStatus = Lbl(this, "", 96, by + 6, 520);
            packStatus.ForeColor = Theme.Ink2;
            var addPack = Add(this, new Button { Text = "Adicionar pacote…" }, 630, by, 170, 30);
            addPack.Click += delegate { AddPack(); };
            var openPacks = Add(this, new Button { Text = "Abrir a pasta" }, 810, by, 130, 30);
            openPacks.Click += delegate { Directory.CreateDirectory(Paths.PacksDir); Process.Start("explorer.exe", "\"" + Paths.PacksDir + "\""); };

            // ----- rodapé -----
            by += 50;
            hookStatus = Lbl(this, "", 20, by + 4, 470, 40);
            status = Lbl(this, "", 20, by + 46, 600);
            status.ForeColor = Color.FromArgb(40, 120, 70);
            hookBtn = Add(this, new PixelButton(), W - 360, by, 166, 44);
            hookBtn.Click += delegate { ToggleHooks(); };
            var test = Add(this, new PixelButton { Text = "Testar salas", Primary = true }, W - 184, by, 166, 44);
            test.Click += delegate { Program.Launch("demo-salas 3 10"); SetStatus("Olha o canto da tela!"); };
            // discreto de propósito: quem quiser sair acha, mas não é o destaque da tela
            var uninstall = Add(this, new LinkLabel { Text = "Desinstalar", TextAlign = ContentAlignment.MiddleRight }, W - 184, by + 50, 166, 20);
            uninstall.Font = Theme.Body(8.5f);
            uninstall.LinkColor = uninstall.ActiveLinkColor = Theme.Ink2;
            uninstall.LinkBehavior = LinkBehavior.HoverUnderline;
            uninstall.LinkClicked += delegate { if (Program.ConfirmUninstall(this)) Close(); };
            ClientSize = new Size(S(W), S(by + 76));
        }

        void AddPlay(int x, int y, string sound)
        {
            var b = Add(this, new Button { Text = "▶" }, x, y - 1, 34, 26);
            b.Click += delegate { Sounds.Play(sound); };
        }

        // ---------- dados <-> tela ----------

        void LoadValues()
        {
            bool old = loading;
            loading = true;
            int mi = Math.Max(0, Array.IndexOf(ModeIds, cfg.Mode));
            for (int i = 0; i < 3; i++) modeRadios[i].Checked = i == mi;
            enabled.Checked = cfg.Enabled;
            mix.Checked = cfg.Theme == "*";
            foreach (var p in previews) { p.Selected = cfg.Theme != "*" && p.RoomTheme.Id == RoomTheme.Find(cfg.Theme).Id; p.Invalidate(); }
            roomSize.Value = Math.Max(1, Math.Min(4, cfg.RoomSize));
            corner.SelectedIndex = cfg.Corner == "esquerda" ? 1 : 0;
            doneSecs.Value = (decimal)Math.Max(2, Math.Min(120, cfg.DoneSeconds));
            showName.Checked = cfg.ShowName;
            permissions.Checked = cfg.RoomPermissions;
            var cur = Character.Find(chars, cfg.Character);
            if (cur != null && cbChar.SelectedItem != cur) { cbChar.SelectedItem = cur; FillSkins(cur); }
            count.Value = Math.Max(1, Math.Min(15, cfg.Count));
            crewSize.Value = Math.Max(1, Math.Min(8, cfg.Size));
            speed.Value = Math.Max(3, Math.Min(30, (int)Math.Round(cfg.Speed * 10)));
            position.SelectedIndex = Math.Max(0, Array.IndexOf(PosIds, cfg.Position));
            bubbles.Checked = cfg.Bubbles;
            terminalPhrases.Checked = cfg.TerminalPhrases;
            soundDone.Checked = cfg.SoundDone; soundAttention.Checked = cfg.SoundAttention; soundFailure.Checked = cfg.SoundFailure;
            roomsPanel.Enabled = cfg.RoomsOn;
            crewPanel.Enabled = cfg.CrewOn;
            UpdateHooks();
            UpdatePacks();
            loading = old;
        }

        void OnCharacterChanged()
        {
            var c = cbChar.SelectedItem as Character;
            if (c == null) return;
            if (!loading && c.Id != cfg.Character) { cfg.Character = c.Id; cfg.Skin = "*"; }
            FillSkins(c);
            if (!loading) Changed();
        }

        void FillSkins(Character c)
        {
            bool old = loading;
            loading = true;
            cbSkin.Items.Clear();
            cbSkin.Items.Add("Misturar todas as cores");
            foreach (var s in c.Skins) cbSkin.Items.Add(s.Name);
            int i = c.Skins.FindIndex(s => s.Id == cfg.Skin);
            cbSkin.SelectedIndex = cfg.Skin == "*" || i < 0 ? 0 : i + 1;
            crewPreview.SetCharacter(c, SelectedSkin());
            loading = old;
        }

        string SelectedSkin()
        {
            var c = cbChar.SelectedItem as Character;
            int i = cbSkin.SelectedIndex;
            return c == null || i <= 0 || i > c.Skins.Count ? "*" : c.Skins[i - 1].Id;
        }

        // Lê a tela, salva e atualiza o que depende disso (frases do spinner, prévia).
        void Changed()
        {
            if (loading) return;
            cfg.Enabled = enabled.Checked;
            if (!mix.Checked && cfg.Theme == "*") cfg.Theme = RoomTheme.All[0].Id;
            if (mix.Checked) cfg.Theme = "*";
            cfg.RoomSize = roomSize.Value;
            cfg.Corner = corner.SelectedIndex == 1 ? "esquerda" : "direita";
            cfg.DoneSeconds = (double)doneSecs.Value;
            cfg.ShowName = showName.Checked;
            cfg.RoomPermissions = permissions.Checked;
            cfg.Count = (int)count.Value;
            cfg.Size = (int)crewSize.Value;
            cfg.Speed = speed.Value / 10.0;
            cfg.Position = PosIds[Math.Max(0, position.SelectedIndex)];
            cfg.Bubbles = bubbles.Checked;
            cfg.TerminalPhrases = terminalPhrases.Checked;
            cfg.SoundDone = soundDone.Checked; cfg.SoundAttention = soundAttention.Checked; cfg.SoundFailure = soundFailure.Checked;
            try
            {
                cfg.Save();
                ClaudeSettings.ApplyPhrases(cfg);
            }
            catch (Exception ex) { Paths.Log("config: " + ex); SetStatus("Não deu para salvar: " + ex.Message); }
            var c = cbChar.SelectedItem as Character;
            if (c != null) crewPreview.SetCharacter(c, SelectedSkin());
            LoadValues();
        }

        void TestCrew()
        {
            try
            {
                var psi = new ProcessStartInfo(Paths.Exe, "demo 12") { UseShellExecute = true };
                Process.Start(psi);
                SetStatus("Olha a equipe em cima desta janela! (12 segundos)");
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        void UpdateHooks()
        {
            bool on = ClaudeSettings.HooksInstalled();
            hookStatus.Text = on ? "✔ Ligado ao Claude Code. Sessões abertas antes disso precisam ser reiniciadas."
                                 : "Ainda não está ligado ao Claude Code.";
            hookStatus.ForeColor = on ? Color.FromArgb(40, 120, 70) : Theme.Accent2;
            hookBtn.Text = on ? "Desligar" : "Ligar ao Claude";
        }

        void ToggleHooks()
        {
            try
            {
                if (ClaudeSettings.HooksInstalled()) ClaudeSettings.RemoveAll();
                else { ClaudeSettings.InstallHooks(); ClaudeSettings.ApplyPhrases(cfg); }
            }
            catch (Exception ex) { SetStatus("Não deu para mexer no settings.json: " + ex.Message); }
            UpdateHooks();
        }

        void UpdatePacks()
        {
            var premium = chars.Where(c => c.Premium).ToList();
            packStatus.Text = premium.Count == 0
                ? "Nenhum pacote instalado. O Supporters Pack traz personagens HD."
                : "★ " + string.Join(", ", premium.Select(c => c.Name).ToArray()) + " (Supporters Pack). Obrigado pelo apoio!";
        }

        // Um pacote é um .zip (ou .json soltos) com personagens; eles vão para a pasta pacotes\.
        void AddPack()
        {
            using (var d = new OpenFileDialog { Filter = "Pacotes do CmdCrew (*.zip;*.json)|*.zip;*.json", Multiselect = true, Title = "Adicionar pacote de personagens" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                int n = 0;
                try
                {
                    Directory.CreateDirectory(Paths.PacksDir);
                    foreach (var f in d.FileNames)
                    {
                        if (f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            using (var zip = ZipFile.OpenRead(f))
                                foreach (var e in zip.Entries.Where(e => e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                                {
                                    e.ExtractToFile(Path.Combine(Paths.PacksDir, e.Name), true);
                                    n++;
                                }
                        else { File.Copy(f, Path.Combine(Paths.PacksDir, Path.GetFileName(f)), true); n++; }
                    }
                }
                catch (Exception ex) { SetStatus("Não deu para instalar o pacote: " + ex.Message); return; }
                SetStatus(n + " personagem(ns) adicionado(s). Reabra esta tela para vê-los na lista.");
            }
        }

        void SetStatus(string s) { status.Text = s; }
    }

    // Prévia animada de um tema de sala: trabalha alguns segundos, termina, e recomeça.
    public class RoomPreview : Control
    {
        public readonly RoomTheme RoomTheme;
        public bool Selected;
        public bool UsesPremium;
        Room room;
        readonly Timer timer = new Timer();
        readonly Font font, small;
        readonly float unit;
        float cycle;

        public RoomPreview(RoomTheme theme, float unit)
        {
            RoomTheme = theme;
            this.unit = unit;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            font = new Font(Theme.PixelFamily, 6.2f * unit, FontStyle.Regular, GraphicsUnit.Pixel);
            small = new Font(Theme.PixelFamily, 5.4f * unit, FontStyle.Regular, GraphicsUnit.Pixel);
            var all = Character.LoadAll(null);
            var ch = all.FirstOrDefault(c => c.Id == theme.CharacterId) ?? all.FirstOrDefault(c => c.Id == theme.FallbackId);
            if (ch != null)
            {
                UsesPremium = ch.Premium;
                room = new Room(RoomTheme, ch, 0, 7) { ProjectName = "meu-projeto", Started = DateTime.UtcNow };
                room.SetScale(unit);
                room.Enter = 1;
            }
            Size = new Size((int)(RoomTheme.W * unit) + 16, (int)(RoomTheme.H * unit) + 16);
            timer.Interval = 33;
            timer.Tick += (s, e) =>
            {
                if (room == null || !Visible) return;
                cycle += 0.033f;
                if (cycle > 9 && room.State == RoomState.Working) room.SetState(RoomState.Done, DateTime.UtcNow);
                if (cycle > 14) { cycle = 0; room.Started = DateTime.UtcNow; room.SetState(RoomState.Working, DateTime.UtcNow); }
                room.Update(0.033f, true);
                Invalidate();
            };
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            if (Selected)
                using (var p = Theme.Round(new RectangleF(2, 2, Width - 5, Height - 5), 10))
                using (var pen = new Pen(Theme.Accent, 4)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.DrawPath(pen, p); }
            if (room != null) room.Draw(g, 8, 8, font, small);
            if (!Enabled)
                using (var b = new SolidBrush(Color.FromArgb(150, Parent != null ? Parent.BackColor : Theme.Bg))) g.FillRectangle(b, ClientRectangle);
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); font.Dispose(); small.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // Prévia animada da equipe: um andando, outro trabalhando.
    class PreviewBox : Control
    {
        readonly Timer timer = new Timer();
        readonly Dictionary<string, Bitmap[]> cache = new Dictionary<string, Bitmap[]>();
        Character ch;
        string skinId;
        int tick;
        float walkerX;

        public PreviewBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            timer.Interval = 110;
            timer.Tick += delegate { tick++; walkerX += 3; Invalidate(); };
            timer.Start();
        }

        public void SetCharacter(Character c, string skin)
        {
            if (c == ch && skin == skinId) return;
            ch = c;
            skinId = skin;
            foreach (var arr in cache.Values) foreach (var b in arr) b.Dispose();
            cache.Clear();
            Invalidate();
        }

        Bitmap[] Frames(Skin s, string kind, int scale, bool flip)
        {
            string key = s.Id + "|" + kind + "|" + scale + "|" + flip;
            Bitmap[] arr;
            if (!cache.TryGetValue(key, out arr)) { arr = ch.Render(s, kind, scale, flip); cache[key] = arr; }
            return arr;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = ClientRectangle;
            using (var bg = new LinearGradientBrush(r, Color.FromArgb(222, 238, 255), Color.FromArgb(255, 246, 228), 90f))
                g.FillRectangle(bg, r);
            int groundH = Math.Max(14, r.Height / 8);
            int ground = r.Bottom - groundH;
            using (var gb = new SolidBrush(Color.FromArgb(214, 190, 150))) g.FillRectangle(gb, 0, ground, r.Width, groundH);
            using (var gl = new SolidBrush(Color.FromArgb(160, 130, 90))) g.FillRectangle(gl, 0, ground, r.Width, 2);
            if (ch == null) return;

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            int rows = ch.BottomRow - ch.TopRow + 1;
            int scale = Math.Max(1, Math.Min((int)(r.Height * 0.6) / Math.Max(1, rows), (r.Width / 2 - 10) / Math.Max(1, ch.Width)));
            Skin skin = skinId == "*" ? ch.Skins[(tick / 18) % ch.Skins.Count] : ch.GetSkin(skinId);
            int top = ground - (ch.BottomRow + 1) * scale;
            if (ch.FlyHeight > 0) top -= (int)(Math.Min(ch.FlyHeight, 8) * scale + Math.Sin(tick * 0.35) * scale);   // quem voa

            // Andando da esquerda até o meio, em loop
            var walk = Frames(skin, "andar", scale, false);
            int sw = ch.Width * scale;
            float span = r.Width / 2f + sw;
            float x = (walkerX * scale / 3f) % span - sw;
            var wb = walk[(tick / 2) % walk.Length];
            int t = tick % 45, hop = t < 6 ? (int)(Math.Sin(Math.PI * t / 6) * rows * scale / 7) : 0;
            g.DrawImage(wb, new Rectangle((int)x, top - hop, wb.Width, wb.Height));

            // Trabalhando à direita (com pausas na pose parada)
            bool resting = (tick / 30) % 3 == 2;
            var work = Frames(skin, resting ? "parado" : "trabalhar", scale, true);
            var kb = work[(tick / 3) % work.Length];
            g.DrawImage(kb, new Rectangle(r.Width - sw - 12, top, kb.Width, kb.Height));
            if (!Enabled)
                using (var b = new SolidBrush(Color.FromArgb(150, Theme.Bg2))) g.FillRectangle(b, r);
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                foreach (var arr in cache.Values) foreach (var b in arr) b.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Editor das frases de um personagem (verbos e dicas do spinner, falas dos balões).
    class PhrasesForm : Form
    {
        static readonly string[] KindNames = {
            "Verbos do spinner (terminal)", "Dicas do spinner (terminal)", "Falas nos balões", "Falas de despedida" };
        readonly Config cfg;
        readonly Character ch;
        readonly Dictionary<string, List<string>> drafts = new Dictionary<string, List<string>>();
        readonly ComboBox kind;
        readonly TextBox text;
        string editing;

        public PhrasesForm(Config cfg, Character ch)
        {
            this.cfg = cfg;
            this.ch = ch;
            float k;
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Func<int, int> S = v => (int)(v * k);
            AutoScaleMode = AutoScaleMode.None;
            Text = "Frases: " + ch.Name;
            Font = Theme.Body(9.5f);
            BackColor = Theme.Bg;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(S(520), S(430));

            Controls.Add(new Label { Text = "Uma frase por linha.", Location = new Point(S(14), S(12)), Size = new Size(S(300), S(20)), ForeColor = Theme.Ink2 });
            kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(S(14), S(36)), Size = new Size(S(320), S(26)) };
            kind.Items.AddRange(KindNames);
            kind.SelectedIndexChanged += delegate { Commit(); Show(); };
            Controls.Add(kind);
            var restore = new Button { Text = "Restaurar padrão", Location = new Point(S(346), S(35)), Size = new Size(S(160), S(28)) };
            restore.Click += delegate { text.Text = string.Join("\r\n", ch.DefaultPhrases(Character.PhraseKinds[kind.SelectedIndex])); };
            Controls.Add(restore);
            text = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, Location = new Point(S(14), S(72)), Size = new Size(S(492), S(300)) };
            Controls.Add(text);
            var ok = new Button { Text = "Salvar", Location = new Point(S(310), S(386)), Size = new Size(S(96), S(32)), DialogResult = DialogResult.OK };
            ok.Click += delegate { Commit(); foreach (var kv in drafts) cfg.SetPhrases(ch, kv.Key, kv.Value); };
            Controls.Add(ok);
            var cancel = new Button { Text = "Cancelar", Location = new Point(S(412), S(386)), Size = new Size(S(96), S(32)), DialogResult = DialogResult.Cancel };
            Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
            Theme.Apply(this);
            kind.SelectedIndex = 0;
        }

        void Commit()
        {
            if (editing == null) return;
            drafts[editing] = text.Text.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        new void Show()
        {
            editing = Character.PhraseKinds[kind.SelectedIndex];
            List<string> l;
            if (!drafts.TryGetValue(editing, out l)) l = cfg.Phrases(ch, editing);
            text.Text = string.Join("\r\n", l);
        }
    }
}
