#!/usr/bin/env bash
# Issue #1364, camp stage: the game has no camp. Worlds start with households
# and Towns, so no code, test, game text or current doc may still say "camp".
# It runs last, after rename.sh, arch.sh, client.sh and domain.sh, from the
# root of their output:
#
#   bash stages/camp.sh                 # every step, in order
#   STEPS="ids check" bash stages/camp.sh
#
# Steps (always run in this order):
#   legacy   delete code that only pre-schema-bump saves could reach: the
#            "Camp Alpha"/"Camp Beta" household label cleanup, the "campfire"
#            map-object kind no generator writes, a retired recipe name in two
#            parser cases, and the camp words of the model-wording test
#   ids      saved and seeded IDs: household:first and household:second, their
#            starting lots (food:first, wood:second, ...), the world seeds
#            world-a/b/c and test-alpha, the client tests' agent-a, the small
#            map's cooking and path objects, the rations meal kind, the
#            noise-drainage chunk generator, the pile event token, the test
#            content package and the world-observation digest tag
#   text     player-facing text, model-facing text, messages and comments
#   docs     current docs, the playtest list, this change's entry, the
#            renamed starting-site reachability measurements and the saves page
#   digests  the golden map digests of the renamed seed corpus
#   check    nothing may say camp outside history, and the old IDs are gone
#
# Every new ID is no longer than the one it replaces, because IDs feed derived
# identities with length limits. Saves made before rename.sh's schema bump are
# refused, so old IDs need no reader. History keeps its words: CHANGELOG.md,
# changes/ (except this change's own entry), docs/archive/, and the probe and
# equivalence evidence in docs/development/*/. The renamed measurements page
# keeps the probe's own names, with a line saying the code then said camp.
#
# The stage is deterministic: ordered literal and regex rules over the tracked
# files, chosen by path pattern so earlier stages may move files. Every rule
# must match, so a changed anchor fails loudly. Run again on its own output it
# finds nothing left and stops without changing anything. It does not commit.
set -euo pipefail

die() { echo "camp.sh: error: $*" >&2; exit 1; }
note() { echo "camp.sh: $*" >&2; }

ROOT=$(git rev-parse --show-toplevel 2>/dev/null) || die "run inside a git checkout"
cd "$ROOT"
[[ -f ClankerWorld.sln ]] || die "not the ClankerWorld repository root: $ROOT"

STEPS=${STEPS:-"legacy ids text docs digests check"}
has_step() { [[ " $STEPS " == *" $1 "* ]]; }

SIM=src/ClankerWorld.Simulation
SERVER=src/ClankerWorld.Server
CLIENT=src/ClankerWorld.GodotClient
TESTS=tests/ClankerWorld.Tests
OLD_DOC=docs/development/camp-reachability-measurements.md
NEW_DOC=docs/development/starting-site-reachability-measurements.md
OLD_PROBE=docs/development/camp-reachability-probe
NEW_PROBE=docs/development/starting-site-reachability-probe
ENTRY=changes/1364-prototype-names.md

# What may not be left once the stage has run: "camp" (not "campaign") outside
# history and the renamed measurements page, and the old IDs anywhere in code.
leftovers() {
  git grep -n -I -i -P 'camp(?!aign)' -- . ':!CHANGELOG.md' ':!changes/*' ':!docs/archive/*' \
    ':!docs/development/*/*' ":!$NEW_DOC" ':(exclude)global.json' || true
  git grep -n -I -i -P 'camp(?!aign)' -- "$ENTRY" || true
  git grep -n -I -P 'playtest-alpha|private-world-observation' -- src tests || true
  # "camp" in base64 at each of its three alignments, as signed payload tests spell IDs.
  git grep -n -I -P 'Y2Ftc|NhbXA|jYW1w' -- src tests || true
  git ls-files | grep -i camp | grep -vP '^(CHANGELOG\.md|changes/|docs/archive/)' || true
}

# Run again on its own output, the stage finds nothing to do.
if [[ "$STEPS" == "legacy ids text docs digests check" && ! -e "$OLD_DOC" && -z "$(leftovers)" ]]; then
  note "nothing says camp outside history; already applied"
  exit 0
fi

# global.json may be blanked locally to build with an older SDK; ignore it.
dirty=$(git status --porcelain --untracked-files=no -- . ':(exclude)global.json')
[[ -z "$dirty" ]] || die "working tree has changes; start from a clean checkout:
$dirty"
[[ -d "$SIM" && -d "$SERVER" && -d "$CLIENT" && -d "$TESTS" ]] ||
  die "run after rename.sh: $SIM, $SERVER, $CLIENT and $TESTS must exist"
SCRATCH=$(mktemp -d)
trap 'rm -rf "$SCRATCH"' EXIT

