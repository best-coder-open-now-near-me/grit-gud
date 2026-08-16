# Turn Review Scope

The player-facing replay is a rolling **turn review**, not a saved full-match
replay. For the current player character, the review window begins at the start
of that character's most recently completed turn and continues through every
subsequent turn and the currently active partial turn.

Including the player's own last turn is deliberate: a returning player may
need to recover what they chose before reviewing the opponents' responses.
`GameplayJournal.GetTurnReviewWindow` exposes this bounded journal slice. It
returns no entries until that actor has completed a turn, and returns an
isolated read-only list so later journal appends do not mutate a review already
being presented.

The eventual visual replay must reconstruct this window in an isolated session
and must not rewind or mutate live gameplay. A later diagnostic mode may retain
more history, but full-match persistence is not part of the player feature.
