# ShopPanel redesign

Shop has been rebuilt in `Assets/Scenes/HomeScene.unity`, at
`Canvas/SafeArea_Container/UI_Panels/Shop_Panel`.
The scene shows the store for editing. Play Mode retains the existing Home startup;
open the Shop tab to use the store.

`PreviewPortrait.png` is the updated actual Game View capture with Home's original
background and navigation. Other preview PNGs document earlier layout checks.

## Layout and data

- Full-width featured banner, five horizontal category tabs and two-column scrolling catalog follow reference image 2.
- Per the requested correction, Shop uses the original HomeScene `Background_Image` and `Bottom_Navigation_Bar` directly. Their component settings, artwork, positions, sizes and existing navigation behavior are preserved. The separate Shop background and bottom navigation were removed.
- Shop also uses Home's original `TopBar_Panel` HUD. The separate `Store_TopBar`, its HUD controller and its wallet buttons were removed. The layout builder preserves the shared Home HUD when rebuilding.
- Ships use the existing `ShipData_1` through `ShipData_8` sprites, names, prices and currencies. Display order is ZEPHYR, NOVA, ORION, AURORA, VANGUARD, SHADOW, TITAN, DREADNOUGHT. Original save indices are preserved.
- HP and shots per second come from `maxHealth` and `1 / fireRate`; no illustrative ATK or rarity numbers have been added.
- The featured ship is the game's NOVA sprite. Avatar, currency and navigation artwork reuse project assets. Weapon/booster icons are slices from the existing store atlas and are now assigned to ItemData assets.
- HomeSceneManager continues updating the original wallet, account level and player name. Ownership, selection and affordability use the existing purchase flow.
- New editable prefabs: `Assets/Prefabs/ShopItemCardWide.prefab` and `Assets/Prefabs/ShopPackCardWide.prefab`.
- Cut-corner frames, gradients, glows, tabs and buttons are native uGUI graphics (`ShopPanelGraphic`), not a flattened mockup.
- UI stays inside SafeArea. The catalog reserves space for Home's original selected navigation tab. Home action buttons are hidden while Shop is open and restored when returning Home.

## IAP catalog

The existing catalog was moved, with its GUID preserved, to
`Assets/Resources/Config/IapCatalog.asset`. The bootstrapped IapPurchaseService
now loads it in Awake. Previously the service created an empty catalog, leaving
the gem/coin tabs empty. The catalog builder menu uses the new location.

The current project does not have an active Unity Purchasing integration.
Editor pack buttons show PREVIEW and use the existing simulation flow; real-money
transactions were not tested or performed. Outside the Editor, unavailable store
buttons are disabled. No product IDs, prices, rewards or ship balance were changed.

## Generated artwork

Tool: built-in imagegen (no CLI fallback).

Previously generated asset (retained but no longer used by Shop): `Assets/Sprites/UI/ShopPanel/ShopSpaceBackground.png`.
Other assets in that directory are named slices of existing Unity artwork.

Final generation prompt:

> Use case: stylized-concept. Asset type: seamless-feeling polished 2D mobile space shooter shop background artwork, portrait 9:16 composition, 1024x1792 or similar portrait. Primary request: dark navy deep space with a gorgeous electric-blue rim-lit planet partially entering from the upper left edge at about 15 percent of image height, and a violet magenta nebula arcing diagonally behind the upper third. Small sparse sharp stars and a few distant blue lights. The bottom two thirds are intentionally very dark navy with restrained blue gaseous haze, a quiet surface for readable game shop cards. Vibrant premium hand-painted game illustration with clean gradients, cinematic cyan light and purple nebula matching a neon sci-fi arcade game. Edge to edge art. No spaceships, no characters, no UI, no frames, no text, no buttons, no logos, no lettering, no watermark. Use dark ink navy #030c24 base; keep strong light mostly in upper third. This will sit behind a separately assembled Unity store UI.

## Verification

Verified through Unity MCP in the connected GalaxyStrike Editor, Unity 6000.3.27f1
(the actual ProjectVersion, newer than the supplied AGENTS snapshot).

- Compilation completed without errors.
- Play Mode: Ships 8, Weapons 2, Items 2, Gems 3, Coins 3; category filtering and tab highlighting passed.
- EventSystem raycasts confirmed category buttons and the gem wallet shortcut are clickable.
- Returning from Upgrade to Store passed.
- The catalog scroll reached the bottom with all eight ships present.
- At the existing 3,053 coins / 30 gems balance, ZEPHYR and NOVA were affordable, higher-priced ships were disabled, and VANGUARD displayed EQUIPPED.
- Confirmed the original Home background and all bottom-navigation components are identical before/after the correction using serialized snapshots.
- Rendered and visually checked 1080x1920 and 1080x2340 layouts, plus Items and Gems views. See adjacent PNG files.
- No compile/runtime errors in the final validation run. TMP missing-ellipsis warnings remain.
- No purchase buttons were pressed and no purchase-related save changes were made.

Editor utilities are under `Tools/GalaxyStrike/Shop`: rebuild the layout, set a
portrait Game View, capture previews, and run navigation validation in Play Mode.
Rebuild replaces the store's child layout, so edit the builder before regenerating
if you want to retain manual layout changes.
