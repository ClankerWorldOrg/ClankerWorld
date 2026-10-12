#!/usr/bin/env bash
# Issue #1364, stage arch: one vocabulary for the world server and its two
# ways of running (owner, October 12). Runs after rename.sh, on its output.
#
#   bash stages/arch.sh                     # every stage, in order
#   STAGES="local secret" bash stages/arch.sh
#
# A world server runs the simulation and is the only authority. The game
# draws the world and sends the player's choices. The server runs either
# locally, when the game starts it on the player's PC (the default), or
# remotely on another machine such as a VPS.
#
# Stages (always run in this order):
#   local        the server the game starts: LocalHostCompanion -> LocalServer, ...
#   secret       the one-launch secret: CompanionSecret -> LaunchSecret, its
#                setting, header and file
#   approval     the loopback admin listener: OwnerPairingHostOptions ->
#                ApprovalListenerOptions, its port setting and /api/v1/approval/*
#   logs         log events, the server's log files and the package folder
#                host/ -> server/
#   remote       a server on another machine: --server-url, the Godot setting,
#                WorldServerOrigin -> ServerOrigin, docs "private server"
#   model        Hosted* (a cloud model call) -> Model*, and the model log events
#   logfields    the log field inhabitant= -> agent= (log lines only; signed
#                payloads keep their bytes)
#   entrypoints  package.json lists each version's entry points, and the
#                launcher reads them (the one behaviour change)
#   config       ClankerWorld:Runtime splits into World, Clock and Providers
#   folders      the server project's folders and namespaces
#   wording      comments and docs that still say host, companion or hosted
#   notes        lists the names a freshly installed remote world server's
#                systemd unit and environment use, in the install section
#                rename.sh adds to the deployment page (fails without it)
#
# Like rename.sh it only renames, apart from the entrypoints stage: git mv for
# files, word-boundary rewrites of known identifiers, and the strings, routes,
# settings and paths listed above. It never touches CHANGELOG.md, the
# changes/ history (it adds one sentence to this PR's own entry), docs/archive/
# or measurement and probe evidence. Old names get no aliases: remote servers
# are installed fresh with this build (owner decision). Saved world fields keep
# their names (the hosted_decision_* world event kinds among them), and the
# schema is not bumped here. It fails loudly when an expected name is missing
# or a new name is already taken. It does not commit.
set -euo pipefail

die() { echo "arch.sh: error: $*" >&2; exit 1; }
note() { echo "arch.sh: $*" >&2; }

ROOT=$(git rev-parse --show-toplevel 2>/dev/null) || die "run inside a git checkout"
cd "$ROOT"
[[ -f ClankerWorld.sln ]] || die "not the ClankerWorld repository root: $ROOT"
dirty=$(git status --porcelain --untracked-files=no -- . ':(exclude)global.json')
[[ -z "$dirty" ]] || die "working tree has changes; start from a clean checkout:
$dirty"

STAGES=${STAGES:-"local secret approval logs remote model logfields entrypoints config folders wording notes"}
has_stage() { [[ " $STAGES " == *" $1 "* ]]; }

SERVER=src/ClankerWorld.Server
CLIENT=src/ClankerWorld.GodotClient
TESTS=tests/ClankerWorld.Tests
[[ -d $SERVER && -d $TESTS ]] || die "run rename.sh first: $SERVER and $TESTS are expected"
HERE=$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")
CODE_WORDS=$HERE/../cs-code-words.pl
[[ -f "$CODE_WORDS" ]] || die "cs-code-words.pl must sit next to rename.sh, one folder up"
SCRATCH=$(mktemp -d)
trap 'rm -rf "$SCRATCH"' EXIT

# ---------------------------------------------------------------------------
# File selection and rewriting (the same rules as rename.sh)
# ---------------------------------------------------------------------------

is_protected() {
  case "$1" in
    CHANGELOG.md|changes/*|docs/archive/*) return 0 ;;
    *.cs.txt) return 0 ;;
    docs/development/*-probe/*) return 0 ;;
    docs/development/*-equivalence/*) return 0 ;;
    docs/development/*-measurements.md) return 0 ;;
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
    is_protected "$file" || printf '%s\0' "$file"
  done
}

eligible_cs_files() {
  eligible_files | while IFS= read -r -d '' file; do
    [[ "$file" == *.cs ]] && printf '%s\0' "$file"
  done
}

files_with_word() { eligible_files | xargs -0 -r grep -lZw -F -- "$1" || true; }

# rename_map MAPFILE [LABEL]: simultaneous word-boundary rewrite of every
# "OLD NEW" line in every eligible file. Each OLD must occur unless its line
# starts with "?"; no NEW may occur before the rewrite unless its line starts
# with "=" (a deliberate merge into an existing name). With RENAME_FILES=1,
# tracked files whose names contain an OLD word move to the rewritten name.
rename_map() {
  local map=$1 label=${2:-$(basename "$1")}
  eligible_files > "$SCRATCH/files"
  MAP="$map" LABEL="$label" RENAME_FILES="${RENAME_FILES:-}" FILES="$SCRATCH/files" perl -e '
    use strict; use warnings;
    my (%map, %optional, %merge, @order);
    open my $m, "<", $ENV{MAP} or die "cannot read $ENV{MAP}\n";
    while (<$m>) {
      chomp; s/#.*//; next unless /\S/;
      my ($flags) = /^\s*([?=]*)/; s/^\s*[?=]*\s*//;
      my @f = split " ";
      die "bad map line in $ENV{LABEL}: $_\n" if @f != 2;
      my ($old, $new) = @f;
      die "duplicate map entry in $ENV{LABEL}: $old\n" if exists $map{$old};
      $map{$old} = $new; push @order, $old;
      $optional{$old} = 1 if $flags =~ /\?/;
      $merge{$new} = 1 if $flags =~ /=/;
    }
    my @olds = sort { length($b) <=> length($a) || $a cmp $b } keys %map;
    my $alt = join "|", map { quotemeta } @olds;
    my $re = qr/(?<![\w])($alt)(?![\w])/;
    my %isold = map { $_ => 1 } @olds;
    my @news = grep { !$isold{$_} && !$merge{$_} } sort { length($b) <=> length($a) || $a cmp $b } values %map;
    my %seen; @news = grep { !$seen{$_}++ } @news;
    my $newre = @news ? do { my $n = join "|", map { quotemeta } @news; qr/(?<![\w])($n)(?![\w])/ } : undef;
    local $/ = "\0";
    open my $list, "<", $ENV{FILES} or die;
    my @files = map { chomp; $_ } <$list>;
    close $list;
    local $/;
    my (%count, %clash, %content);
    for my $file (@files) {
      open my $in, "<", $file or die "cannot read $file\n";
      my $text = <$in>; close $in;
      $text = "" unless defined $text;
      if ($newre) { while ($text =~ /$newre/g) { push @{$clash{$1}}, $file; } }
      my $hits = 0;
      while ($text =~ /$re/g) { $count{$1}++; $hits++; }
      $content{$file} = $text if $hits;
    }
    my @missing = grep { !$count{$_} && !$optional{$_} } @order;
    die "$ENV{LABEL}: expected names not found: @missing\n" if @missing;
    if (%clash) {
      my @lines = map { my %f = map { $_ => 1 } @{$clash{$_}}; "  $_ in " . join(", ", sort keys %f) } sort keys %clash;
      die "$ENV{LABEL}: new names already used:\n" . join("\n", @lines) . "\n";
    }
    for my $file (sort keys %content) {
      my $text = $content{$file};
      $text =~ s/$re/$map{$1}/g;
      open my $out, ">", $file or die "cannot write $file\n";
      print $out $text; close $out;
    }
    printf STDERR "arch.sh: %s: %d names in %d files\n", $ENV{LABEL}, scalar(grep { $count{$_} } @order), scalar(keys %content);
    if ($ENV{RENAME_FILES}) {
      open my $all, "-|", "git", "ls-files", "-z" or die;
      local $/ = "\0";
      my @tracked = map { chomp; $_ } <$all>;
      close $all;
      for my $path (@tracked) {
        next if $path =~ m{^(?:CHANGELOG\.md|changes/|docs/archive/)} || $path =~ /\.cs\.txt$/ ||
          $path =~ m{^docs/development/[^/]*-(?:probe|equivalence)/};
        my ($dir, $base) = $path =~ m{^(.*/)?([^/]+)$};
        $dir //= "";
        (my $renamed = $base) =~ s/$re/$map{$1}/g;
        next if $renamed eq $base;
        my $target = "$dir$renamed";
        die "target already exists: $target\n" if -e $target;
        system("git", "mv", $path, $target) == 0 or die "git mv $path failed\n";
        print STDERR "arch.sh: moved $path -> $target\n";
      }
    }
  '
}

# code_words MAPFILE FILE...: rewrites C# identifiers only (never strings or
# comments) in the named files. Each OLD must occur in them, and no NEW may
# already be a code name there.
code_words() {
  local map=$1; shift
  local files=() file old new left
  for file in "$@"; do [[ -f "$file" ]] || die "expected file is missing: $file"; files+=("$file"); done
  while read -r old new; do
    [[ -n "$old" ]] || continue
    left=$(perl "$CODE_WORDS" list "(?-i)^$old\$" "${files[@]}")
    [[ -n "$left" ]] || die "expected code name '$old' was not found in: $*"
    left=$(perl "$CODE_WORDS" list "(?-i)^$new\$" "${files[@]}")
    [[ -z "$left" ]] || die "new code name '$new' is already used in: $*"
  done < <(sed 's/#.*//' "$map" | awk 'NF == 2')
  perl "$CODE_WORDS" rewrite "$map" "${files[@]}"
  note "renamed code names from $(basename "$map") in $# files"
}

