# Bug Report Export

Gameplay includes a local, account-free bug report export. Press **F8** or use
**Export Bug Report (.txt)** below the contextual guidance panel. WebGL downloads
the report through the browser; desktop builds write it to the game's persistent
`Exports` directory.

The report is plain UTF-8 text intended to be attached directly to a development
conversation. No report is uploaded or sent anywhere automatically.

## Included snapshot

- the current guidance ID and its Expected, Why, and Tip copy;
- authoritative session mode, turn context, encounter and operation state;
- initiative, active actor, actor poses, stances, AP, and movement resources;
- the last voluntary-cycle state before resource replenishment;
- the last encounter turn transition;
- pending authoritative movement plus provisional route/playback status;
- sequenced resolved action costs and effects;
- scenario objective positions, interaction radii, target-owned turn costs,
  completion state, and
  non-identifying build, platform, graphics, and screen information.

The top of the file contains optional prompts for what the player did, what they
saw, and how often it repeats. Those can be filled in before sharing, but the raw
snapshot is still useful on its own.

## Privacy and delivery

The export does not include save files, arbitrary local files, usernames,
location, or network identifiers. The player chooses whether and where to share
the downloaded file. If a hosted intake path is added later, it should remain an
explicit second step rather than changing this local export into an automatic
upload.
