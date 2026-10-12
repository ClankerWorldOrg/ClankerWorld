#!/usr/bin/env bash
# Issue #1364, client stage: organise the Godot game client and the tests so
# that names and places say what things are. Runs after rename.sh and the
# architecture stage (arch.sh), before the domain stage:
#
#   bash rename.sh && bash stages/arch.sh && bash stages/client.sh && bash stages/domain.sh
#
# Run from the root of a ClankerWorld checkout; it does not commit. Steps run
# in this order (STEPS="..." runs a subset, in the same order):
#
#   unused      delete Protocol/WorldObservationProtocol.cs, which nothing uses
#   namespaces  Protocol/* and Networking/OwnerWorldApi.cs get the namespace
#               of their folder instead of ClankerWorld.GodotClient.UI
#   panels      client field, method and class names match what the player
#               sees: Agents, World Map, Pause Menu, Event Log, Agent model,
#               world edit; WorldOverview -> WorldMapAtlas, MenuScene -> MenuValleyArt
#   partials    Screens/Main/Main.*.cs named and split for their contents
#   scene       Main.cs and Main.tscn move into Screens/Main/
#   text        UI/Theme/*Text.cs -> UI/Text/
#   menu        the menu art into UI/Menu/; MarketSprites.cs -> BuildingSprites.Market.cs
#   checks      Validation/ -> ClientChecks/, *SmokeTests -> *Checks
#   tests       client tests into the folders of what they test; Native* prefixes dropped
#   uids        Godot .uid sidecars for the new scripts, and a final check
#
# It only moves and renames: git mv for files (with their .cs.uid sidecars),
# whole class members moved between partial files of the same class, and
# identifier rewrites in code. Behaviour does not change. It never touches
# CHANGELOG.md, changes/, docs/archive/ or measurement and probe evidence.
# It fails loudly when something it expects is missing or a new name is
# already taken. Names the architecture stage may already have changed
# (LocalHostCompanion -> LocalServer, Main.LocalHost.cs, Hosted* -> Model*)
# are accepted in either form, or left alone.
set -euo pipefail

die() { echo "client.sh: error: $*" >&2; exit 1; }
note() { echo "client.sh: $*" >&2; }

HERE=$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")
CS_TOOL=$HERE/client-cs.pl
[[ -f "$CS_TOOL" ]] || die "client-cs.pl must sit next to this script"
cs() { perl "$CS_TOOL" "$@"; }

ROOT=$(git rev-parse --show-toplevel 2>/dev/null) || die "run inside a git checkout"
cd "$ROOT"
[[ -f ClankerWorld.sln ]] || die "not the ClankerWorld repository root: $ROOT"
# global.json may be blanked locally to build with an older SDK; ignore it.
dirty=$(git status --porcelain --untracked-files=no -- . ':(exclude)global.json')
[[ -z "$dirty" ]] || die "working tree has changes; start from a clean checkout:
$dirty"

STEPS=${STEPS:-"unused namespaces panels partials scene text menu checks tests uids"}
has_step() { [[ " $STEPS " == *" $1 "* ]]; }

C=src/ClankerWorld.GodotClient
M=$C/Screens/Main
T=tests/ClankerWorld.Tests
TC=$T/GodotClient
TEST_PROJECT=$T/ClankerWorld.Tests.csproj
ART=tools/ArtPreview
SCRATCH=$(mktemp -d)
trap 'rm -rf "$SCRATCH"' EXIT
[[ -d "$C" && -f "$TEST_PROJECT" ]] || die "expected $C and $TEST_PROJECT"
# The client checks folder, whichever name it has at this step.
checks_dir() { if [[ -d $C/ClientChecks ]]; then echo "$C/ClientChecks"; else echo "$C/Validation"; fi; }

# ---------------------------------------------------------------------------
# Files and text
# ---------------------------------------------------------------------------

# Tracked text files that may name code identifiers or paths, NUL separated,
# without history and evidence that must keep its original wording.
eligible_files() {
  git ls-files -z -- '*.cs' '*.csproj' '*.props' '*.targets' '*.sln' \
    '*.sh' '*.js' '*.mjs' '*.ps1' '*.py' '*.json' '*.yml' '*.yaml' '*.md' \
    '*.tscn' '*.tres' '*.godot' '*.cfg' '*.txt' '*.html' '*.css' '*.iss' |
  perl -0 -ne 'chomp; next unless -f $_;
    next if m{^(?:CHANGELOG\.md|changes/|docs/archive/)} || /\.cs\.txt$/ || m{/packages\.lock\.json$} ||
      m{^docs/development/[^/]*-(?:probe|equivalence)/} || m{^docs/development/[^/]*-measurements\.md$};
    print "$_\0"'
}

# The C# files of the class Main: Main.cs, Screens/Main/ and the client checks.
main_files() {
  git ls-files -- "$C/Main.cs" "$M/*.cs" "$C/Validation/*.cs" "$C/ClientChecks/*.cs"
}
client_files() { git ls-files -- "$C/*.cs"; }