# rewrite_regex PERL_REGEX REPLACEMENT: rewrite over eligible files that must
# change at least one file. REPLACEMENT may use $1 and $2.
rewrite_regex() {
  local pattern=$1 replacement=$2 files=()
  mapfile -d '' files < <(eligible_files | xargs -0 -r grep -lZP -- "$pattern" || true)
  (( ${#files[@]} > 0 )) || die "expected text /$pattern/ was not found"
  PAT="$pattern" REP="$replacement" perl -pi -e '
    BEGIN { $pat = qr/$ENV{PAT}/; $rep = $ENV{REP}; }
    s{$pat}{ my @m = ($1, $2); my $r = $rep; $r =~ s/\$(\d)/defined $m[$1 - 1] ? $m[$1 - 1] : ""/ge; $r }ge;
  ' "${files[@]}"
  note "rewrote /$pattern/ in ${#files[@]} files"
}

rewrite_regex_optional() {
  if eligible_files | xargs -0 -r grep -qP -- "$1"; then rewrite_regex "$@"; else note "skipped /$1/: not found"; fi
}

# rewrite_regex_in FILE PERL_REGEX REPLACEMENT: the same, in one file.
rewrite_regex_in() {
  local file=$1
  [[ -f "$file" ]] || die "expected file is missing: $file"
  PAT="$2" REP="$3" perl -0pi -e '
    BEGIN { $pat = qr/$ENV{PAT}/; $rep = $ENV{REP}; }
    $n += s{$pat}{ my @m = ($1, $2); my $r = $rep; $r =~ s/\$(\d)/defined $m[$1 - 1] ? $m[$1 - 1] : ""/ge; $r }ge;
    END { exit($n ? 0 : 3) }
  ' "$file" || die "expected text /$2/ not found in $file"
}

# replace_in FILE OLD NEW: literal rewrite in one file that must match.
replace_in() {
  local file=$1
  [[ -f "$file" ]] || die "expected file is missing: $file"
  OLD="$2" NEW="$3" perl -0pi -e '$n += s/\Q$ENV{OLD}\E/$ENV{NEW}/g; END { exit($n ? 0 : 3) }' "$file" ||
    die "expected text not found in $file: $2"
}

replace_in_optional() {
  if [[ -f "$1" ]] && grep -qF -- "$2" "$1"; then replace_in "$@"; else note "skipped in $1: '$2' not found"; fi
}

require_file() { [[ -f "$1" ]] || die "expected file is missing: $1"; }
require_dir() { [[ -d "$1" ]] || die "expected folder is missing: $1"; }
require_absent() { [[ ! -e "$1" ]] || die "target already exists: $1"; }

move_path() {
  local from=$1 to=$2
  [[ -e "$from" ]] || die "expected path is missing: $from"
  require_absent "$to"
  mkdir -p "$(dirname "$to")"
  git mv "$from" "$to"
  note "moved $from -> $to"
}

# A Godot C# script moves with its .uid sidecar, so scenes keep their UID.
move_script() {
  move_path "$1" "$2"
  if [[ -e "$1.uid" ]]; then move_path "$1.uid" "$2.uid"; fi
}

# none_left PERL_REGEX WHAT [GIT_PATHSPEC...]: fails if the text is still
# anywhere in the eligible files (or the given pathspecs).
none_left() {
  local pattern=$1 what=$2 left; shift 2
  if (( $# > 0 )); then
    left=$(git grep -n -P -- "$pattern" -- "$@" ':!CHANGELOG.md' ':!changes' ':!docs/archive' || true)
  else
    left=$(eligible_files | xargs -0 -r grep -nP -- "$pattern" || true)
  fi
  [[ -z "$left" ]] || die "$what left:
$left"
}

# ---------------------------------------------------------------------------
# Stage local: the server the game starts on the player's PC is the local
# server. "Companion host", "bundled host" and "local host" all named it.
# ---------------------------------------------------------------------------
stage_local() {
  note "stage local"
  move_script "$CLIENT/Screens/Main/Main.LocalHost.cs" "$CLIENT/Screens/Main/Main.LocalServer.cs"
  cat > "$SCRATCH/local.map" <<'EOF'
LocalHostCompanion LocalServer
LocalHostCompanionTests LocalServerTests
LocalHostLayout LocalServerLayout
LocalHostStart LocalServerStart
LocalHostOutcome LocalServerStartOutcome
StartedHost StartedServer
IsCompanionHost IsLocalServer
IsCompanionShutdownRequest IsLocalServerShutdownRequest
CompanionHostTelemetry LocalServerTelemetry
CompanionHostTests ApprovalListenerTests        # the approval listener's launch-secret checks
FindBundledLocalHost FindBundledLocalServer
StartLocalHostAsync StartLocalServerAsync
ShowLocalHostStopFailure ShowLocalServerStopFailure
HostArguments ServerArguments
HostDirectory ServerDirectory
HostFolderName ServerFolderName
WindowsHostFileName WindowsServerFileName
WithholdHostSettings WithholdServerSettings
IsHostSetting IsServerSetting
CompanionShutdownRequest LocalServerShutdownRequest
?ServerHttpCompanionShutdownTests ServerHttpLocalServerShutdownTests   # a file name only
BundledHostIsFoundBesideTheGameAndKeepsDataInTheUserFolder BundledServerIsFoundBesideTheGameAndKeepsDataInTheUserFolder
BundledHostGetsNoProviderKeysOrSettingOverridesFromThePlayersEnvironment BundledServerGetsNoProviderKeysOrSettingOverridesFromThePlayersEnvironment
GameStartsPairsWithAndStopsItsOwnHost GameStartsPairsWithAndStopsItsOwnServer
AHostThatCannotStartIsReportedNotHidden AServerThatCannotStartIsReportedNotHidden
AnUnresponsiveRelaunchPreservesItsHostAndSecretOnTimeoutOrCancellation AnUnresponsiveRelaunchPreservesItsServerAndSecretOnTimeoutOrCancellation
RelaunchRetriesOneTimedOutProbeAndReusesItsActualCompanion RelaunchRetriesOneTimedOutProbeAndReusesItsActualServer
SameBuildRequiresTheRunningCompanionToRecognizeItsSavedSecret SameBuildRequiresTheRunningServerToRecognizeItsSavedSecret
OnlyTheGameThatStartedTheHostCanApproveOrStopIt OnlyTheGameThatStartedTheServerCanApproveOrStopIt
AServerHostKeepsItsLocalApprovalAndCannotBeStoppedOverHttp ARemoteServerKeepsItsApprovalListenerAndCannotBeStoppedOverHttp
StoppingTheHostSavesTheLatestTickAndCancelsModelWork StoppingTheServerSavesTheLatestTickAndCancelsModelWork
CompanionShutdownSavesPausedWorldBeforeRequestingExitAndCanRetryFailedWrite LocalServerShutdownSavesPausedWorldBeforeRequestingExitAndCanRetryFailedWrite
CompanionShutdownWithoutItsSecretDoesNotPauseSaveOrExit LocalServerShutdownWithoutItsSecretDoesNotPauseSaveOrExit
CompanionDiscoveryRequiresTheSecretAndDoesNotChangeTheWorld LaunchCheckRequiresTheSecretAndDoesNotChangeTheWorld
EOF
  RENAME_FILES=1 rename_map "$SCRATCH/local.map" local

  # Locals and fields that name the local server, in code only.
  cat > "$SCRATCH/local-code.map" <<'EOF'
localHost localServer
localHostFailure localServerFailure
localHostStopFailure localServerStopFailure
EOF
  local files=()
  mapfile -t files < <(git grep -l -w -E 'localHost|localHostFailure|localHostStopFailure' -- "$CLIENT/*.cs")
  (( ${#files[@]} > 0 )) || die "expected the client's localHost field"
  code_words "$SCRATCH/local-code.map" "${files[@]}"
  cat > "$SCRATCH/local-layout.map" <<'EOF'
hostDirectory serverDirectory
hostExecutable serverExecutable
hostPrefix settingPrefix
HostStatus ServerStatus
EOF
  code_words "$SCRATCH/local-layout.map" "$CLIENT/Networking/LocalServer.cs"
  # Locals that held the server's "companion" flag or options.
  mapfile -t files < <(git grep -l -E '\bvar companion = ' -- "$SERVER/*.cs" "$TESTS/*.cs")
  (( ${#files[@]} > 0 )) || die "expected locals named companion"
  echo 'companion localServer' > "$SCRATCH/local-flag.map"
  code_words "$SCRATCH/local-flag.map" "${files[@]}"
  cat > "$SCRATCH/local-connection.map" <<'EOF'
ownHost ownServer
EOF
  code_words "$SCRATCH/local-connection.map" "$CLIENT/Screens/Main/Main.Connection.cs"
  # Test scratch folders named after the companion.
  rewrite_regex '"clankerworld-companion-' '"clankerworld-local-server-'
  rewrite_regex_optional '\bClankerWorld__Tests__CompanionEnvironmentProbe\b' 'ClankerWorld__Tests__LocalServerEnvironmentProbe'
  none_left '\b(?:LocalHost(?:Companion|Layout|Start|Outcome)|CompanionHost|IsCompanionHost|localHost[A-Z]?\w*)\b' "local host names" \
    "$CLIENT/*.cs" "$SERVER/*.cs" "$TESTS/*.cs"
}

# ---------------------------------------------------------------------------
# Stage secret: the value the game writes before each start of its local
# server is the launch secret. Its setting moves to a LocalServer section,
# and the header and file say launch secret too. The problem report keeps
# blanking "companion secret", so older logs stay covered.
# ---------------------------------------------------------------------------
stage_secret() {
  note "stage secret"
  # The setting, in both its key and environment-variable spellings.
  rewrite_regex 'ClankerWorld(:|__)Pairing\1CompanionSecretPath\b' 'ClankerWorld$1LocalServer$1LaunchSecretPath'
  rewrite_regex '\bX-ClankerWorld-Companion-Secret\b' 'X-ClankerWorld-Launch-Secret'
  replace_in "$CLIENT/Networking/LocalServer.cs" 'Path.Combine(DataDirectory, "host", "companion.secret")' \
    'Path.Combine(DataDirectory, "server", "launch.secret")'
  replace_in "$SERVER/Control/Authority/OwnerEndpointContracts.cs" '"The companion secret file must hold' \
    '"The launch secret file must hold'
  cat > "$SCRATCH/secret.map" <<'EOF'
CompanionSecret LaunchSecret
CompanionSecretHeader LaunchSecretHeader
MinimumCompanionSecretLength MinimumLaunchSecretLength
HasCompanionSecret HasLaunchSecret
ReadCompanionSecret ReadLaunchSecret
CompanionSecretFileMustHoldOneLongValue LaunchSecretFileMustHoldOneLongValue
CompanionProof LaunchCheck
IsCompanionAsync CheckLaunchSecretAsync
companionSecretPath launchSecretPath
companionSecret launchSecret
EOF
  rename_map "$SCRATCH/secret.map" secret
  rewrite_regex '\bcompanion secret\b' 'launch secret'
  # The redaction keeps the old name and learns the new one.
  replace_in "$CLIENT/Diagnostics/ProblemReport.cs" '|companion[-_ ]?secret|' '|companion[-_ ]?secret|launch[-_ ]?secret|'
  local report_tests=$TESTS/GodotClient/Diagnostics/ProblemReportTests.cs
  replace_in "$report_tests" '$"debug: companion header {launchSecret}",' \
    '$"debug: launch secret header {launchSecret}",
            "debug: launch_secret=Lw3Xc9Vb1Nm7Qa5Sd2Fg8Hj4Kl6Zx0Pq companion-secret: Rt5Yu7Io9Pa1Sd3Fg5Hj7Kl9Zx1Cv3Bn",'
  replace_in "$report_tests" '"482913", "hunter22", "Zx81Qw7Er6Ty5Ui4Op3As2Df1Gh0JkLmNb",' \
    '"482913", "hunter22", "Zx81Qw7Er6Ty5Ui4Op3As2Df1Gh0JkLmNb", "Lw3Xc9Vb1Nm7Qa5Sd2Fg8Hj4Kl6Zx0Pq",
                     "Rt5Yu7Io9Pa1Sd3Fg5Hj7Kl9Zx1Cv3Bn",'
  none_left '(?i)companion[-_ ]?secret(?!\|)' "companion secret names" "$CLIENT/*.cs" "$SERVER/*.cs" "$TESTS/*.cs" \
    ':!'"$report_tests"
}

# ---------------------------------------------------------------------------
# Stage approval: the second, loopback-only listener that approves pairings,
# recovers lost devices and stops a local server is the approval listener.
# Every server has one, local or remote, so its names no longer say "local",
# and its routes move from /api/v1/local/* to /api/v1/approval/*.
# ---------------------------------------------------------------------------
stage_approval() {
  note "stage approval"
  rewrite_regex 'ClankerWorld(:|__)Pairing\1LocalApprovalPort\b' 'ClankerWorld$1Pairing$1ApprovalPort'
  rewrite_regex '\bapi/v1/local/companion\b' 'api/v1/approval/launch-check'
  rewrite_regex '\bapi/v1/local\b' 'api/v1/approval'
  cat > "$SCRATCH/approval.map" <<'EOF'
OwnerPairingHostOptions ApprovalListenerOptions
=LocalApprovalPort ApprovalPort           # a test's own ApprovalPort constant already names the same port
IsLocalApprovalRequest IsApprovalRequest
LocalApprovalHostRefused ApprovalHostNameRefused
LogLocalApprovalHostRefused LogApprovalHostNameRefused
LocalApprovalAcceptsALiteralLoopbackHostName ApprovalListenerAcceptsALiteralLoopbackHostName
LocalApprovalRefusesAnyOtherHostNameEvenWithTheSecret ApprovalListenerRefusesAnyOtherHostNameEvenWithTheSecret
LocalPairings ApprovalPairings
LocalPairingApprovalHttpRequest PairingApprovalHttpRequest
LocalDeviceRevokeHttpRequest DeviceRevokeHttpRequest
HostRequestRefusals RequestRefusals
pairingHostOptions approvalListenerOptions
localApprovalHost approvalHostName
EOF
  RENAME_FILES=1 rename_map "$SCRATCH/approval.map" approval
  cat > "$SCRATCH/approval-program.map" <<'EOF'
localApprovalPort approvalPort
EOF
  code_words "$SCRATCH/approval-program.map" "$SERVER/Program.cs"
  none_left '\bapi/v1/local\b|\bLocalApproval\w*|\blocalApproval\w*' "approval listener names"
}

# ---------------------------------------------------------------------------
# Stage logs: the server's log events, the log files the game keeps for its
# local server, and the package folder the server ships in.
# ---------------------------------------------------------------------------
stage_logs() {
  note "stage logs"
  rewrite_regex '\bcompanion_host(?= outcome=)' 'local_server'
  rewrite_regex '\bhost_shutdown\b' 'server_shutdown'
  rewrite_regex '\bhost_launch\b' 'server_launch'
  rewrite_regex '\blocal_approval\b' 'approval_listener'
  # <data>/logs/host.log and the launch before it.
  rewrite_regex '\bhost(\.previous)?\.log\b' 'server$1.log'

  # The package folder: <version>/host/ -> <version>/server/.
  local package=scripts/package-windows.sh
  replace_in "$package" 'readonly HOST_EXE=' 'readonly SERVER_EXE='
  replace_in "$package" 'readonly HOST_ASSEMBLY=' 'readonly SERVER_ASSEMBLY='
  replace_in "$package" '${HOST_EXE}' '${SERVER_EXE}'
  replace_in "$package" '${HOST_ASSEMBLY}' '${SERVER_ASSEMBLY}'
  replace_in "$package" '--output "${stage}/host"' '--output "${stage}/server"'
  replace_in "$package" '"${stage}/host/' '"${stage}/server/'
  replace_in "$package" 'self-contained win-x64 host' 'self-contained win-x64 world server'
  replace_in "$package" '# The game starts the host from its host/ folder' '# The game starts its world server from the server/ folder'
  replace_in "$package" 'version and no host:' 'version and no world server:'
  replace_in "$package" '# The game and host must come' '# The game and server must come'
  replace_in "$package" '# Keep host source and version' '# Keep server source and version'
  replace_in "$package" 'staged_host_project' 'staged_server_project'
  replace_in "$package" "printf 'The host executable was not published." "printf 'The server executable was not published."
  replace_in "$package" "printf 'The host is not a Windows x64 PE file" "printf 'The server is not a Windows x64 PE file"
  replace_in "$package" "printf 'The host assembly does not carry" "printf 'The server assembly does not carry"
  none_left '\bhost\b|HOST_' "host wording in $package" "$package"
  replace_in scripts/package-windows-installer.sh '"${game_files}/host/ClankerWorld.Server.exe"' \
    '"${game_files}/server/ClankerWorld.Server.exe"'
  rewrite_regex '`host/ClankerWorld\.Server\.exe`' '`server/ClankerWorld.Server.exe`'
  replace_in docs/development/build-and-test.md 'self-contained `win-x64` host in `host/`' \
    'self-contained `win-x64` world server in `server/`'
  none_left '"host/ClankerWorld|`host/|\bhost/ClankerWorld\.Server' "the old package folder" \
    scripts docs/development src/ClankerWorld.GodotClient
}

# ---------------------------------------------------------------------------
# Stage remote: a world server on another machine, such as a VPS, is a remote
# server. The game's address option and setting name the server, and the
# docs stop calling it the private server or the paired world. The client's
# saved registration keeps its WorldUrl field.
# ---------------------------------------------------------------------------
stage_remote() {
  note "stage remote"
  rewrite_regex '(?<![\w-])--world-url\b' '--server-url'
  rewrite_regex '\bclankerworld/world_url\b' 'clankerworld/server_url'
  replace_in "$CLIENT/project.godot" '[clankerworld]
world_url=' '[clankerworld]
server_url='

  # WorldServerOrigin -> ServerOrigin. A pending submission's binding already
  # has a ServerOrigin property, so its one call names the class in full.
  local pending=$CLIENT/ClientState/OwnerPendingSubmissionStore.cs used
  used=$(git grep -l -w ServerOrigin -- '*.cs' | sort | tr '\n' ' ')
  [[ "$used" == "$CLIENT/ClientState/OwnerPendingSubmissionStore.cs $TESTS/GodotClient/ClientState/OwnerPendingSubmissionStoreTests.cs " ]] ||
    die "ServerOrigin is now used beyond the pending-submission binding: $used"
  replace_in "$pending" 'if (!WorldServerOrigin.TryResolve(serverOrigin.AbsoluteUri' \
    'if (!global::ClankerWorld.GodotClient.ClientState.ServerOrigin.TryResolve(serverOrigin.AbsoluteUri'
  cat > "$SCRATCH/remote.map" <<'EOF'
=WorldServerOrigin ServerOrigin
WorldServerOriginTests ServerOriginTests
TryGetCommandLineWorldUrl TryGetCommandLineServerUrl
ConfiguredWorldUrl ConfiguredServerUrl
worldUrlInput serverUrlInput
EOF
  RENAME_FILES=1 rename_map "$SCRATCH/remote.map" remote
  cat > "$SCRATCH/remote-code.map" <<'EOF'
worldUrl serverUrl
EOF
  code_words "$SCRATCH/remote-code.map" "$CLIENT/Screens/Main/Main.Connection.cs"

  # Docs: the deployment checklist is for a remote world server. Main renamed
  # the page itself (#1813); an older base still has the old name.
  if [[ -f docs/development/private-server-deployment.md ]]; then
    move_path docs/development/private-server-deployment.md docs/development/remote-server-deployment.md
    echo 'private-server-deployment remote-server-deployment' > "$SCRATCH/remote-docs.map"
    rename_map "$SCRATCH/remote-docs.map" remote-docs
  else
    require_file docs/development/remote-server-deployment.md
    note "the deployment checklist is already remote-server-deployment.md"
  fi
  rewrite_regex_optional '\bPrivate-server deployment\b' 'Remote world server deployment'
  rewrite_regex_optional '\bPrivate[- ]server\b' 'Remote world server'
  rewrite_regex_optional '\bprivate[- ]server\b' 'remote world server'
  # The playtest list's marker for a check that needs the remote server.
  rewrite_regex_optional '\bOn the paired world:' 'On a remote server:'
  rewrite_regex_optional '\bIt enters the paired world\b' 'It enters the world on the remote server'
  none_left '(?i)private[- ]server|--world-url|clankerworld/world_url|\bWorldServerOrigin\b' "remote server names"
}

# ---------------------------------------------------------------------------
# Stage model: "hosted" meant a call to a model provider, which the local
# server makes too, so Hosted* names become Model* names. ASP.NET's
# IHostedService and AddHostedService keep theirs. The world event kinds
# hosted_decision_started/_completed/_discarded are saved world events and
# keep their bytes; only log lines are renamed.
# ---------------------------------------------------------------------------
stage_model() {
  note "stage model"
  require_file src/ClankerWorld.Simulation/Cognition/HostedModelClient.cs
  move_path src/ClankerWorld.Simulation/Cognition/HostedModelClient.cs src/ClankerWorld.Simulation/Cognition/ModelClient.cs
  # The game had both names for one check; keep the one that reads well.
  replace_in "$CLIENT/Screens/Main/Main.AgentCard.cs" \
    '    private static bool IsModelProvider(string provider) => IsHostedProvider(provider);
' ''
  cat > "$SCRATCH/model.map" <<'EOF'
IHostedModelClient IModelClient
HostedModelCall ModelCall
HostedModelReply ModelReply
HostedModelPrompt ModelPrompt
HostedModelUnusableReply UnusableModelReply
HostedDecisionOutcome ModelDecisionOutcome
PendingHostedDecision PendingModelDecision
PendingHostedInhabitantIds PendingModelInhabitantIds
pendingHostedInhabitantIds pendingModelInhabitantIds
HostedDecisionsFinished ModelDecisionsFinished
StartHostedDecisions StartModelDecisions
CancelPendingHosted CancelPendingModelCall
CancelPendingHostedDecisions CancelPendingModelCalls
PreviewHostedRequests PreviewModelRequests
HostedRequests ModelRequests
HostedResponse ModelResponse
IsHosted IsModelProvider                     # PlayerDecisionProviders.IsHosted(provider)
=IsHostedProvider IsModelProvider            # the game's check, merged with its old wrapper
pendingHosted pendingModelCalls
activeHostedIds activeModelCallIds
retainedHostedIds retainedModelCallIds
deferHosted deferModelCalls
waitingHosted waitingForModelIds
hostedAgent agentUsesModel
hostedCalls modelCalls
LogHostedDecision LogModelDecisionProgress
AdvanceHostedUntilAsync AdvanceModelDecisionsUntilAsync
AdvanceWithHostedDecisionsAsync AdvanceWithModelDecisionsAsync
WaitForHostedCompletion WaitForModelCompletion
WaitForHostedCompletionAsync WaitForModelCompletionAsync
HostedModelThinkingTests ModelThinkingTests
HostedOldAdultConsentCannotSupplyTheConsentOfANewAdultWhoMustActuallyVisitReadAndAccept AnOldAdultsModelConsentCannotSupplyTheConsentOfANewAdultWhoMustActuallyVisitReadAndAccept
SlowHostedFounderDoesNotHoldWorldOrOtherFounders AFounderWithASlowModelDoesNotHoldWorldOrOtherFounders
ThinkingIsSavedWithTheAgentsModelAndOnlyForHostedAgents ThinkingIsSavedWithTheAgentsModelAndOnlyForAgentsWithAModel
EOF
  # Every other Hosted identifier (test providers and test names) swaps the word.
  local cs=() name
  mapfile -d '' cs < <(eligible_cs_files)
  for name in $(perl "$CODE_WORDS" list '(?-i)Hosted' "${cs[@]}"); do
    case "$name" in IHostedService|AddHostedService) continue ;; esac
    grep -q -E "^=?$name " "$SCRATCH/model.map" && continue
    echo "$name ${name//Hosted/Model}" >> "$SCRATCH/model.map"
  done
  RENAME_FILES=1 rename_map "$SCRATCH/model.map" model
  # rename_map also rewrites the tests' reflection on the renamed field.
  git grep -q -F '"pendingModelCalls"' -- "$TESTS" || die "expected tests to reflect on pendingModelCalls"

  # Locals named hosted, by what they hold in each file.
  echo 'hosted usesModel' > "$SCRATCH/model-uses.map"
  code_words "$SCRATCH/model-uses.map" "$CLIENT/Screens/Main/Main.Providers.cs" \
    "$TESTS/Simulation/World/Runtime/NativeActionRefusalTests.cs"
  echo 'hosted modelProvider' > "$SCRATCH/model-provider.map"
  code_words "$SCRATCH/model-provider.map" "$TESTS/Simulation/World/Runtime/WorldInstructionOrderTests.cs" \
    "$TESTS/Simulation/World/Runtime/WorldDeferredCognitionTests.cs" "$TESTS/Simulation/World/Towns/ModelCivicVoteCandidateTests.cs"
  echo 'hosted modelCalls' > "$SCRATCH/model-calls.map"
  code_words "$SCRATCH/model-calls.map" "$TESTS/Simulation/World/Settlement/Community/SettlementLearningTests.cs" \
    "$TESTS/TestSupport/NativeLandHouseholdFixture.cs"
  echo 'hosted modelWorld' > "$SCRATCH/model-world.map"
  code_words "$SCRATCH/model-world.map" "$TESTS/Simulation/World/Settlement/Family/SettlementContinuityTests.cs"
  local left
  mapfile -d '' cs < <(eligible_cs_files)
  left=$(perl "$CODE_WORDS" list 'hosted' "${cs[@]}" | grep -v -x -e IHostedService -e AddHostedService || true)
  [[ -z "$left" ]] || die "hosted names left in code: $left"

  # Log events. Model decisions and their progress stay separate lines, as do
  # decision calls and conversation calls: their fields differ, and tests read
  # each by name, so merging them would not be trivially safe.
  rewrite_regex '\bcognition_decision(?= tick=)' 'model_decision'
  rewrite_regex '"hosted_decision(?=[ "])' '"model_decision_progress'
  rewrite_regex '\bcognition_provider_call(?= status=)' 'model_call'
  rewrite_regex '\bconversation_call(?= status=)' 'model_conversation_call'
  none_left '\bcognition_provider_call\b|\bconversation_call\b|"hosted_decision[ "]|\bcognition_decision tick' "old model log events"
  # The saved world event kinds are unchanged.
  git grep -q -F '"hosted_decision_completed"' -- src || die "expected the saved hosted_decision_completed event kind"
}

# ---------------------------------------------------------------------------
# Stage logfields: log lines name the agent with agent=, not inhabitant=.
# The signed action payloads' inhabitant= lines are wire format and keep
# their bytes.
# ---------------------------------------------------------------------------
stage_logfields() {
  note "stage logfields"
  local files=()
  # Log templates: inhabitant={InhabitantId}.
  mapfile -t files < <(git grep -l -P '(?<![\w\\])inhabitant=(?=\{)' -- 'src/*.cs')
  (( ${#files[@]} > 0 )) || die "expected inhabitant= log fields in src"
  perl -pi -e 's/(?<![\w\\])inhabitant=(?=\{)/agent=/g' "${files[@]}"
  note "renamed the inhabitant= log field in ${#files[@]} files"
  # Tests that read those lines. A signed payload's line ends with its value.
  mapfile -t files < <(git grep -l -P '(?<=[" ])inhabitant=(?!" \+ Base64Url)' -- "$TESTS/*.cs")
  (( ${#files[@]} > 0 )) || die "expected tests that read the inhabitant= log field"
  perl -pi -e 's/(?<=[" ])inhabitant=(?!" \+ Base64Url)/agent=/g' "${files[@]}"
  note "updated ${#files[@]} tests that read the field"
  local left
  left=$(git grep -n 'inhabitant=' -- 'src/*.cs' "$TESTS/*.cs" | grep -v -E '"\\ninhabitant=" \+ EncodeRequired|"inhabitant=" \+ Base64Url' || true)
  [[ -z "$left" ]] || die "inhabitant= left outside the signed payloads:
$left"
}

# ---------------------------------------------------------------------------
# Stage entrypoints (the one behaviour change): each game version's folder
# carries a package.json naming its programs,
#   {"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"},
# and the launcher and the game read the entry points from the version they
# check or start, instead of hard-coding this version's paths for every
# installed version. A version without package.json is older: its server is
# host/ClankerWorld.Viewer.exe or host/ClankerWorld.Server.exe.
# ---------------------------------------------------------------------------
stage_entrypoints() {
  note "stage entrypoints"
  local layout=$CLIENT/Launcher/LauncherLayout.cs store=$CLIENT/Launcher/GameVersionStore.cs
  local settings=$CLIENT/Launcher/LauncherSettings.cs local=$CLIENT/Networking/LocalServer.cs
  git grep -q -w PackageEntryPoints && die "PackageEntryPoints already exists"
  grep -q '^using System.Text.Json;' "$layout" && die "unexpected using in $layout"
  replace_in "$layout" 'namespace ClankerWorld.GodotClient.Launcher;
' 'using System.Text.Json;

namespace ClankerWorld.GodotClient.Launcher;
'
  cat >> "$layout" <<'EOF'

/// <summary>
/// The programs in one game version's folder, as its own <c>package.json</c>
/// names them: <c>{"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"}</c>.
/// The launcher reads them from the version it checks or starts, so a newer
/// package can move or rename its programs without making installed versions
/// look damaged. A version packaged before <c>package.json</c> has its server
/// in <c>host/</c>, under the name it had then.
/// </summary>
public sealed record PackageEntryPoints(string Game, string Server)
{
    public const string FileName = "package.json";

    private static readonly string[] OlderServers =
        [Path.Combine("host", "ClankerWorld.Viewer.exe"), Path.Combine("host", "ClankerWorld.Server.exe")];

    /// <summary>
    /// A version folder's entry points as paths relative to it, or null when
    /// its <c>package.json</c> cannot be read or names an unsafe path. Without
    /// one, the server is the first older path <paramref name="isPresent"/>
    /// accepts, or the oldest if none is.
    /// </summary>
    public static PackageEntryPoints? Read(string directory, Func<string, bool> isPresent)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            return new(LauncherLayout.GameExecutable, OlderServers.FirstOrDefault(isPresent) ?? OlderServers[0]);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                RelativePath(document.RootElement, "game") is { } game &&
                RelativePath(document.RootElement, "server") is { } server
                    ? new(game, server) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The entry points of a version on disk, as when starting it.</summary>
    public static PackageEntryPoints? ReadInstalled(string directory) =>
        Read(directory, relative => File.Exists(Path.Combine(directory, relative)));

    private static string? RelativePath(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        value.GetString() is { } relative && GameVersionStore.IsSafeRelativePath(relative)
            ? relative.Replace('/', Path.DirectorySeparatorChar) : null;
}
EOF
  note "added PackageEntryPoints to $layout"

  # The installed version's game program comes from its own entry points.
  replace_in "$store" '    public string Executable => Path.Combine(Directory, LauncherLayout.GameExecutable);' \
    '    public string Executable => Path.Combine(Directory,
        PackageEntryPoints.ReadInstalled(Directory)?.Game ?? LauncherLayout.GameExecutable);'
  replace_in "$store" '            var names = manifest.Select(item => item.Relative).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!names.Contains(LauncherLayout.GameExecutable) ||
                !names.Contains(Path.Combine("host", "ClankerWorld.Server.exe")))
                return [LauncherLayout.PackageManifest];' \
'            var names = manifest.Select(item => item.Relative).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // This version'"'"'s own entry points, so another version'"'"'s layout never looks damaged.
            if (PackageEntryPoints.Read(version.Directory, names.Contains) is not { } entryPoints)
                return [PackageEntryPoints.FileName];
            if (!names.Contains(entryPoints.Game) || !names.Contains(entryPoints.Server))
                return [LauncherLayout.PackageManifest];'
  replace_in "$store" '            if (!File.Exists(Path.Combine(staging, LauncherLayout.GameExecutable)))' \
    '            if (PackageEntryPoints.ReadInstalled(staging) is not { } entryPoints ||
                !File.Exists(Path.Combine(staging, entryPoints.Game)))'
  replace_in "$store" '    private static bool IsSafeRelativePath(string relative) =>' \
    '    internal static bool IsSafeRelativePath(string relative) =>'

  replace_in "$settings" '        var directory = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        if (!File.Exists(executable) || !File.Exists(Path.Combine(directory, LocalServerLayout.ServerFolderName, LocalServerLayout.WindowsServerFileName)))
            throw new GameInstallException("Choose a locally built portable game with its host folder. Your saves are safe.");' \
'        var directory = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        if (!File.Exists(executable) || PackageEntryPoints.ReadInstalled(directory) is not { } entryPoints ||
            !File.Exists(Path.Combine(directory, entryPoints.Server)))
            throw new GameInstallException("Choose a locally built portable game with its world server. Your saves are safe.");'
  if ! grep -q 'LocalServerLayout\|Networking' <(sed '1,/^namespace/d' "$settings"); then
    replace_in "$settings" 'using ClankerWorld.GodotClient.Networking;
' ''
  fi

  # The game finds its own server the same way.
  replace_in "$local" '    public const string ServerFolderName = "host";
    public const string WindowsServerFileName = "ClankerWorld.Server.exe";

' ''
  rewrite_regex_in "$local" '(?s)    /// <summary>\n    /// Finds the host shipped in the <c>host</c> folder beside the game\..*?\n    \}\n' \
'    /// <summary>
    /// Finds the world server that the package.json beside the game names. A
    /// development build or a server-only setup has none, and keeps
    /// connecting to whatever server the player chose.
    /// </summary>
    public static LocalServerLayout? FindBundled(string gameExecutablePath, string localApplicationData)
    {
        var gameDirectory = Path.GetDirectoryName(Path.GetFullPath(gameExecutablePath));
        if (gameDirectory is null || string.IsNullOrWhiteSpace(localApplicationData) ||
            PackageEntryPoints.ReadInstalled(gameDirectory) is not { } entryPoints)
            return null;
        var serverExecutable = Path.Combine(gameDirectory, entryPoints.Server);
        return File.Exists(serverExecutable)
            ? new LocalServerLayout(serverExecutable, [], Path.GetDirectoryName(serverExecutable)!,
                Path.Combine(localApplicationData, "ClankerWorld"))
            : null;
    }
'
  grep -q '^using ClankerWorld.GodotClient.Launcher;' "$local" ||
    replace_in "$local" 'using ClankerWorld.GodotClient.ClientState;
' 'using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Launcher;
'

  # The package says where its programs are.
  replace_in scripts/package-windows.sh 'printf '"'"'Writing the package manifest\n'"'"'' \
'if [[ "${launcher}" == false ]]; then
    # The launcher reads each version'"'"'s entry points from here (GameVersionStore).
    printf '"'"'{"game": "%s", "server": "server/%s"}\n'"'"' "${game_exe}" "${SERVER_EXE}" > "${stage}/package.json"
fi

printf '"'"'Writing the package manifest\n'"'"''
  replace_in docs/development/build-and-test.md 'a short `README.txt` and a `manifest.sha256` of
every file.' 'a short `README.txt`, a `package.json` naming its entry points
(`{"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"}`)
and a `manifest.sha256` of every file. The launcher and the game read the
entry points from the version they check or start, so a newer package can
move its programs without making an installed version look damaged; a
version without `package.json` is older, with its server in `host/`.'
  replace_in docs/development/build-and-test.md 'Local builds need their
bundled `server/ClankerWorld.Server.exe` too.' 'Local builds need the world
server their `package.json` names too.'

  replace_in docs/development/device-pairing.md 'The game does this itself when its folder has `server/ClankerWorld.Server.exe`
and no `--server-url` argument (`LocalServer`).' 'The game does this itself when the `package.json` in its folder names a world
server that is there, and no `--server-url` argument is given (`LocalServer`).'

  # Players see the new folder and log name, and that older versions still open.
  local entry=changes/1364-prototype-names.md
  require_file "$entry"
  grep -q 'server` folder' "$entry" && die "$entry already mentions the server folder"
  perl -0pi -e 's/\n\z/ The game now keeps its world server in a `server` folder and its log in `logs\\server.log`, and the launcher still recognizes game versions installed before this change.\n/' "$entry"
  (( $(wc -l < "$entry") == 1 )) || die "$entry should stay one bullet"

  stage_entrypoints_tests
}

stage_entrypoints_tests() {
  local tests=$TESTS/Server/Control/LauncherTests.cs local_tests=$TESTS/Server/Control/LocalServerTests.cs
  # Packages are laid out as the package script makes them now.
  rewrite_regex_in "$tests" '\("host/ClankerWorld\.Server\.exe", ("[^"]*")\)' 'EntryPoints, ("server/ClankerWorld.Server.exe", $1)'
  rewrite_regex_in "$tests" 'Path\.Combine\(installed\.Directory, "host", ' 'Path.Combine(installed.Directory, "server", '
  rewrite_regex_in "$tests" 'Path\.Combine\(repaired\.Directory, "host", ' 'Path.Combine(repaired.Directory, "server", '
  replace_in "$tests" 'Assert.Equal([Path.Combine("host", "ClankerWorld.Server.exe")], GameVersionStore.Verify(installed));' \
    'Assert.Equal([Path.Combine("server", "ClankerWorld.Server.exe")], GameVersionStore.Verify(installed));'
  replace_in "$tests" '            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(executable)!, "host"));
            File.WriteAllText(executable, "local executable");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(executable)!, "host", "ClankerWorld.Server.exe"), "host");' \
'            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(executable)!, "server"));
            File.WriteAllText(executable, "local executable");
            Assert.Throws<GameInstallException>(() => GameStarter.StartLocal(executable, "launcher.exe", settings, layout));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(executable)!, EntryPoints.Path), EntryPoints.Text);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(executable)!, "server", "ClankerWorld.Server.exe"), "server");'
  replace_in "$tests" '    private const string Page = "https://github.com/ClankerWorldOrg/ClankerWorld/releases/tag/";
' '    private const string Page = "https://github.com/ClankerWorldOrg/ClankerWorld/releases/tag/";
    private static readonly (string Path, string Text) EntryPoints =
        ("package.json", """{"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"}""");
'
  replace_in "$tests" '    [Fact]
    public void VersionsSortNewestReleaseFirstAndOnlySafeNamesParse()' \
'    [Fact]
    public void EachVersionNamesItsOwnEntryPointsInPackageJson()
    {
        var root = Directory.CreateTempSubdirectory("launcher-entry-points-");
        try
        {
            using var http = new HttpClient();
            var store = new GameVersionStore(new LauncherLayout(root.FullName), http);
            var version = GameVersionName.Parse("0.1.0-alpha.3")!;
            var zip = Path.Combine(root.FullName, "package.zip");
            File.WriteAllBytes(zip, Package(version, ("ClankerWorld.exe", "game"),
                ("package.json", """{"game": "ClankerWorld.exe", "server": "bin/World Server.exe"}"""),
                ("bin/World Server.exe", "server")));
            var installed = store.InstallPackage(zip, version);
            Assert.Empty(GameVersionStore.Verify(installed));
            Assert.Equal(new PackageEntryPoints("ClankerWorld.exe", Path.Combine("bin", "World Server.exe")),
                PackageEntryPoints.ReadInstalled(installed.Directory));
            Assert.Equal(Path.Combine(installed.Directory, "ClankerWorld.exe"), installed.Executable);

            // A server the manifest does not list, or an unsafe path, is damage, never a different program.
            File.WriteAllBytes(zip, Package(version, ("ClankerWorld.exe", "game"),
                ("package.json", """{"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"}""")));
            Assert.Throws<GameInstallException>(() => store.InstallPackage(zip, version));
            File.WriteAllBytes(zip, Package(version, ("ClankerWorld.exe", "game"),
                ("package.json", """{"game": "ClankerWorld.exe", "server": "../outside.exe"}""")));
            Assert.Throws<GameInstallException>(() => store.InstallPackage(zip, version));
            Assert.Empty(GameVersionStore.Verify(Assert.Single(store.Installed())));
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("host/ClankerWorld.Viewer.exe")]
    [InlineData("host/ClankerWorld.Server.exe")]
    public void AVersionWithoutPackageJsonKeepsTheServerPathItShippedWith(string server)
    {
        var root = Directory.CreateTempSubdirectory("launcher-older-version-");
        try
        {
            using var http = new HttpClient();
            var store = new GameVersionStore(new LauncherLayout(root.FullName), http);
            var version = GameVersionName.Parse("0.1.0-alpha.1")!;
            var zip = Path.Combine(root.FullName, "package.zip");
            File.WriteAllBytes(zip, Package(version, ("ClankerWorld.exe", "game"), (server, "server")));
            var installed = store.InstallPackage(zip, version);
            var serverPath = server.Replace('"'"'/'"'"', Path.DirectorySeparatorChar);
            Assert.Empty(GameVersionStore.Verify(installed));
            Assert.Equal(new PackageEntryPoints("ClankerWorld.exe", serverPath), PackageEntryPoints.ReadInstalled(installed.Directory));
            Assert.Equal(Path.Combine(installed.Directory, "ClankerWorld.exe"), installed.Executable);

            File.WriteAllText(Path.Combine(installed.Directory, serverPath), "changed");
            Assert.Equal([serverPath], GameVersionStore.Verify(installed));
            File.Delete(Path.Combine(installed.Directory, serverPath));
            Assert.Equal([serverPath], GameVersionStore.Verify(installed));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void VersionsSortNewestReleaseFirstAndOnlySafeNamesParse()'

  # The game finds its own server from its package.json.
  replace_in "$local_tests" '            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(game)!, "host"));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(game)!, "host", LocalServerLayout.WindowsServerFileName), "");
            var layout = LocalServerLayout.FindBundled(game, localAppData);

            Assert.NotNull(layout);' \
'            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(game)!, "server"));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(game)!, "server", "ClankerWorld.Server.exe"), "");
            Assert.Null(LocalServerLayout.FindBundled(game, localAppData));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(game)!, "package.json"),
                """{"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"}""");
            var layout = LocalServerLayout.FindBundled(game, localAppData);

            Assert.NotNull(layout);
            Assert.Equal(Path.Combine(Path.GetDirectoryName(game)!, "server", "ClankerWorld.Server.exe"), layout.FileName);
            Assert.Equal(Path.Combine(Path.GetDirectoryName(game)!, "server"), layout.ServerDirectory);'
  none_left '\bServerFolderName\b|\bWindowsServerFileName\b|\bHOST_EXE\b' "hard-coded package paths" "$CLIENT/*.cs" "$TESTS/*.cs" scripts
}

# ---------------------------------------------------------------------------
# Stage config: the ClankerWorld:Runtime section held three kinds of setting.
# It splits into World (which world, its seed and its save), Clock (whether
# the clock runs, when it stops and how strictly a failed tick is treated)
# and Providers (models and their files). The three StatePath keys get names
# that say which file they are. Each key keeps its meaning; both the setting
# and the environment-variable spellings change, so a remote server's
# systemd unit must be updated with the release.
# ---------------------------------------------------------------------------
stage_config() {
  note "stage config"
  cat > "$SCRATCH/config.map" <<'EOF'
Runtime:Seed World:Seed
Runtime:WorldMode World:Mode
Runtime:StatePath World:SavePath                      # the active world's checkpoint
?Runtime:WorldStatePath World:GameWorldSavePath       # fixture mode only (gone with the fixture world, #1738)
?Runtime:LegacyStatePath World:FixtureSavePath        # the fixture world's checkpoint (gone with it, #1738)
Runtime:AdvanceScript Clock:Run
Runtime:ClientPresenceTimeoutSeconds Clock:ClientPresenceTimeoutSeconds
Runtime:StrictTickFailures Clock:StrictTickFailures
Runtime:DecisionProvider Providers:DecisionProvider
Runtime:JevModel Providers:JevModel
Runtime:Model Providers:Model
Runtime:OpenAiModel Providers:OpenAiModel
Runtime:OllamaCloudModel Providers:OllamaCloudModel
Runtime:AnthropicModel Providers:AnthropicModel
Runtime:ProviderStatePath Providers:ConfigurationPath # model assignments and protected keys
Runtime:ProviderUsagePath Providers:UsagePath
Pairing:StatePath Pairing:AuthorityPath               # paired devices
EOF
  local old new old_section old_key new_section new_key optional left
  : > "$SCRATCH/config-done.tsv"
  while read -r old new; do
    optional=; [[ $old == \?* ]] && { optional=1; old=${old#\?}; }
    old_section=${old%%:*} old_key=${old#*:} new_section=${new%%:*} new_key=${new#*:}
    eligible_files | xargs -0 -r grep -qP -- "ClankerWorld(:|__)${new_section}\\1${new_key}\\b" &&
      die "setting ClankerWorld:$new is already used"
    # A setting the server no longer reads is not renamed: a new name for it
    # would promise it does something.
    if ! git grep -q -P "ClankerWorld:${old_section}:${old_key}\\b" -- "$SERVER/*.cs"; then
      [[ -n $optional ]] || die "the server no longer reads ClankerWorld:$old"
      note "skipped ClankerWorld:$old: the server no longer reads it"
      # The unit is a fresh-install template: it sets nothing the server ignores.
      if grep -q -E "^Environment=ClankerWorld__${old_section}__${old_key}=" deploy/clankerworld-server.service; then
        perl -ni -e "print unless /^Environment=ClankerWorld__${old_section}__${old_key}=/" deploy/clankerworld-server.service
        note "removed ClankerWorld__${old_section}__${old_key} from the deploy unit"
      fi
      left=$(git grep -l -P "ClankerWorld(:|__)${old_section}(:|__)${old_key}\\b" -- . ':!CHANGELOG.md' ':!changes' ':!docs/archive' || true)
      [[ -z $left ]] || die "ClankerWorld:$old is no longer read but still named in: $left"
      continue
    fi
    rewrite_regex "ClankerWorld(:|__)${old_section}\\1${old_key}\\b" "ClankerWorld\$1${new_section}\$1${new_key}"
    printf '%s\t%s\n' "$old" "$new" >> "$SCRATCH/config-done.tsv"
  done < <(sed 's/#.*//' "$SCRATCH/config.map" | awk 'NF == 2')
  none_left 'ClankerWorld(:|__)Runtime(:|__)' "ClankerWorld:Runtime settings" "$SERVER/*.cs" "$CLIENT/*.cs" "$TESTS/*.cs" docs .github scripts
  none_left 'ClankerWorld(:|__)Pairing(:|__)StatePath\b' "ClankerWorld:Pairing:StatePath"

  replace_in deploy/clankerworld-server.service 'Description=ClankerWorld world host' 'Description=ClankerWorld world server'

  # Names in code that still say the clock runs a script.
  echo 'advanceRuntime runClock' > "$SCRATCH/config-program.map"
  if [[ -n $(perl "$CODE_WORDS" list '(?-i)^advanceFixture$' "$SERVER/Program.cs") ]]; then
    echo 'advanceFixture runFixtureClock' >> "$SCRATCH/config-program.map"
  fi
  code_words "$SCRATCH/config-program.map" "$SERVER/Program.cs"
  echo 'advanceScript runClock' > "$SCRATCH/config-tests.map"
  local files=()
  mapfile -t files < <(git grep -l -w advanceScript -- "$TESTS/*.cs")
  (( ${#files[@]} > 0 )) || die "expected the test server's advanceScript option"
  code_words "$SCRATCH/config-tests.map" "${files[@]}"

  # The deployment checklist names one key by its last part.
  replace_in docs/development/remote-server-deployment.md 'When `ProviderUsagePath` is unset' 'When `Providers:UsagePath` is unset'
}

# ---------------------------------------------------------------------------
# Stage folders: the server project's folders say what each part does, and
# each namespace matches its folder:
#   Control/Authority -> Pairing       Observation/Persistence -> Saves
#   Control/Content   -> Content       Observation/Worlds      -> Worlds
#   Observation/Assets-> Content       Observation/Runtime     -> Runtime
#   Control/Providers -> Providers     Control/World           -> Runtime
#   Observation/Projections -> Observation (the projections only)
#   Endpoints stays.
# Every file that used ClankerWorld.Server.Control or .Observation gets the
# new namespaces of the types it names from them, and nothing else, so a
# test that also imports the game's own copies of the contracts gains no
# ambiguity. Test folders keep their layout; their namespace is
# ClankerWorld.Tests throughout.
# ---------------------------------------------------------------------------
stage_folders() {
  note "stage folders"
  cat > "$SCRATCH/folders.map" <<'EOF'
Control/Authority Pairing
Control/Content Content
Control/Providers Providers
Control/World Runtime
Observation/Assets Content
Observation/Persistence Saves
Observation/Projections Observation
Observation/Runtime Runtime
Observation/Worlds Worlds
Endpoints Endpoints
EOF
  # Every server source file must have a destination.
  local file stray=() from to
  while IFS= read -r file; do
    [[ $file == "$SERVER/Program.cs" ]] && continue
    from=${file#"$SERVER"/}; from=${from%/*}
    grep -q -x -E "$from [A-Za-z]+" <(sed 's/#.*//' "$SCRATCH/folders.map") || stray+=("$file")
  done < <(git ls-files -- "$SERVER/*.cs")
  (( ${#stray[@]} == 0 )) || die "server files outside the known folders: ${stray[*]}"
  git grep -q -E '^namespace ClankerWorld\.Server\.(Control|Observation|Endpoints)\.' -- '*.cs' &&
    die "a sub-namespace of the server exists; extend stage_folders"

  # Record each type's old and new namespace, then move the files.
  : > "$SCRATCH/types.tsv"
  : > "$SCRATCH/moves.tsv"
  local ns_old ns_new target type
  while read -r from to; do
    require_dir "$SERVER/$from"
    while IFS= read -r file; do
      [[ $file == *.cs ]] || continue
      ns_old=$(sed -n 's/^namespace \(ClankerWorld\.Server\.[A-Za-z]*\);$/\1/p' "$file")
      [[ -n $ns_old ]] || die "no file-scoped server namespace in $file"
      ns_new=ClankerWorld.Server.$to
      for type in $(perl -ne 'print "$1\n" if /^(?:public |internal |file )?(?:(?:static|sealed|abstract|readonly|partial|ref|unsafe) )*(?:record struct|record class|record|class|struct|enum|interface) ([A-Za-z_][A-Za-z0-9_]*)/' "$file" | sort -u); do
        printf '%s\t%s\t%s\n' "$type" "$ns_old" "$ns_new" >> "$SCRATCH/types.tsv"
      done
      target=$SERVER/$to/${file##*/}
      printf '%s\t%s\t%s\t%s\n' "$file" "$target" "$ns_old" "$ns_new" >> "$SCRATCH/moves.tsv"
    done < <(git ls-files -- "$SERVER/$from/")
  done < <(sed 's/#.*//' "$SCRATCH/folders.map" | awk 'NF == 2')
  sort -u "$SCRATCH/types.tsv" -o "$SCRATCH/types.tsv"
  local dupes
  dupes=$(cut -f1 "$SCRATCH/types.tsv" | sort | uniq -d)
  [[ -z "$dupes" ]] || die "types declared in two server namespaces: $dupes"
  local old_file new_file
  while IFS=$'\t' read -r old_file new_file ns_old ns_new; do
    [[ $old_file == "$new_file" ]] && continue
    move_path "$old_file" "$new_file"
  done < "$SCRATCH/moves.tsv"
  while IFS=$'\t' read -r old_file new_file ns_old ns_new; do
    perl -pi -e 's/^namespace ClankerWorld\.Server\.[A-Za-z]+;$/namespace '"$ns_new"';/' "$new_file"
  done < "$SCRATCH/moves.tsv"
  find "$SERVER" -depth -type d -empty -delete

  # Usings: each file that was in, or imported, an old namespace imports the
  # new namespaces of the types it names from those, except its own.
  local cs=()
  mapfile -t cs < <(git ls-files -- '*.cs' | while read -r file; do
    is_protected "$file" && continue
    if [[ $file == "$SERVER/"* ]] || grep -q -E '^using ClankerWorld\.Server\.(Control|Observation|Endpoints);' "$file" ||
      grep -q -E '\bClankerWorld\.Server\.(Control|Observation)\.[A-Z]|\b(Control|Observation)\.[A-Z]\w+(>|\(|\.|\s)' "$file"; then
      echo "$file"
    fi
  done)
  TYPES="$SCRATCH/types.tsv" MOVES="$SCRATCH/moves.tsv" perl -e '
    use strict; use warnings;
    my (%old, %new);
    open my $t, "<", $ENV{TYPES} or die;
    while (<$t>) { chomp; my ($type, $o, $n) = split /\t/; $old{$type} = $o; $new{$type} = $n; }
    my $alt = join "|", sort { length($b) <=> length($a) || $a cmp $b } keys %new;
    my $types = qr/\b($alt)\b/;
    my %oldns = map { $_ => 1 } values %old;
    my %wasin;
    open my $mv, "<", $ENV{MOVES} or die;
    while (<$mv>) { chomp; my ($from, $to, $o, $n) = split /\t/; $wasin{$to} = $o; }
    for my $file (@ARGV) {
      open my $in, "<", $file or die "cannot read $file\n";
      local $/; my $text = <$in>; close $in;
      my $original = $text;
      my ($own) = $text =~ /^namespace ([\w.]+);/m;
      $own //= "";
      # Which old namespaces this file could see: the one it was in and its usings.
      my %visible;
      $visible{$wasin{$file}} = 1 if $wasin{$file};
      $visible{$1} = 1 while $text =~ /^using (ClankerWorld\.Server\.\w+);$/mg;
      # Fully qualified names move with their type; partly qualified ones,
      # such as Control.ApprovalListenerOptions, lose the old namespace.
      $text =~ s/\bClankerWorld\.Server\.(?:Control|Observation)\.($alt)\b/$new{$1} . "." . $1/ge;
      $text =~ s/(?<![\w.])(?:Control|Observation)\.($alt)\b/$visible{$old{$1}} = 1; $1/ge;
      my %need;
      my $body = $text; $body =~ s/^using [^\n]*\n//mg;
      while ($body =~ /$types/g) { my $type = $1; $need{$new{$type}} = 1 if $visible{$old{$type}}; }
      # Program.cs and others name the endpoint namespace for its extension methods.
      $need{"ClankerWorld.Server.Endpoints"} = 1 if $text =~ /^using ClankerWorld\.Server\.Endpoints;$/m;
      delete $need{$own};
      my @usings = map { "using $_;\n" } sort keys %need;
      my @lines = split /(?<=\n)/, $text;
      my ($first, @kept);
      for my $line (@lines) {
        if ($line =~ /^using ClankerWorld\.Server\.\w+;\n\z/) { $first //= scalar @kept; next; }
        push @kept, $line;
      }
      if (@usings) {
        if (!defined $first) {
          my $last;
          for my $i (0 .. $#kept) { $last = $i if $kept[$i] =~ /^using [\w.]+;\n\z/; last if $kept[$i] =~ /^(?:namespace|public|internal|\[)/; }
          $first = defined $last ? $last + 1 : 0;
          splice @kept, $first, 0, ($first == 0 ? (@usings, "\n") : @usings);
        } else {
          splice @kept, $first, 0, @usings;
        }
      }
      $text = join "", @kept;
      next if $text eq $original;
      open my $out, ">", $file or die; print $out $text; close $out;
    }
  ' "${cs[@]}"
  note "checked the server namespaces and usings in ${#cs[@]} files"
  none_left '\bClankerWorld\.Server\.Control\b|\bnamespace ClankerWorld\.Server\.Observation;' "old server namespaces" \
    "$SERVER/Pairing/*.cs" "$SERVER/Content/*.cs" "$SERVER/Providers/*.cs" "$SERVER/Runtime/*.cs" "$SERVER/Saves/*.cs" "$SERVER/Worlds/*.cs" "$SERVER/Endpoints/*.cs"
  none_left '\bClankerWorld\.Server\.Control\b' "the Control namespace" '*.cs'

  # Paths that name the old folders: docs, scripts and the label rules.
  while read -r from to; do
    [[ $from == "$to" ]] && continue
    rewrite_regex_optional "\\bClankerWorld\\.Server/${from}/" "ClankerWorld.Server/${to}/"
  done < <(sed 's/#.*//' "$SCRATCH/folders.map" | awk 'NF == 2')
  none_left 'ClankerWorld\.Server/(Control|Observation/(Assets|Persistence|Projections|Runtime|Worlds))\b' "old server folder paths"
}

# ---------------------------------------------------------------------------
# Stage wording: prose that still calls the local server a companion,
# bundled or standalone host, or a model call hosted. In C# only comments
# change (never strings: player-facing text is out of scope here); in docs
# the whole text. "Cloud-hosted AI models" and "a hosted game account" mean
# something else and are left alone.
# ---------------------------------------------------------------------------
stage_wording() {
  note "stage wording"
  # Device pairing's section on the server the game starts.
  replace_in docs/development/device-pairing.md '## Host started by the game' '## Local server started by the game'
  rewrite_regex '#host-started-by-the-game\b' '#local-server-started-by-the-game'
  rewrite_regex '\[Host started by the game\]' '[Local server started by the game]'
  replace_in_optional docs/development/device-pairing.md 'A host the game starts on the player'"'"'s own PC also needs a
   one-launch secret ([companion host](#local-server-started-by-the-game)).' 'The local server, which the game starts on the player'"'"'s own PC, also needs a
   launch secret ([local server](#local-server-started-by-the-game)).'
  replace_in_optional docs/development/device-pairing.md 'the game starts its own host on the player'"'"'s PC.' \
    'the game starts its own world server, the local server, on the player'"'"'s PC.'
  replace_in_optional docs/development/device-pairing.md 'Before starting the host, the game writes' 'Before starting it, the game writes'
  replace_in_optional docs/development/device-pairing.md 'and starts the
host with:' 'and starts the
server with:'
  replace_in_optional docs/development/device-pairing.md 'A server host
started on its own still reads both.' 'A server
started on its own still reads both.'
  replace_in_optional docs/development/device-pairing.md 'The server host
started without these settings behaves as before' 'A server
started without these settings behaves as before'
  replace_in_optional docs/development/device-pairing.md 'so its host takes keys only from' 'so its local server takes keys only from'
  replace_in_optional docs/development/device-pairing.md 'also pauses a companion
world;' 'also pauses a local
server'"'"'s world;'
  replace_in_optional "$TESTS/Server/Control/ApprovalListenerTests.cs" \
    '/// <summary>A host the game starts on the player'"'"'s own PC (#1563).</summary>' \
    '/// <summary>The approval listener, and the launch secret of a local server (#1563).</summary>'
  replace_in_optional docs/game-design/world.md '**On a player'"'"'s computer, the host is a bundled companion process.**' \
    '**On a player'"'"'s computer, the local server is a separate process bundled with the game.**'
  replace_in_optional docs/game-design/world.md 'the companion-process host or the first package' 'the local server process or the first package'
  replace_in_optional docs/game-design/world.md 'starts the bundled host in the background' 'starts its local server in the background'
  replace_in_optional docs/game-design/world.md 'If the host cannot start, the game says so' 'If the local server cannot start, the game says so'

  # Phrases, in docs and in C# comments.
  cat > "$SCRATCH/wording.pl" <<'EOF'
s/\bworld companion host\b/local server/g;
s/\b(a|A) host the game start(?:s|ed) on the player's own PC\b/$1 local server/g;
s/\b[Cc]ompanion hosts?\b/local server/g;
s/\bthe companion holding\b/the local server holding/g;
s/\bthe companion alive\b/the local server alive/g;
s/\bthe companion's\b/the local server's/g;
s/\bCompanion shutdown\b/Local server shutdown/g;
s/\bThe companion relaunch fixture\b/The local server relaunch fixture/g;
s/\b[Bb]undled host\b/local server/g;
s/\bstandalone host\b/server started on its own/g;
s/\bthe game's own host\b/the game's local server/g;
s/\b(its|their|the game's) own host\b/$1 own local server/g;
s/\bhosted-provider\b/model-provider/g;
s/\bhosted agent (assignment|credential)(s?)\b/agent model $1$2/g;
s/\bhosted personal model(s?)\b/personal model$1/g;
s/\bHosted-decision\b/Model-decision/g;
s/\bhosted (model|provider|decision|call|request|task|work|cognition|agent|account|reply|replies|answer|attempt|choice|language model)(s?)\b/($1 eq "model" || $1 eq "language model") ? "$1$2" : "model $1$2"/ge;
s/\bHosted (provider|decision|call|request|model)(s?)\b/Model $1$2/g;
s/\bModel model(s?)\b/Model$1/g;
EOF
  local files=() file
  mapfile -d '' files < <(eligible_files | xargs -0 -r grep -lZ -i -E 'companion|bundled host|standalone host|own host|hosted' || true)
  for file in "${files[@]}"; do
    case "$file" in
      *.md) perl -pi "$SCRATCH/wording.pl" "$file" ;;
      *.cs) WORDING="$SCRATCH/wording.pl" perl -pi -e '
              BEGIN { open my $w, "<", $ENV{WORDING} or die; local $/; $rules = <$w>; }
              if (m{^(.*?)(//.*)$} && $1 !~ /"/) {
                my ($code, $comment) = ($1, $2);
                my $line = do { local $_ = $comment; eval $rules; die $@ if $@; $_ };
                $_ = $code . $line . "\n";
              }
            ' "$file" ;;
    esac
  done
  note "rewrote host and hosted wording in docs and comments"
  none_left '(?i)\bcompanion host|\bbundled host|\bstandalone host' "local server wording" '*.md' '*.cs'
}

# ---------------------------------------------------------------------------
# Stage notes: remote world servers are installed fresh with this build (the
# owner's decision), so the deployment page's install section, which
# rename.sh adds, lists the names a new systemd unit and environment use,
# taken from the deploy unit and the server itself. No old names are kept as
# aliases anywhere; a short list of older log names helps read old logs.
# ---------------------------------------------------------------------------
stage_notes() {
  note "stage notes"
  local page=docs/development/remote-server-deployment.md headings
  require_file "$page"
  headings=$(grep -n -P '^#+ (.*\b[Ii]nstall(?:s|ing|ed)?\b.*|Upgrading a remote world server across the rename)$' "$page" || true)
  [[ -n $headings ]] || die "$page has no install section to list the new names in; run the newest rename.sh first"
  (( $(wc -l <<< "$headings") == 1 )) || die "$page has more than one install section:
$headings"
  local heading=${headings#*:}
  heading=${heading#"${heading%%[!#]*}"}; heading=${heading# }
  local unit=deploy/clankerworld-server.service block=$SCRATCH/notes.md setting settings=()
  require_file "$unit"
  # Every setting the server reads, by its own name.
  mapfile -t settings < <(git grep -h -o -P '"ClankerWorld:[A-Za-z]+:[A-Za-z]+"' -- "$SERVER/*.cs" | tr -d '"' | sort -u)
  (( ${#settings[@]} > 5 )) || die "expected the server's ClankerWorld settings"
  {
    printf '\n'
    printf 'The world server names (#1364). A new unit sets these environment\n'
    printf 'variables, as the [systemd template](../../deploy/clankerworld-server.service) does:\n\n'
    grep -E '^Environment=ClankerWorld__' "$unit" | sed 's/^Environment=//; s/=.*//' | while read -r setting; do
      printf -- '- `%s`\n' "$setting"
    done
    printf '\nThe server also reads these settings, which a unit sets only when needed:\n\n'
    for setting in "${settings[@]}"; do
      grep -q -E "^Environment=${setting//:/__}=" "$unit" || printf -- '- `%s`\n' "$setting"
    done
    cat <<'EOF'

The approval listener, on `ClankerWorld:Pairing:ApprovalPort` and never
forwarded, serves `POST /api/v1/approval/pairings`,
`POST /api/v1/approval/pairings/{pairingId}/approve` and
`POST /api/v1/approval/devices/{deviceId}/revoke`. Only a local server, which
the game starts, also serves `POST /api/v1/approval/shutdown` and
`GET /api/v1/approval/launch-check`, and needs the `X-ClankerWorld-Launch-Secret`
header and `ClankerWorld:LocalServer:LaunchSecretPath`.

Log lines name the agent with `agent=`. The server's events include
`server_shutdown`, `approval_listener`, `model_decision`,
`model_decision_progress`, `model_call` and `model_conversation_call`. Older
logs used `host_shutdown`, `local_approval`, `cognition_decision`,
`hosted_decision`, `cognition_provider_call`, `conversation_call` and
`inhabitant=` for these.
EOF
  } > "$block"
  HEADING="$heading" BLOCK="$block" perl -0pi -e '
    BEGIN { open my $b, "<", $ENV{BLOCK} or die; local $/; $add = <$b>; }
    my $h = quotemeta $ENV{HEADING};
    /^(#+) $h[ \t]*\n/m or die "section not found\n";
    my $level = length $1;
    my $start = $+[0];
    my $rest = substr($_, $start);
    my $end = $rest =~ /^#{1,$level} /m ? $start + $-[0] : length $_;
    my $before = substr($_, 0, $end); $before =~ s/\n*\z/\n/;
    my $after = substr($_, $end);
    $_ = $before . $add . ($after eq "" ? "" : "\n" . $after);
  ' "$page"
  note "listed the new names in $page, section '$heading'"
}

# ---------------------------------------------------------------------------
ALL_STAGES="local secret approval logs remote model logfields entrypoints config folders wording notes"
for stage in $STAGES; do
  [[ " $ALL_STAGES " == *" $stage "* ]] || die "unknown stage: $stage"
done
for stage in $ALL_STAGES; do
  if has_stage "$stage"; then "stage_$stage"; fi
done
note "done; review with git status and git diff, then build and test"
