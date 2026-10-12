#!/usr/bin/perl
# C# helpers for stages/client.sh. A small lexer that knows strings
# (regular, verbatim, interpolated with code holes, raw), char literals and
# comments, so nothing inside a literal or comment is mistaken for code.
#
#   perl client-cs.pl members FILE
#       Lists the class-level members of FILE's one partial class:
#       "FIRST-LAST KIND NAME", with 1-based line numbers.
#   perl client-cs.pl move SRC DST NAME...
#       Moves every member called NAME (overloads move together) from the
#       class in SRC to the end of the class in DST. DST is created from
#       SRC's usings, namespace and class line when it does not exist, with
#       SUMMARY (environment) as its class summary. Fails when a NAME is not
#       found or when SRC would be left empty. Members keep their order.
#   perl client-cs.pl summary FILE TEXT
#       Replaces (or adds) the one-line class summary above the class.
#   perl client-cs.pl rewrite MAPFILE FILE...
#       Rewrites identifiers in code by "old new" lines (not in strings or
#       comments; interpolation holes are code). Fails when an old name is
#       found nowhere in the files given.
#   perl client-cs.pl types FILE...
#       Prints "NAMESPACE TYPE" for each top-level type declared.
#   perl client-cs.pl uses FILE TYPESFILE
#       Prints the namespaces whose types (from TYPESFILE, "NAMESPACE TYPE"
#       lines) FILE names unqualified in code, one per line.
use strict;
use warnings;

my @OPS = ('<<=', '??=', '=>', '==', '!=', '<=', '>=', '&&', '||', '??', '?.', '+=', '-=', '*=', '/=',
  '%=', '&=', '|=', '^=', '++', '--', '->', '::');
my $OPRE = join '|', map { quotemeta } @OPS;

sub slurp {
  my ($file) = @_;
  open my $in, '<', $file or die "client-cs: cannot read $file\n";
  local $/;
  my $text = <$in>;
  close $in;
  return defined $text ? $text : '';
}

sub spew {
  my ($file, $text) = @_;
  open my $out, '>', $file or die "client-cs: cannot write $file\n";
  print $out $text;
  close $out;
}

# Tokens: [type, text, hole] where type is ws, comment, pp, str, char, id,
# num or p, and hole is how deep in interpolation holes the token sits.
sub lex {
  my ($text, $file) = @_;
  my @t;
  pos($text) = 0;
  lex_code(\$text, \@t, 0, $file);
  my $joined = join '', map { $_->[1] } @t;
  die "client-cs: lexer lost text in $file\n" if $joined ne $text;
  return \@t;
}

