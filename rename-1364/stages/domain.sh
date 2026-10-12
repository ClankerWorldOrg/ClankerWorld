#!/usr/bin/env bash
# Issue #1364, domain stage: the code uses the game design's words, one name
# per concept (docs/game-design/). It runs after rename.sh, arch.sh and
# client.sh, from the root of their output:
#
#   bash stages/domain.sh                       # every step, in order
#   STEPS="agent fullness" bash stages/domain.sh
#
# Steps (always run in this order):
#   agent        Inhabitant -> Agent everywhere, with its saved, wire and
#                derived-ID spellings
#   stewardship  the world-wide food-policy vote: SettlementCouncil -> FoodStewardship
#   barter       person-to-person trade: SettlementTrade -> DirectBarter
#   shop         trade at a household's shop: BusinessTrade -> ShopTrade
#   settlement   the Town-era Settlement prefix and World/Settlement/ folder
#   fullness     HungerBasisPoints -> FullnessBasisPoints (it stores fullness)
#   resident     "resident" meaning any agent -> agent
#   orders       suggestions and orders: OwnerInstructionKind, OrderTask
#   council      TownGovernanceState -> TownCouncilState
#   hearings     non-land hearings and the Town's land records
#   town         CivicSystem -> TownSystem, Town projects, tree planting,
#                item kinds, the aging override
#   docs         saves-and-replay, derived identities and how-it-works notes
#   restore      checks that nothing that should have changed is left
#
# Saved names follow the code (owner decision: old alpha saves need not load);
# rename.sh bumps the save schema once and this stage does not bump it again.
# The owner's provider-configuration file holds model keys, not a save, so it
# keeps loading: its own schema moves from 3 to 4 and older files are upgraded
# in place. Content-package IDs, map resource IDs and other digest or seed
# inputs keep their bytes.
#
# The stage only renames. It is deterministic, finds files by pattern rather
# than fixed path where earlier stages may have moved them, and fails loudly
# when an expected name is missing or a new name is already taken. It does not
# commit.
set -euo pipefail

die() { echo "domain.sh: error: $*" >&2; exit 1; }
note() { echo "domain.sh: $*" >&2; }

ROOT=$(git rev-parse --show-toplevel 2>/dev/null) || die "run inside a git checkout"
cd "$ROOT"
[[ -f ClankerWorld.sln ]] || die "not the ClankerWorld repository root: $ROOT"
dirty=$(git status --porcelain --untracked-files=no -- . ':(exclude)global.json')
[[ -z "$dirty" ]] || die "working tree has changes; start from a clean checkout:
$dirty"

STEPS=${STEPS:-"agent stewardship barter shop settlement fullness resident orders council hearings town docs restore"}
has_step() { [[ " $STEPS " == *" $1 "* ]]; }

SIM=src/ClankerWorld.Simulation
SERVER=src/ClankerWorld.Server
CLIENT=src/ClankerWorld.GodotClient
TESTS=tests/ClankerWorld.Tests
[[ -d "$SERVER" && -d "$TESTS" ]] || die "run after rename.sh: $SERVER and $TESTS must exist"
SCRATCH=$(mktemp -d)
trap 'rm -rf "$SCRATCH"' EXIT
HERE=$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")
CODE_WORDS=""
for candidate in "$HERE/cs-code-words.pl" "$HERE/../cs-code-words.pl"; do
  [[ -f "$candidate" ]] && { CODE_WORDS=$candidate; break; }
done
[[ -n "$CODE_WORDS" ]] || die "cs-code-words.pl must sit next to rename.sh, one folder up"

