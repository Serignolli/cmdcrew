using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CmdCrew
{
    class Worker
    {
        public float X = float.NaN, Target, Timer, FrameTimer, Delay, SpeedMul, BubbleTimer;
        public float Y, VY, Squash, MeetCooldown, CheerTimer;   // Y: altura do pulo (para cima)
        public float IdleHold = 0.8f;
        public float Phase;   // defasagem do sobe e desce de quem voa
        public int Dir = 1, State, Frame, TopRow;
        public bool Carrying, Gone, Looked;
        public Bitmap[] WalkR, WalkL, WorkR, WorkL, IdleR, IdleL;
        public Bitmap[][] WorksR, WorksL;   // um conjunto por tipo de trabalho (regar, cavar...)
        public string[] WorkFx;
        public Bitmap CarryR, CarryL;
        public string Bubble, Effect;
    }

    class Particle
    {
        public float X, Y, VX, VY, Life, MaxLife, Gravity, Grow;
        public Color Color;
        public string Text;
        public int Size;
        public bool Bug;
    }

    public class OverlayForm : Form
    {
        const int WALK = 0, WORK = 1, IDLE = 2, JUMP = 3;

        readonly Config cfg;
        readonly Character ch;
        readonly bool sessionMode;
        readonly double demoSeconds;
        readonly List<Worker> workers = new List<Worker>();
        readonly List<Particle> parts = new List<Particle>();
        readonly Random rnd = new Random();
        readonly Stopwatch clock = new Stopwatch();
        readonly Timer timer;
        readonly int scale, px, spriteW, formH;   // scale: acessórios/partículas; px: pontos do personagem
        readonly float gravity;
        readonly Font bubbleFont, symbolFont;
        readonly List<string> speech, endSpeech;
        readonly Dictionary<Color, SolidBrush> brushes = new Dictionary<Color, SolidBrush>();

        public IntPtr FixedTarget = IntPtr.Zero;   // usado na demonstração
        IntPtr target = IntPtr.Zero, owner = IntPtr.Zero;
        DateTime targetStamp = DateTime.MinValue;
        double lastT, elapsed, nextCheck, nextZ, nextBubble = 2.5, ending = -1;

        public OverlayForm(Config cfg, bool sessionMode, double demoSeconds)
        {
            this.cfg = cfg;
            this.sessionMode = sessionMode;
            this.demoSeconds = demoSeconds;

            var errors = new List<string>();
            ch = Character.Find(Character.LoadAll(errors), cfg.Character);
            foreach (var e in errors) Paths.Log("personagem com erro: " + e);
            if (ch == null) throw new InvalidOperationException("Nenhum personagem encontrado em " + Paths.CharactersDir);

            float dpi;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX;
            scale = Math.Max(1, (int)Math.Round(Math.Max(1, cfg.Size) * dpi / 96.0));
            px = ch.PixelScale(scale);
            spriteW = ch.Width * px;
            gravity = 900f * scale / 3f;
            // altura do desenho + objeto carregado + espaço para os pulos + balão
            formH = (ch.BottomRow - ch.TopRow + 1) * px + 7 * scale + 6 * scale + (int)(40 * dpi / 96.0)
                  + (ch.FlyHeight > 0 ? ch.FlyHeight * px + 2 * scale : 0);   // espaço para quem voa

            speech = cfg.Phrases(ch, "falas");
            endSpeech = cfg.Phrases(ch, "falasFim");
            bubbleFont = new Font("Segoe UI", 9f, FontStyle.Regular);
            symbolFont = new Font("Segoe UI Symbol", 6f + Math.Max(1, cfg.Size) * 1.6f, FontStyle.Bold);

            var cache = new Dictionary<string, Bitmap[][]>();
            var carries = new Dictionary<string, Bitmap[]>();
            int n = Math.Max(1, Math.Min(20, cfg.Count));
            // Misturando skins, cada um começa com uma diferente antes de repetir.
            var order = ch.Skins.OrderBy(x => rnd.Next()).ToList();
            for (int i = 0; i < n; i++)
            {
                Skin skin = cfg.Skin == "*" ? order[i % order.Count] : ch.GetSkin(cfg.Skin);
                Bitmap[][] set;
                var workKinds = ch.WorkKinds(skin);
                if (!cache.TryGetValue(skin.Id, out set))
                {
                    // andar R/L, parado R/L e, para cada tipo de trabalho, R/L
                    var l = new List<Bitmap[]> {
                        ch.Render(skin, "andar", px, false), ch.Render(skin, "andar", px, true),
                        ch.Render(skin, "parado", px, false), ch.Render(skin, "parado", px, true) };
                    foreach (var k in workKinds) { l.Add(ch.Render(skin, k, px, false)); l.Add(ch.Render(skin, k, px, true)); }
                    set = l.ToArray();
                    cache[skin.Id] = set;
                }
                string carryName = ch.CarryOf(skin) ?? "";
                Bitmap[] carry;
                if (!carries.TryGetValue(carryName, out carry))
                {
                    var prop = Prop.Get(carryName);
                    carry = prop == null ? new Bitmap[2]
                        : new[] { PixelArt.Render(prop.Rows, prop.Colors, scale, false), PixelArt.Render(prop.Rows, prop.Colors, scale, true) };
                    carries[carryName] = carry;
                }
                var w = new Worker();
                w.WalkR = set[0]; w.WalkL = set[1]; w.IdleR = set[2]; w.IdleL = set[3];
                int nw = workKinds.Count;
                w.WorksR = new Bitmap[nw][]; w.WorksL = new Bitmap[nw][]; w.WorkFx = new string[nw];
                for (int k = 0; k < nw; k++)
                {
                    w.WorksR[k] = set[4 + 2 * k]; w.WorksL[k] = set[5 + 2 * k];
                    w.WorkFx[k] = ch.EffectOf(skin, workKinds[k]);
                }
                PickWork(w);
                w.CarryR = carry[0]; w.CarryL = carry[1];
                w.TopRow = skin.TopRow;
                w.SpeedMul = 0.8f + (float)rnd.NextDouble() * 0.4f;
                w.Delay = i * 0.6f + (float)rnd.NextDouble() * 0.4f;
                w.Dir = i % 2 == 0 ? 1 : -1;
                w.State = WALK;
                w.MeetCooldown = 2f;
                w.Phase = (float)(rnd.NextDouble() * Math.PI * 2);
                workers.Add(w);
            }

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = cfg.Position != "terminal";
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Text = "CmdCrew";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Bounds = new Rectangle(-32000, -32000, 10, 10);

            timer = new Timer();
            timer.Interval = 30;
            timer.Tick += OnTick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // layered + transparente a cliques + fora do Alt-Tab + nunca rouba o foco
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08000000;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ReadTarget();
            Reposition();
            clock.Start();
            timer.Start();
        }

        // ---------- posição ----------

        void ReadTarget()
        {
            if (FixedTarget != IntPtr.Zero) { target = FixedTarget; return; }
            try
            {
                if (!File.Exists(Paths.TargetFile)) return;
                var stamp = File.GetLastWriteTimeUtc(Paths.TargetFile);
                if (stamp == targetStamp) return;
                targetStamp = stamp;
                long v;
                if (long.TryParse(File.ReadAllText(Paths.TargetFile).Trim(), out v)) target = new IntPtr(v);
            }
            catch { }
        }

        void Reposition()
        {
            bool show = true;
            bool haveTarget = target != IntPtr.Zero && Native.IsWindow(target);
            Rectangle b;
            if (cfg.Position == "terminal" && haveTarget)
            {
                if (Native.IsIconic(target) || !Native.IsWindowVisible(target)) show = false;
                Rectangle wr = Native.WindowBounds(target);
                Rectangle sb = Screen.FromRectangle(wr).Bounds;
                if (wr.Top - formH >= sb.Top)
                    b = new Rectangle(wr.Left, wr.Top - formH + scale, wr.Width, formH);  // em pé no topo da janela
                else
                    b = new Rectangle(wr.Left, wr.Bottom - formH, wr.Width, formH);       // janela maximizada: na borda de baixo
            }
            else
            {
                Screen s = haveTarget ? Screen.FromHandle(target) : Screen.PrimaryScreen;
                Rectangle wa = s.WorkingArea;
                b = cfg.Position == "topo"
                    ? new Rectangle(wa.Left, wa.Top, wa.Width, formH)
                    : new Rectangle(wa.Left, wa.Bottom - formH, wa.Width, formH);
            }
            if (b.Width < spriteW * 2) b.Width = spriteW * 2;

            if (Bounds != b)
            {
                Bounds = b;
                foreach (var w in workers)
                {
                    if (float.IsNaN(w.X)) continue;
                    w.X = Math.Max(-spriteW, Math.Min(Width + spriteW, w.X));
                    if (w.Target > Width) w.Target = RandomTarget();
                }
            }
            if (show && !Visible) Show();
            else if (!show && Visible) Hide();
        }

        void FixZOrder()
        {
            if (cfg.Position == "terminal")
            {
                // Pendurados no terminal: acima dele, mas outras janelas os cobrem normalmente.
                IntPtr want = target != IntPtr.Zero && Native.IsWindow(target) ? target : IntPtr.Zero;
                if (want != owner)
                {
                    owner = want;
                    Native.SetOwner(Handle, owner);
                    // (a propriedade TopMost do WinForms ativaria a janela e roubaria o foco do terminal)
                    Native.SetWindowPos(Handle, owner == IntPtr.Zero ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                }
            }
            else if (Visible)
            {
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }
        }

        // ---------- ciclo ----------

        void OnTick(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.1, now - lastT);
            lastT = now;
            elapsed += dt;

            if (now >= nextCheck)
            {
                nextCheck = now + 0.4;
                ReadTarget();
                CheckStillWorking();
            }
            if (now >= nextZ) { nextZ = now + 0.25; FixZOrder(); }
            Reposition();

            if (ending >= 0)
            {
                ending += dt;
                const double hold = 2.4, fade = 0.9;
                if (ending > hold)
                {
                    double t = (ending - hold) / fade;
                    if (t >= 1) { timer.Stop(); Close(); return; }
                    Opacity = Math.Max(0.01, 1 - t);
                }
            }

            foreach (var w in workers) UpdateWorker(w, dt);
            if (ending < 0) CheckMeetings();
            UpdateParticles(dt);
            UpdateBubbles(dt);
            Invalidate();
        }

        void CheckStillWorking()
        {
            bool active = sessionMode ? Sessions.AnyActive(cfg.IdleMinutes) : elapsed < demoSeconds;
            if (!active && ending < 0) BeginEnding();
            else if (active && ending >= 0) CancelEnding();
        }

        void BeginEnding()
        {
            ending = 0;
            var visible = workers.Where(w => w.Delay <= 0 && !float.IsNaN(w.X)).ToList();
            foreach (var w in workers)
            {
                if (!visible.Contains(w)) { w.Gone = true; continue; }
                // Comemoram com pulinhos antes de sumir.
                w.State = IDLE; w.Timer = 999; w.Carrying = false; w.Frame = 0;
                w.CheerTimer = (float)rnd.NextDouble() * 0.5f;
                w.Bubble = null;
            }
            if (cfg.Bubbles && endSpeech.Count > 0)
            {
                foreach (var w in visible.OrderBy(x => rnd.Next()).Take(Math.Min(3, (visible.Count + 1) / 2)))
                {
                    w.Bubble = endSpeech[rnd.Next(endSpeech.Count)];
                    w.BubbleTimer = 3f;
                }
            }
        }

        void CancelEnding()
        {
            ending = -1;
            Opacity = 1;
            foreach (var w in workers)
            {
                w.Bubble = null;
                if (w.Gone) { w.Gone = false; w.X = float.NaN; w.Delay = (float)rnd.NextDouble() * 1.5f; w.State = WALK; }
                else Decide(w);
            }
        }

        float RandomTarget()
        {
            float margin = spriteW * 0.75f;
            float span = Math.Max(1, Width - 2 * margin);
            return margin + (float)rnd.NextDouble() * span;
        }

        void Decide(Worker w)
        {
            double r = rnd.NextDouble();
            w.Frame = 0; w.FrameTimer = 0; w.Looked = false;
            if (r < 0.46)
            {
                w.State = WORK;
                PickWork(w);
                w.Timer = 2.5f + (float)rnd.NextDouble() * 4f;
                if (w.Carrying) { w.Carrying = false; Spawn("poeira", w, 3); }
            }
            else if (r < 0.8)
            {
                w.State = WALK;
                w.Target = RandomTarget();
                w.Carrying = w.CarryR != null && rnd.NextDouble() < 0.45;
            }
            else if (r < 0.88 && w.Y <= 0 && ch.FlyHeight == 0)
            {
                // pulinho de alegria no lugar
                w.State = JUMP;
                Launch(w, 5 + (float)rnd.NextDouble() * 3);
            }
            else
            {
                w.State = IDLE;
                w.Timer = 0.9f + (float)rnd.NextDouble() * 1.6f;
                if (rnd.NextDouble() < 0.5) w.Dir = -w.Dir;
            }
        }

        // Sorteia qual trabalho fazer (quando o personagem tem mais de um).
        void PickWork(Worker w)
        {
            int k = rnd.Next(w.WorksR.Length);
            w.WorkR = w.WorksR[k]; w.WorkL = w.WorksL[k]; w.Effect = w.WorkFx[k];
        }

        // Pulo de "height" pontos de altura.
        void Launch(Worker w, float height)
        {
            if (w.Y > 0) return;
            w.VY = (float)Math.Sqrt(2 * gravity * height * scale);
            w.Y = 0.01f;
        }

        void UpdateWorker(Worker w, float dt)
        {
            if (w.Gone) return;
            if (w.Delay > 0) { w.Delay -= dt; return; }
            if (float.IsNaN(w.X))
            {
                w.X = w.Dir > 0 ? -spriteW : Width + spriteW;
                w.Target = RandomTarget();
                w.State = WALK;
            }
            if (w.BubbleTimer > 0) { w.BubbleTimer -= dt; if (w.BubbleTimer <= 0) w.Bubble = null; }
            if (w.Squash > 0) w.Squash -= dt;
            if (w.MeetCooldown > 0) w.MeetCooldown -= dt;
            w.FrameTimer += dt;

            // física do pulo
            if (w.Y > 0)
            {
                w.VY -= gravity * dt;
                w.Y += w.VY * dt;
                if (w.Y <= 0)
                {
                    w.Y = 0; w.VY = 0; w.Squash = 0.12f;
                    Spawn("poeira", w, 2);
                    if (w.State == JUMP) Decide(w);
                }
            }

            float speed = (float)(34.0 * scale / 3.0 * cfg.Speed) * w.SpeedMul;
            switch (w.State)
            {
                case WALK:
                    float dx = w.Target - w.X;
                    if (Math.Abs(dx) <= speed * dt) { w.X = w.Target; Decide(w); break; }
                    w.Dir = dx > 0 ? 1 : -1;
                    w.X += w.Dir * speed * dt;
                    // desenhos com mais quadros trocam mais rápido, para o passo continuar do mesmo tamanho
                    float step = (w.WalkR.Length >= 8 ? 0.095f : w.WalkR.Length >= 4 ? 0.13f : 0.18f) / (float)cfg.Speed;
                    if (w.FrameTimer > step)
                    {
                        w.FrameTimer = 0;
                        w.Frame = (w.Frame + 1) % w.WalkR.Length;
                    }
                    break;
                case WORK:
                    w.Timer -= dt;
                    // animações quadro a quadro (golpe de picareta etc.) andam mais rápido que as de 2 quadros
                    if (w.FrameTimer > (w.WorkR.Length >= 4 ? 0.11f : 0.3f) / Math.Max(0.5, cfg.Speed))
                    {
                        w.FrameTimer = 0;
                        w.Frame = (w.Frame + 1) % w.WorkR.Length;
                        if (w.Frame == w.WorkR.Length - 1) Spawn(w.Effect, w, 0);
                    }
                    if (w.Timer <= 0) Decide(w);
                    break;
                case JUMP:
                    break;
                default:
                    if (ending >= 0)
                    {
                        w.CheerTimer -= dt;
                        if (w.CheerTimer <= 0 && w.Y <= 0 && ending < 2.0)
                        {
                            Launch(w, 4 + (float)rnd.NextDouble() * 3);
                            w.CheerTimer = 0.15f + (float)rnd.NextDouble() * 0.35f;
                            if (rnd.NextDouble() < 0.3) w.Dir = -w.Dir;
                        }
                        break;
                    }
                    w.Timer -= dt;
                    AnimateIdle(w);
                    // olha para o outro lado no meio da pausa
                    if (!w.Looked && w.Timer < 0.6f && rnd.NextDouble() < 0.5) { w.Dir = -w.Dir; w.Looked = true; }
                    if (w.Timer <= 0) Decide(w);
                    break;
            }
        }

        // "parado" com vários quadros: o primeiro fica na tela um tempo (respirando) e os outros
        // passam rápido (uma piscada), depois volta ao primeiro.
        void AnimateIdle(Worker w)
        {
            if (w.IdleR.Length < 2) { w.Frame = 0; return; }
            float hold = w.Frame == 0 ? w.IdleHold : 0.09f;
            if (w.FrameTimer < hold) return;
            w.FrameTimer = 0;
            w.Frame = (w.Frame + 1) % w.IdleR.Length;
            if (w.Frame == 0) w.IdleHold = 1.2f + (float)rnd.NextDouble() * 2.2f;
        }

        // Quando dois se cruzam andando, às vezes param, se olham e se cumprimentam.
        void CheckMeetings()
        {
            for (int i = 0; i < workers.Count; i++)
            {
                var a = workers[i];
                if (a.State != WALK || a.Gone || a.Delay > 0 || float.IsNaN(a.X) || a.MeetCooldown > 0) continue;
                for (int j = i + 1; j < workers.Count; j++)
                {
                    var b = workers[j];
                    if (b.State != WALK || b.Gone || b.Delay > 0 || float.IsNaN(b.X) || b.MeetCooldown > 0) continue;
                    if (a.Dir == b.Dir) continue;
                    float gap = b.X - a.X;
                    if (Math.Abs(gap) > spriteW * 0.9f || Math.Abs(gap) < spriteW * 0.5f) continue;
                    if ((gap > 0) != (a.Dir > 0)) continue;   // só quando estão indo um na direção do outro
                    a.MeetCooldown = b.MeetCooldown = 10f;
                    if (rnd.NextDouble() > 0.45) continue;
                    foreach (var w in new[] { a, b })
                    {
                        w.State = IDLE; w.Timer = 1.3f + (float)rnd.NextDouble() * 0.5f; w.Frame = 0; w.Looked = true;
                    }
                    a.Dir = gap > 0 ? 1 : -1; b.Dir = -a.Dir;
                    var hopper = rnd.Next(2) == 0 ? a : b;
                    Launch(hopper, 3);
                    Add((a.X + b.X) / 2 - scale * 2, HeadTopOf(a) - scale * 2, R(-6, 6), -22, 0, 1.3f,
                        new[] { Color.FromArgb(236, 64, 122), Color.FromArgb(255, 202, 40) }[rnd.Next(2)], rnd.Next(3) == 0 ? "!" : "♥", 0);
                    break;
                }
            }
        }

        void UpdateBubbles(float dt)
        {
            if (!cfg.Bubbles || ending >= 0 || speech.Count == 0) return;
            nextBubble -= dt;
            if (nextBubble > 0) return;
            var candidates = workers.Where(w => !w.Gone && w.Delay <= 0 && !float.IsNaN(w.X) && w.Bubble == null
                                                && w.X > 0 && w.X < Width).ToList();
            if (candidates.Count > 0)
            {
                var w = candidates[rnd.Next(candidates.Count)];
                w.Bubble = speech[rnd.Next(speech.Count)];
                w.BubbleTimer = 3.2f;
            }
            nextBubble = 5 + rnd.NextDouble() * 8;
        }

        // ---------- partículas ----------

        int Ground { get { return ClientSize.Height; } }
        int SpriteTop { get { return Ground - (ch.BottomRow + 1) * px; } }
        int HeadTopOf(Worker w) { return SpriteTop + w.TopRow * px - (int)w.Y - Fly(w); }

        // Altura de quem voa: fixa + um sobe e desce suave (bater de asas).
        int Fly(Worker w)
        {
            if (ch.FlyHeight == 0) return 0;
            return ch.FlyHeight * px + (int)Math.Round(Math.Sin(elapsed * 3.2 + w.Phase) * scale * 1.3);
        }

        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        Color Pick(params Color[] c) { return c[rnd.Next(c.Length)]; }
        string Pick(params string[] s) { return s[rnd.Next(s.Length)]; }

        void Spawn(string effect, Worker w, int count)
        {
            float frontX = w.X + w.Dir * spriteW * 0.45f;
            float headY = HeadTopOf(w);
            float midY = Ground - (ch.BottomRow - w.TopRow) * px * 0.3f - Fly(w);
            float headX = w.X + w.Dir * spriteW * 0.2f;
            // "faiscas+terra": vários efeitos de uma vez
            if (effect != null && effect.IndexOf('+') >= 0)
            {
                foreach (var part in effect.Split('+')) Spawn(part.Trim(), w, count);
                return;
            }
            switch ((effect ?? "").ToLowerInvariant())
            {
                case "faiscas":
                    for (int i = 0; i < (count > 0 ? count : 7); i++)
                        Add(frontX, Ground - scale * 2, R(-110, 110), R(-190, -60), 420, R(0.35f, 0.7f),
                            Pick(Color.Gold, Color.Orange, Color.White, Color.OrangeRed), null, scale);
                    break;
                case "poeira":
                    for (int i = 0; i < (count > 0 ? count : 4); i++)
                        Add(w.X + R(-spriteW * 0.3f, spriteW * 0.3f), Ground - scale, R(-25, 25), R(-35, -10), 0, R(0.4f, 0.8f),
                            Pick(Color.FromArgb(190, 170, 140), Color.FromArgb(150, 130, 110)), null, scale * (1 + rnd.Next(2)));
                    break;
                case "terra":
                    for (int i = 0; i < (count > 0 ? count : 6); i++)
                        Add(frontX, Ground - scale * 2, R(-40, 90) * -w.Dir, R(-170, -80), 420, R(0.4f, 0.8f),
                            Pick(Color.FromArgb(121, 85, 58), Color.FromArgb(93, 64, 45), Color.FromArgb(150, 110, 70)), null, scale * (1 + rnd.Next(2)));
                    break;
                case "neve":
                    for (int i = 0; i < (count > 0 ? count : 5); i++)
                        Add(frontX + R(-10, 10), midY, R(-30, 30), R(-60, -20), 60, R(0.6f, 1.1f),
                            Pick(Color.White, Color.FromArgb(180, 220, 255)), null, scale);
                    break;
                case "estrelas":
                    for (int i = 0; i < (count > 0 ? count : 3); i++)
                        Add(frontX + R(-6, 6), midY, R(-40, 40), R(-80, -30), 60, R(0.5f, 0.9f),
                            Pick(Color.Gold, Color.FromArgb(255, 240, 150), Color.HotPink), null, scale);
                    break;
                case "magia":
                    for (int i = 0; i < (count > 0 ? count : 2); i++)
                        Add(headX + R(-10, 10), headY + R(0, 12), R(-24, 24), R(-40, -20), 0, R(1.0f, 1.5f),
                            Pick(Color.FromArgb(179, 136, 255), Color.FromArgb(100, 255, 218), Color.Gold), Pick("✦", "✧", "★", "✶"), 0);
                    break;
                case "zzz":
                    Add(w.X + w.Dir * spriteW * 0.3f, headY + scale * 4, w.Dir * R(8, 16), R(-26, -18), 0, 2.2f,
                        Color.FromArgb(90, 110, 200), rnd.Next(3) == 0 ? "Z" : "z", 0);
                    break;
                case "notas":
                    Add(headX, headY, w.Dir * R(10, 22), R(-30, -20), 0, 1.8f,
                        Pick(Color.FromArgb(120, 60, 160), Color.FromArgb(40, 120, 60), Color.FromArgb(200, 80, 40)),
                        rnd.Next(2) == 0 ? "♪" : "♫", 0);
                    break;
                case "claude":
                    // as mesmas estrelinhas do spinner do Claude Code
                    Add(headX, headY, w.Dir * R(6, 20), R(-34, -20), 0, 1.6f,
                        Pick(Color.FromArgb(217, 119, 87), Color.FromArgb(245, 163, 127), Color.FromArgb(184, 95, 66)),
                        Pick("✻", "✳", "✢", "✶", "✽"), 0);
                    break;
                case "codigo":
                    for (int i = 0; i < (count > 0 ? count : 2); i++)
                        Add(headX + R(-8, 8), headY + R(0, 10), w.Dir * R(4, 16), R(-34, -18), 0, R(1.2f, 1.7f),
                            Pick(Color.FromArgb(0, 230, 118), Color.FromArgb(105, 240, 174), Color.FromArgb(46, 160, 67)),
                            Pick("0", "1", "{ }", "</>", ";", "=>", "#", "()"), 0);
                    break;
                case "dados":
                    for (int i = 0; i < (count > 0 ? count : 2); i++)
                        Add(headX + R(-8, 8), headY + R(0, 10), w.Dir * R(4, 16), R(-34, -18), 0, R(1.2f, 1.7f),
                            Pick(Color.FromArgb(66, 165, 245), Color.FromArgb(38, 198, 218), Color.FromArgb(255, 202, 40)),
                            Pick("%", "Σ", "π", "▲", "≈", "μ", "σ"), 0);
                    break;
                case "bugs":
                    // besourinhos que pulam para fora e saem correndo pelo chão
                    for (int i = 0; i < (count > 0 ? count : 1 + rnd.Next(2)); i++)
                    {
                        var p = Add(frontX, midY, R(-60, 60), R(-150, -90), 420, R(1.6f, 2.4f), Color.FromArgb(229, 57, 53), null, scale * 2);
                        if (p != null) p.Bug = true;
                    }
                    break;
                case "fumaca":
                    for (int i = 0; i < (count > 0 ? count : 3); i++)
                    {
                        var p = Add(headX + R(-6, 6), headY + R(0, 8), R(-10, 10), R(-30, -16), 0, R(1.0f, 1.6f),
                            Pick(Color.FromArgb(120, 120, 120), Color.FromArgb(150, 150, 150), Color.FromArgb(95, 95, 95)), null, scale);
                        if (p != null) p.Grow = scale * 1.8f;
                    }
                    break;
                case "moedas":
                    for (int i = 0; i < (count > 0 ? count : 2); i++)
                        Add(frontX, midY, R(-50, 50), R(-190, -120), 420, R(0.6f, 0.9f), Pick(Color.Gold, Color.FromArgb(255, 213, 79)), null, scale * 2);
                    if (rnd.Next(3) == 0) Add(headX, headY, w.Dir * 10, -30, 0, 1.2f, Color.Gold, "★", 0);
                    break;
                case "latidos":
                    Add(headX + w.Dir * spriteW * 0.25f, headY + scale * 2, w.Dir * R(10, 20), R(-26, -16), 0, 1.3f,
                        Pick(Color.FromArgb(255, 183, 77), Color.FromArgb(255, 241, 118)), Pick("au!", "au au!", "auu!", "woof!"), 0);
                    break;
                case "coracoes":
                    Add(headX, headY, w.Dir * R(6, 16), R(-30, -18), 0, 1.6f,
                        Pick(Color.FromArgb(236, 64, 122), Color.FromArgb(244, 143, 177), Color.FromArgb(229, 57, 53)), "♥", 0);
                    break;
                case "fogo":
                    // labareda saindo da boca, para a frente
                    for (int i = 0; i < (count > 0 ? count : 12); i++)
                        Add(w.X + w.Dir * spriteW * 0.5f, headY + R(2, 4) * scale, w.Dir * R(140, 240), R(-25, 10), -40, R(0.25f, 0.55f),
                            Pick(Color.FromArgb(255, 235, 59), Color.FromArgb(255, 152, 0), Color.FromArgb(244, 67, 54), Color.FromArgb(255, 243, 176)),
                            null, scale * (1 + rnd.Next(2)));
                    break;
                case "flechas":
                    Add(w.X + w.Dir * spriteW * 0.5f, midY - scale * 3, w.Dir * R(240, 300), R(-8, -2), 0, 0.8f,
                        Color.FromArgb(141, 110, 99), w.Dir > 0 ? "➳" : "⟵", 0);
                    break;
                case "flores":
                    for (int i = 0; i < (count > 0 ? count : 2); i++)
                        Add(frontX + R(-6, 6), midY, R(-30, 30), R(-60, -30), 20, R(1.0f, 1.5f),
                            Pick(Color.FromArgb(236, 64, 122), Color.FromArgb(255, 235, 59), Color.FromArgb(171, 71, 188), Color.FromArgb(102, 187, 106)),
                            Pick("✿", "❀", "✾"), 0);
                    break;
            }
        }

        Particle Add(float x, float y, float vx, float vy, float grav, float life, Color c, string text, int size)
        {
            if (parts.Count > 300) return null;
            var p = new Particle();
            p.X = x; p.Y = y; p.VX = vx; p.VY = vy; p.Gravity = grav; p.Life = p.MaxLife = life;
            p.Color = PixelArt.Safe(c); p.Text = text; p.Size = size;
            parts.Add(p);
            return p;
        }

        void UpdateParticles(float dt)
        {
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                p.Life -= dt;
                if (p.Life <= 0) { parts.RemoveAt(i); continue; }
                p.VY += p.Gravity * dt;
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                if (p.Gravity > 0 && p.Y > Ground - p.Size)
                {
                    p.Y = Ground - p.Size;
                    if (p.Bug) { p.VY = 0; p.Gravity = 0; p.VX = (p.VX >= 0 ? 1 : -1) * 38f * scale / 3f; }
                    else { p.VY *= -0.3f; p.VX *= 0.6f; }
                }
            }
        }

        // ---------- desenho ----------

        SolidBrush Brush(Color c)
        {
            SolidBrush b;
            if (!brushes.TryGetValue(c, out b)) { b = new SolidBrush(c); brushes[c] = b; }
            return b;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.Magenta);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            int top = SpriteTop;
            foreach (var w in workers)
            {
                if (w.Gone || w.Delay > 0 || float.IsNaN(w.X)) continue;
                bool r = w.Dir > 0;
                Bitmap[] set = w.State == WORK ? (r ? w.WorkR : w.WorkL)
                             : w.State == WALK ? (r ? w.WalkR : w.WalkL)
                             : (r ? w.IdleR : w.IdleL);
                int frame = w.State == JUMP || ending >= 0 ? 0 : w.Frame % set.Length;
                var bmp = set[frame];
                int x = (int)w.X - bmp.Width / 2;
                int y = top - (int)w.Y - Fly(w);
                if (w.Squash > 0)
                {
                    // amassadinho ao aterrissar
                    int dh = Math.Max(scale, bmp.Height / 10);
                    g.DrawImage(bmp, new Rectangle(x - dh / 2, y + dh, bmp.Width + dh, bmp.Height - dh));
                }
                else g.DrawImage(bmp, new Rectangle(x, y, bmp.Width, bmp.Height));
                if (w.Carrying && w.State == WALK && w.CarryR != null)
                {
                    var c = r ? w.CarryR : w.CarryL;
                    int bob = (frame % 2) * scale;
                    g.DrawImage(c, new Rectangle((int)w.X - c.Width / 2, HeadTopOf(w) - c.Height - scale + bob, c.Width, c.Height));
                }
            }

            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            foreach (var p in parts)
            {
                if (p.Text != null) { g.DrawString(p.Text, symbolFont, Brush(p.Color), p.X, p.Y); continue; }
                int s = p.Size + (p.Grow > 0 ? (int)(p.Grow * (1 - p.Life / p.MaxLife)) : 0);
                g.FillRectangle(Brush(p.Color), (int)p.X, (int)p.Y, s, s);
                if (p.Bug)
                {
                    // cabeça e perninhas do besouro
                    int hx = p.VX >= 0 ? (int)p.X + s : (int)p.X - scale;
                    g.FillRectangle(Brush(Color.FromArgb(33, 33, 33)), hx, (int)p.Y, scale, scale);
                    g.FillRectangle(Brush(Color.FromArgb(33, 33, 33)), (int)p.X + s / 2 - scale / 2, (int)p.Y, Math.Max(1, scale / 2), s);
                }
            }

            foreach (var w in workers)
                if (w.Bubble != null && !w.Gone && !float.IsNaN(w.X)) DrawBubble(g, w);
        }

        void DrawBubble(Graphics g, Worker w)
        {
            var size = TextRenderer.MeasureText(g, w.Bubble, bubbleFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int pw = 7, ph = 3;
            int bw = size.Width + pw * 2, bh = size.Height + ph * 2;
            int headY = HeadTopOf(w) - (w.Carrying && w.CarryR != null ? w.CarryR.Height + scale : 0);
            int by = Math.Max(0, headY - bh - 7);
            int bx = (int)w.X - bw / 2;
            bx = Math.Max(1, Math.Min(ClientSize.Width - bw - 1, bx));

            var border = Brush(Color.FromArgb(40, 40, 48));
            g.FillRectangle(border, bx, by, bw, bh);
            g.FillRectangle(Brushes.White, bx + 1, by + 1, bw - 2, bh - 2);
            // rabinho
            int tx = Math.Max(bx + 4, Math.Min(bx + bw - 10, (int)w.X - 3));
            for (int i = 0; i < 5; i++)
            {
                int ww = 7 - i * 2 + 2;
                if (ww <= 0) break;
                g.FillRectangle(border, tx + i, by + bh - 1 + i, Math.Max(1, ww), 1);
                if (ww > 2) g.FillRectangle(Brushes.White, tx + i + 1, by + bh - 1 + i, ww - 2, 1);
            }
            TextRenderer.DrawText(g, w.Bubble, bubbleFont, new Point(bx + pw, by + ph), Color.FromArgb(34, 34, 40), Color.White, TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                foreach (var b in brushes.Values) b.Dispose();
                bubbleFont.Dispose();
                symbolFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