sub lex_code {
  my ($r, $t, $hole, $file) = @_;
  my $nest = 0;
  while (pos($$r) < length $$r) {
    if ($hole && $nest == 0) {
      return 1 if $$r =~ /\G\}/gc;
      if ($$r =~ /\G(:(?!:)[^}]*)/gc) { push @$t, ['str', $1, $hole]; next; }
    }
    if ($$r =~ /\G(\s+)/gc) { push @$t, ['ws', $1, $hole]; next; }
    if ($$r =~ /\G(\/\/[^\n]*)/gc) { push @$t, ['comment', $1, $hole]; next; }
    if ($$r =~ /\G(\/\*.*?\*\/)/gcs) { push @$t, ['comment', $1, $hole]; next; }
    if (!$hole && $$r =~ /\G(#[^\n]*)/gc) { push @$t, ['pp', $1, $hole]; next; }
    if ($$r =~ /\G(\$*)("{3,})/gc) {
      my ($dollars, $quotes) = ($1, $2);
      $$r =~ /\G(.*?)\Q$quotes\E/gcs or die "client-cs: unterminated raw string in $file\n";
      push @$t, ['str', $dollars . $quotes . $1 . $quotes, $hole];
      next;
    }
    if ($$r =~ /\G(\$\@"|\@\$")/gc) { lex_interp($r, $t, $hole, 1, $1, $file); next; }
    if ($$r =~ /\G(\@"(?:[^"]|"")*")/gc) { push @$t, ['str', $1, $hole]; next; }
    if ($$r =~ /\G(\$")/gc) { lex_interp($r, $t, $hole, 0, $1, $file); next; }
    if ($$r =~ /\G("(?:[^"\\\n]|\\.)*")/gc) { push @$t, ['str', $1, $hole]; next; }
    if ($$r =~ /\G('(?:[^'\\\n]|\\.)+')/gc) { push @$t, ['char', $1, $hole]; next; }
    if ($$r =~ /\G(\@?[A-Za-z_][A-Za-z0-9_]*)/gc) { push @$t, ['id', $1, $hole]; next; }
    if ($$r =~ /\G(\d\w*(?:\.\d\w*)?)/gc) { push @$t, ['num', $1, $hole]; next; }
    if ($$r =~ /\G($OPRE)/gc) { push @$t, ['p', $1, $hole]; next; }
    $$r =~ /\G(.)/gcs;
    my $c = $1;
    if ($hole) {
      $nest++ if $c eq '(' || $c eq '[' || $c eq '{';
      $nest-- if $c eq ')' || $c eq ']' || $c eq '}';
    }
    push @$t, ['p', $c, $hole];
  }
  die "client-cs: unterminated interpolation hole in $file\n" if $hole;
  return 0;
}

sub lex_interp {
  my ($r, $t, $hole, $verbatim, $prefix, $file) = @_;
  my $buf = $prefix;
  while (pos($$r) < length $$r) {
    if ($$r =~ /\G(\{\{|\}\})/gc) { $buf .= $1; next; }
    if ($verbatim ? $$r =~ /\G("")/gc : $$r =~ /\G(\\.)/gcs) { $buf .= $1; next; }
    if ($$r =~ /\G"/gc) { push @$t, ['str', $buf . '"', $hole]; return; }
    if ($$r =~ /\G\{/gc) {
      push @$t, ['str', $buf . '{', $hole];
      lex_code($r, $t, $hole + 1, $file) or die "client-cs: unterminated interpolation hole in $file\n";
      $buf = '}';
      next;
    }
    $$r =~ /\G(.)/gcs;
    $buf .= $1;
  }
  die "client-cs: unterminated interpolated string in $file\n";
}

sub significant { my ($tok) = @_; return $tok->[0] ne 'ws' && $tok->[0] ne 'comment'; }

my %KEYWORD = map { $_ => 1 } qw(public private protected internal static readonly const volatile unsafe
  extern override virtual abstract sealed new partial async ref out in params required file void int long
  float double decimal bool string char byte sbyte short ushort uint ulong object dynamic var nint nuint event
  implicit explicit operator this scoped);
my %TYPEWORD = map { $_ => 1 } qw(class struct interface enum record delegate);

# Splits a file into head (usings and namespace), the class summary and
# declaration, members and tail. Offsets are character offsets.
sub parse_class {
  my ($file) = @_;
  my $text = slurp($file);
  my $t = lex($text, $file);
  my @off;
  my $pos = 0;
  for my $tok (@$t) { push @off, $pos; $pos += length $tok->[1]; }
  my ($depth, $nsEnd, $typeAt, $open, $close) = (0);
  for my $i (0 .. $#$t) {
    my $tok = $t->[$i];
    next if $tok->[2] || !significant($tok);
    die "client-cs: preprocessor line in $file is not supported\n" if $tok->[0] eq 'pp';
    if ($tok->[0] eq 'p' && $tok->[1] eq '{') {
      $open = $i if $depth == 0 && defined $typeAt && !defined $open;
      $depth++;
    } elsif ($tok->[0] eq 'p' && $tok->[1] eq '}') {
      $depth--;
      if ($depth == 0 && defined $open && !defined $close) { $close = $i; }
    } elsif ($depth == 0 && $tok->[0] eq 'id') {
      if ($tok->[1] eq 'namespace' && !defined $nsEnd) {
        my $j = $i;
        $j++ until $j > $#$t || ($t->[$j][0] eq 'p' && $t->[$j][1] eq ';');
        die "client-cs: $file needs a file-scoped namespace\n" if $j > $#$t;
        $nsEnd = $j;
      } elsif ($TYPEWORD{$tok->[1]}) {
        die "client-cs: $file declares more than one type\n" if defined $close || (defined $typeAt && !defined $open);
        $typeAt //= $i;
      }
    }
  }
  die "client-cs: no class found in $file\n" unless defined $open && defined $close && defined $nsEnd;
  my $headEnd = $off[$nsEnd] + 1;
  $headEnd++ while substr($text, $headEnd, 1) =~ /\s/;
  # The class line starts at the start of the line holding its first modifier.
  my $declLine = rindex($text, "\n", $off[$typeAt]) + 1;
  my $openEnd = $off[$open] + 1;
  substr($text, $openEnd) =~ /\A[ \t]*\n/ or die "client-cs: text after the class brace in $file\n";
  $openEnd += $+[0];
  my $closeStart = rindex($text, "\n", $off[$close]) + 1;
  substr($text, $closeStart, $off[$close] - $closeStart) =~ /\A[ \t]*\z/
    or die "client-cs: the class's closing brace in $file is not on its own line\n";
  my %c = (file => $file, text => $text, head => substr($text, 0, $headEnd),
    summary => substr($text, $headEnd, $declLine - $headEnd),
    decl => substr($text, $declLine, $openEnd - $declLine),
    after => substr($text, $closeStart), members => []);
  # Members of the class body.
  # $d counts braces inside the member, $p parentheses and brackets outside
  # braces. A member that is an expression (it has = or => outside braces,
  # parentheses and brackets) ends only at its semicolon.
  my ($d, $p, $expr, $start, $hasCode) = (0, 0, 0, $openEnd, 0);
  my $i = $open + 1;
  my $finish = sub {
    my ($endTok) = @_;
    my $end = $off[$endTok] + length $t->[$endTok][1];
    if (substr($text, $end) =~ /\A[ \t]*(?:\/\/[^\n]*)?\n/) { $end += $+[0]; }
    else { die "client-cs: two members share a line in $file near offset $end\n"; }
    push @{$c{members}}, member(\%c, $t, \@off, $start, $end);
    $start = $end;
    ($hasCode, $expr, $p) = (0, 0, 0);
  };
  while ($i < $close) {
    my $tok = $t->[$i];
    if ($tok->[2] || !significant($tok)) { $i++; next; }
    die "client-cs: preprocessor line in $file is not supported\n" if $tok->[0] eq 'pp';
    $hasCode = 1;
    my $v = $tok->[0] eq 'p' ? $tok->[1] : '';
    if ($v eq '{') { $d++; }
    elsif ($v eq '}') {
      $d--;
      if ($d == 0 && !$expr) {
        my $j = $i + 1;
        $j++ while $j < $close && !significant($t->[$j]);
        my $next = $j < $close ? $t->[$j][1] : '}';
        if ($next eq ';') { $i = $j; $finish->($i); $i++; next; }
        if ($next eq '=') { $expr = 1; }
        else { $finish->($i); $i++; next; }
      }
    } elsif ($d == 0) {
      if ($v eq '(' || $v eq '[') { $p++; }
      elsif ($v eq ')' || $v eq ']') { $p--; }
      elsif ($p == 0 && ($v eq '=' || $v eq '=>')) { $expr = 1; }
      elsif ($p == 0 && $v eq ';') { $finish->($i); $i++; next; }
    }
    $i++;
  }
  die "client-cs: unfinished member at the end of $file\n" if $hasCode;
  $c{tail} = substr($text, $start, $closeStart - $start);
  return \%c;
}

sub member {
  my ($c, $t, $off, $start, $end) = @_;
  my $raw = substr($c->{text}, $start, $end - $start);
  $raw =~ /\A((?:[ \t]*\n)*)/;
  my $sep = ($1 =~ tr/\n//);
  my $content = substr($raw, length $1);
  # The member's significant tokens, without leading attributes.
  my @sig;
  for my $i (0 .. $#$t) {
    next if $off->[$i] < $start || $off->[$i] >= $end;
    my $tok = $t->[$i];
    next if $tok->[2] || !significant($tok);
    push @sig, $tok;
  }
  while (@sig && $sig[0][1] eq '[') {
    my $n = 0;
    while (@sig) {
      my $tok = shift @sig;
      $n++ if $tok->[1] eq '[';
      $n-- if $tok->[1] eq ']';
      last if $n == 0;
    }
  }
  my ($kind, $name) = name_of(\@sig, $c->{file});
  my $first = 1 + (substr($c->{text}, 0, $start + length($raw) - length($content)) =~ tr/\n//);
  my $last = (substr($c->{text}, 0, $end) =~ tr/\n//);
  return { sep => $sep, content => $content, kind => $kind, name => $name, first => $first, last => $last };
}

sub name_of {
  my ($sig, $file) = @_;
  my $n = @$sig;
  for my $i (0 .. $n - 1) {
    my $tok = $sig->[$i];
    last if $tok->[1] =~ /^(?:\{|=>|=|;)$/;
    if ($tok->[0] eq 'id' && $TYPEWORD{$tok->[1]}) {
      my $j = $i + 1;
      $j++ while $j < $n && $sig->[$j][0] eq 'id' && ($sig->[$j][1] eq 'struct' || $sig->[$j][1] eq 'class');
      if ($tok->[1] eq 'delegate') { last; }
      return ('type', $sig->[$j][1]) if $j < $n && $sig->[$j][0] eq 'id';
    }
  }
  my ($paren, $lastId) = (0, undef);
  for (my $i = 0; $i < $n; $i++) {
    my $tok = $sig->[$i];
    if ($tok->[1] eq '(' ) {
      my $prev = $i > 0 ? $sig->[$i - 1] : undef;
      if ($prev && $prev->[0] eq 'id' && !$KEYWORD{$prev->[1]}) { return ('method', $prev->[1]); }
      if ($prev && $prev->[1] eq '>') {
        my ($k, $angle) = ($i - 1, 0);
        while ($k >= 0) {
          $angle++ if $sig->[$k][1] eq '>';
          $angle-- if $sig->[$k][1] eq '<';
          last if $angle == 0;
          $k--;
        }
        return ('method', $sig->[$k - 1][1]) if $k > 0 && $sig->[$k - 1][0] eq 'id' && !$KEYWORD{$sig->[$k - 1][1]};
      }
      # A tuple type: skip to its closing parenthesis.
      my $p = 0;
      for (; $i < $n; $i++) {
        $p++ if $sig->[$i][1] eq '(';
        $p-- if $sig->[$i][1] eq ')';
        last if $p == 0;
      }
      next;
    }
    if ($tok->[1] eq '[' && $lastId && $lastId eq 'this') { return ('indexer', 'this'); }
    if ($tok->[1] =~ /^(?:\{|=>|=|;)$/) {
      die "client-cs: cannot name a member in $file\n" unless defined $lastId;
      return ($tok->[1] eq '{' || $tok->[1] eq '=>' ? 'property' : 'field', $lastId);
    }
    $lastId = $tok->[1] if $tok->[0] eq 'id';
  }
  die "client-cs: cannot name a member in $file\n";
}

sub render_members {
  my ($members, $leading) = @_;
  my $out = '';
  my $firstDone = 0;
  for my $m (@$members) {
    my $sep = $firstDone ? ($m->{sep} > 1 ? 1 : $m->{sep}) : $leading;
    $out .= "\n" x $sep . $m->{content};
    $firstDone = 1;
  }
  return $out;
}

sub render_class {
  my ($c) = @_;
  my $tail = $c->{tail};
  return $c->{head} . $c->{summary} . $c->{decl} . render_members($c->{members}, 0) . $tail . $c->{after};
}

sub usings_of {
  my ($head) = @_;
  return [ $head =~ /^(using [^\n]*;)\n/mg ];
}

# Adds missing using lines to a class head, keeping a sorted block sorted.
sub merge_usings {
  my ($c, $extra) = @_;
  my $have = usings_of($c->{head});
  my %seen = map { $_ => 1 } @$have;
  my @add = grep { !$seen{$_}++ } @$extra;
  return unless @add;
  my $sorted = join("\n", @$have) eq join("\n", sort @$have);
  my @all = $sorted ? sort(@$have, @add) : (@$have, @add);
  my $block = join '', map { "$_\n" } @all;
  if (@$have) {
    $c->{head} =~ s/\A((?:[^\n]*\n)*?)((?:using [^\n]*;\n)+)/$1$block/ or die "client-cs: cannot find the usings in $c->{file}\n";
  } else {
    $c->{head} = $block . "\n" . $c->{head};
  }
}


# Adds one using line to a file: in order when its using block is sorted,
# otherwise after the last using line.
sub add_using {
  my ($text, $line, $file) = @_;
  my @have = $text =~ /^(using [^\n]*;)\n/mg;
  @have = grep { !/=/ } @have;
  unless (@have) {
    $text =~ s/\A((?:\/\/[^\n]*\n)*)/$1$line\n/ or die "client-cs: cannot add a using to $file\n";
    $text =~ s/\A((?:\/\/[^\n]*\n)*$line\n)(?!\n)/$1\n/;
    return $text;
  }
  my $sorted = join("\n", @have) eq join("\n", sort @have);
  my $anchor;
  if ($sorted) {
    my @after = grep { $_ gt $line } @have;
    if (@after) {
      my $next = $after[0];
      $text =~ s/^(\Q$next\E\n)/$line\n$1/m;
      return $text;
    }
  }
  my @near = grep { /^using ClankerWorld\.GodotClient[.;]/ } @have;
  my $last = @near ? $near[-1] : $have[-1];
  $text =~ s/^(\Q$last\E\n)/$1$line\n/m;
  return $text;
}

# A class summary: one line when it fits in 100 columns, otherwise wrapped
# at 78 like the other summaries.
sub summary_block {
  my ($text) = @_;
  my $one = "/// <summary>$text</summary>\n";
  return $one if length($one) <= 101;
  my @lines;
  my $line = '';
  for my $word (split ' ', $text) {
    if (length($line) && length("/// $line $word") > 78) { push @lines, $line; $line = $word; }
    else { $line = length($line) ? "$line $word" : $word; }
  }
  push @lines, $line if length $line;
  return "/// <summary>\n" . join('', map { "/// $_\n" } @lines) . "/// </summary>\n";
}

my $mode = shift @ARGV // '';
if ($mode eq 'members') {
  my $c = parse_class($ARGV[0]);
  printf "%d-%d %s %s\n", $_->{first}, $_->{last}, $_->{kind}, $_->{name} for @{$c->{members}};
} elsif ($mode eq 'move' || $mode eq 'merge') {
  my ($src, $dst, @names) = @ARGV;
  die "client-cs: $mode needs SRC DST" . ($mode eq 'move' ? " NAME..." : '') . "\n"
    unless defined $dst && ($mode eq 'merge' ? !@names : @names);
  my $s = parse_class($src);
  # A name may be optional (?Name) or one of several (Old|New), for members
  # another stage may already have renamed or removed.
  my (%want, @specs);
  for my $spec (@names) {
    my $optional = $spec =~ s/^\?//;
    my @alts = split /\|/, $spec;
    push @specs, { alts => \@alts, optional => $optional, label => $spec };
    $want{$_} = 0 for @alts;
  }
  my (@keep, @moved);
  my $gap;    # the blank lines before the first of the members just moved
  for my $m (@{$s->{members}}) {
    if ($mode eq 'merge' || exists $want{$m->{name}}) {
      $want{$m->{name}}++;
      $gap //= $m->{sep};
      push @moved, $m;
    } else {
      # A member that followed moved ones keeps the blank line that came before them.
      $m->{sep} = $gap if defined $gap && $gap > $m->{sep};
      undef $gap;
      push @keep, $m;
    }
  }
  for my $spec (@specs) {
    my @found = grep { $want{$_} } @{$spec->{alts}};
    die "client-cs: $src has no member named $spec->{label}\n" if !@found && !$spec->{optional};
    die "client-cs: $src has members named both " . join(' and ', @found) . "\n" if @found > 1;
    print STDERR "client.sh: $src has no $spec->{label}; skipped\n" if !@found;
  }
  die "client-cs: moving @names would leave $src empty\n" if $mode eq 'move' && !@keep;
  die "client-cs: nothing to move from $src\n" unless @moved;
  $s->{members} = \@keep;
  if (-e $dst) {
    my $d = parse_class($dst);
    die "client-cs: $src and $dst declare different classes\n" if $d->{decl} ne $s->{decl};
    merge_usings($d, usings_of($s->{head}));
    # Moved fields join the fields at the top of the class; the rest go at the end.
    my @fields = grep { $_->{kind} eq 'field' } @moved;
    my @others = grep { $_->{kind} ne 'field' } @moved;
    my @existing = @{$d->{members}};
    my $lead = 0;
    $lead++ while $lead < @existing && $existing[$lead]{kind} eq 'field';
    my @top = @existing[0 .. $lead - 1];
    my @rest = @existing[$lead .. $#existing];
    if (@fields) {
      $fields[0]{sep} = @top ? 1 : 0;
      $rest[0]{sep} = 1 if @rest && !@top;
    }
    my $body = render_members([@top, @fields, @rest], 0);
    $body .= render_members(\@others, (@top || @fields || @rest) ? 1 : 0) if @others;
    my $tail = $d->{tail} =~ /\S/ ? $d->{tail} : '';
    spew($dst, $d->{head} . $d->{summary} . $d->{decl} . $body . $tail . $d->{after});
  } else {
    my $head = $s->{head};
    if (defined $ENV{USINGS}) {
      my $block = join '', map { "$_\n" } grep { /\S/ } split /\n/, $ENV{USINGS};
      $head =~ s/\A(?:using [^\n]*;\n)+/$block/ or die "client-cs: $src has no using block to replace\n";
    }
    my $summary = defined $ENV{SUMMARY} && length $ENV{SUMMARY} ? summary_block($ENV{SUMMARY}) : '';
    my $decl = defined $ENV{DECL} ? "$ENV{DECL}\n{\n" : $s->{decl};
    spew($dst, $head . $summary . $decl . render_members(\@moved, 0) . "}\n");
  }
  if ($mode eq 'merge') { unlink $src or die "client-cs: cannot remove $src\n"; }
  else { spew($src, render_class($s)); }
  printf STDERR "client.sh: moved %d members of %s to %s\n", scalar(@moved), $src, $dst;
} elsif ($mode eq 'roundtrip') {
  for my $file (@ARGV) {
    my $c = parse_class($file);
    my $again = render_class($c);
    if ($again ne $c->{text}) { print "$file differs\n"; spew("$file.roundtrip", $again) if $ENV{KEEP}; }
  }
} elsif ($mode eq 'namespaces') {
  # Splits the types of OLD_NS that now live in other namespaces (the
  # TYPESFILE lists every client type as "NAMESPACE TYPE") and fixes the
  # files that could see OLD_NS: qualified names, aliases of OLD_NS and the
  # using lines. MOVED lists files whose own namespace moved out of OLD_NS.
  my ($typesFile, @files) = @ARGV;
  my $old = $ENV{OLD_NS} or die "client-cs: OLD_NS is required\n";
  my %moved = map { $_ => 1 } split ' ', ($ENV{MOVED} // '');
  my (%nsOf, %all);
  for my $line (split /\n/, slurp($typesFile)) {
    my ($n, $type) = split ' ', $line;
    next unless defined $type;
    $nsOf{$type}{$n} = 1;
    $all{$n} = 1;
  }
  my @split = grep { $_ ne $old && index($_, ($old =~ s/\.[^.]+$//r) . '.') == 0 } sort keys %all;
  my %splitSet = map { $_ => 1 } @split;
  # A type that moved: declared in one of the split namespaces and not in OLD_NS.
  my $movedTo = sub {
    my ($type) = @_;
    return undef unless $nsOf{$type} && !$nsOf{$type}{$old};
    my @to = grep { $splitSet{$_} } sort keys %{$nsOf{$type}};
    return @to == 1 ? $to[0] : undef;
  };
  for my $file (@files) {
    my $text = slurp($file);
    my $before = $text;
    # Qualified names.
    $text =~ s/\b\Q$old\E\.(\w+)/ my $type = $1; my $to = $movedTo->($type); defined $to ? "$to.$type" : "$old.$type" /ge;
    # Aliases of the old namespace.
    my @aliases = $text =~ /^using (\w+) = \Q$old\E;\n/mg;
    for my $alias (@aliases) {
      my %added;
      $text =~ s/(?<![\w.])\Q$alias\E\.(\w+)/ my $type = $1; my $to = $movedTo->($type);
        if (defined $to) { my $a = $alias . (split m{\.}, $to)[-1]; $added{$a} = $to; "$a.$type" } else { "$alias.$type" } /ge;
      my $lines = join '', map { "using $_ = $added{$_};\n" } sort keys %added;
      $text =~ s/^(using \Q$alias\E = \Q$old\E;\n)/$1$lines/m;
      my $uses = () = $text =~ /(?<![\w.])\Q$alias\E\./g;
      $text =~ s/^using \Q$alias\E = \Q$old\E;\n//m unless $uses;
    }
    my ($ns) = $text =~ /^namespace ([\w.]+);/m;
    $ns //= '';
    my $saw = $moved{$file} || $ns eq $old || $text =~ /^using \Q$old\E;$/m;
    if ($saw) {
      my $t = lex($text, $file);
      my %used;
      my $prev = '';
      for my $tok (@$t) {
        next unless significant($tok);
        if ($tok->[0] eq 'id' && $prev ne '.' && $prev ne '::' && $prev ne '?.' && $nsOf{$tok->[1]}) {
          $used{$_} = 1 for keys %{$nsOf{$tok->[1]}};
        }
        $prev = $tok->[1];
      }
      for my $n (@split, $old) {
        my $need = $used{$n} && $n ne $ns;
        my $has = $text =~ /^using \Q$n\E;\n/m;
        if ($has && !$need && $n eq $old) { $text =~ s/^using \Q$n\E;\n//m; }
        elsif (!$has && $need) { $text = add_using($text, "using $n;", $file); }
      }
    }
    spew($file, $text) if $text ne $before;
  }
} elsif ($mode eq 'uid') {
  # A Godot resource UID for a new script, derived from its path so reruns
  # give the same text: Godot's base-34 digits (a-y, 0-8), lowest first.
  require Digest::SHA;
  require Math::BigInt;
  my ($path) = @ARGV;
  my $hex = substr(Digest::SHA::sha256_hex("ClankerWorld script uid: $path"), 0, 16);
  my $id = Math::BigInt->from_hex($hex)->band(Math::BigInt->from_hex('7fffffffffffffff'));
  my $text = '';
  do {
    my ($q, $c) = $id->copy->bdiv(34);
    $c = $c->numify;
    $text .= $c < 25 ? chr(ord('a') + $c) : chr(ord('0') + $c - 25);
    $id = $q;
  } while (!$id->is_zero);
  print "uid://$text\n";
} elsif ($mode eq 'summary') {
  my ($file, $text) = @ARGV;
  my $c = parse_class($file);
  die "client-cs: ${file}'s class summary is not one plain summary block\n"
    if $c->{summary} =~ /\S/ && $c->{summary} !~ /\A(?:[ \t]*\/\/\/[^\n]*\n)+\z/;
  $c->{summary} = summary_block($text);
  spew($file, render_class($c));
} elsif ($mode eq 'rewrite') {
  my ($mapFile, @files) = @ARGV;
  my (%map, %found);
  for my $line (split /\n/, slurp($mapFile)) {
    $line =~ s/#.*//;
    my @f = split ' ', $line;
    next unless @f;
    die "client-cs: bad map line: $line\n" unless @f == 2;
    die "client-cs: duplicate map entry: $f[0]\n" if exists $map{$f[0]};
    $map{$f[0]} = $f[1];
  }
  my %new = map { $_ => 1 } values %map;
  # Comments are rewritten too, for names that can only be code (camelCase
  # or PascalCase with two humps), so prose words stay as they are.
  my @codeLike = grep { /[a-z0-9][A-Z]/ } sort { length($b) <=> length($a) || $a cmp $b } keys %map;
  my $commentRe = @codeLike ? do { my $alt = join '|', map { quotemeta } @codeLike; qr/(?<![\w])($alt)(?![\w])/ } : undef;
  my %out;
  for my $file (@files) {
    my $t = lex(slurp($file), $file);
    my $changed = 0;
    my ($prev, $prevprev) = ('', '');
    for my $tok (@$t) {
      # With UNQUALIFIED set, only names used without a qualifier (or after
      # this.) are members of the class being renamed; obj.Name is another's.
      my $qualified = $ENV{UNQUALIFIED} && ($prev eq '.' || $prev eq '?.' || $prev eq '::') && $prevprev ne 'this';
      if (significant($tok)) { ($prevprev, $prev) = ($prev, $tok->[1]); }
      if ($tok->[0] eq 'comment' && $commentRe) {
        $changed += ($tok->[1] =~ s/$commentRe/$map{$1}/g);
        next;
      }
      next unless $tok->[0] eq 'id';
      die "client-cs: new name $tok->[1] is already used in $file\n" if $new{$tok->[1]} && !exists $map{$tok->[1]};
      next unless exists $map{$tok->[1]};
      next if $qualified;
      $found{$tok->[1]}++;
      $tok->[1] = $map{$tok->[1]};
      $changed++;
    }
    $out{$file} = join '', map { $_->[1] } @$t if $changed;
  }
  my @missing = sort grep { !$found{$_} } keys %map;
  die "client-cs: names not found in code: @missing\n" if @missing;
  spew($_, $out{$_}) for sort keys %out;
  printf STDERR "client.sh: renamed %d identifiers\n", scalar(keys %map);
} elsif ($mode eq 'types') {
  for my $file (@ARGV) {
    my $t = lex(slurp($file), $file);
    my ($depth, $ns, $pending) = (0, undef, undef);
    for my $i (0 .. $#$t) {
      my $tok = $t->[$i];
      next if $tok->[2] || !significant($tok);
      if ($tok->[1] eq '{') { $depth++; next; }
      if ($tok->[1] eq '}') { $depth--; next; }
      next unless $depth == 0 && $tok->[0] eq 'id';
      if ($tok->[1] eq 'namespace') {
        my $j = $i + 1;
        my $name = '';
        while ($j <= $#$t && $t->[$j][1] ne ';' && $t->[$j][1] ne '{') { $name .= $t->[$j][1] if significant($t->[$j]); $j++; }
        $ns = $name;
      } elsif ($TYPEWORD{$tok->[1]}) {
        next if $i > 0 && grep { $_ eq 'record' } map { $t->[$_][1] } grep { significant($t->[$_]) } reverse(($i > 3 ? $i - 3 : 0) .. $i - 1);
        my $j = $i + 1;
        $j++ while $j <= $#$t && (!significant($t->[$j]) || $t->[$j][1] eq 'struct' || $t->[$j][1] eq 'class');
        if ($tok->[1] eq 'delegate') {
          my $k = $j;
          $k++ while $k <= $#$t && $t->[$k][1] ne '(';
          $k--;
          $k-- while $k > $j && !significant($t->[$k]);
          $j = $k;
        }
        print "", ($ns // ''), " $t->[$j][1]\n" if $t->[$j][0] eq 'id';
      }
    }
  }
} elsif ($mode eq 'uses') {
  my ($file, $typesFile) = @ARGV;
  my %ns;
  for my $line (split /\n/, slurp($typesFile)) {
    my ($n, $type) = split ' ', $line;
    next unless defined $type;
    $ns{$type}{$n} = 1;
  }
  my $t = lex(slurp($file), $file);
  my %used;
  my $prev = '';
  for my $tok (@$t) {
    next unless significant($tok);
    if ($tok->[0] eq 'id' && $prev ne '.' && $prev ne '::' && $prev ne '?.' && $ns{$tok->[1]}) {
      $used{$_} = 1 for keys %{$ns{$tok->[1]}};
    }
    $prev = $tok->[1];
  }
  print "$_\n" for sort keys %used;
} else {
  die "usage: client-cs.pl members|move|summary|rewrite|types|uses ...\n";
}
