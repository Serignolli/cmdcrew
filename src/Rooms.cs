using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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

    // Retângulo de uma sala, em pontos da cena.
    public struct RoomRect
    {
        public Color C;
        public float X, Y, W, H;
    }

    // Uma animação pronta da sala (ver ferramentas/gerar_salas.py). O programa de macOS/Linux (go/sala.go)
    // interpreta exatamente do mesmo jeito.
    public class RoomAnim
    {
        public string Type;
        public HashSet<string> When;          // estados em que aparece (null = sempre)
        public float[][] Points = new float[0][];
        public Color[] Colors = new Color[0];
        public float Speed, Threshold, X, Y, W, H, Radius, Height, VX, VY, SizeW = 1, SizeH = 1;
        public int N;
        public Color Color, Ready, Fail, Background;
        public Dictionary<string, Color> StateColors = new Dictionary<string, Color>();
        public bool Blink;
        public List<RoomRect[]> Frames = new List<RoomRect[]>();
    }

    // Tema de uma sala, lido de salas\<id>.json (gerados por ferramentas/gerar_salas.py).
    public class RoomTheme
    {
        public const int W = 100, H = 64, Frame = 2, Header = 10;
        public const int SceneX = Frame, SceneY = Frame + Header, SceneW = W - 2 * Frame, SceneH = H - SceneY - Frame;

        public string Id, Name, Description;
        public List<string> Characters = new List<string>();   // de quem é a sala (o primeiro disponível trabalha nela)
        public Dictionary<string, string[]> Skins = new Dictionary<string, string[]>();   // cores preferidas de cada um
        public string Accent = "#7c6cf0";
        public float FloorY = 40;          // onde ficam os pés (pontos da cena)
        public float CharHeight = 24;      // altura desejada do personagem (pontos)
        public List<Spot> Spots = new List<Spot>();
        readonly List<RoomRect> back = new List<RoomRect>(), front = new List<RoomRect>();
        readonly List<RoomAnim> anims = new List<RoomAnim>();

        public const string GenericId = "generica";
        public const string ResourcePrefix = "salas/";

        static List<RoomTheme> all;
        public static List<RoomTheme> All
        {
            get
            {
                if (all != null) return all;
                var list = new List<RoomTheme>();
                foreach (var f in Files())
                {
                    try { list.Add(Load(Json.Parse(f.Value) as Dictionary<string, object>)); }
                    catch (Exception ex) { Paths.Log("sala " + f.Key + ": " + ex.Message); }
                }
                if (list.Count == 0) list.Add(new RoomTheme { Id = GenericId, Name = "Salinha", Spots = { new Spot { X = 40, Kind = "trabalhar" } } });
                return all = list;
            }
        }

        // salas\*.json ao lado do programa; sem a pasta (o Setup.exe sozinho), as embutidas no .exe.
        public static IEnumerable<KeyValuePair<string, string>> Files()
        {
            if (Directory.Exists(Paths.RoomsDir))
            {
                foreach (var f in Directory.GetFiles(Paths.RoomsDir, "*.json").OrderBy(x => x))
                    yield return new KeyValuePair<string, string>(Path.GetFileName(f), File.ReadAllText(f, Encoding.UTF8));
                yield break;
            }
            foreach (var f in EmbeddedFiles()) yield return f;
        }

        public static IEnumerable<KeyValuePair<string, string>> EmbeddedFiles()
        {
            var asm = typeof(RoomTheme).Assembly;
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix) && n.EndsWith(".json")).OrderBy(n => n))
                using (var r = new StreamReader(asm.GetManifestResourceStream(name), Encoding.UTF8))
                    yield return new KeyValuePair<string, string>(name.Substring(ResourcePrefix.Length), r.ReadToEnd());
        }

        public static RoomTheme Find(string id)
        {
            return All.FirstOrDefault(t => t.Id == id) ?? All.FirstOrDefault(t => t.Id == GenericId) ?? All[0];
        }

        // A sala do personagem; quem não tem sala própria (personagens feitos em casa) trabalha na genérica.
        public static RoomTheme ForCharacter(string characterId)
        {
            if (characterId == "oompa-loompas") characterId = "chocolateiros";
            return All.FirstOrDefault(t => t.Characters.Contains(characterId)) ?? Find(GenericId);
        }

        // ---------- leitura ----------

        static float F(object o) { try { return Convert.ToSingle(o, CultureInfo.InvariantCulture); } catch { return 0; } }

        static RoomRect ReadRect(object o)
        {
            var a = (o as IEnumerable).Cast<object>().ToArray();
            return new RoomRect { C = Painter.C(Convert.ToString(a[0])), X = F(a[1]), Y = F(a[2]), W = F(a[3]), H = F(a[4]) };
        }

        static List<RoomRect> ReadRects(IDictionary<string, object> d, string key)
        {
            return (Json.Arr(d, key) ?? new List<object>()).Select(ReadRect).ToList();
        }

        static RoomTheme Load(Dictionary<string, object> d)
        {
            var t = new RoomTheme();
            t.Id = Json.Str(d, "id", "sala");
            t.Name = Json.Str(d, "nome", t.Id);
            t.Description = Json.Str(d, "descricao", "");
            t.ImageName = Json.Str(d, "imagem", null);
            t.Characters = Json.Strs(d, "personagens") ?? new List<string>();
            t.Accent = Json.Str(d, "cor", t.Accent);
            t.FloorY = (float)Json.Num(d, "chao", t.FloorY);
            t.CharHeight = (float)Json.Num(d, "altura", t.CharHeight);
            var skins = Json.Obj(d, "skins");
            if (skins != null) foreach (var kv in skins) t.Skins[kv.Key] = (Json.Strs(skins, kv.Key) ?? new List<string>()).ToArray();
            foreach (var o in Json.Arr(d, "lugares") ?? new List<object>())
            {
                var l = o as Dictionary<string, object>;
                if (l == null) continue;
                t.Spots.Add(new Spot { X = (float)Json.Num(l, "x", 40), Kind = Json.Str(l, "tipo", "trabalhar"), WalkKind = Json.Str(l, "andar", "andar"),
                    FaceRight = Json.Bool(l, "direita", true), MinT = (float)Json.Num(l, "min", 4), MaxT = (float)Json.Num(l, "max", 8) });
            }
            if (t.Spots.Count == 0) t.Spots.Add(new Spot { X = 40, Kind = "trabalhar" });
            t.back.AddRange(ReadRects(d, "fundo"));
            t.front.AddRange(ReadRects(d, "frente"));
            foreach (var o in Json.Arr(d, "animacoes") ?? new List<object>())
            {
                var a = o as Dictionary<string, object>;
                if (a != null) t.anims.Add(ReadAnim(a));
            }
            return t;
        }

        static RoomAnim ReadAnim(Dictionary<string, object> a)
        {
            var r = new RoomAnim();
            r.Type = Json.Str(a, "tipo", "");
            var when = Json.Strs(a, "quando");
            if (when != null) r.When = new HashSet<string>(when);
            r.Points = (Json.Arr(a, "pontos") ?? new List<object>()).Select(p => (p as IEnumerable).Cast<object>().Select(F).ToArray()).ToArray();
            r.Colors = (Json.Strs(a, "cores") ?? new List<string>()).Select(Painter.C).ToArray();
            r.Speed = (float)Json.Num(a, "vel", 1);
            r.Threshold = (float)Json.Num(a, "limiar", 0);
            r.X = (float)Json.Num(a, "x", 0); r.Y = (float)Json.Num(a, "y", 0);
            r.W = (float)Json.Num(a, "w", 1); r.H = (float)Json.Num(a, "h", 1);
            r.Radius = (float)Json.Num(a, "raio", 10);
            r.Height = (float)Json.Num(a, "altura", 5);
            r.N = (int)Json.Num(a, "n", 2);
            r.VX = (float)Json.Num(a, "vx", 0); r.VY = (float)Json.Num(a, "vy", 0);
            var size = Json.Arr(a, "tam");
            if (size != null && size.Count >= 2) { r.SizeW = F(size[0]); r.SizeH = F(size[1]); }
            r.Color = Painter.C(Json.Str(a, "cor", "#ffffff"));
            r.Ready = Painter.C(Json.Str(a, "pronto", Json.Str(a, "cor", "#ffffff")));
            r.Fail = Painter.C(Json.Str(a, "erro", Json.Str(a, "cor", "#ffffff")));
            r.Background = Painter.C(Json.Str(a, "fundo", "#0f1a24"));
            r.Blink = Json.Bool(a, "piscar", false);
            var sc = Json.Obj(a, "cores");
            if (sc != null) foreach (var kv in sc) r.StateColors[kv.Key] = Painter.C(Convert.ToString(kv.Value));
            var area = Json.Arr(a, "area");
            if (area != null && area.Count >= 4) { r.X = F(area[0]); r.Y = F(area[1]); r.W = F(area[2]); r.H = F(area[3]); }
            foreach (var q in Json.Arr(a, "quadros") ?? new List<object>())
                r.Frames.Add((q as IEnumerable).Cast<object>().Select(ReadRect).ToArray());
            return r;
        }

        // ---------- desenho ----------

        public static string StateName(RoomState st)
        {
            switch (st)
            {
                case RoomState.Waiting: return "esperando";
                case RoomState.Done: return "pronto";
                case RoomState.Failed: return "erro";
                case RoomState.Stopped: return "parado";
                default: return "trabalhando";
            }
        }

        // Número pseudoaleatório em [0, 1), igual em C# e em Go (as salas ficam idênticas nos dois programas).
        public static double Hash(int n)
        {
            unchecked
            {
                uint x = (uint)n * 2654435761u;
                x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
                return x / 4294967296.0;
            }
        }

        static float Mod(float a, float m) { if (m <= 0) return 0; float r = a % m; return r < 0 ? r + m : r; }

        // Arte de fundo opcional ("imagem": "mina.png", ao lado do JSON ou embutida no .exe), esticada na cena inteira.
        // Os retângulos do "fundo" são desenhados por cima dela.
        public string ImageName;

        Bitmap LoadImage()
        {
            if (string.IsNullOrEmpty(ImageName)) return null;
            try
            {
                string file = Path.Combine(Paths.RoomsDir, ImageName);
                if (File.Exists(file)) using (var b = new Bitmap(file)) return new Bitmap(b);
                var st = typeof(RoomTheme).Assembly.GetManifestResourceStream(ResourcePrefix + ImageName);
                if (st != null) using (st) using (var b = new Bitmap(st)) return new Bitmap(b);
            }
            catch (Exception ex) { Paths.Log("imagem da sala " + ImageName + ": " + ex.Message); }
            return null;
        }

        public void Background(Painter p)
        {
            using (var img = LoadImage())
                if (img != null)
                {
                    var mode = p.G.InterpolationMode;
                    p.G.InterpolationMode = InterpolationMode.NearestNeighbor;
                    p.G.DrawImage(img, new RectangleF(p.OX, p.OY, SceneW * p.U, SceneH * p.U));
                    p.G.InterpolationMode = mode;
                }
            foreach (var r in back) p.R(r.C, r.X, r.Y, r.W, r.H);
        }

        public void Front(Painter p, double t, RoomState st) { foreach (var r in front) p.R(r.C, r.X, r.Y, r.W, r.H); }

        static readonly string[] CodeColors = { "#7fd1ae", "#ffca3a", "#8ab4f8", "#f28b82", "#c58af9", "#e8eaed" };

        public void Animate(Painter p, double t, RoomState st, double stateT)
        {
            string stn = StateName(st);
            foreach (var a in anims)
            {
                if (a.When != null && !a.When.Contains(stn)) continue;
                switch (a.Type)
                {
                    case "piscar":
                        for (int i = 0; i < a.Points.Length; i++)
                        {
                            if (Math.Sin(t * a.Speed + i * 1.7) <= a.Threshold || a.Colors.Length == 0) continue;
                            var pt = a.Points[i];
                            p.R(a.Colors[i % a.Colors.Length], pt[0], pt[1], pt.Length > 2 ? pt[2] : 1, pt.Length > 3 ? pt[3] : 1);
                        }
                        break;
                    case "brilho":
                        for (int i = 0; i < a.Points.Length; i++)
                            if (Math.Sin(t * 1.7 + i * 2.1) > 0.85)
                            {
                                p.R("#ffffff", a.Points[i][0], a.Points[i][1], 1, 1);
                                p.R("#ffffff", a.Points[i][0] - 1, a.Points[i][1] + 1, 1, 1);
                            }
                        break;
                    case "fumaca":
                        for (int i = 0; i < a.N; i++)
                        {
                            double ph = (t * 0.8 + i * 0.5) % 1.0;
                            p.R(a.Color, a.X + (float)Math.Round(Math.Sin(ph * 6 + i) * 0.8), a.Y - (float)(ph * a.Height), 1, 1);
                        }
                        break;
                    case "luz":
                        {
                            double flick = 0.5 + 0.25 * Math.Sin(t * 13) + 0.25 * Math.Sin(t * 7.3 + 1);
                            Color c = st == RoomState.Done ? a.Ready : st == RoomState.Failed ? a.Fail : a.Color;
                            p.Glow(c, (int)(9 + 5 * flick), a.X, a.Y, a.Radius);
                            p.Glow(c, (int)(12 + 7 * flick), a.X, a.Y, a.Radius * 0.65f);
                            p.Glow(c, (int)(18 + 10 * flick), a.X, a.Y, a.Radius * 0.35f);
                        }
                        break;
                    case "estado":
                        {
                            Color c;
                            if (!a.StateColors.TryGetValue(stn, out c) && !a.StateColors.TryGetValue("trabalhando", out c)) c = Painter.C(Accent);
                            if (a.Blink && (st == RoomState.Waiting || st == RoomState.Working) && (int)(t * (st == RoomState.Waiting ? 2.5 : 1.2)) % 2 == 1) break;
                            p.R(c, a.X, a.Y, a.W, a.H);
                        }
                        break;
                    case "quadros":
                        if (a.Frames.Count == 0) break;
                        foreach (var r in a.Frames[a.Speed <= 0 ? 0 : (int)(t * a.Speed) % a.Frames.Count]) p.R(r.C, r.X, r.Y, r.W, r.H);
                        break;
                    case "particulas":
                        for (int i = 0; i < a.N; i++)
                        {
                            if (a.Colors.Length == 0) break;
                            float k = 0.7f + 0.6f * (float)Hash(i * 7 + 13);
                            float x = a.X + Mod((float)Hash(i * 7 + 11) * a.W + (float)t * a.VX * k, a.W);
                            float y = a.Y + Mod((float)Hash(i * 7 + 12) * a.H + (float)t * a.VY * k, a.H);
                            p.R(a.Colors[i % a.Colors.Length], (float)Math.Floor(x), (float)Math.Floor(y), a.SizeW, a.SizeH);
                        }
                        break;
                    case "tela":
                        Screen(p, a, t, st);
                        break;
                }
            }
        }

        // Monitor: código subindo enquanto trabalha, visto verde no fim, X vermelho no erro e "!" piscando esperando.
        void Screen(Painter p, RoomAnim a, double t, RoomState st)
        {
            float cx = (float)Math.Floor(a.X + a.W / 2), cy = (float)Math.Floor(a.Y + a.H / 2);
            if (st == RoomState.Done)
            {
                p.R("#123826", a.X, a.Y, a.W, a.H);
                foreach (var d in new[] { new[] { -5, 0 }, new[] { -3, 2 }, new[] { -1, 0 }, new[] { 1, -2 }, new[] { 3, -4 } })
                    p.R("#3ecf7a", cx + d[0], cy + d[1], 2, 2);
            }
            else if (st == RoomState.Failed)
            {
                p.R("#3a1418", a.X, a.Y, a.W, a.H);
                for (int i = 0; i < 7; i++) { p.R("#f05a5a", cx - 3 + i, cy - 3 + i, 1, 1); p.R("#f05a5a", cx + 3 - i, cy - 3 + i, 1, 1); }
            }
            else if (st == RoomState.Waiting)
            {
                p.R("#3a2e10", a.X, a.Y, a.W, a.H);
                if ((int)(t * 2) % 2 == 0) { p.R("#ffb020", cx, cy - 3, 2, 5); p.R("#ffb020", cx, cy + 3, 2, 2); }
            }
            else
            {
                p.R(a.Background, a.X, a.Y, a.W, a.H);
                var colors = a.Colors.Length > 0 ? a.Colors : CodeColors.Select(Painter.C).ToArray();
                double speed = st == RoomState.Working ? 2.2 : 0;
                int scroll = (int)(t * speed);
                int rows = Math.Max(1, (int)((a.H - 1) / 2)), maxLen = Math.Max(1, (int)a.W - 2);
                for (int row = 0; row < rows; row++)
                {
                    int n = scroll + row;
                    int indent = (int)(Hash(n * 3 + 1) * 3) * 2, len = 3 + (int)(Hash(n * 3 + 2) * 10);
                    if (len + indent > maxLen) len = Math.Max(1, maxLen - indent);
                    float y = a.Y + 1 + row * 2;
                    if (row == rows - 1)
                    {
                        // a linha de baixo está sendo digitada
                        double frac = t * speed - Math.Floor(t * speed);
                        len = Math.Max(1, (int)(len * frac));
                        if ((int)(t * 4) % 2 == 0) p.R("#e8eaed", a.X + 1 + indent + len, y, 1, 1);
                    }
                    p.R(colors[(int)(Hash(n * 3 + 3) * colors.Length) % colors.Length], a.X + 1 + indent, y, len, 1);
                }
            }
        }
    }

    // Confete do fim e as faíscas, labaredas, flechas... do trabalho (em pontos da cena).
    class Confetti { public float X, Y, VX, VY, Life, Gravity = 60, W = 1, H = 1; public Color C; }

    // Uma sala na tela = uma sessão do Claude Code.
    public class Room
    {
        public string SessionId;
        public RoomTheme Theme;
        public string ProjectName = "";
        public IntPtr Terminal = IntPtr.Zero;
        public int TerminalPid;
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

        // variant: quantas salas iguais já estão na tela (cada uma ganha outra cor, para não confundir os terminais).
        // preferSkin: a cor escolhida na configuração, que fica com a primeira sala.
        public Room(RoomTheme theme, Character ch, int variant, int seed, string preferSkin = null)
        {
            Theme = theme;
            this.ch = ch;
            rnd = new Random(seed);
            // as cores preferidas da sala para este personagem; senão, todas as dele
            string[] pref;
            var ids = (theme.Skins.TryGetValue(ch.Id, out pref) ? pref : new string[0])
                .Where(id => ch.Skins.Any(x => x.Id == id)).ToList();
            if (ids.Count == 0) ids = ch.Skins.Select(x => x.Id).ToList();
            if (!string.IsNullOrEmpty(preferSkin) && preferSkin != "*" && ch.Skins.Any(x => x.Id == preferSkin))
            {
                ids.Remove(preferSkin);
                ids.Insert(0, preferSkin);
            }
            skin = ch.GetSkin(ids[variant % ids.Count]);
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
            charVisTop = skin.TopRow; charVisBottom = ch.FeetRow;
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
                b = ch.Render(skin, ch.HasKind(skin, k) || k.StartsWith("trabalhar") || k == "parado" ? k : "andar", px, !right);
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
                c.Life -= dt; c.VY += c.Gravity * dt; c.X += c.VX * dt; c.Y += c.VY * dt;
                if (c.Life <= 0 || c.Y > RoomTheme.SceneH || c.X < -4 || c.X > RoomTheme.SceneW + 4) confetti.RemoveAt(i);
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
                    int next = spot != 0 ? 0 : (Theme.Spots.Count > 1 && rnd.NextDouble() < 0.55 ? 1 + rnd.Next(Theme.Spots.Count - 1) : 0);
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
            if (frameT >= step)
            {
                frameT = 0; frame++;
                // o golpe de cada ciclo de trabalho solta o efeito do personagem (faíscas, fogo, flecha...)
                if (State == RoomState.Working && mode == "work" && kind.StartsWith("trabalhar") && u > 0)
                {
                    int n = Frames(kind, faceRight).Length;
                    if (n > 0 && frame % n == n - 1) SpawnEffect(ch.EffectOf(skin, kind));
                }
            }
        }

        // Quem voa (o dragão) fica no ar, subindo e descendo devagar.
        float Fly { get { return ch.FlyHeight <= 0 || u <= 0 ? 0 : Math.Min(12f, ch.FlyHeight * charScale / u) + (float)Math.Sin(t * 3.2) * 1.2f; } }

        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        void Particle(float x, float y, float vx, float vy, float grav, float life, string color, float w = 1, float h = 1)
        {
            if (confetti.Count > 160) return;
            confetti.Add(new Confetti { X = x, Y = y, VX = vx, VY = vy, Gravity = grav, Life = life, C = Painter.C(color), W = w, H = h });
        }

        // Os efeitos de trabalho, em versão de pontinhos para caber na sala (os com texto, como "zzz", ficam só na equipe).
        void SpawnEffect(string effect)
        {
            if (string.IsNullOrEmpty(effect)) return;
            if (effect.IndexOf('+') >= 0) { foreach (var e in effect.Split('+')) SpawnEffect(e.Trim()); return; }
            float dir = faceRight ? 1 : -1;
            float halfW = ch.Width * charScale / u / 2;
            float front = ax + dir * halfW * 0.9f, floor = Theme.FloorY - Fly;
            float head = HeadY, mid = head + (Theme.FloorY - head) * 0.4f;
            Func<string[], string> pick = c => c[rnd.Next(c.Length)];
            switch (effect.ToLowerInvariant())
            {
                case "faiscas":
                    for (int i = 0; i < 6; i++) Particle(front, floor - 1, R(-30, 30), R(-55, -20), 140, R(0.3f, 0.6f), pick(new[] { "#ffd54f", "#ff9800", "#ffffff", "#ff5722" }));
                    break;
                case "terra":
                    for (int i = 0; i < 5; i++) Particle(front, floor - 1, -dir * R(-10, 25), R(-50, -25), 140, R(0.4f, 0.7f), pick(new[] { "#79553a", "#5d402d", "#966e46" }));
                    break;
                case "poeira":
                    for (int i = 0; i < 3; i++) Particle(ax + R(-halfW * 0.6f, halfW * 0.6f), floor - 0.5f, R(-8, 8), R(-10, -3), 0, R(0.4f, 0.8f), pick(new[] { "#beaa8c", "#96826e" }));
                    break;
                case "neve":
                    for (int i = 0; i < 4; i++) Particle(front, mid, R(-10, 10), R(-20, -7), 20, R(0.6f, 1.0f), pick(new[] { "#ffffff", "#b4dcff" }));
                    break;
                case "fogo":
                    for (int i = 0; i < 10; i++)
                        Particle(ax + dir * halfW, head + (Theme.FloorY - head) * 0.25f + R(-1, 1), dir * R(35, 65), R(-6, 3), -10, R(0.25f, 0.5f),
                            pick(new[] { "#ffeb3b", "#ff9800", "#f44336", "#fff3b0" }), R(1, 2), 1);
                    break;
                case "flechas":
                    Particle(front, mid, dir * 80, -1, 0, 0.8f, "#8d6e63", 4, 1);
                    break;
                case "moedas":
                    for (int i = 0; i < 2; i++) Particle(front, mid, R(-15, 15), R(-55, -35), 140, R(0.6f, 0.9f), pick(new[] { "#ffd700", "#ffd54f" }), 2, 2);
                    break;
                case "flores": case "estrelas": case "claude": case "magia": case "coracoes": case "notas": case "codigo": case "dados":
                    {
                        string[] cols =
                            effect == "flores" ? new[] { "#ec407a", "#ffeb3b", "#ab47bc", "#66bb6a" } :
                            effect == "estrelas" ? new[] { "#ffd700", "#fff096", "#ff69b4" } :
                            effect == "claude" ? new[] { "#d97757", "#f5a37f", "#b85f42" } :
                            effect == "magia" ? new[] { "#b388ff", "#64ffda", "#ffd700" } :
                            effect == "coracoes" ? new[] { "#ec407a", "#f48fb1", "#e53935" } :
                            effect == "notas" ? new[] { "#7a3ca0", "#287a3c", "#c85028" } :
                            effect == "dados" ? new[] { "#42a5f5", "#26c6da", "#ffca28" } :
                            new[] { "#00e676", "#69f0ae", "#2ea043" };
                        for (int i = 0; i < 3; i++) Particle(ax + dir * halfW * 0.4f + R(-2, 2), head + R(0, 3), dir * R(2, 7), R(-12, -6), 0, R(0.9f, 1.4f), pick(cols));
                    }
                    break;
            }
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
            foreach (var c in confetti) sp.R(c.C, c.X, c.Y, c.W, c.H);
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
            float cx = sx + ax * u, fy = sy + Theme.FloorY * u - (jumpY + Fly) * u;
            // sombra
            using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            {
                float sw = Math.Min(w * 0.45f, 14 * u) * (1 - Math.Min(0.5f, (jumpY + Fly) / 20f));
                g.FillRectangle(b, cx - sw / 2, sy + Theme.FloorY * u - u * 0.5f, sw, u);
            }
            g.DrawImage(bmp, new RectangleF((float)Math.Round(cx - w / 2), (float)Math.Round(fy - feet), w, h));
        }

        public float HeadY { get { return Theme.FloorY - (charVisBottom - charVisTop + 1) * charScale / u - jumpY - Fly; } }

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
