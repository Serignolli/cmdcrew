// Idiomas do site. O português está escrito direto no HTML e nos personagens (dados.js);
// aqui fica só o inglês. Os elementos marcados com data-i18n trocam o conteúdo inteiro, e
// data-i18n-attr="atributo:chave;..." troca atributos. A escolha fica salva no navegador.
(function () {
  "use strict";

  const STORE = "cmdcrew:lang";
  const param = new URLSearchParams(location.search).get("lang");
  let saved = null;
  try { saved = localStorage.getItem(STORE); } catch { /* sem armazenamento */ }
  const browser = (navigator.language || "pt").toLowerCase().startsWith("pt") ? "pt" : "en";
  const lang = ["pt", "en"].includes(param) ? param : ["pt", "en"].includes(saved) ? saved : browser;
  document.documentElement.lang = lang === "en" ? "en" : "pt-BR";

  const EN = {
    "meta.title": "CmdCrew — a tiny pixel crew for your Claude Code",
    "meta.description": "A pixel art crew that works alongside Claude Code and lets you know when it's done. On top of the terminal or in a little room in the corner of the screen, one per terminal. Free and open source.",
    "meta.ogTitle": "CmdCrew for Claude Code",
    "meta.ogDescription": "A pixel art crew that works alongside Claude Code and lets you know when it's done.",

    "nav.rooms": "Rooms",
    "nav.crew": "Crew",
    "nav.characters": "Characters",
    "nav.faq": "FAQ",
    "nav.download": "Download",
    "nav.langLabel": "Ler em português",

    "hero.pill": "for Claude Code, Codex, Gemini, Qwen, OpenCode and Copilot · Windows, macOS and Linux · free",
    "rooms.eachTitle": "Every character has its own room.",
    "rooms.eachSub": "The dragon breathes fire in its lair, the penguin keeps the servers cold, the elf practices archery in the woods. The one on the terminal can be someone else: the dragon on top of the terminal and the dev in the room, for example.",
    "rooms.each1": "<b>Dragon's lair.</b> Torches, treasure and fire.",
    "rooms.each2": "<b>Elven woods.</b> Fireflies and target practice.",
    "rooms.each3": "<b>Frozen data center.</b> Blinking servers and snow outside.",
    "rooms.each4": "<b>Chocolate factory.</b> A chocolate river and a running conveyor.",
    "rooms.each5": "<b>Mine.</b> Pickaxe, lamp and shining rocks.",
    "rooms.each6": "<b>Claude's studio.</b> Clawd at the orange terminal.",
    "hero.title": "Claude thinks.<br><span class=\"hl\">The crew works.</span>",
    "hero.lead": "While Claude Code works on your code, a pixel art crew works right along: on top of the terminal or in a little room in the corner of the screen, one per terminal. When it's done, they celebrate and let you know. If it needs you, they call you.",
    "hero.cta": "Hire the crew",
    "hero.cta2": "See them at work",
    "hero.meta": "<span>never steals focus</span><span>tells you when it's done</span><span>100% local</span><span>open source</span>",

    "rooms.eyebrow": "Rooms mode",
    "rooms.title": "One room per terminal, in the corner of the screen.",
    "rooms.sub": "Every Claude Code session gets a little themed room near the clock, with the agent working inside. You can watch every terminal at once without getting in the way of what you're doing.",
    "rooms.shot1Alt": "Office room: the dev types and the monitor fills with code",
    "rooms.shot1": "<b>Working.</b> Project name, timer and the agent hard at work.",
    "rooms.shot2Alt": "Room asking for permission, with Allow, Deny and Terminal buttons",
    "rooms.shot2": "<b>Needs you.</b> Allow or deny right there, without opening the terminal.",
    "rooms.shot3Alt": "Room with a DONE sign and confetti",
    "rooms.shot3": "<b>Done!</b> A sign, confetti and a chime. The room stays a few seconds and leaves.",
    "rooms.feat1": "<h3>Permissions in the room</h3><p>When Claude asks to run a command or touch a file, the room shows the request with <em>Allow</em>, <em>Deny</em> and <em>Terminal</em>. The question still shows up in the terminal too: answer wherever you like.</p>",
    "rooms.feat2": "<h3>Double click, terminal</h3><p>Double click a room to bring its terminal to the front. With several Claudes open, it's the fastest way to find the one that finished.</p>",
    "rooms.feat3": "<h3>Sounds that tell you</h3><p>One sound when it's done, another when it needs you and another when something fails. Each one turns on and off on its own.</p>",
    "rooms.feat4": "<h3>Any size you like</h3><p>Smaller or bigger rooms, in the right or left corner, or dragged wherever you want. They remember where they were.</p>",

    "demo.eyebrow": "Crew mode",
    "demo.title": "Or on top of the terminal, walking back and forth.",
    "demo.sub": "Pick who's working today. Watch the spinner: it starts using the crew's phrases. You can use both modes together.",
    "demo.pickerLabel": "Pick the crew",
    "demo.termLabel": "Claude Code terminal with the crew walking on top",
    "demo.note": "A simulation made with the same drawings as the app. On your computer they stand on top of the terminal window and follow it when you drag it.",

    "gallery.eyebrow": "The crew",
    "gallery.title": "Meet the ones who'll work for you.",
    "gallery.sub": "All of these are free. Each crew has its own job, its own quirks and several colors. You can even mix everyone together.",

    "pack.title": "Support the project and take the medieval guild home.",
    "pack.sub": "A pack of high definition fantasy and RPG characters, animated frame by frame, with more animations and more detail. CmdCrew stays free and open: the pack is for those who want to support it and get a polished crew in return.",
    "pack.card4": "<h3>And more on the way</h3><p>Knights, bards and other RPG heroes join the pack as they're ready.</p><div class=\"tags\"><span>knights</span><span>bards</span><span>heroes</span></div>",
    "pack.cta": "I want to support",
    "pack.note": "The pack comes as a <code>.zip</code> file. In CmdCrew, open the settings and click <em>Add pack</em>.",

    "how.eyebrow": "How it works",
    "how.title": "They know when to show up and when to leave.",
    "how.step1": "<span class=\"n\">1</span><h3>You send a prompt</h3><p>The moment you hit Enter in Claude Code, the crew is called: it walks onto the terminal or opens that terminal's room.</p>",
    "how.step2": "<span class=\"n\">2</span><h3>They work along</h3><p>They hammer, dig, type, carry things and comment on your code in little speech bubbles. If Claude asks for permission, they call you.</p>",
    "how.step3": "<span class=\"n\">3</span><h3>Claude finishes</h3><p>They celebrate, play a chime and leave. Until the next prompt.</p>",
    "how.feat1": "<h3>Never in the way</h3><p>The windows never steal focus. The crew on the terminal lets clicks pass through; the rooms only respond to their buttons and to double clicks.</p>",
    "how.feat2": "<h3>Spinner phrases</h3><p>Claude Code's \"Thinking…\" becomes \"Breaking rocks…\", \"Watering the dependencies…\" and so on. All editable.</p>",
    "how.feat3": "<h3>Many sessions</h3><p>Each terminal gets its own room. In Crew mode, they only leave when the last Claude is done.</p>",
    "how.sfx": "<p class=\"eyebrow\">Fun fact</p><h3>The sounds were forged in <a href=\"https://github.com/Serignolli/sfx-forge\" target=\"_blank\" rel=\"noopener\">SFX Forge</a>.</h3><p>The \"done\" chime, the \"needs you\" call and the \"something broke\" thud didn't come from a sound library: they were synthesized in SFX Forge, another in-house project that creates game sound effects from what you want them to convey.</p>",
    "how.sfxDone": "▶ Done",
    "how.sfxAttention": "▶ Needs you",
    "how.sfxFail": "▶ Failed",

    "dl.title": "Hire the crew.",
    "dl.sub": "Download the installer, pick your characters and where they show up. Next time your agent gets to work, they arrive.",
    "dl.button": "Download for Windows",
    "dl.note": "Windows 10 or 11 · no admin rights and nothing else besides your agent",
    "dl.unixTitle": "On macOS and Linux, paste this in the terminal:",
    "dl.copy": "Copy",
    "dl.unixNote": "Installs without sudo and opens the setup right in the terminal. To change it later: <code>cmdcrew config</code>.",
    "dl.worksWith": "Works with",
    "dl.repo": "code on GitHub",
    "dl.steps": "<li><b>1</b> Open <code>CmdCrew-Setup.exe</code></li><li><b>2</b> Pick the crew and where it shows up</li><li><b>3</b> Click <em>Install</em></li><li><b>4</b> Send a prompt to your agent!</li>",

    "faq.eyebrow": "FAQ",
    "faq.title": "Questions the crew has heard before.",
    "faq.q1": "<summary>Will they get in my way while I work?</summary><p>No. No window steals focus. The crew on the terminal lets clicks pass through; the rooms sit in a corner and only respond to their buttons and to double clicks. Everything only shows up while Claude Code is working.</p>",
    "faq.q2": "<summary>How does approving permissions in the room work?</summary><p>When Claude Code needs permission, it calls CmdCrew (through the <code>PermissionRequest</code> hook) and the room shows the request. <em>Allow</em> and <em>Deny</em> answer Claude directly; <em>Terminal</em> hands the decision back to the terminal. The usual question still appears in the terminal too, so you can answer there if you prefer. This can be turned off in the settings.</p>",
    "faq.q3": "<summary>Does it need internet? Does it send any data?</summary><p>No. Everything runs on your computer: no account, no telemetry, no server. The app only talks to Claude Code itself, through the hooks it provides.</p>",
    "faq.q4": "<summary>Can I see the code before installing?</summary><p>You can and you should: CmdCrew is open source (MIT license). You can even build it yourself with <code>scripts\\instalar.cmd</code> from the repository, using only the compiler that already ships with Windows.</p>",
    "faq.q5": "<summary>What does it change in my agents?</summary><p>It adds start, stop, permission and notification hooks (a plugin, in OpenCode) and, in Claude Code, if you want, swaps the spinner phrases. Each config file is backed up before the first change.</p>",
    "faq.q6": "<summary>What is the Supporters Pack?</summary><p>A pack of high definition medieval and RPG characters with better animations, for those who want to support the project. The app and the regular characters stay free.</p>",
    "faq.q7": "<summary>What if I press Esc halfway through? And long tasks?</summary><p>Claude Code doesn't announce when it's interrupted, so the crew reads the conversation and notices the interruption on its own. For long tasks (builds, tests, subagents, long reasoning) the room stays until the end: it only leaves on its own after a long time with no activity at all.</p>",
    "faq.q8": "<summary>How do I change the settings or uninstall?</summary><p>Open CmdCrew from the Start menu: that's the settings screen. There you can switch the crew, choose who works in the room, turn each agent on and off or, tucked in the corner, uninstall it. You can also uninstall it from Windows' \"Installed apps\". On macOS and Linux it's <code>cmdcrew config</code> and <code>cmdcrew desinstalar</code>. It removes the hooks and restores the original spinner phrases.</p>",

    "footer.made": "Made with pixels and coffee",
    "footer.license": "MIT license",
    "footer.credit": "Built by <a href=\"https://serignolli.com\" target=\"_blank\" rel=\"noopener noreferrer\">Serignolli</a> · <a href=\"https://buymeacoffee.com/gabrielserignolli\" target=\"_blank\" rel=\"noopener noreferrer\">Buy the crew a coffee</a>",

    "pack.card1": "<h3>Code-mining dwarves</h3><p>They chip away at the repository looking for veins of gold, push the commit cart and haul the bugs out of the mine in sacks. They come with the mine: a flickering lamp, rails and shining rocks.</p><div class=\"tags\"><span>character + room</span><span>5 animations</span><span>5 colors</span></div>",
    "pack.card3": "<h3>Bug-hunting elves</h3><p>They find the error before the compiler does and hit it right in the middle of the stack trace. They come with the woods: fireflies, the moon and the target on the tree.</p><div class=\"tags\"><span>character + room</span><span>new</span><span>5 colors</span></div>",
    "pack.card1AltChar": "HD code-mining dwarf walking, mining and pushing the cart",
    "pack.card1AltRoom": "The mine: the HD dwarf mining and pushing the cart",
    "pack.card3AltChar": "HD elf archer walking and shooting the bow",
    "pack.card3AltRoom": "The woods: the HD elf shooting at the target",
    "pack.card5": "<h3>Wizards who conjure code</h3><p>They raise the staff, mutter a spell and the bug turns into purple smoke. When the magic fails, they open the grimoire. They come with the tower: cauldron, bookshelf and a starry sky.</p><div class=\"tags\"><span>character + room</span><span>new</span><span>5 colors</span></div>",
    "pack.card6": "<h3>The dev who watched the sunrise</h3><p>Runs on coffee, yawns at every build and the code still compiles. Comes with the office: monitor, bookshelf and the moon in the window.</p><div class=\"tags\"><span>character + room</span><span>new</span><span>5 colors</span></div>",
    "pack.card7": "<h3>Penguins in scarves</h3><p>They flap their wings to cool the servers and celebrate every deploy with a fish. They come with the icy data center: blinking racks and snow outside.</p><div class=\"tags\"><span>character + room</span><span>new</span><span>5 colors</span></div>",
    "pack.card8": "<h3>Treasure dragonlings</h3><p>They fly above the terminal and breathe fire on legacy code until only the good parts are left. They come with the lair: torches, a chest and the pile of gold.</p><div class=\"tags\"><span>character + room</span><span>new</span><span>5 colors</span></div>",
    "pack.card5AltChar": "HD code wizard walking with the staff, casting and reading the grimoire",
    "pack.card5AltRoom": "The tower: the HD wizard casting next to the cauldron",
    "pack.card6AltChar": "HD tired dev walking, drinking coffee, yawning and typing on the laptop",
    "pack.card6AltRoom": "The office: the HD dev drinking coffee in front of the monitor",
    "pack.card7AltChar": "HD penguin waddling, flapping its wings and catching a fish",
    "pack.card7AltRoom": "The icy data center: the HD penguin cooling the servers",
    "pack.card8AltChar": "HD dragonling flying and breathing fire",
    "pack.card8AltRoom": "The lair: the HD dragon flying over the treasure",
    "pack.swapChar": "character",
    "pack.swapRoom": "and its room",
    "support.title": "The crew is on its way!",
    "support.text": "Thanks for hiring the crew. The project is free, open and ad-free. If they make your day more fun, support the project and get the Supporters Pack: dwarves, elves, wizards and other heroes in high definition, each with its own room.",
    "support.pack": "See the Supporters Pack",
    "support.close": "Not now",

    // textos montados pelo main.js
    "js.project": "my-project",
    "js.prompts": [
      ["make the login button look nicer", "src/components/LoginButton.tsx", "\"button\"", "npm test", "Gave the button rounded corners and a softer hover."],
      ["fix the shopping cart bug", "src/cart/total.ts", "\"calculateTotal\"", "npm test cart", "The discount was applied twice. Fixed, with a new test."],
      ["write tests for the payments module", "src/payments/index.ts", "\"charge(\"", "npm test payments", "Added 14 tests covering success, decline and refund."],
      ["find out why the build broke", "package.json", "\"import \\\"\"", "npm run build", "A type wasn't exported. The build passes again."],
      ["refactor this giant file", "src/utils/helpers.js", "\"function\"", "npm run lint", "Split it into 4 smaller files without changing behavior."],
    ],
    "js.thinking": "Thinking",
    "js.frameByFrame": "✨ frame by frame",
    "js.jobs": "jobs",
    "js.colors": "colors",
    "js.tryIt": "See it on the terminal",
    "js.work": {
      faiscas: "sparks", poeira: "dust", terra: "dirt", neve: "snow", estrelas: "sparkles", magia: "magic", zzz: "naps (zzz)",
      notas: "singing ♪", claude: "sparkles ✻", codigo: "code", dados: "data", bugs: "little bugs", fumaca: "smoke",
      moedas: "coins", latidos: "barks", coracoes: "little hearts", flores: "flowers ✿",
    },
  };

  // Personagens gratuitos (personagens/*.json): nome, descrição, frases e nomes das cores.
  const CHARS_EN = {
    chocolateiros: {
      nome: "Chocolatiers",
      descricao: "Chocolate factory workers who hammer the code until it's crunchy. Every bug fixed becomes a chocolate bar and a little moral song.",
      verbos: ["Humming", "Drumming on the molds", "Stirring the chocolate river", "Seasoning the caramel", "Testing new gum", "Polishing golden wrappers", "Wrapping candies", "Composing a moral song", "Tuning the chocolate waterfall", "Hauling cocoa sacks", "Blowing soda bubbles", "Painting lollipops", "Inspecting the factory"],
      dicas: ["Chocolatiers work best with a cup of hot cocoa.", "Never taste the chocolate before the code compiles.", "Every bug gets a moral song. The chocolatiers are already writing this one."],
      falas: ["La-la-la, one more bug!", "More chocolate on line 42!", "Who touched my caramel?", "This bug tastes like raspberry", "Winning bar found!", "Mind the river!", "Almost there!", "One more chocolate commit"],
      falasFim: ["Done, boss!", "La-la-la, done!", "All wrapped up!", "Factory in order!"],
      skins: ["Classic (green hair)", "Modern (red overalls)", "Cotton candy", "Mint chocolate"],
    },
    dragao: {
      nome: "Dragon",
      descricao: "Guardian of the repository's treasure. Flies over the terminal and breathes fire on legacy code until only the good parts are left.",
      verbos: ["Breathing fire on the bug", "Hatching the feature egg", "Guarding the repo treasure", "Flying over the code", "Heating up the server", "Counting gems", "Scaring the QA knights", "Melting the legacy code", "Blowing smoke", "Sharpening claws", "Flapping wings"],
      dicas: ["Dragons don't deploy on Fridays. They set things on fire on Thursdays.", "Every dragon guards a treasure: yours is the git history.", "If the build breaks, the dragon breathes fire. If it passes, too."],
      falas: ["ROOOAR!", "Who touched my treasure?", "This bug is getting barbecued", "Hot in here, huh?", "One more gem for the pile!", "Smells like burnt code", "Fly, little dragon!", "Pfffff..."],
      falasFim: ["Treasure secured!", "All nicely toasted!", "Mission accomplished, ROAR!", "Back to the cave!"],
      skins: ["Forest green", "Volcano red", "Night purple", "Treasure gold", "Ice blue"],
    },
    elfos: {
      nome: "Elves",
      descricao: "Bug hunters from the woods. They spot a missing semicolon 200 lines away and hit it with an arrow.",
      verbos: ["Aiming at the bug", "Tuning the harp", "Gathering backlog herbs", "Reading ancient runes", "Weaving the documentation", "Walking without a sound", "Enchanting the functions", "Reading the stars", "Braiding hair", "Guarding the dependency forest"],
      dicas: ["An elf never misses. Sometimes the bug moves.", "Elvish code: beautiful, ancient and nobody knows how to change it anymore.", "The elves think your README is too short."],
      falas: ["Bullseye!", "Bug down!", "What a lovely forest of folders", "I hear a bug approaching", "One more arrow!", "This is ancient elvish code", "Quiet, I'm aiming", "The forest thanks you"],
      falasFim: ["All bugs down!", "Until the next moon!", "The forest is at peace!", "Elvish quest complete!"],
      skins: ["Forest (golden hair)", "Night (silver hair)", "Autumn (redhead)", "Snow"],
    },
    gnomos: {
      nome: "Miner dwarves",
      descricao: "Bearded dwarves who dig through the source code for rare gems: reusable functions and, every now and then, a fossilized bug.",
      verbos: ["Mining", "Hammering", "Digging for crystals", "Forging tools", "Braiding the beard", "Cutting gems", "Counting coins", "Sharpening the pickaxe", "Lighting the lamp"],
      dicas: ["All good code has at least one hidden gem.", "The dwarves say a well-mined bug is worth more than gold.", "Never argue with a dwarf holding a pickaxe."],
      falas: ["Let's dig!", "Found a crystal!", "Deeper!", "This rock is tough", "Ale for lunch", "Gold! Oh no, it's pyrite", "Nobody touches my beard"],
      falasFim: ["Shift's over, heading home!", "Mine closed!", "Treasure secured!"],
      skins: ["Classic (yellow helmet)", "Red beard (iron helmet)", "Elder (white beard)", "Gold mine"],
    },
    clawd: {
      nome: "Clawd (Claude Code)",
      descricao: "Claude Code's mascot in the flesh. Types faster than your linter and gives off sparkles every time a function compiles on the first try.",
      verbos: ["Clauding", "Thinking with tiny claws", "Reading CLAUDE.md", "Running the tests", "Compacting the context", "Grepping the repo", "Refactoring with care", "Checking the skills", "Writing the diff", "Summoning subagents"],
      dicas: ["Clawd reminds you: a good CLAUDE.md saves a lot of explaining.", "Esc interrupts Claude, but Clawd keeps working for a few minutes.", "Clawd's tip: ask for a plan before big changes."],
      falas: ["Clawd's here!", "Leave it to me", "Reading CLAUDE.md…", "Running the tests…", "May I edit this file?", "Hmm, interesting", "Context almost full!", "✻ ✳ ✢ ✶", "Found the bug!"],
      falasFim: ["Done! ✻", "Task complete", "See you next prompt!"],
      skins: ["Classic terracotta", "Cream", "Dark mode", "Mint", "Gold"],
    },
    claude: {
      nome: "Claude Sparkles",
      descricao: "Claude's icon grew little legs and ran away from the spinner. It pulses to the beat of \"Thinking…\" and carries a whole terminal on its head.",
      verbos: ["Clauding", "Shining", "Pulsing", "Radiating ideas", "Thinking in sparkles", "Spinning the spinner", "Untangling the code", "Tokenizing"],
      dicas: ["The sparkles pulse to the same beat as the spinner: ✢ ✳ ✶ ✻ ✽", "Every sparkle on screen is a happy token."],
      falas: ["✻", "Shining…", "Thinking deep", "One token at a time", "Look at the spinner!", "I'm in the terminal", "What a nice prompt"],
      falasFim: ["✻ Done!", "Shine complete", "Bye! ✳"],
      skins: ["Classic terracotta", "Cream", "Charcoal", "Peach"],
    },
    "robos-classicos": {
      nome: "Classic robots",
      descricao: "A science fiction crew: the short one that beeps in binary, the golden one that translates stack traces and the vacuum that clears the cache.",
      verbos: ["Calculating the odds", "Beeping in binary", "Vacuuming the logs", "Checking the memory bank", "Translating too many languages", "Recharging batteries", "Sweeping the repo", "Spinning the dome", "Obeying the three laws", "Soldering the circuit"],
      dicas: ["The odds of a Friday deploy going well are a thousand to 1.", "Cleaning robots also sweep up unused dependencies.", "Beep boop: this commit was approved by a dome bot."],
      falas: ["Beep boop!", "Oh dear, this will go wrong!", "Vrrrrrr...", "My circuits can't take it", "Beep beep booo!", "Dust detected!", "How unpleasant!", "Battery at 12%"],
      falasFim: ["Beep boop, done!", "Phew, I survived!", "Returning to base!", "All squeaky clean!"],
      skins: ["Dome bot (beeps)", "Red dome bot", "Golden chatterbox", "Vacuum"],
    },
    gatos: {
      nome: "Cats",
      descricao: "Senior code reviewers. They review the PR by lying on the keyboard and approve anything in exchange for a fish.",
      verbos: ["Purring", "Kneading biscuits", "Knocking things off the desk", "Chasing the mouse pointer", "Napping on the keyboard", "Sharpening claws on the code", "Ignoring bugs with elegance", "Staring into the void", "Sitting on the keyboard", "Chasing the cursor"],
      dicas: ["If it doesn't fit, the cat sits anyway.", "The cats reviewed the code and knocked three imports off the desk.", "A cat sleeping on the keyboard counts as a code review."],
      falas: ["Meow.", "This code is mine now.", "Zzz… reviewing", "Where's my snack?", "I knocked off a semicolon", "Meow? (a test is missing)", "Prrrr"],
      falasFim: ["Meow! Done.", "Now I want a treat", "Time for another nap"],
      skins: ["Orange tabby", "Black with yellow eyes", "Siamese", "Gray", "Tuxedo"],
    },
    pinguins: {
      nome: "Penguins",
      descricao: "The data center's cooling crew. They keep the processor nice and chilly, waddle between the servers and take payment in fresh fish.",
      verbos: ["Sliding", "Waddling", "Fishing for sardines", "Stacking ice cubes", "Hatching the egg", "Skating on the code", "Breaking the ice", "Cooling the processor", "Marching in line"],
      dicas: ["The penguins keep your processor at -4 °F.", "No penguins caught a cold while running this code.", "When the code freezes, the penguins call it controlled freezing."],
      falas: ["Quack quack!", "Fresh fish!", "Ice just right", "Slipped on a null", "It's cold in this server", "Let's slide!", "Penguin pose!"],
      falasFim: ["Quack! Done!", "Iceberg delivered!", "Icy mission complete!"],
      skins: ["Emperor", "Little blue penguin", "Bubblegum pink", "Gold"],
    },
    formigas: {
      nome: "Ants",
      descricao: "A whole colony marching single file and carrying bytes. Every sugar cube is a data packet delivered on time.",
      verbos: ["Anting", "Carrying crumbs", "Stacking leaves", "Digging tunnels", "Organizing the colony", "Following the pheromone trail", "Lifting 50 times their weight", "Storing sugar", "Building the anthill"],
      dicas: ["One ant does little; a thousand ants do a refactor.", "The ants already found three bug crumbs in your code.", "Single file is the oldest way to process tasks in order."],
      falas: ["One more leaf!", "Sugar ahead!", "Trail clear!", "For the queen!", "This weighs 50x me", "Tunnel almost done", "Single file!"],
      falasFim: ["Colony satisfied!", "All stored away!", "Mission accomplished, my queen!"],
      skins: ["Fire ant (red)", "Carpenter ant (black)", "Leafcutter", "Golden"],
    },
    robos: {
      nome: "Little robots",
      descricao: "Little maintenance robots that solder circuits, tighten the back-end's loose screws and beep with joy when the tests pass.",
      verbos: ["Computing", "Soldering circuits", "Tightening screws", "Recalibrating sensors", "Oiling gears", "Compiling beep-boops", "Processing bits", "Defragmenting ideas", "Updating firmware", "Beeping"],
      dicas: ["Maintenance robots recommend a git commit every 42 screws.", "Robot oil level: great. Human coffee level: unknown.", "Robots don't dream of electric sheep; they dream of passing tests."],
      falas: ["Beep boop!", "Running task 0x2A", "Loose screw detected", "Oil at 42%", "01101111 01101001!", "Recalculating route", "Humans are funny"],
      falasFim: ["Task complete. Beep!", "Entering sleep mode…", "Success: 100%"],
      skins: ["Classic silver", "Retro orange", "Neon", "Deluxe gold"],
    },
  };

  function apply() {
    document.querySelectorAll("[data-i18n]").forEach((el) => {
      const v = EN[el.dataset.i18n];
      if (lang === "en" && typeof v === "string") el.innerHTML = v;
    });
    document.querySelectorAll("[data-i18n-attr]").forEach((el) => {
      el.dataset.i18nAttr.split(";").forEach((pair) => {
        const [attr, key] = pair.split(":");
        if (lang === "en" && EN[key]) el.setAttribute(attr, EN[key]);
      });
    });
    const btn = document.getElementById("lang");
    if (btn) {
      if (lang === "en") btn.textContent = "PT";
      btn.addEventListener("click", () => {
        const next = lang === "en" ? "pt" : "en";
        try { localStorage.setItem(STORE, next); } catch { /* sem armazenamento */ }
        // recarrega para refazer os personagens e o terminal no outro idioma. O idioma vai também na URL:
        // aberto direto do disco (file://) ou com o armazenamento bloqueado, o localStorage não guarda nada.
        const url = new URL(location.href);
        url.searchParams.set("lang", next);
        location.replace(url.href);
      });
    }
  }

  // Troca os textos dos personagens (a cópia em português continua no dados.js).
  function localizeChars(list) {
    if (lang !== "en") return list;
    list.forEach((ch) => {
      const t = CHARS_EN[ch.id];
      if (!t) return;
      ["nome", "descricao", "verbos", "dicas", "falas", "falasFim"].forEach((k) => { if (t[k]) ch[k] = t[k]; });
      if (t.skins) ch.skins.forEach((s, i) => { if (t.skins[i]) s.nome = t.skins[i]; });
    });
    return list;
  }

  window.I18N = {
    lang,
    apply,
    localizeChars,
    // texto do main.js: o inglês se houver, senão o português passado
    t: (key, pt) => (lang === "en" && EN["js." + key] !== undefined ? EN["js." + key] : pt),
  };
})();
