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

- play/pause, which by default plays the turns that followed the active
  character's previous turn;
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

The active character's previous-turn segment hangs off the left edge of the
initial playback range. It remains visible and seekable as context, but opening
replay does not automatically play it. The player can reach it with the
previous-turn control or by scrubbing left into that segment. After seeking
into it, ordinary playback continues from that point through the later turns.

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

Replay uses the player-controlled character's normal gameplay camera without a
special replay camera mode. All ordinary camera behavior and controls remain
available, including looking around and moving between the normal third-person
and first-person views. The camera follows that character's recorded movement
just as it follows the live character during play. Turn segments do not switch
control or camera ownership to their acting characters. Camera input never
changes the playhead or authoritative replay state.

The timeline is presentation time, not wall-clock decision time. Idle thinking
time is omitted, and deterministic visual durations are assigned to recorded
actions. Segment widths should follow those durations with a minimum clickable
width rather than giving every turn equal space.

## Initial interaction limits

- Replay opens only while a player-controlled character owns the active turn.
- Opening replay pauses live character and action input, but not camera input.
- The active player character keeps their complete normal gameplay camera and
  camera controls throughout replay; only character control remains paused.
- Exiting replay restores the live HUD and camera exactly as they were.
- No generated prose, replay library, full-match persistence, or free-standing
  recap drawer is required.

## Implementation checkpoint

The first vertical slice now records ordinary and emergency turn kinds
explicitly, projects an immutable replay window for the active player
character, and exposes that window through the right-side active-character
control and an interactive top timeline. The timeline starts at the boundary
after the character's optional previous-turn segment and supports play/pause,
previous/next turn, speed, and scrubbing. Small markers expose meaningful
movement, stance, action, displacement, projectile, and destruction events
inside each turn without generating a textual recap.

The playhead now seeks recorded actor movement, displacement, facing, and
stance in the world. Opening replay preserves the live presentation state,
places gameplay input in camera-only mode, and restores the exact live actor
transforms and stances on exit. The character's ordinary camera remains active
throughout.

Action animation, wounds and equipment visuals, projectiles, destructibles,
vehicles, smoke, and other persistent effect snapshots remain subsequent
presentation slices. Until those are connected, the timeline is an actor-pose
replay rather than a complete reconstruction of every recorded consequence.
Replay is available only before the player commits an action in the new live
turn, ensuring the reconstruction remains anchored to the exact recorded
turn-boundary state.
