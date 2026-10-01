using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace CmdCrew
{
    // A faixa no canto de baixo da tela, logo acima da barra de tarefas: uma sala por terminal trabalhando.
    public class StripForm : Form
    {
        readonly Timer timer = new Timer();
        readonly Stopwatch clock = new Stopwatch();
        readonly List<Room> rooms = new List<Room>();
        readonly Dictionary<string, Character> characters = new Dictionary<string, Character>();
        // salas que já saíram, com o "mudou" de quando saíram: só voltam se a sessão mudar de novo
        readonly Dictionary<string, DateTime> finished = new Dictionary<string, DateTime>();
        readonly Random rnd = new Random();
        Config cfg;
        DateTime cfgStamp;
        float unit;
        int gap;
        Font font, small;
        double lastT, nextPoll, emptyFor;
        int created;
        readonly Dictionary<string, RoomState> lastStates = new Dictionary<string, RoomState>();
        bool paintFailed;

        Point? anchor;          // canto de referência arrastado pelo usuário (null = canto padrão)
        Point downAt;
        Point downForm;
        bool dragging, mouseDown;

        public StripForm()
        {
            cfg = Config.Load();
            cfgStamp = Stamp(Paths.ConfigFile);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Text = "CmdCrew";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Bounds = new Rectangle(-32000, -32000, 10, 10);
            LoadAnchor();
            ApplyScale();
            ContextMenuStrip = BuildMenu();
            timer.Interval = 33;
            timer.Tick += OnTick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // layered + fora do Alt-Tab + nunca rouba o foco + sempre por cima
                cp.ExStyle |= 0x80000 | 0x80 | 0x08000000 | 0x8;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            clock.Start();
            Poll();
            timer.Start();
        }

        static DateTime Stamp(string f)
        {
            try { return File.Exists(f) ? File.GetLastWriteTimeUtc(f) : DateTime.MinValue; } catch { return DateTime.MinValue; }
        }

        void ApplyScale()
        {
            float dpi;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX;
            unit = Math.Max(1, (float)Math.Round(cfg.RoomSize * dpi / 96.0));
            gap = (int)(4 * dpi / 96.0);
            if (font != null) font.Dispose();
            if (small != null) small.Dispose();
            font = new Font(Theme.PixelFamily, 6.2f * unit, FontStyle.Regular, GraphicsUnit.Pixel);
            small = new Font(Theme.PixelFamily, 5.4f * unit, FontStyle.Regular, GraphicsUnit.Pixel);
            foreach (var r in rooms) r.SetScale(unit);
        }

        ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            m.Items.Add("Configurar…", null, (s, e) => Program.Launch("config"));
            m.Items.Add("Voltar ao canto", null, (s, e) => { anchor = null; SaveAnchor(); });
            m.Items.Add("Dispensar as salas prontas", null, (s, e) => { foreach (var r in rooms) if (r.State != RoomState.Working && r.State != RoomState.Waiting) r.Dismissed = true; });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Fechar", null, (s, e) => Close());
            return m;
        }

        // ---------- sessões ----------

        // O personagem do tema; se ele for de um pacote que não está instalado, o substituto gratuito.
        Character CharacterFor(RoomTheme theme)
        {
            Character c;
            if (characters.TryGetValue(theme.Id, out c)) return c;
            var all = Character.LoadAll(null);
            c = all.FirstOrDefault(x => x.Id == theme.CharacterId) ?? all.FirstOrDefault(x => x.Id == theme.FallbackId) ?? all.FirstOrDefault();
            if (c == null) throw new InvalidOperationException("Nenhum personagem em " + Paths.CharactersDir);
            characters[theme.Id] = c;
            return c;
        }

        RoomTheme NextTheme()
        {
            if (cfg.Theme == "*") return RoomTheme.All[created % RoomTheme.All.Count];
            return RoomTheme.Find(cfg.Theme);
        }

        void Poll()
        {
            var stamp = Stamp(Paths.ConfigFile);
            if (stamp != cfgStamp)
            {
                cfgStamp = stamp;
                int oldSize = cfg.RoomSize;
                cfg = Config.Load();
                if (cfg.RoomSize != oldSize) ApplyScale();
            }

            var sessions = Sessions.LoadAll();
            var ids = new HashSet<string>(sessions.Select(s => s.Id));
            foreach (var s in sessions)
            {
                var st = s.Resolve(cfg.IdleMinutes);
                var room = rooms.FirstOrDefault(r => r.SessionId == s.Id && !r.Leaving);
                if (room == null)
                {
                    DateTime seen;
                    if (finished.TryGetValue(s.Id, out seen) && seen == s.Changed) continue;
                    bool recent = (DateTime.UtcNow - s.Changed).TotalSeconds < cfg.DoneSeconds;
                    bool wanted = st == RoomState.Working || st == RoomState.Waiting || ((st == RoomState.Done || st == RoomState.Failed) && recent);
                    if (!wanted) continue;
                    // salas de mesmo tema ganham personagens diferentes, para não confundir os terminais
                    var theme = NextTheme();
                    int variant = rooms.Count(r => r.Theme == theme && !r.Leaving);
                    room = new Room(theme, CharacterFor(theme), variant, rnd.Next());
                    room.SessionId = s.Id;
                    room.SetScale(unit);
                    rooms.Add(room);
                    created++;
                }
                room.ProjectName = cfg.ShowName ? s.ProjectName : "";
                room.Terminal = new IntPtr(s.Hwnd);
                room.Started = s.Started;
                room.Ask = s.Ask;
                room.SetState(st, s.Changed);
                PlayFor(s.Id, st);
            }
            foreach (var r in rooms)
                if (!r.Leaving && (!ids.Contains(r.SessionId) || r.ShouldLeave(cfg.DoneSeconds)))
                {
                    r.Leaving = true;
                    finished[r.SessionId] = r.Changed;
                }
        }

        // Toca o som quando a sala entra em "pronto", "precisa de você" ou "falha".
        void PlayFor(string id, RoomState st)
        {
            RoomState old;
            bool had = lastStates.TryGetValue(id, out old);
            lastStates[id] = st;
            if (had && old == st) return;
            if (st == RoomState.Done && had && cfg.SoundDone) Sounds.Play("pronto");
            else if (st == RoomState.Waiting && cfg.SoundAttention) Sounds.Play("atencao");
            else if (st == RoomState.Failed && cfg.SoundFailure) Sounds.Play("falha");
        }

        // ---------- ciclo ----------

        void OnTick(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.1, now - lastT);
            lastT = now;
            if (now >= nextPoll) { nextPoll = now + 0.4; Poll(); }

            foreach (var r in rooms) r.Update(dt, cfg.Bubbles);
            rooms.RemoveAll(r => r.Gone);

            if (rooms.Count == 0)
            {
                emptyFor += dt;
                if (emptyFor > 1.5) { timer.Stop(); Close(); return; }
            }
            else emptyFor = 0;

            LayoutRooms(dt);
            if (Visible) Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            Invalidate();
        }

        int RoomW { get { return (int)(RoomTheme.W * unit); } }
        int RoomH { get { return (int)(RoomTheme.H * unit); } }

        bool GrowsLeft { get { return cfg.Corner != "esquerda"; } }

        void LayoutRooms(float dt)
        {
            // posição de cada sala dentro da faixa; as salas deslizam quando uma sai
            var order = rooms.OrderBy(r => r.Started).ToList();
            int n = Math.Max(1, order.Count);
            int width = n * RoomW + (n - 1) * gap;
            for (int i = 0; i < order.Count; i++)
            {
                // a mais antiga fica no canto; as novas vão aparecendo para dentro da tela
                float target = GrowsLeft ? width - (i + 1) * RoomW - i * gap : i * (RoomW + gap);
                var r = order[i];
                if (float.IsNaN(r.SlotX)) r.SlotX = target;
                else r.SlotX += (target - r.SlotX) * Math.Min(1, dt * 10);
            }

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Point a = anchor ?? new Point(GrowsLeft ? wa.Right - gap * 2 : wa.Left + gap * 2, wa.Bottom - gap);
            if (dragging) return;
            var b = new Rectangle(GrowsLeft ? a.X - width : a.X, a.Y - RoomH, width, RoomH);
            if (Bounds != b)
            {
                // se a faixa cresce para a esquerda, as salas precisam ser deslocadas junto
                int dx = Bounds.Width > 0 && GrowsLeft && Left > -30000 ? b.Width - Bounds.Width : 0;
                if (dx != 0) foreach (var r in rooms) r.SlotX += dx;
                Bounds = b;
            }
            if (!Visible) Show();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.Magenta);
            foreach (var r in rooms.OrderBy(x => x.Started))
            {
                if (float.IsNaN(r.SlotX)) continue;   // ainda sem lugar na faixa
                try { r.Draw(g, (float)Math.Round(r.SlotX), 0, font, small); }
                catch (Exception ex) { if (!paintFailed) Paths.Log("desenho: " + ex); paintFailed = true; }
            }
        }

        // ---------- mouse: clique leva ao terminal, arrastar move a faixa ----------

        Room RoomAt(Point p)
        {
            foreach (var r in rooms)
                if (p.X >= r.SlotX && p.X < r.SlotX + RoomW && p.Y >= 0 && p.Y < RoomH) return r;
            return null;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            mouseDown = true; dragging = false;
            downAt = Cursor.Position; downForm = Location;
        }

        // Botão do painel de permissão sob o ponto (sala e índice), ou -1.
        int AskButtonAt(Point p, out Room room)
        {
            room = RoomAt(p);
            if (room == null || room.Ask == null) return -1;
            for (int i = 0; i < room.AskButtons.Length; i++)
                if (room.AskButtons[i].Contains(p.X - room.SlotX, p.Y)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Room over;
            int btn = AskButtonAt(e.Location, out over);
            foreach (var r in rooms) r.AskHover = r == over ? btn : -1;
            Cursor = btn >= 0 ? Cursors.Hand : Cursors.Default;
            if (!mouseDown) return;
            var c = Cursor.Position;
            if (!dragging && (Math.Abs(c.X - downAt.X) > 4 || Math.Abs(c.Y - downAt.Y) > 4)) dragging = true;
            if (dragging) Location = new Point(downForm.X + c.X - downAt.X, downForm.Y + c.Y - downAt.Y);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            foreach (var r in rooms) r.AskHover = -1;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !mouseDown) return;
            mouseDown = false;
            if (dragging)
            {
                dragging = false;
                anchor = new Point(GrowsLeft ? Right : Left, Bottom);
                SaveAnchor();
                return;
            }
            // um clique só responde aos botões de permissão; ir ao terminal pede duplo clique
            Room room;
            int btn = AskButtonAt(e.Location, out room);
            if (btn < 0) return;
            Sessions.Answer(room.Ask.Id, btn == 0 ? "allow" : btn == 1 ? "deny" : "terminal");
            room.Ask = null;
            if (btn == 2) Native.Focus(room.Terminal);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            Room room;
            if (AskButtonAt(e.Location, out room) >= 0) return;
            room = RoomAt(e.Location);
            if (room == null) return;
            // com um pedido aberto, devolve a decisão para o terminal antes de ir até ele
            if (room.Ask != null) { Sessions.Answer(room.Ask.Id, "terminal"); room.Ask = null; }
            Native.Focus(room.Terminal);
            if (room.State != RoomState.Working && room.State != RoomState.Waiting) room.Dismissed = true;
        }

        void LoadAnchor()
        {
            try
            {
                if (!File.Exists(Paths.PositionFile)) return;
                var parts = File.ReadAllText(Paths.PositionFile).Trim().Split(',');
                int x, y;
                if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                {
                    // só vale se ainda estiver dentro de alguma tela
                    if (Screen.AllScreens.Any(s => s.Bounds.Contains(new Point(x - 1, y - 1)))) anchor = new Point(x, y);
                }
            }
            catch { }
        }

        void SaveAnchor()
        {
            try
            {
                Directory.CreateDirectory(Paths.StateDir);
                if (anchor == null) File.Delete(Paths.PositionFile);
                else File.WriteAllText(Paths.PositionFile, anchor.Value.X.ToString(CultureInfo.InvariantCulture) + "," + anchor.Value.Y.ToString(CultureInfo.InvariantCulture));
            }
            catch { }
        }
    }

    // Sons embutidos no .exe (src/recursos/sons), feitos no SFX Forge.
    public static class Sounds
    {
        static readonly Dictionary<string, SoundPlayer> players = new Dictionary<string, SoundPlayer>();

        public static void Play(string name)
        {
            try
            {
                SoundPlayer p;
                if (!players.TryGetValue(name, out p))
                {
                    var stream = typeof(Sounds).Assembly.GetManifestResourceStream("sons/" + name + ".wav");
                    if (stream == null) return;
                    p = new SoundPlayer(stream);
                    p.Load();
                    players[name] = p;
                }
                p.Play();
            }
            catch (Exception ex) { Paths.Log("som " + name + ": " + ex.Message); }
        }
    }
}
