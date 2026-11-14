---
title: Councils and elections
type: game-design
status: active
updated: 2026-11-14
---

# Councils and elections

Town governance, council proposals and elections.

## Town laws and governance

### Agreed

- Agents may establish Town laws and later change them. These are
  **in-world social rules**, not unbreakable physics. An agent can violate a
  rule—for example, cut a tree in a protected grove—and the world can record
  the act for discovery, dispute, and consequences. The game should not simply
  refuse every illegal action, because crime and enforcement belong to the
  simulation.
- The game rules still protect fixed world facts and action
  rules: agents cannot create goods, erase physical constraints, or silently
  rewrite formal ownership by declaring a new law. Unlawful use or occupation
  can be represented as an action/dispute without automatically changing the
  underlying ownership record. Laws may guide or contest transfers through
  validated mechanisms, but cannot bypass those mechanisms.
- A newly founded Town starts with a **simple council** rather than a
  single starting agent automatically ruling everyone. **Every adult resident** sits
  on this initial council and can propose laws and vote. Towns may later
  change their governing arrangement through in-world decisions; the council
  is the starting form, not a universal permanent government.

### Election threshold to try

Once a Town has **four adult residents**, the same as the starting
population, it begins electing a smaller representative council instead of
keeping every adult as a council member. **Agreed by the owner in chat on
October 11, 2026**, lowered from eight so that a new Town holds its first
election on day 1. Count adults in that Town, not across the world. Four is
an initial threshold to playtest, not a claim that every Town must forever
follow this exact rule. All adult residents should retain a vote in those
elections.

### Council proposals and election ballots

**Agreed with the owner on October 1, answers 24A–28A:**

- **Any adult Town resident may bring a proposal to the council**, including
  after representatives are elected. An ordinary newcomer may submit their
  own admission request but cannot vote on it. Proposing is not approval or
  authority to change membership, ownership or fixed world facts.