# ---------------------------------------------------------------------------
# Files
# ---------------------------------------------------------------------------

# History and evidence that keep their original words.
is_history() {
  case "$1" in
    changes/1364-prototype-names.md) return 1 ;;       # this change's own entry
    CHANGELOG.md|changes/*|docs/archive/*) return 0 ;;
    docs/development/*/*) return 0 ;;                  # probe and equivalence evidence
    */packages.lock.json) return 0 ;;
  esac
  return 1
}

eligible_files() {
  git ls-files -z -- '*.cs' '*.csproj' '*.props' '*.targets' '*.sln' \
    '*.sh' '*.js' '*.mjs' '*.ps1' '*.py' '*.json' '*.yml' '*.yaml' '*.md' \
    '*.tscn' '*.tres' '*.godot' '*.cfg' '*.txt' '*.service' '*.html' '*.css' '*.iss' |
  while IFS= read -r -d '' file; do
    [[ -f "$file" ]] || continue
    is_history "$file" || printf '%s\0' "$file"
  done | sort -z
}

# ---------------------------------------------------------------------------
# Rules: one per line, "KIND ⟫ PATH ⟫ FROM ⟫ TO", applied in order to every
# eligible file whose path matches the Perl regex PATH. KIND is "lit" (literal
# FROM) or "re" (Perl regex FROM; TO may use $1..$3). "¶" stands for a line
# break. A rule must change at least one file. Lines starting with # are notes.
# ---------------------------------------------------------------------------
APPLY=$SCRATCH/apply-rules.pl
cat > "$APPLY" <<'PERL'
use strict; use warnings;
my ($rules_file, $files_file, $label) = @ARGV;
my @rules;
open my $r, '<', $rules_file or die "cannot read $rules_file\n";
while (my $line = <$r>) {
  chomp $line;
  next if $line =~ /^\s*(#|$)/;
  my @f = split / ?\xE2\x9F\xAB ?/, $line, -1;
  die "bad rule in $label at line $.: $line\n" unless @f == 4;
  my ($kind, $path, $from, $to) = @f;
  die "unknown rule kind '$kind' in $label at line $.\n" unless $kind =~ /^(lit|re)$/;
  s/\xC2\xB6/\n/g for $from, $to;
  push @rules, { kind => $kind, path => qr/$path/, from => $kind eq 'lit' ? qr/\Q$from\E/ : qr/$from/m,
                 to => $to, text => $line, line => $., hits => 0 };
}
close $r;
local $/ = "\0";
open my $l, '<', $files_file or die;
my @files = map { chomp; $_ } <$l>;
close $l;
local $/;
my $changed = 0;
for my $file (@files) {
  my @mine = grep { $file =~ $_->{path} } @rules;
  next unless @mine;
  open my $in, '<', $file or die "cannot read $file\n";
  binmode $in; my $text = <$in> // ''; close $in;
  my $before = $text;
  for my $rule (@mine) {
    my $n;
    if ($rule->{kind} eq 'lit') {
      my $to = $rule->{to};
      $n = ($text =~ s/$rule->{from}/$to/g);
    } else {
      my $rep = $rule->{to};
      $n = ($text =~ s{$rule->{from}}{ my @m = ($1, $2, $3); my $x = $rep; $x =~ s/\$(\d)/defined $m[$1 - 1] ? $m[$1 - 1] : ""/ge; $x }ge);
    }
    $rule->{hits} += $n || 0;
  }
  if ($text ne $before) {
    open my $out, '>', $file or die "cannot write $file\n"; binmode $out; print $out $text; close $out;
    $changed++;
  }
}
my @missing = grep { !$_->{hits} } @rules;
die "$label: these rules matched nothing:\n" . join('', map { "  line $_->{line}: $_->{text}\n" } @missing) if @missing;
printf STDERR "camp.sh: %s: %d rules changed %d files\n", $label, scalar(@rules), $changed;
PERL

# apply_rules LABEL: the rules on standard input, over every eligible file.
apply_rules() {
  local label=$1
  cat > "$SCRATCH/$label.rules"
  eligible_files > "$SCRATCH/files"
  perl "$APPLY" "$SCRATCH/$label.rules" "$SCRATCH/files" "$label"
}

require_file() { [[ -f "$1" ]] || die "expected file is missing: $1"; }
require_absent() { [[ ! -e "$1" ]] || die "target already exists: $1"; }

# one_file PATTERN: the single tracked file whose path matches a Perl regex.
one_file() {
  local found=()
  mapfile -t found < <(git ls-files | grep -P -- "$1" || true)
  (( ${#found[@]} == 1 )) || die "expected one file matching /$1/, found ${#found[@]}: ${found[*]:-none}"
  echo "${found[0]}"
}

# delete_member FILE NAME: removes the method NAME, with its attributes and
# doc comment, from a C# file formatted the usual way (closing brace at the
# method's own indentation), and one blank line after it.
delete_member() {
  local file=$1 name=$2
  require_file "$file"
  NAME="$name" perl -0pi -e '
    my $name = $ENV{NAME};
    my @lines = split /(?<=\n)/, $_;
    my ($sig) = grep { $lines[$_] =~ /^(\s*)(?:(?:public|private|internal|protected|static|async)\s+)*[\w<>\[\],? ]+\s\Q$name\E\s*\(/ } 0..$#lines;
    defined $sig or die "method $name not found\n";
    my ($indent) = $lines[$sig] =~ /^(\s*)/;
    my $start = $sig;
    $start-- while $start > 0 && $lines[$start - 1] =~ /^\Q$indent\E(?:\[|\/\/\/)/;
    my $end = $sig;
    $end++ while $end <= $#lines && $lines[$end] !~ /^\Q$indent\E\}\s*$/;
    die "end of method $name not found\n" if $end > $#lines;
    $end++ if $end + 1 <= $#lines && $lines[$end + 1] =~ /^\s*$/;
    splice @lines, $start, $end - $start + 1;
    $_ = join "", @lines;
  ' "$file"
}

# ---------------------------------------------------------------------------
# Step legacy: code only an old save could reach.
#
# - New worlds name their households "First household" and "Second household"
#   at genesis. The observer's mapping from "Camp Alpha" and "Camp Beta" only
#   served saves from before that change, which the schema now refuses, so it
#   goes with the test of it.
# - No map generator writes an object of kind "campfire"; the small map's
#   cooking object has kind "cooking". The client's "campfire" alternatives,
#   their glyph and their check go.
# - "cook camp meal" checks that an order naming a retired recipe is not
#   understood; the other retired names keep that check.
# - The model-wording check stops listing camp words: nothing says camp now.
# ---------------------------------------------------------------------------
step_legacy() {
  note "step legacy"
  delete_member "$(one_file "^$TESTS/(.*/)?GameUiTextTests\\.cs\$")" HouseholdDisplayCleanupPreservesSavedMembershipAndCustomNames
  apply_rules legacy <<'RULES'
re ⟫ /OwnerWorldObservationStore\.cs$ ⟫ new StockpileView\(household\.Id, \(household\.Id, household\.Name\) switch\s*\{\s*\("household:camp-alpha", "Camp Alpha"\) => "First household",\s*\("household:camp-beta", "Camp Beta"\) => "Second household",\s*_ => household\.Name,\s*\}, InventoryFor ⟫ new StockpileView(household.Id, household.Name, InventoryFor
lit ⟫ /BuildingSprites\.cs$ ⟫ "campfire" or "cooking" => BuildingKind.Hearth, ⟫ "cooking" => BuildingKind.Hearth,
lit ⟫ /Main\.Presentation\.cs$ ⟫ "campfire" or "cooking" => "Campfire", ⟫ "cooking" => "Cooking fire",
lit ⟫ /Main\.Presentation\.cs$ ⟫         "campfire" => "✦",¶ ⟫
lit ⟫ /Main\.UiChecks\.cs$ ⟫                 BuildingSprites.KindForObject("campfire") != BuildingKind.Hearth ||¶ ⟫
lit ⟫ /WorldProduction(OrderParser|Order)Tests\.cs$ ⟫     [InlineData("cook camp meal")]¶ ⟫
lit ⟫ /ModelFacingWordingTests\.cs$ ⟫ "settlements", "settlement", "camps", "camp"] ⟫ "settlements", "settlement"]
lit ⟫ /ModelFacingWordingTests\.cs$ ⟫ such as "campfire" from tripping ⟫ such as "resettlement" from tripping
lit ⟫ ^docs/development/how-it-works\.md$ ⟫ *inhabitant*, *settlement* and *camp*, including plurals. ⟫ *inhabitant* and *settlement*, including plurals.
RULES
  local file left
  for file in "$(one_file '/WorldProductionOrderParserTests\.cs$')" "$(one_file '/WorldProductionOrderTests\.cs$')"; do
    grep -q 'InlineData("cook household meal")' "$file" || die "$file lost its other retired recipe cases"
  done
  left=$(git grep -n 'Camp Alpha\|Camp Beta\|"campfire" or\|"campfire" =>' -- src || true)
  [[ -z "$left" ]] || die "legacy camp code is left:
$left"
}

# ---------------------------------------------------------------------------
# Step ids: saved, seeded and derived IDs. Old saves are refused by schema, so
# nothing reads the old spellings. The seeds change their small maps; the
# digests step updates the golden map digests.
# ---------------------------------------------------------------------------
step_ids() {
  note "step ids"
  local name
  for name in '"household:first"' '"household:second"' '"world-a"' '"world-b"' '"world-c"' '"test-alpha"' \
    '"new-site"' '"rations"' '"noise-drainage"' 'test-kitchen' 'test-recipes' '"reuse-kind"' '"zz-spare-wood"' \
    '"retained-wood"' '"expansion-heap-wood"' 'clankerworld.world-observation/'; do
    ! git grep -q -F -- "$name" -- src tests || die "new ID $name is already used"
  done
  apply_rules ids <<'RULES'
# The spoiled-delivery event names where the goods went, not a household.
lit ⟫ /WorldEventTextTests\.cs$ ⟫ ":spoiled-greens:4:camp-alpha" ⟫ ":spoiled-greens:4:pile"
# In the client's tests the bare ID is the agent an order targets or acts as.
re ⟫ ^tests/[^/]+/GodotClient/ ⟫ (?<=")camp-alpha(?=["\\]) ⟫ agent-a
# The signed payload tests spell that agent ID in base64url too.
lit ⟫ ^tests/[^/]+/GodotClient/ ⟫ target-agent-id=Y2FtcC1hbHBoYQ" ⟫ target-agent-id=YWdlbnQtYQ"
# Everywhere else a bare ID is a world seed and world ID.
re ⟫ . ⟫ (?<=")camp-alpha(?=") ⟫ world-a
re ⟫ ^tests/ ⟫ (?<=")camp-beta(?=") ⟫ world-b
re ⟫ ^tests/ ⟫ (?<=")camp-gamma(?=") ⟫ world-c
re ⟫ ^tests/ ⟫ (?<=")playtest-alpha(?=") ⟫ test-alpha
re ⟫ ^tests/ ⟫ (?<=")new-camp(?=") ⟫ new-site
# The two starting households and the starting lots named after them.
re ⟫ . ⟫ :camp-alpha\b ⟫ :first
re ⟫ . ⟫ :camp-beta\b ⟫ :second
# The small map's objects take their kind as ID, like shelter and storage.
re ⟫ ^(src|tests)/ ⟫ "campfire"(?=, "cooking") ⟫ "cooking"
lit ⟫ /SmallTemperateMapGenerator\.cs$ ⟫ new MapObject("camp-path", "path", ⟫ new MapObject("path", "path",
# A meal from stored food, the large maps' chunk generator.
lit ⟫ ^(src|tests)/ ⟫ "camp_rations" ⟫ "rations"
lit ⟫ /EnvironmentSystem\.Genesis\.cs$ ⟫ "noise-drainage-camp" ⟫ "noise-drainage"
# Spare supplies set down outside a House go to the household's storage pile.
lit ⟫ /WorldRuntime\.HouseholdHauling\.cs$ ⟫ {physicalQuantity}:camp" ⟫ {physicalQuantity}:pile"
lit ⟫ /WorldRuntime\.HouseholdHauling\.cs$ ⟫ ?? "camp"} ⟫ ?? "pile"}
re ⟫ ^tests/ ⟫ \{Pot\}:4:camp" ⟫ {Pot}:4:pile"
# The observation digest's tag: the game's world, not a private world.
lit ⟫ /WorldRuntime\.Cognition\.cs$ ⟫ "clankerworld.private-world-observation/v2|" ⟫ "clankerworld.world-observation/v2|"
# Test fixtures: a content package's kitchen, resource and lot IDs.
lit ⟫ /GroundOccupancyTests\.cs$ ⟫ ["camp"] = ⟫ ["objects"] =
re ⟫ /GroundOccupancyTests\.cs$ ⟫ (?<=[" ])camp(?=[" ]) ⟫ objects
lit ⟫ ^tests/ ⟫ "camp-kitchen ⟫ "test-kitchen
lit ⟫ ^tests/ ⟫ "Camp kitchen" ⟫ "Test kitchen"
lit ⟫ ^tests/ ⟫ camp-recipes ⟫ test-recipes
re ⟫ ^tests/ ⟫ (\[|"tags":\[)"camp"\] ⟫ $1"kitchen"]
lit ⟫ ^tests/ ⟫ buildingTag = "camp" ⟫ buildingTag = "kitchen"
lit ⟫ /Main\.ResourceAppearanceChecks\.cs$ ⟫ "reuse-camp" ⟫ "reuse-kind"
lit ⟫ ^tests/ ⟫ "zz-spare-camp-wood" ⟫ "zz-spare-wood"
lit ⟫ ^tests/ ⟫ "retained-camp-wood" ⟫ "retained-wood"
lit ⟫ ^tests/ ⟫ "expansion-camp-wood" ⟫ "expansion-heap-wood"
# A save in the save-timeline check, with its name.
lit ⟫ /Main\.SaveTimelineChecks\.cs$ ⟫ First camp ⟫ First farm
lit ⟫ /Main\.SaveTimelineChecks\.cs$ ⟫ "camp" ⟫ "farm"
RULES
  local left
  left=$(git grep -n -P 'camp-(alpha|beta|gamma)|playtest-alpha|new-camp|camp_rations|noise-drainage-camp|camp-path|private-world-observation' -- . \
    ':!CHANGELOG.md' ':!changes/*' ':!docs/archive/*' ':!docs/development/*/*' || true)
  [[ -z "$left" ]] || die "old IDs are left:
$left"
}

# ---------------------------------------------------------------------------
# Step text: what players and models read first, then messages and comments.
# The words say what the thing is: the household, its House or storage pile,
# the shared storage spot (SharedStoragePosition), the starting site, map
# objects, the server's hidden bootstrap world.
# ---------------------------------------------------------------------------
step_text() {
  note "step text"
  apply_rules text <<'RULES'
# Player-facing and model-facing text.
lit ⟫ /GameUiText\.cs$ ⟫ "collect_shared_food" => "collect food from camp", ⟫ "collect_shared_food" => "collect household food",
lit ⟫ /WorldRuntime\.HouseholdHauling\.cs$ ⟫ "Return unusable delivery supplies to your household's pile at camp." ⟫ "Return unusable delivery supplies to your household's storage pile."
lit ⟫ /EnvironmentSystem\.Physical\.cs$ ⟫ "make the camp self-sufficient" ⟫ "make the household self-sufficient"
lit ⟫ /GameUiTextTests\.cs$ ⟫ "Camp Alpha" ⟫ "First household"
lit ⟫ ^(src|tests)/ ⟫ suitable base-camp clearing ⟫ suitable starting clearing
lit ⟫ /ServerHttpCandidateFailureTests\.cs$ ⟫ Assert.DoesNotContain("base-camp", ⟫ Assert.DoesNotContain("starting clearing",
lit ⟫ /MapAcceptance\.cs$ ⟫ A required camp-start ⟫ A required starting-site
lit ⟫ /TownContracts\.cs$ ⟫ on a map without camp objects. ⟫ on a map without placed objects.
# Map objects: what the small maps place. Generated maps place none.
lit ⟫ /BuildingSprites\.cs$ ⟫ placed building or legacy camp object. ⟫ placed building or map object.
lit ⟫ /BuildingSprites\.cs$ ⟫ retired camp objects keep their earlier provisional drawing. ⟫ map objects keep their earlier provisional drawing.
lit ⟫ /BuildingSprites\.cs$ ⟫ Legacy camp objects that have a building look ⟫ Map objects that have a building look
lit ⟫ /BuildingSprites\.cs$ ⟫ kept for the retired camp objects. ⟫ kept for map objects.
lit ⟫ /WorldTerrainLayer\.cs$ ⟫ legacy camp objects that have a building look. ⟫ map objects that have a building look.
lit ⟫ /Main\.Presentation\.cs$ ⟫ Objects drawn as camp art are named like buildings ⟫ Objects drawn with building art are named like buildings
lit ⟫ /RiverBankWideningTests\.cs$ ⟫ resource and camp blocker ⟫ resource and map-object blocker
re ⟫ ^(src|tests|docs)/ ⟫ \bcamp objects\b ⟫ map objects
re ⟫ ^(src|tests)/ ⟫ \bcamp object\b ⟫ map object
lit ⟫ /WorldSelectionCoordinator\.cs$ ⟫ fresh maps have no placed camp or Town at this site. ⟫ fresh maps have no placed objects or Town at this site.
# Resources that record only a resource kind.
lit ⟫ /NatureSprites\.cs$ ⟫ older camp resources and a Town's starting supplies ⟫ a Town's starting supplies
lit ⟫ /WorldTerrainLayer\.cs$ ⟫ Older camp resources carry only a resource kind; draw them with the ⟫ Resources that carry only a resource kind draw with the
lit ⟫ /Main\.UiChecks\.cs$ ⟫ "Older camp resources such as the wood store must draw ⟫ "Resources with only a resource kind, such as the wood store, must draw
lit ⟫ /Main\.ResourceAppearanceChecks\.cs$ ⟫ "Removing a camp resource must ⟫ "Removing a resource-kind-only resource must
# Night light comes from forges and lanterns; nothing lights a fire object.
lit ⟫ /NightLightShapes\.cs$ ⟫ from a forge or a¶/// campfire, or from a lantern; ⟫ from a forge or¶/// from a lantern;
# The save timeline's You are here marker: an orange disc with a person.
lit ⟫ /SaveTimeline\.cs$ ⟫ An orange camp marker at the end ⟫ An orange marker with a person at the end
# The hidden bootstrap world a fresh server starts with, before New World.
lit ⟫ /Main\.FreshHostChecks\.cs$ ⟫ without exposing or resuming its old camp. ⟫ without exposing or resuming its bootstrap world.
lit ⟫ /Main\.FreshHostChecks\.cs$ ⟫ must keep the camp behind the menus ⟫ must keep the bootstrap world behind the menus
lit ⟫ /ObservationContracts\.cs$ ⟫ should open New World instead of the retired camp. ⟫ should open New World instead of being shown.
lit ⟫ /KnowledgeReadOrderTests\.cs$ ⟫ The retired camp has only thirty tiles. ⟫ The small test map has only thirty tiles.
lit ⟫ /WorldCreationObservationTests\.cs$ ⟫ not the retired¶        // base-camp generator ⟫ not the small¶        // starting-site generator
lit ⟫ /NormalPathWorld\.cs$ ⟫ near the camp without ⟫ near the starting site without
# The household's storage pile at the shared storage spot.
lit ⟫ /WorldRuntime\.HouseholdHauling\.cs$ ⟫ otherwise the household's pile at camp. ⟫ otherwise the household's storage pile.
lit ⟫ /WorldRuntime\.Production\.cs$ ⟫ stages a multi-load first House at camp. ⟫ stages a multi-load first House at the shared storage spot.
lit ⟫ /FarmhouseSiloReplenishmentTests\.cs$ ⟫ Keep unrelated camp deliveries claimed ⟫ Keep unrelated household deliveries claimed
lit ⟫ /FarmhouseSiloReplenishmentTests\.cs$ ⟫ // camp supplies claimed for this ⟫ // household supplies claimed for this
lit ⟫ /FarmhouseSiloReplenishmentTests\.cs$ ⟫ camp={startingSite} ⟫ site={startingSite}
lit ⟫ /FilledVesselHouseHaulingTests\.cs$ ⟫ to collect the camp vessel. ⟫ to collect the vessel from the storage pile.
lit ⟫ /PersonalEquipmentTests\.cs$ ⟫ then stages it at camp ⟫ then stages it at the storage pile
lit ⟫ /SpoiledHouseholdDeliveryBuiltInTests\.cs$ ⟫ route to camp within ⟫ route to the storage pile within
lit ⟫ /SpoiledHouseholdDeliveryBuiltInTests\.cs$ ⟫ actual foot steps to camp. ⟫ actual foot steps to the storage pile.
lit ⟫ /SpoiledHouseholdDeliveryContinuationTests\.cs$ ⟫ stay physically at camp, instead ⟫ stay physically at the storage pile, instead
lit ⟫ /SpoiledHouseholdDeliveryFamilyTests\.cs$ ⟫ discarded at the actual camp before ⟫ discarded at the actual storage pile before
lit ⟫ /SpoiledHouseholdDeliveryOrderRecoveryTests\.cs$ ⟫ to the household camp. ⟫ to the household's storage pile.
lit ⟫ /SpoiledHouseholdDeliveryOrderRecoveryTests\.cs$ ⟫ must walk to camp while ⟫ must walk to the storage pile while
lit ⟫ /SpoiledHouseholdDeliveryTests\.cs$ ⟫ must actually walk to camp before ⟫ must actually walk to the storage pile before
lit ⟫ /SpoiledHouseholdDeliveryTests\.cs$ ⟫ becoming real household camp stock. ⟫ becoming real household stock at the storage pile.
lit ⟫ /SpoiledHouseholdDeliveryTests\.cs$ ⟫ Hold unrelated loose camp supplies ⟫ Hold unrelated loose household supplies
lit ⟫ /SurvivalTests\.cs$ ⟫ call them to the camp. ⟫ call them to the shared storage spot.
# Memories and thoughts in test worlds.
lit ⟫ /CognitionTests\.cs$ ⟫ campfire promise ⟫ riverside promise
lit ⟫ /DeceasedAgentArchiveTests\.cs$ ⟫ the first campfire. ⟫ the first harvest.
lit ⟫ /DeceasedAgentArchiveTests\.cs$ ⟫ I hope the camp lasts. ⟫ I hope the farm lasts.
lit ⟫ /WorldMemoryRetrievalTests\.cs$ ⟫ repaired the old camp roof. ⟫ repaired the old shed roof.
lit ⟫ /PlayerRenameMemoryTests\.cs$ ⟫ build shelter near camp. ⟫ build shelter near home.
RULES
}

# ---------------------------------------------------------------------------
# Step docs: current docs say what exists today. The measurements page takes
# the name the code uses now and keeps its measured data.
# ---------------------------------------------------------------------------
step_docs() {
  note "step docs"
  require_file "$OLD_DOC"
  require_file "$OLD_PROBE/probe.cs.txt"
  require_absent "$NEW_DOC"
  require_absent "$NEW_PROBE"
  git mv "$OLD_DOC" "$NEW_DOC"
  git mv "$OLD_PROBE" "$NEW_PROBE"
  local doc=docs/development/saves-and-replay.md
  require_file "$doc"
  grep -qP '^Schema \d+ removes prototype names from the save$' "$doc" ||
    die "$doc has no 'Schema N removes prototype names from the save' paragraph from rename.sh"
  apply_rules docs <<'RULES'
# The renamed measurements page and the links to it.
lit ⟫ ^docs/ ⟫ [Camp reachability measurements](camp-reachability-measurements.md) ⟫ [Starting-site reachability measurements](starting-site-reachability-measurements.md)
lit ⟫ ^docs/development/starting-site-reachability-measurements\.md$ ⟫ title: Camp reachability measurements ⟫ title: Starting-site reachability measurements
lit ⟫ ^docs/development/starting-site-reachability-measurements\.md$ ⟫ # Camp reachability measurements¶¶ ⟫ # Starting-site reachability measurements¶¶When this was measured, the code called the starting site the camp, so the probe¶and the commands below still use camp names.¶¶
lit ⟫ ^docs/development/starting-site-reachability-measurements\.md$ ⟫ repeated¶camp flood-fill ⟫ repeated¶starting-site flood-fill
lit ⟫ ^docs/development/starting-site-reachability-measurements\.md$ ⟫ [The exact disposable probe](camp-reachability-probe/probe.cs.txt) ⟫ [The exact disposable probe](starting-site-reachability-probe/probe.cs.txt)
lit ⟫ ^docs/development/starting-site-reachability-measurements\.md$ ⟫ 15,230 camp-reachable positions; ⟫ 15,230 positions reachable from the starting site;
lit ⟫ ^docs/development/starting-site-reachability-measurements\.md$ ⟫ exercise the existing camp query separately. ⟫ exercise the existing starting-site query separately.
lit ⟫ ^docs/development/starting-site-reachability-measurements\.md$ ⟫ Resource targets and camp placements ⟫ Resource targets and starting-site placements
# Saves: the schema history names what is saved now.
lit ⟫ ^docs/development/saves-and-replay\.md$ ⟫ saved as `mapObjects` instead of `campObjects`, ⟫ saved as `mapObjects`,
lit ⟫ ^docs/development/saves-and-replay\.md$ ⟫ older Towns keep their camp-derived border. ⟫ older Towns keep the border derived from their starting site.
# Developer guide.
lit ⟫ ^docs/development/how-it-works\.md$ ⟫ route to camp. Recovery moves ⟫ route to the shared storage spot. Recovery moves
lit ⟫ ^docs/development/how-it-works\.md$ ⟫ after reaching the House¶or camp pile. ⟫ after reaching the House¶or storage pile.
# Players' pages.
lit ⟫ ^docs/what-works\.md$ ⟫ instead of entering the retired¶test camp. ⟫ instead of entering the¶empty world the server starts with.
lit ⟫ ^docs/what-works\.md$ ⟫ New worlds open paused with no old camp. ⟫ New worlds open paused with nothing built.
lit ⟫ ^docs/what-works\.md$ ⟫ to the House if it has room or to the¶camp pile otherwise ⟫ to the House if it has room or to the¶household's storage pile at the first Town's Warehouse otherwise
lit ⟫ ^docs/what-works\.md$ ⟫ walk it back to camp and set down ⟫ walk it back to the storage pile and set down
lit ⟫ ^docs/what-works\.md$ ⟫ using the same physical House or camp storage as ⟫ using the same physical House or storage pile as
lit ⟫ ^docs/what-works\.md$ ⟫ carry their construction materials to the camp¶pile in loads. ⟫ carry their construction materials to the¶household's storage pile in loads.
lit ⟫ ^docs/playing\.md$ ⟫ household House or camp to make room for food. ⟫ household House or storage pile at the first Town's Warehouse to make room for food.
# Game design: one name per removed building, and the marker as drawn.
lit ⟫ ^docs/game-design/content-list\.md$ ⟫ separate Cooking¶fire/Campfire, Stone hearth ⟫ separate Cooking¶fire, Stone hearth
lit ⟫ ^docs/game-design/towns\.md$ ⟫ Remove separate Cooking fire/Campfire and ⟫ Remove separate Cooking fire and
lit ⟫ ^docs/game-design/interface-and-art\.md$ ⟫ is an orange camp marker at the end ⟫ is an orange marker with a person at the end
# The playtest list.
lit ⟫ ^playtest/alpha-1\.md$ ⟫ New World opens with no old camp or start time. ⟫ New World opens with no old world or start time.
lit ⟫ ^playtest/alpha-1\.md$ ⟫ There is no old camp or Town border, ⟫ There are no buildings and no Town border,
lit ⟫ ^playtest/alpha-1\.md$ ⟫ and use camp supplies. ⟫ and use the Town's starting supplies.
lit ⟫ ^playtest/alpha-1\.md$ ⟫ They put things on the camp pile, ⟫ They put things on the household's storage pile,
lit ⟫ ^playtest/alpha-1\.md$ ⟫ at a legal campsite ⟫ at a legal site
lit ⟫ ^playtest/alpha-1\.md$ ⟫ It is brought back to camp and set down, ⟫ It is brought back to the storage pile and set down,
lit ⟫ ^playtest/alpha-1\.md$ ⟫ Leave a filled water jug at camp because ⟫ Leave a filled water jug at the storage pile because
RULES

  # The saves page lists the saved names this stage changed after the ones
  # rename.sh and the domain stage list for the same schema bump.
  perl -0pi -e '
    my $add = <<"TEXT";
The starting households are saved as `household:first` and
`household:second`, and their starting goods are named after them, such as
`food:first` and `wood:second`. A meal from stored food is recorded as
`rations`, spare supplies set down outside a House are recorded as `pile` in
`spare_cargo_stored` and `household_delivery_recovered` events, the chunks of
generated maps name the generator `noise-drainage`, and the small test map\x27s
cooking fire and path are saved as `cooking` and `path`. Decision intentions
check observations against a `clankerworld.world-observation/v2` digest.
TEXT
    s/(^Schema \d+ removes prototype names from the save\n(?:.+\n)*?)(?=\n)/$1$add/m or die "paragraph end not found\n";
  ' "$doc"

  # This change's entry says what players notice.
  local entry=$ENTRY
  require_file "$entry"
  grep -q '^- ' "$entry" || die "$entry does not start with a bullet"
  perl -0pi -e '
    my $add = " Agents now collect household food and set spare supplies down at their household\x27s storage pile.";
    s/\A(- [^\n]*?)(\n|\z)/$1$add$2/ or die "entry first line not found\n";
  ' "$entry"
}

# ---------------------------------------------------------------------------
# Step digests: the fixed seed corpus is world-a, world-b and world-c now, and
# the small map's cooking fire is saved as "cooking". These are the manifest
# digests of SmallTemperateMapGenerator.Generate(seed), without and with the
# legacy bedroll, for each seed, as the corpus test computes them.
# ---------------------------------------------------------------------------
step_digests() {
  note "step digests"
  apply_rules digests <<'RULES'
re ⟫ /WorldMapRulesTests\.cs$ ⟫ \["world-a", "[0-9a-f]{64}",(\s*)"[0-9a-f]{64}"\] ⟫ ["world-a", "8a9376ad91f76c0ceddee21a88c82eddf124b27cf57c1cc1fc8d2c82f019ae55",$1"5f5601189bb96d1c76071bfd3120d8d82866438f4b804cc1d76424e9a0e3e2cd"]
re ⟫ /WorldMapRulesTests\.cs$ ⟫ \["world-b", "[0-9a-f]{64}",(\s*)"[0-9a-f]{64}"\] ⟫ ["world-b", "4e6c8f314e306a844539ad71c58a1e972aa07eb25c7436418c5d9218f27c4f44",$1"54f6c5c60722ebfc1a509d332e2de66ac34b97639b3793b57d71dba4c95b2efb"]
re ⟫ /WorldMapRulesTests\.cs$ ⟫ \["world-c", "[0-9a-f]{64}",(\s*)"[0-9a-f]{64}"\] ⟫ ["world-c", "141e7f970d2615b801a2af5134328fc9268aff0213e14742ffbb3550d04d8412",$1"c14aee9c51d505461a41b1b5ff354009f372759a2faf62b8b0cc4f67ec7e1565"]
RULES
}

# ---------------------------------------------------------------------------
# Step check: nothing says camp outside history.
# ---------------------------------------------------------------------------
step_check() {
  note "step check"
  local left
  left=$(leftovers)
  [[ -z "$left" ]] || die "camp is left outside history:
$left"
}

# --- run ---------------------------------------------------------------------
for step in $STEPS; do
  declare -F "step_$step" >/dev/null || die "unknown step: $step"
done
for step in legacy ids text docs digests check; do
  has_step "$step" || continue
  "step_$step"
done
git add -A -- . ':(exclude)global.json'
note "done: steps '$STEPS'"