# replace_in FILE OLD NEW: literal rewrite in one file that must match.
replace_in() {
  local file=$1
  [[ -f "$file" ]] || die "expected file is missing: $file"
  OLD="$2" NEW="$3" perl -0pi -e '$n += s/\Q$ENV{OLD}\E/$ENV{NEW}/g; END { exit($n ? 0 : 3) }' "$file" ||
    die "expected text not found in $file: $2"
}

require_file() { [[ -f "$1" ]] || die "expected file is missing: $1"; }
require_absent() { [[ ! -e "$1" ]] || die "target already exists: $1"; }

# Moved paths wait in a list and are rewritten in one pass at the end of each
# step (flush_paths): wherever a tracked text file names one, with / or \
# (and \\ in JavaScript strings), after a folder prefix such as
# src/ClankerWorld.GodotClient/ or GodotClient\, and as res://PATH for client
# files. In Markdown a client path inside a folder also matches without the
# prefix (`UI/MenuScene.cs`). A path nothing names is fine.
PATHS=$SCRATCH/paths
: > "$PATHS"
queue_path() { printf '%s\t%s\n' "$1" "$2" >> "$PATHS"; }
flush_paths() {
  [[ -s "$PATHS" ]] || return 0
  local files=()
  mapfile -d '' files < <(eligible_files)
  PAIRS="$PATHS" CLIENT="$C/" perl -e '
    use strict; use warnings;
    my $client = $ENV{CLIENT};
    open my $p, "<", $ENV{PAIRS} or die;
    my @pairs;
    while (<$p>) {
      chomp;
      my ($old, $new) = split /\t/;
      my @forms;
      for my $sep ("/", "\\", "\\\\") {
        (my $o = $old) =~ s{/}{$sep}g;
        (my $n = $new) =~ s{/}{$sep}g;
        push @forms, [qr/\Q$o\E(?![\w])/, $n];
      }
      my ($relOld, $relNew);
      if (index($old, $client) == 0 && index($new, $client) == 0) {
        $relOld = substr($old, length $client);
        $relNew = substr($new, length $client);
        (my $short = $client) =~ s{^src/ClankerWorld\.}{};
        for my $sep ("/", "\\", "\\\\") {
          (my $q = $short) =~ s{/}{$sep}g;
          (my $o = $relOld) =~ s{/}{$sep}g;
          (my $n = $relNew) =~ s{/}{$sep}g;
          push @forms, [qr/\Q$q$o\E(?![\w])/, "$q$n"];
        }
        push @forms, [qr{res://\Q$relOld\E(?![\w])}, "res://$relNew"];
      }
      my ($base) = $old =~ m{([^/]+)$};
      my $short = defined $relOld && $relOld =~ m{/} ? [qr{(?<![\w/.\\-])\Q$relOld\E(?![\w])}, $relNew] : undef;
      push @pairs, { old => $old, base => $base, forms => \@forms, short => $short, count => 0 };
    }
    for my $file (@ARGV) {
      open my $in, "<", $file or die "cannot read $file\n";
      local $/; my $text = <$in>; close $in;
      my $before = $text;
      for my $pair (@pairs) {
        next if index($text, $pair->{base}) < 0;
        my $hits = 0;
        for my $f (@{$pair->{forms}}) { my ($re, $n) = @$f; $hits += ($text =~ s/$re/$n/g); }
        if ($pair->{short} && $file =~ /\.md$/) { my ($re, $n) = @{$pair->{short}}; $hits += ($text =~ s/$re/$n/g); }
        $pair->{count}++ if $hits;
      }
      next if $text eq $before;
      open my $out, ">", $file or die "cannot write $file\n"; print $out $text; close $out;
    }
    for my $pair (@pairs) {
      print STDERR "client.sh: $pair->{old} was named in $pair->{count} files\n" if $pair->{count};
    }
  ' "${files[@]}"
  : > "$PATHS"
}

# move_file FROM TO: git mv with its Godot .uid sidecar (required for client
# scripts), then rewrites the old path wherever it is named.
move_file() {
  local from=$1 to=$2
  require_file "$from"
  require_absent "$to"
  mkdir -p "$(dirname "$to")"
  git mv "$from" "$to"
  if [[ -f "$from.uid" ]]; then
    require_absent "$to.uid"
    git mv "$from.uid" "$to.uid"
  elif [[ "$from" == $C/*.cs ]]; then
    die "client script has no .uid sidecar: $from"
  fi
  queue_path "$from" "$to"
  note "moved $from -> $to"
}

# remove_file FILE: git rm with its .uid sidecar.
remove_file() {
  require_file "$1"
  git rm -q "$1"
  [[ -f "$1.uid" ]] && git rm -q "$1.uid"
  note "removed $1"
}

# rename_code MAPFILE FILE...: identifier rewrite in C# code (and in comments
# for camelCase names), never in strings. Every old name must occur and no
# new name may already exist in the files given.
rename_code() {
  local map=$1; shift
  cs rewrite "$map" "$@"
}

# rename_words MAPFILE: word rewrite in every eligible text file, for class
# and file names that docs and CI scripts also name. Each old name must occur
# somewhere and no new name may occur anywhere before the rewrite.
rename_words() {
  local map=$1 files=()
  mapfile -d '' files < <(eligible_files)
  MAP="$map" perl -e '
    use strict; use warnings;
    my (%map, @order);
    open my $m, "<", $ENV{MAP} or die;
    while (<$m>) { s/#.*//; my @f = split; next unless @f; die "bad map line: $_" unless @f == 2; $map{$f[0]} = $f[1]; push @order, $f[0]; }
    my $alt = join "|", map { quotemeta } sort { length($b) <=> length($a) || $a cmp $b } keys %map;
    my $newAlt = join "|", map { quotemeta } sort values %map;
    my (%count, %content, @clash);
    for my $file (@ARGV) {
      open my $in, "<", $file or die "cannot read $file\n";
      local $/; my $text = <$in>; close $in;
      push @clash, "$1 in $file" while $text =~ /(?<![\w])($newAlt)(?![\w])/g;
      my $hits = 0;
      while ($text =~ /(?<![\w])($alt)(?![\w])/g) { $count{$1}++; $hits++; }
      $content{$file} = $text if $hits;
    }
    my @missing = grep { !$count{$_} } @order;
    die "client.sh: expected names not found: @missing\n" if @missing;
    die "client.sh: new names already used: @clash\n" if @clash;
    for my $file (sort keys %content) {
      (my $text = $content{$file}) =~ s/(?<![\w])($alt)(?![\w])/$map{$1}/g;
      open my $out, ">", $file or die; print $out $text; close $out;
    }
    printf STDERR "client.sh: renamed %d names in %d files\n", scalar(@order), scalar(keys %content);
  ' "${files[@]}"
}

# ---------------------------------------------------------------------------
# Step unused: the observation protocol of the first prototype. Its 14 types
# are compiled into the tests but used nowhere; the live protocol is
# Protocol/OwnerWorldContracts.cs and OwnerWorldObservationSession.cs.
# ---------------------------------------------------------------------------
step_unused() {
  note "step unused"
  local file=$C/Protocol/WorldObservationProtocol.cs ns other
  require_file "$file"
  ns=$(sed -n 's/^namespace \([A-Za-z.]*\);$/\1/p' "$file")
  [[ -n "$ns" ]] || die "$file has no file-scoped namespace"
  # Only code that can see its namespace could use its types.
  cs types "$file" | sort -u > "$SCRATCH/unused.types"
  while IFS= read -r other; do
    [[ "$other" == "$file" ]] && continue
    [[ -z "$(cs uses "$other" "$SCRATCH/unused.types")" ]] || die "$other uses a type from $file"
  done < <(git grep -lF -- "$ns" -- '*.cs' || true)
  remove_file "$file"
  replace_in "$TEST_PROJECT" '    <Compile Include="..\..\src\ClankerWorld.GodotClient\Protocol\WorldObservationProtocol.cs" Link="GodotClient\Protocol\WorldObservationProtocol.cs" />
' ''
  # The planner test needs a client file the tests compile; the live contracts are one.
  replace_in .github/scripts/ci-plan.test.js "src/ClankerWorld.GodotClient/Protocol/WorldObservationProtocol.cs" \
    "src/ClankerWorld.GodotClient/Protocol/OwnerWorldContracts.cs"
}

# ---------------------------------------------------------------------------
# Step namespaces: Protocol/* and Networking/OwnerWorldApi.cs declared
# ClankerWorld.GodotClient.UI. Each now declares its folder's namespace, and
# the files that used them gain the using lines they need (and lose a UI
# using they no longer need). The art preview's copy of three protocol
# records follows them.
# ---------------------------------------------------------------------------
step_namespaces() {
  note "step namespaces"
  local ui=ClankerWorld.GodotClient.UI moved=() file
  for file in $(git ls-files -- "$C/Protocol/*.cs"); do
    replace_in "$file" "namespace $ui;" "namespace ClankerWorld.GodotClient.Protocol;"
    moved+=("$file")
  done
  (( ${#moved[@]} > 0 )) || die "no protocol files found in $C/Protocol"
  replace_in "$C/Networking/OwnerWorldApi.cs" "namespace $ui;" "namespace ClankerWorld.GodotClient.Networking;"
  moved+=("$C/Networking/OwnerWorldApi.cs")
  replace_in "$ART/Shim/Contracts.cs" "namespace $ui;" "namespace ClankerWorld.GodotClient.Protocol;"
  moved+=("$ART/Shim/Contracts.cs")
  # Every type the client (and the art preview's stand-ins) declares, by namespace.
  cs types $(client_files) $(git ls-files -- "$ART/Shim/*.cs") | sort -u > "$SCRATCH/types"
  local files=()
  mapfile -t files < <(git ls-files -- "$C/*.cs" "tests/*.cs" "$ART/*.cs")
  OLD_NS=$ui MOVED="${moved[*]}" cs namespaces "$SCRATCH/types" "${files[@]}"
  if git grep -n "GodotClient\.UI\.\(OwnerWorldApi\|OwnerWorld[A-Z]\)" -- '*.cs' >/dev/null; then
    die "qualified protocol names still name $ui:
$(git grep -n "GodotClient\.UI\.\(OwnerWorldApi\|OwnerWorld[A-Z]\)" -- '*.cs')"
  fi
}

# ---------------------------------------------------------------------------
# Step panels: the client's names for panels match what the player sees.
# Only identifiers the client declares for these panels change. Names that
# say Inhabitant or Settlement, and the contract types shared with the world
# server (OwnerWorldCognition, OwnerAuthoring*, the pending-submission
# store's ForAuthoring and TryBeginPendingAuthoring), belong to the domain
# stage and stay.
# ---------------------------------------------------------------------------
step_panels() {
  note "step panels"
  cat > "$SCRATCH/panels.map" <<'EOF'
# The Agents panel (R): its frame, top-bar button, hidden selection list and
# the portrait cards the player sees. The selected agent's card beside them
# on the map is the agent quick card.
rosterPanel agentsPanel
inhabitantsButton agentsButton
rosterSummaryLabel agentsSummaryLabel
rosterBody agentsBody
inhabitantList agentList
rosterCards agentListCards
rosterCardIds agentListCardIds
rosterWantedHeight agentListWantedHeight
BuildRosterCards BuildAgentListCards
RenderRosterCards RenderAgentListCards
SelectRosterCard SelectAgentListCard
keepRosterOpen keepAgentListOpen
RefreshRosterSelection RefreshAgentListSelection
RosterPresentation AgentListPresentation
RosterPresentationFor AgentListPresentationFor
RosterRow AgentListRow
renderedRosterPresentation renderedAgentListPresentation
renderedRosterWorldId renderedAgentListWorldId
renderedRosterTheme renderedAgentListTheme
VerifyRosterKeyboardAsync VerifyAgentListKeyboardAsync
VerifyRosterPresentationChanges VerifyAgentListPresentationChanges
VerifyRosterRefreshScrollAsync VerifyAgentListRefreshScrollAsync
RosterCardText AgentListCardText
RosterAgent ListedAgent
rosterMap agentListMap
rosterPosition agentListPosition
selectedInhabitantCard agentQuickCard
# The World Map (M).
worldOverviewPanel worldMapPanel
worldOverview worldMapAtlas
# The Pause Menu (Esc).
gameMenuPanel pauseMenuPanel
gameMenuVisible pauseMenuVisible
CloseGameMenu ClosePauseMenu
CloseGameMenuAsync ClosePauseMenuAsync
ToggleGameMenuAsync TogglePauseMenuAsync
# The Event Log (E).
eventsPanel eventLogPanel
eventsButton eventLogButton
eventsBadge eventLogBadge
eventsBody eventLogBody
eventsWantedHeight eventLogWantedHeight
eventsMinimum eventLogMinimum
eventsWorldId eventLogWorldId
# The Agent model panel, in World Settings and an agent's Profile.
cognitionSettingsPanel agentModelPanel
BuildCognitionSettingsPanel BuildAgentModelPanel
cognitionTargetChoice agentModelTargetChoice
cognitionRoleChoice agentModelRoleChoice
cognitionProviderChoice agentModelProviderChoice
cognitionCredentialChoice agentModelCredentialChoice
cognitionThinkingChoice agentModelThinkingChoice
cognitionModelPicker agentModelPicker
cognitionApiKeyInput agentModelApiKeyInput
cognitionCredentialLabelInput agentModelCredentialLabelInput
cognitionConfigurationStatus agentModelConfigurationStatus
cognitionCredentialHint agentModelCredentialHint
cognitionKeyEdits agentModelKeyEdits
cognitionModelContext agentModelContext
cognitionModelLookup agentModelLookup
cognitionModelSetupCheckButton agentModelSetupCheckButton
cognitionModelSetupCheckContext agentModelSetupCheckContext
cognitionModelSetupCheckRevision agentModelSetupCheckRevision
cognitionModelSetupCheckStatus agentModelSetupCheckStatus
saveCognitionProviderButton saveAgentModelProviderButton
forgetCognitionCredentialButton forgetAgentModelCredentialButton
deleteCognitionCredentialSlotButton deleteAgentModelCredentialSlotButton
refreshCognitionProviderButton refreshAgentModelProviderButton
ClearCognitionModelSetupCheck ClearAgentModelSetupCheck
ResetCognitionModelSetupCheckForCurrentChoice ResetAgentModelSetupCheckForCurrentChoice
RunCognitionModelSetupCheckAsync RunAgentModelSetupCheckAsync
OnCognitionKeyEdited OnAgentModelKeyEdited
PopulateCognitionTargets PopulateAgentModelTargets
SelectedCognitionTarget SelectedAgentModelTarget
SyncCognitionModelPicker SyncAgentModelPicker
# The world edit form in Developer tools.
authoringBody worldEditBody
authoringKind worldEditKind
authoringId worldEditId
authoringValue worldEditValue
authoringSecondaryValue worldEditSecondaryValue
authoringX worldEditX
authoringY worldEditY
authoringRenewable worldEditRenewable
authoringHintLabel worldEditHintLabel
submitAuthoringButton submitWorldEditButton
BuildDeveloperAuthoring BuildDeveloperWorldEdit
AddAuthoringKinds AddWorldEditKinds
AddAuthoringKind AddWorldEditKind
SelectedAuthoringKind SelectedWorldEditKind
UpdateAuthoringHint UpdateWorldEditHint
SubmitAuthoringAsync SubmitWorldEditAsync
VerifyAuthoringCoordinatesAsync VerifyWorldEditCoordinatesAsync
EOF
  local files=()
  mapfile -t files < <(main_files)
  UNQUALIFIED=1 rename_code "$SCRATCH/panels.map" "${files[@]}"
  # The Agents list check's own words.
  local hud
  hud=$(git ls-files -- "$(checks_dir)/Main.HudLists*.cs")
  [[ -n "$hud" ]] || die "expected the HUD lists client check"
  # Its comments and messages say Agents list too; agent IDs such as
  # roster-keyboard-1 and the ROSTER_REFRESH_PROOF log marker stay.
  perl -pi -e 's/(?<![-\w])[Rr]oster(?![-\w])/Agents list/g' "$hud"

  # The World Map's atlas: the whole map drawn small, with a frame for the view.
  cat > "$SCRATCH/atlas.map" <<'EOF'
WorldOverview WorldMapAtlas
EOF
  mapfile -t files < <(client_files)
  rename_code "$SCRATCH/atlas.map" "${files[@]}"
  move_file "$C/UI/Components/WorldOverview.cs" "$C/UI/Components/WorldMapAtlas.cs"

  # The menu art: it paints the valley behind the Main Menu and the launcher.
  cat > "$SCRATCH/menu.map" <<'EOF'
MenuScene MenuValleyArt
MenuSceneProposed MenuValleyArtProposed
EOF
  mapfile -t files < <(git ls-files -- "$C/*.cs" "$ART/*.cs")
  rename_code "$SCRATCH/menu.map" "${files[@]}"
}

# ---------------------------------------------------------------------------
# Step partials: each partial file of Main is named for what it holds, and
# files that held several things are split by moving whole members. The
# pending-submission requests and RefreshControlAvailability (actionDisabled)
# stay in Main.OwnerActions.cs, where open bug fixes expect them.
# ---------------------------------------------------------------------------
move_members() {  # SRC DST NAME...
  local src=$1 dst=$2; shift 2
  require_file "$src"
  local created=0
  [[ -e "$dst" ]] || created=1
  cs move "$src" "$dst" "$@"
  (( created )) && git add "$dst"
  return 0
}
summary() { cs summary "$1" "$2"; }

step_partials() {
  note "step partials"
  local f
  for f in WorldDetails Lists Inhabitants AgentCard Panels MapPanels OwnerActions KeyboardMap Providers Shortcuts \
    Responsive OrderList DeveloperTools ModelLists; do
    require_file "$M/Main.$f.cs"
  done
  for f in EventLog Selection AgentList AgentQuickCard AgentProfile TextColors MapLegends KeyboardTileSelection \
    AgentModelSettings ModelCalls PairedDevices WorldEdit; do
    require_absent "$M/Main.$f.cs"
  done

  # Main.WorldDetails.cs held the Event Log and selecting an agent.
  move_file "$M/Main.WorldDetails.cs" "$M/Main.EventLog.cs"
  SUMMARY="Selecting an agent from the Agents list or the map, and clearing the selection." \
    move_members "$M/Main.EventLog.cs" "$M/Main.Selection.cs" \
    SelectInhabitantFromList SelectInhabitant ClearInhabitantSelection

  # Main.Lists.cs held the Agents list cards, the Event Log rows, the list
  # fitting the HUD panels share and the controls list's keycaps.
  move_file "$M/Main.Lists.cs" "$M/Main.AgentList.cs"
  move_members "$M/Main.AgentList.cs" "$M/Main.EventLog.cs" \
    eventRows eventScroll EventIcon BuildEventRows RenderEventRows AddNewcomerOffer
  move_members "$M/Main.AgentList.cs" "$M/Main.Shortcuts.cs" \
    ControlGroups controlsColumns renderedControlsTheme BuildControlsGroups FillControlsGroups
  move_members "$M/Main.AgentList.cs" "$M/Main.Responsive.cs" \
    agentListWantedHeight eventLogWantedHeight hudListsRefitQueued FitHudLists QueueHudListsFit ListRoom
  # Main.Inhabitants.cs drew the same list's rows; it joins the cards.
  cs merge "$M/Main.Inhabitants.cs" "$M/Main.AgentList.cs"
  git rm -q "$M/Main.Inhabitants.cs" "$M/Main.Inhabitants.cs.uid"
  summary "$M/Main.AgentList.cs" "The Agents list: portrait cards the player sees, over a hidden text list that keeps the selection, living agents first and the deceased after."
  summary "$M/Main.EventLog.cs" "The Event Log: rows with an icon per kind of event under a heading for each day, newest first, with the newcomer offer above them while the continuity rule is on."

  # Main.AgentCard.cs held both steps of the selected agent.
  move_file "$M/Main.AgentCard.cs" "$M/Main.AgentProfile.cs"
  SUMMARY="The selected agent's quick card beside them on the map: what they are doing and how they are. Rendering it also fills the Profile, which shows the same snapshot." \
    move_members "$M/Main.AgentProfile.cs" "$M/Main.AgentQuickCard.cs" \
    QuickCardWidth quickCardNameLabel quickCardActivityLabel quickCardOrderLabel quickCardMeters \
    quickFullnessMeter quickWarmthMeter quickIllnessMeter quickCardProfileButton quickCardSpeakButton \
    agentCardSnapshot BuildAgentCards BuildQuickCard '?IsModelProvider' renderedAgentCardTheme RefreshAgentCardIcons \
    CenterOnSelectedAgent SelectedInhabitant IsDeceased Sentence ThoughtTime RenderSelectedInhabitantCard
  move_members "$M/Main.AgentProfile.cs" "$M/Main.OrderList.cs" \
    PendingOrderToCancel InstructionOrderSummary OrderItemName ProgressUnitLabel OrderBuildingName
  summary "$M/Main.AgentProfile.cs" "The selected agent's Profile, docked on the left with everything the quick card leaves out, and the thoughts reader it opens. Historical profiles open straight to the Profile, because the dead have no place on the map."

  # Names for what the files hold.
  move_file "$M/Main.Panels.cs" "$M/Main.TextColors.cs"
  move_file "$M/Main.MapPanels.cs" "$M/Main.MapLegends.cs"
  move_file "$M/Main.KeyboardMap.cs" "$M/Main.KeyboardTileSelection.cs"

  # Main.OwnerActions.cs: features move to their own files; the signed
  # requests the pending-submission store guards stay, with control availability.
  SUMMARY="Paired devices in Developer tools: approve a device waiting to pair, list the paired devices and revoke one." \
    move_members "$M/Main.OwnerActions.cs" "$M/Main.PairedDevices.cs" \
    ApprovePairingAsync RevokeDeviceAsync RefreshDeviceRegistryAsync RenderPairedDevices
  move_members "$M/Main.DeveloperTools.cs" "$M/Main.PairedDevices.cs" BuildDeveloperDevices
  SUMMARY="The world edit form in Developer tools: one operation at a time on a paused world, which the world server checks before it applies it." \
    move_members "$M/Main.DeveloperTools.cs" "$M/Main.WorldEdit.cs" BuildDeveloperWorldEdit
  move_members "$M/Main.OwnerActions.cs" "$M/Main.WorldEdit.cs" \
    AddWorldEditKinds AddWorldEditKind UpdateWorldEditHint ConfigureCoordinate
  move_members "$M/Main.Layout.cs" "$M/Main.WorldEdit.cs" SelectedWorldEditKind
  move_members "$M/Main.OwnerActions.cs" "$M/Main.DeveloperTools.cs" SaveLifePaceAsync
  move_members "$M/Main.OwnerActions.cs" "$M/Main.ModelLists.cs" SaveRoutineHelperAsync
  move_members "$M/Main.OwnerActions.cs" "$M/Main.AgentProfile.cs" RenameSelectedAgentAsync
  summary "$M/Main.OwnerActions.cs" "Signed owner requests: pause, instructions, order cancellation and world edits, each guarded by the pending-submission store, and which controls are available while one is in flight."

  # Main.Providers.cs held the Agent model settings and the model-call limit.
  move_file "$M/Main.Providers.cs" "$M/Main.AgentModelSettings.cs"
  SUMMARY="The model-call limit: how many model calls the world may make, the calls made so far, and the pause when the limit is reached." \
    move_members "$M/Main.AgentModelSettings.cs" "$M/Main.ModelCalls.cs" \
    usageScopeHint usageReads usageLimitEdited RefreshUsageAsync ObserveUsagePauseAsync UsageLimitPausedMessage \
    RenderUsageStatus ConfigureUsageAsync BuildUsageLimitPanel
  summary "$M/Main.AgentModelSettings.cs" "The Agent model settings: which model and key the world, a role or one agent uses, in World Settings and in an agent's Profile."
}

# ---------------------------------------------------------------------------
# Step scene: the game scene and its script live with the rest of Main.
# ---------------------------------------------------------------------------
step_scene() {
  note "step scene"
  move_file "$C/Main.cs" "$M/Main.cs"
  move_file "$C/Main.tscn" "$M/Main.tscn"
  flush_paths
  grep -qF 'run/main_scene="res://Screens/Main/Main.tscn"' "$C/project.godot" || die "project.godot does not name the moved scene"
  grep -qF 'path="res://Screens/Main/Main.cs"' "$M/Main.tscn" || die "Main.tscn does not name the moved script"
}

# ---------------------------------------------------------------------------
# Step text: the game's wording lives in UI/Text/, apart from the theme.
# ---------------------------------------------------------------------------
step_text() {
  note "step text"
  local name
  for name in GameUiText WorldEventText LandHearingText LandTransferText NonviolentHearingText; do
    move_file "$C/UI/Theme/$name.cs" "$C/UI/Text/$name.cs"
  done
  flush_paths
  grep -qF 'UI\Text\GameUiText.cs' "$TEST_PROJECT" || die "the test project does not compile the moved text files"
}

# ---------------------------------------------------------------------------
# Step menu: the Main Menu's art in UI/Menu/. The art preview compiles the
# valley and the logo by name; UI/Graphics/*.cs stays its only glob.
# ---------------------------------------------------------------------------
step_menu() {
  note "step menu"
  move_file "$C/UI/MenuScene.cs" "$C/UI/Menu/MenuValleyArt.cs"
  move_file "$C/UI/MenuBackdrop.cs" "$C/UI/Menu/MenuBackdrop.cs"
  move_file "$C/UI/MenuLogo.cs" "$C/UI/Menu/MenuLogo.cs"
  replace_in "$ART/ArtPreview.csproj" 'Link="Game/MenuLogo.cs"' 'Link="Game/Menu/MenuLogo.cs"'
  replace_in "$ART/ArtPreview.csproj" 'Link="Game/MenuScene.cs"' 'Link="Game/Menu/MenuValleyArt.cs"'
  # MarketSprites.cs is another part of the partial class BuildingSprites.
  grep -q "partial class BuildingSprites" "$C/UI/Graphics/MarketSprites.cs" ||
    die "MarketSprites.cs is no longer part of BuildingSprites"
  move_file "$C/UI/Graphics/MarketSprites.cs" "$C/UI/Graphics/BuildingSprites.Market.cs"
}

# ---------------------------------------------------------------------------
# Step checks: the in-engine checks the game runs with --ui-smoke-test and
# the other check flags. They are partial files of Main, not xUnit tests.
# ---------------------------------------------------------------------------
step_checks() {
  note "step checks"
  local from=$C/Validation to=$C/ClientChecks file base name
  [[ -d "$from" ]] || die "expected folder is missing: $from"
  require_absent "$to"
  while IFS= read -r file; do
    base=${file##*/}
    case "$base" in
      Main.SmokeTests.cs) name=Main.ChimneySmokeChecks.cs ;;          # chimney smoke over houses
      Main.MapDrawMeasurement.cs) name=Main.MapDrawMeasurementCheck.cs ;;
      Main.*SmokeTests.cs) name=${base%SmokeTests.cs}Checks.cs ;;
      Main.*Tests.cs) name=${base%Tests.cs}Checks.cs ;;
      *) die "unexpected file in $from: $base" ;;
    esac
    move_file "$file" "$to/$name"
  done < <(git ls-files -- "$from/*.cs")
  [[ -z "$(git ls-files -- "$from")" ]] || die "files left in $from: $(git ls-files -- "$from")"
  rmdir "$from" 2>/dev/null || true
}

# ---------------------------------------------------------------------------
# Step tests: xUnit tests sit in the folder of what they test.
# ---------------------------------------------------------------------------
move_test() { move_file "$T/$1" "$T/$2"; }

# The one file that matches a name pattern under tests/ (the architecture
# stage may rename or move it first).
find_test() {
  local pattern found
  for pattern in "$@"; do
    found=$(git ls-files -- "$T/**/$pattern" "$T/$pattern")
    if [[ -n "$found" ]]; then
      [[ $(wc -l <<<"$found") == 1 ]] || die "several test files match $pattern: $found"
      echo "${found#"$T/"}"
      return
    fi
  done
  die "expected a test file named one of: $*"
}

step_tests() {
  note "step tests"
  local rel name
  # Godot client code tested from the server folder.
  move_test Server/Control/LauncherTests.cs GodotClient/Launcher/LauncherTests.cs
  rel=$(find_test LocalHostCompanionTests.cs LocalServerTests.cs)
  name=${rel##*/}
  if [[ "$rel" != GodotClient/Networking/* ]]; then move_test "$rel" "GodotClient/Networking/$name"; fi
  for name in OwnerActionBodyDeadlineTests WorldCatalogDeadlineTests WorldSettingsCompatibilityTests; do
    move_test "GodotClient/Pairing/$name.cs" "GodotClient/Networking/$name.cs"
  done
  move_test GodotClient/UI/WorldListRequestTests.cs GodotClient/ClientState/WorldListRequestTests.cs

  # Class and file names that say what they test.
  move_test GodotClient/UI/GodotOwnerWorldApiTests.cs GodotClient/Protocol/OwnerWorldObservationSessionTests.cs
  move_test GodotClient/Pairing/GodotOwnerPairingClientTests.cs GodotClient/Pairing/OwnerPairingClientTests.cs
  # "Native" meant "through the real world runtime", which these tests all use.
  local file
  while IFS= read -r file; do
    name=${file##*/}
    move_file "$file" "${file%/*}/${name#Native}"
  done < <(git ls-files -- "$T/**/Native*.cs")
  cat > "$SCRATCH/tests.map" <<'EOF'
GodotOwnerWorldApiTests OwnerWorldObservationSessionTests
GodotOwnerPairingClientTests OwnerPairingClientTests
NativeEventContentNamesTests EventContentNamesTests
NativeOrchardObservationTests OrchardObservationTests
NativeActionRefusalTests ActionRefusalTests
NativeWorldGenesisTests WorldGenesisTests
NativeForagedDietTests ForagedDietTests
# Files of the partial class GrownAgentHelperMemoryTests, which the
# derived-identity guide names by file.
NativeDerivedInventoryIdTests DerivedInventoryIdTests
NativePaidMilkDerivedIdTests PaidMilkDerivedIdTests
NativeDerivedSocietyIdTests DerivedSocietyIdTests
NativeDerivedWorkIdTests DerivedWorkIdTests
NativeDerivedCivicIdTests DerivedCivicIdTests
NativeDerivedPrivateFactoryTests DerivedPrivateFactoryTests
EOF
  rename_words "$SCRATCH/tests.map"

  # The display preference tests get their own class beside the store.
  local src=$TC/UI/GameUiTextTests.cs dst=$TC/ClientState/GameDisplayPreferencesStoreTests.cs
  require_absent "$dst"
  USINGS=$'using ClankerWorld.GodotClient.ClientState;\nusing ClankerWorld.GodotClient.UI;' \
    DECL="public sealed class GameDisplayPreferencesStoreTests" \
    cs move "$src" "$dst" GameClockPreferencePersistsOutsideWorldSave OlderSavedInterfaceAndRenderChoicesAreIgnored \
    TheInterfaceDropsASizeRatherThanSqueezeTheMenus ThePictureFollowsTheScreenInFullscreenAndTheWindowOtherwise
  git add "$dst"

  # "playtest-alpha" stays: every use is the seed of a test world.
  local ids
  ids=$(git grep -n '"playtest-alpha"' -- '*.cs' | grep -v 'new WorldRuntime("playtest-alpha"\|Assert.Equal("playtest-alpha", snapshot.WorldId)' || true)
  [[ -z "$ids" ]] || die "\"playtest-alpha\" is used other than as a world seed; decide each:
$ids"
}

# ---------------------------------------------------------------------------
# Step uids: Godot keeps a .uid beside every script. New scripts get one
# derived from their path, so a rerun writes the same text.
# ---------------------------------------------------------------------------
step_uids() {
  note "step uids"
  local file uid
  while IFS= read -r file; do
    [[ -f "$file.uid" ]] && continue
    uid=$(cs uid "$file")
    if git grep -qF -- "$uid" -- "$C/*.uid"; then die "generated uid for $file is already used: $uid"; fi
    printf '%s\n' "$uid" > "$file.uid"
    git add "$file.uid"
    note "added $file.uid"
  done < <(git ls-files -- "$C/*.cs")
  while IFS= read -r file; do
    [[ -f "${file%.uid}" ]] || die "uid without its script: $file"
  done < <(git ls-files -- "$C/*.cs.uid")
  flush_paths
  # No old path or name is left anywhere it would be read.
  local stale
  stale=$(eligible_files | xargs -0 -r grep -nE \
    'GodotClient[/\\]+(Main\.(cs|tscn)|Validation[/\\]|UI[/\\]+Theme[/\\]+[A-Za-z]*Text\.cs|UI[/\\]+Menu(Scene|Backdrop|Logo)\.cs|Protocol[/\\]+WorldObservationProtocol)|res://Main\.|WorldObservationProtocol' || true)
  [[ -z "$stale" ]] || die "old client paths are still named:
$stale"
}

for step in unused namespaces panels partials scene text menu checks tests uids; do
  if has_step "$step"; then "step_$step"; flush_paths; fi
done
note "done"
