# Gjallarhorn

A server-side Viking kill feed for Valheim.

Player deaths land in the top-left message feed and the chat window. Boss defeats get the
centre-screen banner, so the two are distinguishable at a glance.

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

## How it works

Everything specific about a death — who swung, what kind of damage, whether another player
did it — lives in `Character.m_lastHit`, set in `Character.Damage`. That runs on whichever
peer owns the dying character's ZDO, which is the victim's own machine. A dedicated server
never instantiates a `Player` component for a remote client and can never read it.

So there are two detectors, and they race on purpose:

- The **server** watches the `dead` flag on each connected player's replicated character ZDO.
  It fires explicitly, once, and catches deaths from full health — unlike polling health,
  which Valheim strips from the ZDO entirely while a character is undamaged.
- The **victim's client**, if it has the mod, ships its real `HitData` up over a Jötunn RPC.

Whichever arrives first wins, keyed on the victim's ZDOID. Valheim destroys the player object
on death and builds a fresh one on respawn, so that ID is unique per life and makes a perfect
dedupe key. The server holds a short grace window before falling back to a guess, giving the
exact report time to land.

When the server has to guess, it looks at the hostile creatures standing near the corpse. That
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
- **The killer guess is a guess.** Log lines are tagged `[guess]` or `[exact]` so you can tell
  which path produced each one.
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

## Building from source

Requires the .NET SDK and a Valheim install. The mod compiles against the game's own
assemblies plus the BepInEx and Jotunn DLLs from a mod-manager profile, so that what it is
built against is exactly what loads at runtime.

```bash
cp Environment.props.example Environment.props   # then edit the paths
dotnet build -c Release
```

The DLL lands in `bin/Release/Gjallarhorn.dll`. Copy it into your profile's
`BepInEx/plugins/Gjallarhorn/` to test.

`Environment.props` is machine-specific and deliberately untracked.

## Compatibility

Do not run this alongside another kill-feed or death-announcement mod. Nothing stops BepInEx
loading both, and every death will simply be announced twice.

Nothing is written to the world save, so it is safe to add or remove at any time.
Announcements are in-memory only.

## Requirements

- [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
- [Jotunn](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/)

## License

[MIT](LICENSE).

## Source

<https://github.com/egaroib/Valheim-Gjallarhorn>
