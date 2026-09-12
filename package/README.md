# Gjallarhorn

A server-side Viking kill feed. Player deaths land in the top-left message feed and the chat
window; boss defeats get the centre-screen banner.

Named for Heimdall's horn — the one sounded to announce across all nine realms.

## Who needs to install this

**The server needs it. Every client is optional, and the mod is not version-enforced.**

- **On the server** it does all the work — detection, wording, and broadcasting. It reaches
  players over `ShowMessage` and `ChatMessage`, two RPCs vanilla Valheim already registers,
  so a server full of unmodded players still sees the whole feed.
- **On a client** it is purely additive. Installed, that player's deaths are reported with the
  real cause and the real killer. Absent, their deaths still announce, but the killer is a
  proximity guess and non-combat causes read as generic.

Mixed populations are fine and expected. Nobody is refused at connect.

## Why clients change the quality of the feed

Everything specific about a death — who swung, what kind of damage, whether another player
did it — lives in `Character.m_lastHit`, set in `Character.Damage`. That runs on whichever
peer owns the dying character's ZDO, which is the victim's own machine. A dedicated server
never instantiates a `Player` component for a remote client and can never read it.

So the server falls back to what it *can* see: the `dead` flag on each connected player's
replicated character ZDO, and whatever hostile creatures are standing near the corpse. That
gets the ordinary case right — you died to the thing that was hitting you, and it is still
there — and gets ranged kills, deaths on a fleeing mob, and every non-combat cause wrong.

| | Vanilla client | Client with Gjallarhorn |
|---|---|---|
| Death announced | yes | yes |
| Victim, position, biome | exact | exact |
| Killer creature | nearest hostile within 20m, a guess | exact |
| Star rank | from the guessed creature | exact |
| PvP kills | never identified | exact, with the killer named |
| Fall, drowning, fire, freezing, poison, falling trees | read as generic | each has its own message |
| Carts, boats, structures, turrets, the edge of the world | read as generic | grouped as environmental |

Boss defeats are exact either way. The server sees them through
`ZoneSystem.RPC_SetGlobalKey`, which is registered server-side only.

## Known limits

- **Repeat boss kills only announce for clients with the mod.** The global key a boss sets on
  death is added once per world; a second Bonemass changes nothing the server can observe. A
  client running Gjallarhorn reports every kill, including repeats.
- **The killer guess is a guess.** It is labelled as such in the logs (`[guess]` versus
  `[exact]`) so you can tell at a glance which path produced a line.
- **Chat lines also appear as floating text** at the death site for players standing nearby,
  or with the map open. That is vanilla `Chat.AddInworldText` behaviour and cannot be
  suppressed from the server. Turn off `UseChatLine` if it bothers you.
- **English only.** Creature and biome names come from a built-in table rather than Valheim's
  `Localization`, whose singleton is set up by the client-side startup path and is not
  dependable on a headless server.

## Configuration

`BepInEx/config/com.ragemedia.gjallarhorn.cfg`, generated on first run. Everything except
`VerboseLogging` is admin-only and synced from the server, so clients cannot give themselves
a different feed.

| Section | Setting | Default | What it does |
|---|---|---|---|
| Channels | `AnnounceDeaths` | `true` | Announce player deaths. |
| Channels | `AnnounceBossDefeats` | `true` | Announce boss defeats. |
| Channels | `UseTopLeftFeed` | `true` | Deaths go to the top-left message feed. |
| Channels | `UseChatLine` | `true` | Deaths also post as a chat line. |
| Channels | `BossUsesCenterBanner` | `true` | Boss defeats use the centre banner. Player deaths never do. |
| Channels | `ChatSenderName` | `Gjallarhorn` | Speaker name on chat lines. |
| Content | `IncludeBiome` | `true` | Resolve `{biome}`. |
| Content | `IncludeStarRank` | `true` | Render `2-star Greydwarf`. |
| Content | `LogToConsole` | `true` | Write announcements to the server log. |
| Detection | `PollInterval` | `1.0` | Seconds between death-flag checks. Vanilla clients only. |
| Detection | `ExactReportGrace` | `1.5` | How long to wait for a client's exact report before guessing. |
| Detection | `KillerSearchRadius` | `20` | Radius searched when guessing a killer. |
| Detection | `SwarmRadius` / `SwarmThreshold` | `6` / `4` | When a death reads as "overwhelmed by a horde". |
| Messages | 14 pools | — | Semicolon-separated; one picked at random. Tokens `{victim}` `{killer}` `{biome}` `{boss}`. |

## Compatibility

Do not run this alongside another kill-feed or death-announcement mod. Nothing stops BepInEx
loading both, and every death will simply be announced twice.

## Safe to add or remove

Nothing is written to the world save. Announcements are in-memory only.
