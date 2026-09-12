# Changelog

## 0.1.0

Initial release.

- Server-side death detection via the `dead` ZDO flag on each connected player's character,
  which fires explicitly once and catches deaths from full health.
- Optional client component reporting real `HitData`: exact killer, star rank, PvP, and all
  24 of Valheim's hit types collapsed into 12 message pools.
- Boss defeats through `ZoneSystem.RPC_SetGlobalKey` server-side, and through
  `Character.OnDeath` on clients that have the mod, so repeat kills announce too.
- Top-left feed and chat line for deaths; centre banner for boss defeats.
- 14 configurable message pools, admin-synced.
