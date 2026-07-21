# Multiplayer Remote Parity

Local multi-client behavior is not proof that real online multiplayer works. Two clients on one computer share a clock, have negligible latency, and often reuse local state. Those conditions previously hid defects that caused remote players to miss combat animation, VFX, combat text, health and damage updates, XP, quest credit, and authoritative loot depletion.

## Rules for multiplayer changes

- The session host/server is authoritative for combat, health, rewards, quest-credit decisions, world objects, and loot. A client snapshot must never overwrite authoritative state.
- Never compare short-lived network data against a timestamp created on another machine. Use receiver-local arrival time, synchronized network time, or durations/sequence numbers.
- Apply persistent player-owned state such as inventory, XP, and quest progress through an explicit targeted result on the owning player. Do not treat a remote presentation proxy as the player's complete state.
- Treat loot and other consumable interactions as validated, idempotent transactions. Delayed or reordered snapshots must not restore consumed state or permit duplicate claims.
- Authenticate the sender and ownership of every client-authored operation. Clients may request changes; they must not authoritatively declare shared outcomes.
- Keep the same networking and gameplay path in the editor and player builds. Do not add local-only or editor-only substitutes that conceal online behavior.

Same-machine behavior must never be used to justify architecture that depends on shared clocks, negligible latency, local state, or message ordering. Do not introduce those assumptions into future multiplayer work, and do not add special-case local paths that conceal real online behavior.
