using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

namespace CmdCrew
{
    // Desenha em "pontos" da sala: cada ponto vira U×U pixels de tela.
    public class Painter
    {
        public Graphics G;
        public float U, OX, OY;
        static readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();
        static readonly Dictionary<int, SolidBrush> brushes = new Dictionary<int, SolidBrush>();

        public Painter(Graphics g, float u, float ox, float oy) { G = g; U = u; OX = ox; OY = oy; }

        public static Color C(string hex)
        {
            Color c;
            if (!colors.TryGetValue(hex, out c)) { c = ColorTranslator.FromHtml(hex); colors[hex] = PixelArt.Safe(c); c = colors[hex]; }
            return c;
        }

        public static SolidBrush B(Color c)
        {
            SolidBrush b;
            if (!brushes.TryGetValue(c.ToArgb(), out b)) { b = new SolidBrush(c); brushes[c.ToArgb()] = b; }
            return b;
        }

        public void R(Color c, float x, float y, float w, float h)
        {
            G.FillRectangle(B(c), OX + (float)Math.Round(x * U), OY + (float)Math.Round(y * U), (float)Math.Round(w * U), (float)Math.Round(h * U));
        }

        public void R(string hex, float x, float y, float w, float h) { R(C(hex), x, y, w, h); }

        // Retângulo com os cantos "comidos" (cara de pixel art).
        public void Box(string hex, float x, float y, float w, float h)
        {
            R(hex, x + 1, y, w - 2, h);
            R(hex, x, y + 1, w, h - 2);
        }

