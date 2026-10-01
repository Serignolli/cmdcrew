using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CmdCrew
{
    public static class Paths
    {
        public static string Exe { get { return Application.ExecutablePath; } }
        public static string Root { get { return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Exe), "..")); } }
        public static string ConfigFile { get { return Path.Combine(Root, "config.json"); } }
        public static string CharactersDir { get { return Path.Combine(Root, "personagens"); } }
        // Pacotes de personagens extras (o Supporters Pack é instalado aqui). Na pasta do projeto,
        // supporters-pack\personagens também vale, para desenvolver sem instalar nada.
        public static string PacksDir { get { return Path.Combine(Root, "pacotes"); } }
        public static string DevPackDir { get { return Path.Combine(Root, "supporters-pack", "personagens"); } }
        public static string StateDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CmdCrew"); } }
        public static string SessionsDir { get { return Path.Combine(StateDir, "sessoes"); } }
        public static string AnswersDir { get { return Path.Combine(StateDir, "respostas"); } }
        public static string PositionFile { get { return Path.Combine(StateDir, "posicao.txt"); } }
        public static string TargetFile { get { return Path.Combine(StateDir, "alvo.txt"); } }
        public static string LogFile { get { return Path.Combine(StateDir, "log.txt"); } }
        public static string ErrorFile { get { return Path.Combine(StateDir, "ultimo-erro.txt"); } }

        public static string ClaudeSettings
        {
            get
            {
                string dir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
                if (string.IsNullOrEmpty(dir))
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
                return Path.Combine(dir, "settings.json");
            }
        }

        public static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(StateDir);
                var fi = new FileInfo(LogFile);
                if (fi.Exists && fi.Length > 200000) fi.Delete();
                File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    public class Skin
    {
        public string Id;
        public string Name;
        public Dictionary<char, Color> Colors = new Dictionary<char, Color>();
        // Opcionais: desenho, efeito e carga próprios (outra raça, outro modelo...). Sem eles, valem os do personagem.
        public Dictionary<string, List<string[]>> Frames;
        public string Effect, Carry, Base;
        public int TopRow;
    }

    public class Character
    {
        public string Id, Name, Category, Description, Effect, Carry, SourceFile;
        public bool Premium;   // veio de um pacote (Supporters Pack)
        public Dictionary<string, List<string[]>> Frames = new Dictionary<string, List<string[]>>();
        public List<Skin> Skins = new List<Skin>();
        public Dictionary<string, List<string>> Phrases = new Dictionary<string, List<string>>();
        public int Width, Height, TopRow, BottomRow;
        // Quantos pontos do desenho cabem em um "ponto" do tamanho padrão (1 = normal, 2 = 2×2, 3 = 3×3).
        public int Detail = 1;
        // Personagens que voam ("voo": altura em pontos do desenho acima do chão). 0 = andam no chão.
        public int FlyHeight;

        public static readonly string[] PhraseKinds = { "verbos", "dicas", "falas", "falasFim" };

        static bool Blank(char c) { return c == '.' || c == ' '; }

        static Dictionary<string, List<string[]>> ReadSprites(Dictionary<string, object> sprites)
        {
            var result = new Dictionary<string, List<string[]>>();
            foreach (var kv in sprites)
            {
                var frames = kv.Value as IEnumerable;
                if (frames == null) continue;
                var list = new List<string[]>();
                foreach (object f in frames)
                {
                    var rows = new List<string>();
                    var fr = f as IEnumerable;
                    if (fr == null || f is string) continue;
                    foreach (object r in fr) rows.Add(Convert.ToString(r));
                    if (rows.Count > 0) list.Add(rows.ToArray());
                }
                if (list.Count > 0) result[kv.Key] = list;
            }
            if (result.Count == 0) return result;
            if (!result.ContainsKey("andar")) throw new InvalidDataException("faltou \"sprites.andar\"");
            if (!result.ContainsKey("trabalhar")) result["trabalhar"] = result["andar"];
            return result;
        }

        public static Character Load(string path)
        {
            return Load(Json.LoadObject(path), path);
        }

        static Character Load(Dictionary<string, object> d, string path)
        {
            var c = new Character();
            c.SourceFile = path;
            c.Id = Json.Str(d, "id", Path.GetFileNameWithoutExtension(path));
            c.Name = Json.Str(d, "nome", c.Id);
            c.Category = Json.Str(d, "categoria", "Outros");
            c.Description = Json.Str(d, "descricao", "");
            c.Effect = Json.Str(d, "efeito", "");
            c.Carry = Json.Str(d, "carga", "");
            c.Detail = Math.Max(1, Math.Min(8, (int)Json.Num(d, "detalhe", 1)));
            c.FlyHeight = Math.Max(0, Math.Min(64, (int)Json.Num(d, "voo", 0)));
            foreach (string k in PhraseKinds) c.Phrases[k] = Json.Strs(d, k) ?? new List<string>();
            var fx = Json.Obj(d, "efeitosTrabalho");
            if (fx != null) foreach (var kv in fx) c.WorkEffects[kv.Key] = Convert.ToString(kv.Value);

            var sprites = Json.Obj(d, "sprites");
            if (sprites != null) c.Frames = ReadSprites(sprites);

            var skins = Json.Arr(d, "skins");
            if (skins != null)
            {
                foreach (object so in skins)
                {
                    var sd = so as Dictionary<string, object>;
                    if (sd == null) continue;
                    var s = new Skin();
                    s.Id = Json.Str(sd, "id", "skin" + c.Skins.Count);
                    s.Name = Json.Str(sd, "nome", s.Id);
                    s.Effect = Json.Str(sd, "efeito", null);
                    s.Carry = Json.Str(sd, "carga", null);
                    s.Base = Json.Str(sd, "base", null);
                    var cores = Json.Obj(sd, "cores");
                    if (cores != null)
                        foreach (var kv in cores)
                            if (kv.Key.Length > 0) s.Colors[kv.Key[0]] = PixelArt.ParseColor(Convert.ToString(kv.Value));
                    var own = Json.Obj(sd, "sprites");
                    if (own != null) { s.Frames = ReadSprites(own); if (s.Frames.Count == 0) s.Frames = null; }
                    c.Skins.Add(s);
                }
            }
            if (c.Skins.Count == 0) throw new InvalidDataException("precisa de pelo menos uma skin");

            // "base": a skin reaproveita desenho, cores, efeito e carga de outra, e só troca o que definir.
            foreach (var s in c.Skins)
            {
                var seen = new HashSet<Skin> { s };
                string b = s.Base;
                while (!string.IsNullOrEmpty(b))
                {
                    var bs = c.Skins.FirstOrDefault(x => x.Id == b);
                    if (bs == null || !seen.Add(bs)) break;
                    if (s.Frames == null) s.Frames = bs.Frames;
                    foreach (var kv in bs.Colors) if (!s.Colors.ContainsKey(kv.Key)) s.Colors[kv.Key] = kv.Value;
                    if (s.Effect == null) s.Effect = bs.Effect;
                    if (s.Carry == null) s.Carry = bs.Carry;
                    b = bs.Base;
                }
            }
            if (c.Frames.Count == 0)
            {
                var first = c.Skins.FirstOrDefault(s => s.Frames != null);
                if (first == null) throw new InvalidDataException("faltou \"sprites\"");
                c.Frames = first.Frames;
            }
            foreach (var s in c.Skins) if (s.Frames == null) s.Frames = c.Frames;

            // Normaliza: todos os quadros (de todas as skins) com a mesma largura/altura, alinhados por baixo.
            var sets = c.Skins.Select(s => s.Frames).Concat(new[] { c.Frames }).Distinct().ToList();
            int w = 0, h = 0;
            foreach (var set in sets)
                foreach (var l in set.Values)
                    foreach (var fr in l) { h = Math.Max(h, fr.Length); foreach (var r in fr) w = Math.Max(w, r.Length); }
            c.Width = w; c.Height = h;
            c.TopRow = h; c.BottomRow = 0;
            var tops = new Dictionary<Dictionary<string, List<string[]>>, int>();
            foreach (var set in sets)
            {
                int top = h;
                foreach (var key in set.Keys.ToList())
                {
                    var norm = new List<string[]>();
                    foreach (var fr in set[key])
                    {
                        var rows = new string[h];
                        int pad = h - fr.Length;
                        for (int y = 0; y < h; y++)
                        {
                            string r = y < pad ? "" : fr[y - pad];
                            rows[y] = r.PadRight(w, '.');
                            if (rows[y].Any(ch => !Blank(ch)))
                            {
                                top = Math.Min(top, y);
                                c.BottomRow = Math.Max(c.BottomRow, y);
                            }
                        }
                        norm.Add(rows);
                    }
                    set[key] = norm;
                }
                tops[set] = top;
                c.TopRow = Math.Min(c.TopRow, top);
            }
            if (c.TopRow > c.BottomRow) throw new InvalidDataException("os sprites estão vazios");
            foreach (var s in c.Skins) s.TopRow = Math.Min(tops[s.Frames], c.BottomRow);

            // Skins sem "base" herdam da primeira as cores que não definirem.
            for (int i = 1; i < c.Skins.Count; i++)
                if (string.IsNullOrEmpty(c.Skins[i].Base))
                    foreach (var kv in c.Skins[0].Colors)
                        if (!c.Skins[i].Colors.ContainsKey(kv.Key)) c.Skins[i].Colors[kv.Key] = kv.Value;
            return c;
        }

        // Personagens embutidos no .exe (usados pelo instalador antes de existir a pasta personagens/).
        public const string ResourcePrefix = "personagens/";

        public static IEnumerable<KeyValuePair<string, string>> EmbeddedFiles()
        {
            var asm = typeof(Character).Assembly;
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix)).OrderBy(n => n))
                using (var r = new StreamReader(asm.GetManifestResourceStream(name), Encoding.UTF8))
                    yield return new KeyValuePair<string, string>(name.Substring(ResourcePrefix.Length), r.ReadToEnd());
        }

        public static List<Character> LoadEmbedded(List<string> errors)
        {
            var list = new List<Character>();
            foreach (var f in EmbeddedFiles())
            {
                try { list.Add(Load(Json.Parse(f.Value) as Dictionary<string, object>, f.Key)); }
                catch (Exception ex) { if (errors != null) errors.Add(f.Key + ": " + ex.Message); }
            }
            return list;
        }

        public static List<Character> LoadAll(List<string> errors)
        {
            var list = Directory.Exists(Paths.CharactersDir) ? LoadDir(Paths.CharactersDir, false, errors) : LoadEmbedded(errors);
            // pacotes: um personagem com o mesmo id substitui o gratuito
            foreach (var dir in new[] { Paths.PacksDir, Paths.DevPackDir })
                foreach (var c in LoadDir(dir, true, errors))
                {
                    list.RemoveAll(x => x.Id == c.Id);
                    list.Add(c);
                }
            return list;
        }

        static List<Character> LoadDir(string dir, bool premium, List<string> errors)
        {
            var list = new List<Character>();
            if (!Directory.Exists(dir)) return list;
            foreach (var f in Directory.GetFiles(dir, "*.json").OrderBy(x => x))
            {
                try { var c = Load(f); c.Premium = premium; list.Add(c); }
                catch (Exception ex) { if (errors != null) errors.Add(Path.GetFileName(f) + ": " + ex.Message); }
            }
            return list;
        }

        public static Character Find(List<Character> all, string id)
        {
            foreach (var c in all) if (c.Id == id) return c;
            return all.Count > 0 ? all[0] : null;
        }

        public override string ToString() { return Name; }

        // Pixels de tela por ponto do desenho, a partir da escala padrão (tamanho × DPI).
        public int PixelScale(int unit) { return Math.Max(1, (int)Math.Round(unit / (double)Detail)); }

        public Skin GetSkin(string id)
        {
            foreach (var s in Skins) if (s.Id == id) return s;
            return Skins[0];
        }

        public string EffectOf(Skin s) { return s != null && s.Effect != null ? s.Effect : Effect; }

        // Efeito de um tipo de trabalho ("efeitosTrabalho": { "trabalhar-cavar": "terra" }); sem ele, o da skin/personagem.
        public Dictionary<string, string> WorkEffects = new Dictionary<string, string>();
        public string EffectOf(Skin s, string kind)
        {
            string e;
            return WorkEffects.TryGetValue(kind, out e) ? e : EffectOf(s);
        }

        // Tipos de trabalho: "trabalhar" e quaisquer "trabalhar-algo" (um é sorteado cada vez que ele vai trabalhar).
        public List<string> WorkKinds(Skin s)
        {
            var set = s.Frames ?? Frames;
            return set.Keys.Where(k => k == "trabalhar" || k.StartsWith("trabalhar-")).OrderBy(k => k == "trabalhar" ? "" : k).ToList();
        }
        public string CarryOf(Skin s) { return s != null && s.Carry != null ? s.Carry : Carry; }

        public List<string> DefaultPhrases(string kind)
        {
            List<string> l;
            return Phrases.TryGetValue(kind, out l) ? l : new List<string>();
        }

        // Tipos de quadro: "andar", "trabalhar" e o opcional "parado" (sem ele, usa o primeiro de "andar").
        public Bitmap[] Render(Skin skin, string kind, int scale, bool flip)
        {
            List<string[]> frames;
            var set = skin.Frames ?? Frames;
            if (!set.TryGetValue(kind, out frames))
                frames = kind == "parado" ? set["andar"].Take(1).ToList() : set["andar"];
            return frames.Select(f => PixelArt.Render(f, skin.Colors, scale, flip)).ToArray();
        }
    }

    public class Config
    {
        // modo: "equipe" (bichinhos em cima do terminal), "salas" (uma sala por terminal no canto) ou "ambos"
        public string Mode = "salas";
        public bool Enabled = true;
        public double IdleMinutes = 5;
        public bool Bubbles = true;

        // equipe
        public string Character = "oompa-loompas";
        public string Skin = "classico";           // "*" = misturar todas
        public string Position = "terminal";       // terminal | rodape | topo
        public string VerbMode = "replace";        // replace | append
        public int Count = 4;
        public int Size = 3;
        public double Speed = 1.0;
        public bool TerminalPhrases = true;
        public bool TerminalTips = true;
        public Dictionary<string, object> Custom = new Dictionary<string, object>();

        // salas
        public string Theme = "escritorio";        // id do tema, ou "*" para alternar (cada terminal ganha o próximo)
        public string Corner = "direita";          // direita | esquerda (embaixo, acima da barra de tarefas)
        public int RoomSize = 2;                   // pixels de tela por ponto da sala (antes do ajuste de DPI)
        public double DoneSeconds = 10;            // quanto tempo a sala fica na tela depois que o agente termina
        public bool ShowName = true;               // nome da pasta do projeto na plaquinha da sala
        public bool RoomPermissions = true;        // aceitar/negar permissões direto na sala

        // sons (feitos no SFX Forge)
        public bool SoundDone = true, SoundAttention = true, SoundFailure = true;

        public bool CrewOn { get { return Mode == "equipe" || Mode == "ambos"; } }
        public bool RoomsOn { get { return Mode == "salas" || Mode == "ambos"; } }

        public static Config Load() { return Load(Paths.ConfigFile); }

        public static Config Load(string path)
        {
            var c = new Config();
            Dictionary<string, object> d;
            try { d = Json.LoadObject(path); }
            catch (Exception ex) { Paths.Log("config inválida: " + ex.Message); return c; }
            c.Mode = Json.Str(d, "modo", c.Mode);
            c.Theme = Json.Str(d, "tema", c.Theme);
            c.Corner = Json.Str(d, "canto", c.Corner);
            c.RoomSize = Math.Max(1, Math.Min(6, (int)Json.Num(d, "tamanhoSala", c.RoomSize)));
            c.DoneSeconds = Math.Max(2, Math.Min(120, Json.Num(d, "segundosFim", c.DoneSeconds)));
            c.ShowName = Json.Bool(d, "mostrarNome", c.ShowName);
            c.RoomPermissions = Json.Bool(d, "permissoesNaSala", c.RoomPermissions);
            c.SoundDone = Json.Bool(d, "somPronto", c.SoundDone);
            c.SoundAttention = Json.Bool(d, "somAtencao", c.SoundAttention);
            c.SoundFailure = Json.Bool(d, "somFalha", c.SoundFailure);
            c.Character = Json.Str(d, "personagem", c.Character);
            c.Skin = Json.Str(d, "skin", c.Skin);
            c.Position = Json.Str(d, "posicao", c.Position);
            c.VerbMode = Json.Str(d, "modoVerbos", c.VerbMode);
            c.Count = (int)Json.Num(d, "quantidade", c.Count);
            c.Size = (int)Json.Num(d, "tamanho", c.Size);
            c.Speed = Json.Num(d, "velocidade", c.Speed);
            c.IdleMinutes = Json.Num(d, "inatividadeMin", c.IdleMinutes);
            c.Enabled = Json.Bool(d, "ativo", c.Enabled);
            c.Bubbles = Json.Bool(d, "baloes", c.Bubbles);
            c.TerminalPhrases = Json.Bool(d, "frasesTerminal", c.TerminalPhrases);
            c.TerminalTips = Json.Bool(d, "dicasTerminal", c.TerminalTips);
            c.Custom = Json.Obj(d, "personalizacoes") ?? new Dictionary<string, object>();
            return c;
        }

        public void Save() { Save(Paths.ConfigFile); }

        public void Save(string path)
        {
            var d = new Dictionary<string, object>();
            d["ativo"] = Enabled;
            d["modo"] = Mode;
            d["personagem"] = Character;
            d["skin"] = Skin;
            d["quantidade"] = Count;
            d["velocidade"] = Speed;
            d["tamanho"] = Size;
            d["posicao"] = Position;
            d["baloes"] = Bubbles;
            d["inatividadeMin"] = IdleMinutes;
            d["frasesTerminal"] = TerminalPhrases;
            d["modoVerbos"] = VerbMode;
            d["dicasTerminal"] = TerminalTips;
            d["tema"] = Theme;
            d["canto"] = Corner;
            d["tamanhoSala"] = RoomSize;
            d["segundosFim"] = DoneSeconds;
            d["mostrarNome"] = ShowName;
            d["permissoesNaSala"] = RoomPermissions;
            d["somPronto"] = SoundDone;
            d["somAtencao"] = SoundAttention;
            d["somFalha"] = SoundFailure;
            d["personalizacoes"] = Custom;
            Json.Save(path, d);
        }

        public List<string> Phrases(Character c, string kind)
        {
            var per = Json.Obj(Custom, c.Id);
            var l = per == null ? null : Json.Strs(per, kind);
            return l ?? c.DefaultPhrases(kind);
        }

        public void SetPhrases(Character c, string kind, List<string> list)
        {
            var per = Json.Obj(Custom, c.Id);
            if (list.SequenceEqual(c.DefaultPhrases(kind)))
            {
                if (per != null) { per.Remove(kind); if (per.Count == 0) Custom.Remove(c.Id); }
                return;
            }
            if (per == null) { per = new Dictionary<string, object>(); Custom[c.Id] = per; }
            per[kind] = list.ToArray();
        }
    }

    public static class PixelArt
    {
        public static Color ParseColor(string html)
        {
            Color c;
            try { c = ColorTranslator.FromHtml(html); }
            catch { c = Color.Gray; }
            return Safe(c);
        }

        // O magenta puro é a cor "transparente" da janela; nenhum pixel pode usá-lo.
        public static Color Safe(Color c)
        {
            if (c.R == 255 && c.G == 0 && c.B == 255) return Color.FromArgb(254, 0, 254);
            return Color.FromArgb(255, c.R, c.G, c.B);
        }

        public static Bitmap Render(string[] rows, Dictionary<char, Color> colors, int scale, bool flip)
        {
            int w = rows.Max(r => r.Length), h = rows.Length;
            var bmp = new Bitmap(Math.Max(1, w * scale), Math.Max(1, h * scale), PixelFormat.Format32bppArgb);
            var brushes = new Dictionary<char, SolidBrush>();
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < rows[y].Length; x++)
                    {
                        char ch = rows[y][x];
                        if (ch == '.' || ch == ' ') continue;
                        SolidBrush b;
                        if (!brushes.TryGetValue(ch, out b))
                        {
                            Color col;
                            if (!colors.TryGetValue(ch, out col)) col = Color.Gray;
                            b = new SolidBrush(Safe(col));
                            brushes[ch] = b;
                        }
                        int dx = flip ? (w - 1 - x) : x;
                        g.FillRectangle(b, dx * scale, y * scale, scale, scale);
                    }
                }
            }
            foreach (var b in brushes.Values) b.Dispose();
            return bmp;
        }
    }

    // Objetos que os trabalhadores carregam na cabeça.
    public class Prop
    {
        public string[] Rows;
        public Dictionary<char, Color> Colors = new Dictionary<char, Color>();

        static Prop Make(string[] rows, params string[] colors)
        {
            var p = new Prop();
            p.Rows = rows;
            foreach (var c in colors) p.Colors[c[0]] = PixelArt.ParseColor(c.Substring(2));
            return p;
        }

        public static Prop Get(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "chocolate":
                    return Make(new[] { "FFFPPPPP", "BLBLBPPP", "BBBBBPPP" }, "F=#d9d9d9", "P=#7b1fa2", "B=#5b3416", "L=#7d4a22");
                case "caixa":
                    return Make(new[] { "KKKKKKKK", "KCCTTCCK", "KCCTTCCK", "KCCCCCCK", "KKKKKKKK" }, "K=#6d4c2f", "C=#d2a86e", "T=#b98a4e");
                case "peixe":
                    return Make(new[] { "B.BBBB..", "BBBBBKBB", "B.BBBB.." }, "B=#64b5f6", "K=#0d47a1");
                case "acucar":
                    return Make(new[] { "WWWW", "WWWS", "WWWS", "WSSS" }, "W=#ffffff", "S=#cfd8dc");
                case "engrenagem":
                    return Make(new[] { "..G.G..", ".GGGGG.", "GGG.GGG", ".GGGGG.", "..G.G.." }, "G=#90a4ae");
                case "gema":
                    return Make(new[] { ".CCC.", "CCWCC", ".CCC.", "..C.." }, "C=#26c6da", "W=#e0f7fa");
                case "folha":
                    return Make(new[] { ".GGG.", "GGJGG", ".GGG." }, "G=#43a047", "J=#2e7d32");
                case "claude":
                    return Make(new[] { "...O...", ".O.O.O.", "..OOO..", "OOOCOOO", "..OOO..", ".O.O.O.", "...O..." }, "O=#d97757", "C=#b85f42");
                case "terminal":
                    return Make(new[] { "KKKKKKKKK", "KGKKKKKKK", "KKGKKKKKK", "KGKKGGGKK", "KKKKKKKKK" }, "K=#262624", "G=#f0eee6");
                case "cafe":
                    return Make(new[] { ".s..s..", "..s..s.", "RRRRR..", "RWRRRRR", "RWRRR.R", "RRRRRRR", ".RRR..." }, "R=#e53935", "W=#ffcdd2", "s=#cfd8dc");
                case "osso":
                    return Make(new[] { "WW....WW", "WWWWWWWW", "WW....WW" }, "W=#f3ead3");
                case "disquete":
                    return Make(new[] { "KKKKKK", "KSSSKK", "KSSSKK", "KKKKKK", "KWWWWK", "KWWWWK" }, "K=#1e3a8a", "S=#b0bec5", "W=#eceff1");
                case "bola":
                    return Make(new[] { ".YYY.", "WYYYY", "YWWYY", "YYYWW", ".YYY." }, "Y=#c6e03a", "W=#f5f5f5");
                case "pizza":
                    return Make(new[] { "OOOOOOO", ".YRYYY.", "..YYR..", "...Y..." }, "O=#c68642", "Y=#ffd54f", "R=#d32f2f");
                case "flor":
                    return Make(new[] { ".P.P.", "PPYPP", ".PPP.", "..G..", ".GG.." }, "P=#ec407a", "Y=#ffeb3b", "G=#43a047");
                default:
                    return null;
            }
        }
    }
}
