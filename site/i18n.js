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

    "hero.pill": "for Claude Code · Windows · free and open source",
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
    "pack.card1Alt": "HD dwarf miner walking, breaking rocks and pushing the cart",
    "pack.card1": "<h3>Dwarf miners</h3><p>They break rocks, push the cart and haul the sack. Five helmet and beard colors.</p><div class=\"tags\"><span>5 animations</span><span>5 colors</span><span>mine room</span></div>",
    "pack.card2Alt": "HD garden gnome walking, watering, digging and raking",
    "pack.card2": "<h3>Garden gnomes</h3><p>They water, dig and rake while Claude works. Five hat colors.</p><div class=\"tags\"><span>5 animations</span><span>5 colors</span></div>",
    "pack.card3Alt": "Mine room with the HD dwarf breaking rocks",
    "pack.card3": "<h3>The mine, in HD</h3><p>In Rooms mode, the HD dwarf takes over the mine: a flickering lamp, rails, the cart and shining rocks.</p><div class=\"tags\"><span>themed room</span></div>",
    "pack.card4": "<h3>And more on the way</h3><p>Elf archers, a real dragon, wizards, knights and other RPG heroes join the pack as they're ready.</p><div class=\"tags\"><span>elves</span><span>dragon</span><span>wizards</span><span>knights</span></div>",
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
    "how.sfx": "<p class=\"eyebrow\">Fun fact</p><h3>The sounds were forged in SFX Forge.</h3><p>The \"done\" chime, the \"needs you\" call and the \"something broke\" thud didn't come from a sound library: they were synthesized in SFX Forge, another in-house project that creates game sound effects from what you want them to convey.</p>",
    "how.sfxDone": "▶ Done",
    "how.sfxAttention": "▶ Needs you",
    "how.sfxFail": "▶ Failed",

    "dl.title": "Hire the crew.",
    "dl.sub": "Download the installer, pick your characters and where they show up. Next time Claude Code gets to work, they arrive.",
    "dl.button": "Download for Windows",
    "dl.note": "Windows 10 or 11 · requires <a href=\"https://docs.anthropic.com/claude-code\" target=\"_blank\" rel=\"noopener\">Claude Code</a> · nothing else needed",
    "dl.repo": "code on GitHub",
    "dl.steps": "<li><b>1</b> Open <code>CmdCrew-Setup.exe</code></li><li><b>2</b> Pick the crew and where it shows up</li><li><b>3</b> Click <em>Install</em></li><li><b>4</b> Send a prompt in Claude Code!</li>",
    "dl.laterOs": "macOS and Linux",
    "dl.soon": "coming soon",

    "faq.eyebrow": "FAQ",
    "faq.title": "Questions the crew has heard before.",
    "faq.q1": "<summary>Will they get in my way while I work?</summary><p>No. No window steals focus. The crew on the terminal lets clicks pass through; the rooms sit in a corner and only respond to their buttons and to double clicks. Everything only shows up while Claude Code is working.</p>",
    "faq.q2": "<summary>How does approving permissions in the room work?</summary><p>When Claude Code needs permission, it calls CmdCrew (through the <code>PermissionRequest</code> hook) and the room shows the request. <em>Allow</em> and <em>Deny</em> answer Claude directly; <em>Terminal</em> hands the decision back to the terminal. The usual question still appears in the terminal too, so you can answer there if you prefer. This can be turned off in the settings.</p>",
    "faq.q3": "<summary>Does it need internet? Does it send any data?</summary><p>No. Everything runs on your computer: no account, no telemetry, no server. The app only talks to Claude Code itself, through the hooks it provides.</p>",
    "faq.q4": "<summary>Can I see the code before installing?</summary><p>You can and you should: CmdCrew is open source (MIT license). You can even build it yourself with <code>instalar.cmd</code> from the repository, using only the compiler that already ships with Windows.</p>",
    "faq.q5": "<summary>What does it change in my Claude Code?</summary><p>It adds start, stop, permission and notification hooks and, if you want, swaps the spinner phrases. Your <code>settings.json</code> is backed up before the first change.</p>",
    "faq.q6": "<summary>What is the Supporters Pack?</summary><p>A pack of high definition medieval and RPG characters with better animations, for those who want to support the project. The app and the regular characters stay free.</p>",
    "faq.q7": "<summary>What if I press Esc halfway through?</summary><p>Claude Code doesn't announce when it's interrupted, so the crew figures it out: after a few minutes without activity, they leave.</p>",
    "faq.q8": "<summary>How do I change the settings or uninstall?</summary><p>Open CmdCrew from the Start menu: that's the settings screen. There you can switch the crew, turn it off without uninstalling or, tucked in the corner, uninstall it. You can also uninstall it from Windows' \"Installed apps\". It removes the hooks and restores the original spinner phrases.</p>",

    "footer.made": "Made with pixels and coffee",
    "footer.license": "MIT license",
    "footer.credit": "Built by <a href=\"https://serignolli.com\" target=\"_blank\" rel=\"noopener noreferrer\">Serignolli</a> · <a href=\"https://buymeacoffee.com/gabrielserignolli\" target=\"_blank\" rel=\"noopener noreferrer\">Buy the crew a coffee</a>",

    "support.title": "The crew is on its way!",
    "support.text": "Thanks for hiring the crew. The project is free, open and ad-free. If they make your day more fun, support the project and get the Supporters Pack, with the dwarves, gnomes and other heroes in high definition.",
    "support.pack": "See the Supporters Pack",
    "support.coffee": "Buy a coffee",
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
    "oompa-loompas": {
      nome: "Oompa-Loompas",
      descricao: "The most dedicated workers in the factory. They hammer, carry chocolate bars and sing a little moral song for every bug.",
      verbos: ["Loompaing", "Doompetying", "Stirring the chocolate river", "Seasoning the caramel", "Testing three-course gum", "Polishing golden tickets", "Wrapping candies", "Composing a moral song", "Tuning the chocolate waterfall", "Hauling cocoa sacks", "Blowing soda bubbles", "Painting lollipops", "Inspecting the factory"],
      dicas: ["Oompa-loompas work best when paid in cocoa beans.", "Never chew the three-course gum before the code compiles.", "Every bug gets a moral song. The oompa-loompas are already writing this one."],
      falas: ["Oompa loompa doompety doo!", "More chocolate on line 42!", "Who touched my caramel?", "This bug tastes like raspberry", "Golden ticket found!", "Mind the river!", "Almost there!", "One more chocolate commit"],
      falasFim: ["Done, boss!", "Doompety dee!", "All wrapped up!", "Factory in order!"],
      skins: ["Classic (green hair)", "Modern (red overalls)", "Cotton candy", "Mint chocolate"],
    },
    programadores: {
      nome: "Developers",
      descricao: "The tired dev with a coffee mug, the gamer with headphones and the hoodie hacker. They type nonstop while Claude works.",
      verbos: ["Compiling", "Refactoring", "Hunting bugs", "Deploying on a Friday", "Reading the docs", "Resolving a merge conflict", "Having one more coffee", "Writing tests", "Blaming the cache", "Checking Stack Overflow", "Restarting the server", "Reviewing the pull request"],
      dicas: ["Works on my machine is a state of mind, not a guarantee.", "Every bug fixed at 6 pm on a Friday comes back Monday at 9.", "Coffee isn't a dependency. It's infrastructure."],
      falas: ["Works on my machine!", "Who touched main?", "It's just a semicolon...", "Out of coffee?!", "Found the bug!", "Hack the planet!", "git push --force? No!", "One more console.log", "It was the cache.", "Level up!"],
      falasFim: ["Deployed!", "All tests passed!", "Merge approved!", "Committed!"],
      skins: ["Tired dev (he, with coffee)", "Tired dev (she, with coffee)", "Gamer (headset and controller)", "Hacker (hoodie and shades)"],
    },
    dragao: {
      nome: "Dragon",
      descricao: "Flies over the terminal flapping its wings and breathes fire while it works. Guards the gems of your code.",
      verbos: ["Breathing fire on the bug", "Hatching the feature egg", "Guarding the repo treasure", "Flying over the code", "Heating up the server", "Counting gems", "Scaring the QA knights", "Melting the legacy code", "Blowing smoke", "Sharpening claws", "Flapping wings"],
      dicas: ["Dragons don't deploy on Fridays. They set things on fire on Thursdays.", "Every dragon guards a treasure: yours is the git history.", "If the build breaks, the dragon breathes fire. If it passes, too."],
      falas: ["ROOOAR!", "Who touched my treasure?", "This bug is getting barbecued", "Hot in here, huh?", "One more gem for the pile!", "Smells like burnt code", "Fly, little dragon!", "Pfffff..."],
      falasFim: ["Treasure secured!", "All nicely toasted!", "Mission accomplished, ROAR!", "Back to the cave!"],
      skins: ["Forest green", "Volcano red", "Night purple", "Treasure gold", "Ice blue"],
    },
    elfos: {
      nome: "Elves",
      descricao: "Nimble and organized. They tread lightly across the terminal and hit every bug with an arrow.",
      verbos: ["Aiming at the bug", "Tuning the harp", "Gathering backlog herbs", "Reading ancient runes", "Weaving the documentation", "Walking without a sound", "Enchanting the functions", "Reading the stars", "Braiding hair", "Guarding the dependency forest"],
      dicas: ["An elf never misses. Sometimes the bug moves.", "Elvish code: beautiful, ancient and nobody knows how to change it anymore.", "The elves think your README is too short."],
      falas: ["Bullseye!", "Bug down!", "What a lovely forest of folders", "I hear a bug approaching", "One more arrow!", "This is ancient elvish code", "Quiet, I'm aiming", "The forest thanks you"],
      falasFim: ["All bugs down!", "Until the next moon!", "The forest is at peace!", "Elvish quest complete!"],
      skins: ["Forest (golden hair)", "Night (silver hair)", "Autumn (redhead)", "Snow"],
    },
    gnomos: {
      nome: "Gnome miners",
      descricao: "Bearded gnomes who pickaxe through the code looking for gems (and sometimes bugs).",
      verbos: ["Mining", "Hammering", "Digging for crystals", "Forging tools", "Growing mushrooms", "Guarding the garden", "Cutting gems", "Counting coins", "Sharpening the pickaxe"],
      dicas: ["All good code has at least one hidden gem.", "The gnomes say a well-mined bug is worth more than gold.", "Never argue with a gnome holding a pickaxe."],
      falas: ["Heigh-ho!", "Found a crystal!", "Deeper!", "This rock is tough", "Mushrooms for lunch", "Gold! Oh no, it's pyrite", "Nobody touches my garden"],
      falasFim: ["Heigh-ho, it's home I go!", "Mine closed!", "Treasure secured!"],
      skins: ["Classic (red hat)", "Forest guardian", "Helmet miner", "Winter"],
    },
    clawd: {
      nome: "Clawd (Claude Code)",
      descricao: "Claude Code's little critter. Types nonstop, gives off Claude's sparkles and carries the icon on its head.",
      verbos: ["Clauding", "Thinking with tiny claws", "Reading CLAUDE.md", "Running the tests", "Compacting the context", "Grepping the repo", "Refactoring with care", "Checking the skills", "Writing the diff", "Summoning subagents"],
      dicas: ["Clawd reminds you: a good CLAUDE.md saves a lot of explaining.", "Esc interrupts Claude, but Clawd keeps working for a few minutes.", "Clawd's tip: ask for a plan before big changes."],
      falas: ["Clawd's here!", "Leave it to me", "Reading CLAUDE.md…", "Running the tests…", "May I edit this file?", "Hmm, interesting", "Context almost full!", "✻ ✳ ✢ ✶", "Found the bug!"],
      falasFim: ["Done! ✻", "Task complete", "See you next prompt!"],
      skins: ["Classic terracotta", "Cream", "Dark mode", "Mint", "Gold"],
    },
    cachorros: {
      nome: "Dogs",
      descricao: "The caramel mutt and friends. They bark at bugs, dig through the repo and bring the ball back.",
      verbos: ["Sniffing out the bug", "Burying the bone", "Fetching the ball", "Wagging the tail", "Barking at the mailman", "Digging through the repo", "Begging for treats", "Chasing their tail", "Guarding the server", "Sniffing the logs"],
      dicas: ["Who's a good boy? The code that passes the tests.", "No bones were buried in main during this deploy.", "The dogs accept payment in treats."],
      falas: ["Woof woof!", "Ball?!", "Found a bone in the code!", "Walk?", "Who's a good boy?", "Grrr... bug!", "Can I get on the couch?", "Smells like a deploy!", "Treat!", "Woof!"],
      falasFim: ["Woof! Done!", "Where's my treat?", "Walk time!", "Mission accomplished!"],
      skins: ["Caramel mutt", "Chocolate", "Dalmatian", "Gray", "Dachshund"],
    },
    claude: {
      nome: "Claude Sparkles",
      descricao: "Claude's icon grew little legs. It pulses like the terminal spinner and carries a terminal on its head.",
      verbos: ["Clauding", "Shining", "Pulsing", "Radiating ideas", "Thinking in sparkles", "Spinning the spinner", "Untangling the code", "Tokenizing"],
      dicas: ["The sparkles pulse to the same beat as the spinner: ✢ ✳ ✶ ✻ ✽", "Every sparkle on screen is a happy token."],
      falas: ["✻", "Shining…", "Thinking deep", "One token at a time", "Look at the spinner!", "I'm in the terminal", "What a nice prompt"],
      falasFim: ["✻ Done!", "Shine complete", "Bye! ✳"],
      skins: ["Classic terracotta", "Cream", "Charcoal", "Peach"],
    },
    "robos-classicos": {
      nome: "Classic robots",
      descricao: "The short one that beeps, the golden one that talks too much and the vacuum that never stops. Inspired by science fiction robots.",
      verbos: ["Calculating the odds", "Beeping in binary", "Vacuuming the logs", "Checking the memory bank", "Translating six million languages", "Recharging batteries", "Sweeping the repo", "Spinning the dome", "Obeying the three laws", "Soldering the circuit"],
      dicas: ["The odds of a Friday deploy going well are 3,720 to 1.", "Cleaning robots also sweep up unused dependencies.", "Beep boop: this commit was approved by an astromech."],
      falas: ["Beep boop!", "Oh no! We're doomed!", "Vrrrrrr...", "We seem to be made to suffer", "Beep beep booo!", "Dust detected!", "How unpleasant!", "Battery at 12%"],
      falasFim: ["Beep boop, done!", "Thank the maker!", "Returning to base!", "All squeaky clean!"],
      skins: ["Astromech (beeps)", "Red astromech", "Golden protocol droid", "Vacuum"],
    },
    gatos: {
      nome: "Cats",
      descricao: "Official code supervisors. Their job is napping on the keyboard and sometimes carrying a fish.",
      verbos: ["Purring", "Kneading biscuits", "Knocking things off the desk", "Chasing the mouse pointer", "Napping on the keyboard", "Sharpening claws on the code", "Ignoring bugs with elegance", "Staring into the void", "Sitting on the keyboard", "Chasing the cursor"],
      dicas: ["If it doesn't fit, the cat sits anyway.", "The cats reviewed the code and knocked three imports off the desk.", "A cat sleeping on the keyboard counts as a code review."],
      falas: ["Meow.", "This code is mine now.", "Zzz… reviewing", "Where's my snack?", "I knocked off a semicolon", "Meow? (a test is missing)", "Prrrr"],
      falasFim: ["Meow! Done.", "Now I want a treat", "Time for another nap"],
      skins: ["Orange tabby", "Black with yellow eyes", "Siamese", "Gray", "Tuxedo"],
    },
    pinguins: {
      nome: "Penguins",
      descricao: "Cooling engineers. They waddle back and forth with fresh fish and keep the processor nice and chilly.",
      verbos: ["Sliding", "Waddling", "Fishing for sardines", "Stacking ice cubes", "Hatching the egg", "Skating on the code", "Breaking the ice", "Cooling the processor", "Marching in line"],
      dicas: ["The penguins keep your processor at -4 °F.", "No penguins caught a cold while running this code.", "When the code freezes, the penguins call it controlled freezing."],
      falas: ["Noot noot!", "Fresh fish!", "Ice just right", "Slipped on a null", "It's cold in this server", "Let's slide!", "Smile and wave"],
      falasFim: ["Noot! Done!", "Iceberg delivered!", "Smile and wave, boys!"],
      skins: ["Emperor", "Little blue penguin", "Bubblegum pink", "Gold"],
    },
    formigas: {
      nome: "Ants",
      descricao: "A whole colony working in line: they lift leaves, carry sugar cubes and kick up dust.",
      verbos: ["Anting", "Carrying crumbs", "Stacking leaves", "Digging tunnels", "Organizing the colony", "Following the pheromone trail", "Lifting 50 times their weight", "Storing sugar", "Building the anthill"],
      dicas: ["One ant does little; a thousand ants do a refactor.", "The ants already found three bug crumbs in your code.", "Single file is the oldest way to process tasks in order."],
      falas: ["One more leaf!", "Sugar ahead!", "Trail clear!", "For the queen!", "This weighs 50x me", "Tunnel almost done", "Single file!"],
      falasFim: ["Colony satisfied!", "All stored away!", "Mission accomplished, my queen!"],
      skins: ["Fire ant (red)", "Carpenter ant (black)", "Leafcutter", "Golden"],
    },
    robos: {
      nome: "Little robots",
      descricao: "Small maintenance robots that solder circuits, throw sparks and carry gears.",
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
        // recarrega para refazer os personagens e o terminal no outro idioma (sem o ?lang= que forçaria o atual)
        const url = new URL(location.href);
        url.searchParams.delete("lang");
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
