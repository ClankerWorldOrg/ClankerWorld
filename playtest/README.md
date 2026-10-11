# Playtest list

Changes that are merged but still need trying in the game by hand. Nothing
here has been checked by hand yet.

## Playing alpha 1

Open [alpha-1.md](alpha-1.md) and do the **Start here** checks first. They
cover what has to work in your first hour: installing, pairing, starting a
world, agents staying alive, and saving and loading. The rest is grouped by
part of the game, so try what you reach while you play. Then tell any agent
what you found; you don't need to edit anything yourself.

## Keeping the list current

- **Adding:** a pull request whose change still needs a hands-on check adds a
  file here in the same pull request. Name it after the issue and the change,
  like a [changelog entry](../changes/README.md), such as
  `431-orchard-harvest.md`. One file per pull request keeps parallel pull
  requests from conflicting. The file contains only check bullets; put setup
  and context in the bullets, not in a heading or a separate status paragraph.
- **Before a release:** the session preparing it folds every per-change file
  into that release's checklist, such as `alpha-2.md`, the way
  `scripts/collect-changes.sh` gathers changelog entries. It merges checks
  that overlap into one "do this, you should see that" step, drops checks a
  later change made false, marks the few that matter most under
  **Start here**, keeps the retained checks' source links, records any omitted
  checks in the pull request description, and deletes the per-change files.
  Checks nobody tried in the previous release's checklist carry over, and that
  checklist is deleted once it is empty.
- **After a playtest:** the agent the owner gives the results to opens one pull
  request that removes each check that passed or failed from the release
  checklist. Checks the owner did not try, or could not run, stay. The pull
  request's description says for each check whether it passed, failed or
  could not be run, with the
  [Windows playtest](../docs/development/build-and-test.md#windows-playtests)
  details the owner gave; don't hold the list back for details nobody gave.
  There is no separate Playtest report issue. Record build details in linked
  Bug or Implementation issues and label that work `from:playtest`.
  Link each check's source in the description, using `Refs` for issues and
  never a closing keyword such as `Closes`. If a check failed in the same way
  as its linked Bug or Implementation issue, reopen that issue with
  `status:needs-pr`. If something else is wrong, or the link is a source pull
  request or retired checklist such as #285, open a Bug issue that links back
  instead; a retired checklist is never reopened. An agreed improvement gets
  an Implementation issue; if it needs a new game choice, follow the existing
  Decisions rule.
- Write in plain English and player terms
  ([Writing clearly](../CONTRIBUTING.md#writing-clearly)). Say what to do and
  what should happen, end each check with its issue or pull request link, and
  never claim a check passed.

A new per-change file looks like this:

````markdown
- Give an adult next to a fruiting orchard an Order to harvest food. Once the food is gathered, the order shows as done. ([#431](https://github.com/compoodment/ClankerWorld/issues/431))
````

When a check needs the private server, start it with "On the paired world:".
The Windows build is the one that counts; a passing automated test or export
does not replace a check here.

Before a release, the owner also runs the short
[Windows release smoke check](../docs/development/releasing.md#windows-release-smoke-check).
The release notes list the release checklist's remaining checks as not yet
checked by hand; the list does not have to be emptied before an alpha release.
