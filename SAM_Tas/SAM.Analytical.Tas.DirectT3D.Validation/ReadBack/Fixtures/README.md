# S08 PR-C equivalence fixtures (D-FIX): provenance

**Status: signed off by the owner on 2026-10-21 at commit `05edbdde`** ("I approve the two fixtures at commit 05edbdde,
their documented synthetic provenance, centre-line/PhysicalThickness conventions, both multizone spaces being V1, and
single-pane glazing. I approve Budapest as the explicit weather choice."). Used by the S08 PR-C licensed acceptance
(`Documentation/S08-PRC-Acceptance-evidence/`).

The two Phase B Tas equivalence fixtures required by the roadmap (box and one multizone model) and S07 §8 D-FIX. The
committed JSON files below are the fixtures, byte for byte; a later regeneration is not a substitute (SAM mints new
GUIDs on every call).

| File | sha256 | Bytes |
|---|---|---|
| `s08-fix-box.json` | `45524172b32f85df89511dd344141023de999faa9552a199e6b49062612acd08` | 723,176 |
| `s08-fix-two.json` | `20da9b586345e9fc67dc6308e6da1578b9bc41b3c694dbdab37e9c677959baa0` | 760,086 |

`SHA256SUMS.txt` holds the same hashes; `.gitattributes` keeps the bytes (`-text`).

## How they were made

- **Factory:** `ReadBack/AcceptanceFixtures.cs` (`CreateBox`, `CreateTwoZones`), committed with these files. Authored by
  Claude Code (Opus 5.5) for S08 PR-C on 2026-10-21 against the owner-approved D-FIX direction of 2026-10-20 (SAM_Tas#105
  record, launch decision 2: "the box fixture is V2, centre-line, with PhysicalThickness. The two-zone fixture is V1").
- **Command:** `direct-t3d-validation.exe readback-fixtures --out <empty dir>` (COM-free; no Tas involved), run once on
  the licensed workstation on 2026-10-21 from the branch `feature/s08-prc-fixtures-acceptance` (base `sow/2026-Q4`
  `989972ce`, factory uncommitted at that moment, identical to the committed file).
  Harness `direct-t3d-validation.dll` sha256 `73cb6176…` (Debug); SAM `sow/2026-Q4` `ee377167` (clean), `SAM/build`
  `SAM.Analytical.dll` `be41b3a2…`, `SAM.Core.dll` `00e18734…`, `SAM.Geometry.dll` `d38f4373…` (built 2026-10-20; SAM
  commits since then are documentation only).
- **Reproducibility:** generating twice gives files that are equal after each minted GUID is replaced by its order of
  first appearance. `AcceptanceFixtureTests.TheCommittedFixture_IsWhatTheFactoryAuthors` asserts exactly that against
  the committed files, so the factory and the files cannot drift apart unnoticed.
- **Independence:** no material, construction, internal condition, space GUID or geometry is shared with the PR-B
  calibration models the frozen rules were measured on (`TheFixture_SharesNothingWithTheCalibrationModels`). The
  calibration models are not relabelled as fixtures.

## Declared conventions (per fixture)

| | `s08-fix-box` | `s08-fix-two` |
|---|---|---|
| Geometry | one zone, 6.4 x 4.2 x 2.85 m | zone A 4.5 x 5.4 m, zone B 3.6 x 5.4 m, both 3.05 m, one shared partition |
| Panel convention | **centre-line (mid-surface)**; every panel carries `PhysicalThickness` = its construction's layer total | same |
| Stored Area / Volume (D-F9 C) | = centre-line geometry (26.88 m², 76.608 m³); the intended volume is the centre-line shell | = centre-line geometry per zone |
| Ventilation (D-F2) | **V2**: requirement 0.01 m³/s per person and an explicitly selected Ventilation profile | **V1** in both zones: A 0.008 m³/s·p + 0.0006 m³/s·m², B 0.75 1/h; no Ventilation profile |
| Openings | framed 2.4 x 1.35 m south, unframed 0.9 x 1.2 m west; closed (no OpeningProperties) | A framed south; B framed north, unframed east; closed |
| Glazing | single laminated pane, no gas layer (gas-gap resistance is not computed by PR-B) | same |
| Gains, fractions, setpoints | all authored: occupancy S/L, lighting, equipment S/L, the three radiant fractions, heating, cooling, humidification, dehumidification, infiltration; 24-hour, 8760-hour and constant profiles | same, distinct values per zone |
| Exhaust, systems, IZAM data | none | none |
| Embedded weather / design days | none: the weather is supplied to the run (below) | none |

The construction layers are listed inside to outside (the first space's side). The partition is asymmetric.

## Weather used with them

The SAM#212 committed TMYx file `SAM/SAM/SAM.Tests/Resources/Weather/HUN_BU_Budapest.Met.Center.128400_TMYx.epw.gz`
(SAM `b721c5f4`; licence and provenance in that folder's README), decompressed to a machine-local path. Its uncompressed
sha256 `472ffb4e2f10867600949a392ce46891fc1bd05f2fc8d8917a99bca7d26209c3` is the S05 explicit EPW and is checked by
`Run-S08-ReadBack.ps1 -Phase acceptance` before any export. 8760 data rows (not a leap year). Design days are derived
from it by the contract route (S07 §3 Tas precedence step 2).

## Owner decisions that apply to them (2026-10-21, S08 PR-C gate)

The provenance and weather sign-off above; OD-G1 to OD-G3 approved as drafted in S07 §6.1; and the verified
inactive-profile transformation `T-VENT-INACTIVE` for a V0/V1 zone's present, all-zero `ticV` on rows 6 and 8b only (see
`Documentation/S08-PRC-FixturesAcceptance-PR.md`).
