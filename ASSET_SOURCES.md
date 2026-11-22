# POLYMULATOR — Asset Sources & Attribution

Daftar lengkap sumber aset eksternal maupun prosedural/orisinal yang digunakan dalam proyek **POLYMULATOR** (Kelompok 8 — Grafika Komputer).

---

## 1. Typography / Fonts

| Aset | Sumber & Penulis | Lisensi | Lokasi di Proyek |
|---|---|---|---|
| **Akt** (`Regular`, `Medium`, `SemiBold`, `Bold`) | Dima Grenev — [`github.com/dimgrenev/akt`](https://github.com/dimgrenev/akt) | SIL Open Font License (OFL) v1.1 | [`Assets/UI/Fonts/Akt/`](Assets/UI/Fonts/Akt/), [`OFL.txt`](Assets/UI/Fonts/Akt/OFL.txt) |
| **Inter** (`Regular`, `SemiBold`) & **Roboto Mono** | Bundled via Unity `com.unity.dt.app-ui` / Google Fonts (Rasmus Andersson / Christian Robertson) | SIL Open Font License (OFL) v1.1 | [`Assets/UI/Fonts/`](Assets/UI/Fonts/) |

---

## 2. Color System & Art Direction

| Aset | Sumber | Lisensi | Implementasi |
|---|---|---|---|
| **Catppuccin Mocha Palette** | [Catppuccin Official Specification](https://github.com/catppuccin/catppuccin) (`Mantle`, `Crust`, `Base`, `Surface0..2`, `Overlay0..2`, `Text`, `Subtext0..1`, dan 14 warna aksen) | MIT License | `Assets/Scripts/Visual/PaletteSettings.cs`, `Assets/Resources/PaletteSettings.asset`, seluruh `.uss` & `.shader` |

---

## 3. 3D Meshes & Geometry

Seluruh aset 3D di proyek ini dibuat secara **prosedural di C#** (tanpa model eksternal berbayar/rip) agar konsisten dalam satuan *Logical Grid Units* (`1 GU = 1.0 m`):

| Aset Mesh | Generator & File | Keterangan |
|---|---|---|
| `ChamferCube`, `ChamferSlab`, `OctagonPrism`, `FacetedCrystalGem`, `TaperedChassis`, `SteelTruss`, `LightShaftCone` | `Assets/Editor/ChamberMeshFactory.cs` | Diekspor ke `Assets/Models/Generated/*.asset` untuk balok ber-bevel, pedestal, kristal Resonance Core / Memory Fragment, chassis `RECT`, truss baja, dan kerucut cahaya |
| *Facility Architecture* (`Trench`, `Control Booth`, `Skylight Gantry`, `Sub-Piers`, *Chapter Set Dressing*) | `Assets/Editor/ChamberArchitectureBuilder.cs` | Disusun otomatis pada seluruh 12+ chamber & `MainMenu` diorama |
| *Runtime Partition / Fusion Geometry* & `PlacementGhost` Wireframe | `Assets/Scripts/Interaction/PartitionFusionController.cs`, `Assets/Scripts/Interaction/PlacementGhost.cs` | Pembelahan, penggabungan blok komposit, serta proyeksi wireframe 3D |

---

## 4. Custom URP Shaders & Procedural VFX Textures

| Aset | Implementasi | Fungsi |
|---|---|---|
| **Custom HLSL URP Shaders** (SRP Batcher compatible) | `Assets/Shaders/SimSurface.shader`, `ChamberGrid.shader`, `ProOutline.shader`, `ProRimOverlay.shader`, `VolumetricBeam.shader`, `MochaGradientSky.shader` | Material PBR matte/bevel, floor grid reaktif & mode dissolve, 360° smoothed-normal contour outline, light shaft beams, dan langit gradien |
| **Procedural 64×64 Bokeh Glow Sprite** | `Assets/Scripts/Visual/SimulationVFX.cs` (`Resources/Sim_ParticleGlow.mat`) | Tekstur partikel radial Gaussian tanpa impor bitmap eksternal |
| **Static Scene Transition Snapshot** | `Assets/Scripts/SceneTransition.cs` | Capture framebuffer langsung (`RenderTexture` / screen capture) saat transisi kamera belakang `RECT` |

---

## 5. Audio (Procedural DSP Synthesis)

| Aset | Implementasi | Keterangan |
|---|---|---|
| **Gameplay SFX & Piano Adi** | `Assets/Scripts/Audio/SimulationAudio.cs` | Sintesis PCM real-time prosedural (`AudioClip.Create`) untuk pickup, drop, rotate, throw, partition, fusion chord, anchor lock, typewriter ticks, serta motif 5 nada piano |

---

## 6. Engine & Official Unity Packages

- **Unity 6 (`6000.3.26f1 LTS` / `6000.6.3f1`)** — Universal Render Pipeline (`URP`), Unity Input System, UI Toolkit, uGUI/TextCore.
