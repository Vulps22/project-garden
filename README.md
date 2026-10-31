# Project Garden

A "Grow A Garden" clone for **Somnium Space VR**. Buy seeds, plant them, wait for them to grow,
carry the produce to a sell point for Thatch.

## Requirements

- **Somnium ProSDK v3** (this repo is cloned *into* an SDK template checkout)
- Unity **6000.3.5f2**, URP 17.3.0, XR Interaction Toolkit 3.3.1
- Photon Fusion via the Somnium `NetworkBridge` wrapper, Photon 2.0.5 + addons

## Asset packages to install

From the Asset Store, into `Assets/`. Folder names must match exactly or references break.

- `Blinktool` — Low poly rocks
- `Cartoon_Farm_Crops`
- `MedievalMarketDemo`
- `propsv2`
- `GVOZDY` — Garden Wooden Fence modular outdoor prop
- `VFXPACK_FIRE_WALLCOEUR`
- `PRJ0818_Murakami` — WoodenBoard
- `Medieval houses` — Log house

## Setup

1. Extract a blank Somnium ProSDK v3 checkout.
2. Clone this repo into it (`git init` + fetch + checkout — the directory is non-empty, so `git
   clone` will not work directly).
3. Install the asset packages listed above.
4. Install the Somnilux editor package into `Packages/com.somnilux.sdk` — Linux-only ProSDK
   workarounds, Editor-only, never reaches an exported world.
5. `unity pipeline install` if you want the `unity` CLI compile-check bridge.

See [`docs/packaging.md`](docs/packaging.md) for the plan to make GAG a clean drop-in, and
[`CLAUDE.md`](CLAUDE.md) for how the project is built and tested.
