# Enemy arsenal

15 artwork families are authored as 51 independent projectile prefabs in `Assets/Prefabs/EnemyBullets`. Prepared sprites are trimmed from logical artwork cells into `Assets/Sprites/Bullets/Prepared`; source sheets are preserved. Rebuild with **GalaxyStrike > Enemy Arsenal > Rebuild Prefabs and Bind Enemies**.

| Family | Enemy prefab |
|---|---|
| DroneFighter | Enemy_Scout |
| LaserTurret | Enemy_Gunship |
| Mine | Enemy_Fighter |
| Bomber | Enemy_Striker |
| Sniper | Enemy_Cruiser |
| Swarmer | Enemy_Swarmer |
| ShieldUnit | Enemy_ShieldUnit |
| HeavyCruiser | Enemy_Destroyer |
| TwinCannon | Enemy_TwinCannon |
| MissileShip | Enemy_MissileShip |
| Stinger | Enemy_Elite |
| Wasp | Enemy_Wasp |
| Dreadnought | Enemy_MiniBoss |
| Alien | Enemy_AlienBoss |
| Boss | Enemy_Boss |

Both authoring prefabs and Resources copies are bound. Each hull has its own firing style, alongside the artwork matched to its sheet: Drone Fighter fires a single shot; Laser Turret fires a direct beam; Mine releases a slow radial ring; Bomber fires a wide fan; Sniper locks a targeted shot behind a 0.45 second warning; Swarmer fires a short burst; Shield Unit aims a slow shot; Heavy Cruiser fires a broad spread; Twin Cannon fires simultaneously from two barrels; Missile Ship fires a pair of short tracking rockets; Stinger sends a quick fan; Wasp fires a bright fan; Dreadnought spirals; Alien Boss sweeps a fan; Final Boss opens with a radial pattern.

The hull's style takes precedence when a level wave provides a generic pattern. Enemy bulletVariants select primary, aimed/spread, radial/spiral, and final phase artwork. Bosses change bullet artwork at 60% and 25% health. Existing formation behavior, movement and chance to shoot stay in control. Sniper and Laser Turret display a warning line before each volley; the sniper locks its direction when the warning starts.

Projectiles move with kinematic Rigidbody2D in FixedUpdate. Circle/capsule hitboxes cover the bright core rather than the glow. Missiles steer at most 50 degrees/second for 0.8 seconds, then continue straight. Offscreen bullets recycle after a short spawn grace period, with a seven second lifetime fallback. BulletPool handles runtime growth and ignores duplicate despawn requests.

New enemies are introduced deterministically without adding enemies to a wave: Swarmer from level 2, Wasp from level 4, Twin Cannon from zone 2, Shield Unit from level 15, Missile Ship from zone 3. Alien Boss appears at major bosses in zones 3 and 4; the final zone keeps Final Boss. Level 1 retains its original lineup.

Tune speed, acceleration, tracking and collider size in each bullet prefab. Tune fireRate and bulletVariants on enemy prefabs. Runtime balancing should be checked on the target Android device.

Verification performed in Unity 6000.3.27f1 through MCP:
- 51 projectile prefab assets validated: script, Rigidbody2D, trigger collider, sprite, EnemyBullet tag.
- All 15 enemy families have projectile references; campaign generation for levels 1–50 includes all 15 and has no missing wave prefab.
- All 51 projectiles spawned together and advanced through physics simulation without stationary or failed spawns.
- Pool growth/reuse with 40 instances and duplicate-despawn guard passed.
- Shield collision recycling, lifetime recycling, offscreen recycling, and missile steering timeout passed.
- No compile errors or runtime errors in the final checks. Existing obsolete API warnings remain elsewhere in the project.
- Hostile variants prewarm at most 8 instances each to fit the shared pool budget; player projectile reserve remains configured by prewarmCount.
- Visual contact sheet: `Captures/enemy-arsenal-prefabs.png`.
- Target Android device performance and gameplay balance have not been measured.
