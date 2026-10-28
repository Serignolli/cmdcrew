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

        const string SfxForgeUrl = "https://github.com/Serignolli/sfx-forge";

        readonly Config cfg;
        List<Character> chars;
        RoomPreview roomPreview;
        ComboBox roomChar, roomSkin;
        Label roomDesc;
        readonly List<string> roomIds = new List<string>();
        readonly RadioButton[] modeRadios = new RadioButton[3];
        readonly float k;
        CheckBox enabled, showName, permissions, bubbles, terminalPhrases, soundDone, soundAttention, soundFailure;
        ComboBox corner, position, cbChar, cbSkin;
        TrackBar roomSize, speed;
        NumericUpDown doneSecs, count, crewSize;
        PreviewBox crewPreview;
        Label hookStatus, packStatus, status;
        readonly Dictionary<Agent, CheckBox> agentBoxes = new Dictionary<Agent, CheckBox>();
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
            StartPosition = FormStartPosition.Manual;   // posicionada em FitToScreen
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildUi();
            Theme.Apply(this);
            LoadValues();
            loading = false;
            FitToScreen();
        }

        // A tela é alta: em monitores baixos (ou com a escala do Windows alta) ela ganha barra de rolagem
        // em vez de esconder os botões de baixo atrás da barra de tarefas.
        void FitToScreen()
        {
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            if (Height > wa.Height)
            {
                AutoScroll = true;
                Size = new Size(Width + SystemInformation.VerticalScrollBarWidth, wa.Height);
            }
            Location = new Point(wa.Left + Math.Max(0, (wa.Width - Width) / 2), wa.Top + Math.Max(0, (wa.Height - Height) / 2));
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
            var sub = Lbl(this, "Uma equipe de bichinhos em pixel art que trabalha junto com o seu agente de IA e avisa quando ele termina.", 20, 52, 760);
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
            // As duas colunas seguem a mesma ordem: personagem, cores e a prévia logo abaixo.
            roomsPanel = Add(this, new Panel { BackColor = Theme.Bg2 }, 14, 118, 600, 470);
            Lbl(roomsPanel, "Salas no canto da tela", 12, 10, 400, 22, true);
            var hint = Lbl(roomsPanel, "Uma sala por terminal trabalhando. Duplo clique na sala leva ao terminal dela.", 12, 32, 576);
            hint.ForeColor = Theme.Ink2;
            // cada personagem tem a sua sala: escolher a sala escolhe quem trabalha nela
            Lbl(roomsPanel, "Sala", 12, 61, 90);
            roomChar = Add(roomsPanel, new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 100, 58, 488, 26);
            roomIds.Add(""); roomChar.Items.Add("A sala de quem está no terminal");
            foreach (var c in chars.OrderBy(c => RoomTheme.ForCharacter(c.Id).Name))
            {
                roomIds.Add(c.Id);
                roomChar.Items.Add(RoomTheme.ForCharacter(c.Id).Name + "  ·  " + c.Name + (c.Premium ? "  ★" : ""));
            }
            roomIds.Add("*"); roomChar.Items.Add("Alternar entre todas (uma por terminal)");
            roomChar.SelectedIndexChanged += delegate
            {
                if (loading) return;
                string id = roomIds[Math.Max(0, roomChar.SelectedIndex)];
                if (id != cfg.RoomCharacter) cfg.RoomSkin = "*";
                cfg.RoomCharacter = id;
                Changed();
            };
            Lbl(roomsPanel, "Cores", 12, 93, 90);
            roomSkin = Add(roomsPanel, new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 100, 90, 488, 26);
            roomSkin.SelectedIndexChanged += delegate { if (!loading && roomSkin.Enabled) { cfg.RoomSkin = SelectedRoomSkin(); Changed(); } };
            roomPreview = new RoomPreview(Math.Max(2f, 3f * k)) { Location = new Point(S(6), S(122)) };
            roomsPanel.Controls.Add(roomPreview);
            int px = (int)(roomPreview.Width / k) + 16;
            roomDesc = Lbl(roomsPanel, "", px, 130, 586 - px, 140);
            roomDesc.ForeColor = Theme.Ink2;
            int y = 122 + (int)(roomPreview.Height / k) + 10;
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
            var crewHint = Lbl(crewPanel, "Andam pela tela enquanto o agente trabalha.", 12, 32, 346);
            crewHint.ForeColor = Theme.Ink2;
            Lbl(crewPanel, "Personagem", 12, 61, 90);
            cbChar = Add(crewPanel, new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 100, 58, 258, 26);
            foreach (var c in chars) cbChar.Items.Add(c);
            cbChar.Format += (s, e) => { var c = e.ListItem as Character; if (c != null) e.Value = c.Name + (c.Premium ? "  ★" : ""); };
            cbChar.SelectedIndexChanged += delegate { OnCharacterChanged(); };
            Lbl(crewPanel, "Cores", 12, 93, 90);
            cbSkin = Add(crewPanel, new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 100, 90, 258, 26);
            cbSkin.SelectedIndexChanged += delegate { if (!loading) { cfg.Skin = SelectedSkin(); Changed(); } };
            crewPreview = Add(crewPanel, new PreviewBox(), 12, 128, 346, 130);
            Lbl(crewPanel, "Quantos", 12, 275, 70);
            count = Add(crewPanel, new NumericUpDown { Minimum = 1, Maximum = 15 }, 82, 272, 56, 26);
            count.ValueChanged += delegate { Changed(); };
            Lbl(crewPanel, "Tamanho", 160, 275, 70);
            crewSize = Add(crewPanel, new NumericUpDown { Minimum = 1, Maximum = 8 }, 230, 272, 56, 26);
            crewSize.ValueChanged += delegate { Changed(); };
            Lbl(crewPanel, "Velocidade", 12, 312, 80);
            speed = Add(crewPanel, new TrackBar { Minimum = 3, Maximum = 30, TickFrequency = 3, AutoSize = false }, 92, 304, 200, 36);
            speed.ValueChanged += delegate { Changed(); };
            position = Add(crewPanel, new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 12, 346, 346, 26);
            position.Items.AddRange(new object[] { "Em cima da janela do terminal", "No rodapé da tela", "No topo da tela" });
            position.SelectedIndexChanged += delegate { Changed(); };
            bubbles = Chk(crewPanel, "Balões de fala", 12, 382, 160);
            terminalPhrases = Chk(crewPanel, "Frases no spinner do terminal", 172, 382, 196);
            var edit = Add(crewPanel, new Button { Text = "Editar frases…" }, 12, 418, 150, 30);
            edit.Click += delegate
            {
                var c = cbChar.SelectedItem as Character;
                if (c == null) return;
                using (var f = new PhrasesForm(cfg, c)) if (f.ShowDialog(this) == DialogResult.OK) Changed();
            };
            var demo = Add(crewPanel, new Button { Text = "▶ Ver na tela" }, 172, 418, 150, 30);
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
            var credit = Add(this, new LinkLabel { Text = "feitos no SFX Forge" }, 630, by + 4, 180, 20);
            credit.LinkArea = new LinkArea(credit.Text.IndexOf("SFX"), "SFX Forge".Length);
            credit.ForeColor = Theme.Ink2;
            credit.LinkColor = credit.ActiveLinkColor = Theme.Accent;
            credit.Font = Theme.Body(8.5f);
            credit.LinkClicked += delegate { try { Process.Start(SfxForgeUrl); } catch (Exception ex) { SetStatus(ex.Message); } };

            // ----- pacotes -----
            by += 36;
            Lbl(this, "Pacotes", 20, by + 6, 70, 22, true);
            packStatus = Lbl(this, "", 96, by + 6, 520);
            packStatus.ForeColor = Theme.Ink2;
            var addPack = Add(this, new Button { Text = "Adicionar pacote…" }, 630, by, 170, 30);
            addPack.Click += delegate { AddPack(); };
            var openPacks = Add(this, new Button { Text = "Abrir a pasta" }, 810, by, 130, 30);
            openPacks.Click += delegate { Directory.CreateDirectory(Paths.PacksDir); Process.Start("explorer.exe", "\"" + Paths.PacksDir + "\""); };

            // ----- agentes -----
            by += 40;
            Lbl(this, "Agentes", 20, by + 3, 70, 22, true);
            int ax = 96;
            foreach (var a in Agents.All)
            {
                var agent = a;
                var cb = Add(this, new CheckBox { Text = a.Name }, ax, by, 148, 24);
                cb.CheckedChanged += delegate { if (!loading) ToggleAgent(agent, cb.Checked); };
                agentBoxes[a] = cb;
                ax += 150;
            }

            // ----- rodapé -----
            by += 40;
            hookStatus = Lbl(this, "", 20, by + 4, 600, 40);
            status = Lbl(this, "", 20, by + 46, 600);
            status.ForeColor = Color.FromArgb(40, 120, 70);
            var test = Add(this, new PixelButton { Text = "Testar salas" }, W - 364, by, 166, 44);
            test.Click += delegate { Program.Launch("demo-salas 3 10"); SetStatus("Olha o canto da tela!"); };
            // cada mudança já é gravada na hora; o botão grava de novo e confirma
            var save = Add(this, new PixelButton { Text = "Salvar", Primary = true }, W - 184, by, 166, 44);
            save.Click += delegate
            {
                Changed();
                if (!status.Text.StartsWith("Não deu")) SetStatus("✔ Configuração salva. Os terminais abertos já usam a nova.");
            };
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
            int ri = roomIds.IndexOf(cfg.RoomCharacter ?? "");
            if (ri < 0) ri = 0;
            if (roomChar.SelectedIndex != ri) roomChar.SelectedIndex = ri;
            FillRoomSkins();
            UpdateRoomPreview();
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

        // A prévia mostra a sala de quem vai trabalhar nela (alternando, passa por todos).
        void UpdateRoomPreview()
        {
            List<Character> show;
            if (cfg.RoomCharacter == "*") show = chars;
            else
            {
                var c = cfg.RoomCharacterFor(chars, 0);
                show = c == null ? new List<Character>() : new List<Character> { c };
            }
            roomPreview.SetCharacters(show, cfg);
            var first = show.FirstOrDefault();
            if (first == null) { roomDesc.Text = ""; return; }
            var theme = RoomTheme.ForCharacter(first.Id);
            roomDesc.Text = cfg.RoomCharacter == "*"
                ? "Cada terminal novo ganha o próximo personagem, cada um na sua sala."
                : theme.Name + (first.Premium ? "  ★ Supporters Pack" : "") + "\n" + theme.Description;
        }

        // Cores da sala: com um personagem escolhido só para ela, dá para escolher as dele; senão segue a equipe.
        void FillRoomSkins()
        {
            roomSkin.Items.Clear();
            var c = RoomOwnCharacter();
            if (c == null)
            {
                roomSkin.Items.Add(cfg.RoomCharacter == "*" ? "As cores de cada sala" : "As mesmas da equipe");
                roomSkin.SelectedIndex = 0;
                roomSkin.Enabled = false;
                return;
            }
            roomSkin.Enabled = true;
            roomSkin.Items.Add("Misturar todas as cores");
            foreach (var s in c.Skins) roomSkin.Items.Add(s.Name);
            int i = c.Skins.FindIndex(s => s.Id == cfg.RoomSkin);
            roomSkin.SelectedIndex = cfg.RoomSkin == "*" || i < 0 ? 0 : i + 1;
        }

        // O personagem escolhido só para a sala (null quando é o mesmo da equipe ou alterna entre todos).
        Character RoomOwnCharacter()
        {
            if (string.IsNullOrEmpty(cfg.RoomCharacter) || cfg.RoomCharacter == "*") return null;
            return chars.FirstOrDefault(c => c.Id == cfg.RoomCharacter);
        }

        string SelectedRoomSkin()
        {
            var c = RoomOwnCharacter();
            int i = roomSkin.SelectedIndex;
            return c == null || i <= 0 || i > c.Skins.Count ? "*" : c.Skins[i - 1].Id;
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
                if (ClaudeSettings.HooksInstalled()) ClaudeSettings.ApplyPhrases(cfg);
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
            var on = Agents.All.Where(a => { try { return a.Installed(); } catch { return false; } }).ToList();
            foreach (var kv in agentBoxes)
            {
                kv.Value.Checked = on.Contains(kv.Key);
                kv.Value.ForeColor = kv.Key.Detected || kv.Value.Checked ? Theme.Ink : Theme.Ink2;
            }
            hookStatus.Text = on.Count > 0
                ? "✔ A equipe acompanha: " + string.Join(", ", on.Select(a => a.Name).ToArray()) + ". Sessões abertas antes disso precisam ser reiniciadas."
                : "Nenhum agente ligado: marque pelo menos um acima.";
            hookStatus.ForeColor = on.Count > 0 ? Color.FromArgb(40, 120, 70) : Theme.Accent2;
        }

        void ToggleAgent(Agent a, bool on)
        {
            try
            {
                if (on) { a.Install(); if (a.Id == "claude") ClaudeSettings.ApplyPhrases(cfg); }
                else a.Remove();
                SetStatus(a.Name + (on ? " ligado." : " desligado."));
            }
            catch (Exception ex) { SetStatus("Não deu para mexer na configuração do " + a.Name + ": " + ex.Message); }
            bool old = loading;
            loading = true;
            UpdateHooks();
            loading = old;
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
                                foreach (var e in zip.Entries.Where(IsPackEntry))
                                {
                                    string dest = Path.Combine(Paths.PacksDir, e.Name);
                                    e.ExtractToFile(dest, true);
                                    if (KeepIfCharacter(dest)) n++;
                                }
                        else
                        {
                            string dest = Path.Combine(Paths.PacksDir, Path.GetFileName(f));
                            File.Copy(f, dest, true);
                            if (KeepIfCharacter(dest)) n++;
                        }
                    }
                }
                catch (Exception ex) { SetStatus("Não deu para instalar o pacote: " + ex.Message); return; }
                Rebuild();
                SetStatus(n == 0 ? "Nenhum personagem encontrado nesse arquivo." : n + " personagem(ns) adicionado(s). Obrigado pelo apoio!");
            }
        }

        // No .zip, só valem os personagens soltos na raiz ou numa pasta personagens\ (nada de arquivo\, rascunhos etc.).
        static bool IsPackEntry(ZipArchiveEntry e)
        {
            if (!e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return false;
            var parts = e.FullName.Replace('\\', '/').Split('/');
            return parts.Length == 1 || parts.Length >= 2 && parts[parts.Length - 2].Equals("personagens", StringComparison.OrdinalIgnoreCase);
        }

        // Um .json que não é personagem não fica na pasta de pacotes.
        static bool KeepIfCharacter(string path)
        {
            try { Character.Load(path); return true; }
            catch (Exception ex) { Paths.Log("pacote " + Path.GetFileName(path) + ": " + ex.Message); try { File.Delete(path); } catch { } return false; }
        }

        // Monta a tela de novo com a lista de personagens atualizada (depois de adicionar um pacote).
        void Rebuild()
        {
            loading = true;
            chars = Character.LoadAll(null);
            SuspendLayout();
            foreach (Control c in Controls.Cast<Control>().ToList()) c.Dispose();
            Controls.Clear();
            roomIds.Clear();
            agentBoxes.Clear();
            BuildUi();
            Theme.Apply(this);
            LoadValues();
            ResumeLayout();
            loading = false;
        }

        void SetStatus(string s) { status.Text = s; }
    }

    // Prévia animada da sala: trabalha alguns segundos, termina e recomeça (alternando, passa para o próximo personagem).
    public class RoomPreview : Control
    {
        Room room;
        List<Character> list = new List<Character>();
        string skin;
        Config cfg;
        int index;
        readonly Timer timer = new Timer();
        readonly Font font, small;
        readonly float unit;
        float cycle;

        public RoomPreview(float unit)
        {
            this.unit = unit;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            font = new Font(Theme.PixelFamily, 6.2f * unit, FontStyle.Regular, GraphicsUnit.Pixel);
            small = new Font(Theme.PixelFamily, 5.4f * unit, FontStyle.Regular, GraphicsUnit.Pixel);
            Size = new Size((int)(RoomTheme.W * unit) + 16, (int)(RoomTheme.H * unit) + 16);
            timer.Interval = 33;
            timer.Tick += (s, e) =>
            {
                if (room == null || !Visible) return;
                cycle += 0.033f;
                if (cycle > 9 && room.State == RoomState.Working) room.SetState(RoomState.Done, DateTime.UtcNow);
                if (cycle > 14)
                {
                    cycle = 0;
                    if (list.Count > 1) { index++; Build(); }
                    else { room.Started = DateTime.UtcNow; room.SetState(RoomState.Working, DateTime.UtcNow); }
                }
                room.Update(0.033f, true);
                Invalidate();
            };
            timer.Start();
        }

        public void SetCharacters(List<Character> chars, Config cfg)
        {
            this.cfg = cfg;
            if (room != null && chars.SequenceEqual(list) && skin == cfg.Skin) return;
            list = chars.ToList();
            skin = cfg.Skin;
            index = 0; cycle = 0;
            Build();
        }

        void Build()
        {
            if (list.Count == 0) { room = null; Invalidate(); return; }
            var ch = list[index % list.Count];
            room = new Room(RoomTheme.ForCharacter(ch.Id), ch, 0, 7, cfg == null ? null : cfg.RoomSkinFor(ch)) { ProjectName = "meu-projeto", Started = DateTime.UtcNow };
            room.SetScale(unit);
            room.Enter = 1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
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
            int rows = ch.FeetRow - ch.TopRow + 1;
            int scale = Math.Max(1, Math.Min((int)(r.Height * 0.6) / Math.Max(1, rows), (r.Width / 2 - 10) / Math.Max(1, ch.Width)));
            Skin skin = skinId == "*" ? ch.Skins[(tick / 18) % ch.Skins.Count] : ch.GetSkin(skinId);
            int top = ground - (ch.FeetRow + 1) * scale;
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
