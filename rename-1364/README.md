# Scripts that generate #1797 (issue #1364)

This branch is never merged. It holds the scripts that generate pull request #1797
from main, so any session can regenerate it.

Run from the root of a clean checkout of the main commit you want, in this order:

```bash
S=/path/to/this/folder
c() { git add -A -- . ':(exclude)global.json' && git commit -qm "stage $1"; }
# Only if your local .NET SDK differs from global.json: echo '{}' > global.json
# before each step and `git checkout global.json` after it. Never commit it.
bash $S/rename.sh            && c rename
bash $S/stages/arch.sh       && c arch
bash $S/stages/client.sh     && c client
bash $S/stages/domain.sh     && c domain
bash $S/stages/camp.sh       && c camp
bash $S/stages/dead-code.sh  && c dead-code
git reset --soft <main commit> && git commit   # squash into one commit
```

Every stage after the first refuses to start on a tree with uncommitted
changes, so commit after each one, then squash.

Each script stops with an error when an expected file or anchor is missing, and
leaves its changes staged. `cs-code-words.pl` must sit next to `rename.sh`, and
`client-cs.pl` next to `client.sh`. `domain.sh` takes about five minutes.