- **An ordinary law or admission proposal needs yes votes from more than
  half the council.** Silence and abstention supply no approval. A four-person
  council needs three yes votes; a three-person council needs two. This is a
  strict majority of the council, not merely more yes than no among replies.
  Changing the governing arrangement uses the separate
  [resident approval rule](laws-and-enforcement.md#town-law-scope-and-changes). Land adjudication and
  inheritance procedures are separate choices; an ordinary council vote does
  not settle them automatically.
- **The initial proposal window is one unpaused world day.** Resolve sooner
  when the result is settled; otherwise close at the deadline. Without sufficient
  approval then, the proposal does not pass. Paused time does not consume the
  window. Agents vote through their ordinary personal-model turns; make no extra
  paid calls merely to poll for votes. Proposal delivery follows the agreed
  [notice rules](#election-eligibility-ballots-and-notices). Changes to the
  council roster follow the agreed
  [proposal and scheduling rules](#council-changes-and-election-scheduling).
- **The first elected council has three seats.** Use the agreed four-adult
  trial threshold to begin representation. A Town may later change
  its governing arrangement through the agreed in-world process; three seats
  are its starting design, not an immutable world constraint.
- **Each adult resident may support up to one different willing candidate
  per available seat.** In a three-seat election a voter may choose up to three
  distinct willing candidates, or fewer. No repeated vote for one candidate on
  the same ballot. Candidates with the highest totals win the available seats.
  Ties and too few candidates follow the agreed
  [council continuity rules](#election-terms-and-council-continuity).
  Turnout, candidate eligibility, self-voting and ballot revision follow the
  agreed [ballot rules](#election-eligibility-ballots-and-notices).

Use recorded Town adult residents for civic eligibility, including homeless
residents and travelers, under the agreed membership work in
[#602](https://github.com/compoodment/ClankerWorld/issues/602).
These proposal, voting-window, seat-count and ballot choices are settled.
The remaining ordinary council-process rules are agreed in
[Council changes and election scheduling](#council-changes-and-election-scheduling).
The completed voting and election decision is tracked in
[#601](https://github.com/compoodment/ClankerWorld/issues/601). The authority and procedure
for broader changes to the governing arrangement are agreed in
[Government-change procedure and safeguards](laws-and-enforcement.md#government-change-procedure-and-safeguards).

### Election terms and council continuity

**Agreed with the owner on October 1, answers 29A–33A:**

- **An elected council's initial term is ten unpaused world days**, one
  season. Open voting before the scheduled term ends so residents can choose
  the next council. For example, a council seated on day 12 has its next term
  due on day 22. Paused time does not advance the term.
- **A tie for an unresolved seat gets one runoff lasting one unpaused world
  day, then a fair draw if the tie remains.** Only the tied candidates and
  unresolved seats enter the runoff; already settled seats stay settled.
  Record the draw so saving and reloading cannot reroll the outcome.
- **A vacant seat needs a replacement election for the remainder of the
  existing term.** Do not automatically promote a runner-up from an earlier
  election or restart the term. If three willing representatives cannot be
  formed, temporarily return to the all-adult council. Proposal majorities
  during vacancies and election retries follow the agreed
  [scheduling rules](#council-changes-and-election-scheduling).
- **Representation ends when only three or fewer adult Town residents
  remain.** Then use the all-adult council until the Town has four adults
  again. Count recorded residents, including travelers and homeless adults,
  rather than adults currently standing inside the border.
- **The initial election window is one unpaused world day.** The existing
  council governs until a valid replacement is ready. During the first
  election, the all-adult council continues governing. The vacancy and small
  population rules above supply the all-adult fallback when needed. Election
  windows and terms are initial values to try, not immutable world laws.

Candidate and voter eligibility, turnout and ballot handling follow the
[agreed rules below](#election-eligibility-ballots-and-notices). Vacancy proposal
majorities, election retries and pending proposals follow
[Council changes and election scheduling](#council-changes-and-election-scheduling).

### Election eligibility, ballots and notices

**Agreed with the owner on October 1, answers 34A–39A:**

- **Any willing adult Town resident may stand for election and vote for
  themselves.** Homeless residents and travelers qualify through recorded
  membership. Standing requires the candidate's recorded agreement; another
  agent cannot make them a willing candidate by merely naming them. Use the
  [continuing candidate register](#council-changes-and-election-scheduling)
  rather than a separate nomination period.
- **There is no minimum election turnout, including for a runoff.** Each
  winner must have received at least one vote in the main election. If a full
  council of three eligible, willing, supported representatives cannot be
  formed, use the all-adult fallback. One voter can therefore elect three
  willing candidates, but a zero-vote candidate cannot gain a seat through
  a draw. A runoff with no new ballots can still end with the agreed fair draw
  among the previously supported, tied candidates.
- **Fix the eligible voter and candidate lists when voting opens.** Later
  arrivals, newly grown adults and late volunteers wait for a later election.
  If an agent dies or leaves recorded Town membership before that window
  closes, remove their eligibility, their ballot and any candidacy. Travel alone
  does not remove eligibility. A runoff starts with a fresh eligible voter
  list, but its candidates remain limited to the original, still-eligible tied
  candidates; it is not a new opportunity to nominate someone else.
- **Election ballots may change until the deadline; a council proposal vote
  is final once cast.** Count only the latest election ballot from each voter.
  Final proposal votes preserve early resolution once a result is settled;
  election ballots do not become final merely because a candidate leads
  before closing.
- **Agents learn about proposals and candidates through actual Town notices
  or another agent relaying information.** What they read or hear enters their
  ordinary personal-model context. Do not automatically inject unseen civic
  information into every resident's knowledge or invent candidate positions.
  A traveler can miss an election. Relayed claims remain what was communicated,
  rather than automatically becoming verified world facts.
- **When a selected candidate withdraws, remove that choice and keep the
  ballot's other choices.** The voter may revise the ballot during the
  remaining window. Withdrawal does not reset the deadline or require every
  affected voter to submit again. A ballot for Mira, Sol and Ash becomes a
  ballot for Sol and Ash if Mira withdraws.

These eligibility, ballot and delivery rules are settled. The remaining
ordinary process choices are agreed below; this completes
[#601](https://github.com/compoodment/ClankerWorld/issues/601)'s voting and
election interview.

### Council changes and election scheduling

**Agreed with the owner on October 1, answers 40A–42A, 43B and 44A–45A:**

- **A representative council still needs two yes votes while seats are
  vacant.** One remaining representative cannot approve an ordinary proposal
  alone. When the all-adult fallback governs, use a strict majority of its
  own adult membership instead of the representative council's threshold.
- **Close unfinished ordinary council proposals without passage when the
  council changes.**
  This applies to changes in eligible council membership, representatives or
  governing form. An agent may submit the proposal afresh to the current
  council, with fresh votes and a new one-unpaused-day window. Previously
  settled decisions stay settled. For example, an admission into an all-adult
  council changes its membership and closes other unfinished proposals.
- **Merge equivalent pending requests without resetting the deadline.**
  After failure or withdrawal, wait one unpaused world day before retrying the
  same request. Material new information or changed circumstances, including
  a changed council, allow earlier reconsideration. Merely rewording the same
  admission request does not restart its window.
- **Keep a continuing register of willing candidates; there is no separate
  nomination period.** Agents may nominate themselves or explicitly accept
  another agent's nomination through ordinary personal-model turns and actual
  notices. Regular voting opens one unpaused world day before the scheduled
  term ends. Initial or replacement voting can open immediately using the
  eligible registered candidates, subject to the one-contest rule below.
  Candidate and voter lists still become fixed when each voting window opens.
  Registration during that window is for a later contest. A cutoff runoff
  remains limited to its original eligible tied candidates.
- **Retry an unsuccessful election after one unpaused world day; materially
  improved circumstances allow an earlier attempt.** Use the continuing
  register for the new attempt, with no added nomination phase. A failed
  regular election does not shorten the incumbents' remaining term; after
  that term ends, use the all-adult fallback while retrying. An unfinished
  runoff retains the existing council under the agreed continuity rule.
  If representation ended because the population fell to three or fewer,
  elections wait until the Town has four adults again.
- **Keep one active election process per Town; the scheduled full election
  takes priority when its voting opens.** Stop unresolved replacement voting
  and use fresh ballots for the full election. Already decided replacement
  seats remain valid for the old term's remainder; do not undo them while
  cancelling an unresolved round for another seat. A vacancy arising during
  the full election is handled by that contest. Agreement to fill a short
  vacancy does not automatically mean agreement to stand for the next full
  term. With the continuing register, priority starts at regular voting, not
  at an invented nomination phase.

These rules complete the initial ordinary proposal and election process.
Law scope and the protected government-change process are agreed in [Town law scope and changes](laws-and-enforcement.md#town-law-scope-and-changes) and [Government-change procedure and safeguards](laws-and-enforcement.md#government-change-procedure-and-safeguards).
The mayor's office is also agreed in [The mayor's office and elections](laws-and-enforcement.md#the-mayors-office-and-elections); enforcement and other topics stay separate.
Implementation is tracked in
[#618](https://github.com/compoodment/ClankerWorld/issues/618), followed by
Town membership and admission integration in
[#602](https://github.com/compoodment/ClankerWorld/issues/602).
