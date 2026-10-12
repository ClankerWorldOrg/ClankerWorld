#!/usr/bin/perl
# Rewrites or lists C# identifiers in code, never inside string or char
# literals or comments. Interpolation holes in $"..." strings are code.
#
#   perl cs-code-words.pl list PATTERN FILE...   identifiers matching PATTERN (case-insensitive)
#   perl cs-code-words.pl rewrite MAPFILE FILE... rewrite identifiers by "old new" lines
use strict; use warnings;
my ($mode, $arg, @files) = @ARGV;
my (%map, %found, $pattern);
if ($mode eq 'rewrite') {
  open my $m, '<', $arg or die "cannot read $arg\n";
  while (<$m>) { s/#.*//; my @f = split; next unless @f; die "bad map line: $_" unless @f == 2; $map{$f[0]} = $f[1]; }
} elsif ($mode eq 'list') {
  $pattern = qr/$arg/i;
} else { die "unknown mode $mode\n"; }
my $changed;

sub word {
  my ($w) = @_;
  if ($mode eq 'list') { $found{$w}++ if $w =~ $pattern; return $w; }
  if (exists $map{$w}) { $changed++; return $map{$w}; }
  return $w;
}

# An interpolated string body after its opening quote; $verbatim for $@"...".
sub interpolated {
  my ($ref, $verbatim) = @_;
  my $out = '';
  while (pos($$ref) < length $$ref) {
    if ($$ref =~ /\G(\{\{|\}\})/gc) { $out .= $1; next; }
    if ($verbatim ? $$ref =~ /\G("")/gc : $$ref =~ /\G(\\.)/gcs) { $out .= $1; next; }
    if ($$ref =~ /\G"/gc) { return $out . '"'; }
    if ($$ref =~ /\G\{/gc) { $out .= '{' . code($ref, 1) . '}'; next; }
    $$ref =~ /\G(.)/gcs; $out .= $1;
  }
  return $out;
}

# Code until end of text, or (in a hole) until the closing brace at depth 0.
sub code {
  my ($ref, $hole) = @_;
  my $out = '';
  my $depth = 0;
  while (pos($$ref) < length $$ref) {
    if ($hole) {
      if ($$ref =~ /\G\}/gc) { if ($depth == 0) { return $out; } $depth--; $out .= '}'; next; }
      if ($$ref =~ /\G\{/gc) { $depth++; $out .= '{'; next; }
    }
    if ($$ref =~ /\G(\/\/[^\n]*)/gc) { $out .= $1; next; }
    if ($$ref =~ /\G(\/\*.*?\*\/)/gcs) { $out .= $1; next; }
    if ($$ref =~ /\G(\$*"""+.*?"""+)/gcs) { $out .= $1; next; }                 # raw strings
    if ($$ref =~ /\G((?:\$@|@\$)")/gc) { $out .= $1 . interpolated($ref, 1); next; }
    if ($$ref =~ /\G(@"(?:[^"]|"")*")/gc) { $out .= $1; next; }                # verbatim
    if ($$ref =~ /\G\$"/gc) { $out .= '$"' . interpolated($ref, 0); next; }
    if ($$ref =~ /\G("(?:[^"\\\n]|\\.)*")/gc) { $out .= $1; next; }            # regular
    if ($$ref =~ /\G('(?:[^'\\\n]|\\.)+')/gc) { $out .= $1; next; }             # char
    if ($$ref =~ /\G(@?)([A-Za-z_]\w*)/gc) { $out .= $1 . word($2); next; }
    if ($$ref =~ /\G(\d[\w.]*)/gc) { $out .= $1; next; }
    $$ref =~ /\G(.)/gcs; $out .= $1;
  }
  return $out;
}

for my $file (@files) {
  open my $in, '<', $file or die "cannot read $file\n";
  local $/; my $text = <$in>; close $in;
  $changed = 0;
  pos($text) = 0;
  my $out = code(\$text, 0);
  die "lexer lost text in $file\n" if $mode eq 'rewrite' && !$changed && $out ne $text;
  if ($mode eq 'rewrite' && $changed) {
    open my $o, '>', $file or die "cannot write $file\n"; print $o $out; close $o;
  }
}
if ($mode eq 'list') { print "$_\n" for sort keys %found; }