# ---------------------------------------------------------------------------
# The C# text rewriter: like cs-code-words.pl, but for the text of string
# literals or of comments only. Interpolation holes are code and stay.
# ---------------------------------------------------------------------------
CS_TEXT=$SCRATCH/cs-text.pl
cat > "$CS_TEXT" <<'PERL'
use strict; use warnings;
my ($mode, $arg, @files) = @ARGV;
my ($pattern, $sub, $file, $text, $changed);
our $FILE = "";
if ($mode =~ /^list-/) { $pattern = qr/$arg/; }
elsif ($mode eq 'strings' || $mode eq 'comments' || $mode eq 'code') {
  open my $p, '<', $arg or die "cannot read $arg\n";
  local $/; my $code = <$p>; close $p;
  $sub = eval "sub { $code }" or die "bad substitution file $arg: $@\n";
} else { die "unknown mode $mode\n"; }
my $want = $mode =~ /strings/ ? 'string' : $mode =~ /code/ ? 'code' : 'comment';
sub line_at { my ($pos) = @_; return 1 + (substr($text, 0, $pos) =~ tr/\n//); }
sub segment {
  my ($kind, $s, $pos) = @_;
  return $s unless $kind eq $want;
  if ($mode =~ /^list-/) { print "$file:" . line_at($pos) . ": $s\n" if $s =~ $pattern; return $s; }
  local $_ = $s;
  $FILE = $file;
  $sub->();
  $changed++ if $_ ne $s;
  return $_;
}
sub interpolated {
  my ($verbatim) = @_;
  my ($out, $buf, $start) = ('', '', pos($text));
  my $flush = sub { $out .= segment('string', $buf, $start) if length $buf; $buf = ''; $start = pos($text); };
  while (pos($text) < length $text) {
    if ($text =~ /\G(\{\{|\}\})/gc) { $buf .= $1; next; }
    if ($verbatim ? $text =~ /\G("")/gc : $text =~ /\G(\\.)/gcs) { $buf .= $1; next; }
    if ($text =~ /\G"/gc) { $flush->(); return $out . '"'; }
    if ($text =~ /\G\{/gc) { $flush->(); $out .= '{' . code(1) . '}'; $start = pos($text); next; }
    $text =~ /\G(.)/gcs; $buf .= $1;
  }
  $flush->();
  return $out;
}
sub code {
  my ($hole) = @_;
  my ($out, $depth, $buf, $bufat) = ('', 0, '', pos($text));
  my $flush = sub { $out .= segment('code', $buf, $bufat) if length $buf; $buf = ''; $bufat = pos($text); };
  while (pos($text) < length $text) {
    my $at = pos($text);
    if ($hole) {
      if ($text =~ /\G\}/gc) { if ($depth == 0) { $flush->(); return $out; } $depth--; $buf .= '}'; next; }
      if ($text =~ /\G\{/gc) { $depth++; $buf .= '{'; next; }
    }
    if ($text =~ /\G\/\/([^\n]*)/gc) { $flush->(); $out .= '//' . segment('comment', $1, $at); $bufat = pos($text); next; }
    if ($text =~ /\G\/\*(.*?)\*\//gcs) { $flush->(); $out .= '/*' . segment('comment', $1, $at) . '*/'; $bufat = pos($text); next; }
    if ($text =~ /\G(\$*)("""+)(.*?)\2/gcs) { $flush->(); $out .= $1 . $2 . segment('string', $3, $at) . $2; $bufat = pos($text); next; }
    if ($text =~ /\G((?:\$@|@\$)")/gc) { $flush->(); $out .= $1 . interpolated(1); $bufat = pos($text); next; }
    if ($text =~ /\G@"((?:[^"]|"")*)"/gc) { $flush->(); $out .= '@"' . segment('string', $1, $at) . '"'; $bufat = pos($text); next; }
    if ($text =~ /\G\$"/gc) { $flush->(); $out .= '$"' . interpolated(0); $bufat = pos($text); next; }
    if ($text =~ /\G"((?:[^"\\\n]|\\.)*)"/gc) { $flush->(); $out .= '"' . segment('string', $1, $at) . '"'; $bufat = pos($text); next; }
    if ($text =~ /\G('(?:[^'\\\n]|\\.)+')/gc) { $buf .= $1; next; }
    if ($text =~ /\G(@?[A-Za-z_]\w*)/gc) { $buf .= $1; next; }
    if ($text =~ /\G(\d[\w.]*)/gc) { $buf .= $1; next; }
    $text =~ /\G(.)/gcs; $buf .= $1;
  }
  $flush->();
  return $out;
}
for my $f (@files) {
  $file = $f;
  open my $in, '<', $file or die "cannot read $file\n";
  local $/; $text = <$in>; close $in;
  $changed = 0; pos($text) = 0;
  my $out = code(0);
  next if $mode =~ /^list-/;
  if ($changed) { open my $o, '>', $file or die "cannot write $file\n"; print $o $out; close $o; }
  elsif ($out ne $text) { die "lexer lost text in $file\n"; }
}
PERL

# ---------------------------------------------------------------------------
# File selection
# ---------------------------------------------------------------------------

# History and evidence that keep their original wording.
is_protected() {
  case "$1" in
    CHANGELOG.md|changes/*|docs/archive/*) return 0 ;;
    *.cs.txt) return 0 ;;
    docs/development/*-probe/*|docs/development/*-equivalence/*) return 0 ;;
    docs/development/*-measurements.md|docs/development/*-comparison.md) return 0 ;;
    */packages.lock.json) return 0 ;;
    playtest/*) return 0 ;;                 # playtest records quote the game's text
  esac
  return 1
}

eligible_files() {
  git ls-files -z -- '*.cs' '*.csproj' '*.props' '*.targets' '*.sln' \
    '*.sh' '*.js' '*.mjs' '*.ps1' '*.py' '*.json' '*.yml' '*.yaml' '*.md' \
    '*.tscn' '*.tres' '*.godot' '*.cfg' '*.txt' '*.service' '*.html' '*.css' '*.iss' |
  while IFS= read -r -d '' file; do
    [[ -f "$file" ]] || continue
    is_protected "$file" || printf '%s\0' "$file"
  done
}
eligible_cs_files() { eligible_files | while IFS= read -r -d '' f; do [[ "$f" == *.cs ]] && printf '%s\0' "$f"; done; }
eligible_text_files() { eligible_files | while IFS= read -r -d '' f; do [[ "$f" != *.cs ]] && printf '%s\0' "$f"; done; }
files_matching() { eligible_files | xargs -0 -r grep -lZP -- "$1" || true; }

# code_names PATTERN [HINT]: code identifiers (not strings or comments) that
# match a Perl pattern, in the C# files that contain HINT (any case).
code_names() {
  local cs=()
  if [[ -n "${2:-}" ]]; then
    mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZiF -- "$2" || true)
  else
    mapfile -d '' cs < <(eligible_cs_files)
  fi
  (( ${#cs[@]} == 0 )) || perl "$CODE_WORDS" list "$1" "${cs[@]}"
}

# rename_map MAPFILE LABEL: word-boundary rewrite of every "OLD NEW" line in
# all eligible files: code, strings, comments and docs. Each OLD must occur
# unless its line starts with "?"; no NEW may occur before. Lines starting
# with "!" skip the clash check for that NEW (an existing name in another
# scope, checked by the build). With RENAME_FILES=1, tracked files whose
# names hold an OLD word move to the rewritten name, .uid sidecars included.
rename_map() {
  local map=$1 label=$2
  eligible_files > "$SCRATCH/files"
  MAP="$map" LABEL="$label" RENAME_FILES="${RENAME_FILES:-}" FILES="$SCRATCH/files" perl -e '
    use strict; use warnings;
    my (%map, %optional, %noclash, @order);
    open my $m, "<", $ENV{MAP} or die "cannot read $ENV{MAP}\n";
    while (<$m>) {
      chomp; s/#.*//; next unless /\S/;
      my $flags = s/^\s*([?!]+)\s*// ? $1 : "";
      my @f = split " ";
      die "bad map line in $ENV{LABEL}: $_\n" if @f != 2;
      my ($old, $new) = @f;
      die "duplicate map entry in $ENV{LABEL}: $old\n" if exists $map{$old};
      $map{$old} = $new; push @order, $old;
      $optional{$old} = 1 if $flags =~ /\?/;
      $noclash{$new} = 1 if $flags =~ /!/;
    }
    my @olds = sort { length($b) <=> length($a) || $a cmp $b } keys %map;
    my $re = do { my $a = join "|", map { quotemeta } @olds; qr/(?<![\w])($a)(?![\w])/ };
    my %isold = map { $_ => 1 } @olds;
    my %seen;
    my @news = grep { !$isold{$_} && !$noclash{$_} && !$seen{$_}++ } values %map;
    my $newre = @news ? do { my $n = join "|", map { quotemeta } sort { length($b) <=> length($a) } @news; qr/(?<![\w])($n)(?![\w])/ } : undef;
    local $/ = "\0";
    open my $list, "<", $ENV{FILES} or die;
    my @files = map { chomp; $_ } <$list>;
    close $list;
    local $/;
    my (%count, %clash, %content);
    for my $file (@files) {
      open my $in, "<", $file or die "cannot read $file\n";
      my $text = <$in> // ""; close $in;
      if ($newre) { while ($text =~ /$newre/g) { $clash{$1}{$file} = 1; } }
      my $hits = 0;
      while ($text =~ /$re/g) { $count{$1}++; $hits++; }
      $content{$file} = $text if $hits;
    }
    my @missing = grep { !$count{$_} && !$optional{$_} } @order;
    die "$ENV{LABEL}: expected names not found: @missing\n" if @missing;
    if (%clash) {
      die "$ENV{LABEL}: new names already used:\n" . join("\n", map { "  $_ in " . join(", ", sort keys %{$clash{$_}}) } sort keys %clash) . "\n";
    }
    for my $file (sort keys %content) {
      (my $text = $content{$file}) =~ s/$re/$map{$1}/g;
      open my $out, ">", $file or die "cannot write $file\n"; print $out $text; close $out;
    }
    printf STDERR "domain.sh: %s: %d names in %d files\n", $ENV{LABEL}, scalar(grep { $count{$_} } @order), scalar(keys %content);
    if ($ENV{RENAME_FILES}) {
      open my $all, "-|", "git", "ls-files", "-z" or die;
      local $/ = "\0";
      my @tracked = map { chomp; $_ } <$all>;
      close $all;
      for my $path (@tracked) {
        next if $path =~ m{^(?:CHANGELOG\.md|changes/|docs/archive/|playtest/)} || $path =~ /\.cs\.txt$/ ||
          $path =~ m{^docs/development/[^/]*-(?:probe|equivalence)/};
        my ($dir, $base) = $path =~ m{^(.*/)?([^/]+)$};
        $dir //= "";
        next unless -e $path;
        (my $renamed = $base) =~ s/$re/$map{$1}/g;
        next if $renamed eq $base;
        die "target already exists: $dir$renamed\n" if -e "$dir$renamed";
        system("git", "mv", $path, "$dir$renamed") == 0 or die "git mv $path failed\n";
        print STDERR "domain.sh: moved $path -> $dir$renamed\n";
      }
    }
  '
}

# rewrite_regex PERL_REGEX REPLACEMENT [PATHSPEC...]: scoped rewrite over
# eligible files that must change at least one file. REPLACEMENT may use $1..$3.
rewrite_regex() {
  local pattern=$1 replacement=$2 files=()
  mapfile -d '' files < <(files_matching "$pattern")
  (( ${#files[@]} > 0 )) || die "expected text /$pattern/ was not found"
  PAT="$pattern" REP="$replacement" perl -pi -e '
    BEGIN { $pat = qr/$ENV{PAT}/; $rep = $ENV{REP}; }
    s{$pat}{ my @m = ($1, $2, $3); my $r = $rep; $r =~ s/\$(\d)/defined $m[$1 - 1] ? $m[$1 - 1] : ""/ge; $r }ge;
  ' "${files[@]}"
  note "rewrote /$pattern/ in ${#files[@]} files"
}
rewrite_regex_optional() {
  if [[ -n "$(files_matching "$1" | head -c 1)" ]]; then rewrite_regex "$@"; else note "skipped /$1/: not found"; fi
}

# replace_in FILE OLD NEW: literal rewrite in one file that must match.
replace_in() {
  local file=$1
  [[ -f "$file" ]] || die "expected file is missing: $file"
  OLD="$2" NEW="$3" perl -0pi -e '$n += s/\Q$ENV{OLD}\E/$ENV{NEW}/g; END { exit($n ? 0 : 3) }' "$file" ||
    die "expected text not found in $file: $2"
}
replace_in_optional() {
  if [[ -f "$1" ]] && grep -qF -- "$2" "$1"; then replace_in "$@"; else note "skipped in ${1}: '$2' not found"; fi
}

require_file() { [[ -f "$1" ]] || die "expected file is missing: $1"; }
require_absent() { [[ ! -e "$1" ]] || die "target already exists: $1"; }

# one_file PATTERN: the single tracked file whose path matches a Perl pattern.
one_file() {
  local found=()
  mapfile -t found < <(git ls-files | grep -P -- "$1" || true)
  (( ${#found[@]} == 1 )) || die "expected one file matching /$1/, found ${#found[@]}: ${found[*]:-none}"
  echo "${found[0]}"
}

# move_file FROM TO: git mv of a tracked file and its .uid sidecar. Repository
# paths that name it (docs links, label rules, architecture lists) follow.
move_file() {
  local from=$1 to=$2
  [[ -f "$from" ]] || die "expected file is missing: $from"
  require_absent "$to"
  mkdir -p "$(dirname "$to")"
  git mv "$from" "$to"
  if [[ -f "$from.uid" ]]; then git mv "$from.uid" "$to.uid"; fi
  local files=()
  mapfile -d '' files < <(eligible_files | xargs -0 -r grep -lZF -- "$from" || true)
  (( ${#files[@]} == 0 )) || FROM="$from" TO="$to" perl -pi -e 's/\Q$ENV{FROM}\E/$ENV{TO}/g' "${files[@]}"
}

# Code-only, string-only and comment-only rewrites of C# files.
code_rewrite() { local map=$1; shift; (( $# == 0 )) || perl "$CODE_WORDS" rewrite "$map" "$@"; }
string_rewrite() { local sub=$1; shift; (( $# == 0 )) || perl "$CS_TEXT" strings "$sub" "$@"; }
comment_rewrite() { local sub=$1; shift; (( $# == 0 )) || perl "$CS_TEXT" comments "$sub" "$@"; }
# code_regex PERLFILE FILE...: Perl substitutions on code text only, between
# literals and comments (an identifier map cannot see "." or ":" around it).
code_regex() { local sub=$1; shift; (( $# == 0 )) || perl "$CS_TEXT" code "$sub" "$@"; }
list_strings() { local pattern=$1; shift; (( $# == 0 )) || perl "$CS_TEXT" list-strings "$pattern" "$@"; }

# check_new_names MAPFILE: no NEW code name of the map may exist yet, apart
# from names listed in ALLOWED (space separated): those exist in other scopes
# and the build checks them.
check_new_names() {
  local map=$1 existing cs=()
  awk '{ sub(/#.*/, ""); if (NF == 2) print $2 }' "$map" | sort -u > "$SCRATCH/new.names"
  mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZwF -f "$SCRATCH/new.names" || true)
  existing=$( (( ${#cs[@]} == 0 )) || perl "$CODE_WORDS" list '.' "${cs[@]}" | sort -u | comm -12 "$SCRATCH/new.names" - || true)
  local name bad=()
  for name in $existing; do
    [[ " ${ALLOWED:-} " == *" $name "* ]] || bad+=("$name")
  done
  (( ${#bad[@]} == 0 )) || die "new code names already exist: ${bad[*]}"
}

# sort_json FILE: rewrites a JSON object of string arrays with sorted keys and
# values, two-space indent, the format of the architecture baselines.
sort_json() {
  perl -MJSON::PP -0e '
    my $file = shift; open my $in, "<", $file or die; local $/; my $data = decode_json(<$in>); close $in;
    for my $k (keys %$data) { $data->{$k} = [ sort { $a cmp $b } @{$data->{$k}} ] if ref $data->{$k} eq "ARRAY"; }
    my $json = JSON::PP->new->canonical->pretty->indent_length(2)->space_before(0)->encode($data);
    open my $out, ">", $file or die; print $out $json; close $out;
  ' "$1"
}

# ---------------------------------------------------------------------------
# Step agent: the design calls every person an agent. "Inhabitant" becomes
# "Agent" in code, strings, comments, docs and file names, keeping case:
# types (InhabitantState -> AgentState, SocietyInhabitant -> SocietyAgent),
# members (InhabitantId -> AgentId), saved JSON names ("inhabitants",
# "inhabitantId"), event kinds (inhabitant_moved -> agent_moved), the
# signed request fields (target-inhabitant-id=, inhabitant=), handshake
# capabilities and the generated agent ID ({worldId}:inhabitant:{request} ->
# {worldId}:agent:{request}, both producer and the tests that spell it).
#
# Kept: the "inhabitant=" lines of signed payloads (their bytes are signed;
# arch.sh already made the log fields "agent="), text that names the old word
# on purpose (the model-wording test,
# tests that check the old field stays out of model requests, design and
# playtest notes that ban the word), and an older provider-configuration file
# in a test, which must keep loading.
# ---------------------------------------------------------------------------
step_agent() {
  note "step agent"
  code_names '(?i)inhabitant' inhabitant > "$SCRATCH/agent.names"
  [[ -s "$SCRATCH/agent.names" ]] || die "no Inhabitant names found in code"
  perl -ne 'chomp; (my $n = $_) =~ s/INHABITANT/AGENT/g; $n =~ s/Inhabitant/Agent/g; $n =~ s/inhabitant/agent/g; print "$_ $n\n"' \
    "$SCRATCH/agent.names" > "$SCRATCH/agent.map"
  # rename.sh names an agent's recent private thought RecentThought, like its
  # RecentThoughts field; a base that still says InhabitantThought gets that name.
  perl -pi -e 's/^InhabitantThought \S+$/InhabitantThought RecentThought/' "$SCRATCH/agent.map"
  # These names already exist on other types or in other scopes, where the
  # renamed one cannot be confused with them (checked by the build):
  # WorldRuntime.AddAgent/RenameAgent beside the adapters' AddInhabitant and
  # SocietyRules.RenameInhabitant; the many AgentId members of records with no
  # InhabitantId; OwnerInstructionOrder.TargetAgentId (the task's other agent)
  # beside the instruction's TargetInhabitantId (the agent it is for).
  # RecentThought is also a string member of the model's self view.
  ALLOWED="Agent Agents AgentId AddAgent RenameAgent TargetAgentId agent agentId agents knownAgents RecentThought" \
    check_new_names "$SCRATCH/agent.map"

  local cs=() text=()
  mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZi -- inhabitant || true)
  code_rewrite "$SCRATCH/agent.map" "${cs[@]}"
  cat > "$SCRATCH/agent-strings.pl" <<'PERL'
return if $FILE =~ m{/ModelFacingWordingTests\.cs$};      # the banned-word list
return if /"SchemaVersion":[0-3],/;                        # an older provider-configuration file
return if $FILE =~ m{^tests/} && $_ eq 'inhabitant_id';    # the old field must stay out of model requests
s/inhabitant=/\x{1}KEEP\x{1}/g; s/InhabitantThought/RecentThought/g; s/INHABITANT/AGENT/g; s/Inhabitant/Agent/g; s/inhabitant/agent/g; s/\x{1}KEEP\x{1}/inhabitant=/g;
PERL
  string_rewrite "$SCRATCH/agent-strings.pl" "${cs[@]}"
  echo 's/inhabitant=/\x{1}KEEP\x{1}/g; s/InhabitantThought/RecentThought/g; s/INHABITANT/AGENT/g; s/Inhabitant/Agent/g; s/inhabitant/agent/g; s/\x{1}KEEP\x{1}/inhabitant=/g;' > "$SCRATCH/agent-comments.pl"
  comment_rewrite "$SCRATCH/agent-comments.pl" "${cs[@]}"
  mapfile -d '' text < <(eligible_text_files | xargs -0 -r grep -lZi -- inhabitant || true)
  # Lines that name the old word itself keep it.
  (( ${#text[@]} == 0 )) || perl -pi -e '
    next if /\*inhabitant\*|`Inhabitant cognition`|"inhabitant"/;
    s/inhabitant=/\x{1}KEEP\x{1}/g; s/InhabitantThought/RecentThought/g; s/INHABITANT/AGENT/g; s/Inhabitant/Agent/g; s/inhabitant/agent/g; s/\x{1}KEEP\x{1}/inhabitant=/g;
  ' "${text[@]}"
  note "renamed Inhabitant to Agent in code, strings, comments and ${#text[@]} other files"

  # File names. The client's Main.* partial files belong to client.sh.
  local path target
  while IFS= read -r path; do
    is_protected "$path" && continue
    [[ "$path" == "$CLIENT"/*/Main.* || "$path" == "$CLIENT"/Main.* ]] && continue
    [[ "$path" == *.uid ]] && continue
    target=$(perl -pe 's/INHABITANT/AGENT/g; s/Inhabitant/Agent/g; s/inhabitant/agent/g' <<< "$path")
    move_file "$path" "$target"
    note "moved $path -> $target"
  done < <(git ls-files | grep -i inhabitant || true)

  # The provider-configuration file keeps the player's model keys, so a file
  # from an earlier build must keep loading. Its schema 4 names each
  # assignment's agent AgentId; schema 2 and 3 files are upgraded in place.
  local store protocol
  # Found by content: arch.sh may rename the provider-configuration types.
  store=$(git grep -lP '\w+CredentialFile\.Decode\(stored\)' -- "$SERVER/*.cs")
  [[ $(wc -w <<< "$store") == 1 ]] || die "expected one provider-configuration store, found: ${store:-none}"
  grep -q 'public const int StateSchemaVersion = 3;' "$store" ||
    die "$store is no longer at provider-configuration schema 3; combine this upgrade with whatever changed it"
  protocol=$(one_file "^$SERVER/.*/OwnerHttpProtocol\\.cs\$")
  perl -0ne 'exit(/record \w*Assignment\(\s*string AgentId,\s*string Role,/ ? 0 : 1)' "$protocol" ||
    die "the model assignment's first member is no longer AgentId; update step_agent"
  replace_in "$store" 'public const int StateSchemaVersion = 3;' 'public const int StateSchemaVersion = 4;'
  perl -0pi -e 's/^(            var json = )(\w+CredentialFile\.Decode\(stored\));$/$1UpgradeAssignmentAgentIds($2);/m or die "the stored-file decode was not found\n"' "$store"
  replace_in "$store" $'            if (loaded.SchemaVersion == 2)\n            {\n                loaded = loaded with { SchemaVersion = StateSchemaVersion, CredentialSlots = [] };\n' \
$'            if (loaded.SchemaVersion == 2)\n                loaded = loaded with { SchemaVersion = 3, CredentialSlots = [] };\n            if (loaded.SchemaVersion == 3)\n            {\n                loaded = loaded with { SchemaVersion = StateSchemaVersion };\n'
  HELPER=$'    // Schema 4 names the agent of each assignment AgentId; schema 2 and 3 files\n    // name it InhabitantId. The file holds the player\'s model keys, so an older\n    // file is upgraded rather than refused.\n    private static string UpgradeAssignmentAgentIds(string json)\n    {\n        if (JsonNode.Parse(json) is not JsonObject root || root["SchemaVersion"] is not JsonValue version ||\n            !version.TryGetValue<int>(out var schema) || schema is not (2 or 3) || root["Assignments"] is not JsonArray assignments)\n            return json;\n        foreach (var assignment in assignments.OfType<JsonObject>())\n        {\n            if (!assignment.TryGetPropertyValue("InhabitantId", out var agentId)) continue;\n            assignment.Remove("InhabitantId");\n            assignment["AgentId"] = agentId;\n        }\n        return root.ToJsonString();\n    }\n\n' \
    perl -0pi -e 's/^(    private static \w+ CreateInitial\(\w+ seed\)\n)/$ENV{HELPER}$1/m or die "CreateInitial was not found\n"' "$store"
  grep -q '^using System.Text.Json.Nodes;$' "$store" || die "$store lost its System.Text.Json.Nodes using"
  note "provider-configuration schema 3 -> 4, upgrading older files"

  # The architecture baseline is sorted by component and path.
  sort_json "$(one_file "^$TESTS/Architecture/agent-writers\\.json\$")"

  local left
  left=$(eligible_files | xargs -0 -r grep -niP 'inhabitant' |
    grep -vP 'ModelFacingWordingTests\.cs:|"SchemaVersion":[0-3],|"inhabitant_id"|\*inhabitant\*|`Inhabitant cognition`|"inhabitant"|inhabitant=' |
    grep -vP '^src/[^:]+:\d+:.*(?:"InhabitantId"|name it InhabitantId\.)' || true)   # the key-file upgrade
  [[ -z "$left" ]] || die "Inhabitant names left:
$left"
}

# ---------------------------------------------------------------------------
# Step stewardship: the world-wide food-policy vote (a steward and a ballot on
# open or essential-first shared food) is not a Town council, which keeps the
# name "council". Its types, the CivicState and saved checkpoint member
# (council -> foodStewardship), the snapshot member, its events, its log and
# the model's candidate IDs say food stewardship or food policy.
# ---------------------------------------------------------------------------
step_stewardship() {
  note "step stewardship"
  cat > "$SCRATCH/stewardship.map" <<'MAP'
SettlementCouncil FoodStewardship
SettlementBallot FoodPolicyBallot
AdvanceSettlementCouncil AdvanceFoodStewardship
LogSettlementCouncil LogFoodStewardship
CouncilView FoodStewardshipView
OwnerWorldCouncil OwnerWorldFoodStewardship
CouncilMembers FoodPolicyVoters
HasCouncilDecision HasFoodPolicyDecision
ResolveCouncilBallot ResolveFoodPolicyBallot
AddCouncilCandidates AddFoodPolicyCandidates
ApplyCouncilCandidate ApplyFoodPolicyCandidate
ValidateCouncil ValidateFoodStewardship
RestoreCouncil RestoreFoodStewardship
SettlementCouncilTests FoodStewardshipTests
CouncilBallotExpiryTests FoodPolicyBallotExpiryTests
? RegisterCouncilActions RegisterFoodPolicyActions
? RegisterCouncilChoices RegisterFoodPolicyChoices
settlement_council food_stewardship
council_propose food_policy_propose
council_vote_yes food_policy_vote_yes
council_vote_no food_policy_vote_no
council_steward_changed food_steward_changed
council_policy_adopted food_policy_adopted
council_policy_rejected food_policy_rejected
council_policy_proposed food_policy_proposed
council_vote_recorded food_policy_vote_recorded
MAP
  rename_map "$SCRATCH/stewardship.map" stewardship
  # Their shared prefix: the action registration, the client's verb text and
  # its conversation poses. "council:" (a Town council round) is another prefix.
  rewrite_regex '"council_"' '"food_policy_"'
  rewrite_regex_optional '\|council_\|council_\|' '|food_policy_|food_policy_|'   # the registration-order test
  # The proposal parser cut "council_propose:" by its length.
  local proposals
  proposals=$(git grep -lF '"food_policy_propose:"' -- "$SIM/*.cs" | xargs grep -l 'candidate\[16\.\.\]' || true)
  [[ $(wc -w <<< "$proposals") == 1 ]] || die "expected one food-policy proposal parser, found: ${proposals:-none}"
  replace_in "$proposals" 'var policy = candidate[16..];' 'var policy = candidate["food_policy_propose:".Length..];'

  # The member that holds it, only where it is the food vote: the Town's
  # council (Governance, and "council" locals beside it) keeps its name.
  echo "Council FoodStewardship" > "$SCRATCH/stewardship-member.map"
  echo "council foodStewardship" >> "$SCRATCH/stewardship-member.map"
  local files=() file page
  page=$(git grep -lP '\bsnapshot\.Council;' -- "$CLIENT/*.cs")
  [[ $(wc -w <<< "$page") == 1 ]] || die "expected one client file reading snapshot.Council, found: ${page:-none}"
  mapfile -t files < <(git ls-files -- "$SIM/World/Civic/CivicState.cs" "$SIM/World/Civic/CivicSystem.cs" \
    "$SIM/World/Civic/CivicSystem.Council.cs" "$SIM/*/SettlementCouncil.cs" \
    "$TESTS/*/CivicSystemTests.cs" "$TESTS/*/CouncilBallotExpiryTests.cs" "$TESTS/*/SettlementCouncilTests.cs"; echo "$page")
  (( ${#files[@]} == 8 )) || die "expected the eight food-stewardship files, found ${#files[@]}: ${files[*]}"
  for file in "${files[@]}"; do
    grep -qwi council "$file" || die "no council member left in $file"
  done
  code_rewrite "$SCRATCH/stewardship-member.map" "${files[@]}"
  local state runtime views store contracts
  state=$(one_file "^$SIM/World/Runtime/WorldRuntimeState\\.cs\$")
  runtime=$(one_file "^$SIM/World/Runtime/WorldRuntime\\.cs\$")
  views=$(one_file "^$SERVER/.*/ObservationContracts\\.cs\$")
  store=$(one_file "^$SERVER/.*/OwnerWorldObservationStore\\.cs\$")
  contracts=$(one_file "^$CLIENT/.*/OwnerWorldContracts\\.cs\$")
  replace_in "$state" 'FoodStewardship? Council = null' 'FoodStewardship? FoodStewardship = null'
  replace_in "$runtime" 'runtime.council = state.Council;' 'runtime.foodStewardship = state.FoodStewardship;'
  rewrite_regex '(survivalState, )council,' '$1foodStewardship,'
  replace_in "$views" 'public FoodStewardshipView? Council { get; init; }' 'public FoodStewardshipView? FoodStewardship { get; init; }'
  replace_in "$contracts" 'public OwnerWorldFoodStewardship? Council { get; init; }' 'public OwnerWorldFoodStewardship? FoodStewardship { get; init; }'
  STORE="$store" perl -0pi -e '
    s{Council = state\.Council is \{ \} council \? new FoodStewardshipView\((.*?)\) : null,}{
      my $body = $1; $body =~ s/\bcouncil\./stewardship./g;
      "FoodStewardship = state.FoodStewardship is { } stewardship ? new FoodStewardshipView($body) : null,"
    }se or die "the food-stewardship view projection was not found\n";
  ' "$store"
  # Tests that set or read it on a checkpoint or snapshot.
  rewrite_regex '\b(Council)( = new\((?:null|actor|state\.Agents\[0\]\.AgentId), (?:policy|"open"|"essential_first"))' 'FoodStewardship$2'
  rewrite_regex '\bsnapshot\.Council\?\.StewardName' 'snapshot.FoodStewardship?.StewardName'
  move_file "$SIM/World/Civic/CivicSystem.Council.cs" "$SIM/World/Civic/CivicSystem.FoodStewardship.cs"
}

# ---------------------------------------------------------------------------
# Step barter: two agents trading goods face to face. The kernel's offer is
# already a DirectBarterOffer; the world's side says direct barter too, with
# barter_* events and direct-barter: offer IDs.
# ---------------------------------------------------------------------------
step_barter() {
  note "step barter"
  cat > "$SCRATCH/barter.map" <<'MAP'
SettlementTradePrefix DirectBarterPrefix
SettlementTradeOfferId DirectBarterOfferId
MaintainSettlementTrades MaintainDirectBarters
LogSettlementTrade LogDirectBarter
SettlementTradeTests DirectBarterTests
SettlementTradeCannotOfferContainerContentsAsLooseLots DirectBarterCannotOfferContainerContentsAsLooseLots
! settlement_trade_offered barter_offered        # the kernel's inventory event of the same offer
! settlement_trade_declined barter_declined
! settlement_trade_completed barter_completed    # also the trust reason for a completed barter
settlement_trade_cancelled barter_cancelled
settlement_trade barter_activity                # the log line, like the other *_activity logs
MAP
  rename_map "$SCRATCH/barter.map" barter
  rewrite_regex '"settlement-trade:' '"direct-barter:'
  rewrite_regex_optional 'Meet the other trader at the settlement and wait' 'Meet the other trader and wait'
}

# ---------------------------------------------------------------------------
# Step shop: buying at a household's shop building (Store, Farmhouse,
# Blacksmith, Restaurant, Clinic) is a shop trade. The content package of
# the Store buildings is StoreContent; its package ID keeps its bytes.
# ---------------------------------------------------------------------------
step_shop() {
  note "step shop"
  code_names 'Business' business | grep -vx 'AFailedSuccessorElectionKeepsTheLeadersCouncilAndPendingBusiness' |
    perl -ne 'chomp; my $n = $_;
      $n = "StoreContent" if $n eq "BusinessContent";
      $n = "ShopItemKind" if $n eq "BusinessResourceKind";
      $n =~ s/Business/Shop/g; $n =~ s/business/shop/g; print "$_ $n\n"' > "$SCRATCH/shop.map"
  grep -q '^BusinessTradeState ShopTradeState$' "$SCRATCH/shop.map" || die "BusinessTradeState was not found"
  # ShopRules.MaySell's parameter; a test's own shopKind parameter is another scope.
  perl -pi -e 's/^(businessKind shopKind)$/! $1/' "$SCRATCH/shop.map"
  cat >> "$SCRATCH/shop.map" <<'MAP'
business_shop shop_buy
business_continue shop_continue
business_cancel shop_cancel
business_stock_store shop_stock_store
business_trade_offered shop_trade_offered
business_trade_completed shop_trade_completed
business_trade_cancelled shop_trade_cancelled
business_trade shop_trade
business_content_staged store_content_staged
? NonFoodBusinessOrderSurvivalTests NonFoodShopOrderSurvivalTests
? BusinessTradeResourceIdentityTests ShopTradeResourceIdentityTests
MAP
  rename_map "$SCRATCH/shop.map" shop
  # The candidate dispatch matches the shared prefix.
  rewrite_regex '"business_"' '"shop_"'
  rewrite_regex_optional '\|business_\|business_\|' '|shop_|shop_|'               # the registration-order test
  rewrite_regex '"business-trade:' '"shop-trade:'
  rewrite_regex_optional 'canonical business exchange records' 'canonical shop trade records'
  rewrite_regex_optional 'A business exchange does not match' 'A shop trade does not match'
  rewrite_regex_optional 'An inventory business offer has no' 'An inventory shop offer has no'
  # The candidate parser cut the old prefixes by their length; it now measures
  # the prefix itself, so the next rename cannot break it.
  local trade
  trade=$(git grep -lF '"shop_continue:" + offer.Id' -- "$SIM/*.cs")
  [[ $(wc -w <<< "$trade") == 1 ]] || die "expected one file offering shop_continue: candidates, found: ${trade:-none}"
  perl -0pi -e '
    my $n = 0;
    $n += s/\bcandidateId\[14\.\.\]/candidateId["shop_buy:".Length..]/g;
    $n += s/\bcandidate\[14\.\.\]/candidate["shop_buy:".Length..]/g;
    $n += s/\bcandidate\[\(cancel \? 16 : 18\)\.\.\]/candidate[(cancel ? "shop_cancel:".Length : "shop_continue:".Length)..]/g;
    $n == 3 or die "expected three cuts of the old shop candidate prefixes, found $n\n";
  ' "$trade"
  move_file "$(one_file "^$SIM/.*/BusinessContent\\.cs\$")" "$(dirname "$(one_file "^$SIM/.*/BusinessContent\\.cs\$")")/StoreContent.cs"
}

# ---------------------------------------------------------------------------
# Step settlement: "Settlement" is the name of the community from before
# Towns. Each type says what it holds; World/Settlement/{Community,Economy,
# Family} move up a level; files of WorldRuntime partials are named
# WorldRuntime.<Topic>.cs. Saved strings follow: events, logs and the
# derived-ID text of partnerships, care relationships and trust memories.
#
# Kept: "settlement" meaning the completion of a transfer (estate
# settlement, SettledTick, settlement time, a land transfer's settlement
# roster), resettlement of an abandoned Town, and digest or seed inputs: the
# clankerworld-settlement-v1 package ID, the settlement-<kind> starting
# resources and the settlement:communal estate owner.
# ---------------------------------------------------------------------------
step_settlement() {
  note "step settlement"
  # Files move first: their paths in docs and architecture baselines must
  # change as whole paths, before a type rename can change a path's file name.
  # Every C# file under World/Settlement/, and every other file named
  # Settlement*, moves; a target that exists stops the step.
  local path dir base target moves=()
  while IFS= read -r path; do
    dir=$(dirname "$path"); base=$(basename "$path")
    case "$path" in
      "$SIM/World/Settlement/"*/*.cs) dir="$SIM/World/$(basename "$dir")" ;;
      "$SIM/World/Settlement/"*.cs) dir="$SIM/World/Runtime" ;;
      "$TESTS/Simulation/World/Settlement/"*/*.cs) dir="$TESTS/Simulation/World/$(basename "$dir")" ;;
      "$TESTS/Simulation/World/Settlement/HouseRelocationRulesTests.cs"|"$TESTS/Simulation/World/Settlement/HouseResidentCapacityRulesTests.cs")
        dir="$TESTS/Simulation/World/Community" ;;
      "$TESTS/Simulation/World/Settlement/"*.cs) dir="$TESTS/Simulation/World/Economy" ;;
    esac
    case "$base" in
      SettlementCouncil.cs) base=WorldRuntime.FoodStewardship.cs ;;
      SettlementTrade.cs) base=WorldRuntime.DirectBarter.cs ;;
      SettlementBusinessTrade.cs) base=WorldRuntime.ShopTrade.cs ;;
      SettlementProjects.cs) base=WorldRuntime.AgentProjects.cs ;;
      SettlementForestry.cs) base=WorldRuntime.TreePlanting.cs ;;
      SettlementDeparture.cs) base=WorldRuntime.HouseholdDeparture.cs ;;
      SettlementContent.cs) base=HearthContent.cs ;;
      CivicSystem.SettlementFirstTownSetup.cs) base=CivicSystem.FirstTownSetup.cs ;;
      SettlementWeatherTestFixture.cs) base=WeatherTestFixture.cs ;;
      SettlementCouncilTests.cs) base=FoodStewardshipTests.cs ;;
      CouncilBallotExpiryTests.cs) base=FoodPolicyBallotExpiryTests.cs ;;
      SettlementTradeTests.cs) base=DirectBarterTests.cs ;;
      SettlementProjectTests.cs) base=AgentProjectTests.cs ;;
      Settlement*.cs)
        if [[ "$path" == "$SIM/"* ]]; then base=WorldRuntime.${base#Settlement}; else base=${base#Settlement}; fi ;;
    esac
    base=${base//Business/Shop}
    target="$dir/$base"
    [[ "$target" == "$path" ]] && continue
    moves+=("$path" "$target")
  done < <(git ls-files -- "$SIM/World/Settlement/" "$TESTS/Simulation/World/Settlement/" '*Settlement*.cs' '*Business*.cs' |
    grep -v -e '/archive/' -e '\.cs\.txt$' | sort -u)
  local i
  for ((i = 0; i < ${#moves[@]}; i += 2)); do
    [[ "$(basename "${moves[i+1]}")" != *Settlement* ]] || die "no new name for ${moves[i]}"
    move_file "${moves[i]}" "${moves[i+1]}"
  done
  note "moved $(( ${#moves[@]} / 2 )) Settlement files"
  [[ -z "$(git ls-files -- "$SIM/World/Settlement" "$TESTS/Simulation/World/Settlement")" ]] || die "files are left under World/Settlement/"
  # The folder paths in label rules and docs.
  rewrite_regex '(ClankerWorld\.(?:Simulation|Tests)/(?:Simulation/)?World)/Settlement/(Community|Economy|Family)/' '$1/$2/'
  rewrite_regex_optional '(ClankerWorld\.Simulation/World)/Settlement/' '$1/'
  local stray
  stray=$(eligible_files | xargs -0 -r grep -nP 'World/Settlement\b' || true)
  [[ -z "$stray" ]] || die "paths into World/Settlement/ are left:
$stray"
  cat > "$SCRATCH/settlement.map" <<'MAP'
SettlementSkillKind SkillKind
SettlementSkill LearnedSkill
SettlementProficiency WorkProficiency
SettlementLesson SkillLesson
SettlementSocialStanding SocialStandingEntry
SettlementParenthoodFood ParenthoodFood
SettlementParenthood ParenthoodPlan
SettlementGuardianPlacement GuardianPlacementState
SettlementGuardianSearch GuardianSearchState
SettlementHousingRequest HousingRequest
SettlementHousingRefusal HousingRefusal
SettlementHousing HousingState
SettlementRelocation RelocationNotice                 # a move-out notice from an overcrowded House
SettlementExplorationGoal ExplorationGoal
SettlementExploration ExplorationState
SettlementContinuityCouple ContinuityCouple
SettlementContinuity PopulationContinuity             # the low-population continuity rule
SettlementSurvivalState SurvivalState
SettlementIllnessRules IllnessRules
SettlementProject AgentProject
! SettlementDeparture HouseholdDeparture          # also the name of its moved file, WorldRuntime.HouseholdDeparture.cs
SettlementContent HearthContent                       # the stone hearth and the hearty meal
SettlementDigest HearthDigest
StageSettlementContent StageHearthContent
SettlementStoragePosition SharedStoragePosition      # the storage object, first warehouse or Town origin
AddSettlementResources AddStartingResources           # the starting materials near that storage
AdvanceSettlementSurvival AdvanceSurvival
SettlementWeatherTestFixture WeatherTestFixture
LogSettlementLesson LogLessonActivity
LogSettlementHousing LogHousingActivity
LogSettlementFamily LogFamilyActivity
LogSettlementActivity LogProjectActivity
SettlementTrustMemoryId TrustMemoryId
SettlementPartnershipId NewPartnershipId              # PartnershipId is the member that holds one
SettlementCareId NewCareRelationshipId                # CareRelationshipId is the member that holds one
settlement_lesson lesson_activity
settlement_family family_activity
settlement_housing housing_activity
settlement_activity project_activity
settlement_content_staged hearth_content_staged
settlement_resource_blocked starting_resource_blocked
settlement_resources_added starting_resources_added
SettlementParenthoodTests ParenthoodTests
SettlementChildhoodActionsTests ChildhoodActionsTests
! SettlementFamilyTests FamilyTests                   # FamilyTests is only sample data in ci-plan.test.js
SettlementGuardianPlacementTests GuardianPlacementTests
SettlementLearningTests LearningTests
SettlementProficiencyTests ProficiencyTests
SettlementProjectTests AgentProjectTests
SettlementSkillWorkTests SkillWorkTests
SettlementSocialStandingTests SocialStandingTests
SettlementSurvivalTests SurvivalTests
ChildCanGrowIntoAWorkingAdultThroughSavedSettlementLife ChildCanGrowIntoAWorkingAdultThroughSavedTownLife
ColdSettlementUsesFuelAndEquipmentAndCanRecoverAcrossRestart ColdTownUsesFuelAndEquipmentAndCanRecoverAcrossRestart
DefaultSettlementGathersDifferentInputsSharesAndCompletesVisibleProjects DefaultTownGathersDifferentInputsSharesAndCompletesVisibleProjects
WorldObservationProjectsTheActiveSettlementAndNeeds WorldObservationProjectsTheActiveTownAndNeeds
MAP
  rename_map "$SCRATCH/settlement.map" settlement
  rewrite_regex '"settlement-partnership:' '"partnership:'
  rewrite_regex '"settlement-care:' '"care-relationship:'
  rewrite_regex '"settlement-trust:' '"trust-memory:'
  rewrite_regex 'The saved settlement survival state is invalid\.' 'The saved survival state is invalid.'
  rewrite_regex 'The saved settlement project is invalid\.' 'The saved agent project is invalid.'
  rewrite_regex_optional 'Content referenced by settlement projects' 'Content referenced by agent projects'
  # Docs and comments that name these by the old words.
  rewrite_regex_optional '`settlement-trust:`' '`trust-memory:`'
  rewrite_regex_optional '\bowns the settlement council,' 'owns the food stewardship,'
  rewrite_regex_optional '\bsettlement (project)(s?)\b' 'agent $1$2'
  rewrite_regex_optional '\bsettlement resources\b' 'starting resources'
  rewrite_regex_optional 'the world runtime and settlement;' 'the world runtime with its community, economy and family code;'

  # Settlement code that is left must be a name this step keeps.
  local left
  left=$(code_names '(?i)settlement' settlement | grep -vP '^(?:\w*[Rr]esettlement\w*|\w*EstateSettlement\w*|secondSettlement|AnUncollectedStoreAllowanceRemainsPhysicalThroughDeathAndEstateSettlement|ModProducedStorePaymentSurvivesHostCheckpointAndSettlement|NativeHeirRecallsFinalWordsFromAnAcceptedWillAfterSettlementAndReload|NaturalDeathUsesRecordedTownAndHistoricalDefaultLawAcrossSettlementReload|SettlementDeliversWhatTheTownCanKeepAndSendsTheRestToTheHousehold|beforeSettlement)$' || true)
  [[ -z "$left" ]] || die "Settlement names left in code: $left"

  sort_json "$(one_file "^$TESTS/Architecture/agent-writers\\.json\$")"
}

# ---------------------------------------------------------------------------
# Step fullness: HungerBasisPoints stores fullness (0 starving, 10,000 full),
# so it is FullnessBasisPoints, saved as fullnessBasisPoints. The model's
# field is fullness_basis_points, with the prompt text that explains it. The
# need-wording comparison record keeps the name it measured.
# ---------------------------------------------------------------------------
step_fullness() {
  note "step fullness"
  cat > "$SCRATCH/fullness.map" <<'MAP'
HungerBasisPoints FullnessBasisPoints
hunger_basis_points fullness_basis_points
?! hungerBasisPoints fullnessBasisPoints   # a test parameter; ModelNeedWords and GameUiText have their own
MAP
  rename_map "$SCRATCH/fullness.map" fullness
  # The decision factor that shows it to the owner says what it is.
  rewrite_regex 'new\("hunger", (\$"\{\w+\.FullnessBasisPoints\} basis points"\))' 'new("fullness", $1'
  sort_json "$(one_file "^$TESTS/Architecture/agent-writers\\.json\$")"
}

# ---------------------------------------------------------------------------
# Step resident: "resident" meant any agent in the shared agent queries and
# the world adapters. A Town's or House's resident keeps the word
# (TownRuntimeState.ResidentIds, HouseResidentCapacityRules).
# ---------------------------------------------------------------------------
step_resident() {
  note "step resident"
  cat > "$SCRATCH/resident.map" <<'MAP'
ResidentQueries AgentQueries
! AdultResident IsAdult       # also SocietyRules', AgentMarriageRules' and AnimalState's own IsAdult
ChildResident IsChild
IResidentWorld IAgentWorld
IEnvironmentResidentWorld IEnvironmentAgentWorld
ResidentWorldAdapter AgentWorldAdapter
residentWorld agentWorld
MAP
  RENAME_FILES=1 rename_map "$SCRATCH/resident.map" resident
  move_file "$(one_file "^$SIM/.*/WorldRuntime\\.ResidentWorld\\.cs\$")" \
    "$(dirname "$(one_file "^$SIM/.*/WorldRuntime\\.ResidentWorld\\.cs\$")")/WorldRuntime.AgentWorld.cs"
  replace_in_optional "$(one_file "^$SIM/.*/AgentQueries\\.cs\$")" 'Shared resident rules' 'Shared agent rules'
}

# ---------------------------------------------------------------------------
# Step orders: the player sends suggestions and orders
# (docs/game-design/interface-and-art.md). OwnerInstructionKind is
# Suggestion or Order, sent and shown to the model as "suggestion" or
# "order"; the task the game understood from an order is an OrderTask, held
# in the instruction's Task member (saved and sent as "task").
# ---------------------------------------------------------------------------
step_orders() {
  note "step orders"
  cat > "$SCRATCH/orders.map" <<'MAP'
OwnerInstructionOrder OrderTask
InstructionOrderView OrderTaskView
OwnerWorldInstructionOrder OwnerWorldOrderTask
InstructionOrderSummary OrderTaskSummary
ParseInstructionOrder ParseOrderTask
WorldInstructionOrderParser OrderTaskParser
MAP
  RENAME_FILES=1 rename_map "$SCRATCH/orders.map" orders
  # The member that holds the task. LINQ's Order() and tuple elements named
  # Order keep theirs: a save-timeline tuple and the terrain transition order.
  local cs=() file
  mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZw -- Order || true)
  local keep=()
  for file in "${cs[@]}"; do
    case "$file" in */SaveTimeline.cs|*/TerrainTransitions.cs) keep+=("$file"); continue ;; esac
  done
  for file in "${keep[@]}"; do
    grep -qP '\b(OrderTask|OwnerWorldOrderTask|OrderTaskView)\b' "$file" && die "$file names the order task; extend step_orders"
  done
  cat > "$SCRATCH/order-member.pl" <<'PERL'
return if $FILE =~ m{/(?:SaveTimeline|TerrainTransitions)\.cs$};
s/(?<=\.)Order\b(?!\s*[(<])/Task/g;                                          # instruction.Order
s/\b((?:OrderTask|OwnerWorldOrderTask|OrderTaskView)\?\s+)Order\b/${1}Task/g;   # the record members
s/(?<![\w.])Order(\s*[:=])(?![=:>])/Task$1/g;      # named arguments, patterns, initializers and with
s/(?<![\w.])Order(?=(?:\.\w+)+\s*:)/Task/g;         # extended property patterns: { Order.Action: ... }
PERL
  code_regex "$SCRATCH/order-member.pl" "${cs[@]}"
  local left
  left=$(eligible_cs_files | xargs -0 -r grep -nP '(?<![\w."])Order\s*[:=](?![=:])' | grep -vP '/(?:SaveTimeline|TerrainTransitions)\.cs:|"[^"]*Order\s*[:=]' || true)
  [[ -z "$left" ]] || die "an instruction's Order member may be left:
$left"

  # Tests that open a save at the instruction's task, before "order" is a kind.
  rewrite_regex '\["order"\]' '["task"]'

  # The kinds. Code first, then their wire and model spellings.
  printf 'Suggestive Suggestion\nMustDo Order\n' > "$SCRATCH/kinds.map"
  mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZwE -- 'Suggestive|MustDo' || true)
  (( ${#cs[@]} > 0 )) || die "OwnerInstructionKind.Suggestive and MustDo were not found"
  code_rewrite "$SCRATCH/kinds.map" "${cs[@]}"
  cat > "$SCRATCH/kind-strings.pl" <<'PERL'
s/\bA must_do message with an understood_task\b/An order message with an understood_task/g;
s/\bmust be suggestive or must_do\b/must be suggestion or order/g;
s/^must_do$/order/; s/^suggestive$/suggestion/;
PERL
  mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZE -- 'must_do|suggestive' || true)
  string_rewrite "$SCRATCH/kind-strings.pl" "${cs[@]}"
  left=$(eligible_cs_files | xargs -0 -r grep -nP '\bmust_do\b|"suggestive"|\b(Suggestive|MustDo)\b' || true)
  [[ -z "$left" ]] || die "instruction kind names left:
$left"
  local doc=docs/development/how-it-works.md
  replace_in "$doc" '`Suggestive`) or orders (**Order**, `MustDo`).' '`Suggestion`) or orders (**Order**, `Order`).'
  rewrite_regex '\bA MustDo with\b' 'An order with'
  rewrite_regex '\ba MustDo never\b' 'an order never'
  rewrite_regex '\bRecognized MustDo instructions\b' 'Recognized orders'
  rewrite_regex '\bstrict MustDo grammar\b' 'strict order grammar'
  rewrite_regex '\bcontinuing intentions and MustDo cannot\b' 'continuing intentions and orders cannot'
  rewrite_regex '\bSuggestive (interpretation|guidance)\b' 'Suggestion $1'
  left=$(eligible_files | xargs -0 -r grep -nP '\bMustDo\b|\bSuggestive\b' || true)
  [[ -z "$left" ]] || die "MustDo or Suggestive left in docs:
$left"
}

# ---------------------------------------------------------------------------
# Step council: a Town's governing body is its council
# (docs/game-design/towns.md). TownGovernanceState is TownCouncilState, held
# in TownRuntimeState.Council and saved and sent as "council". The food vote
# left the name free in step stewardship.
# ---------------------------------------------------------------------------
step_council() {
  note "step council"
  cat > "$SCRATCH/council.map" <<'MAP'
TownGovernanceState TownCouncilState
TownGovernanceRules TownCouncilRules
TownGovernanceValidation TownCouncilValidation
AdvanceTownGovernance AdvanceTownCouncil
WithGovernance WithCouncil
? SaveTownGovernance SaveTownCouncil
OwnerTownGovernance OwnerTownCouncil
TownGovernanceView TownCouncilView
? TownGovernanceTests TownCouncilTests
? TownGovernanceStaleRoundTests TownCouncilStaleRoundTests
? TownGovernanceSaveValidationTests TownCouncilSaveValidationTests
? TownGovernanceRuntimeTests TownCouncilRuntimeTests
? TownGovernanceProjectionTests TownCouncilProjectionTests
MAP
  RENAME_FILES=1 rename_map "$SCRATCH/council.map" council
  # The member, in code only (prose keeps "governance" as a word).
  local cs=() file
  echo "Governance Council" > "$SCRATCH/council-member.map"
  mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZw -- Governance || true)
  (( ${#cs[@]} > 0 )) || die "TownRuntimeState.Governance was not found"
  code_rewrite "$SCRATCH/council-member.map" "${cs[@]}"
  # Locals and parameters too, except beside a "council" of their own.
  echo "governance council" > "$SCRATCH/council-local.map"
  mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZw -- governance || true)
  for file in "${cs[@]}"; do
    [[ -n "$(perl "$CODE_WORDS" list '(?-i)^governance$' "$file")" ]] || continue
    if [[ -n "$(perl "$CODE_WORDS" list '(?-i)^council$' "$file")" ]]; then note "$file keeps its governance locals beside council ones"; continue; fi
    code_rewrite "$SCRATCH/council-local.map" "$file"
  done
  echo 's/^governance$/council/;' > "$SCRATCH/council-strings.pl"
  mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZF -- '"governance"' || true)
  string_rewrite "$SCRATCH/council-strings.pl" "${cs[@]}"
  file=$(one_file "^$SIM/World/Civic/CivicSystem\\.Governance\\.cs\$")
  move_file "$file" "$SIM/World/Civic/CivicSystem.Council.cs"
  file=$(one_file "^$SIM/World/Towns/TownGovernance\\.cs\$")
  move_file "$file" "$SIM/World/Towns/TownCouncil.cs"
  file=$(one_file "^$SIM/World/Towns/WorldRuntime\\.Governance\\.cs\$")
  move_file "$file" "$SIM/World/Towns/WorldRuntime.Council.cs"
}

# ---------------------------------------------------------------------------
# Step hearings: the mayor's office hears land cases and non-land cases
# (towns.md, "Land hearings and rulings" and the non-land procedure). The
# "nonviolent" code is the non-land hearing system: TownNonviolentState ->
# TownNonLandHearingState in TownRuntimeState.NonLandHearings (saved as
# "nonLandHearings"), TownViolationCase -> TownNonLandCase, the
# CivicSystem/WorldRuntime.Nonviolent.* files -> *.NonLandHearings.*, and
# NonviolentHearingText -> NonLandHearingText in the client.
#
# The land side also holds transfers, sales and right changes, so
# TownLandHearingState is TownLandRecords in TownRuntimeState.LandRecords
# (saved as "landRecords"). The views' LandHearings list holds hearings and
# keeps its name.
#
# Kept: model-contract and derived-ID strings that say nonviolent
# (nonviolent_remedy:, nonviolent_relay:, civic_nonviolent, nonviolent-id-*).
# ---------------------------------------------------------------------------
step_hearings() {
  note "step hearings"
  code_names 'Nonviolent' nonviolent | perl -ne 'chomp; my $n = $_;
      my %special = (TownNonviolentState => "TownNonLandHearingState", Nonviolent => "NonLandHearings",
        CivicNonviolent => "CivicNonLandHearing", NonviolentHearingText => "NonLandHearingText");
      if (exists $special{$n}) { $n = $special{$n}; } else { $n =~ s/Nonviolent/NonLand/g; $n =~ s/nonviolent/nonLand/g; }
      print "$_ $n\n"' > "$SCRATCH/hearings.map"
  grep -q '^TownNonviolentState TownNonLandHearingState$' "$SCRATCH/hearings.map" || die "TownNonviolentState was not found"
  echo "TownViolationCase TownNonLandCase" >> "$SCRATCH/hearings.map"
  echo "TownLandHearingState TownLandRecords" >> "$SCRATCH/hearings.map"
  check_new_names "$SCRATCH/hearings.map"
  # The bare word is also the design's "Nonviolent law enforcement" heading:
  # it changes in code only.
  grep '^Nonviolent ' "$SCRATCH/hearings.map" > "$SCRATCH/hearings-bare.map" || die "TownRuntimeState.Nonviolent was not found"
  grep -v '^Nonviolent ' "$SCRATCH/hearings.map" > "$SCRATCH/hearings-words.map"
  rename_map "$SCRATCH/hearings-words.map" hearings
  local bare=()
  mapfile -d '' bare < <(eligible_cs_files | xargs -0 -r grep -lZw -- Nonviolent || true)
  code_rewrite "$SCRATCH/hearings-bare.map" "${bare[@]}"
  # Saved names in tests' JSON paths, and an error message.
  cat > "$SCRATCH/hearing-strings.pl" <<'PERL'
s/^nonviolent$/nonLandHearings/; s/^landHearings$/landRecords/;
s/^Unsupported nonviolent case action\.$/Unsupported non-land case action./;
PERL
  local cs=()
  mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZE -- '"(nonviolent|landHearings)"|Unsupported nonviolent' || true)
  string_rewrite "$SCRATCH/hearing-strings.pl" "${cs[@]}"

  # TownRuntimeState.LandHearings -> LandRecords, not the views' list of
  # hearings. The client has only views; elsewhere every use is the state's
  # except where it is a list: declared as one, filled by a projection or
  # enumerated, which TownLandRecords cannot be. The build checks both sides.
  mapfile -d '' cs < <(eligible_cs_files | xargs -0 -r grep -lZw -- LandHearings || true)
  cat > "$SCRATCH/land-records.pl" <<'PERL'
return if $FILE =~ m{^src/ClankerWorld\.GodotClient/|^tests/[^/]+/GodotClient/};
s/\bLandHearings\b/LandRecords/g;
s/\b(IReadOnlyList<\w+>\s+)LandRecords\b/${1}LandHearings/g;                                   # the view's list
s/\bLandRecords(\s*=\s*Project\w*\()/LandHearings$1/g;                                       # its projection
s/\bLandRecords(?=\s*(?:\.\s*(?:Single|First|FirstOrDefault|Last|Count|Select|SelectMany|Where|Any|All|ToArray|OrderBy)\b|\[))/LandHearings/g;
s/(\bAssert\.(?:Single|Empty|NotEmpty|Contains|DoesNotContain|Collection)\((?:[^()]|\([^()]*\))*?\.)LandRecords(?=\s*[),])/${1}LandHearings/g;
s/(\bSelectMany\(\s*\w+\s*=>\s*\w+\.)LandRecords\b/${1}LandHearings/g;
s/(\bforeach\s*\([^)]*\bin\s+[\w.!?]+\.)LandRecords(?=\s*\))/${1}LandHearings/g;
PERL
  code_regex "$SCRATCH/land-records.pl" "${cs[@]}"

  # File names.
  local path target
  while IFS= read -r path; do
    [[ "$path" == *.uid ]] && continue
    is_protected "$path" && continue
    [[ "$path" == "$CLIENT"/*/Main.* || "$path" == "$CLIENT"/Main.* ]] && continue
    target=$(perl -pe 's/\.Nonviolent\./.NonLandHearings./; s/TownNonviolentState/TownNonLandHearingState/; s/NonviolentHearingText/NonLandHearingText/; s/Nonviolent/NonLand/g' <<< "$path")
    move_file "$path" "$target"
  done < <(git ls-files | grep 'Nonviolent' || true)
}

# ---------------------------------------------------------------------------
# Step town: the system that runs Towns is the TownSystem (ITownWorld is its
# view of the world); a Town's shared building project is a TownProject; the
# Forestry code plants trees; the developer's life pace is the aging override
# (docs/playing.md), with its route, signed-request domain, capability, event
# and log. The other ICivic* adapters keep their names.
# ---------------------------------------------------------------------------
step_town() {
  note "step town"
  cat > "$SCRATCH/town.map" <<'MAP'
CivicSystem TownSystem
ICivicTownWorld ITownWorld
civicSystem townSystem
CivicSystemTests TownSystemTests
! TownConstructionProject TownProject      # GoodsDeliveryPurpose.TownProject is an enum member of the same name
AddForestryCandidates AddTreePlantingCandidates
RegisterForestryChoices RegisterTreePlantingChoices
ForestryContent TreePlantingContent
MAP
  code_names 'LifePace' lifepace | perl -ne 'chomp; (my $n = $_) =~ s/LifePace/AgingOverride/g; $n =~ s/lifePace/agingOverride/g; print "$_ $n\n"' >> "$SCRATCH/town.map"
  grep -q '^SetLifePace SetAgingOverride$' "$SCRATCH/town.map" || die "SetLifePace was not found"
  RENAME_FILES=1 rename_map "$SCRATCH/town.map" town
  rewrite_regex 'life-pace' 'aging-override'
  rewrite_regex '\b(world_)?life_pace\b' '$1aging_override'
  rewrite_regex '\blife_pace_changed\b' 'aging_override_changed'
  rewrite_regex_optional '\bLife-pace and Jev actions\b' 'Aging-override and Jev actions'
  local path target
  while IFS= read -r path; do
    [[ "$path" == *.uid ]] && continue
    is_protected "$path" && continue
    [[ "$path" == "$CLIENT"/*/Main.* || "$path" == "$CLIENT"/Main.* ]] && continue
    target=$(perl -pe 's/LifePace/AgingOverride/g; s/Forestry/TreePlanting/g; s/TownConstructionProject/TownProject/g' <<< "$path")
    move_file "$path" "$target"
  done < <(git ls-files | grep -E 'LifePace|Forestry|TownConstructionProject' || true)
  local left
  left=$(code_names '(?i)lifepace|forestry|^CivicSystem|ICivicTownWorld|TownConstructionProject' civic || true)
  left+=$(code_names '(?i)lifepace|forestry' pace || true)
  [[ -z "$left" ]] || die "town names left in code: $left"
}

# ---------------------------------------------------------------------------
# Step docs: the saved names this stage changed join the paragraph rename.sh
# writes for its schema bump, and the derived-ID page names the new text.
# ---------------------------------------------------------------------------
step_docs() {
  note "step docs"
  local doc=docs/development/saves-and-replay.md
  require_file "$doc"
  grep -qP '^Schema \d+ removes prototype names from the save$' "$doc" ||
    die "$doc has no 'Schema N removes prototype names from the save' paragraph from rename.sh"
  perl -0pi -e '
    my $add = <<"TEXT";
The same schema saves the game design\x27s names: `agents`, `deceasedAgents`,
`agentId` and `targetAgentId` instead of `inhabitants`, `deceasedInhabitants`,
`inhabitantId` and `targetInhabitantId`; `fullnessBasisPoints` instead of
`hungerBasisPoints`; `foodStewardship` instead of the world\x27s `council` for
the food-policy vote; `shopTrades` instead of `businessTrades`; and, for each
Town, `council`, `landRecords` and `nonLandHearings` instead of `governance`,
`landHearings` and `nonviolent`. An instruction\x27s understood task is saved
as `task` instead of `order`. Event kinds follow: `agent_*` instead of
`inhabitant_*`, `barter_*` instead of `settlement_trade_*`, `shop_trade_*`
instead of `business_trade_*`, `food_policy_*` and `food_steward_changed`
instead of `council_*`, `hearth_content_staged` and `starting_resource*`
instead of the `settlement_*` names. Generated IDs use `{world}:agent:` instead
of `{world}:inhabitant:`, and `direct-barter:`, `shop-trade:`, `partnership:`,
`care-relationship:` and `trust-memory:` instead of `settlement-trade:`,
`business-trade:`, `settlement-partnership:`, `settlement-care:` and
`settlement-trust:` ([derived identities](derived-identities.md)). The
provider-configuration file, which keeps model keys outside the world, moves
to its own schema 4 for the `AgentId` of each model assignment; an older file
is upgraded in place, so saved keys keep working.
TEXT
    s/(^Schema \d+ removes prototype names from the save\n(?:.+\n)*?)(?=\n)/$1$add/m or die "paragraph end not found\n";
  ' "$doc"

  doc=docs/development/derived-identities.md
  require_file "$doc"
  perl -0pi -e '
    my $add = <<"TEXT";
## Generated agent IDs

A child born in the world gets `DerivedId.Create("{worldId}:agent:{request}")`
from its accepted birth request. Saves made before the
[#1364](https://github.com/compoodment/ClankerWorld/issues/1364) rename spelled
it `:inhabitant:`; those saves are refused by schema, so no producer or reader
accepts the old spelling. The same rename changed the text of these
generators: `direct-barter:` (direct barter offers), `shop-trade:` (shop
trades), `partnership:` and `care-relationship:` (relationship records) and
`trust-memory:` (trust memories).

TEXT
    s/(^## Matching generated IDs\n)/$add$1/m or die "no Matching generated IDs heading\n";
  ' "$doc"
}

# ---------------------------------------------------------------------------
# Step restore: what must not be left, and the architecture baseline sorted.
# ---------------------------------------------------------------------------
step_restore() {
  note "step restore"
  sort_json "$(one_file "^$TESTS/Architecture/agent-writers\\.json\$")"
  local left
  left=$(git ls-files | grep -iP 'inhabitant|Settlement[A-Z]|/Settlement/|Business|Nonviolent|TownGovernance|ResidentQueries|IEnvironmentResidentWorld' |
    grep -vP '^(CHANGELOG\.md|changes/|docs/archive/)|\.cs\.txt$|^docs/development/[^/]*-(probe|equivalence)/' |
    grep -vP "^$CLIENT/(.*/)?Main\\.[^/]*$" || true)
  [[ -z "$left" ]] || die "files with old names are left:
$left"
}

# --- run ---------------------------------------------------------------------
for step in $STEPS; do
  declare -F "step_$step" >/dev/null || die "unknown step: $step"
done
for step in agent stewardship barter shop settlement fullness resident orders council hearings town docs restore; do
  has_step "$step" || continue
  declare -F "step_$step" >/dev/null || die "step $step is not written yet"
  "step_$step"
done
git add -A -- . ':(exclude)global.json'
note "done: steps '$STEPS'"