        public void Glow(Color c, int alpha, float cx, float cy, float radius)
        {
            using (var b = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c)))
                G.FillEllipse(b, OX + (cx - radius) * U, OY + (cy - radius) * U, radius * 2 * U, radius * 2 * U);
        }
    }

    // Um lugar da sala onde o agente trabalha.
    public class Spot
    {
        public float X;                // centro do personagem, em pontos da cena
        public string Kind;            // animação feita ali ("trabalhar", "parado"...)
        public string WalkKind = "andar";   // animação usada para chegar até aqui
        public bool FaceRight = true;
        public float MinT = 4, MaxT = 8;
    }

    public abstract class RoomTheme
    {
        public const int W = 100, H = 64, Frame = 2, Header = 10;
        public const int SceneX = Frame, SceneY = Frame + Header, SceneW = W - 2 * Frame, SceneH = H - SceneY - Frame;

        public string Id, Name, Description, CharacterId;
        public string[] Skins;
        // personagem gratuito usado quando o principal (de um pacote) não está instalado
        public string FallbackId;
        public string[] FallbackSkins = new string[0];
        public string Accent = "#7c6cf0";
        public float FloorY = 40;          // onde ficam os pés (pontos da cena)
        public float CharHeight = 24;      // altura desejada do personagem (pontos)
        public List<Spot> Spots = new List<Spot>();

        public abstract void Background(Painter p);
        public virtual void Animate(Painter p, double t, RoomState st, double stateT) { }
        public virtual void Front(Painter p, double t, RoomState st) { }

        static List<RoomTheme> all;
        public static List<RoomTheme> All
        {
            get { return all ?? (all = new List<RoomTheme> { new OfficeTheme(), new MineTheme() }); }
        }

        public static RoomTheme Find(string id)
        {
            return All.FirstOrDefault(t => t.Id == id) ?? All[0];
        }

        // Pontinhos aleatórios, mas sempre iguais para a mesma sala.
        protected static IEnumerable<int[]> Scatter(int seed, int n, int w, int h)
        {
            var r = new Random(seed);
            for (int i = 0; i < n; i++) yield return new[] { r.Next(w), r.Next(h), r.Next(100) };
        }
    }

    // ---------------- Escritório: o dev e o monitor cheio de código ----------------
    public class OfficeTheme : RoomTheme
    {
        static readonly string[] CodeColors = { "#7fd1ae", "#ffca3a", "#8ab4f8", "#f28b82", "#c58af9", "#e8eaed" };

        public OfficeTheme()
        {
            Id = "escritorio"; Name = "Escritório do dev";
            Description = "O dev digita sem parar, o monitor enche de código e a caneca solta fumaça.";
            CharacterId = "programadores"; Skins = new[] { "cansado", "cansada", "gamer", "hacker" };
            Accent = "#7c6cf0"; FloorY = 41; CharHeight = 22;
            Spots.Add(new Spot { X = 40, Kind = "trabalhar", FaceRight = true, MinT = 5, MaxT = 10 });
            Spots.Add(new Spot { X = 82, Kind = "parado", FaceRight = true, MinT = 2.5f, MaxT = 4 });
        }

        public override void Background(Painter p)
        {
            // parede listrada e rodapé
            p.R("#3d3657", 0, 0, SceneW, 40);
            for (int x = 2; x < SceneW; x += 6) p.R("#433c60", x, 0, 2, 38);
            p.R("#2a2440", 0, 38, SceneW, 2);
            // piso de tábuas
            p.R("#7a5236", 0, 40, SceneW, SceneH - 40);
            p.R("#6a4630", 0, 43, SceneW, 1);
            p.R("#6a4630", 0, 46, SceneW, 1);
            for (int x = 7; x < SceneW; x += 19) { p.R("#6a4630", x, 40, 1, 3); p.R("#6a4630", x + 9, 44, 1, 2); p.R("#6a4630", x + 4, 47, 1, 3); }
            // tapete
            p.R("#8e3b46", 18, 45, 32, 3); p.R("#a8505b", 19, 45, 30, 1);
            // janela com céu noturno
            p.R("#2a2440", 5, 4, 24, 22);
            p.R("#1b2447", 6, 5, 22, 10); p.R("#243060", 6, 15, 22, 10);
            p.R("#f4e9c1", 21, 7, 4, 4); p.R("#1b2447", 23, 7, 2, 2);   // lua minguante
            p.R("#2a2440", 16, 5, 2, 20); p.R("#2a2440", 6, 14, 22, 2);
            p.R("#8b6a4f", 4, 26, 26, 2);
            // quadro na parede
            p.R("#1f1a2e", 35, 7, 12, 10); p.R("#e8d8b0", 36, 8, 10, 8);
            p.R("#d97757", 40, 10, 2, 4); p.R("#d97757", 38, 11, 6, 2); p.R("#b85f42", 40, 11, 2, 2);
            // estante de livros
            p.R("#4a2f1f", 73, 9, 20, 31); p.R("#2e1d13", 74, 10, 18, 29);
            string[] books = { "#c0392b", "#2e86c1", "#f1c40f", "#27ae60", "#8e44ad", "#e67e22", "#16a085", "#d35400" };
            for (int shelf = 0; shelf < 3; shelf++)
            {
                int top = 10 + shelf * 10;
                int x = 75, i = shelf * 3;
                while (x < 90)
                {
                    int w = 2 + (i * 7 % 3 == 0 ? 1 : 0), h = 6 + (i % 3 == 1 ? 1 : 0);
                    p.R(books[i % books.Length], x, top + 9 - h, w, h);
                    x += w + (i % 4 == 3 ? 1 : 0); i++;
                }
                p.R("#6d4630", 73, top + 9, 20, 1);
            }
            // vaso de planta
            p.R("#2f6b3a", 30, 27, 4, 5); p.R("#3f8f4a", 27, 29, 4, 4); p.R("#3f8f4a", 33, 28, 4, 5); p.R("#4fb05c", 30, 24, 3, 5);
            p.R("#b5651d", 28, 33, 8, 8); p.R("#8e4f17", 28, 33, 8, 1); p.R("#9b5518", 29, 39, 6, 2);
            // mesa
            p.R("#8b5a3c", 45, 29, 28, 2); p.R("#6d4630", 45, 31, 28, 1);
            p.R("#5a3a26", 46, 32, 2, 9); p.R("#5a3a26", 70, 32, 2, 9);
            p.R("#6d4630", 60, 32, 10, 6); p.R("#c9a26b", 64, 34, 2, 1);
            // monitor
            p.R("#2b2b33", 57, 25, 3, 4); p.R("#2b2b33", 54, 28, 9, 1);
            p.R("#1e1e26", 49, 13, 19, 13);
            p.R("#0f1a24", 50, 14, 17, 11);
            // teclado e caneca
            p.R("#cfd8dc", 49, 28, 5, 1);
            p.R("#e57373", 66, 26, 3, 3); p.R("#c62828", 69, 27, 1, 1); p.R("#4e342e", 66, 26, 3, 1);
        }

        public override void Animate(Painter p, double t, RoomState st, double stateT)
        {
            // estrelas piscando
            int[][] stars = { new[] { 8, 7 }, new[] { 12, 10 }, new[] { 19, 12 }, new[] { 9, 18 }, new[] { 24, 19 }, new[] { 14, 21 } };
            for (int i = 0; i < stars.Length; i++)
                if (Math.Sin(t * 2.3 + i * 1.7) > -0.3) p.R(i % 2 == 0 ? "#fff6c8" : "#cfd8ff", stars[i][0], stars[i][1], 1, 1);

            // tela: linhas de código subindo enquanto trabalha; um visto verde quando termina
            if (st == RoomState.Done)
            {
                p.R("#123826", 50, 14, 17, 11);
                string g = "#3ecf7a";
                p.R(g, 53, 19, 2, 2); p.R(g, 55, 21, 2, 2); p.R(g, 57, 19, 2, 2); p.R(g, 59, 17, 2, 2); p.R(g, 61, 15, 2, 2);
            }
            else if (st == RoomState.Failed)
            {
                p.R("#3a1418", 50, 14, 17, 11);
                string r = "#f05a5a";
                for (int i = 0; i < 7; i++) { p.R(r, 55 + i, 16 + i, 1, 1); p.R(r, 61 - i, 16 + i, 1, 1); }
            }
            else if (st == RoomState.Waiting)
            {
                bool on = (int)(t * 2) % 2 == 0;
                p.R("#3a2e10", 50, 14, 17, 11);
                if (on) { p.R("#ffb020", 58, 16, 2, 5); p.R("#ffb020", 58, 22, 2, 2); }
            }
            else
            {
                double speed = st == RoomState.Working ? 2.2 : 0;
                int scroll = (int)(t * speed);
                for (int row = 0; row < 5; row++)
                {
                    int n = scroll + row;
                    var r = new Random(n * 7919);
                    int indent = r.Next(3) * 2, len = 3 + r.Next(10);
                    if (len + indent > 15) len = 15 - indent;
                    int y = 15 + row * 2;
                    if (row == 4)
                    {
                        // a linha de baixo está sendo digitada
                        double frac = (t * speed) - Math.Floor(t * speed);
                        len = Math.Max(1, (int)(len * frac));
                        if ((int)(t * 4) % 2 == 0) p.R("#e8eaed", 51 + indent + len, y, 1, 1);
                    }
                    p.R(CodeColors[r.Next(CodeColors.Length)], 51 + indent, y, len, 1);
                }
            }

            // fumacinha da caneca
            if (st == RoomState.Working)
                for (int i = 0; i < 2; i++)
                {
                    double ph = (t * 0.8 + i * 0.5) % 1.0;
                    p.R("#b0bec5", 67 + (float)Math.Round(Math.Sin(ph * 6 + i) * 0.8), 25 - (float)(ph * 5), 1, 1);
                }
        }
    }

    // ---------------- Mina: o anão pica pedra, empurra o carrinho e leva o saco ----------------
    public class MineTheme : RoomTheme
    {
        public MineTheme()
        {
            Id = "mina"; Name = "Mina dos anões";
            Description = "O anão pica pedra, empurra o carrinho pelos trilhos e a lamparina tremeluz.";
            CharacterId = "anoes"; Skins = new[] { "classico", "ferro-ruivo", "ouro", "azul", "verde" };
            FallbackId = "gnomos";
            Accent = "#d98e3a"; FloorY = 44; CharHeight = 26;
            Spots.Add(new Spot { X = 22, Kind = "trabalhar", FaceRight = true, MinT = 5, MaxT = 9 });
            Spots.Add(new Spot { X = 70, Kind = "trabalhar-saco", WalkKind = "trabalhar-carrinho", FaceRight = true, MinT = 2.5f, MaxT = 3.5f });
        }

        public override void Background(Painter p)
        {
            p.R("#2d2226", 0, 0, SceneW, SceneH);
            foreach (var d in Scatter(7, 70, SceneW, 42))
            {
                string c = d[2] < 50 ? "#3a2c2f" : d[2] < 85 ? "#241b1e" : "#46363a";
                int s = 1 + d[2] % 3;
                p.R(c, d[0], d[1], s + 1, s);
            }
            // túnel ao fundo
            p.R("#140f12", 62, 16, 22, 28); p.R("#140f12", 64, 13, 18, 3); p.R("#140f12", 67, 11, 12, 2);
            p.R("#1c1518", 62, 16, 2, 28);
            // escoras de madeira
            p.R("#8b5e34", 0, 3, SceneW, 3); p.R("#6b4526", 0, 6, SceneW, 1);
            p.R("#7a5230", 3, 6, 4, 38); p.R("#5e3e22", 6, 6, 1, 38);
            p.R("#7a5230", 88, 6, 4, 38); p.R("#5e3e22", 91, 6, 1, 38);
            // rocha com minério (onde ele bate a picareta)
            p.R("#5b4a4f", 38, 30, 12, 14); p.R("#5b4a4f", 36, 34, 16, 10); p.R("#6e5c61", 39, 31, 5, 3); p.R("#463a3e", 44, 38, 7, 6);
            p.R("#4dd0e1", 41, 36, 2, 2); p.R("#4dd0e1", 46, 33, 2, 1); p.R("#ffca3a", 47, 39, 2, 2);
            // chão, trilho e dormentes
            p.R("#4a3426", 0, 44, SceneW, SceneH - 44);
            p.R("#3b291e", 0, 44, SceneW, 1);
            for (int x = 1; x < SceneW; x += 5) p.R("#5d3b22", x, 46, 3, 2);
            p.R("#9aa3ad", 0, 45, SceneW, 1);
            foreach (var d in Scatter(3, 12, SceneW, 3)) p.R("#5c4332", d[0], 48 + d[1] % 2, 2, 1);
            // pedras preciosas na parede
            p.R("#e57373", 14, 12, 2, 2); p.R("#4dd0e1", 55, 8, 2, 2); p.R("#ffca3a", 30, 20, 2, 1); p.R("#81c784", 85, 30, 1, 2);
        }

        public override void Animate(Painter p, double t, RoomState st, double stateT)
        {
            // brilho das pedras
            int[][] gems = { new[] { 14, 12 }, new[] { 55, 8 }, new[] { 41, 36 }, new[] { 47, 39 }, new[] { 30, 20 } };
            for (int i = 0; i < gems.Length; i++)
            {
                double s = Math.Sin(t * 1.7 + i * 2.1);
                if (s > 0.85) { p.R("#ffffff", gems[i][0], gems[i][1], 1, 1); p.R("#ffffff", gems[i][0] - 1, gems[i][1] + 1, 1, 1); }
            }
            // lamparina pendurada, com a luz tremendo
            float lx = 48;
            p.R("#55555e", lx, 6, 1, 5);
            double flick = 0.5 + 0.25 * Math.Sin(t * 13) + 0.25 * Math.Sin(t * 7.3 + 1);
            Color light = st == RoomState.Done ? Painter.C("#8ff0b0") : st == RoomState.Failed ? Painter.C("#ff7070") : Painter.C("#ffc862");
            p.Glow(light, (int)(9 + 5 * flick), lx + 0.5f, 14, 20);
            p.Glow(light, (int)(12 + 7 * flick), lx + 0.5f, 14, 13);
            p.Glow(light, (int)(18 + 10 * flick), lx + 0.5f, 14, 7);
            p.R("#caa04a", lx - 2, 11, 5, 1); p.R("#caa04a", lx - 2, 16, 5, 1);
            p.R("#caa04a", lx - 2, 12, 1, 4); p.R("#caa04a", lx + 2, 12, 1, 4);
            p.R(st == RoomState.Done ? "#8ff0b0" : "#ffe08a", lx - 1, 12, 3, 4);
            p.R("#fff6d0", lx, 13 + (flick > 0.6 ? 0 : 1), 1, 2);
        }
    }

    class Confetti { public float X, Y, VX, VY, Life; public Color C; }

    // Uma sala na tela = uma sessão do Claude Code.
    public class Room
    {
        public string SessionId;
        public RoomTheme Theme;
        public string ProjectName = "";
        public IntPtr Terminal = IntPtr.Zero;
        public RoomState State = RoomState.Working;
        public DateTime Started = DateTime.UtcNow, Changed = DateTime.UtcNow;
        public bool Dismissed;           // clicou numa sala pronta: sai antes do tempo
        public float SlotX = float.NaN;  // posição atual na faixa (px), anima quando outras salas entram/saem
        public float Enter;              // 0..1 subindo
        public float Leave;              // 0..1 descendo
        public bool Leaving, Gone;
        public PermissionAsk Ask;        // pedido de permissão esperando resposta na sala
        public int AskHover = -1;        // botão sob o mouse (0 permitir, 1 negar, 2 terminal)
        public RectangleF[] AskButtons = new RectangleF[0];   // em pixels, relativos ao canto da sala

        readonly Character ch;
        readonly Skin skin;
        readonly Random rnd;
        readonly List<string> speech, endSpeech;
        readonly List<Confetti> confetti = new List<Confetti>();
        readonly Dictionary<string, Bitmap[]> frames = new Dictionary<string, Bitmap[]>();
        float u, charScale;
        int charVisTop, charVisBottom;
        Bitmap background;
        double t, stateT;

        // agente
        float ax, jumpY, jumpV;
        bool faceRight = true;
        int spot, frame;
        string mode = "work", kind = "trabalhar";   // work | walk
        float modeT, frameT, bubbleT, nextBubble = 3;
        string bubble, doneLine;
        RoomState lastState = RoomState.Working;

        public Room(RoomTheme theme, Character ch, int variant, int seed)
        {
            Theme = theme;
            this.ch = ch;
            rnd = new Random(seed);
            // as skins preferidas do tema, se o personagem for o principal; senão, as do próprio personagem
            var ids = ch.Id == theme.CharacterId && theme.Skins.Length > 0 ? theme.Skins
                    : ch.Id == theme.FallbackId && theme.FallbackSkins.Length > 0 ? theme.FallbackSkins
                    : ch.Skins.Select(x => x.Id).ToArray();
            skin = ch.GetSkin(ids[variant % ids.Length]);
            speech = ch.DefaultPhrases("falas");
            endSpeech = ch.DefaultPhrases("falasFim");
            ax = theme.Spots[0].X;
            faceRight = theme.Spots[0].FaceRight;
            kind = theme.Spots[0].Kind;
            modeT = RandT(theme.Spots[0]);
            nextBubble = 2 + (float)rnd.NextDouble() * 4;
        }

        float RandT(Spot s) { return s.MinT + (float)rnd.NextDouble() * (s.MaxT - s.MinT); }

        public int PixelWidth { get { return (int)(RoomTheme.W * u); } }
        public int PixelHeight { get { return (int)(RoomTheme.H * u); } }

        public void SetScale(float unit)
        {
            if (Math.Abs(unit - u) < 0.01f && background != null) return;
            u = unit;
            foreach (var arr in frames.Values) foreach (var b in arr) b.Dispose();
            frames.Clear();
            if (background != null) { background.Dispose(); background = null; }
            // altura visível do desenho (sem as linhas vazias) define a escala do personagem
            charVisTop = skin.TopRow; charVisBottom = ch.BottomRow;
            int vis = Math.Max(1, charVisBottom - charVisTop + 1);
            float s = Theme.CharHeight * u / vis;
            charScale = s >= 2 ? (float)Math.Round(s) : Math.Max(0.5f, s);
        }

        Bitmap[] Frames(string k, bool right)
        {
            string key = k + (right ? ">" : "<");
            Bitmap[] b;
            if (!frames.TryGetValue(key, out b))
            {
                // os desenhos olham para a direita; para a esquerda, espelha
                int px = charScale >= 1 && Math.Abs(charScale - Math.Round(charScale)) < 0.01 ? (int)Math.Round(charScale) : 1;
                b = ch.Render(skin, k, px, !right);
                frames[key] = b;
            }
            return b;
        }

        public void SetState(RoomState st, DateTime changed)
        {
            Changed = changed;
            if (st == State) return;
            State = st;
            stateT = 0;
            if (st == RoomState.Working) { Dismissed = false; if (Leaving && !Gone) { Leaving = false; } }
        }

        public bool ShouldLeave(double doneSeconds)
        {
            if (Dismissed) return true;
            double since = (DateTime.UtcNow - Changed).TotalSeconds;
            if (State == RoomState.Done || State == RoomState.Failed) return since > doneSeconds && stateT > 1.5;
            if (State == RoomState.Stopped) return stateT > 3;
            return false;
        }

        public void Update(float dt, bool bubbles)
        {
            t += dt; stateT += dt;
            if (Leaving) { Leave = Math.Min(1, Leave + dt / 0.4f); if (Leave >= 1) Gone = true; }
            else { Leave = Math.Max(0, Leave - dt / 0.3f); Enter = Math.Min(1, Enter + dt / 0.45f); }

            if (State != lastState)
            {
                if (State == RoomState.Done)
                {
                    SpawnConfetti();
                    bubble = null;
                    doneLine = bubbles && endSpeech.Count > 0 ? endSpeech[rnd.Next(endSpeech.Count)] : null;
                }
                else if (State == RoomState.Waiting) Say("Preciso de você!", 9999);
                else if (State == RoomState.Failed) Say("Ops, deu erro...", 5);
                else if (lastState == RoomState.Waiting) bubble = null;
                if (State != RoomState.Working) { jumpY = 0; jumpV = 0; }
                lastState = State;
            }

            UpdateAgent(dt, bubbles);

            if (bubble != null) { bubbleT -= dt; if (bubbleT <= 0) bubble = null; }
            for (int i = confetti.Count - 1; i >= 0; i--)
            {
                var c = confetti[i];
                c.Life -= dt; c.VY += 60 * dt; c.X += c.VX * dt; c.Y += c.VY * dt;
                if (c.Life <= 0 || c.Y > RoomTheme.SceneH) confetti.RemoveAt(i);
            }
        }

        void Say(string text, float seconds) { bubble = text; bubbleT = seconds; }

        void SpawnConfetti()
        {
            string[] cols = { "#ffca3a", "#3ecf7a", "#8ab4f8", "#f28b82", "#c58af9", "#ffffff" };
            for (int i = 0; i < 36; i++)
                confetti.Add(new Confetti {
                    X = RoomTheme.SceneW / 2f + (float)(rnd.NextDouble() - 0.5) * 20, Y = 14,
                    VX = (float)(rnd.NextDouble() - 0.5) * 70, VY = -30 - (float)rnd.NextDouble() * 35,
                    Life = 1.6f + (float)rnd.NextDouble(), C = Painter.C(cols[rnd.Next(cols.Length)]) });
        }

        void UpdateAgent(float dt, bool bubbles)
        {
            float step = 0.13f;
            if (State == RoomState.Working)
            {
                modeT -= dt;
                if (mode == "walk")
                {
                    var target = Theme.Spots[spot];
                    float dir = target.X > ax ? 1 : -1;
                    faceRight = dir > 0;
                    ax += dir * 11f * dt;
                    if ((dir > 0 && ax >= target.X) || (dir < 0 && ax <= target.X))
                    {
                        ax = target.X; mode = "work"; kind = target.Kind; faceRight = target.FaceRight;
                        modeT = RandT(target); frame = 0;
                    }
                }
                else if (modeT <= 0)
                {
                    // volta ao lugar principal, ou (às vezes) vai até o outro
                    int next = spot != 0 ? 0 : (Theme.Spots.Count > 1 && rnd.NextDouble() < 0.55 ? 1 : 0);
                    if (next == spot) { modeT = RandT(Theme.Spots[spot]); }
                    else
                    {
                        mode = "walk"; spot = next; frame = 0;
                        kind = next == 0 ? "andar" : Theme.Spots[next].WalkKind;
                    }
                }
                if (bubbles && speech.Count > 0 && bubble == null)
                {
                    nextBubble -= dt;
                    if (nextBubble <= 0) { Say(speech[rnd.Next(speech.Count)], 3.2f); nextBubble = 8 + (float)rnd.NextDouble() * 10; }
                }
                if (ch.Detail == 1) step = 0.22f;
            }
            else
            {
                // parou: fica em pé; se terminou, comemora com pulinhos
                if (mode == "walk") mode = "work";
                kind = "parado";
                if (State == RoomState.Done && stateT < 3.2)
                {
                    if (jumpY <= 0 && jumpV <= 0 && (int)(stateT * 10) % 8 == 0) jumpV = 26;
                }
                step = 0.3f;
            }
            if (jumpY > 0 || jumpV > 0)
            {
                jumpY += jumpV * dt; jumpV -= 120 * dt;
                if (jumpY <= 0) { jumpY = 0; jumpV = 0; }
            }
            frameT += dt;
            if (frameT >= step) { frameT = 0; frame++; }
        }

        // ---------------- desenho ----------------

        Color FrameColor()
        {
            switch (State)
            {
                case RoomState.Waiting: return (int)(t * 2.5) % 2 == 0 ? Painter.C("#ffb020") : Painter.C("#8a5a00");
                case RoomState.Done:
                    double k = 0.5 + 0.5 * Math.Sin(t * 5);
                    return Blend(Painter.C("#3ecf7a"), Painter.C("#b9f5cf"), k * 0.6);
                case RoomState.Failed: return Painter.C("#e05050");
                case RoomState.Stopped: return Painter.C("#6b6b78");
                default: return Painter.C(Theme.Accent);
            }
        }

        static Color Blend(Color a, Color b, double k)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
        }

        static string Clock(TimeSpan d)
        {
            if (d.TotalSeconds < 0) d = TimeSpan.Zero;
            return d.TotalHours >= 1 ? string.Format("{0}:{1:00}:{2:00}", (int)d.TotalHours, d.Minutes, d.Seconds)
                                     : string.Format("{0}:{1:00}", (int)d.TotalMinutes, d.Seconds);
        }

        void BuildBackground()
        {
            background = new Bitmap(Math.Max(1, (int)(RoomTheme.SceneW * u)), Math.Max(1, (int)(RoomTheme.SceneH * u)));
            using (var g = Graphics.FromImage(background))
            {
                g.PixelOffsetMode = PixelOffsetMode.Half;
                Theme.Background(new Painter(g, u, 0, 0));
            }
        }

        public void Draw(Graphics g, float x, float y, Font font, Font small)
        {
            if (background == null) BuildBackground();
            if (Ask == null) AskButtons = new RectangleF[0];
            float yoff = (1 - Ease(Enter)) * PixelHeight + Ease(Leave) * PixelHeight;
            var state = g.Save();
            g.SetClip(new RectangleF(x, y, PixelWidth, PixelHeight));
            g.TranslateTransform(x, y + yoff);
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.SmoothingMode = SmoothingMode.None;

            var p = new Painter(g, u, 0, 0);
            // moldura
            p.R("#0d0a12", 0, 0, RoomTheme.W, RoomTheme.H);
            p.R(FrameColor(), 0.5f, 0.5f, RoomTheme.W - 1, RoomTheme.H - 1);
            // cabeçalho
            p.R("#16121f", RoomTheme.Frame, RoomTheme.Frame, RoomTheme.SceneW, RoomTheme.Header);
            DrawHeader(g, p, font);

            // cena
            float sx = RoomTheme.SceneX * u, sy = RoomTheme.SceneY * u;
            g.DrawImageUnscaled(background, (int)sx, (int)sy);
            var sp = new Painter(g, u, sx, sy);
            var inner = g.Save();
            g.SetClip(new RectangleF(sx, sy, RoomTheme.SceneW * u, RoomTheme.SceneH * u), CombineMode.Intersect);
            Theme.Animate(sp, t, State, stateT);
            DrawAgent(g, sx, sy);
            Theme.Front(sp, t, State);
            foreach (var c in confetti) sp.R(c.C, c.X, c.Y, 1, 1);
            if (State == RoomState.Done) DrawBanner(g, sp, font, small);
            if (Ask != null) DrawAsk(g, sp, font, small);
            else if (bubble != null) DrawBubble(g, sp, small);
            g.Restore(inner);
            g.Restore(state);
        }

        static float Ease(float k) { k = Math.Max(0, Math.Min(1, k)); return 1 - (1 - k) * (1 - k) * (1 - k); }

        void DrawHeader(Graphics g, Painter p, Font font)
        {
            // bolinha de status
            Color dot;
            switch (State)
            {
                case RoomState.Done: dot = Painter.C("#3ecf7a"); break;
                case RoomState.Failed: dot = Painter.C("#e05050"); break;
                case RoomState.Waiting: dot = (int)(t * 2.5) % 2 == 0 ? Painter.C("#ffb020") : Painter.C("#5a4010"); break;
                case RoomState.Stopped: dot = Painter.C("#6b6b78"); break;
                default: dot = (int)(t * 1.5) % 2 == 0 ? Painter.C("#7fd1ae") : Painter.C("#3a7a62"); break;
            }
            p.Box(ColorTranslator.ToHtml(dot), 4, 5, 4, 4);

            string status;
            switch (State)
            {
                case RoomState.Done: status = "pronto " + Clock(Changed - Started); break;
                case RoomState.Failed: status = "erro"; break;
                case RoomState.Waiting: status = "espera você"; break;
                case RoomState.Stopped: status = "parado"; break;
                default: status = Clock(DateTime.UtcNow - Started); break;
            }
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var sf = StringFormat.GenericTypographic;
            float right = (RoomTheme.W - RoomTheme.Frame - 2) * u;
            var ssize = g.MeasureString(status, font, 1000, sf);
            float ty = RoomTheme.Frame * u + (RoomTheme.Header * u - font.GetHeight(g)) / 2f + u * 0.3f;
            Color stc = State == RoomState.Done ? Painter.C("#7ff0a8") : State == RoomState.Waiting ? Painter.C("#ffcf66") : Painter.C("#b8b2cc");
            g.DrawString(status, font, Painter.B(stc), right - ssize.Width, ty, sf);

            string name = ProjectName ?? "";
            float maxW = right - ssize.Width - 11 * u - 3 * u;
            if (name.Length > 0 && maxW > 4 * u)
            {
                string shown = name;
                while (shown.Length > 1 && g.MeasureString(shown, font, 1000, sf).Width > maxW)
                    shown = shown.Substring(0, shown.Length - 1);
                if (shown != name) shown = shown.Substring(0, Math.Max(1, shown.Length - 1)) + "…";
                g.DrawString(shown, font, Painter.B(Painter.C("#f3eefe")), 10.5f * u, ty, sf);
            }
        }

        void DrawAgent(Graphics g, float sx, float sy)
        {
            var set = Frames(kind, faceRight);
            if (set.Length == 0) return;
            var bmp = set[frame % set.Length];
            int px = charScale >= 1 && Math.Abs(charScale - Math.Round(charScale)) < 0.01 ? (int)Math.Round(charScale) : 1;
            float k = charScale / px;   // escala extra (só para desenhos detalhados em tamanhos pequenos)
            float w = bmp.Width * k, h = bmp.Height * k;
            float feet = (charVisBottom + 1) * px * k;
            float cx = sx + ax * u, fy = sy + Theme.FloorY * u - jumpY * u;
            // sombra
            using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            {
                float sw = Math.Min(w * 0.45f, 14 * u) * (1 - Math.Min(0.5f, jumpY / 20f));
                g.FillRectangle(b, cx - sw / 2, sy + Theme.FloorY * u - u * 0.5f, sw, u);
            }
            g.DrawImage(bmp, new RectangleF((float)Math.Round(cx - w / 2), (float)Math.Round(fy - feet), w, h));
        }

        public float HeadY { get { return Theme.FloorY - (charVisBottom - charVisTop + 1) * charScale / u - jumpY; } }

        // Letreiro "PRONTO!" que desce do teto, com a fala de fim do personagem embaixo.
        void DrawBanner(Graphics g, Painter sp, Font font, Font small)
        {
            float k = (float)Math.Min(1, stateT / 0.35);
            var sf = StringFormat.GenericTypographic;
            string text = "PRONTO!";
            var size = g.MeasureString(text, font, 1000, sf);
            var sub = doneLine == null ? SizeF.Empty : g.MeasureString(doneLine, small, 1000, sf);
            float wUnits = (float)Math.Ceiling(Math.Max(size.Width, sub.Width) / u) + 8;
            float hUnits = doneLine == null ? 9 : 15;
            wUnits = Math.Min(wUnits, RoomTheme.SceneW - 4);
            float by = -hUnits - 2 + (hUnits + 5) * Ease(k);
            float bx = (float)Math.Round((RoomTheme.SceneW - wUnits) / 2);
            sp.Box("#0d0a12", bx - 1, by - 1, wUnits + 2, hUnits + 2);
            sp.Box("#3ecf7a", bx, by, wUnits, hUnits);
            sp.R("#2aa860", bx + 1, by + hUnits - 2, wUnits - 2, 1);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float cx = sp.OX + (bx + wUnits / 2) * u;
            g.DrawString(text, font, Painter.B(Painter.C("#0d2a18")), cx - size.Width / 2, sp.OY + (by + 1.5f) * u, sf);
            if (doneLine != null)
                g.DrawString(doneLine, small, Painter.B(Painter.C("#14502e")), cx - Math.Min(sub.Width, (wUnits - 4) * u) / 2, sp.OY + (by + 8.5f) * u, sf);
        }

        static readonly string[] AskLabels = { "Permitir", "Negar", "Terminal" };
        static readonly string[] AskColors = { "#3ecf7a", "#f05a5a", "#c9c3d9" };
        static readonly string[] AskDark = { "#2aa860", "#c03a3a", "#9d97ae" };

        static string ToolLabel(string tool)
        {
            switch (tool ?? "")
            {
                case "Bash": case "PowerShell": return "rodar um comando";
                case "Edit": case "MultiEdit": return "editar um arquivo";
                case "Write": return "criar um arquivo";
                case "WebFetch": return "abrir um site";
                case "WebSearch": return "pesquisar na web";
                default: return "usar " + tool;
            }
        }

        // Painel "o Claude quer ...": o que ele quer fazer e os botões permitir / negar / abrir o terminal.
        void DrawAsk(Graphics g, Painter sp, Font font, Font small)
        {
            var sf = StringFormat.GenericTypographic;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float px = 3, py = 2, pw = RoomTheme.SceneW - 6, ph = 27;
            bool blink = (int)(t * 2.5) % 2 == 0;
            sp.Box("#0d0a12", px - 1, py - 1, pw + 2, ph + 2);
            sp.Box(blink ? "#ffe3a3" : "#ffd98a", px, py, pw, ph);
            float tx = sp.OX + (px + 2.5f) * u, maxW = (pw - 5) * u;
            g.DrawString("Quer " + ToolLabel(Ask.Tool) + ":", font, Painter.B(Painter.C("#231a2e")), tx, sp.OY + (py + 1.5f) * u, sf);
            string sum = (Ask.Summary ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (sum.Length == 0) sum = Ask.Tool ?? "";
            string shown = sum;
            while (shown.Length > 1 && g.MeasureString(shown, small, 1000, sf).Width > maxW) shown = shown.Substring(0, shown.Length - 1);
            if (shown != sum) shown = shown.Substring(0, Math.Max(1, shown.Length - 1)) + "…";
            g.DrawString(shown, small, Painter.B(Painter.C("#5a4630")), tx, sp.OY + (py + 8.5f) * u, sf);

            float bw = (pw - 4 - 2 * 2) / 3f, bh = 8, by = py + ph - bh - 2;
            var rects = new RectangleF[3];
            for (int i = 0; i < 3; i++)
            {
                float bx = px + 2 + i * (bw + 2);
                bool hover = AskHover == i;
                sp.Box("#0d0a12", bx, by, bw, bh);
                sp.Box(hover ? AskDark[i] : AskColors[i], bx + 0.5f, by + 0.5f, bw - 1, bh - 1.5f);
                var size = g.MeasureString(AskLabels[i], small, 1000, sf);
                g.DrawString(AskLabels[i], small, Painter.B(Painter.C("#14101c")),
                    sp.OX + (bx + bw / 2) * u - size.Width / 2, sp.OY + (by + 1.6f) * u, sf);
                rects[i] = new RectangleF(sp.OX + bx * u, sp.OY + by * u, bw * u, bh * u);
            }
            AskButtons = rects;
        }

        void DrawBubble(Graphics g, Painter sp, Font font)
        {
            var sf = StringFormat.GenericTypographic;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float maxW = (RoomTheme.SceneW - 10) * u;
            var size = g.MeasureString(bubble, font, (int)maxW, sf);
            float wU = (float)Math.Ceiling(size.Width / u) + 5, hU = (float)Math.Ceiling(size.Height / u) + 3;
            float head = Math.Max(hU + 3, HeadY);
            float bx = Math.Max(1, Math.Min(RoomTheme.SceneW - wU - 1, ax - wU / 2 + (faceRight ? 4 : -4)));
            float by = Math.Max(1, head - hU - 3);
            bool warn = State == RoomState.Waiting;
            sp.Box("#0d0a12", bx - 1, by - 1, wU + 2, hU + 2);
            sp.Box(warn ? "#ffe3a3" : "#fbf7ee", bx, by, wU, hU);
            float tx = Math.Max(bx + 2, Math.Min(bx + wU - 4, ax - 1));
            sp.R("#0d0a12", tx - 1, by + hU, 4, 2); sp.R(warn ? "#ffe3a3" : "#fbf7ee", tx, by + hU, 2, 1); sp.R("#0d0a12", tx + 0.5f, by + hU + 2, 1, 1);
            g.DrawString(bubble, font, Painter.B(Painter.C("#231a2e")), new RectangleF(sp.OX + (bx + 2.5f) * u, sp.OY + (by + 1.5f) * u, maxW, size.Height + u), sf);
        }
    }
}
