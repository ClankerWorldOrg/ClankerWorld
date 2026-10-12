# Scripts that generate #1797 (issue #1364)

This branch is never merged. It holds the scripts that generate pull request #1797
from main, so any session can regenerate it.

Run from the root of a clean checkout of the main commit you want, in this order:

```bash
S=/path/to/this/folder
echo '{}' > global.json   # only if your local SDK differs from global.json; never commit it
bash $S/rename.sh
bash $S/stages/arch.sh
bash $S/stages/client.sh
bash $S/stages/domain.sh
bash $S/stages/camp.sh
bash $S/stages/dead-code.sh
git checkout global.json
git add -A && git commit
```

Each script stops with an error when an expected file or anchor is missing, and
leaves its changes staged. `cs-code-words.pl` must sit next to `rename.sh`, and
`client-cs.pl` next to `client.sh`. `domain.sh` takes about five minutes.
