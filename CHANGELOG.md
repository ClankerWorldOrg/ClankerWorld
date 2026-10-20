# Changelog

All notable player-facing, world-simulation, save-compatibility, deployment,
and security changes are documented here. Newer changes that are not
collected here yet are in
[changes/](changes/README.md).

## Unreleased

## v0.1.0-alpha.1 — 2026-10-17

- Waiting and fallback choices retain their existing capability outcome through shared action checks.
- Waiting uses existing typed idle and age-rejection events.
- Scouting and return choices retain their selected visible step or recorded waypoint through shared checks.
- Scouting keeps shared doorway movement checks, complete purpose references and typed native events.
- Personal collection, storage and borrowed returns retain their selected goods, quantities and destinations through shared checks.
- Delayed storage and return choices refuse a replacement House rebuilt with the same ID and position.
- Personal goods choices use shared complete references and existing typed native events.
- House guest invitations and revocations retain their selected House and named guest through shared checks.
- Guest choices refuse a rebuilt House while keeping their permission through an expansion of the original House.
- Invite and revoke references use the shared complete-reference helper without changing their spelling.
- Project input delivery, Warehouse collection and harvesting retain their selected lot, source, tool and destination through shared checks.
- Project input steps use the current typed events and shared complete transaction-key helper, keeping native fields and spelling.
- The native delivery fixture checks public stock selection and its post-transfer refusal without private planner reflection.
- Expansion deliveries, pickups and harvests retain their selected material lot or source through shared checks before applying.
- Material steps emit the current typed delivery and pickup events with their retained quantities.
- Transfer operation keys use the shared complete-reference helper with the same native spelling.
- Expansion choices retain the selected building, target footprint and available reservation inputs through shared checks.
- Expansion choice references use the shared complete-reference helper without changing their spelling.
- Retained expansion choices refuse a removed and rebuilt building even when its ID, design, site and ownership are reused.
- Construction choices retain their design, site, building identity and available material lots through shared checks and native payment.
- When selected materials become unavailable, the original builder can choose again using current stock without allowing duplicate household projects.
- Retained construction plans recheck the selected building's reachable work inputs when other work reserves them (#2411).
- Project assistance retains the requesting project, recipient and selected material lot or harvest through shared checks.
- Project helpers leave stock promised to an animal supply trip with that trip.
- Assistance preserves complete candidate references and current typed gratitude memory and fulfillment events (#2394, #2395).
- Household construction preparation retains its building need, material source, tool, harvest and return destination through shared checks.
- Building material candidates retain their complete spelling through the shared reference constructor (#2393).
- Retained construction-material choices recheck the selected work building's reachable inputs after other work reserves them (#2410).
- Recipe projects retain their selected workstation, owner and available input lots through shared checks and native job reservation.
- Remove the obsolete identity allowance for the recipe candidate now built by the shared helper (#2392).
- Knowledge collection and storage retain their exact artifact, custody and House destination through shared checks.
- Storage and collection construct their complete candidate references through the shared identity boundary (#2391).
- Voluntary knowledge reading and sharing retain the selected artifact contents and recipient through shared checks.
- Reading and sharing use the shared reference constructor and typed sharing events on current main (#2389, #2390).
- Knowledge writing and copying retain their selected contents, source, material lots and original work project through shared checks.
- Original writing refuses an unrecorded fact version before reserving materials, keeping its resulting save valid.
- Writing preserves shared typed reservations and complete candidate references; the delayed-observation regression uses controlled model work.
- Shared-food policy proposals and votes retain the original authority and ballot through shared checks. Proposal references use the shared identity helper and civic capabilities have one shared declaration.
- Shop responses retain the original shop binding, exact offer terms and personal household authority through shared checks.
- Hungry buyers keep their personally selected shop-offer response when it interrupts an unrelated owner order.
- Shop buyers retain the selected shop trip or exact goods and payment quote through shared checks.
- Hungry adults keep their admitted personal shop-food choice when it interrupts an unrelated owner order.
- Direct barter retains its chosen goods, counterpart and original offer through shared checks and shared food thresholds. Candidate references keep their full spelling through the shared identity helper.
- Market quotes and responses retain their exact goods, payment, borrowing and original offer terms through shared checks.
- Agents leave buildings through their doorways and walk outside to neighbouring buildings instead of passing through their shared walls. New buildings and expansions leave existing doorways clear, and construction routes respect the same walls. Earlier saves with blocked building doorways are refused and kept. Reverse route planning skips already reached positions before repeating terrain and wall checks.
- Market loading, deposits and physical collection retain their exact goods, quantities, origin and stall through shared checks.
- Market stall borrowing and leaving retain the selected stall, household, qualifying goods and occupancy through shared checks.
- Released Town project recovery and return retain their exact goods, quantities, release receipts and destinations through shared checks.
- Town project work and personal material gathering retain their tools, delivered budget, source and harvest through shared checks.
- Residents can keep using a still-borrowed carried tool for Town projects after leaving its household, without changing its ownership.
- New worlds no longer force straight strips of polar sea along the north and south edges. Ordinary terrain reaches the edges, and agents still stop there. Earlier generated worlds whose maps differ from regeneration cannot load in this build; their save files are kept.
- Town project pickup and delivery retain their selected Town-owned goods, exact load and destination through shared checks.
- Town project donations retain the exact personal goods, quantity, approved project and destination through shared checks.
- Town project proposals retain their selected site, entrance, budget and Port while binding the admitted name through shared native checks.
- A proposal for another Market stall stops if a resident frees a standing stall before it is submitted.
- Developer tools now let you choose terrain, resources or camp objects, click or drag across map tiles, review a preview and Apply. The server saves accepted batches as Developer edit and explains why it refuses an edit without changing the world. Typed fields are under Advanced. Earlier alpha saves use an older format and are preserved but cannot be loaded.
- Voluntary remedy work retains its agreement, selected goods, service destination and native repair plan; known-act relays retain their exact source and listener.
- Voluntary goods returns refuse stock promised to another task before transferring it.
- Agent profiles wrap activity, orders and trade notes to the available width, and scroll long histories without overlapping sections.
- Quit to Launcher now shows the same pixel door icon as the other quit buttons.
- Save World keeps its wide layout after opening, so a short world's timeline stays in view when the disk-space check finishes.
- Load World now shows a world’s save timeline as soon as you select it. Load any save on any branch directly, or use Load Save in the Pause Menu for the current world.
- Non-land voluntary offers and personal responses retain their named contributions and exact noticed terms through shared native checks; a delayed proposal cannot duplicate another accepted equipment repair.
- Retained voluntary agreements preserve native identity, response text and notice allocation checks when published.
- The Event Log leaves out routine storage, stocking, hauling and automatic source changes. Important refusals appear at most once per agent and target each world day, player order problems remain visible, and long lines wrap in full.
- Non-land findings and reopening retain their case material, judge, people and cited grounds through shared native checks.
- Adjudication uses shared bounded identity/text checks and preserves native assessment tokens and notice allocation.
- Agents refresh their choices when another agent takes the last supplies or exhausts a material source before their model call starts, avoiding repeated Blacksmith trips for unavailable materials.
- Life reviews completed together now keep the same event order when continuing after a save and reload.
- Towns allow one Store, Tailor Shop, Restaurant, Clinic and Blacksmith of each kind per eight residents, rounded up, including shops under construction. Existing shops stay when a Town is already above the limit.
- While the continuity rule is on, eligible single adults choose partners and form couples after up to two world days. Mutual choices come first, followed by the strongest relationships. The Event Log explains these pairings, and choices and deadlines survive saving. Earlier alpha saves cannot load in this build and are preserved.
- Non-land case judge consent and ballots retain their mandate, exact round and selected candidate through shared native checks; another candidate's withdrawal preserves a valid original ballot in that round.

- Validate decoded judge references through the shared identity rules while preserving complete native composite ballot tokens.
- Farmhouse households establish their first crop while starter food is still available, instead of waiting for the Town's ready food to run low before preparing and planting a field.
- Non-land case inspection, evidence, personal responses and relays retain their selected sources, people and read material through shared checks.
- Retained non-land evidence uses the shared bounded identity and historical-law excerpt rules, including typed choice reconstruction.
- Households plan work buildings only when they have reachable work to do: fields or seeds for farms, wild animals for Animal Yards, and supplies for their own recipes. Restaurants also wait for food beyond the Town's daily meals. Plans are checked again before construction, so missing supplies cannot leave an unusable new building.
- Non-land filing retains the selected known act, historical law, source and governing authority through shared native checks.
- Filing choice decoding uses the shared bounded identity rules for its Town and report references.
- Save a TypeSafe key in API keys and choose it for the Jev routine helper. Keys used by the helper are protected from deletion. New Jev selections use 1.14.0; saved older model names are kept.
- Report a problem now includes the latest game log lines and keeps the previous run's log after a forced close. Ordinary file paths stay readable while keys and secrets are blanked.
- Property filings and personal consent retain their exact assets, authority and noticed material through shared checks.
- Settled property cases skip personal-consent planning.
- Property hearing choices validate complete Town, property and target references through the native shared bounds.
- Agent profiles spread text and scrolling layout updates across frames to keep the game responsive when a profile first opens.
- Reopening requests and assessments retain the selected ruling, grounds and assessor through shared native refusal checks. Planning retains the existing informed-party/request filters and shared native identity/text/notice contracts.
- CI timing reports also appear in job logs, so reviewers can read the comparison with main without manually copying web summaries.
- Land case rulings retain their authority, outcome, cited material and property snapshot through shared native refusal checks. Ruling plans keep the pending-case response guard and shared native identity/text checks.
- Case judge consent, resignation and ballots retain the selected revision, mandate and ballot round through shared checks. An eligible held vote remains available when another candidate withdraws. Personal mandate plans skip closed cases without pending reopening.
- Case judge choices validate complete Town, round and candidate references through the native shared bounds.
- Land case inspection, observations, statements, responses and relays retain their selected revision and material through shared checks.
- A remaining adult can answer or waive a land case after another household adult leaves, while keeping the original notice and response window.
- Land case choices skip public-file and plot-observation planning while the actor is away from the notice board or disputed plot.
- Land case evidence choices validate complete Town and revision, party or recipient references through the native shared bounds.
- Land case offers filter closed cases and unrelated parties before planning while still rechecking the retained choices.
- Land hearing filings retain their authority, plot, requested outcome and statement through shared checks. Unbound filing availability follows current affected plots; original suggestions do not restrict the reply. Filing planning keeps the notice-board guard.
- Permission-sale travel and payment retain the meeting, exact personal goods and actual seller through shared checks and native identity validation.
- Accepted permission-sale collection retains its selected stock and quantity through shared consent, goods and carrying checks.
- Household permission-transfer reading and personal responses retain the exact request and party through shared checks.
- Household permission-transfer proposals retain their source, exact plot, beneficiary and goods price through shared checks, and skip proposal planning away from the notice board.
- Household land filing and expansion requests retain the household and exact plot through shared refusal checks. Missing household plots are refused before submission; retained expansion choices still wait for pending requests on alternative House tiles.
- Household land acceptance, decline and withdrawal retain the exact request and terms through shared checks.
- Opening a Council ballot preserves pending personal responses to unchanged household land terms.
- Town title claims retain their offered plot options and admitted exact tiles through shared checks.
- A waiting land-claim reply can still propose its valid plot after other residents claim the nearby suggestions.
- Personal land-claim replies without a plot receive an explained refusal before proposal submission.
- Mayoral ballots retain the selected round and candidate through shared checks without changing other residents' ballots.
- Personal office consent, resignation and additional duties retain their exact mandate or elected term through shared checks.
- Protected government proposals and responses retain their selected arrangement or voting window through shared checks. Authors can still withdraw queued proposals when an earlier withdrawal opens their vote, and accepting added duties preserves other residents' selected votes.
- Estate-default and communal-boat access proposals retain their selected policy and visitor through shared checks.
- Town law changes keep their selected scope, site, version and admitted wording when checked before proposal submission.
- Town proposal responses keep their original proposal and voting window when checked before applying.
- Public notice reads retain their board and recipient, including when saving archives older notices while a reply is pending; relay retains its selected learned notices and recipient when checked before sharing.
- Founding and resettlement keep their chosen site or Town and dependent group when checked before they apply.
- Founding after a delayed personal choice keeps saves valid when the Town adopts an owned building.
- Town admission choices keep their selected applicant, approval and dependent group when checked before they apply.
- Council ballots keep their original election round and selected candidates when checked before voting.
- Council candidacy choices retain their selected nominee and term when checked again before they apply.
- Fixed the game waiting without starting its world server on Windows. The Main Menu now says the world server is starting while it gets ready.
- The world server applies its pairing limit to every way of writing the pairing address, gives a paired device a new request check by retiring its oldest unused ones instead of refusing, and names itself in pairing replies with a random ID instead of the computer's name. A server that already has paired devices keeps its existing ID.
- Nightly downloads now include the launcher setup and its SHA-256 beside the portable game zip, so you can install the launcher directly from GitHub Releases.
- Civic visits keep their selected public notice place and explain when that visit is no longer available.
- Children who know a reported act can still personally walk to its public notice place without gaining adult civic rights.
- Revoking a paired device is saved before it takes effect and also cancels any unfinished pairing for the same key. One key can belong to only one active device, and a revoked key can be paired again only from the world server's own computer.
- Finished-tool collection keeps the quoted tool and payment while a reply waits, checking their current availability and stock promises before an exchange begins.
- The game checks that the world server on your PC is its own with a fresh challenge the server answers, instead of sending the server its launch secret, and deletes the launch secret file when the server stops cleanly.
- The world server's log names a model's chosen action only when it is one of the choices the agent was offered, and writes a fixed placeholder otherwise.
- Tool commission choices now explain when their request, worker or project changes before they can continue.
- Visits for tool requests now explain when the selected Blacksmith or recipe changes before they can continue.
- Tool-request consent choices now explain when the selected Blacksmith, recipe or request changes before they can act.
- A pending personal withdrawal can still stop the same request after ordinary production progress without replacing its current job or exchange details.
- Relocation volunteering now explains when the selected House, residents or inherited notices change before it can act.
- Replacement-care choices now explain when the selected child’s household or primary care changes before they can act.
- Resuming household work now explains when the selected job, committed inputs or knife changes before it can act.
- Household departure choices now explain when their care group or selected food allowance changes before they can act.
- Food held for an active Market stall stays protected from departure allowances, including a hold added while a choice waits.
- Lesson choices now explain when the selected teacher, request or agreed lesson changes before they can act.
- Either participant can stop the same lesson after it progresses while their reply is pending, keeping its latest progress.
- Housing choices now explain when the selected House, request or adult membership changes before an answer can act.
- Rebuilding a House with the same ID requires a fresh housing choice before a member can answer.
- Partnership choices now explain when the selected proposal or consent changes before they can act.
- Parenthood choices retain their original request, partnership, caregiver and intended home, and explain when that plan is no longer available.
- Guardian journeys keep their selected child, accepted care and home through choice checks, and explain when that journey is no longer available.
- Practical child and dependent care retain the selected person and food source, and explain when that care plan is no longer available.
- Caregiving choices and guardian orders retain the original relationship and intended home, and explain when that responsibility is no longer available.
- Guardian acceptance refuses a rebuilt House even when it reuses the original ID, without taking primary care or changing the world.
- Conversation choices retain their original participants and revision, and explain when that invitation or agreement is no longer available.
- Medicine collection and return choices retain the selected dose and last-seen patient destination, and explain when that plan is no longer available.
- Medicine treatment retains its patient and dose, sharing target and stock checks at offer, admission and apply while preserving public treatment results.
- Medical permission choices retain the selected caregiver, consent state and interrupted dose, with shared checks before offering and applying.
- Boat trip requests, queue returns and cancellations retain their Ports and requests and recheck the same plans before applying.
- Cart build-material collection retains its recipe, Blacksmith, stock and quantity, with the same full-set and physical pickup checks at every action boundary. Cart material transfers use the bounded identity factory while preserving ordinary IDs.
- Handcart repairs and supply pickups retain exact carts and material lots, with shared stock and reach checks before applying.
- Cart loading and unloading retain exact stock, cart families and quantities, with shared access and capacity checks before committing.
- Handcart pulling retains the selected attachment, cargo and destination, while blocked routes keep their existing wait behavior.
- Handcart attach, park and give choices retain the selected cart, cargo and recipient, with a reasoned refusal when they change.
- Animal permissions and trades retain exact recipients, offers, yards, payments and riding preparation; changed choices receive a reasoned refusal.
- Animal travel choices retain their horse or leading destination; dismount choices and preparation retain the exact cargo to put down at the rider’s position.
- Dismounting can put down its authorized animal-trip cargo and release that trip before splitting the load. Dropping other cargo keeps the trip; ownership and unrelated reservations stay protected.
- Retained animal supply trips keep their selected delivery or work plan, with exact carried loads and clean refusals when stock, targets or space change.
- Animal supply trips can use their own held load for the retained work or delivery step. Releasing the trip removes that access while keeping the remaining goods in place.
- Animal care, taming and saddling retain exact inputs and preparation steps, and refuse a changed stock, target, work count or route with a reason.
- Animal supply pickups retain their selected load, vessel contents, yard and task, and refuse changed stock or demand before starting a physical supply trip.
- Animal milk, product and hide choices retain their selected stock and animal through offer, acceptance and execution, and refuse a stale selection with a reason.
- Ordinary eating, household food collection and wild-food choices keep the selected serving or resource. Changed supplies and routes get plain refusals, while nourishment, serving quantities, orchard seeds and boat-passenger eating retain their existing behavior.
- Ornament choices keep the selected unit and recipient through admission and application, with plain refusals for changed stock or recipients. Wear, removal and gifts still require a fresh personal choice, and their one-unit costs and property rules remain unchanged.
- Fire tending and warmth choices keep their selected hearth, fuel and protection destination. Changed supplies or routes get a plain refusal; fuel costs, heat duration and the ability to stay in existing cover remain the same.
- Personal equipment repair now keeps the selected worn item, work site and material lots from the offered choice through pickup and repair start. Changed stock gets a plain refusal. Timed repair, orders and material costs keep their existing behavior.
- Repair trips check the complete material load before crossing water, and orders use the selected pickup order too.
- Clothing and carrying-aid choices keep their selected item. If the stock,
  equipped slot or access changes, the Event Log explains why the original
  choice was refused.
- Jug, pot and empty-vessel choices keep their selected vessel, food and load.
  When the stock, capacity or trip changes, the Event Log explains why the
  original choice was refused.

Filling a jug keeps newly created water-lot IDs within the shared derived-ID limit, while retaining the complete jug reference.
- Tree and orchard planting keep the selected seed and ground or stump. If
  another person takes the seed or the target changes, the Event Log explains
  why the original choice was refused.
- Field choices keep their selected crop, planting stock and tools. If another
  person starts that field work or the selected supplies become unavailable,
  the Event Log explains why the choice was refused.
- Keep rare mining tied to its chosen deposit, pickaxe and load, and explain when that work is no longer possible.
- Keep tool repairs tied to their selected tool, materials and preparation, and explain when that work is no longer possible.
- Refuse pending repair supplies from a Town Warehouse after the agent loses permission to collect them.
- Keep tool collection tied to the chosen shared tool and explain when it is no longer available.
- Keep Blacksmith gathering choices tied to their original source, tool and cargo preparation, and explain when that work is no longer possible.
- A held reply for a planned choice now reaches its plain refusal when that choice disappears, instead of being dropped before the selected plan is checked.
- A malformed reply keeps its safe idle fallback without the refused choice's plan metadata.
- Blacksmith delivery choices keep their selected ore or other supplies and load. A captured direct load must still be carried; storing it first refuses that delivery instead of moving stored goods remotely. The Event Log explains when that shipment is no longer available.
- Store stocking keeps its selected goods and load. The Event Log explains when those goods or the Store shelf become unavailable.
- Workstation supply choices keep their chosen building, goods, source and gathering tool. The Event Log explains when that supply plan is no longer possible.
- Farm stock choices keep the selected load and storage destination. If that stock or trip becomes unavailable, the Event Log explains why the agent waited.
- Food-space recovery keeps its selected supplies, storage destination and orchard seed release through the choice. The Event Log explains when that load is no longer available.
- Household stock choices keep their selected supplies and load through delivery or recovery. If that shipment or its destination becomes unavailable, the Event Log explains why the agent waited.
- Town stock choices keep their selected goods, load and Warehouse. If the goods, vessel contents or place change before the action runs, the Event Log explains why the agent waited.
- Children keep the goods and destination they originally chose. If that choice becomes unavailable before they act, the Event Log explains why they waited. This raises the alpha save format; earlier saves are refused and preserved without migration.
- Agent need states and activity labels come from the world server. The Cold label now follows the world's urgent-warmth threshold.
- Long names fit their headings and remain available in full on hover. World refreshes keep the text and selections you are editing.
- Owner actions share a deadline and discard replies from screens you have closed, so old results cannot overwrite your current work.
- Event Log descriptions name the correct animal and Town, and retain both traders in a cancelled Market trade.
- The game checks that the world server uses the same Event Log format before refreshing. Update remote servers alongside the game.
- World events keep their people, places and outcomes in structured records. Earlier alpha saves are no longer supported by this save format; their files are preserved.
- Event Log descriptions now come from the world server. Update remote servers alongside the game; an older server prompts for an update and keeps the last confirmed view.
- Saves keep typed tasks for each agent's current orders and six most recent ended orders. Older alpha saves are kept on disk but cannot be loaded.
- Retiring an old order preserves valid exploration saves and marks paid construction complete even when its receipt leaves recent history.
- The inventory save format now records promises on goods. Earlier alpha saves cannot load in this build and are kept without migration.
- Storing the remainder of a personal load no longer fails because an earlier partial move already created a split lot.
- Saves move older finished production jobs into history while keeping current work and its proof. Building observations show current work and a recent window of finished jobs. Earlier alpha saves cannot be opened by this build; their files are kept.
- Older repair records move into saved history so they no longer grow every live save; current work and needed proof stay available. Earlier alpha saves cannot load in this build and are kept unchanged.
- Crowded tile cards stay inside the window and let you scroll through all their facts and goods while Close remains visible.
- Keep the world responsive when a child or another resident blocks a passage while agents look for materials to bring back to a workshop.
- Restarting after an interrupted Load Save now restores matching model choices, autosave settings and save history before the world becomes available. Its recovery copies stay protected until recovery finishes.
- Games packaged with their program in a subfolder can find their bundled server, including local developer runs.
- The Event Log explains changes to shared household food access without attributing them to a Town council.
- Old Council election attempts and their notices move into saved history, keeping recent results and learned notices available without growing every live checkpoint. Earlier alpha saves use an unsupported format and are kept unchanged.
- Tile cards show the named owner of goods on the ground, including personally owned goods and later name changes.
- Land-use sale prices show readable goods names while keeping the agreed goods and quantity unchanged.
- Overlapping game launches now protect the local server's startup files, so a surviving server can still be reused after the owning game closes unexpectedly.
- Quitting after an interrupted newborn model setup saves the child's original key choice before shutdown. If setup still cannot be saved, the game stays open and paused for retry.
- An agent's final will can recall their saved private memory summaries, including after the routine helper is turned off.
- Death during an unanswered partnership proposal no longer uses the survivor's chance to reconsider their identity after losing a partner or calls them a partner in will context. An accepted partner's death still gives that opportunity.
- Leaving a household takes a food allowance from available household supplies and preserves food still on sale at a housemate's borrowed Market stall.
- Saved keys used by the world's routine helper cannot be deleted until you choose another helper key or turn the helper off.
- Starter clay is placed on a freshwater bank reachable from the chosen first Town. An island without a usable bank records the existing clay blocker instead of placing clay on another landmass.
- Animal care and saddle supply trips check the loaded route before picking up goods, so a jug or saddle cannot leave the carrier too heavily loaded to swim back to the animal.
- Tab and Shift+Tab reach the controls for a lost request from Profile and Main Menu; Enter retries or discards it, then focus returns to the screen underneath.
- Heirs keep recalling final words after many newer experiences. Other protected social memories use the same recall protection as permanent name changes.
- The routine helper avoids repeating importance assessments that cannot fit in its saved scoring index.
- Replacing an agent's current order keeps queued orders, their targets and remaining amounts, and runs the replacement first.
- Households wait for usable building materials and keep gathering shortages visible while their wood is still on sale at a borrowed Market stall.
- Load World and startup recovery now check a saved OpenAI Decisions helper's key, so a world with missing helper setup is no longer advertised as usable.
- Reducing autosave rotation or turning it off keeps usable recovery copies when a newer autosave is damaged. Damaged copies stay preserved.
- Town donations can now complete agreed public-service work when an agent has a long saved identity, without refusing the donation or failing a strict save.
- Town hearing records explain why a judge left or a case election was cancelled, including what happens next for the case.
- Quantity-limited delivery orders now keep track of the goods they pick up when long inventory identifiers are shortened, so the requested delivery can finish after saving and loading.
- The save format and save file name changed during a code cleanup, so the game no longer finds or opens saves from earlier builds and starts as if there were none. Older saves stay on disk; nothing is deleted. A world server on another machine needs a fresh install with this build, and its devices need pairing again. The game now keeps its world server in a `server` folder and its log in `logs\server.log`, and the launcher still recognizes game versions installed before this change. Agents now collect household food and set spare supplies down at their household's storage pile.
- Text the world server writes for the Towns page, agent and building cards, Load World and the model test now uses plain words: mayors' duties, vacancies, election and handover results, Town project and order blockers, joining a Town, trade cancellations and model test results no longer mention mandates, incumbents, adjudicators, admission, providers, paid checks or checkpoints.
- Menus, the pairing screen and building details now use clearer, consistent words. The Main Menu offers **Connect to a world server**, pairing explains each step without technical terms, Building Details calls its section **Manage building**, land details always say **use right**, and Developer tools calls its aging control **Aging override**.
- Connection, save and recovery messages, model settings, the Towns page, hearing records and several Event Log lines now use plain words instead of developer terms. For example, hearings name a **Judge** instead of an adjudicator, and say someone "chose not to respond" instead of "explicitly waived a response".
- After a lost server reply, the game automatically checks your last instruction, cancellation or world edit when the same running world reconnects. Normal recovery controls let you review or discard an unresolved request without Developer mode.
- The security policy now explains private vulnerability reporting, supported builds and which problems are in scope.
- Agent choices and action routes now use ordered registrations, preserving existing choice order, prefixes, saves and replay while preparing the remaining runtime systems for extraction.
- Remove the unused one-agent fixture world and prototype faction, currency and culture save records. The host now rejects fixture mode; normal private worlds keep their existing gameplay. Earlier alpha saves are refused and preserved without migration because the save format changes.
- Hover over a clipped building name on its quick card or Details to read it in full.
- Agents using better tools can gather Blacksmith supplies without getting stuck on a harvest that exceeds their carrying limit. Larger loads still use the full tool bonus when they fit.
- Hover over an agent's name on their quick card or Profile to read the full current name when it is clipped.
- Install ClankerWorld with a per-user Windows setup, including the launcher, current game and shortcuts; uninstalling keeps your game versions, worlds, keys and model-call count.
- The world server now refuses oversized requests before reading them. Its local approval routes answer only when addressed as `127.0.0.1`, `[::1]` or `localhost`, as the game already does.
- Reduce temporary allocations when agents consider building sites in grown Towns, while preserving their choices and placement rules.
- The game's own world server no longer picks up model API keys or ClankerWorld settings from environment variables on your PC; save keys in **Settings → Game → API keys** instead.
- Long agent names no longer push the Close buttons in thoughts, orders and conversation readers off screen. Hover over a shortened heading to read it in full.
- Hover world and save list cards to read their full names, including names shortened to fit.
- Long item names in building storage fit inside their slots. Hovering still shows the full name and amount.
- The agent Profile shows at least 1% condition for clothing and carrying aids with any condition left, distinguishing nearly worn-out items from broken ones.
- Developer mode in the launcher offers a separate test data folder, starting a locally built portable game, and detailed logs for a run. Test worlds, keys and settings stay apart from your usual data; older builds that cannot honor these options are refused.
- When one agent's action causes a game error, the agent waits and their current order is cancelled while the world keeps running. The Event Log explains the refusal, and Developer tools count refused actions. The same choice is blocked for that agent for one world day.
- World placement and route checks share named occupancy layers while retaining their existing obstacle sets, order, saved state and replay behavior.
- New Towns start with an empty mayor's office for land disputes and non-land cases. A willing adult can stand for both duties and start the first election without first creating the office by a government-change vote. Residents can still remove or narrow its duties through that protected vote. Older alpha saves are refused and preserved without migration.
- Malformed Select World requests now receive a clear refusal instead of a server error, leaving the saved world unchanged.
- Agents check whether goods can reach their destination before collecting animal-yard water, planting seeds, medicine, repair supplies, cart materials or food for a child. Swimming pickups keep within the load limit. Ordinary milk uses the shared Store stocking checks for reachable sources and loaded returns.
- Late model replies and ignored model failures record known world time rather than a run number in cognition diagnostics, including after reload.
- Towns now start council elections at four living adult residents, so an ordinary new Town opens its first election on day 1. At three or fewer adults, all adults govern until the Town has four again; elections still require willing candidates and actual votes.
- Quit to Launcher and Open in another version keep your world selected in Load World after Play, ready to open when you choose.
- A parent can no longer remove the last surname their minor child can use.
  The refused rename leaves names and memories unchanged, and the child can
  still be named or renamed using the remaining biological parent's surname.
- Milk needs no jug and can be carried, stored, cooked, gifted and traded like other food. Water jugs hold fresh water only. This changes the save format; older alpha saves will not load.
- Mod content IDs share a 512-character limit checked when a package is imported. Accepted IDs work in crafting, wills, land-sale prices and resource knowledge; longer IDs are refused with a clear reason.
- Prepared ticks preserve unchanged environment snapshot identities, and restoring a saved world initializes its saved services without running new-world genesis first.
- Walking-route searches avoid allocating a neighbor iterator for each visited tile while preserving their movement and construction rules.
- When households decide to make, grow or buy supplies, they ignore goods in broken containers, promised for delivery or held at a borrowed market stall. Paper reserved for their own books still counts toward their writing supply.
- The simulation's settlement council and Town civic rules now run through a
  separate civic system with immutable saved-state snapshots. Civic-history
  lookups can be reused across ticks. Gameplay, checkpoint format, events and
  replay behavior are unchanged.
- Agents can collect book, cart-repair and tool-repair supplies in a different order when taking nearby materials first would leave them too heavily loaded to reach the next ingredient.
- Keep the selected save name in view when the timeline finishes resizing, while allowing you to scroll elsewhere.
- Failed New World creation no longer leaves an extra world in Load World when
  the active checkpoint cannot be replaced. The previous world and settings
  remain selected, and creation can be retried after the write problem is fixed.
- IDs derived by births, family records, stock splits and repeated milk sales now stay within one shared limit. Memories and work receipts keep their complete source links and remain eligible for recall. Short IDs keep their existing spelling. This changes the alpha save format; older saves are preserved and refused without migration.
- Native-born adults can ask for household use of Town land even when their family identity is long, including after forming their own household. Their requests still need household consent and Council approval before granting any land rights.
- Relaunching the game retries brief timeouts from its local world server before
  reporting a startup failure. It still checks the running server's build and
  saved companion secret before reusing it.
- Town law decisions, government votes and handovers, and mayoral elections or
  office changes now appear in the Event Log, with details on the Towns page.
- Town layout searches avoid allocating a neighbor iterator for every visited tile while preserving their route and construction rules.
- Native-born adults can now recall newly completed personal barter exchanges
when their generated IDs make the cooperation memory key exceed 128 characters.
Both traders retain their own exchange memory, with unchanged consent, stock
transfers and trust gains. Existing saved memory keys are not migrated.
- Newly inherited final words remain available in heirs' later decisions, including heirs born in the world.
- Dead household animals no longer offer gifts, sales, or new care and riding permissions. Existing permissions can still be revoked.
- Native-born residents can recall newly received help with project materials after saving and reopening, while retaining the helper's identity and the original gratitude and trust effects.
- Overwriting a named save removes the replaced checkpoint file after the new version is saved, while keeping the intended recovery copy.
- Adults born in the world can recall newly completed lessons, with their teacher and learned skill preserved through save/load.
- The local host finishes its save confirmation before exiting when the game asks it to shut down.
- Households founded by native-born adults can receive recovered Town buildings and have expired land permissions ended, renewed or amended through the public hearing.
- With Developer mode on, the launcher also offers nightly builds of main, published as GitHub pre-releases. The seven newest are kept.
- Leaving a household keeps the world running when the food allowance comes from its Store. The food stays there as personal property until it is collected, including after saving and loading.
- Repeated milk sales keep saving and loading correctly when the same portion has a long trading history.
- Load World marks a world that another game version saved and offers a red Open in <version> button, which reopens the launcher with that version chosen.
- Blacksmith household members can buy needed iron or gold while a housemate sells the household's existing metal at a borrowed Market stall. The housemate's sale stock stays protected.
- Deleting a world is no longer blocked by damaged saves or saves from an older version recorded as belonging to another world. Those saves stay on disk.
- Storage and delivery tasks share one calculation that leaves room for promised inbound loads, including vessels and their contents. Private Silos hold 64 units per footprint tile; a full 1×1 Silo keeps surplus farm stock outside until space becomes available.
- F12 Developer tools now open only in Developer mode: turn it on in the launcher, or start the game with `--developer-mode`. A game the launcher started has Quit to Launcher on its Main Menu.
- Construction choices skip unnecessary resource checks in grown Towns.
- **Report a problem**, in **Settings → Game → Build**, saves a zip with the game's and its world server's latest logs and a short summary to send with a bug report. Keys, saves and model replies are never included, and anything that looks like a key is blanked.
- A Windows launcher installs ClankerWorld versions from the published releases, checks each download before installing it, repairs or removes versions without touching your worlds, and starts the version you choose. It never updates itself.
- Cooking continues and saves normally when a carried knife has a long inventory history from splitting or moving a stack. Completing a meal still spends its real ingredients and wears one knife once.
- Households founded by native-born adults can receive offers to transfer or buy existing land use rights, with each affected adult still required to read and accept the terms separately.
- A child can leave with its caregiver or move to an accepted guardian while carrying household goods without stopping the world. Unfinished deliveries keep their original household owner and physical carrier.
- Later-generation relatives can start conversations with each other without halting the world when their combined internal identities are long.
- Later-generation adults can fetch animal-care supplies and save their trips with complete identities and physical custody.
- A Town can inherit food left in a House when no household heir survives without halting the world. The goods keep their original location, quantity and Town ownership through saving and loading.
- A portable Windows zip that includes its own world server is now built with code changes, so the game can run entirely on your PC.
- Saves and the world list now record which game version last saved each world, and a save this version cannot open names the version that last saved it.
- Material-gathering orders now finish and save for later-generation adults, keeping their full identities and gathered supplies.
- Later-generation adults can propose and accept household land-permission transfers without their full identities stopping the world's saves.
- Cancel task now works in the game client for agents born in the world, including retry after a lost reply. The pending request keeps the agent's complete identity and its original order and world.
- A game folder with its own world server now starts it, pairs with it and stops it for you, keeping saves and keys in your Windows user folder. It verifies a running host with its saved secret before reusing it. If the host refuses to save and stop, the game stays open for retry.
- Recipes that would produce fresh water without a jug are refused before taking materials. Previously started batches cancel safely and free their materials, so they no longer halt the world.
- Milk offers now save and reload with payment lots whose IDs grew through earlier stock moves, keeping the world running and preserving the exact exchange.
- Claude token totals now include provider-reported usage for refused, cut-off and malformed model replies, including Test model checks. Failed calls still count as one attempt, and invalid token counts no longer leave those attempts unfinished.
- Stores and Markets can save exchanges and stock receipts with valid long mod resource names. Paying with such a resource no longer halts the world at its next checkpoint; saves keep the full name and exact inventory bindings.
- An agent can collect their own stored or dropped work tool after leaving a household without halting the world. The tool keeps its owner, and normal access, reservations and carrying limits still apply.
- Accepted recipes that produce several jugs, pots or handcarts now complete with separate physical vessels instead of halting the world.
- The host now saves the world when it is stopped, and a host the game starts on your own PC can only be paired with or stopped by that game. The companion pauses and saves before quitting; if saving fails, it stays open so the game can retry.
- Warehouse and handcart material collection now share stock and route checks, and recheck the selected goods before pickup.
- Depositing goods at a Market remains saveable after many splits and moves of the same stack, including partial deliveries. Resuming and loading a checkpoint preserve the actual deposited stock and its owner.
- Land fertility now uses the rainfall of the selected generated map, so soil inspection and farming agree with that map when generation chose its second or third candidate.
- A refused instruction or order cancellation no longer leaves the owner controls locked after the host answers. Requests with an uncertain result remain available for safe retry.
- Tool-making requests for recipes with long content names can now be saved and reloaded without halting the world. Requests keep the full recipe identity, including after withdrawal.
- Law amendment and repeal proposals keep complete emoji when shortened, so their saves remain loadable. Named saves and autosaves validate the written checkpoint before publication; a rejected overwrite keeps the previous save and its history intact.
- Recovery cleanup keeps the latest usable recovery copies even when a newer copy cannot be loaded. Unloadable copies remain preserved for investigation.
- Overwriting a save with a long name that includes emoji or accents keeps whole characters in the recovery copy's name, instead of storing a broken character.
- Guardians keep complete characters in shortened placement notes when a renamed child's name includes emoji.
- Land-hearing evidence summaries supplied to judges now keep complete emoji at the length limit, preserving full statements and their inspected sources.
- Town notice summaries supplied to agents now keep complete emoji at their length limits, preserving the full posted text and who read or relayed it.
- Memory excerpts supplied to an agent's model or Jev now keep complete emoji at the length limit, while the original saved memory stays unchanged.
- Routine helpers can choose shorter summaries of an agent’s old private experiences during an existing call. Summaries keep their original sources, hearsay and uncertainty, survive saves, and remain available for recall when the helper is off. Older alpha saves are refused and preserved.
- Building and crafting entries in the Event Log show their building or recipe names, including older recorded work, instead of “Sha256.” ([#1289](https://github.com/compoodment/ClankerWorld/issues/1289))
- A handcart that is nearly worn out but can still be pulled now shows as pulled or parked, with at least 1% condition, instead of as broken. Only a cart at zero condition is shown as broken.
- Handcarts and rowing boats keep their own look at medium map zoom: carts no longer turn into the cart icon, and boats no longer shrink their close-up picture. They use smaller drawings made for that zoom, in every direction, loaded or empty, rowing or at rest.
- Town resident portrait tooltips now show an accepted rename at the next refresh, while keeping the list scroll position and reusing unchanged portraits.
- Map name tags keep whole accented letters, flags and joined emoji in bounded given names. Longer given names still use an initial, and names still appear only on hover or selection.
- A new Town's first completed building now connects it to the nearest Town by a legal Road, reusing existing streets and bridging narrow rivers where possible.
- This alpha records the first completion in saves; older alpha saves are refused and kept without migration.
- Agents can now use Claude models from Anthropic. Choose Anthropic in Add an agent or an agent's Model panel, and save its key under API keys. The short list is Claude Opus 5.5, Claude Sonnet 5.5 and Claude Haiku 5.5, and a new agent starts on Claude Haiku 5.5. Every agent on a hosted model also has a **Thinking** setting: Model default, Low, Medium or High. More thinking takes longer and uses more paid tokens, and Model default keeps each model's own setting.
- Building cards and result notices now distinguish property recovery and grants from use-permission rulings. Cards retain the current recorded owner alongside case history, and notices with missing case context use neutral wording.
- Roofs now hold observed snow along their own slopes, with shaded faces covered and sunny slopes dusted; it melts with ground snow while flues, outlines, shadows and open yards stay clear.
- Final words preserve valid Unicode letters and emoji in the saved will and the heirs' private memories.
- Deaths leave wooden crosses or headstones on open ground at close and mid zoom. They stay for a game year and fade over the following day. [#1476](https://github.com/compoodment/ClankerWorld/issues/1476)
- Buildings finishing on screen show a ring of settling dust and a few roof twinkles once at close and mid zoom. Pause holds the moment, and reopening the world does not replay it.
- Rain, storms and snow fade in and out over about a second when regional weather changes, and stop fading while paused. [#1469](https://github.com/compoodment/ClankerWorld/issues/1469)
- Construction layout and shared route searches reuse the foot-step legality they just checked when computing Road costs, preserving offered choices and placement checks.
- Find and agent/building jumps now glide to their destination, and zoom steps ease while keeping the pointed-at tile under the cursor. New requests start from the shown view; dragging and keyboard panning remain immediate. [#1467](https://github.com/compoodment/ClankerWorld/issues/1467)
- Dawn adds a soft rose glow and dusk a gold one, following seasonal twilight and fading away in full day and night.
- Snowfall covers open ground with grass tips and trodden Roads showing, then melts; walking agents and animals leave footprints that fade within a game hour.
- Stored goods now show as log, crate and sack piles beside store doors at close and mid zoom. Piles grow with recorded storage fullness, disappear when empty, and stay clear of Roads, doorsteps, buildings and the Town edge.
- Agents and animals now glide between reported tiles with smooth walking frames and a small bob; boats and handcarts glide too. Clicking follows the moving figure, pausing freezes it, and long moves or reloads snap into place.
- Occupied Houses and working Blacksmith forges now emit soft grey chimney smoke at close and mid zoom. Smoke rises, drifts east and fades, and freezes while the world is paused.
- Autumn adds a few fallen leaves beneath mature broadleaf and fruiting or picked orchard trees at close and mid zoom. They stay off Roads and water and vanish when autumn ends. [#1464](https://github.com/compoodment/ClankerWorld/issues/1464)
- Weather uses the approved pixel-streak look: straight rain with tiny landing bursts, gusting stepped storm streaks and wind-blown snowflake crosses. Cloud haze and soft flashes keep their existing controls. [#1427](https://github.com/compoodment/ClankerWorld/issues/1427)
- World Info keeps its unchanged statistic tiles while the date and local weather update, reducing work while the panel is closed and keeping its latest information ready to show.
- Wild animals can reach another fresh-water shore when people or animals crowd the preferred shore's approach.
- Dismounting and making room for food can move unreserved surplus from a partly claimed loose stack. Promised units and whole reserved vessel families stay protected.
- Household equipment, planting supplies and animal-care cargo share pickup checks for usable stock, reserved vessels, delivery and Market promises, ownership, whole-jug carrying space and reachable paths.
- Household milk restocking leaves a housemate's jug and milk at their actively borrowed Market stall. After the seller leaves, the same jug can be restocked normally.
- Keep Towns-page boat trip rows pointed at the current arrival Port during recovery, and show when a boat is returning or waiting for a safe arrival.
- Conversation summaries and bubble tooltips keep whole accented letters, flags and joined emoji when shortening public speech. The full heard turn remains in Show history.
- New World previews keep generating and packing the same maps while the current world keeps ticking. Concurrent previews remain bounded, and creating or selecting a world still requires a confirmed pause and its normal save/provider transaction. [#1388](https://github.com/compoodment/ClankerWorld/issues/1388)
- Animal breeding counts delivered feed and water only when their pot or jug is usable. Broken vessels cannot supply a new pregnancy; healthy available stock still can.
- Hungry children with full loads can set aside spare unreserved supplies before collecting household food. Equipment, maps, reservations and delivery loads remain protected, and infants still rely on caregivers.
- The Event Log reads more clearly. Lines now say who was involved, such as "Ada and Bram became partners." or "Corin needs a guardian.", and use plain words instead of legal or code terms. Marriages, skills learned, animal births and deaths, and scouting trips now appear. Routine steps that a later line already covers, such as meals, forage trips, cart loading and each Town project work shift, no longer fill the log.
- Unchanged Agents list refreshes keep their existing cards and portraits, reducing work while the world is paused and preserving browsing position.
- Hungry children can drink fresh milk from their own usable carried jugs, gaining fullness and keeping the jug intact.
- Preparing births leave household food on actively borrowed Market stalls untouched. The food gate and parent guidance exclude sale stock until borrowing ends; usable household food elsewhere can still pay for the birth.
- Developer tools' host tick time now includes applying completed conversation replies and committing the world. Waiting for runtime gates, model replies between ticks and checkpoint saves remain outside the readout.
- Households can make replacement writing paper while their existing sheets are being sold at a borrowed Market stall, without taking the sale stock.
- Conversation belief history is validated without repeated full-ledger scans, while retaining every private belief and its correction history.
- Agents no longer drink milk from jugs assigned to a building delivery. The promised milk reaches its destination intact; unassigned carried milk remains drinkable and keeps its reusable jug.
- Tab and Shift+Tab reach controls within open menus and panels, including numeric fields, Rename and overflowing readers and confirmation dialogs, with visible focus. Focused scrollbars use arrows to scroll. Keyboard Profile actions enter its controls, and closing panels returns focus to their opener.
- Press K to browse world tiles with arrows and select agents, buildings (including street lanterns) or ground, or place with Enter. The World Map atlas also moves the camera with arrows.
- Households can sell an existing land use right for an exact goods price. Every affected adult must accept, then a buyer brings real goods to meet a seller adult at the notice place. Payment and the use right move together; Town title and private buildings stay with their holders. Details show pending payment and completed receipts, which survive saving and loading.
- Show removed Town Halls as “Built, later removed” while keeping their paid construction history, retain project names in older Event Log lines, and explain when blocked Town work can retry.
- Households cook replacement meals when their finished food is trapped in a broken pot. Usable stored meals still count toward their normal reserve, and explicit cooking orders still work.
- Animal care protects the family's usable meals even when other food is trapped in a broken pot or committed to an actively borrowed Market stall. Healthy-pot food, returned stall stock and grain feed retain their normal behavior.
- Renaming an agent now gives them a dated, private permanent memory of their new name. It stays available to their model and is marked Permanent in Memories, including after save/load. A married surname change records it for both people whose names change; other agents are not told.
- New worlds no longer start with an unused Copper currency, a 100-unit household wallet or a dormant theft-fine rule. Trade continues to use real goods and existing ownership checks.
- The continuity rule now counts eligible adult couples instead of everyone who is not an elder. Unpartnered newcomers and children no longer switch it off; the Event Log explains the couple count. The provisional threshold is four couples, and the two-day postponement stays the same.
- Agents offer tree planting or stump replanting from a shared seed only when they have room to collect it. An agent already carrying its seed can still plant with a full load.
- Agents with materials but no legal building site are pointed to available household land requests and Council land claims; the House waiting message explains these routes and the need for approval.
- Town Councils can vote on a supported default estate law giving the Town a stated share when a resident dies without a valid will. Only later deaths use the rule, and valid wills keep priority. When no household heir survives, fallback goods now belong to the deceased resident's actual Town. In a default estate, eligible goods enter its Warehouse while space allows and remain available through normal abandoned-Town salvage. Books, maps and other belongings keep their identity and quantities. Older alpha saves are refused and preserved.
- Agents can follow **Propose marriage** with their current partner, preserving each person’s consent and waiting for the shared surname before reporting marriage completion. The order clears a recovered eligibility blocker while the personal conversation continues.
- Healthy, warm agents carrying a light load can swim wider rivers and lakes, moving much more slowly than wading and losing warmth. Swimming has distinct movement and a label on the map and survives save/reload. Cold, ill or heavily loaded agents do not start a swim, and someone already swimming can still reach shore if their condition changes. Carts and animals keep their existing movement rules; the sea still requires a boat. Speed, warmth loss and the four-unit load limit are provisional for playtesting.
- Agents searching for project materials keep looking when a known source across water would leave the harvested load unable to return home or to the worksite.
- Save World can preview and confirm cleanup of older recovery copies, keeping a provisional number per save and its latest verified recovery. Manual saves, unclassified copies and unverifiable checkpoints are kept; cleanup never runs automatically.
- Save branches show the name of the save they started from, such as From Before the flood. The label stays with the branch if the source save changes later; shortened names keep whole visible characters, including emoji and accents. Hover a shortened label to read it in full.
- Adults and elders can follow an order to repair an owned worn or broken cart, including one named by its tile. They collect real permitted supplies, reach the selected cart and spend one carried wood, iron fitting and rope. Orders keep their target and progress through saves and interruptions, preserve cargo and ownership, and explain missing supplies or physical blockers. Cancellation keeps any collected goods.
- Light and Dark themes keep text readable in hover, pressed, disabled, placeholder and selected states. Save-branch and warning badge text uses contrasting ink, and deceased family profiles keep full text opacity with an explicit died label and tooltip when names are clipped.
- Adults and elders can follow orders to load a requested quantity of loose goods into an owned handcart, or unload into carrying space or onto its ground tile. Orders keep their cart and current cargo stack across saves and interruptions, count real transfers, and respect ownership, reservations, food protection and space. Broken carts can still unload damaged cargo. Older alpha saves stop loading and are kept unchanged.
- Warn when the server's save disk has little free space, before manual saves and while autosaves run, while continuing to attempt saving and automatic recovery.
- Agents can look for needed food, project materials, forests or mountains. Untargeted gathering orders remember what they are searching for and resume the real task after a usable discovery.
- Earlier alpha saves no longer load; their files are kept.
- If the latest saved state cannot open after a restart, a recovery screen offers the last usable autosave from that world's history. Recovery keeps the refused file and other saves, explains the missing later progress, and opens the recovered world paused.
- Later-generation adults can take part in land and property hearings, give personal consent and stand in case elections without their ancestry-based identities preventing the hearing from saving.
- Agents keep their chosen physical task going while a model reply is pending. Built-in rules handle urgent hunger or cold and practical care for nearby dependants, while new important choices wait for the personal model.
- Field records and books can carry recipes learned through production or reading. Copies preserve their source, and reading teaches the person without granting a skill.
- Completed or postponed parenthood plans no longer keep ordinary old memories and beliefs from moving into the private archive. Active plans and lasting memories remain protected.
- Adults born in the world can inspect land and property hearings, contribute evidence, personally accept property grants and serve as elected judges using their complete saved identities.
- Learned building, farming, crafting and smithing skills now shorten matching work with a provisional 20% bonus. Model requests show each agent their own learned skills.
- Old, low-importance ordinary memories now move into an agent's saved private archive once per world day, including with the routine helper off. Ordinary recall skips them; life events, relationships, skills and commitments stay available. Original evidence and later corrections survive saves. The three-day age and 25% importance limits are provisional. ([#1311](https://github.com/compoodment/ClankerWorld/issues/1311))
- Adults can plan ingredient-shopping and Restaurant meal trips to other Towns. Visitors buy through the same physical barter and seller consent as residents, without gaining access to Warehouse stock or private household goods.
- Children can gather nearby wild food and fallen wood and carry small household food or wood loads home, with physical travel, storage and carrying limits. Infants remain inactive.
- Orders for agents born in the world can now be cancelled normally.
- Towns can hear cases over an empty household's buildings and shared goods. Living former members must personally agree before a ruling passes property to the Town, which can grant a recovered House onward to an accepting household. Personal belongings keep their owners and collection access. Earlier alpha saves no longer load and remain preserved.
- Agents born in the world can start and take part in conversations even when their internal identity is longer than a founder's. Their own models and saved dialogue keep the full identity.
- Adults born in the world can found a household and continue making decisions after saving and reloading. Long household identities stay intact in saves and use bounded references in model requests, including final wills.
- Adults can deliberately found a new Town on unclaimed land through their personal model. Their dependent Town care group follows membership, the new council starts from adult residents, and existing household property stays owned. Founding supplies no free House or materials; later visitors need admission.
- Beliefs from residents born in the world can reach personal decisions and routine-helper memory scoring even when the reporter has a long native identity. Saved reporter identities and evidence remain intact.
- During storms, residents without a House can take shelter in their own Town's completed Town Hall. The Hall gives building cover without household stock or fire access.
- A field nobody works for a full season now goes back to grass. Any crop left on it is lost, the seed kept for replanting is freed, the land keeps its fertility, and the Event Log says so. Saves from earlier builds no longer load.
- Forests and trees now give only partial storm protection. Cooling agents prefer a permitted building over natural cover and keep its cover while looking for usable heat.
- New World can create different maps using the same seed while keeping their saves and settings separate; an identical map still cannot be created again just by changing its name. [#1298](https://github.com/compoodment/ClankerWorld/issues/1298)
- Agents waiting for a model show the approved circling gold spark after two seconds. The spark clears on reply or pause; actual provider failures add one Event Log line naming the agent and problem.
- Restaurant workers can buy missing ingredients while their housemates have household goods on sale at a borrowed Market stall. Those sale goods stay protected until the stall is released.
- Farms plan fields from their Town's living population and usable food reserve, sharing the remaining need among its farming households. A Town with one farm can plan enough fields for its other households, while stocked Towns stop preparing and planting more fields.
- Town construction planning avoids allocating complete footprints for sites whose origin is already blocked, reducing managed allocations in measured grown-Town workloads while retaining the same offered sites and placement checks.
- Urgently hungry agents can pause an owner order to drink milk from their carried jug, then resume the task with the jug intact.
- Zooming and unchanged observation refreshes reuse the map's tree and natural-site indexes, reducing repeated allocations while still updating growth, depletion and camp-resource sprites. [#1292](https://github.com/compoodment/ClankerWorld/issues/1292)
- New conversations no longer hide an older unfinished invitation or session from the game. Every unfinished conversation stays visible after reconnecting or reloading, alongside up to 16 recently closed conversations.
- Adults and elders born in the world can use the selected routine helper for ordinary choices and their own memory scores. Children keep their own model, and personal conversations, identity, wills and planning keep using each agent's own model.
- Unreachable household stock no longer prevents Blacksmith supply from gathering local wood or ore. Routine supply and supply orders use current pickup and delivery routes while leaving remote stock in place.
- Either partner can end a marriage alone, and a partner's death ends it too. Former partners keep their names and may marry again; past marriages remain in their profiles and saves.
- Routine personal recovery now skips unreachable belongings and collects reachable goods instead. Unreachable items retain their owner and location; recovery rechecks the route as the world changes.
- Children born in the world choose their own personality and aspiration after infancy, using their parents' chosen identities as background. Unusable replies keep the choice pending for a later ordinary decision.
- Personal storage and borrowed-goods returns leave House space for supplies already collected for delivery, including each jug and its contents.
- Adults can follow **Travel by boat to Port at (x, y)** orders using actual communal boats and the normal permission and queue rules. Only the named destination completes the order; cancelling aboard preserves safe travel and recovery. Older alpha saves are refused and preserved after the checkpoint format changes.
- Ecology updates avoid repeating configuration checks for every resource, reducing allocation work while retaining input validation and the existing growth rules.
- Generated worlds choose starter clay on a reachable freshwater bank that accepts the resource. Rocky mountain banks no longer cause the first checkpoint save to fail and pause the world; if no suitable bank exists, clay remains unavailable and the world keeps running.
- Agents collect and keep handcart materials only when the remaining household supplies can be physically reached, avoiding partial cart sets blocked by inaccessible fittings.
- Agents can follow an order to read a personally held record, map or book, learning only its written sites and keeping the exact selected item through waits and saving.
- Food trapped in broken pots no longer stops households from planting replacement crops.
- Orders to collect a cow's milk now finish when the adult fetches a jug at the yard and collects the milk in the same step.
- Milk collection chooses a jug only when its existing contents and the cow's waiting milk fit the adult's carrying space. A fuller jug no longer blocks an empty one that fits.
- Panning the map no longer rebuilds the agent card's appearance on every movement; changing the theme still updates it.
- Planting and other resource updates keep unchanged ground, overview and mountain relief cached while still refreshing trees and resource quantities.
- Animal-supply errands leave household goods on sale while someone borrows their Market stall. The goods become available for collection after borrowing ends.
- Agents can follow a named talk order by walking to the person and making a real invitation, with each person’s own consent, visible outcomes and queued work that waits for the conversation.
- World Settings now shows the world's size, climate, wrapping and seed without changing them.
- Adults and elders can follow orders to pull or park an owned handcart, including a cart named by its tile. Orders keep their chosen cart across saves and interruptions, preserve cargo and ownership, and explain physical blockers. Older alpha saves stop loading and are kept unchanged.
- Adults can carry usable flour home from the Farmhouse even when a broken flour pot is also stored there.
- Animal care can fetch water split across several jugs or water lots, counting water already carried and preserving the jugs and carrying limits.
- Newly planted orchard trees stay visible while they grow, using the approved fruitless orchard artwork.
- Grass and tree canopies now follow the seasons, with brighter spring colours, warmer autumn colours and muted winter colours. Summer keeps the existing art, and snow still follows the world state.
- Agents can continue scouting beyond eight steps and choose to return to the trip's starting point. They learn only from real observation, and returns keep the usual movement and survival rules. Older alpha saves stop loading and are kept unchanged.
- You can browse the Agents list with Up and Down while it stays open, then press Enter to open the chosen agent's Profile.
- Saving an unchanged world reuses its checked camp routes, reducing repeated walkability searches while retaining map validation and durable checkpoint writes.
- Cooking selects healthy ingredients when a broken pot or jug remains in the same building, and leaves the broken vessel and its contents untouched.
- Building payment leaves goods promised to another household building for their delivery and uses the available unpromised materials.
- World ticks reuse verified unchanged chunk metadata, reducing repeated allocation while preserving weather, resource checks and save bytes.
- World ticks and loading now reuse the existing ecology, chunk metadata and weather state instead of generating an unused initial world first, reducing preparation work without changing world detail or save contents.
- Wild animals find nearby feed with less server work while preserving the same forage choices, real feed costs and blocked-route fallback.
- Older finished boat-trip records move into the durable history archive when saving, keeping every current trip and the latest 40 finished requests in the live world. Earlier alpha saves are refused and preserved because the checkpoint format changes.
- Cached client refreshes reuse terrain calculations, reducing server allocations while keeping the same map detail and live world updates.
- Mountain relief finishes fading without leaving the temporary hill drawings underneath.
- Animals now step as they walk: hooves, or a hen's feet, step out from under them on each move, and they stand still again when they stop.
- Inspecting a Town law with emoji no longer creates an unreadable checkpoint. The active checkpoint writer checks that its bytes load before replacing the previous file.
- Offer unloading for cargo in every owned cart on the same tile, including broken carts, so an empty cart cannot hide another cart’s goods.
- Adults can follow orders to copy held field records, maps and books with real materials and work. Counts and repeating tasks wait for new eligible accounts; lost sources release unused supplies and wait for that same source. Current-format saves retain and validate source, work and completion bindings; older alpha saves are refused without migration.
- Reduce repeated household building-plan work in grown Towns by computing each proposed building identity once per design.
- Adults can follow orders to write field records, draw maps or bind books using learned sites and real materials. Counted and repeating writing waits for new information after an account has been written, and cancellation releases unused supplies. Older alpha saves are refused and preserved without migration.
- People inside a building are no longer squeezed onto its roof on the map. They are hidden while inside, and the building shows a small badge with how many people are in it; hover the badge to see who.
- Keep the highlighted building owner in an open dropdown during world updates, so Enter still selects the intended owner.
- In an abandoned Town, the lanterns fixed to buildings, such as a Warehouse's door lantern and a Port's pier lantern, now fade with the weathered building instead of looking new.
- You can give agents tree-planting tasks with a quantity, species or exact tile outside Town borders. They fetch real permitted seeds, walk to the site and count completed saplings. Tasks can be queued, cancelled or saved during travel; a blocked site keeps its seed and explains why planting must wait.
- Adults carrying out an active Must Do task are no longer offered as free teachers. Cancelling or finishing the task makes lessons available again, and pending requests can continue after cancellation.
- Mountains no longer flash their old tile-by-tile drawing as often. Mountain relief near the middle of the screen and just off its edges is drawn ahead of time, and relief that is still being drawn when it comes into view fades in instead of popping.
- Prevent clothing or carrying aids left in an old House or on the ground from being equipped without collection after an agent leaves their household, keeping the world valid and saveable.
- Urgently hungry adults can retrieve their owned Market food during another order, keeping that task and its progress.
- Agents carrying several water jugs now return one that is free to move and fits at home, leaving another jug's promised water intact instead of stopping the world or returning the wrong jug.
- Building Details now shows animals standing inside, including their care, ready products and permissions, so yard animals can be inspected by clicking their building.
- Household suppliers choose reachable workshops instead of repeatedly trying to deliver to a blocked building while another building needs their supplies.
- Guardians can collect another accepted child with a clear route while an earlier collection is blocked. Accepted care stays intact, and the waiting journey resumes when its route clears.
- An order to build an animal yard now waits for the household to complete a House, keeping yard materials unspent until then.
- Riders dismount before caring for another animal, so valid care orders can walk to their target and finish using real feed and water.
- House hauling can collect another reachable pile when its first household supply pile is occupied, leaving the blocked goods untouched.
- Scouts pulling handcarts choose exits the cart can use instead of repeatedly trying to enter an unroaded mountain.
- Nearby descendants with long generated identities can now hear ordinary conversations, keep the heard memory and recall it during later decisions without rejecting the speakers' turn or stopping normal ticks.
- Replacing or cancelling an animal task no longer leaves its supply trip blocking care for another animal. Collected goods keep their real owners and custody.
- Builders can gather reachable construction materials when a stocked Town Warehouse is blocked, leaving its stock in place.
- Add Agent now closes as soon as placement succeeds. A failed model-settings refresh preserves the placement confirmation, so another map click cannot accidentally add another agent.
- Voluntary agreements to return goods can now use a later carried lot with enough goods when an earlier lot is too small. Named source lots, reservations and recorded payments remain respected.
- Orders to return borrowed water jugs now bring collected milk back to the owning House even when a Store could stock it. The jug and milk stay together, and reserved goods and committed deliveries remain protected.
- Eggs, milk, cooked eggs, milk porridge and rich meals left to a Town now follow the household inheritance path, keeping pots and jugs together with their contents.
- Hungry boat passengers can drink available fresh milk from their carried jugs during travel and while waiting to land, keeping the reusable jug.
- Accepting a counteroffer while renegotiating a remedy replaces the original
  unfinished agreement, so agents perform only the newly agreed remaining
  contributions. Save checks preserve that link through successive counters.
- A handcart being built no longer holds storage space inside the Blacksmith.
- Wild animals can forage from another nearby patch when people or other
  animals block the approach to the nearer patch.
- House expansion leaves borrowed materials with the person carrying them until they return them.
- Adults can barter two field records, maps or books of the same kind when both
contain discoveries the other adult has not learned. The exchange transfers
the existing objects and teaches only their recorded contents.
- A Port's pier lantern in an abandoned Town now stays dark at night, and lights again once someone resettles the Town.
- Grown Towns avoid repeated Warehouse checks while planning buildings and recipes.
- Adults can stock a spare tool in their household Store when identical tools share one carried lot. They still keep one best usable tool in each family. ([#1072](https://github.com/compoodment/ClankerWorld/issues/1072))
- Street lanterns in an abandoned Town now stay dark at night, and light again once someone resettles the Town.
- Farmers keep their chosen crop while someone briefly occupies the field after an upgrade to an iron hoe. Losing a usable hoe still lets another eligible farmer plant. ([#1069](https://github.com/compoodment/ClankerWorld/issues/1069))
- Granting more model calls and resuming keeps time running when an earlier limit notice was delayed.
- Buildings now show on the map while they go up: cleared, staked-out ground first, then the frame, then the walls with the roof half on. Port materials wait on the bank and ladders follow the doorway. Street lanterns rise from a dug hole to their finished fitting and remain visible when zoomed out. Hover a site to see whether it is waiting for materials and how much of the work is done.
- Riders can collect another animal's wool or milk and saddle another horse by dismounting first; extra cargo stays where they dismount, and collection needs room on foot.
- Scouts now remember changed resources on tiles they revisit during an outward trip or return journey. Existing maps and records keep their original contents, and blocked travel reveals nothing about the destination.
- Supplying an animal yard now delivers only the chosen feed load, preserving the rest of an already carried stack and its owner. Water jugs still travel whole with their contents.
- Require replacement for fully broken tools during ordinary repair, matching tool repair orders; worn tools still use real repair materials.
- Adults can reclaim their spoiled goods from Market stalls, freeing physical space while preserving ownership and freshness. Urgent food recovery still selects only usable food.
- Gathering orders without a named site can explore locally when their only remembered sources are unreachable, while keeping those memories. Orders naming a particular source or tile still wait for that destination.
- Agents now remember changed resources on tiles they revisit, including orchards they plant, so known fruit sources can be used for harvesting orders. Existing maps, records, books and unfinished writing keep their original contents. Older alpha saves are preserved but cannot be loaded.
- Housemates leave household sale stock on a Market stall while another member borrows it.
- Care orders pick up yard feed and a water jug before walking to an animal on another tile, preventing repeated trips without care.
- Animal care orders explain when safe feed and jug water are missing, and can resume once supplies are available.
- Agents can put down excess repair materials to make carrying room for a missing ingredient, while keeping the materials and tools needed for the repair.
- Cold agents can leave natural storm cover that is not keeping them warm and reach a lit House without being drawn back to a nearby tree. They can stay where their current protection stops cooling. Lit destinations respect existing household heating access; a guest's shelter-only invitation does not grant use of the hearth.
- Adult patients can choose between nearby caregivers for their last medical permission, while the 16-caregiver limit stays enforced.
- Urgently hungry adults can retrieve their available food from Market stalls and eat it.
- Practical lessons skip teachers who cannot reach the teaching site, so an isolated skilled adult no longer hides another available teacher. Both participants must be able to reach the site before requesting or accepting a lesson.
- Buildings in an abandoned Town now look neglected on the map, with faded mossy roofs, weeds and boarded doors, and fall apart once the Town has stood empty for a full season. Its street lanterns and bridges weather with them, and its bridges lose planks. A Town that is resettled looks lived in again.
- Children can play or learn with another reachable person when nearer contacts are on cooldown for every child-social action. The three-person choice limit still applies, and recent encounters keep their cooldowns.
- Refined gold, gold ore, plain gold ornaments, iron fittings, saddles, cooked eggs, milk, berry and fruit porridge, rich meals and leather sacks now have their own item icons instead of a crate or another item's picture. Crude wooden axes and pickaxes show the wooden tool icons.
- Animals on the map now use their finished pixel art instead of placeholder ovals, in all eight directions and at mid zoom. Young animals have their own look, horses show whether they wear a saddle, sheep look shorn after their wool is taken, and the animal yard is a fenced pen with a gate, a water trough and hay.
- Rotating autosaves resume within their configured interval after the host's clock moves backwards, even if the host restarts, and keep the newest copies on each known branch despite earlier future dates.
- Urgently hungry adults can pause an order to buy and eat available shop food, including Restaurant meals, while keeping the order pending. Ordinary equipment purchases and ingredient shopping remain subject to the order.
- Loading a handcart now respects the hungry-members-first food reserve, including the amount a healthy adult may take from a larger shared lot.
- Agents can barter spare personal goods held in separate stacks while keeping their usual reserve and protecting worn, reserved and uncarried items.
- Animal care uses available grain and reserve-safe greens even when feed is split across lots, while keeping the household's protected food and reusable water jug.
- Stores can replenish usable goods after shelf stock spoils, while keeping spoiled property and respecting physical storage and personal food reserves.
- Building Details now lists recent recorded storage additions and removals with exact quantities and times, including vessel contents, across saves and reconnects.
- Children who leave with their caregiver can collect their own belongings from the former House once grown, including after the caregiver dies.
- Planting orders can use accessible household seeds when someone blocks the pickup route to another seed lot.
- Full-handed neighbors no longer hide a nearby adult who can receive an ornament.
- Farm hauling can use another permitted store when the preferred Farmhouse or Silo cannot accept the actual load, including a filled pot that needs more room than the Silo has left.
- Construction planning does less repeated work in grown Towns while keeping the same legal sites and placement checks.
- Store stocking keeps one food reserve across usable lots of the same owner and kind, so collecting four berries in two loads still lets an agent stock two surplus berries.
- Agreed Town service can use spare personal materials split across carried lots, keeping the four-unit reserve and a valid saved receipt. Ordinary donations that fulfill the agreement no longer halt the world. This alpha save format changes; earlier saves are refused and preserved.
- Long agent names fit within two lines in the Profile, keeping Rename and Back visible. Hover over the name or open Rename to read the whole name.
- Agents can eat available food inside a storage pot they own and carry, preserving the pot and remaining contents while protecting reserved food and deliveries.
- Closing a reopened Pause Menu resumes a previously running world even when an earlier Resume is still pending, while worlds you deliberately paused stay paused.
- Agents carrying multiple water jugs can fill an empty one even when another is full and their House has no storage room.
- Two housemates offering milk from the same jug no longer halt the world; each milk lot can have one pending sale at a time.
- Building quick cards and Details now show a storage-space bar with exact used/limit counts, updating as goods move or a building expands.
- Guardian orders recognize equivalent accented spellings of a child's full name, including names typed or pasted with combining accents.
- Building Details now lists what each workstation can make, with the materials and output quantities for each batch. Running work also shows the materials currently held for it.
- Opening New World while a Settings action is still finishing now generates the map preview automatically once that action completes.
- Delayed life-pace and Jev settings now apply only to their intended world; the host refuses them if another world has been selected. These controls require matching client and host updates.
- Model calls keeps unfinished limit edits through delayed refreshes, ignores older pause replies after a limit is saved, and clears the previous server's usage and accounting error when pairing changes.
- Households can build animal yards, tame and care for chickens, sheep, cows and horses with real feed and jug water, collect eggs, milk and wool, breed within reserved space, and process old-age hides into leather goods. Saddled horses offer faster travel and extra cargo; animal gifts, sales and named permissions preserve private ownership. Animal inspection, orders and paused Developer Tools are connected. Existing alpha saves need a new world for schema 96; Windows playtesting and balance tuning are pending.
- Recovering a pairing after a lost activation reply now restores the Main Menu, with Continue available to enter the world.
- Map dragging now stops when the pause menu opens, the game loses focus or the middle mouse button is released. Returning to the world no longer lets ordinary pointer movement pan the camera.
- Empty Towns now appear as abandoned while keeping their buildings, borders, property and laws. Agents can salvage real unreserved communal goods in person, and one adult can deliberately resettle the same Town with their dependent care group. Revival restores ordinary communal access and council admission for later newcomers.
- Unfinished agent renames now survive world refreshes when the name field loses focus. Closing the editor or choosing another agent or world still discards the edit.
- Grown Towns spend less time checking nearby construction materials and looking up shared gathering tools when choosing work.
- Opening Settings while Continue waits keeps the world paused; a late reply no longer resumes it behind the menus.
- Blacksmiths can use reachable supplies when an earlier stock pile is blocked by another agent.
- Orchard planting waits for room to collect a household seed, while a seed already carried can still be planted with a full load.
- Town Roads extend past doors across the map's east-west seam and keep their normal gap from nearby Roads on the opposite edge.
- Orders for unavailable farm goods no longer take grain from a Silo instead, and supplying a Silo no longer counts its own stock as a delivery.
- Paused world editing accepts coordinates across the whole map and updates their bounds when you change worlds.
- Switching an agent's provider during setup now asks you to choose a model when your key cannot use the new provider's default.
- Built-in choices no longer keep asking for tools when the request requires a personal model.
- Choose Off, Jev or OpenAI Decisions as each world's routine helper while paused, with a model picker and saved OpenAI key selection. Switching keeps memories and cancels pending work. The updated alpha save format refuses older saves and keeps their files.
- Agents can follow orders to gather nearby berries and fruit even when their map memory is full.
- Long-running Markets keep bounded recent trading history and exact live stock and payment records. This changes the save format; earlier alpha saves are refused and preserved without conversion.
- A forced replacement for an overcrowded House’s move-out notice now gets one fresh world day when the old deadline is already due. Future deadlines and volunteer deadlines stay the same.
- Town residents return unused construction materials to a Warehouse when it has room, while leaving carrying space for food.
- Adults can follow named House and Restaurant cooking orders, with real ingredients and two servings per batch. Orders can supply cooking ingredients, collect personal prepared food, return borrowed meals, share spare meals with the House and stock them in a Store, using the usual ownership, reserves, carrying and save/reload rules.
- Residents can collect and return Town Warehouse stock and supply, donate or build at an approved Town project while another resident stands on its work tile.
- Delete Save opens its confirmation when you manage the current world's saves straight after starting the client.
- Adults can follow "Build a Restaurant" orders using real wood, stone, travel and construction work. The order supports an exact site, Queue, Cancel and save/reload, and finishes only after the Restaurant is built.
- Children with generated identities can recall newly recorded talk, play and learning experiences after saving and reloading.
- The Agents list keeps your scroll position when it refreshes, so you can browse other agents while one remains selected.
- Healthy caregivers can fetch medicine and return to a consenting patient after saving and reopening the world.
- Town donations keep one four-unit personal reserve for each material, even after storing and collecting goods splits them into smaller lots.
- Construction and Store-stocking orders can progress after pausing ordinary work. Construction starts a fresh paid project; Store stocking lets the earlier project continue afterward. Existing reserved production finishes first.
- Heirs can collect their own inherited belongings from another household's House without joining that household.
- Built-in agents leave handcart transfers alone when they have no delivery task, instead of repeatedly loading and unloading the same goods. Personal models can still use the cart controls.
- Add an optional Windows checkpoint trace job that retains synthetic file-I/O evidence and existing save-failure diagnostics without changing persistence behavior.
- Parents' cards and continuity guidance explain the household food shortage that holds up a child plan, including how many ready-to-eat portions are missing. Grain and flour need cooking.
- Worlds with long valid seeds keep running after a birth, including when reloading a newborn whose model setup was interrupted. The owner can change that child's model normally.
- Applying autosave settings after a world switch no longer changes the other world's settings or deletes its saved copies. Older hosts show an update message before the client sends the change.
- Shelter and fire orders aimed at water or other impassable terrain remain blocked without stopping the world or its saves.
- Town Councils can approve paid Ports and communal boats. Adults travel between usable Ports with their carried goods; visitors need Council permission. Dock reservations, queues, cancellation and blocked-arrival return survive saves. Boats and Ports use the approved drawings, with boat details and dock use in inspection. Older alpha saves are refused and preserved.
- Children keep their spare food when their House has no unpromised storage space. If another delivery fills the House after a helping action was offered, the child safely keeps the serving and the world continues running.
- Food orders and personal-model actions involving repeatedly split inventory now save successfully and keep the world running. Food-order completion receipts stay bounded, and the last accepted model choice keeps its exact identity through save and reload.
- Towns can hear non-land law reports under a separately approved mandate, with sourced evidence, responses, independent judges and voluntary restitution, repair or public service that completes only when the real goods or work arrive.
- Council votes can recover a model reply that omits the empty final field of an otherwise uniquely offered civic choice, while unoffered actions still fail safely.
- A building or crafting project carrying supplies to full household storage now shows that storage blocker instead of repeatedly trying to gather into a full load. The delivery and project remain available to resume when storage has room.
- Towns with many Roads no longer slow the world down: residents choosing civic projects, such as street lanterns, Markets and Town Halls, now check possible sites far faster, with the same choices as before.
- Agents can follow orders to seek permitted shelter or light a fire with real wood. Tasks keep their destination, respect household and storm-guest access, and preserve progress across interruptions and saves. Reaching cover completes a shelter task without claiming that warmth has recovered.
- Agents can follow orders to build household buildings or complete the next supported House or Town Warehouse expansion. Tasks use real materials and work, keep their selected site and building, and preserve cancellation, survival pauses and completion across saves.
- Agents can follow orders to haul household stock, supply workstations, share spare food, stock a Store and donate Town Warehouse materials. Tasks keep their destination and count actual deliveries while respecting normal demand, reserves, space and ownership.
- Agents can collect and store named personal goods, including filled pots and jugs, and return borrowed household goods. Orders count actual moves, keep their chosen House destination and preserve ownership through cancellation and reload.
- Households can make crude wooden axes and pickaxes at their own House from stored wood. They can fell trees and extract stone without a Blacksmith, but gather less and wear out sooner than smith-made tools. This changes the saved-world format; older alpha saves are refused and kept unchanged.
- Order adults to make supported goods or mill flour through normal work sites and ingredients. Tasks track finished items or batches, keep their chosen site, and resume after survival interruptions or save/reload. Queue another task or cancel unfinished work and release its unused ingredients.
- Partners can agree to marry through their own models and then choose a shared surname in a saved conversation. A disclosed draw settles continued disagreement after four turns. Player surname changes update both spouses together, and a taken first name leaves both unchanged. Older alpha saves are refused and preserved by the new marriage save format.
- Handcarts now show their approved empty and loaded pictures at close zoom, turn with their movement, and lift their shafts in the two available pulling poses. Smaller zooms keep the cart icon.
- Adults and elders can put away their own carried clothing, carrying aids and tools with orders such as "store my basket" or "store two iron knives in my House". Storage preserves ownership and condition, leaves worn equipment with its wearer and respects reservations and House space. Quantities, queues, cancellation and progress survive saves; older alpha saves are refused and preserved unchanged.
- The Event Log fits wrapped rows and day headings before it needs to scroll, and adjusts its height when the window is resized.
- Adults and elders can collect their own stored or dropped clothing, carrying aids and tools with orders such as "collect my basket" or "collect two iron knives from (12, 4)". Pickup preserves condition and ownership without equipping or repairing the goods. Quantities, queues, cancellation and progress survive saves; older alpha saves are refused and preserved unchanged.
- Council elections keep their own results and retries when a government change is waiting for a successor. An adult leaving during voting no longer lets an unrelated failed handover cancel that election.
- Adults and elders can collect their own stored or dropped ready-to-eat food with orders such as "collect three berries" or "collect food from (12, 4)". Exact quantities, source tiles, queues, cancellation and partial progress survive saves. Shared, reserved and promised goods remain unavailable. Older alpha saves are refused and preserved unchanged.
- Reroll in New World now clears the old map and refreshes the preview for the latest seed, even if another preview is still generating. An empty world name no longer leaves a stale map or an enabled Create World button behind.
- Town residents can propose and build stone street lamps and hanging street lanterns beside Roads after Council approval. They carry real materials, keep the approved Road edge and light automatically at dusk without fuel. Budgets and work are provisional; earlier alpha saves are refused and preserved.
- New fields, land grants, Roads and bridges respect household land: farmers leave other households' held or requested plots clear, ordinary grants cannot take over another household's field, and new Roads and bridge entrances avoid private plots. Existing Roads and bridges remain usable.
- A sponsored newcomer's Town admission approval expires after one unpaused world day without acceptance. Their Profile shows the deadline and the Event Log explains an expired approval; someone may ask the council again later. A newcomer's own passed request still admits them immediately. Saves preserve the remaining world time using the council's original approval time. Private-world schema 78 adds the expiry history; earlier alpha saves are refused and preserved without migration. Windows hands-on checks remain pending in `playtest/alpha-1.md`.
- Field-work orders can name an exact tile, such as "plant grain at (12, 4)". Agents till, plant, tend or harvest only there, using their household's land, real tools and planting stock. The target stays fixed through waiting, interrupted work, queued tasks and reload. Older alpha saves are refused and preserved unchanged.
- Land disputes and expired household use rights can receive public hearings, sourced evidence, explicit answers or waivers, independent land-mayor decisions and grounded rehearings. Town, plot and property details show the case while title, buildings, crops and goods stay with their holders. Wider enforcement and punishment remain unfinished. [#633](https://github.com/compoodment/ClankerWorld/issues/633)
- Households can transfer exact existing use rights after every current adult in all giving and receiving households learns and personally accepts the published terms. No routine Council or mayor approval is needed; original dates and private property stay intact. Inspection separates current hearing responders from parties at publication and at each ruling. [#633](https://github.com/compoodment/ClankerWorld/issues/633)
- The alpha save format uses schema 72 for hearing and transfer records, original grant receipts, bounded permission adjustments and exact request-resolution history. Older schemas are refused and preserved without migration. Windows hands-on checks remain unchecked in [the land-hearing playtest](https://github.com/ClankerWorldOrg/ClankerWorld/blob/main/playtest/alpha-1.md).
- Households can build a 1×2 Farmhouse, 2×2 Blacksmith, 2×2 Tailor Shop, 1×1 Clinic or 2×2 Restaurant as an alternative to the existing size. Each uses real materials and work and keeps the building's usual recipes and household access. First-Town buildings and the one-building-per-kind rule stay the same; costs and stock capacities remain provisional.
- Councils can approve a Town-owned Market with a hall, fixed packed-earth plaza and two starter stalls. Residents must deliver real Town materials and finish the work; further stalls need their own approval and payment when existing stalls are borrowed.
- Adults can carry their own or their household's surplus to a borrowed Market stall and trade exact goods with buyers from any Town or with no Town membership. Both people meet at the stall and choose the exchange. Purchases become the buyer's personal cargo; payment stays with the seller's household. Leaving frees the stall while leftover goods keep their recorded owner. Adults may also buy missing paused-recipe inputs or one needed planting unit for an actual ready field; usable carried, household and reserved replanting stock satisfy that need first.
- Market cards show paid stalls, borrowers, goods, owners and exchange progress. The map draws only built stalls with the approved hall, awnings and packed-earth plaza. Market and Town project events appear in the Event Log. Windows playtesting is pending.
- Saves retain paid Market construction, stall use, stock receipts and actual barter history. Older alpha saves are refused and preserved without migration.
- Collection orders can name an exact source tile, such as "collect three wood from (12, 4)". Agents collect only their available personal goods there and wait if those goods move or run out, keeping the target and remaining quantity across saves. Older alpha saves are refused and preserved unchanged.
- Adults and elders can follow orders to repair their own worn wooden, stone and iron tools at their household Blacksmith using real supplies. Quantities count only finished repairs; queues, preparation, cancellation and survival interruptions preserve remaining work across saves. Older alpha saves are refused and preserved unchanged.
- Buildings in use now glow at night. Light falls on the ground from a building's windows and open door, never its roof: a House while someone is inside, workshops while someone works there, the Blacksmith's forge while it is busy, and a Warehouse's door lantern while goods are fetched. Each glow has a ragged edge that drifts gently, and the forge flickers.
- Agents now repair another worn personal item when the preferred item's materials or work site are unavailable. A basket still comes first when both it and a garment can be repaired; real material costs and equipped items are preserved.
- Adults and elders can follow orders to till, plant, tend and harvest their household's fields, using real seeds, tools and work. Quantities count completed fields and named crops are respected. Cancellation releases unused planting stock; queues and partial work survive save/reload. Older alpha saves are refused and preserved unchanged.
- Nights now follow the seasons: about 30% of the day at the start of summer, 50% at the start of winter and 40% at the start of spring and autumn, changing a little each day. Winter nights are longer and so stay cold for longer.
- A vacant mayoral office no longer blocks a government change that keeps that office, and Towns wait for a willing candidate instead of recording an empty failed election every day.
- Clinics, Restaurants, medicinal herb patches and picked or depleted natural sites now use their approved drawings. Potatoes, cultivated green seeds, medicinal herbs, diamond ornaments and simple meals show their matching item icons in stock, inventory and trade views.
- A guardian in another Town can collect a child and accompany them to a completed House with room. The child's household and Town change on arrival; the agent card distinguishes accepted care, waiting and travel. Older alpha saves are refused and preserved.
- Chosen first names are unique across living and deceased agents, even with different surnames. Temporary names stay available until deliberately chosen. Children use a biological parent's surname. Older alpha saves are refused and preserved because the new format records whether each name was chosen.
- Adults and elders can follow orders to repair their own worn clothing, padded coats, rain cloaks, baskets and sacks using real materials and their household work sites. Only finished repairs count toward quantities; queueing, cancellation and save/reload preserve the remaining task, and cancellation releases unused supplies. Older alpha saves are refused and preserved unchanged.
- Adult residents can propose, supply and build a named Town Hall after the current Council approves its exact site and budget. It needs real delivered Town materials and construction work on uncontested Town-titled land. Its trial budget is 24 wood and 12 stone, with 10 work units. Donating personal goods needs a separate fresh choice at the site.
- The Towns page shows the proposed site and budget, approval, supplied materials, work and blockers. A project whose site can no longer be used is cancelled with its reason, and its land is free again; its leftover materials stay Town property. A completed Hall uses the approved bell-tower drawing and becomes the Town's civic notice place. Windows playtesting is pending.
- Town construction keeps its exact loads, material claims and work across saves. Older alpha saves are refused and kept unchanged; no migration is added.
- New worlds now open at 06:00 on Spring 1, Year 1, so founder setup happens in daylight. Existing saves keep their recorded clock, and founder ages and the length of day and night are unchanged.
- Adults and elders can follow orders to collect their own stored or dropped raw materials, including personal goods left in a former household. Physical pickup respects ownership, reservations and carrying space; exact quantities, queueing, cancellation and saved progress count only goods actually collected. Older alpha saves are refused and preserved unchanged.
- An agreed birth can now complete if the caregiver loses access to shelter during preparation. The baby joins the caregiver's household in nearby free space and records its housing need; food is still required.
- Connected devices recover when another paired device selects a world or loads an earlier save, refreshing the map and Event Log together. Retained requests stay available to inspect but cannot be retried against a different loaded world state.
- Large custom weather weights now keep the intended rain and snow chances in cold regions, and storm cooldown no longer selects clear skies because adjusted weights overflow.
- Adults and elders can follow orders to store their own carried raw materials in their House, preserving personal ownership. Exact quantities, repetition, queueing, cancellation and save/reload count only goods actually put away; missing goods or storage space leaves the task waiting. Older alpha saves are refused and preserved unchanged.
- Avoid an unnecessary extra model call when choices only disappear while an earlier reply is pending.
- Withdrawing or letting an unanswered partnership proposal expire no longer splits existing families or removes a House's extra family places. Ending an accepted partnership still separates the partners' family groups.
- Placing a building beside a bridge across the world's east/west edge now extends the street beyond its door in the correct direction when there is room.
- Roads reuse an existing bridge when the river widens between connected narrow crossings, instead of building an unnecessary second bridge. Separate tributaries and impassable banks still leave room for needed crossings.
- Continue on a fresh server now opens New World instead of the retired test camp. Choose and preview the map normally before placing the first Town; existing worlds with progress remain available.
- An expired household food-policy vote can no longer pass because a nonapproving voter dies afterward. A majority reached on the deadline still counts.
- Farm households can carry reachable crops and seeds to storage even when another pile or vessel is blocked by an agent.
- Cold agents no longer keep trying to tend a hearth blocked by other people. They can use another reachable hearth or seek cover, and keep their wood until they reach a hearth.
- Explorers choose another legal exit when an agent blocks a diagonal corner, instead of repeatedly trying the same blocked step.
- Agents can follow orders to gather wood, stone, fiber, clay, iron ore, gold ore and diamond using normal tools, carrying limits and resource stock. Orders support quantities, repetition, exact sites, queueing, cancellation and save/reload. Older alpha saves are refused and preserved unchanged because the order state has changed.
- Hungry agents can set down some reserved orchard seeds to make room for food after a carrying aid breaks, preserving the seeds for their household and keeping other reserved supplies protected.
- A bridge cut off by impassable Peaks no longer prevents a needed crossing elsewhere on the river. Existing bridges are still reused when agents can reach them along both banks.
- Overcrowded Houses give eligible adults one unpaused world day to find another home, with volunteers first, current capacity and care checks, and visible notice reasons, remaining time and expansion progress. Children and their sole caregivers are never forced out; families can arrange expansion or a voluntary split instead. Moving keeps personal goods, physical collection and the once-only food allowance.
- The Event Log now describes an overcrowded House as overcrowded instead of saying its residents have no home (#816).
- Move-out notices and pending relocation requests survive saving and loading without restarting the deadline. This alpha build requires save schema 64; earlier saves are refused and preserved without migration.
- Agents can follow "Move to tile (12, 4)" orders through normal walking routes. Trips support Queue and Cancel task, wait for blocked destinations, and resume after urgent needs or save/reload. They finish only on the requested tile. This uses a new alpha save format; older saves are refused and kept unchanged.
- Redo Town site works after removing an empty starter building before placing any founders, including after saving and loading the unfinished setup.
- Developer tools can edit a selected agent’s needs, carried goods, skills and partnerships while paused. Each accepted change is saved and clearly marked as a Developer edit in the Event Log.
- Households make paper from fiber and water, and agents use real paper and cloth to write maps, records and books. Reading and trading share only the knowledge those goods contain; copying uses fresh supplies. Older alpha saves are preserved but cannot be loaded.
- Agents keep new choices and observer messages that arrive while their model is answering, so an older reply no longer makes an idle agent overlook the changed situation.
- The agent card and Profile name the agent's current order, how far along it is, why it is held up and how many orders are queued after it, or how their latest order ended. **All orders** in the Profile lists every open order and their six latest closed orders, with the agent's replies set apart from the game's status. Closed suggestions no longer push recent orders off the card.
- New World describes the previewed map in plain words, such as "Plenty of forest and some mountain ranges", instead of a line of percentages and candidate numbers. Hover the text for the exact figures.
- Adults can follow "Become guardian for Lina" orders for children in an active guardian search. Orders keep the same child after renaming, support queueing and cancellation, and respect existing care and House placement rules. This uses a new alpha save format; older saves are refused and kept unchanged.
- A new Town's starting clay deposit and grain-seed patch are drawn as a clay bank and a seed patch, instead of small white diamonds.
- Agents can build visible handcarts at their household Blacksmith using carried
  wood, iron fittings and rope. A cart carries a larger separate load, follows
  its owner along legal routes, and can be parked, unloaded, repaired or given
  away with its goods intact. Roads make pulling easier and reduce wear. Map,
  tile and agent inspection show who owns a cart, its condition and its cargo.
  Older alpha saves stop loading with this save-format change.
- Load Save and Save World show a world's saves as a timeline: a line for each branch across the seasons, a banner on each branch's newest save, and **You are here** where the running world is. Click a save to see it described underneath, or switch to **List** for the cards grouped by branch.
- Households can request land-use rights through Council approval and explicit acceptance from every current adult member. Plot details show approval progress, and building expansion waits for permission on its extra tiles. This changes the alpha save format; older saves are refused without being overwritten.
- Elders can no longer have children. Only adults can plan or have a child, and a plan ends if either partner becomes an elder before the birth. The continuity rule no longer holds elder couples. Elders can still form partnerships and care for children.
- New worlds have a few large mountain ranges instead of many small patches: one or two on Small worlds and two to four on Medium worlds. Peaks run along each range's crest inside walkable mountain, the band of hills around a range is wider for larger ranges, and the starting clearing always has stone within walking reach. Worlds created before this change are refused and their saves are kept.
- Town councils can approve explicit claims to adjoining unclaimed land. The Towns page shows the proposed tiles and votes, and the Town title filter updates after approval. Existing titles, household rights, buildings and goods are preserved. This changes the alpha save format; older saves are refused without being overwritten.
- A customer can ask a Blacksmith household to make an existing tool recipe. The household can accept or refuse. A request takes no payment and creates no ownership of future goods.
- Accepted work uses the household's real materials, workplace and normal production. The finished tool remains its property until an ordinary barter exchange completes. Both traders still agree the actual goods and payment and meet at the Blacksmith; a request does not fix a price.
- The Blacksmith card names the requester, recipe, progress and any missing-input or full-storage reason. Relevant agents get a bounded request note. Withdrawing or interrupting a request leaves materials, work and goods with their existing owners.
- Placing, accepting, refusing and withdrawing requests are deliberate fresh personal-model choices. Jev, fallback, repeated intentions and owner orders cannot create those commitments. Existing routine supply and accepted work remain available.
- Saves use schema 57. Older alpha worlds are refused and preserved without migration. The [hands-on tool-making check](https://github.com/ClankerWorldOrg/ClankerWorld/blob/main/playtest/alpha-1.md) is pending. Market stalls, shared Town projects, currency, art and combat remain separate work.
- Households now cook named potato or greens meals, porridge, bread and vegetable
stew from ingredients carried into their House. A Restaurant can cook grain and
vegetable meals and improve bread into a more nourishing Restaurant meal.
Restaurant adults visit shops in their Town to buy missing ingredients using their own
carried goods, while keeping the ingredients their Restaurant still needs.
Purchases must be carried in before cooking. Customers can walk to a Restaurant
in their Town and buy the finished meals, including berry and fruit porridge,
without gaining private stock or cooking access. A trial offer exchanges two
cooked servings for one wood;
barter rates remain provisional.
Water and wood are real inputs, and water jugs remain reusable. Cooked food
spoils, with pots slowing that loss; raw grain, potatoes and flour keep their
freshness. Each workstation keeps its needed ingredient reserve when another
building is supplied, including ingredients inside a moved vessel. Separate
cooked servings can fund a birth without taking the household's food reserve.
The House provides heat; a Restaurant's wood pays for its cooking recipes.
Builders complete valid household supply deliveries before collecting more
materials for their project, so a carried load does not strand the work.
Prepared meals give better nourishment and dietary variety, and an exact
food order can ask for a named meal.
Older alpha worlds are refused and preserved after this recipe and diet update.
- Town residents can adopt scoped laws, approve government changes and elect mayors through their normal personal-model turns. Laws retain their history and never change ownership or prevent physical actions. Government changes require a resident majority and a valid handover; mayoral ties need further votes. The Towns page shows laws, handovers and separate land and governing mandates. Saves now include these records; older alpha saves are refused and preserved. Land hearings and enforcement remain unfinished.
- An adult with no Town can now join one through its council. They ask at the notice place, or a resident asks for them and they accept. Dependent children join with their caregiver. An admitted resident may collect their Town's Warehouse stock, but approval never gives a House or household place. The agent's profile shows their Town, what it lets them do and any admission in progress.
- The Event Log now names the Town an agent joined or left instead of always saying "the first Town".
- Settings and Developer tools now show the client version and source commit for bug reports. The host reports its own build at `/api/v1/status`, and Windows export metadata and the checksum manifest identify the source build. Untagged builds are labelled `dev`.
- The world keeps closer to its normal speed while agents travel far from town.
  Agents reuse matching route checks when choosing what to do, and building
  and recipe lookups are faster.
  Agents choose and walk exactly the same routes as before.
- Reassigning a household building now moves its existing footprint land-use right to the new household, keeping Town title, surrounding rights and grant terms intact. Disputed, expired or differently held rights prevent reassignment.
- New World keeps playable maps when another generation attempt has no room for a first Town, lists the failed attempts, and gives a useful message when none can be used.
- Saving no longer interrupts an ongoing conversation when older world history is archived. Loading a world still requires both agents to agree to resume their conversation.
- Agents using built-in rules no longer keep putting their belongings away at home and picking them straight back up. They leave maps and field records stored at home and keep other belongings with them. Any agent can still choose to store or collect its belongings, and recover them after moving out.
- Every world day now has a night, 40% of the day from 19:12 to 04:48 on the clock, with a short dusk and dawn. The map darkens gently at night while names, agents and panels keep their colors, and nights are colder outdoors; clothing, shelter and a lit fire still keep agents warm.
- Severe illness slows field work while planting supplies stay held until the work ends. Grain in a Silo can return to the Farmhouse for milling.
- Agents choose a food harvest that fits their load and can make room for a household serving. Caregivers can collect food for a hungry infant despite a full load, and older children can eat ready-to-eat food from a household pot.
- A filled jug can reach home when the jug and all its contents fit. Loose goods keep their small delivery batches.
- Owned spoiled food can leave a pot without being destroyed. No automatic pot clearing or disposal control is added.
- Adults can walk spoiled household deliveries back to camp and set them down without losing the goods. This frees their load and clears the old delivery promise. A building still needs no stock, work or other deliveries before it can be removed or reassigned.
- A dying agent's final will can name up to three heirs, including children and their Town, and divide their belongings item by item or in equal shares. Personal inheritance changes ownership without automatically making the heir carry the goods. The Town keeps permitted goods in its Warehouse while there is room; undeliverable shares follow household inheritance. The will may leave short final words, which each person who receives goods remembers privately, and the dead agent's profile shows the will and the final words.
- Worlds with thousands of trees and other natural sources no longer slow to a
  crawl while agents look for wood, stone or ore. Agents still pick the same
  nearest source they can use.
- Ellipses sit on the line like full stops instead of floating halfway up the
  letters, in messages such as "Connecting...", in shortened world and save
  names, and in what agents think and say.
- Ornaments connect finite rare deposits to Blacksmith refining and ornament making, then to wearing, named gifts and physical barter. Gold ore, refined gold, diamonds and finished ornaments remain separate real goods. Recipe quantities and work times are provisional.

- Wearing an ornament keeps its item identity; putting on one from household stock makes it the wearer's own. It gives no protection or carrying bonus and does not change the agent's map appearance. Giving or selling one requires the actual goods and a nearby recipient or the existing shop exchange; private stock and reservations remain protected.

- Putting on, taking off and giving an ornament are deliberate fresh personal-model choices. Jev, failed replies, repeated intentions and owner orders cannot give away an agent's ornament.

- Saving preserves the selected ornament, work and transfers; older alpha saves are refused and preserved without migration. No combat, universal money or new art is included.
- Personal barter now completes only when both people have actually met and each has room to carry what they receive. The one who proposed an accepted offer waits at the meeting place while the other arrives ([#758](https://github.com/compoodment/ClankerWorld/issues/758)).
- The Mod Library shows each add-on as a card with its name, who proposed it, its version and an In use tag, without the internal package name. Confirmation dialogs centre their message, keep Cancel and the action together, use straight quotes around names and have a framed close button inside the frame. Status messages show a green tick or an amber warning sign beside plain text, in a box that fits the message.
- An agent's Model panel and Add an agent caption their fields Provider, API key and Model, and long names no longer widen the Profile. On an agent's page, **Open model settings** replaces Delete this key and opens the agent's model in World Settings, where keys can be deleted. Add an agent now says "Click on land to place them" and, while you point, which household and Town they would join.
- Memories shows its entries as cards with a tab for each kind, how sure the agent is of each belief, and a picture of the ground for mapped places. The Family Tree uses small portrait boxes with a heart between partners and a key, fits the tree and opens beside the Profile. The Profile names the provider whose model made the agent's latest choice instead of "chosen by", previews only the newest thought, lists people with an icon for each tie, and joins Suggest and Order into one switch.
- Households can build a 1×2 Clinic and gather renewable medicinal herbs and make care supplies through the normal building, gathering, carrying, production and local barter paths. A House or Tailor Shop cuts cloth into bandages; a Clinic makes medicine from herbs, wood fuel and water in a reusable jug. Recipes and costs remain provisional.

- The owner's October 2 decision keeps bandage production, storage and trade in this stage. Injury causes and bandage treatment wait for the later injury stage. Medicine uses the existing illness system; it does not instantly restore full health.

- Adults now return empty household pots and jugs from workstations to the House through real pickup and delivery, so reusable supplies can circulate again. Ownership, reservations, carrying room and destination space still apply ([#749](https://github.com/compoodment/ClankerWorld/issues/749)).

- Medicine consumes one real dose and reduces illness gradually. Adults choose named caregivers through a fresh personal-model decision and may revoke permission. Self-care and a dependent's accepted caregiver use their existing authority. Buying supplies or sharing a household grants no private-stock or treatment access; Jev, failed replies, repeated intentions and owner orders cannot grant adult permission.

- A treatment course and its permission survive saving and reopening. Interruption stops the remaining effect without refunding the spent dose. Death releases live inventory claims while preserving completed consumption receipts. Older alpha saves are refused and preserved without migration.
- World Info's World page opens with today's date, season and local weather, then shows the world's counts as tiles with icons. Its Towns page shows each Town's residents as portraits, household stores as item slots and projects with a progress bar, and no longer repeats whether Town borders are shown.
- Dates now name the season and its day by default, such as Autumn 2, Year 1 · 14:20, and the top bar no longer repeats the season beside them. Game Settings still offers DD-MM-YYYY, MM-DD-YYYY or YYYY-MM-DD and a 12-hour clock; a numeric date chosen before this update needs choosing again.
- When a child loses their last caregiver, the game asks adult relatives first,
  then adults in the child's home, then other adults in the same Town, and keeps
  asking the earlier groups as it widens. An adult must choose to help. A child moves into that adult's home only when its
  completed House has room and both homes are in the same Town; otherwise the
  child stays where they are. A child who still needs a guardian remains visible,
  and nearby adults can still feed them. Birth and parent records stay intact.
  A caregiver who ends care no longer changes the child's family group when
  they start a new relationship.
- The model-call limit moved from World Settings to Game Settings, where the Main Menu can open it too, and says that one count covers every world and every call attempt.
- The Event Log warns once when model calls reach 80% of the limit, before the limit pauses the world.
- Reaching the model-call limit while starting a conversation turn or canceling a provider call now pauses and saves the world instead of leaving it stuck.
- Food orders can eat carried or accessible household food, travel to a food source, and gather a named food kind or source. Queue keeps the current task; Cancel task stops a waiting or active one. Targets and physical progress survive waiting, urgent survival and save/reload. Instructions the game cannot understand preserve the current task and its queue. Eating respects fullness, gathering respects carrying space, and order steps do not request a model call every tick.
- Each Town has its own council, proposals and representative elections. Agents personally agree to stand, learn from real notices or nearby relays, vote through their ordinary choices and keep their civic records across saves. The Towns page shows the current council, election and recent results, and long readouts scroll. Council approval is separate from joining a household.
- Older alpha saves cannot be opened with this version. They are kept unchanged, and the game explains why a new world is needed.
- Preserve previously walked scouting paths when a later bridge changes the crossing. Keep the scout's recorded visits and discoveries when saving or loading, including in a deceased profile. Returning scouts still follow routes that are legal now, and impossible saved steps are refused.
- Adults can leave a household without a vote, physically collect their personal belongings and return borrowed household tools. A departure grants up to two available unreserved food portions once. Dependent children stay with their primary caregiver unless another adult explicitly takes over care; housing admission checks room for the whole group. When no suitable existing home accepts them, one adult can start a household and build a House using ordinary materials and work. Town membership and the old household's property remain recorded. A remaining member can resume private work paused by the departure, using its original materials. Saves now record departures and physical custody separately from ownership; older alpha saves are refused and preserved.
- Developer tools now open with **F12** inside a world, without pausing it, instead of from Pause Menu Settings. Besides the earlier tools, they show the tile under the pointer, frame time, how long the server takes per step of world time and the number of living agents, move the camera to any agent you choose, and can draw the selected agent's planned path.
- The Agents list shows a portrait row per agent with what they are doing and Hungry, Cold or Ill tags. The Event Log has an icon per kind of event, a heading per day and a Find button for events with a place. The controls list draws keys as keycaps in two grouped columns. Both lists stop above the bottom of the screen and scroll.
- Jev's routine choices now see the agent's warmth and illness as well as
  fullness, with unknown values left unknown.
- Agents' models keep seeing their fullness, warmth and illness as exact
  numbers. Plain words on a stated scale, such as "hungry (starving, hungry,
  fine, full; starving is worst, full is best)", were built and compared with
  real models, but did worse with one of them, so they stay switched off.
- The tile card is headed by the ground's name with a picture of the tile, and lists its facts in short rows, including climate, soil and any field or goods on it. The hover label shows a small picture of the ground, the World Map marks Towns and agents on parchment with a legend, each map filter says what it draws, and on/off switches have a new look.
- The Event Log now warns when the world is at risk of dying out and offers
**Add a newcomer** while the continuity rule is on. It opens Add Agent so you
can choose a key, model and placement; opening the offer adds nobody and makes
no paid model call. The offer disappears when the rule turns off.
- Agents can now wade across rivers two tiles wide, straight from bank to bank, at a provisional third of walking speed, slower than a one-tile river. They never wade along a river or diagonally, and wider rivers, lakes and the sea still cannot be crossed on foot. A two-tile crossing that agents wade often (six crossings by at least two agents within two world days) gets a bridge across both tiles, walked at dry-ground speed. Saves keep the same format.
- A world can no longer die out only because every couple keeps declining children. While fewer than eight agents who are not elders are alive, the continuity rule is on: a partnered couple with no infant may say "not yet" to a child, but not refuse, and two world days later their plan goes ahead. The birth still needs food and shelter. New worlds start with the rule on, the Event Log says when it turns on or off, and each partner's own model request explains it. The rule never creates a partnership. Once eight non-elders are alive, couples may refuse again. The numbers are provisional.
- Saves now carry the continuity rule's state and each couple's deadline. Saves from older builds are refused and kept.
- Founders and adults placed with Add Agent now arrive at an age from day 15 to day 25 instead of all at day 15, so founders reach old age at different times. The world seed picks the ages: the four founders always get different ages, and the same seed gives the same founder ages. Existing worlds keep their agents' saved ages; adults added to them later also get a seeded age.
- Aunts and uncles, including half-aunts and half-uncles, can no longer propose to, accept or plan a child with their nieces or nephews. First cousins can still pair, and shared households and caregiving still do not count as kinship.
- Household shops can exchange goods kept at their actual location. Both traders meet before the exchange, Store stock must be carried in, and the building card shows each offer and its outcome. Unfinished exchanges stay visible ahead of recent completed or cancelled offers.

- A Blacksmith can sell spare refined iron from its own stock. Another Blacksmith household can buy it for tool work; a transaction still reserves exact goods and payment and preserves both households' private work rights.

- Buyers can consider better tools and clothing suited to the weather while keeping their existing equipment. Their best usable tool in each family stays out of Store payments and shelf stock. Optional restocking waits while the household gathers materials it needs.

- Stores use their approved brown shingle roof and striped awning, facing the road beside their entrance.
- The first Town now records its land title and starter household use rights. The map shows them as separate filters, and Add Agent follows recorded rights. Land requests, grant approval, transfer consent and land-case decisions remain future work.
- Agents can reconsider their personality and aspiration when they reach the middle of life, become a parent or elder, or lose a partner or parent. Each kind of moment gives one optional personal-model request. Accepted changes and their reasons are saved and shown on the profile; failed or interrupted requests keep the current identity without automatic retries. This changes the alpha save format. Older worlds are refused and kept.
- Agents can mine finite deposits with the right pickaxe, make and repair wooden, stone and iron tool tiers at a Blacksmith, and work faster with hoes, hammers, wooden and iron sickles, and knives. Using tools wears them; fallen wood can still be gathered by hand. Trial rates remain provisional.
- If a tool breaks before field work finishes, the work stops safely and the crop remains available. Saving at that point preserves the broken tool and the unfinished crop.
- Adults with a usable iron pickaxe and enough carrying space can mine gold and diamonds from finite outcrops. They keep the extracted goods; making ornaments remains later work.
- Blacksmith input gathering can set spare supplies down with the household to make room for a whole harvest. The required tool and reserved goods stay carried.
- Agents can receive the player's exact suggestions and recognized orders in their next personal-model request, then show when they heard a message and any short reply on the agent card.
- Load World can load an older save directly from the selected world’s timeline. Playing on from an older save starts a new branch, so the saves from the first version of events stay as they were. The save list groups saves by branch and marks each branch's latest save.
- Houses now have a limit on permanent residents, set by their size and family make-up, and expanding a House adds places. A baby always joins its main caregiver's home, even if that makes the home overcrowded.
- Renaming an agent now refuses a first name already chosen by another living or deceased agent. The Profile tells you to choose a different first name and keeps the name field open with your attempt in it. Renaming keeps the same person and preserves past spoken lines, and a player-entered name makes no model request.
- A malformed provider model list now shows a retryable check error instead of disabling models for ten minutes.
- Adults can replant reachable local stumps on islands. A blocked or unreachable nearer stump no longer hides another stump they can reach.
- Device pairing now times out if the host stops sending a response, so the
  connection controls become available again without restarting the game.
- Households can make storage pots and water jugs from clay, store food in pots to slow spoilage, and carry fresh water in reusable jugs.
- Hungry adults can set down a spare filled vessel with all its contents to make room for food. Reserved contents stay in place, and the entire vessel load must fit the destination.
- A full carrier can make room for food stored in the household pot. Returning jugs and spare food leave room for incoming House deliveries, and pot-storage choices require a reachable House.
- Caregivers can feed an infant from the household pot, and a pot serves only food that can be eaten now.
- The map uses the pixel art approved in the October 1 art review. Grass, forest, sand, rock, snow and tundra ground has soft, calm patches; water has gentle depth; Roads are calmer packed dirt; and bridges are plank decks that the street runs straight onto. Trees, bushes, outcrops and clay banks have clearer light and outlines, and the House, Warehouse, Blacksmith, Silo and Tailor Shop show real roof materials, a visible door and one telling feature each, such as the forge yard.
- Agents now face the way they last moved, in eight directions, take walking steps as they move, and show when they are carrying, working, talking or hurt.
- Water, wild greens, potatoes, porridge, stew, meals and diamonds have their own item icons instead of a crate, and wood, stone, bread, clothing and the wooden axe have redrawn icons.
- Mountains, peaks and hills are drawn as one landform: a mountain range now reads as a single mass with a snowy crest instead of a grid of little triangles, and hills are soft foothill shading instead of rings.
- Farm fields show tilled soil with grain, potatoes or greens drawn at each stage, from seed to harvest. Scrub, dry brush, desert brush and tundra snow ground are redrawn to match the rest of the map.
- The Farmhouse shows grain sheaves and sacks, and the Workshop is redrawn. Every item has its own icon, and a few interface icons are clearer.
- The Main Menu valley has more detailed snowy mountains, by day and at dusk.
- Desert cactus cover now shows barrel, saguaro and prickly-pear cacti on some of its tiles, and snow thins into clumps and frost at its edges instead of ending in a flat white band.
- The map, pencil, play, speech and weather icons are clearer.
- The owner can remove a building or change its household/Town assignment from
  Building Details. The host rejects changes for a different selected world or stale ownership and any removal or
  reassignment while the building holds stored or inbound stock or active
  production/expansion work. Private-building ownership stays separate from
  Town title; moving a Warehouse updates the recorded assignment without
  shifting borders. Removal keeps Town borders and Roads and preserves
  completed work history.
- Saved worlds can record multiple Towns with unique current resident
  memberships. Residents use their recorded Town Warehouse even when it lies
  outside the border. Any agent may physically collect unreserved communal
  stock from an empty Town, with access checked again at pickup. Helper goods
  still transfer to the recipient's House, or directly to the requester when
  no House exists.
- Agents make and equip padded coats, rain cloaks, baskets and sacks, with weather protection, carrying limits, wear and private household repairs. Broken or replaced gear keeps its goods, hungry adults can set down spare supplies with their household to make room for food, and the agent card shows equipment, cargo and repair progress. Before a household has a House, a helper meets the adult who asked for building materials and transfers only what fits; the builder can still stage their own materials at camp. If a household recipe cannot get its ingredients, it pauses so an adult can choose another task, and a fresh choice can resume the saved plan when supplies are available. Farmers keep a fresh-food planting choice when someone briefly occupies its field.
- An adult carrying the full cost of a first House can build without collecting an optional work-speed tool that would exceed their load limit.
- Hungry adults can set down broken or spoiled spare cargo without losing it. Full carriers no longer keep an unpaid household recipe waiting on a pickup they cannot carry. Choosing a conversation releases a repair's unspent materials, and a farmer with a paused build plan keeps their selected planting claim.
- Repairs with expired material reservations stop safely even while an agent waits for a model reply.
- A save with an empty entry in any saved list, or an unplayable world size, no longer hides healthy worlds in Load World. The damaged save is refused and kept for recovery.
- After the server starts, it checks saved worlds in the background, so once those checks finish, the first Load World list opens as quickly as later ones.
- Load World reuses checks for unchanged worlds, so reopening its list is faster. It still notices changed or damaged saves and missing history or model keys.
- New worlds have denser grass forests, beaches in shorter patches, cacti only on desert sand, and less frequent rain, storms and snow.
- Default Balanced Small/Medium previews compare up to three maps for each applicable Normal forest or mountain target, show the measured coverage, and create only the exact previewed map after any misses are accepted. Low/High controls remain distinct, and incompatible climates receive no target.
- A versioned Normal trial targets 20–40% forest and 5–12% mountains on dry land. The fixed Small/Medium × wrapped/bounded seed matrix uses the player defaults of 50% water and hydrology version 1; its selected maps cover 26.82–30.20% forest and 5.28–10.43% mountains. These are automated coverage checks; Windows visual tuning remains in the playtest list.
- Save state records the selected candidate and visibility algorithm version. Restore regenerates the same candidate and refuses a map that no longer matches deterministic generation; the source save is left intact.
- Nearby agents can hold public conversations using their own assigned models. Speech bubbles and each agent's history open a reader showing who spoke and who heard each line. Escape closes the reader while keeping the selected profile open.
- Conversations keep up to six public turns and one wrap-up. Both people see the same wrap-up and proposed effect before accepting it; interrupted talks require fresh agreement from both after reloading. Ordinary lessons wait while either participant is talking.
- Older alpha private-world saves can no longer be opened. The game refuses them and keeps the save files unchanged.
- Agents can till household fields on fertile land with a hoe, plant grain, potatoes or cultivated greens, tend and harvest them, and save planting stock for the next crop. Harvests stay on the field until carried to storage: raw crops and planting stock go to the Farmhouse or Silo, and fresh greens and fruit go to the House. Soil inspection describes fertility in plain words, and worked soil and crops appear on the map and overview. Orchard seeds can grow new fruit trees that produce fruit in autumn. All quantities and timings remain provisional.
- The family tree panel now grows to fit shorter trees and keeps longer trees inside a scrollable area on screen.
- A newborn keeps the model chosen by its parents, with the parent who started their family plan breaking a disagreement. Infants make no model calls. After infancy, a child uses that model only when its own saved key is available and normal call checks allow it; otherwise built-in choices keep the child safe, with no paid fallback. The Model panel also keeps showing that choice while its settings await recovery. The owner can change the child's personal model later, or explicitly leave the child without one. That choice is saved; a child without a personal model continues on built-in rules and never borrows a world default.
- An adult with no household can ask a household that holds a House in their Town to take them in. Every adult member of that household must agree; one refusal or no answer ends the request, and that household is not asked again for two world days. Standing beside a House still grants nothing, and a pending request grants no access to the household's food, stock or shelter.
- An adult without a home is told why on their card and in their own model request: no household, a House still to plan, missing materials or no legal site. The Event Log says when someone asks to move in, moves in, or is turned down.
- Saves now carry pending housing requests and recent refusals. Saves from older builds that already carry them are refused and kept.
- Load World shows a small map of each world on its card. Worlds made before
this change get theirs the first time the list shows them.
- New World, Load World and Save World are easier to use. Load World and the
save lists show each world or save as a card with when it was saved, and tag
the current world, worlds that can't open and autosaves. New World groups its
choices in boxes, uses Low, Normal and High buttons and a water slider, and
keeps the rarer choices under More options. Save World starts each new save
named after the world's date, so one click saves.
- Settings is easier to read. Each group of Game settings sits in its own box,
the scrollbar no longer touches the drop-downs, and the model call count and
limit have their own box on the World page. Several settings use shorter
wording. UI Scale and Render Resolution are removed: menus, panels and text
grow with the screen in whole steps (200% at 1080p and 1440p, 300% at 4K) so
pixel letters stay crisp, and the game always draws at the screen's own
resolution.
- A damaged skills record no longer hides healthy worlds in Load World. The
  affected checkpoint is refused and preserved for recovery.
- Agents start with no skills and first learn building, farming, crafting or smithing by finishing that kind of work. Adults can ask a free, healthy agent who knows a skill for a lesson. A completed lesson keeps the teacher and time on the agent card, without assigning a job role. Skills do not yet change action access or work speed.
- Skills and lesson progress survive current save/load. Older alpha lesson records may stop loading; no migration is included.
- Choosing the first Town now shades more promising buildable land and explains nearby food, fertile ground, wood, stone and space for Roads. The advice is provisional and does not limit site choice; the game still checks whether the Town layout fits.
- Houses and Warehouses can expand their storage while keeping the same building, stock and cooking jobs. Construction reserves its space and materials, and a stale expansion returns its materials without changing the old footprint. Building inspection shows storage use, expansion progress and invited storm guests.
- Any adult household member may invite a named guest to shelter during storms, or revoke the invitation. Invitations survive saving and loading; guests have no access to household stock or cooking.
- Trial values for playtesting: 64 stock units per footprint tile, an 80% full threshold, 20 ticks of work, four wood per added tile, and an additional two stone per added Warehouse tile. Footprint variants may rotate to fit the available space. These values are provisional.
- Project helpers deliver materials at the recipient household's House without gaining stock access. An agent building its first House keeps new materials personally carried until they are used, rather than creating household stock without a location.
- Agents keep legal model choices and valid chosen names even at low confidence. Model replies no longer need a probability map, and failed attempts finish without an automatic paid retry. Agent cards show the latest model request's status separately from the last accepted choice, including after save/reload. Older builds may refuse these alpha saves.
- Add an explicit model setup check that makes one paid call and reports a bounded result.
- Personal models get one metered chance to choose a different first name when their first valid choice uses a taken first name. The game keeps the placeholder if that retry is duplicated or unusable.
- Add Agent now refuses tiles where household ownership or Town borders overlap. Its preview shows where the adult will belong, and the server checks the current placement again when you confirm.
- Small fixes from the October 1 playtest. Settings and the Mod Library now
open inside the Pause Menu window, with a back arrow, so the menu fits on
screen at large UI Scales. Developer tools no longer make the menu taller.
The Pause button's word is one pixel bolder and the button keeps its width
when it changes to Paused. The Event Log's unread count is a true circle.
The Quit Game icon matches the other icons' size, its confirmation no longer
says the game will close, and the Main Menu river no longer sparkles on top of
the bridge.
- Newly placed adults can choose their own personality and aspiration in their
  first personal-model reply. The choice is saved, shown on the profile and
  included in later requests. Missing or invalid text keeps the placeholders
  without another call, and later replies cannot overwrite the choice.
- Clicking a building opens a small card beside it with its owner, the work
  in progress or who is inside, and its stored items as icons with amounts.
  **Details** docks the rest on the left: who may use it, when it was built,
  its door side, work, a named storage grid and who lives there.
- Stored items have hand-drawn pixel icons, including orchard fruit and tree
  seeds; an item without its own icon shows a crate.
- Households can build a Tailor Shop. There they weave plant fiber into cloth and sew cloth into clothing, bringing fiber in from their own stock or gathering it. An adult without clothing collects a finished garment from the shop.
- The Weaving frame and its "Woven clothing" are gone. Each starting agent's garment now waits in their household's House.
- Saves from worlds that were running before this change can no longer be loaded.
- Rivers can now get bridges. A crossing that agents often wade across (six crossings by at least two agents within two world days) gets a bridge, and when a Town grows, a new side street or a street running on past a door crosses a river up to two tiles wide on a new bridge. Bridges are permanent, cost nothing, are walked at dry-ground speed, and show on the map, tile card and hover readout. No second bridge is built where one already joins the same connected river banks.
- Models read agent, Town and House wording, with readable household and Town
  names. Jev requests omit internal run counters, and personal models no longer
  receive the ranking scores used to select their memories. Agent model settings
  and the instruction tooltip use the same wording.
- Households plan the buildings they need again: a House, Farmhouse, Blacksmith or Silo they do not hold yet, one of each, once they have the materials. Agents gather missing stone or wood first. The new Silo stands beside or near its household's Farmhouse and stores its harvests.
- An agent carrying supplies into a household building no longer hands the same load back to help a project, which could repeat for hours.
- While one agent waits for a busy field or workstation, others are not offered new work there, so the waiting agent gets the next turn.
- Agents no longer build approved owner building designs, since households plan only their own kinds of building.
- Agents can plant trees. Felling a tree gives a tree seed, and an adult carrying one can replant a stump or plant a new broadleaf or conifer on grass or forest ground outside the Town. Trees are never planted on sand, water, buildings or Roads, and a refused planting keeps the seed. Saplings grow into trees after 3 days, are saved with the world, and the Event Log shows each planting. Replanting a stump now needs a tree seed rather than grain seed.
- Orchard fruit trees now bear fruit only in autumn and drop any fruit left when autumn ends. New worlds start in spring with orchards not yet fruiting.
- Roads draw as joined packed-dirt tracks with rounded bends, smooth diagonals and a doorstep path to each building's door, with a darker edge on sand and snow.
- New worlds keep sand to deserts and stretches of ocean beach, so rivers and lakes have grass banks, and trees and plants no longer grow on sand. Forests show groves of trees on darker forest floor among scattered trees on forest grass, and a band of hills now rings each mountain area. Hills look different but cost the same to walk and build on as grass. The numbers behind these rules are provisional.
- Worlds generated before the new terrain rules can no longer be loaded. They are refused and their saves are kept; a host whose active world is refused will not start until that save is moved aside.
- Towns grow along their streets: building sites that can face a Road rank higher, each new building's street runs on a few tiles past it, and a building away from the Roads gets a new side street that can take diagonals.
- Town borders now show as a pale dashed line, and map Filters start off.
  Placing a founder or an added agent still shows Town borders and household
  property without turning Filters on.
- A new world's first Town is laid out along a winding main road with side streets that take diagonals where the land allows. Its buildings face those streets, the streets run on a few tiles past the last building, and the Town border keeps about three tiles of spare land around the buildings and Roads, following the Town's shape.
- Players can save named OpenAI and Ollama Cloud API keys in Game Settings before
placing agents. Keys use the host's existing private credential storage and can
be reused from the agent key picker. Saving a key does not call a model.
- Load World waits for a slow first check of saved worlds instead of failing after
  15 seconds and requiring the screen to be opened again. Back still cancels the
  check, and incompatible saves remain blocked.
- Add Agent's provider, key and model controls stay in place as the placement preview changes. Moving into the panel or opening a picker or text menu keeps the last visible placement preview instead of showing land hidden behind the interface.
- Agents now farm, mill grain, make tools and mine ore through the buildings their household holds, without needing a work role. In a new world the first household holds the Farmhouse and the second holds the Blacksmith; a household cannot use a building it does not hold.
- Buildings show their door on the side that faces their Road, not always on the bottom edge.
- Caregivers can gather reachable food for infants when their household food store is blocked, instead of repeatedly heading for supplies they cannot reach.
- Cold agents no longer get stuck fetching blocked stored wood when they can gather local fuel. Shared fuel and equipment choices now check the route to their collection point.

- An agent-card **Order** the game can't act on, such as "build a house", now closes at once, and the Event Log says the agent didn't understand it. Before, it never finished, asked the agent's model for a new decision about once a second and held up later instructions to that agent. An order that can't be carried out yet, such as "eat" with no food, still waits, but without the extra requests. Orders are now read as whole words, so "heat" no longer counts as "eat" and "good" no longer counts as "go".
- A damaged inactive checkpoint with missing required state no longer breaks the entire Load World list. It is marked incompatible and cannot replace the healthy active world.
- Loading or overwriting a damaged manual save reports a recoverable conflict before creating backups or changing the active world, instead of returning a server error or accepting invalid metadata.

- Keep descendants able to explore and save even when their inherited identities are long.
- New World explains when the game and world server need matching updates, keeping valid device pairing instead of asking players to connect the device again.
- New agents choosing names receive a stable first-letter suggestion to encourage variety without adding a model call.

- A mandatory harvest instruction completes after gathering orchard fruit, allowing the agent's next queued instruction to proceed after saving and loading too.
- Ollama Cloud's DeepSeek V4 Pro and Gemma 4 choices use the model IDs returned by its live list, so a valid key does not incorrectly grey them out.

- Model requests now use the game's own wording for agents, apart from one phrase in the instructions, and no longer send internal clock and counter values to the personal model. A few choice descriptions no longer mention the old camp.
- Agents gather heating fuel, help with other agents' projects and Blacksmith ore from reachable local resources, including island Towns disconnected from the original map anchor.
- Hungry agents and caregivers skip blocked food routes and can gather from another reachable source instead of repeatedly getting stuck.
- Storm refuge selection checks reachable, unoccupied routes, so an inaccessible nearby forest does not hide usable cover.

- Prototype survival priorities leave more room for ordinary activities below comfortable fullness and warmth. Urgent food or continuing cold exposure still interrupts discretionary work, while safe nearby care remains possible. Routine food and outing reserves remain provisional for playtesting.
- Add confirmed permanent deletion of one selected save, or an inactive world and all its saves. The active world and other worlds stay intact.
- Damaged leftover saves no longer stop host startup during pending deletion cleanup; failed cleanup stays pending and preserves unverified files. Missing save identities in deletion metadata are handled the same way.
- New World adds a compact Advanced section with 20–80% water (50% default), Low/Normal/High forest, mountains, rivers and resources, climate choices, wrapping and latitude cooling. Reset restores the full supported preset; preview and creation use the same saved settings.

- New worlds keep inland lakes smaller and route rivers into lakes instead of around their shores, while preserving the selected open-water area. Existing worlds retain their saved geography.
- Load World shows saved-world checks in progress, displays delayed results on the first opening, and ignores replies from closed or superseded lists.
- Save World ignores replies from closed or earlier openings, preserving the current selection and keeping Overwrite working when an older list arrives late.
- Opening a world keeps the chosen world even if its list refreshes, and Open and Delete wait for the current world action to finish.

- Remove the redundant Load World tooltip from the Main Menu.
- Crafting agents can choose a reachable free Workshop instead of repeatedly trying to enter an occupied one.

- Personal models receive their own saved name, life stage, personality, aspiration, household, survival condition and most recent private thought; unknown conditions stay unknown.
- A failed active recovery write holds the in-memory world paused and retries saving without advancing or resuming paid work; other tick faults halt for inspection.

- The Main Menu now opens on a pixel-art valley instead of a plain brown screen: snowy mountains, patchwork fields, a winding river and two small Towns with a Farmhouse, Warehouse, Blacksmith and Houses. Clouds drift, chimney smoke curls up, birds fly past and the river sparkles. With the Dark theme it becomes dusk, with twinkling stars, glowing windows and lanterns, a flickering forge, fireflies and the moon on the river. The scene stays behind Main Menu Settings, New World and Load World, and holds still once you are in a world.
- World selection and named checkpoint loading verify required history segments before replacing the healthy active world. Missing or corrupted archive chains remain recoverable without becoming the active save.
- Simultaneous lesson refusal and student cancellation safely preserve the terminal lesson instead of halting world advancement.
- Forgetting registration or activating a new pairing clears the previous host’s observation and terrain cache, so Continue can enter a younger world without restarting the game. Recovered activation follows the same rule.
- World Connection now says whether Connect reached the paired world, instead of leaving a successful check invisible.
- Create/Load World resets the observation timeline before sending the switch, so a lost response cannot trap reconnect on the old world’s event cursor.
- Interrupted world selection restores routing and autosave settings during startup, before Resume or owner mutations can be acknowledged.
- A mandatory instruction stays pending when its requested action is unavailable or makes no progress; unrelated accepted idling no longer counts as completion.
- Food choices use the acting agent’s connected ground, so an adult on another island can harvest its reachable local food instead of seeking an unreachable camp resource.
- Crop planning and preparation use the worker’s actual household stock. Household cooking rechecks on-site ingredients, and failed production starts enter a recoverable blocked state instead of remaining stuck at completed preparation.
- Production requests now refuse workers who are too young, before taking materials or creating a job, using the same age limits as agents' own choices.
- Adults can only propose barter to another adult or elder, so a trade can no longer reserve a child's belongings when the child cannot answer it.
- Loading a save and switching worlds can no longer archive a mixture of one checkpoint and another set of model/autosave settings.
- Manual overwrite atomically publishes a complete checkpoint/model/settings generation; a failed metadata replacement keeps the prior selected save and its recovery backup.

- Scouting safely starts a new local path after another action moves the agent away, instead of joining nonadjacent steps and breaking saves.
- Returning explorers keep their destination and discoveries when detouring around a resident, including after save/reload, instead of starting another outing away from home.
- Inherited physical maps and field records keep their lot identity, preserving the knowledge artifact link and saveability without broadcasting their contents.
- Start World keeps time paused until its checkpoint is saved; a failed write leaves setup retryable instead of running an unsaved world.
- Failed founder checkpoint writes roll back the founder and provider configuration. World selection and founder setup share one transaction gate so successful placements keep their model assignments.

- Death cancels unfinished barter through the normal two-sided release, immediately freeing the survivor’s stock without cancelling completed exchanges.
- Inventory lot splits keep stock committed to reservations and barter in the original lot, so those commitments can still be fulfilled.
- Resume keeps the world paused until its running checkpoint is saved, and retries cannot report success while saving still fails—even when an earlier request left memory running.
- Successful pause and rename retries now persist the acknowledged state even when a failed earlier save already changed it in memory.
- Retrying an accepted instruction still confirms its original receipt after the agent dies or the host restarts. Instruction keys with surrounding spaces no longer fail on an exact retry or skip a sequence number.
- Retained instructions stay tied to their original world. Retrying after selecting another world cannot issue the instruction there; return to the original world to recover its receipt.

- Starting households are named First household and Second household instead of
  Camp Alpha/Beta. Existing default camp names display the new wording without
  changing saved membership, property or custom household names. Founder setup
  instructions are shorter and describe the two-household grouping plainly.

- Public pairing creation is limited to eight attempts per minute and pairing
  request bodies to 16 KiB. Signed owner actions remain available. Host-local
  recovery can replace one unapproved request in a full pairing queue without
  discarding an approved pairing or revoking an active device.

- Loading an older world removes only Road tiles embedded inside saved building footprints, preserving buildings, ownership, stock and all other Roads.
- The Event Log and unread badge now select explicitly supported player events, including public partnership, care, trade and Town policy milestones; new internal event kinds no longer appear automatically as humanized diagnostics.
- The Event Log keeps agent names for food, births, deaths and Town membership, including renamed and deceased agents whose IDs contain colons.
- Changing autosave rotation trims only the selected world, including Rotation off; other worlds’ checkpoint files remain untouched.
- A delayed autosave reply from an earlier world or Settings opening can no longer replace the current world's choices or enable Apply.

- Invalid hosted choices and out-of-range confidence complete with safe idle instead of repeatedly consuming model calls for the same decision.
- A late model reply no longer cancels the agent's newer pending choice; the current reply can still be accepted.
- A damaged model-call meter no longer prevents the host from starting. Paid
  calls stay blocked, and World Settings explains how to restore accounting
  without losing spent calls. Meter writes flush before replacing the file.
- Pause/resume waits for a pending owner action instead of disappearing; Quit to Menu requires a saved pause receipt and can retry a failed pause instead of trusting an unsaved paused observation. Signed owner operations time out across challenge and response bodies so stalled reads release the controls.
- A stalled world refresh stops after four seconds instead of holding the
  client for the default network timeout. Owner actions cancel an older refresh
  so it cannot overwrite their result; Pause remains available during polling.
- Map name tags use a whole given name or initial instead of cutting full names
  after seven letters. Activity symbols distinguish exploration, warmth, trade,
  social activity and care. Resource captions use ordinary case and disappear
  at overview zoom; full names and resource facts stay in hover/inspection.
- Held movement keys now pan smoothly with elapsed frame time, with equal
  straight and diagonal speed. Error notices give a plain recovery hint instead
  of showing raw exception text, private file paths or server responses.
- On Windows, saved provider keys are protected for the current Windows user.
  Valid old key files are migrated when loaded; an unreadable protected file is
  preserved rather than reset. Forgetting a key still removes it from the
  current installation, not from independent backups or the provider account.

- A new look for every menu and panel: wooden frames around parchment
  panels, ink text and chunky pixel buttons, with green for the main action.
  Settings has a new Theme choice of Light, Dark (dark wood with cream text)
  or Match system, which follows your computer's setting. Switching applies at
  once and is remembered.
- Settings can turn off the drifting cloud haze and the soft lightning flashes
  in storms. Both are on to start with and your choice is remembered.
- Escape now backs out one step at a time: a focused text field, an open
  menu, Town-site selection or a founder move, the newest open panel, then the
  selected agent. With nothing open it opens the Pause Menu. The mouse wheel
  now zooms toward the pointer instead of the screen center. The open Settings
  category shows as a selected tab instead of looking disabled, and New World
  labels its name and seed fields.
- Keyboard shortcuts in the world: Space or P pauses and resumes, N and
  Shift+N step through living agents and bring each into view, C re-centers
  the selected agent, H returns to the Town, + and − zoom, and M, F, I, R, T
  and E open the Map, Filters, World Info, Agents, Town and Event Log. F1 or ?
  shows the full controls list, and top-bar tooltips name each key. Clicking
  a top-bar button or the map no longer leaves the arrow keys and Space stuck
  on that button.
- A small readout in the bottom corner of the world view names the ground
  under the pointer: its surface, forest or water, any building or resource
  there, Road, the Town it belongs to and its tile position. It doesn't take
  clicks away from the map.
- Plainer in-game wording: the agent card names households and relatives
  ("Member of Camp Alpha", "Parent of Mira") instead of internal IDs, leaves
  out an unassigned role or unreported condition, and reads "Wants to take it
  easy" instead of "Wants to keeping a safe routine". When a model does not
  provide a usable choice, the card says built-in rules chose the safe action;
  local action failures are no longer described as lost host connectivity. The
  non-model option is called Built-in rules instead of Deterministic. Status
  messages drop request, revision and tick jargon ("World paused", "Connection lost · showing
  the world as of …"), and placing a founder or agent names the household they
  joined.
- Fresh generated worlds no longer show a second pre-placed camp or provisional
  Town beside the player-chosen five-building site. The New World preview no
  longer marks a supposed starting camp. Older saves keep their original map
  identity and camp objects rather than being rewritten on load.

- Starter Town and later Town-building Roads now connect beside building
  entrances, never through their footprints. New branches use connected
  orthogonal ground tiles instead of painting a building anchor as Road. Future
  construction sites cannot overlap an existing Road.

- Small/Medium world zoom-out now stops before showing empty space beyond the
  map, while larger maps retain a shared 8 px overview floor. Maximum zoom-in
  now frames roughly the same world height across 720p and 1440p render sizes.

- Removed the noisy per-tile grain, striped surface edges and large circular
  cloud overlays from the provisional world view. Rain and snow marks still
  show regional weather without changing the simulation or saved terrain.

- Game Settings now opens fullscreen by default while remembering a windowed
  choice. UI Scale enlarges window widths as well as text and controls. Main
  Menu Settings uses a single compact back button and shorter setting labels;
  Load World drops redundant helper copy. The agent model panel and model-call
  limit use plainer wording; the card no longer retains an oversized height
  after a view change. Tile
  inspection omits facts that are absent or not yet measured instead of
  showing `none` or `unavailable` placeholders.

- Hosted-model usage now defaults beside the configured private provider state,
  so a root-owned application directory cannot silently stop agents from
  thinking. A usage reservation that cannot be saved no longer counts as a
  model attempt or survives as a phantom pending call.

- During paused four-founder setup, select an already placed founder and use
  Move founder to choose another passable tile. The founder keeps their
  identity, household, Town membership and model assignment; the new position
  survives reload. The last placement can also be undone, removing that founder
  and their personal-model assignment while leaving shared keys available.
  Start World closes both edit controls.
- The paused-world host can accept or redo a deterministic five-building first
  Town layout before any founders are placed. Its two Houses belong to the two
  starting households, while Farmhouse and Blacksmith claims stay unassigned;
  the chosen site, Town border, buildings and connected Roads survive reload.
  The signed Godot New World setup now offers a map-click Choose/Redo Town site
  control before Add founders. Accepting the site locates each starting
  household's existing food in its House and puts one usable wooden axe and one
  wooden pickaxe in the communal Warehouse. Redoing a site moves the food with
  the Houses without duplicating tools. Supply choices, final quantities and
  suitability guidance remain open. Save schema 25 records the selected site
  while older Town saves retain their camp-derived border.
- Newly created worlds now load the built-in House, Warehouse, Farmhouse,
  Blacksmith and cooking definitions during paused founder setup, before the
  first simulation tick. The five starter buildings are placed when the player
  chooses a rough site, not automatically when the map is created.
- Adults in a household with both a Farmhouse and a House can carry milled
  flour from its Farmhouse to its House. Flour remains with the carrier until
  arrival, then becomes inspectable private House stock, including after a
  save/reload. This does not yet add bread or other flour-based cooking.
- A provisional household-claimed 1×2 Blacksmith joins the additive first-Town
  content. Its on-site stock can receive physically carried household wood;
  adults can mine natural iron ore and carry it to the building. Household
  members can make distinct wooden axes and pickaxes or refine ore into a
  separate iron item there. Another household cannot work
  the building. Carrying the matching wooden tool increases one resource-site
  wood or stone/ore gathering action from four to six units. The first-Town
  Blacksmith footprint is now generated but initially unclaimed; tool
  durability, sales/requests, later tool tiers, footprint and cost balance
  remain unfinished.
- Household builders can claim a provisional 1×1 Farmhouse. A distinct
  universal-grain crop now yields household-owned grain; household members
  carry grain to their Farmhouse before milling it into flour. Only that
  household can process there, and the resulting flour remains inspectable
  on site after reload. Unclaimed Farmhouses cannot produce, and food crops or
  flour cannot be stored in the communal Warehouse. A field's output now goes
  to its actual farmer's household instead of the old camp-alpha fallback.
  The first-Town Farmhouse footprint is now generated but initially unclaimed;
  farm fields' full growth states, private Silo, sales and final
  recipes/footprints remain unfinished.
- Town residents can build one 2×2 Warehouse inside their Town's growing
  building area. Adults may carry spare personal wood, stone, fiber or seeds
  there as communal stock; residents of another household can collect stored
  tools, clothing, wood or seeds there for work. The building inspection shows
  the actual stored lots after delivery, and they survive reload. Food cannot
  be Warehouse stock. The old Storehouse remains loadable in existing saves
  but is no longer offered for new agent construction once Warehouse content
  activates. Expansion, full resource logistics and formal ownership remain
  unfinished.
- Town-assigned building placement now generates world-owned Road tiles along a
  bounded legal dry-land route to the camp or existing Road network. Roads are
  visible in the world and overview, named in tile inspection, survive reload,
  and give a provisional route/diagonal-travel advantage; ordinary walking does
  not paint Roads. A building with no legal dry route stays unconnected. Initial
  Town layout, river bridges, inter-Town links and final Road-speed rules remain
  unfinished.
- Members helping another person in their own household with a project now
  deliver carried materials at their shared House. The contribution becomes
  household stock there only on arrival and survives save/reload. A helper
  outside the requesting household keeps the camp handoff; invitation and
  private-House entry rules remain undecided.
- A member carrying gathered materials for their own household's project now
  delivers them at their House instead of the old camp stockpile. The materials
  become household-owned stock at that House only on arrival and remain there
  after save/reload. Projects without a member-accessible House keep the camp
  route; other gathering, cross-household helper and production deliveries
  still need physical logistics.
- Adults with spare personally carried food can bring a bounded load to their
  own House while keeping one serving. The transfer happens only after they
  reach the House, becomes household-owned stock at that location, and survives
  save/reload; a resident without a House cannot use this route.
- Add Agent placement now previews the saved Town and recorded household owner
  under the pointer and reveals their map overlays. Placing an adult on an
  owned building footprint joins that household and the enclosing Town, if
  any; even an occupied House tile can accept another member. Placement
  on unclaimed Town land joins the Town without a household or access to a
  founding household's private stock; outside Town borders, it starts a new
  independent household. Moving later does not change that affiliation.
- The top-bar Filters panel can hide the saved Town-border outline and show
  household-owned building footprints with stable household colors. It never
  tints unclaimed land as property; selecting a tile names its recorded
  household property owner or says none is recorded. Filter changes follow
  new observations without changing the underlying Town or ownership facts.
- Adult household members can carry bounded loads of older camp supplies to
  their own House. The load remains with its carrier through save/reload and
  appears in the House's inspected stock only after the carrier reaches home;
  invalid delivery destinations are rejected on reload. Residents without a
  household cannot use the first household's House through the legacy camp
  fallback. Other gathering, cross-household helper and production paths still
  need physical delivery before all household supplies live at the House.
- Meals finished at a House now record that House as their physical stock
  location. Its map inspection shows only items actually recorded there, and
  household members enter the House tile to collect those meals, cook, or take
  refuge rather than using it from an adjacent tile or the old camp store.
  Multiple household members can share the House tile without a headcount cap.
  Children carrying spare food deliver it to their household House when one
  exists. Invalid or missing storage buildings are rejected on reload; older
  household stock remains unlocated until a hauling path moves it into a House.
- Members can now cook a household meal at their own House. The job reserves
  only food and wood actually stored in that House and returns the meal there
  after save or reload; outsiders cannot start the job, and remote camp supplies
  cannot be used as if they were already at the hearth.
- A first 1×1 House design can now be built for a specific existing household.
  Its ownership survives save/load, appears on the building's map inspection,
  charges construction materials to its household, and limits storm refuge and
  lit-house warmth to that household. Legacy Shelters remain loadable in existing
  worlds while larger Houses, physical household storage and guest invitations
  are still being developed.
- Game Settings now offers a locally saved UI Scale from 100% to 200%, making
  controls and text easier to read without changing the selected map render
  resolution or terrain detail.
- Generated Small/Medium worlds now visibly distinguish grass, forest floor,
  beach sand, dry scrub, rocky upland, snow and saved fertile-land sites, with
  subtle deterministic ground variants and readable coast/surface edges. Dry
  climates can show cactus cover. Natural sites now include berry bushes, wild
  greens, fiber plants/reeds, stone and iron/gold/diamond outcrops, clay banks,
  orchard trees and woodland trees; each retains an inspectable object kind and
  resource state, and trees remain separate objects with at most one tree per
  tile. Mineral outcrops and clay banks are finite, inspectable deposits only—
  recipes and processing are not included. Fertile-soil ground marks an actual
  saved fertile-land site; it does not represent a general fertility estimate.
  The new terrain and object marks are code-drawn prototypes rather than final
  production textures/sprites. Existing generated saves without the new natural
  object details and geology sites remain loadable.
- The paused New World setup now has a persistent first Town. Founders join it
  as they are placed; its amber map border and World Info show only its saved
  founding state, residents, and assigned buildings. Adults placed within the
  border join the Town, and Town-assigned buildings can expand it. Existing
  founder-setup saves migrate without inferring unrecorded adult membership or
  building assignments.
- Building plans now offer agents up to five ranked legal sites with reasons
  for access, clear ground, Town growth, nearby materials and related purpose.
  The agent chooses the site or declines by choosing another action; an
  accepted tile stays with the project. If it becomes illegal, fresh choices
  return after 60 ticks. The ranking follows the prototype's single rectangular
  Town border and does not yet model land claims or multiple Towns.
- After an agent dies, their assigned personal model can make one bounded final
  choice to leave frozen personal belongings on the household inheritance path
  or give the whole estate to a living agent. Invalid, unavailable, timed-out or
  interrupted choices use the household default. The result survives saves and
  appears on the deceased agent card and in the Event Log.
- Agent memory inspection can distinguish owner-private beliefs from social
  memories, showing firsthand/hearsay/inference provenance, confidence, and
  retained correction history. Beliefs stay out of the public event log;
  schema-19 private saves remain loadable and migrate to schema 21.
- Optional Jev memory assistance now scores small batches of one agent's
  existing memories and beliefs during Jev's normal routine decision call.
  Personal-model decisions can retrieve up to four relevant source records with
  their provenance, confidence, and correction status; Jev-off retrieval still
  works locally. The source text is not rewritten, and compaction adds no
  separate provider request.
- Local exploration now gives each agent a bounded personal ledger of terrain
  and resource-site facts. A completed outing can produce a physical field
  record or map that the agent may share nearby or barter; the recipient learns
  only those recorded sites, with discoverer and source tracked. Personal-model
  decisions receive only that agent's bounded facts, and the Godot Memories and
  Maps view shows their knowledge and held artifacts. Schema-21 and schema-22
  saves load with an empty knowledge ledger and migrate to schema 23.

- Choosing an agent's model no longer means typing its exact name. In Add an
  agent and in an agent's Model settings, the key is chosen first, and the
  model comes from the game's own short list, newest at the top: GPT-6.1 Sol,
  GPT-6 Astra, GPT-6 Sol and GPT-6 Luna for OpenAI, and GLM 5.3 Flash,
  GLM 5.3, DeepSeek V4.1 Flash, DeepSeek V4 Pro, MiniMax M3, Kimi K3 and
  Gemma 4 for Ollama Cloud. Models the key can't use are greyed out. If the
  key can't use a new agent's model, no other model is chosen for you: the
  picker asks you to choose one first. An existing agent's model stays shown,
  greyed, with the same request.
  **Type a model name…** covers any other model, and an agent already on an
  unlisted model keeps it as a typed name. If the key can't be checked, the
  reason is shown with **Retry** and the list stays usable. The host checks
  keys, so they never leave it, and a newly pasted key is not saved until the
  agent is placed or the change applied.
- New OpenAI agents now start on GPT-6 Luna (`gpt-6-luna`) instead of
  `gpt-5-mini`, and new Ollama Cloud agents on GLM 5.3 Flash
  (`glm-5.3-flash:cloud`) instead of `gpt-oss:120b-cloud`. Agents and saved
  settings that already name a model keep it.
- The map is quieter. An agent's name appears only while you hover over or
  select them, as outlined pixel letters without a dark box (gold for the
  selected agent), and no longer carries an activity symbol. Buildings no
  longer show their names on the map; hover over one to see its name.
- The Main Menu now shows the ClankerWorld logo instead of a plain text title and slogan: wood-grain letters either side of a friendly robot waving in front of a little planet, floating over the valley above the menu buttons. The line of text in the menu only appears when something needs your attention, such as connecting this device or reaching your world. The robot and planet are also the game window's icon and the Windows program's icon.
- Agents no longer start Shelters, Storehouses, Cooking fires or Stone
  hearths. A household's House now provides shelter, cooking, warmth from its
  fire and food storage, and a Town's Warehouse holds shared supplies.
  Experienced builders also stop suggesting shelter, storehouse and hearth
  designs. Worlds that already have these buildings keep them working,
  projects for them that are already under way still finish, and designs
  suggested earlier stay in the Mod Library. An adult without a household
  cannot build a House yet, so in a new world clothing and natural cover are
  their only protection from cold.
- Selecting an agent now opens a small card beside them instead of one tall
  card: their name, what they are doing, pixel bars for fullness, warmth and
  illness, and **Profile** and **Speak** buttons. **Profile** docks the rest on
  the left of the screen: a portrait, a pencil to rename, age, diet, belongings,
  work and learning, private thoughts, people, Memories, Family and Model, and a
  message box with a **Suggest** or **Order** choice instead of a drop-down.
  Click the thoughts, or **Read all**, to read every recent thought in a larger
  panel beside the Profile, grouped by day; Memories opens beside it too.
  Back, or Escape, returns to the small card. The card no longer repeats what
  the agent wants to do, no longer shows an always-open rename box, and no
  longer leaves empty gaps. A deceased agent opens straight to their historical
  Profile.
- Buttons are consistent across the game. Every panel closes with the same
  small square button showing a pixel ×, and a screen reached from another
  (Main Menu Settings, New World, Load World, Save World and an agent's model
  settings) has a pixel back chevron in that same place instead of a Back
  button elsewhere. The Pause Menu's choices match the Main Menu's, the agent
  card's Find is a crosshair icon, dialogs close with the same pixel ×, and
  confirming a permanent deletion uses a red button. Ordinary buttons share
  one height.
- The hover readout at the bottom of the map names what is under the pointer
  without its map coordinates.
- Text now uses pixel fonts that match the art. Body text is Fusion Pixel,
  and titles, headings, section labels, dialog titles and the Main Menu's
  choices use Timber, thin capitals based on the logo's letters. Base text
  sizes use whole multiples of 12 px and are drawn without smoothing.
- UI Scale now enlarges the whole interface in whole steps (Automatic, 100%,
  200%, 300% and 400%), so pixel text stays sharp. Panels, padding, buttons,
  icons, the Quit Game and other confirmations, drop-down lists, tooltips and
  names on the map all grow together instead of text alone. Automatic is the
  new default: it picks 200% on 1080p and 1440p screens and 300% on 4K. A
  saved 125% becomes 100%, and 150% or 175% becomes 200%. Sizes that would not
  fit the window are greyed out, and on a narrow layout the top bar shows its
  icons without words.
- Weather on the map now moves. Rain falls as short drops that land with
  small splash rings, storms darken the sky with heavier slanted rain and a
  soft flash of lightning every several seconds, and snow drifts down. Rain,
  storms and snow no longer fill square blocks: their edges wander in
  irregular shapes that creep slowly and fade out softly. A very light haze of
  cloud drifts over the land now and then, more often under cloudy or rainy
  skies, without covering the view. All of it holds still while time is
  paused and carries on when you resume.
- Neighboring land surfaces now blend at close zoom instead of meeting in a
  hard tile-grid line. Grass reaches softly into sand, forest into grass, snow
  into rock and tundra, and mountains shed a rocky edge. The edge wanders and
  merges with a few bumps and half-transparent rim pixels, continues smoothly
  from tile to tile, and rounds corners, while staying in a narrow band so
  every tile still reads as a square of its own ground. Water keeps its
  shoreline edges, and the zoomed-out overview is unchanged.
- Coasts, lakes and rivers now have rounded, wandering shores instead of
  straight tile-edge strips. The land reaches a narrow way into the water, and
  the lighter shallows and the thin foam line on coasts and lakes follow that
  edge; rivers keep a quiet bank without foam and still read as one-tile
  channels.
- Where a river runs into the sea or a lake spills into a river, the lighter
  water now fans softly into the darker instead of changing color in a
  straight line.
- Open water no longer repeats the same small square: seas, lakes and rivers
  carry wave crests and the odd sparkle that run across tile edges and gather
  in some stretches, and shallow water shows faint light ripples across its
  bottom. Seas have the most swell, lakes are calmer and rivers ripple in
  short marks, all in their usual colors.
- The Town panel and Event Log are easier to scan. The Town panel puts
  shared stores, projects, the household council and social activity under
  headings, and says "No one is working on a project right now" instead of
  leaving an empty gap. The Event Log groups entries under each day with a
  dimmed time per row, and located events are highlighted as links. The
  Agents, Town, Event Log, World Info, Filters and World Map panels now have
  a close button, and the panels are titled Agents and Event Log to match the
  top bar.
- The top bar is now three small wooden panels floating over the map, with
  pixel icons on every button: Map and Filters; the pause control with the
  date, time, season and local weather; and the world actions. When time is
  stopped the pause button turns orange and reads Paused. The Agents button
  shows how many are alive and a small orange dot when someone is hungry, and
  the Event Log button shows a red count of events since you last opened it,
  with those rows dotted in the log.
  During first-Town setup, its extra controls sit in a second row so Start
  World and Menu stay on screen at 1280×720.
- The Town button is gone, since a world can hold more than one Town. World
  Info now opens on a Towns page listing every Town with its residents, when
  it was founded and a Show button that moves the map there, followed by the
  household stores, projects and council; the World page holds the rest. T
  now opens Towns.
- The Agents list now shows what each living agent is doing and flags anyone
  hungry, lists the deceased under their own heading, and fits its height to
  the people in it. Choosing someone moves the map to them, and the agent
  card's new Find button does the same.
- The menus match the new look. Main Menu, Pause Menu and New World buttons
  carry pixel icons, Quit to Menu sits apart at the bottom of the Pause Menu,
  and Game Settings groups its choices under Interface, Display, and Date and
  time, with Fullscreen as an on/off switch. New World now speaks of choosing
  your first Town's site.
- New World fits on one screen at 1280×720: the options sit in a captioned
  column beside a map preview about twice as large as before, with Back and
  Create World always in view. The preview keeps its place while it updates,
  and Preview again is there if an update fails. Load World shows each world's
  name, whether it is current and when it was saved, flags only worlds that
  can't open or haven't been checked, and opens a world on double-click.
- Zooming in now shows pixel-art ground instead of flat color squares: two
  calm 32×32 textures for each kind of grass, forest floor, sand, scrub, rock,
  soil, snow and water, small top-down mountains with snowy peaks, and
  shoreline foam or river banks where water meets land. The zoomed-out overview
  keeps its flat colors, and the old ≈ and ▲ map symbols are gone.
- Confirmation dialogs (Quit to Menu, Quit Game, Load Save and Overwrite) now
  match the game's dark panels instead of the default grey window, and their
  confirm button names the action, such as "Quit to Menu" or "Overwrite",
  instead of OK.
- Trees and natural sites are now pixel-art sprites instead of plain circles:
  leafy broadleaf and star-shaped conifer canopies, stumps with growth rings,
  saplings, fruiting and picked orchard trees, berry bushes, wild greens, fiber
  plants, reeds, stone boulders, rust-streaked iron, gold-flecked and
  crystal-studded outcrops, clay banks, seed heads, tilled soil, and distinct
  depleted and regrowing sites. Their map markers no longer draw a second
  symbol on top.
- The starting camp no longer shows plain ■ squares: its cooking fire draws as
  the stone-ringed hearth, the camp path as flagstones, and an old bedroll as
  a blanket. The camp's original food, wood, stone, fiber and seed stores draw
  as berry bushes, a log pile, boulders, fiber plants and seed heads instead of
  ●, ⬟ and ♧ symbols. Camp objects are named like buildings ("Campfire",
  "Shelter", "Path") instead of FIRE and HOME.
- Buildings are now drawn as top-down pixel-art roofs across their whole
  footprint instead of a text symbol: red-tiled houses with chimneys, slate
  warehouses with loading doors, thatched farmhouses, smithies with a glowing
  forge, hide tents for shelters, plank storehouses, workshops with a hammer
  sign, weaving frames, and stone-ringed hearths for campfires. Roads have a
  worn edge and a lighter center. Agents appear as small top-down people with a
  stable look per person and different sprites for infants, children, adults
  and elders; hovering or selecting one rings them and shows their name.

- The Quit Game icon shows its whole arrow; the tip was cut off at the edge.
- The unread count on the Events button is no longer half hidden under the
  button next to it.

- A damaged named-save entry no longer hides other saves or stops autosave
  rotation. Damaged files are preserved, with a safe diagnostic for recovery.

- The Main Menu's Quit Game confirmation now just says the game will close,
  instead of telling you your progress is saved after you have already left
  your world.

- Hovering the Town panel no longer pops up a technical tooltip of world
  ticks, revisions, map digests and internal system counts.
- Tooltips no longer fade from dark edges into a lighter middle; their
  parchment and border are drawn as crisp pixels like the rest of the interface.
- Panels opened from the top bar open under their own button. Filters used to
  appear on the right, and Agents and World Info on the left, far from the
  buttons that opened them.
- Scrolling the mouse wheel over a panel no longer zooms the map behind it,
  including when the panel has nothing left to scroll.
- Panels and confirmations fit what they hold instead of keeping a fixed size.
  Quit Game, Quit to Menu and the save and delete confirmations are no taller
  than their message and buttons, and long messages wrap at a readable width.
  Agents, World Info, the Event Log, Memories and the agent card no longer
  leave empty space below short text, and a panel with long text scrolls
  instead of running off the bottom of the screen.
- The selected-agent card no longer covers the agent it describes: when it
  cannot fit above or below them, it opens beside them. On short screens its
  profile scrolls inside the card, so Speak and Send are no longer cut off
  below the bottom edge. Rows in the Agents list are no longer clipped.
- A world where an agent ate orchard fruit now saves and reloads normally;
  that meal source was previously rejected by save validation.
- The Town panel no longer goes blank while the world is paused, and
  reselecting an agent no longer leaves their relationships, private thoughts
  or memories empty. The Event Log keeps your scroll position between updates
  and no longer repeats "Initial content activated" when a world is created.
- Action results and map-click instructions, such as choosing the Town site or
  moving a founder, stay on screen long enough to read instead of disappearing
  within a second; mode instructions remain until the mode ends. A request the
  world host refuses is reported as not accepted rather than as a lost
  connection. Messages from Main Menu Settings are no longer hidden behind the
  title backdrop.
- Opening a world now centers the camera on its first Town or living agents;
  a new, unsettled world starts over dry land instead of possibly over open sea.
- The selected-tile card no longer extends below the bottom of the screen or
  hides its last fact behind a scrollbar.
- Map markers too small for their name now show only their symbol, with full
  details in the tooltip, instead of clipped fragments such as "rehou".
- Top-bar actions such as Start World, Play and Undo last founder are dimmed
  and blocked while the Pause Menu or Settings are open. Choosing the Town
  site now changes its button to Cancel Town site until a site is chosen.
- The Connect/Pair screen keeps the title backdrop, uses the same compact back
  button as Main Menu Settings instead of a second large Back button, and
  scrolls the pairing code into view. Settings and autosave choices line up in
  one column at every UI Scale.

- Adults in a household that already has a House no longer plan a redundant
  new House. An in-progress second-House project ends with a visible reason
  before spending supplies; household invitation and new-household choices for
  unaffiliated adults remain open.
- New World map preview no longer fails with HTTP 409 when the previously
  selected world is running. Preview leaves that world untouched; creating or
  selecting a different world still pauses it first.
- Regional storms now end within three-quarters of a game day, becoming rain
  for the rest of that day. The world, inspected weather and resumed saves use
  the same transition, so severe exposure cannot persist through a full day.
- Agents caught away from buildings can now seek nearby forest or a standing
  tree during a storm. Natural cover reduces exposure while they occupy it.

- Adding an adult on disconnected land no longer breaks world reconnects while
  the client previews a path to food. An accepted pause also lets Quit to Menu
  work when the next world refresh fails. The host can again load pre-layer
  generated worlds whose original resource layout and settlement package
  predate the current generator.
- Render Resolution now defaults to the current window size or fullscreen
  display size, so a 2560×1440 screen renders sharply instead of enlarging a
  720p/1080p viewport. Fixed resolution choices remain available and include
  the detected display size; older explicit non-default choices are preserved.
- Hosts now report accepted Jev memory-index updates with the owner and bounded
  record counts, without logging the underlying memory or belief text.
- Main Menu Settings now accepts clicks while keeping the title backdrop visible;
  Back returns to the Main Menu instead of leaving the game apparently frozen.
- New agents' personal models are now asked to choose a full name with a family
  name; a middle name is optional.
- Turning Jev off keeps an agent's saved social memories and private beliefs useful in personal-model decisions. Each agent receives only a small, relevant set of its own records, with belief provenance and confidence preserved; provider failures do not add or share memories.
- Generated Small/Medium maps now keep climate, elevation, water, surface and
  vegetation cover distinct through saves, previews and owner observations.
  Ground drawing and movement use those facts separately; individual trees
  remain objects. Older generated saves recover the missing layers without
  changing their terrain manifest or resource layout.
- Agents can cross narrow river tiles on foot only between opposite dry banks.
  They no longer walk along a channel or cut diagonally through its water;
  wider rivers remain impassable without later transport or bridges.
- Children born in a world no longer inherit a billable world cognition default
  when their personal model has not been selected. Infants cannot queue personal
  decisions; after infancy they make safe local decisions until an explicit
  personal model is assigned in the agent card. Removing that assignment
  returns them to local decisions rather than another paid account.
- Children now have age-appropriate choices to talk, play, learn by observing
  an adult, and carry spare food home. These encounters persist as social
  memories and trust. The world rejects adult-only work, trade, partnerships,
  parenthood and council actions for children even if a model or owner request
  tries to select one; infants still make no personal-model decisions.
- Added selected-tile inspection. Clicking ground
  highlights the tile and shows terrain kind, separate generated climate,
  elevation, surface, hydrology and vegetation cover, objects, resources,
  buildings, regional weather and moisture. Fertility remains explicitly
  unavailable. The owner accepted this direction; future field and layout
  refinements can follow the underlying world systems.
- Signed owner reconnects now use a versioned terrain-cache claim. After an
  initial generated-map transfer, unchanged terrain bytes are omitted from
  routine observations; clients reuse only a matching world, terrain manifest
  and map-layer digest, and fetch fresh map data after a cache miss or change.
  Ordinary world events and dynamic objects remain fresh.
- Private-world saves now store generated terrain in versioned 64×64 byte
  chunks with per-chunk integrity hashes rather than one JSON object per tile.
  Existing v1 saves load and migrate atomically to v2/schema 19; damaged or
  incomplete chunks fail closed without replacing the checkpoint.
- Regional rain, snow and storm conditions now draw bounded top-down cloud and
  precipitation marks over camera-visible terrain, including wrapped world
  seams. Clear regions stay unobscured; the effects are presentation only.
- Agents no longer have an energy meter, passive energy drain, sleep action,
  bedroll recovery, or energy gates on work, social life and exploration. Food
  and severe-weather effects remain. New worlds omit bedrolls and bedding;
  older private-world saves remain loadable with hidden legacy bedroll markers
  and migrate to the current save schema when loaded.
- Illness now slows work and travel by severity without suppressing ordinary
  social choices. Food, shelter and caregiver actions support bounded recovery;
  Bandage, Medicine and Clinic have catalog-only identifiers without guessed
  recipes or treatment effects. No sleep/energy loop was added.
- New World now refreshes the exact seed/settings preview after edits without
  requiring another button press. Stale previews cannot enable Create World;
  preview failures leave the active world unchanged.
- Out-of-view event pop-ups and their Game Settings switches are gone. Births,
  deaths and other important events remain in the Event Log, where located
  entries can still move the camera to the event.
- The Pause Menu now shows Save World, Settings, Mod Library, and Quit to Menu
  in a compact vertical order, with Quit to Menu last. Game and World are
  categories inside Settings; Quit Game remains on the Main Menu. The old
  player-facing Create building workbench has been removed, while existing
  agent proposals remain recorded and visible in the read-only world package
  list.
- Wrapped worlds now pan continuously across the east/west map seam. The
  overview marks seam-spanning camera areas, and placement and hover use the
  matching map tile; non-wrapped worlds keep their bounded camera edges.
- The world view can zoom farther out to show more of a large map at once.
  The 8-pixel terrain-tile minimum is a provisional playtest setting, not a
  settled final readability/performance cap.
- Load World now checks archived worlds before selection and labels them compatible,
  incompatible or unknown. Proven-unloadable saves are preserved and cannot be
  selected; an older but still loadable save is not blocked merely for its version.
- Save World now separates Create New Save from a confirmed overwrite of one
  selected named checkpoint. Matching names never replace a save by themselves,
  and overwriting keeps the previous checkpoint as a recovery save.
- Main Menu Settings keeps the title-screen background, and selecting the
  already-active Settings page no longer closes it. Game Settings now separates
  physical Window Size from Render Resolution; both choices are remembered
  locally, with viewport scaling applied to the game and UI.
- Hovering bare ground now outlines its square tile, while an agent under the
  pointer keeps hover and selection priority. Agent markers sharing a tile
  divide that tile into bounded click targets at supported zoom levels, so
  adjacent-tile clicks no longer select them.
- Owners can now delete an unused named provider API key from the host through the
  cognition settings. A key assigned in the active world must be switched first;
  older saved assignments to a deleted key return to deterministic cognition
  instead of silently using another account's key.
- Main Menu Settings now hides and blocks World Settings until a world is
  loaded, so opening settings cannot jump into the world. Removed the dark
  gaps between square terrain tiles that appeared as black grid lines.
- Agent markers now stay under the cursor across world refreshes, so their
  hover tooltips are no longer cut off every tick. Warmth, illness, diet and
  equipment status stay in a fixed area of the selected agent card.
- Settings, model, key and paid-call tooltips and messages now use short,
  plain wording, and several developer-style messages read as ordinary
  sentences. The agent list shows Fullness instead of Hunger, because 100%
  means well fed and the old label read backwards.

- World Settings now shows installation-lifetime paid-model attempt and known
  token totals by provider/model. An optional call-attempt cap pauses the world
  at the limit; the owner must explicitly allow more paid calls or turn the cap
  off before resuming. Retries and abandoned calls count as attempts.
- Agents now route and move diagonally when both orthogonal corner tiles are
  clear. Diagonal steps have a deterministic longer cost, work across wrapped
  east/west seams, and cannot squeeze past blocked or occupied corners.
- Stable adult agents can now choose short curiosity outings. They scout only
  adjacent passable ground, remember tiles they actually visit, and return to
  their starting point. Hunger and urgent exposure take precedence;
  a cooldown prevents continuous wandering.
- Generated forests and scattered meadow sites now have individual broadleaf
  and conifer trees, with no more than one tree per tile. Cutting a tree for
  project wood leaves a visible stump that regrows in season; agents can carry
  household seeds to a depleted site to replant a sapling. Tree stages persist
  through saved worlds, and the top-down client draws only camera-visible trees.
  Existing generated saves retain their original vegetation layout and remain loadable.
- Suitable newly generated meadows now also contain individual generic orchard
  fruit trees. Agents can pick a distinct fruit item and eat or share it; the
  tree visibly changes from fruiting to picked to growing and regrows its fruit.
  Species, yield and seasonality are provisional, and older generated saves
  keep their original tree layouts.
- Agents can walk through one-tile-wide river crossings and mountain tiles at
  half the dry-ground travel speed. Wider river sections, peaks, lakes and
  oceans remain impassable, and river/mountain tiles remain non-buildable.
- Generated worlds with east/west wrapping now route agents and evaluate
  interaction range across the seam; non-wrapped worlds retain bounded paths.
  Old generated checkpoints inherit their saved wrapping choice on reload.

- Generated Small/Medium worlds now place sparse food, fiber, seed, stone and
  regrowing wood sites beyond the starter camp according to local ground.
  New World offers Sparse, Normal and Abundant resource settings that change
  actual site placement and the signed preview count, not just a label.
  The preview reports the number of sites, and the same sites persist through
  world creation and reload. Agents can seek reachable food beyond the starter
  berry patch, while inaccessible islands do not count as immediately
  gatherable; some remote sites will require later boats.

- New World now offers balanced, uniform, or dominant climate generation,
  a selected climate family for uniform/dominant worlds, and optional
  equator-to-pole cooling. The preview and created world use the same signed
  options. Generated ground distinguishes provisional sand, forest, and snow
  appearances; regional rain/snow likelihood and resulting soil moisture use
  the saved local climate. Clear-day cold exposure also respects that climate,
  so tropical winters do not behave like temperate winters. Mountain and peak
  tiles remain unbuildable.

- Recent local rain now raises a bounded soil-moisture estimate, while dry days
  lower it. Moist soil modestly improves food-crop harvests and very dry soil
  reduces them; snow and storm crop penalties still take priority. World Info
  shows the moisture estimate near the camera, and affected harvests explain
  the change in the event log.

- Prototype saved regional weather episodes with bounded storms and recovery intervals; rain weights remain provisional pending owner playtesting.
- Generated worlds now have 32×32-tile weather regions instead of one weather
  condition across the whole map. Agents' warmth, clothing/fire choices and
  travel fatigue use weather where they stand or travel; crops use weather at
  their field. The top bar and World Info show weather at the camera location.
  The small development fixture keeps its original single local condition.

- New World now shows a signed, read-only terrain preview before creation.
  Changing the seed, size, water share, climate, resource abundance or wrapping
  marks it stale; rerolling
  the seed can regenerate it, and Create World only enables for the previewed
  options. The camp location is marked on the atlas. The preview and created
  world use the same deterministic map and do not alter the current world.

- Main Menu **New World** now creates a separately saved Small or Medium map
  from a chosen seed, water share and east/west-wrap choice, then opens its
  empty camp paused for four-founder setup. **Load World** selects an archived
  world and restores its per-agent model/key-slot assignments and autosave
  choices without copying API keys or re-pairing. The current world is saved
  before switching; named checkpoints remain in the pause menu for the selected
  world. Larger playable presets are still pending.

- Generated Small/Medium worlds now have a compact row-major terrain payload
  for owner observations instead of tens of thousands of JSON tile objects.
  The Godot client can draw that payload, inspect the minimap and place founders
  through the same single-view camera. The current live world still uses its
  existing small-map wire format.

- The deterministic geography generator can now form a valid empty base camp
  on Small and Medium maps and carry its seed, wrapping and water options
  through the private-world save. A four-founder generated world can advance
  and reload in the simulation. River/lake/ocean and mountain/peak ground are
  represented in the current physical map; mountain and peak are not buildable.
  The later New World menu uses this bridge; layered ecology is not finished.
  Larger presets remain in the compact geography generator only.
  Terrain lookups are indexed, and proposed ticks reuse their committed map
  instead of regenerating the whole geography each tick.

- The world view now draws only camera-visible terrain instead of creating a
  Godot button for every tile. The top-left overview uses a compact atlas of
  the same terrain data; it remains the existing single zoomable view, not a
  separate regional art set. A regional-size UI smoke map exercises this path.
  New World uses the compact map observation protocol added below.

- World Settings now controls per-world autosaves: on by default every five
  minutes with five rotating copies, with the agreed interval and rotation
  choices. Rotating snapshots appear in Load Save and can be restored like
  manual checkpoints; turning autosave off does not disable the host's separate
  per-change emergency recovery checkpoint.

- Pause Menu → **Save World** now creates an unlimited named checkpoint of the
  paused world. Pause Menu → **Load Save** lists those checkpoints, confirms a
  rewind, preserves the current state as a new checkpoint first, and opens the
  loaded world paused. Saves survive restart and retain per-agent model/key-slot
  choices without copying API-key secrets into world files.

- Freshly placed founders and later added adults can choose their own names in
  an accepted ordinary personal-model decision, without a separate naming
  request. If the player renames one first, a delayed model answer cannot
  overwrite that choice. Names persist with the world.

- The selected-agent card now lets the paired player rename an agent. The
  chosen name updates the visible world and family tree and survives reload;
  biological identity and relationships do not change.

- After the four-founder start, the top bar now offers **Add Agent**. Choose a
  provider, model, and saved or new API key, then click an empty passable tile.
  The adult joins the running world in a separate one-person household and
  survives save/reload. Placement requires the paired owner and rejects
  occupied or impassable tiles; the current map has no property claims yet.

- The Windows client now starts at a Main Menu with Continue, Settings,
  connection/pairing and Quit Game. Quit to Menu pauses the host world; Continue
  returns to it. New World and Load World were enabled in a subsequent batch;
  Load Save remains for checkpoints of the selected world.

- Building placement now has an explicit buildable-ground rule. The existing
  mountain tiles reject construction, including owner-authored placement;
  future peak terrain is also reserved as no-build ground.

- A fresh private world now opens with an empty two-household base camp. The
  paired player picks a provider, model and saved or new API key for each of
  four unrelated founders, then places them on empty map tiles. Progress is
  saved after every placement; time cannot resume until the player explicitly
  chooses **Start World**. Existing development worlds keep their state.

- In World Settings, an inhabitant can now use one personal provider/model for
  both daily and project decisions. Players can save multiple named API keys
  for the same provider and choose which one an agent uses. Keys remain in
  private host storage, never in the world save or owner status. Existing
  provider settings migrate without dropping their saved credentials. The
  selected agent's card also opens those model/key controls directly.

- Fresh private worlds now start paused with six-minute days, a 40-day year and four
  10-day seasons. Agents born in those worlds become children at day 3, adults
  at day 15, elders at day 45 and cannot survive past day 60 from birth.
  Agent profiles show age in days. The prior development save was archived,
  and a fresh paused world was created to playtest this pace.

- World Settings now has a saved Jev assistance switch. Disabling it while
  paused keeps memories and credentials, invalidates older decisions, and
  routes work Jev would have handled to the agent's personal planning model,
  the world planner, or a local safe fallback. Jev can be re-enabled later;
  changing the setting writes a newer save format so older hosts cannot
  silently discard the choice.

- The top bar, World Info, agent histories and event log now format dates and
  time from the saved world's calendar pace. Game Settings offers DD-MM-YYYY,
  MM-DD-YYYY and YYYY-MM-DD display without changing world time. The old aging
  multiplier has moved out of World Settings into clearly labelled prototype
  developer tools; it is not the decided custom calendar.

- Agent profiles now open a separate Memories panel showing that agent's
  saved records, including private memories and memories retained after death.
  These records no longer masquerade as public social notes. Quit Game now
  asks for confirmation before closing the client.

- Agent profiles now show a small scrollable history of recent private thoughts
  written alongside accepted personal-model decisions. These thoughts survive
  saves and remain inspectable after death; routine fallback and hidden model
  reasoning are not presented as thoughts.
- Important out-of-view births, deaths and building proposals can raise brief
  notices that jump to their location or open the event log. Game Settings
  remembers notice choices by category without hiding full log entries.

- Game Settings now remembers a 24-hour or AM/PM clock preference on this
  installation. The top bar, World Info, settlement view, and event log use
  the same display choice without changing world time.

- An interactive Family Tree opens from an agent's card. It draws accepted
  parent–child and partnership links, keeps unrelated household members
  separate, and opens living or deceased relatives' profiles when clicked.

- Slow hosted agent-model calls no longer hold the entire world tick in the
  private-host build. Other agents and world systems advance while a request
  waits; pause/disconnect discard the external call, and saved unresolved
  decisions can be retried safely after reload. An agent card marks a queued
  decision so the wait is visible.
- The Events panel can jump to the recorded map location of an
  actor-associated event; global events remain informational.

- Deceased inhabitants now leave a saved, read-only record with their last
  location, age, role, relationships and death details. They remain inspectable
  from the inhabitant list after reload without appearing as living map actors.

- The Windows world view now supports mouse-wheel zoom, WASD/arrow and
  middle-drag panning, plus a top-left Map button. Its data-drawn overview
  marks the visible area and lets players click or drag to move the camera;
  this adds no second regional texture set. The top bar shows the living-agent
  count and opens a concise World Info panel; the pause menu separates current
  Game Settings from World Settings.

- Experienced builders can now propose bounded 1×1 shelter, storehouse or
  hearth designs through planning cognition. Completed building practice gates
  the choice; proposals are rate-limited, retain durable inhabitant authorship
  and enter Menu → Create as **proposed only**, labelled with their proposer.
  They never validate, approve,
  stage, activate or consume live materials without the owner's existing review
  steps.

- Added bounded directed trust earned from completed material help, mutually
  accepted barter and finished teaching. Trust survives restart, is visible on
  inhabitant cards, prioritizes familiar barter partners and remains required
  before proposing partnership. Refusal, withdrawal and disagreement do not
  reduce trust. Save schema 12 persists scores while projecting older
  cooperation memories without rewriting paused saves.

- Added bounded building, farming and crafting practice. Inhabitants earn one
  point only when useful work completes successfully; every ten points speeds
  the hands-on preparation stage without bypassing roles, materials or crop
  growth. Practice persists in save schema 11 and appears on inhabitant cards.

- Added Menu → Create: a data-only building workbench for named shelters,
  storehouses and fuelled hearths. Isolated construction previews consume no
  live materials or paid cognition. Saved designs require separate validation,
  approval and staging; activation waits while paused. Used designs cannot be
  withdrawn destructively. Inhabitants can select and construct active designs.
- Constructed buildings now appear on the map with readable names, footprints
  and hover help instead of existing only in the server's building list.

- Package withdrawal now rejects committed building, production-history and
  settlement-project references until an explicit migration is available.
  A rejected request preserves the world; withdrawing unused content no longer
  cancels another package's work. Rollback outcome logs omit private reason text.

- Resource markers show remaining stock and capacity, with working hover help
  for finite deposits and seasonal regrowth. Markers persist across refreshes
  so updates no longer discard the hovered control.
- Unfinished production and crop jobs are cancelled when their worker dies,
  releasing remaining reserved inputs before completion can produce output.
  Finished work remains intact; operators receive a safe cancellation event.

- Added managed coppice: farmers reserve seeds and a fertile plot for a full
  world day, then harvest timber and replacement seeds. Exhausted wild wood is
  not refilled; food crops and forestry compete for the same growing space.
- Fixed crowded rest and food access: inhabitants choose reachable shelters,
  fall back to bedding or slower outdoor rest, and do not keep choosing blocked
  shared food over reachable alternatives. Outdoor rest retains exposure risk.
- Construction retains the worker's current legal site instead of chasing newly
  vacated earlier tiles. Busy mentors can respond to teaching requests before
  finishing their existing project.
- Descendant activity/condition logs preserve complete colon-bearing inhabitant
  IDs instead of truncating them or silently losing condition events.
- Descendants can now finish buildings: generated instance IDs are canonical
  stable hashes when society IDs contain separators. Existing founder-building
  IDs remain unchanged, so already-built structures are still recognized.
- Preserved sibling and direct-ancestor partnership exclusions after relatives
  die, including grandparents. Ordinary relationship commands can no longer
  revoke historical parentage or accept a fabricated parentage proposal.
- Connected replacement caregiving after a dependent loses all active carers.
  Adults volunteer; infants receive protected care without fabricated consent,
  while older dependents independently accept/refuse. Offers expire, either
  participant may withdraw, and replacement carers use the real feeding loop.
  The client shows missing care and readable caregiving decisions.
- Fixed household caregiver tracking when one of several care obligations ends;
  remaining dependents no longer lose the adult from the household projection.
- Added pause-only, signed **Life pace** settings: original calendar aging or
  opt-in generational aging. Changes preserve current ages, birth dates and the
  365-day world calendar; they affect future biological aging, not tick or model
  cadence. Faster modes bring adulthood, elderhood and mortality sooner.
- Persist biological clock anchors and newborn life dates in schema 10, show
  biological ages on inhabitant cards, and keep adult work/social choices
  unavailable to minors. Old saves remain unchanged until the owner opts in.
- Connected separate parenthood proposals and consent to delayed, atomic births
  when food, shelter and caregivers remain available. Withdrawal, separation or
  loss of a parent cancels preparation; partnership alone never creates a child.
- Added dependent infants with physical needs and actual caregiver food/warmth
  delivery. Infants make no hosted-model calls or adult work decisions; the UI
  shows age bands and family-plan status. Schema 9 preserves preparation across
  restart without changing an older paused save.
- Connected adult partnership proposals to independent acceptance/refusal and
  unilateral withdrawal. Prior cooperation opens a choice, not automatic
  consent; unanswered proposals expire and rejected pairs have a cooldown.
  Partnerships are visible in the relationship panel and do not create children.
- Added persistent practical apprenticeships: eligible adults request a builder
  or farmer role, a qualified mentor independently accepts/refuses, and joint
  lessons at camp earn the role. Hunger, exhaustion, exposure, cancellation and
  mentor death cannot silently grant completion.
- Show work roles and lesson progress on inhabitant cards, preserve training
  across pause/restart in schema 8, and record bounded lesson-stage telemetry.
- Added a persistent household council: demonstrated contributors become
  stewards, but food-allocation changes require independent majority votes.
  A scarce-food reserve protects hungry members' access; rejection retains the
  previous rule. Policies survive leadership succession and restart.
- Show the steward, active food rule and vote counts in the Settlement panel.
  Schema 7 stores council state and ballots; older paused saves remain unchanged.
- Connected bounded inhabitant barter: surplus-for-needed-item offers, separate
  planning-provider acceptance/refusal, expiry and unusable-item cancellation,
  with no transfer until both parties agree. Completed exchanges leave public
  memories; pending offers and social notes appear in the settlement UI.
- Fixed idle reuse hiding choices created earlier in the same tick by another
  inhabitant. Cached decision context now reflects the provider's observation,
  not the later world state at execution time.
- Kept clothing, fire tending and warmth-seeking decisions on the assigned
  routine provider rather than accidentally routing them to the planning model.
- Connected weather exposure to warmth and recoverable illness, with clothing
  insulation, shelter, fuelled hearths, better rest from bedding and faster
  project work with carried tools. Inhabitants collect equipment and seek heat.
- Added perishable-food decay, slower household decay with a storehouse, and
  fresh-only consumption/reservation selection. Production now observes stock
  targets instead of endlessly manufacturing equipment.
- Weather now changes crop food yields and travel fatigue. Inhabitants prefer
  varied food sources; diet quality survives transfers and affects fatigue.
- Cancelled production safely when reserved ingredients become unusable,
  releasing remaining inputs instead of repeatedly failing the world tick.
- Fixed builders repeatedly selecting occupied or unreachable sites. Urgent
  exposure still permits protective construction and clothing work, while
  interrupting unrelated projects; exposure changes invalidate idle reuse.
- Added visible warmth/illness/equipment state and bounded survival-transition
  logs. Save schema 6 retains survival conditions and fire fuel deadlines;
  older paused saves are unchanged and receive no retroactive spoilage.

- Added persistent settlement projects with material acquisition, travel, work,
  interruption/restart recovery and explicit blockers. Other inhabitants can
  fulfil material requests; successful help leaves inspectable public memories.
- Added a versioned settlement supplement with stone, fiber and seed sources,
  hearth/weaving buildings, bedding, clothing, meals and grain production.
  Existing worlds receive additive resources only after resuming; schema 5
  preserves projects and validates additions against the original seeded map.
- Show shared stores, project progress and cooperation memories in the normal
  world/inhabitant UI, with technical identifiers relegated to diagnostics.
- Added a Settlement toolbar panel and secret-safe `settlement_activity` logs
  for project transitions and fulfilled requests, without logging content text.
- Kept authoring revisions monotonic after event-history compaction.

- Bounded hot event histories with durable, hash-verified archive segments and
  explicit stale-cursor snapshot resets. Save schema 4 keeps global event IDs;
  backups must include the save's adjacent `.history` directory.
- Hardened ticks against cancelled or slow providers: observations and pause
  stay responsive, and cancelled/superseded ticks leave no partial world state.
- Cancelled in-flight provider work when the last client lease expires or the
  owner pauses, and rechecked client presence before committing a proposed tick.
- Capped hosted-provider response bodies at 256 KiB before JSON parsing.
- Corrected reservation/barter expiry, automatic release on society clock
  advancement, asset-charge conflicts/overflow, and prefix-ID ledger restore.
- Made dependency quarantine block subsequent content activation and enforce
  dependency-first activation order. Active dependents must be rolled back
  before their dependency, preventing an unrecoverable content graph.
- Included crop work in owner job projections and kept empty-population worlds
  observable after all inhabitants die.
- Eliminated idle authority-file rewrites: one-use challenges are process-local
  and fail closed across restart; paired identities and revocations stay durable.

- Added per-inhabitant routine/planning provider and model overrides, with
  explicit inheritance from world defaults and shared host-only credentials.
- Added automatic, versioned starter content activation on the first resumed
  client-present tick: shelter, storage, cooking fire, workshop, crops, meals
  and tools. Existing paused worlds remain unchanged until resumed.
- Connected household food pickup to movement, inventory ownership and eating.
- Added per-inhabitant accepted decision, fallback, model, token and latency
  telemetry to the selection card without showing prompts or keys.
- Reused unchanged idle decisions across ticks and reloads, with a bounded
  reevaluation deadline and immediate reconsideration when legal choices change.
- Compacted cognition settings and centered the game menu independently of
  inhabitant selection, with engine-level layout regression checks.
- Limited the product roadmap to single-player; multiplayer is out of scope.

- Added structured, secret-safe live observability for cognition provider
  calls, authoritative intention outcomes, usage, latency, and world lifecycle
  gates so private-world behavior can be diagnosed from host logs.
- Added a deterministic .NET 10 headless simulation with an integer world
  clock, atomic ticks, durable pause/resume epochs, bounded recovery, and a
  pinned PCG32 random stream.
- Added seeded world generation with canonical terrain and resource manifests,
  stable cardinal routing, deterministic multi-inhabitant movement, contention
  resolution, legal direct swaps, and route-cache invalidation.
- Added survival simulation for hunger, energy, harvesting, eating, sleeping,
  renewable resources, and resource regeneration.
- Added canonical snapshots and event logs, schema migrations, save/reload,
  physical replay, compatibility checks, and digest proofs for deterministic
  world recovery.
- Added lot-based inventories with quantity, freshness, spoilage, reservations,
  ownership, capacity checks, atomic transfers, and exact barter settlement.
- Added durable world commands and messages with idempotency, stale-result
  rejection, and crash-safe processing boundaries.
- Added a private hosted world runtime with reconnectable observation, a
  browser diagnostic viewer, and a Godot client for normal play.
- Added owner-device pairing, signed owner requests, anti-replay protection,
  device management, and owner-only controls for pausing, resuming, and giving
  inhabitants instructions.
- Added deterministic cognition with bounded legal choices, validated
  responses, retries, fallbacks, usage accounting, provider-outage pausing,
  and restart-safe pending decisions. Supported adapters include the local
  deterministic provider, Jev, OpenAI-compatible providers, and Ollama Cloud.
- Added player-managed hybrid cognition settings. A paired owner can assign
  Deterministic or Jev to routine survival decisions, assign Deterministic,
  OpenAI, or Ollama Cloud to planning and work, and save, replace, or forget
  each provider's API key directly in the game.
- Added independently scheduled inhabitants with persistent identities, roles,
  needs, skills, intentions, inventories, and per-inhabitant cognition
  configuration.
- Added typed, consent-aware relationships, households, caregivers, social
  interactions, and atomic person-to-person trade.
- Added family and mortality simulation including birth, aging, death,
  tombstones, estates, inheritance, and household continuity.
- Added a four-inhabitant private world that saves and restores movement,
  needs, inventories, relationships, cognition, production, and world events.
- Added ecology and weather state, factions and laws, currencies, cultural
  state, and deterministic chunk manifests to the private-world simulation.
- Added governed data-only content packages with canonical IDs, semantic
  versions, dependency locks, validation, owner approval, staging, activation,
  rollback, and quarantine.
- Added typed content definitions for materials, buildings, recipes, and
  production, with deterministic preview and validation before activation.
- Added deterministic building placement, production jobs, ingredient and
  asset reservations, workstation checks, and inventory completion.
- Added asset governance with format and quota validation, rights and source
  metadata, provenance manifests, canonical package digests, deterministic
  cache accounting, previews, and portable artifact envelopes.
- Added public inhabitant intention and relationship summaries without
  exposing private model reasoning.
- Added inhabitant-chosen `build` actions. Inhabitants can independently choose
  valid structures or recipes; every generated world supplies reachable fertile
  land, and crop builds such as carrots complete into household inventory.
- Added a portable Windows 11 x64 game build and an automated verification
  pipeline that tests the simulation, starts the Godot client, exports Windows,
  and uploads the resulting artifact.

- Main Menu, New World, save and load, founder setup, filter and Mod Library
  text now uses short, plain sentences. Save lists and messages show the
  in-world date and time instead of tick numbers, and error and confirmation
  messages say what happened and that your save is safe.

- Completed the pre-release ClankerWorld rename across save/content identifiers,
  package and signature domains, Windows user storage/device-key names, and the
  systemd template and installation paths. Previous development saves and
  pairings are not compatible; the old installation is retained as a rollback
  backup, while provider credentials are carried into the fresh installation.

- Renamed the game, .NET projects, Godot client and Windows export to
  **ClankerWorld**.
- Agent models are now told that the hunger number they receive is how well fed
  the agent is (10000 is full, 0 is starving). It was not explained before, so
  a model could read a low number as "not very hungry" and neglect eating.
- Changed private-world lifetime so simulation ticks and hosted-provider calls
  run only while at least one authenticated game client remains connected.
  Closing or losing the last client stops the world after a five-second grace
  period; reconnecting does not clear a manual pause or simulate offline time.
- Connection and server errors now read as plain sentences ("cannot reach the
  world server", "the server cannot handle another connection request") instead of raw HTTP status text.
- Changed inhabitant cognition from one provider request per person per world
  second to bounded, persistent intentions. Inhabitants now carry out legal
  movement, rest, gathering, eating, and building work locally until the plan
  completes, becomes invalid, or reaches a scheduled reevaluation; hosted Jev
  decisions for different inhabitants are dispatched concurrently.
- Animated inhabitant movement between observed tiles and added compact
  activity markers and intention summaries, making travel and current work
  visible without exposing private reasoning.
- Changed hosted-provider configuration to a durable private-world setting.
  Provider-role and model changes take effect at the next cognition boundary
  while stale responses from an older configuration epoch are rejected.
- Changed urgent survival candidate generation to withhold strategic building
  work until hunger and exhaustion are out of the critical range.
- Replaced the inspector-style Godot shell with a world-first, responsive 16:9
  play surface. The world now fills the screen beneath a compact clock and
  weather HUD instead of sharing space with permanent developer panels.
- Moved the inhabitants roster and recent events into temporary popovers, and
  moved settings, display controls, and developer tools into the pause menu.
- Changed inhabitant inspection to a closeable card anchored near the selected
  person. No empty selection panel is shown before a person is selected.
- Replaced raw ticks with a player-readable `Day N · HH:MM` clock and filtered
  routine movement, cognition, and tick noise out of the recent-events view so
  it can focus on meaningful world events.
- Removed protocol versions, fixture IDs, provider state, authoring drafts, and
  other implementation language from ordinary play; diagnostics remain
  available under Developer tools.
- Made the integrated private world the intended playable runtime and isolated
  its save and pairing authority from the preserved one-person fixture used by
  diagnostics.
- Expanded the map from a fixture grid into layered terrain, resources,
  buildings, and selectable inhabitants with contextual inspection.
- World Info, tile details, the Event Log and the memories panel now use plain
  wording: the year length, map size and soil moisture read naturally, event
  lines no longer mention internal steps, and agent plans read like "look after
  someone who is ill" or "offer a trade". Resource tips say "Grows back" or
  "Does not grow back".

- An unavailable or low-confidence model now leaves its agent on the explicit
  safe-idle fallback instead of silently choosing the highest-priority legal
  action. Pending instructions are retained rather than marked completed by
  that fallback; repeated failure no longer pauses the legacy fixture world.

- Fixed the four-inhabitant world deadlocking around a single berry tile.
  Inhabitants now route around occupied tiles, interact with resources from an
  adjacent tile, prioritize critical sleep, and suppress repeated blocked-path
  noise while they yield or retry.
- Fixed survival gathering so renewable ecology produces carried food rather
  than trying to transfer a depleted household fixture lot. Regenerating
  resources are no longer treated as harvestable, and inhabitants stop
  stripping the patch while they still carry food.
- Fixed re-pairing after a world-authority change by exposing a `Pair again`
  action in Settings; players no longer need to find and delete client files to
  replace an obsolete saved registration.
- Fixed restart and replay edge cases across inventories, reservations,
  owner-control idempotency, cognition scheduling, stale provider responses,
  and durable command recovery.
- Fixed client selection and map-layer interactions so clearing a selection,
  selecting an inhabitant from either the map or roster, and reconnecting all
  produce the same observation state.
- Fixed the game viewport so it scales to the display without making the whole
  play screen scrollable or reserving a permanent right-hand sidebar.

- Stored paired-owner signing keys as non-exportable Windows CNG keys and
  required explicit pairing approval before owner capabilities are granted.
- Stored provider credentials in a separate service-account-only file with
  atomic replacement and `0600` Unix permissions. Keys are accepted only over
  signed paired-owner requests, are represented by a digest in canonical
  request bindings, and are never returned to the client or written to world
  saves, replay digests, observations, telemetry, or the Windows client.
- Kept content packages data-only and fail-closed: executable mods, invalid
  dependencies, unapproved assets, and over-quota packages cannot activate.
