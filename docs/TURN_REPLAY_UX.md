# Turn Replay UX

Turn replay is a short, in-world playback surface rather than a textual event
log or a saved full-match recording. It is a bounded rolling window: history
begins with the currently active player character's last completed turn and
ends when that character's current turn begins. Nothing before that previous
turn is retained for the player-facing replay.

## Combined initiative and replay controls

The existing initiative queue remains on the right side of the HUD. A replay
button beside the active character opens a playback bar across the top of the
screen. Closing replay collapses the bar without changing the live camera,
initiative, or gameplay state.

The playback bar contains:

- play/pause, which plays the entire retained range;
- previous-turn and next-turn controls;
- playback speed and close controls; and
- a scrubber divided into one segment for each character turn that occurred
  inside this short retained window.

The window includes the active character's previous turn followed by whichever
player-controlled characters, allies, or enemies actually took a turn before
control returned to that character. It is not a history of every turn in the
encounter. Each included turn uses its actor's portrait, name, and allegiance
treatment. Emergency reactions appear as markers within the turn that triggered
them rather than as ordinary initiative turns.

Selecting a segment seeks to the beginning of that character's turn. Previous
and next move between turn segments, while play advances continuously across
all segments. The scrubber permits seeking within a turn as well as across the
complete retained range.

## Retained range

The range is anchored to the active player character:

1. that character's last completed turn;
2. every character turn completed afterward, in actual execution order; and
3. the boundary at which the character's current turn began.

For example, if Mara's prior turn was followed by Raider, Vale, and Guard before
Mara became active again, the replay contains exactly those four completed turn
segments. Earlier Mara, Raider, Vale, or Guard turns are outside the window.

The current partial turn is not part of the initial replay scope. This freezes
the available timeline when control returns to the player and avoids mixing
replay state with new live actions. If the active character has not completed a
prior turn, replay is unavailable.

## Playback contract

Replay is reconstructed from authoritative records in an isolated presentation
state. It must never rewind or mutate the live gameplay session. Scrubbing
restores recorded visual state and samples seekable movement and projectile
paths; transient audio, particles, and other cosmetic effects may snap or
restart when forward playback resumes.

The timeline is presentation time, not wall-clock decision time. Idle thinking
time is omitted, and deterministic visual durations are assigned to recorded
actions. Segment widths should follow those durations with a minimum clickable
width rather than giving every turn equal space.

## Initial interaction limits

- Replay opens only while a player-controlled character owns the active turn.
- Opening replay pauses live input.
- Camera orbit and zoom remain available; automatic follow changes at turn
  boundaries only while follow mode is enabled.
- Exiting replay restores the live HUD and camera exactly as they were.
- No generated prose, replay library, full-match persistence, or free-standing
  recap drawer is required.
