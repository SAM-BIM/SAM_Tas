// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.InputEquivalence;
using SAM.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EQ = SAM.Analytical.InputEquivalence.Query;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>A series result with the native encoding it was read through. Every element is kept.</summary>
    public sealed class SeriesRecord
    {
        public string QuantityId;
        public string ObjectKey;
        public string NativeEncoding;
        public SeriesResult Result;
    }

    /// <summary>
    /// The S08 comparison of a frozen SAM model with an independent native snapshot, every outcome decided by the merged
    /// S07 core (<see cref="EQ.Evaluate"/>, <see cref="EQ.EvaluateSeries"/>, <see cref="EQ.Gate"/>). It adds no rule:
    /// it reads raw native fields, pairs them by geometric identity, applies the committed calibration conventions and
    /// hands each pair to the core. The required coverage is built from the frozen model alone (<see cref="RequiredFromModel"/>),
    /// never from what the reader happened to find.
    /// </summary>
    public sealed class ReadBackComparison
    {
        public ExpectedModel Expected { get; private set; }
        public NativeSnapshot Native { get; private set; }
        public CalibrationRules Rules { get; private set; }
        public IdentityResult Identity { get; private set; }
        public GateConfiguration Configuration { get; private set; }

        public List<RequiredComparison> Required { get; private set; } = new List<RequiredComparison>();

        /// <summary>The gate inputs: scalar results, each series' <see cref="SeriesResult.GateResult"/> and identity defects.</summary>
        public List<QuantityResult> Results { get; } = new List<QuantityResult>();
        public List<SeriesRecord> Series { get; } = new List<SeriesRecord>();

        /// <summary>T-FRAME fields read from the T3D only (uncertainty 4). Writer-only identity; reported, never gated.</summary>
        public List<QuantityResult> T3DFieldChecks { get; } = new List<QuantityResult>();

        /// <summary>Native fields read as supporting evidence (S07 "(info)" reads, the HDD condition, IZAMs), never gated.</summary>
        public List<string> Supporting { get; } = new List<string>();

        /// <summary>Native objects SAM never stated outside the S07 table (IZAMs, HVAC); each makes the run at least FAIL.</summary>
        public List<string> NativeExtras { get; } = new List<string>();
        public GateReport Gate { get; private set; }
        public GateReport GateProvisional { get; private set; }

        private const double SecondsPerHour = 3600;
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        public static ReadBackComparison Run(ExpectedModel expected, NativeSnapshot native, CalibrationRules rules, GateRuleSet ruleSet = GateRuleSet.Interim)
        {
            ReadBackComparison result = new ReadBackComparison
            {
                Expected = expected,
                Native = native,
                Rules = rules ?? CalibrationRules.None()
            };

            result.Required = RequiredFromModel(expected).ToList();
            result.Configuration = new GateConfiguration { RuleSet = ruleSet, RequiredComparisons = result.Required };
            result.Identity = ReadBackIdentity.Match(expected, native);
            result.Compare();

            result.Gate = EQ.Gate(result.Results, result.Configuration);
            result.GateProvisional = EQ.Gate(result.Results, new GateConfiguration { RuleSet = GateRuleSet.ProvisionalProposed, RequiredComparisons = result.Required });
            return result;
        }

        // ---- object keys: one place, used by the coverage list and by the comparison alike ---------------------------------

        public static class Keys
        {
            public static string Space(Guid space) => "space:" + space.ToString("D");
            public static string Side(Guid panel, Guid space) => "panel:" + panel.ToString("D") + "@space:" + space.ToString("D");
            public static string Layer(string side, int k, string property) => side + "#layer" + k.ToString(ci) + "." + property;
            public static string Aperture(Guid aperture, Guid space) => "aperture:" + aperture.ToString("D") + "@space:" + space.ToString("D");
            public static string Profile(Guid space, string slot, string what) => Space(space) + ".ic." + slot + "." + what;
            public static string Weather(string what) => "weather." + what;
            public static string DesignDay(string role, int index, string what) => "designday." + role + "[" + index.ToString(ci) + "]." + what;
        }

        public static readonly string[] OpaqueProperties = { "thickness", "conductivity", "density", "specificHeat", "externalSolarReflectance", "internalSolarReflectance", "externalLightReflectance", "internalLightReflectance", "externalEmissivity", "internalEmissivity" };
        public static readonly string[] TransparentProperties = { "thickness", "conductivity", "solarTransmittance", "lightTransmittance", "externalSolarReflectance", "internalSolarReflectance", "externalLightReflectance", "internalLightReflectance", "externalEmissivity", "internalEmissivity" };
        public static readonly string[] GasProperties = { "thickness", "conductivity", "density", "specificHeat", "convectionCoefficient", "dynamicViscosity" };
        public static readonly string[] GainSlots = { "ticOSG", "ticOLG", "ticLG", "ticESG", "ticELG" };
        public static readonly string[] ThermostatSlots = { "ticLL", "ticUL", "ticHLL", "ticHUL" };
        public static readonly string[] VentilationBases = { "flow", "flowPerArea", "flowPerPerson", "airChangesPerHour" };

        private static string[] PropertiesOf(IMaterial material, bool pane)
        {
            if (material is GasMaterial) return GasProperties;
            if (pane || material is TransparentMaterial) return TransparentProperties;
            return OpaqueProperties;
        }

        /// <summary>
        /// The quantity-on-object comparisons the fixture verdict needs, from the frozen model only: every space, every panel
        /// side and its layers, every aperture side, every authored profile, the weather and every design day.
        /// </summary>
        public static IEnumerable<RequiredComparison> RequiredFromModel(ExpectedModel expected)
        {
            foreach (ExpectedSpace space in expected.Spaces)
            {
                yield return new RequiredComparison("1a", Keys.Space(space.Guid));
                yield return new RequiredComparison("2", Keys.Space(space.Guid));
                yield return new RequiredComparison("2", Keys.Space(space.Guid) + ".storedVolume");

                foreach (ExpectedPanel panel in space.Panels)
                {
                    string side = Keys.Side(panel.Guid, space.Guid);
                    yield return new RequiredComparison("1b", side);
                    yield return new RequiredComparison(UQuantity(panel), side + ".U");
                    for (int k = 0; k < panel.Layers.Count; k++)
                    {
                        foreach (string property in PropertiesOf(panel.Layers[k].Material, false))
                        {
                            yield return new RequiredComparison("3a", Keys.Layer(side, k + 1, property));
                        }
                    }

                    foreach (ExpectedAperture aperture in panel.Apertures)
                    {
                        string a = Keys.Aperture(aperture.Guid, space.Guid);
                        yield return new RequiredComparison("1b", a + ".pane");
                        yield return new RequiredComparison("1b", a + ".frame");
                        yield return new RequiredComparison("3c.U", a + ".U");
                        yield return new RequiredComparison("8d", a + ".opening");
                        for (int k = 0; k < aperture.PaneLayers.Count; k++)
                        {
                            foreach (string property in PropertiesOf(aperture.PaneLayers[k].Material, true))
                            {
                                yield return new RequiredComparison("3c.layer", Keys.Layer(a + ".pane", k + 1, property));
                            }
                        }

                        if (aperture.FrameArea != null && aperture.FrameArea.State == SamInputState.Value)
                        {
                            for (int k = 0; k < aperture.FrameLayers.Count; k++)
                            {
                                foreach (string property in PropertiesOf(aperture.FrameLayers[k].Material, false))
                                {
                                    yield return new RequiredComparison("3a", Keys.Layer(a + ".frame", k + 1, property));
                                }
                            }
                        }
                    }
                }

                // The internal-condition rows are required for every space, with or without a SAM internal condition: what SAM
                // does not state is S-UNSPEC and is compared with the persisted native state, never left out.
                foreach (string slot in GainSlots)
                {
                    yield return new RequiredComparison("4a.density", Keys.Profile(space.Guid, slot, "factor"));
                    yield return new RequiredComparison("4b", Keys.Profile(space.Guid, slot, "total"));
                }

                yield return new RequiredComparison("4a.people", Keys.Space(space.Guid) + ".people");
                foreach (string fraction in new[] { "lightingRadProp", "occupantRadProp", "equipmentRadProp" })
                {
                    yield return new RequiredComparison("4c", Keys.Space(space.Guid) + ".ic." + fraction);
                }

                foreach (string slot in ThermostatSlots)
                {
                    yield return new RequiredComparison(slot == "ticLL" || slot == "ticUL" ? "5.temperature" : "5.humidity", Keys.Profile(space.Guid, slot, "hourly"));
                }

                foreach (string slot in ScheduleSlots)
                {
                    yield return new RequiredComparison("6.hourly", Keys.Profile(space.Guid, slot, "hourly"));
                    yield return new RequiredComparison("6.eflh", Keys.Profile(space.Guid, slot, "eflh"));
                }

                yield return new RequiredComparison("7a", Keys.Profile(space.Guid, "ticI", "factor"));
                yield return new RequiredComparison("7b", Keys.Profile(space.Guid, "ticI", "flow"));
                foreach (string basis in VentilationBases)
                {
                    yield return new RequiredComparison("8a", Keys.Space(space.Guid) + ".ventilation." + basis);
                }

                yield return new RequiredComparison("8b", Keys.Profile(space.Guid, "ticV", "delivered"));
            }

            yield return new RequiredComparison("9", Keys.Weather("latitude"));
            yield return new RequiredComparison("9", Keys.Weather("longitude"));
            yield return new RequiredComparison("9", Keys.Weather("altitude"));
            yield return new RequiredComparison("9", Keys.Weather("timeZone"));
            yield return new RequiredComparison("9", Keys.Weather("building.latitude"));
            yield return new RequiredComparison("9", Keys.Weather("building.longitude"));
            yield return new RequiredComparison("9", Keys.Weather("building.timeZone"));
            yield return new RequiredComparison("9.ground", Keys.Weather("groundTemperature"));
            foreach (KeyValuePair<int, Weather.WeatherDataType> item in ReadBackExpected.AnnualWeather)
            {
                yield return new RequiredComparison("9", Keys.Weather("annual." + item.Value));
            }

            foreach (string role in new[] { "cooling", "heating" })
            {
                List<ExpectedDesignDay> days = role == "cooling" ? expected.CoolingDesignDays : expected.HeatingDesignDays;
                for (int i = 0; i < days.Count; i++)
                {
                    yield return new RequiredComparison("10.yearDay", Keys.DesignDay(role, i, "yearDay"));
                    foreach (KeyValuePair<string, Weather.WeatherDataType> item in ReadBackExpected.DesignDayWeather)
                    {
                        yield return new RequiredComparison("10", Keys.DesignDay(role, i, item.Key));
                    }
                }
            }
        }

        /// <summary>
        /// The schedule rows (6) of the bounded S07 contract: gains, infiltration and ventilation. Every slot is compared for
        /// every space; a slot without a SAM profile is S-UNSPEC against the persisted native schedule.
        /// </summary>
        public static readonly string[] ScheduleSlots = { "ticOSG", "ticOLG", "ticLG", "ticESG", "ticELG", "ticI", "ticV" };

        // ticOSG and ticOLG both carry the one SAM Occupancy profile (MA/Modify/UpdateInternalCondition.cs).
        private static ExpectedProfile ProfileFor(ExpectedSpace space, string slot)
        {
            string key = slot == "ticOLG" ? "ticOSG" : slot;
            return space.Profiles.TryGetValue(key, out ExpectedProfile profile) ? profile : null;
        }

        private static string UQuantity(ExpectedPanel panel)
        {
            return Kind(panel) == ElementKind.GroundOrAdiabatic ? "3b.ground" : "3b";
        }

        private static ElementKind Kind(ExpectedPanel panel)
        {
            if (panel.Adiabatic) return ElementKind.GroundOrAdiabatic;
            switch (panel.PanelType)
            {
                case PanelType.SlabOnGrade:
                case PanelType.UndergroundWall:
                case PanelType.UndergroundSlab:
                case PanelType.UndergroundCeiling:
                    return ElementKind.GroundOrAdiabatic;
            }

            return panel.SpaceOrder.Count >= 2 ? ElementKind.Internal : ElementKind.External;
        }

        // ---- the comparison ---------------------------------------------------------------------------------------------

        private void Compare()
        {
            foreach (ExpectedSpace space in Expected.Spaces)
            {
                CompareSpace(space);
            }

            CompareUnmatched();
            CompareWeather();
            CompareDesignDays();

            foreach (string izam in Native.IzamNames ?? new List<string>())
            {
                NativeExtras.Add("8e: native IZAM '" + izam + "' present; the contract route ran with AddIZAMs=false and SAM states none");
            }
        }

        private QuantityInput Input(string quantityId, string key, ExpectedValue expected, ReaderResult native, IdentityStatus identity, AlignmentStatus alignment = AlignmentStatus.Unverified, ContractListing listing = ContractListing.None, string transformation = null, bool transformationVerified = false, ModelInterpretation interpretation = ModelInterpretation.NotRequired)
        {
            return new QuantityInput
            {
                Definition = QuantityCatalog.Get(quantityId),
                FixtureId = Expected.FixtureId,
                ObjectKey = key,
                Expected = expected,
                Native = native,
                Identity = identity,
                Alignment = alignment,
                Listing = listing,
                TransformationId = transformation,
                TransformationVerified = transformationVerified,
                Interpretation = interpretation
            };
        }

        private void Scalar(string quantityId, string key, ExpectedValue expected, ReaderResult native, IdentityStatus identity, string convention = null, ModelInterpretation interpretation = ModelInterpretation.NotRequired, ContractListing listing = ContractListing.None)
        {
            Results.Add(EQ.Evaluate(Input(quantityId, key, expected, native, identity, AlignmentStatus.Unverified, listing, convention, false, interpretation), Configuration));
        }

        private void SeriesOf(string quantityId, string key, IReadOnlyList<ExpectedValue> expected, IReadOnlyList<ReaderResult> native, IdentityStatus identity, AlignmentStatus alignment, string convention, string encoding, ModelInterpretation interpretation = ModelInterpretation.NotRequired)
        {
            SeriesResult series = EQ.EvaluateSeries(Input(quantityId, key, null, null, identity, alignment, ContractListing.None, convention, false, interpretation), expected, native, Configuration);
            Series.Add(new SeriesRecord { QuantityId = quantityId, ObjectKey = key, NativeEncoding = encoding, Result = series });
            Results.Add(series.GateResult);
        }

        /// <summary>
        /// The unverified native conventions a read depends on, as a transformation name (an unverified transformation is never
        /// a pass: equal values are NOT RUN), or null when every convention used is verified by the committed calibration.
        /// </summary>
        private string Conventions(params string[] ruleIds)
        {
            List<string> unverified = ruleIds.Where(x => !Rules.IsVerified(x)).ToList();
            return unverified.Count == 0 ? null : "native convention unresolved: " + string.Join(", ", unverified);
        }

        private AlignmentStatus HourAlignment()
        {
            return Rules.IsVerified(CalibrationRules.Alignment) && Rules.IsVerified(CalibrationRules.Calendar) ? AlignmentStatus.Verified : AlignmentStatus.Unverified;
        }

        // ---- per space ----------------------------------------------------------------------------------------------------

        private void CompareSpace(ExpectedSpace space)
        {
            ZoneMatch zoneMatch = Identity.For(space.Guid);
            NativeZone zone = zoneMatch != null && zoneMatch.ZoneIndex >= 0 ? Native.Zones[zoneMatch.ZoneIndex] : null;
            IdentityStatus zoneIdentity = zoneMatch?.Status ?? IdentityStatus.NotEstablished;
            string spaceKey = Keys.Space(space.Guid);

            Scalar("1a", spaceKey, space.FloorArea, Field(zone?.FloorArea, zone), zoneIdentity);
            Scalar("2", spaceKey, space.ShellVolume, Field(zone?.Volume, zone), zoneIdentity);
            ModelInterpretation volumeInterpretation = StoredVolume(space, spaceKey);
            Supporting.Add(spaceKey + ": stored Area " + Num(space.StoredArea) + " m2 vs geometric floor " + space.FloorArea + " (reported, S07 1a); identity " + zoneIdentity + " - " + zoneMatch?.Evidence);

            foreach (ExpectedPanel panel in space.Panels)
            {
                CompareSide(space, panel, zone, zoneMatch);
            }

            // A space without a SAM internal condition states every internal-condition quantity as S-UNSPEC; the native
            // conditions are still read and compared, so a non-zero native gain or schedule cannot disappear.
            NativeInternalCondition ic = NormalCondition(zone, out string icProblem);
            ReaderResult icMissing = zone == null ? ReaderResult.Absent("no native zone") : ReaderResult.Unreadable(icProblem);
            ReaderResult[] dayMask = DayCoverage(ic, out string calendarNote);
            if (calendarNote != null) Supporting.Add(spaceKey + ": " + calendarNote);

            CompareGains(space, zone, ic, icMissing, zoneIdentity);
            CompareThermostat(space, ic, icMissing, zoneIdentity, dayMask);
            CompareSchedules(space, ic, icMissing, zoneIdentity, dayMask);
            CompareInfiltrationAndVentilation(space, zone, ic, icMissing, zoneIdentity, dayMask, volumeInterpretation);
            RecordHdd(space, zone);
        }

        private ModelInterpretation StoredVolume(ExpectedSpace space, string spaceKey)
        {
            // D-F9 C: stored SpaceParameter.Volume against the authored-shell geometric volume (OD-W2, approved for PR-B launch).
            QuantityInput input = Input("2", spaceKey + ".storedVolume", space.ShellVolume, ReaderResult.FromValue(space.StoredVolume), IdentityStatus.Verified);
            NumericComparison comparison = space.ShellVolume?.State == SamInputState.Value && !double.IsNaN(space.StoredVolume) ? EQ.Compare(space.ShellVolume.Value, space.StoredVolume, input.Definition.Tolerance) : null;
            if (comparison != null && comparison.IsWithin)
            {
                QuantityResult within = EQ.Evaluate(input, Configuration);
                Results.Add(new QuantityResult(input, within.Outcome, within.Status, "D-F9 C: the stored SpaceParameter.Volume (compared value) agrees with the authored-shell geometric volume (reference). " + within.Reason, within.Comparison, null, null, null, null));
                return ModelInterpretation.NotRequired;
            }

            string reason = double.IsNaN(space.StoredVolume) ? "the space has no stored SpaceParameter.Volume" : "the stored SpaceParameter.Volume " + Num(space.StoredVolume) + " differs from the authored-shell geometric volume " + space.ShellVolume;
            Results.Add(new QuantityResult(input, EquivalenceOutcome.ModelInterpretationRequired, GateStatus.BlockedOwnerDecision, "D-F9 C: " + reason + "; a model interpretation is required and none is recorded.", comparison, null, "record the model interpretation (S07 D-F9) for " + spaceKey, null, null));
            return ModelInterpretation.RequiredNotRecorded;
        }

        private void CompareSide(ExpectedSpace space, ExpectedPanel panel, NativeZone zone, ZoneMatch zoneMatch)
        {
            string side = Keys.Side(panel.Guid, space.Guid);
            SurfaceMatch match = zoneMatch?.Surfaces.Find(x => x.PanelGuid == panel.Guid);
            NativeSurface surface = match != null && match.SurfaceIndex >= 0 && zone != null ? zone.Surfaces[match.SurfaceIndex] : null;
            IdentityStatus identity = match?.Status ?? IdentityStatus.NotEstablished;
            ReaderResult noSurface = zone == null ? ReaderResult.Absent("no native zone") : ReaderResult.Absent("no native surface matches the panel");

            // 1b host area: gross or net of its openings, as the committed calibration (N-1) found; never chosen per fixture.
            double gross = panel.Outer?.GetArea() ?? double.NaN;
            double openings = panel.Apertures.Sum(x => x.Outer?.GetArea() ?? 0);
            string hostRule = Rules.Answer(CalibrationRules.HostSurfaceArea);
            ReaderResult hostNative = surface == null ? noSurface : surface.Area.ToReaderResult();
            if (hostRule == null && panel.Apertures.Count > 0 && hostNative.HasNumber && EQ.Compare(gross, hostNative.Value, QuantityCatalog.Get("1b").Tolerance).IsWithin)
            {
                // N-1 unresolved: the convention that explains the native value is used, and the result stays unverified (NOT RUN).
                hostRule = "gross";
            }

            ExpectedValue hostArea = hostRule == "gross" ? ExpectedValue.FromValue(gross) : ExpectedValue.FromValue(gross - openings);
            Scalar("1b", side, double.IsNaN(gross) || gross <= 0 ? ExpectedValue.Invalid("the panel outline is missing") : hostArea, surface == null ? noSurface : surface.Area.ToReaderResult(), identity, panel.Apertures.Count == 0 ? null : Conventions(CalibrationRules.HostSurfaceArea));

            // 3a: the layers in the order this zone side sees them (zone-facing first). SAM: the first space of the panel sees
            // them as listed (SAM lists inside to outside), a later space reversed. Native: materials(1..n) for reversed = 0,
            // reversed otherwise - the N-2 calibration rule.
            bool samListed = panel.SpaceOrder.Count == 0 || panel.SpaceOrder[0] == space.Guid;
            List<ExpectedLayer> expectedLayers = samListed ? panel.Layers : Enumerable.Reverse(panel.Layers).ToList();
            NativeBuildingElement element = surface == null ? null : Native.BuildingElements.Find(x => x.Guid == surface.BuildingElementGuid);
            List<NativeLayer> nativeLayers = null;
            string orientation = Conventions(CalibrationRules.LayerOrientation);
            if (element != null)
            {
                bool reversed = surface.Reversed.HasNumber && surface.Reversed.Value != 0;
                nativeLayers = reversed ? Enumerable.Reverse(element.Layers).ToList() : element.Layers.ToList();
                if (!surface.Reversed.HasNumber)
                {
                    nativeLayers = null;
                }
            }

            CompareLayers(side, expectedLayers, nativeLayers, surface == null ? noSurface : element == null ? ReaderResult.Absent("the surface has no building element") : ReaderResult.Unreadable("the reversed flag is unreadable"), identity, false, orientation);

            // 3b: U recomputed from the SAM layers and from the native layers, the F-OPQ films chosen per side.
            ElementKind kind = Kind(panel);
            InputEquivalence.HeatFlowDirection direction = Direction(space, panel);
            ExpectedValue expectedU = EQ.ExpectedOpaqueU(expectedLayers.Select(x => LayerResistance.Solid(x.Thickness, Conductivity(x.Material))), direction, kind);
            ReaderResult nativeU = surface == null ? noSurface : NativeU(nativeLayers, surface);
            Scalar(UQuantity(panel), side + ".U", expectedU, nativeU, identity, orientation);

            foreach (ExpectedAperture aperture in panel.Apertures)
            {
                CompareAperture(space, aperture, zone, zoneMatch);
            }
        }

        private static InputEquivalence.HeatFlowDirection Direction(ExpectedSpace space, ExpectedPanel panel)
        {
            if (panel.PanelGroup == PanelGroup.Wall || panel.Outer == null)
            {
                return InputEquivalence.HeatFlowDirection.Horizontal;
            }

            double bottom = space.Panels.Where(x => x.Outer != null).SelectMany(x => x.Outer.GetPoints()).Min(x => x.Z);
            return panel.Outer.GetPoints().Max(x => x.Z) <= bottom + Core.Tolerance.MacroDistance ? InputEquivalence.HeatFlowDirection.Downward : InputEquivalence.HeatFlowDirection.Upward;
        }

        /// <summary>The native surface types the film selection recognises (unchanged from revision 1); no other is assumed.</summary>
        private static readonly Dictionary<string, ElementKind> SurfaceKinds = new Dictionary<string, ElementKind>(StringComparer.Ordinal)
        {
            { "tbdExposed", ElementKind.External },
            { "tbdInternal", ElementKind.Internal },
            { "tbdLink", ElementKind.Internal },
            { "tbdGround", ElementKind.GroundOrAdiabatic },
            { "tbdNullLink", ElementKind.GroundOrAdiabatic }
        };

        private static ReaderResult NativeU(List<NativeLayer> layers, NativeSurface surface)
        {
            if (layers == null || layers.Count == 0)
            {
                return ReaderResult.Absent("no native layers");
            }

            foreach (NativeLayer layer in layers)
            {
                foreach (NativeField field in new[] { layer.MaterialWidth, layer.Conductivity })
                {
                    if (field == null || field.Kind == FieldKind.Unreadable) return ReaderResult.Unreadable("a native layer field is unreadable");
                    if (field.Kind == FieldKind.NaN) return ReaderResult.NotANumber("a native layer field is NaN");
                }
            }

            // Films from the native geometry: inclination 90 is a wall, 0 faces up (roof, ceiling), 180 down (floor), as measured.
            // An inclination that is not a number in 0..180, or a surface type outside the recognised ones, leaves the film and
            // so the native U undeterminable: unreadable with the input's own reader state named, never an assumed film. (Not
            // Absent: against an S-UNSPEC U that would pass, although the native layers exist.)
            if (surface.Inclination == null || !surface.Inclination.HasNumber)
            {
                return ReaderResult.Unreadable("the film selection needs the native inclination, which " + (surface.Inclination == null ? "was not read" : "is " + surface.Inclination) + "; no film is assumed");
            }

            if (surface.Inclination.Value < 0 || surface.Inclination.Value > 180)
            {
                return ReaderResult.Unreadable("the native inclination " + Num(surface.Inclination.Value) + " is outside 0..180; no film is assumed");
            }

            if (!SurfaceKinds.TryGetValue(surface.Type ?? string.Empty, out ElementKind kind))
            {
                return ReaderResult.Unreadable("the film selection needs the native surface type, which " + (surface.Type == null ? "was not read" : "is '" + surface.Type + "', not one of " + string.Join(", ", SurfaceKinds.Keys)) + "; no film is assumed");
            }

            InputEquivalence.HeatFlowDirection direction = surface.Inclination.Value < 60 ? InputEquivalence.HeatFlowDirection.Upward : surface.Inclination.Value > 120 ? InputEquivalence.HeatFlowDirection.Downward : InputEquivalence.HeatFlowDirection.Horizontal;
            ExpectedValue u = EQ.ExpectedOpaqueU(layers.Select(x => LayerResistance.Solid(x.MaterialWidth.Value, x.Conductivity.Value)), direction, kind);
            return u.State == SamInputState.Value ? ReaderResult.FromValue(u.Value) : ReaderResult.Absent("the native layers give no U (" + u.Detail + ")");
        }

        private void CompareLayers(string owner, List<ExpectedLayer> expected, List<NativeLayer> native, ReaderResult missing, IdentityStatus identity, bool pane, string convention, string ownerPrefixOverride = null)
        {
            int count = System.Math.Max(expected.Count, native?.Count ?? 0);
            for (int k = 0; k < count; k++)
            {
                ExpectedLayer e = k < expected.Count ? expected[k] : null;
                NativeLayer n = native != null && k < native.Count ? native[k] : null;
                string[] properties = e != null ? PropertiesOf(e.Material, pane) : pane ? TransparentProperties : OpaqueProperties;
                foreach (string property in properties)
                {
                    string key = Keys.Layer(owner, k + 1, property);
                    ExpectedValue expectedValue = e == null ? ExpectedValue.Unspecified("SAM states no layer " + (k + 1)) : LayerValue(e, property);
                    ReaderResult nativeValue = native == null ? missing : n == null ? ReaderResult.Absent("the native construction has no layer " + (k + 1)) : NativeLayerValue(n, property);
                    Scalar(pane ? "3c.layer" : "3a", key, expectedValue, nativeValue, identity, convention);
                }
            }
        }

        private static ExpectedValue LayerValue(ExpectedLayer layer, string property)
        {
            if (property == "thickness")
            {
                return ExpectedValue.FromValue(layer.Thickness);
            }

            if (!(layer.Material is Core.Material material))
            {
                return ExpectedValue.Invalid("material '" + layer.Name + "' is not in the frozen material library");
            }

            switch (property)
            {
                case "conductivity": return ExpectedValue.FromValue(material.ThermalConductivity);
                case "density": return ExpectedValue.FromValue(material.Density);
                case "specificHeat": return ExpectedValue.FromValue(material.SpecificHeatCapacity);
                case "convectionCoefficient": return Parameter(material, GasMaterialParameter.HeatTransferCoefficient);
                case "dynamicViscosity": return material is GasMaterial gas ? ExpectedValue.FromValue(gas.DynamicViscosity) : ExpectedValue.Unspecified();
            }

            if (material is TransparentMaterial)
            {
                switch (property)
                {
                    case "solarTransmittance": return Parameter(material, TransparentMaterialParameter.SolarTransmittance);
                    case "lightTransmittance": return Parameter(material, TransparentMaterialParameter.LightTransmittance);
                    case "externalSolarReflectance": return Parameter(material, TransparentMaterialParameter.ExternalSolarReflectance);
                    case "internalSolarReflectance": return Parameter(material, TransparentMaterialParameter.InternalSolarReflectance);
                    case "externalLightReflectance": return Parameter(material, TransparentMaterialParameter.ExternalLightReflectance);
                    case "internalLightReflectance": return Parameter(material, TransparentMaterialParameter.InternalLightReflectance);
                    case "externalEmissivity": return Parameter(material, TransparentMaterialParameter.ExternalEmissivity);
                    case "internalEmissivity": return Parameter(material, TransparentMaterialParameter.InternalEmissivity);
                }
            }
            else
            {
                switch (property)
                {
                    case "externalSolarReflectance": return Parameter(material, OpaqueMaterialParameter.ExternalSolarReflectance);
                    case "internalSolarReflectance": return Parameter(material, OpaqueMaterialParameter.InternalSolarReflectance);
                    case "externalLightReflectance": return Parameter(material, OpaqueMaterialParameter.ExternalLightReflectance);
                    case "internalLightReflectance": return Parameter(material, OpaqueMaterialParameter.InternalLightReflectance);
                    case "externalEmissivity": return Parameter(material, OpaqueMaterialParameter.ExternalEmissivity);
                    case "internalEmissivity": return Parameter(material, OpaqueMaterialParameter.InternalEmissivity);
                }
            }

            return ExpectedValue.Invalid("no SAM source for layer property " + property);
        }

        private static ExpectedValue Parameter(Core.Material material, Enum parameter)
        {
            return material.TryGetValue(parameter, out double value) ? ExpectedValue.FromValue(value) : ExpectedValue.Unspecified("not stated");
        }

        private static ReaderResult NativeLayerValue(NativeLayer layer, string property)
        {
            NativeField field;
            switch (property)
            {
                case "thickness": field = layer.MaterialWidth; break;
                case "conductivity": field = layer.Conductivity; break;
                case "density": field = layer.Density; break;
                case "specificHeat": field = layer.SpecificHeat; break;
                case "solarTransmittance": field = layer.SolarTransmittance; break;
                case "lightTransmittance": field = layer.LightTransmittance; break;
                case "externalSolarReflectance": field = layer.ExternalSolarReflectance; break;
                case "internalSolarReflectance": field = layer.InternalSolarReflectance; break;
                case "externalLightReflectance": field = layer.ExternalLightReflectance; break;
                case "internalLightReflectance": field = layer.InternalLightReflectance; break;
                case "externalEmissivity": field = layer.ExternalEmissivity; break;
                case "internalEmissivity": field = layer.InternalEmissivity; break;
                case "convectionCoefficient": field = layer.ConvectionCoefficient; break;
                case "dynamicViscosity": field = layer.DynamicViscosity; break;
                default: field = null; break;
            }

            return field == null ? ReaderResult.Unreadable("the reader has no field for " + property) : field.ToReaderResult();
        }

        private static double Conductivity(IMaterial material)
        {
            return material is Core.Material m ? m.ThermalConductivity : double.NaN;
        }

        // ---- apertures --------------------------------------------------------------------------------------------------

        private void CompareAperture(ExpectedSpace space, ExpectedAperture aperture, NativeZone zone, ZoneMatch zoneMatch)
        {
            string key = Keys.Aperture(aperture.Guid, space.Guid);
            ApertureMatch match = zoneMatch?.Apertures.Find(x => x.ApertureGuid == aperture.Guid);
            IdentityStatus identity = match?.Status ?? IdentityStatus.NotEstablished;
            List<NativeSurface> panes = zone == null || match == null ? new List<NativeSurface>() : match.PaneSurfaces.Select(i => zone.Surfaces[i]).ToList();
            List<NativeSurface> frames = zone == null || match == null ? new List<NativeSurface>() : match.FrameSurfaces.Select(i => zone.Surfaces[i]).ToList();

            // 1b pane and frame areas against the T-FRAME geometry. A 0 % frame has no frame surface (N-3.3).
            Scalar("1b", key + ".pane", aperture.PaneArea, SumAreas(panes, "no native pane surface"), identity, Conventions(CalibrationRules.FrameSurfaces));
            ReaderResult frameNative = SumAreas(frames, "no native frame surface");
            bool zeroFrame = aperture.FrameArea?.State == SamInputState.Zero;
            string frameConvention = zeroFrame ? (Rules.Answer(CalibrationRules.ZeroFrame) == "no-frame-surface" ? null : "T-FRAME 0 %: native frame representation unresolved (N-3.3)") : Conventions(CalibrationRules.FrameSurfaces);
            if (zeroFrame && frameNative.Kind == ReaderKind.Absent && frameConvention == null)
            {
                // S-ZERO with the frame omitted under T-FRAME's verified 0 % behaviour: ACCEPTED-TRANSFORMATION, never an export defect.
                Results.Add(EQ.Evaluate(Input("1b", key + ".frame", aperture.FrameArea, frameNative, identity, AlignmentStatus.Unverified, ContractListing.None, "T-FRAME (0 %: no frame surface, N-3.3 verified)", true), Configuration));
            }
            else
            {
                Scalar("1b", key + ".frame", aperture.FrameArea, frameNative, identity, frameConvention);
            }

            // 3c pane layers and derived U, 3a frame layers, read through the first pane/frame surface's building element.
            NativeBuildingElement paneElement = panes.Count == 0 ? null : Native.BuildingElements.Find(x => x.Guid == panes[0].BuildingElementGuid);
            CompareLayers(key + ".pane", aperture.PaneLayers, paneElement?.Layers, ReaderResult.Absent("no native pane construction"), identity, true, null);
            Scalar("3c.U", key + ".U", GlazingU(aperture.PaneLayers.Select(x => new KeyValuePair<double, IMaterial>(x.Thickness, x.Material))), GlazingUNative(paneElement), identity);

            if (aperture.FrameArea != null && aperture.FrameArea.State == SamInputState.Value)
            {
                NativeBuildingElement frameElement = frames.Count == 0 ? null : Native.BuildingElements.Find(x => x.Guid == frames[0].BuildingElementGuid);
                CompareLayers(key + ".frame", aperture.FrameLayers, frameElement?.Layers, ReaderResult.Absent("no native frame construction"), identity, false, null);
            }

            // 8d window opening (D-F8): closed windows state nothing; the native object is an aperture type on the pane element.
            ExpectedValue opening = aperture.HasOpeningProperties ? ExpectedValue.Invalid("PR-B reads no OpeningProperties; the equivalence fixtures are closed-window (D-FIX)") : ExpectedValue.Unspecified("closed window: no OpeningProperties");
            List<string> types = paneElement?.ApertureTypeNames ?? new List<string>();
            NativeApertureType type = types.Count == 0 ? null : Native.ApertureTypes.Find(x => x.Name == types[0]);
            ReaderResult openingNative = paneElement == null ? ReaderResult.Absent("no native pane element") : types.Count == 0 ? ReaderResult.Absent("the pane element has no aperture type") : type == null ? ReaderResult.Unreadable("aperture type '" + types[0] + "' not found") : type.DischargeCoefficient.ToReaderResult();
            Scalar("8d", key + ".opening", opening, openingNative, identity);

            // T-FRAME uncertainty 4: the frame fields exist only in the T3D, whose window carries the aperture GUID the writer put
            // there. That is writer-only identity, so these checks are reported and never gated.
            List<NativeT3DWindow> windows = Native.T3D?.Windows?.Where(x => x.Description != null && x.Description.Trim().Equals(aperture.Guid.ToString("D"), StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<NativeT3DWindow>();
            NativeT3DWindow window = windows.Count == 1 ? windows[0] : null;
            IdentityStatus t3dIdentity = window == null ? IdentityStatus.NotEstablished : IdentityStatus.WriterOnly;
            ReaderResult framePerc = window == null ? ReaderResult.Absent(windows.Count == 0 ? "no T3D window names this aperture" : "several T3D windows name this aperture") : window.IsPercFrame.Kind == FieldKind.Finite && window.IsPercFrame.Value == 0 ? ReaderResult.Absent("isPercFrame is false: TAS does not use framePerc") : window.FramePerc.ToReaderResult();
            T3DFieldChecks.Add(EQ.Evaluate(Input("1b", key.Replace("@space:" + space.Guid.ToString("D"), string.Empty) + ".t3d.framePerc", aperture.FramePercentage, framePerc, t3dIdentity), Configuration));
            T3DFieldChecks.Add(EQ.Evaluate(Input("3a", key.Replace("@space:" + space.Guid.ToString("D"), string.Empty) + ".t3d.frameWidth", aperture.FrameWidth, window == null ? framePerc : window.FrameWidth.ToReaderResult(), t3dIdentity), Configuration));
            if (window != null)
            {
                Supporting.Add(key + ": T3D window isPercFrame " + window.IsPercFrame + ", framePerc " + window.FramePerc + ", frameWidth " + window.FrameWidth + "; SAM rule '" + aperture.FrameRule + "', SAM GetFace3Ds pane area (cross-check) " + Num(aperture.CrossCheckPaneArea));
            }
        }

        private static ReaderResult SumAreas(List<NativeSurface> surfaces, string none)
        {
            if (surfaces.Count == 0)
            {
                return ReaderResult.Absent(none);
            }

            double sum = 0;
            foreach (NativeSurface surface in surfaces)
            {
                ReaderResult r = surface.Area.ToReaderResult();
                if (!r.HasNumber) return r;
                sum += r.Value;
            }

            return ReaderResult.FromValue(sum);
        }

        private static ExpectedValue GlazingU(IEnumerable<KeyValuePair<double, IMaterial>> layers)
        {
            List<KeyValuePair<double, IMaterial>> list = layers.ToList();
            if (list.Any(x => x.Value is GasMaterial))
            {
                return ExpectedValue.Invalid("a gas layer: S07 gives no gas-gap resistance rule, so the derived glazing U is not computed");
            }

            return EQ.ExpectedGlazingU(list.Select(x => LayerResistance.Solid(x.Key, Conductivity(x.Value))));
        }

        private static ReaderResult GlazingUNative(NativeBuildingElement element)
        {
            if (element == null || element.Layers.Count == 0)
            {
                return ReaderResult.Absent("no native pane construction");
            }

            if (element.Layers.Any(x => x.Type.HasNumber && x.Type.Value == 4))
            {
                return ReaderResult.Unreadable("a native gas layer: no S07 gas-gap rule");
            }

            if (element.Layers.Any(x => !x.MaterialWidth.HasNumber || !x.Conductivity.HasNumber))
            {
                return ReaderResult.Unreadable("a native pane layer field is not a number");
            }

            ExpectedValue u = EQ.ExpectedGlazingU(element.Layers.Select(x => LayerResistance.Solid(x.MaterialWidth.Value, x.Conductivity.Value)));
            return u.State == SamInputState.Value ? ReaderResult.FromValue(u.Value) : ReaderResult.Absent("the native layers give no U");
        }

        // ---- internal conditions ----------------------------------------------------------------------------------------

        /// <summary>The zone's annual internal condition: the one whose day types exclude HDD (native structure, not names).</summary>
        private static NativeInternalCondition NormalCondition(NativeZone zone, out string problem)
        {
            problem = null;
            if (zone == null)
            {
                problem = "no native zone";
                return null;
            }

            List<NativeInternalCondition> normal = zone.InternalConditions.Where(x => !x.DayTypes.Contains("HDD")).ToList();
            if (normal.Count != 1)
            {
                problem = "the zone has " + normal.Count + " internal condition(s) without the HDD day type";
                return null;
            }

            return normal[0];
        }

        /// <summary>
        /// Which simulated days the annual condition applies on: calendar days(i) (index base per N-6) whose day type the
        /// condition carries. Per day: null when it applies; native Absent when the read day type is not carried; Unreadable
        /// when the snapshot does not prove the day's type (no calendar, no days, the day not read exactly once, or without a
        /// type), so missing applicability evidence is never an unmasked profile. Null when the calendar rule is unverified
        /// (then nothing is masked and alignment stays unverified) or there is no condition.
        /// </summary>
        private ReaderResult[] DayCoverage(NativeInternalCondition ic, out string note)
        {
            note = null;
            int first = Rules.Offset(CalibrationRules.Calendar, 1, out bool verified);
            if (!verified || ic == null)
            {
                note = "calendar day-type coverage not checked (N-6 unverified or no condition)";
                return null;
            }

            ReaderResult[] result = new ReaderResult[365];
            string whole = Native.Calendar == null ? "the snapshot holds no native calendar" : Native.Calendar.Days == null ? "the native calendar holds no days" : null;
            if (whole != null)
            {
                note = whole + ": the day-type applicability of every annual schedule is unread";
                for (int d = 0; d < 365; d++) result[d] = ReaderResult.Unreadable(whole + " (N-6): day-type applicability unread");
                return result;
            }

            List<string> missing = new List<string>(), unread = new List<string>();
            for (int d = 0; d < 365; d++)
            {
                List<NativeCalendarDay> days = Native.Calendar.Days.Where(x => x != null && x.Index == d + first).ToList();
                string label = "calendar day " + (d + first).ToString(ci);
                string problem = days.Count != 1 ? label + " was read " + days.Count.ToString(ci) + " times" : days[0].DayType == null ? label + " has no day type (" + (days[0].Detail ?? "none") + ")" : null;
                if (problem != null)
                {
                    result[d] = ReaderResult.Unreadable(problem + " (N-6): day-type applicability unread");
                    unread.Add(problem);
                }
                else if (!ic.DayTypes.Contains(days[0].DayType))
                {
                    result[d] = ReaderResult.Absent("the calendar day's type is not carried by the annual internal condition");
                    missing.Add((d + 1).ToString(ci) + ":" + days[0].DayType);
                }
            }

            List<string> notes = new List<string>();
            if (unread.Count > 0) notes.Add(unread.Count + " calendar day(s) unread: " + string.Join(", ", unread.Take(10)));
            if (missing.Count > 0) notes.Add(missing.Count + " calendar day(s) carry a day type the annual internal condition does not: " + string.Join(", ", missing.Take(10)));
            note = notes.Count == 0 ? null : string.Join("; ", notes);
            return result;
        }

        private static ReaderResult Field(NativeField field, object owner)
        {
            if (owner == null) return ReaderResult.Absent("no native object");
            return field == null ? ReaderResult.Unreadable("the field was not read") : field.ToReaderResult();
        }

        private static NativeProfile GainProfile(NativeInternalCondition ic, string slot)
        {
            return ic?.GainProfiles != null && ic.GainProfiles.TryGetValue(slot, out NativeProfile p) ? p : null;
        }

        private static NativeProfile ThermostatProfile(NativeInternalCondition ic, string slot)
        {
            return ic?.ThermostatProfiles != null && ic.ThermostatProfiles.TryGetValue(slot, out NativeProfile p) ? p : null;
        }

        private static ReaderResult ProfileField(NativeProfile profile, Func<NativeProfile, NativeField> field, ReaderResult missing)
        {
            if (profile == null) return missing;
            if (profile.Kind == FieldKind.Unreadable) return ReaderResult.Unreadable(profile.Detail);
            if (profile.Kind == FieldKind.Absent) return ReaderResult.Absent(profile.Detail);
            NativeField f = field(profile);
            return f == null ? ReaderResult.Unreadable("not read") : f.ToReaderResult();
        }

        private static ExpectedValue Total(ExpectedSpace space, string perArea, string absolute, string perPerson, out double people)
        {
            people = Peoples(space);
            double a = Param(space, perArea), b = Param(space, absolute), c = Param(space, perPerson);
            if (double.IsNaN(a) && double.IsNaN(b) && double.IsNaN(c)) return ExpectedValue.Unspecified("no gain is stated");
            double sum = 0;
            if (!double.IsNaN(a))
            {
                if (double.IsNaN(space.StoredArea) || space.StoredArea <= 0) return ExpectedValue.Invalid(perArea + " needs a positive stored Area");
                sum += a * space.StoredArea;
            }

            if (!double.IsNaN(b)) sum += b;
            if (!double.IsNaN(c))
            {
                if (double.IsNaN(people)) return ExpectedValue.Invalid(perPerson + " needs a people count (Occupancy or Area/AreaPerPerson)");
                sum += c * people;
            }

            return ExpectedValue.FromValue(sum);
        }

        private static double Param(ExpectedSpace space, string name)
        {
            return name != null && space.Parameters.TryGetValue(name, out double value) ? value : double.NaN;
        }

        private static double Peoples(ExpectedSpace space)
        {
            if (!double.IsNaN(space.Occupancy)) return space.Occupancy;
            double app = Param(space, "AreaPerPerson");
            return double.IsNaN(app) || app <= 0 || double.IsNaN(space.StoredArea) ? double.NaN : space.StoredArea / app;
        }

        private static ExpectedValue GainTotal(ExpectedSpace space, string slot, out double people)
        {
            switch (slot)
            {
                case "ticOSG": return Total(space, null, null, "OccupancySensibleGainPerPerson", out people);
                case "ticOLG": return Total(space, null, null, "OccupancyLatentGainPerPerson", out people);
                case "ticLG": return Total(space, "LightingGainPerArea", "LightingGain", "LightingGainPerPerson", out people);
                case "ticESG": return Total(space, "EquipmentSensibleGainPerArea", "EquipmentSensibleGain", "EquipmentSensibleGainPerPerson", out people);
                default: return Total(space, "EquipmentLatentGainPerArea", "EquipmentLatentGain", null, out people);
            }
        }

        private void CompareGains(ExpectedSpace space, NativeZone zone, NativeInternalCondition ic, ReaderResult icMissing, IdentityStatus identity)
        {
            ReaderResult floor = Field(zone?.FloorArea, zone);
            foreach (string slot in GainSlots)
            {
                ExpectedValue total = GainTotal(space, slot, out _);
                ExpectedValue density = total.State == SamInputState.Invalid ? total : EQ.ExpectedGainDensity(total.State == SamInputState.Unspecified ? double.NaN : total.Value, space.StoredArea);
                ReaderResult factor = ProfileField(GainProfile(ic, slot), x => x.Factor, icMissing);
                Scalar("4a.density", Keys.Profile(space.Guid, slot, "factor"), density, factor, identity);
                Scalar("4b", Keys.Profile(space.Guid, slot, "total"), total, EQ.Multiply(factor, floor), identity);
            }

            // People density, derived from native fields only: (sensible + latent occupancy factor) / personGain.
            ExpectedValue people = EQ.ExpectedPeopleDensity(space.Occupancy, Param(space, "AreaPerPerson"), space.StoredArea);
            ReaderResult sensible = ProfileField(GainProfile(ic, "ticOSG"), x => x.Factor, icMissing), latent = ProfileField(GainProfile(ic, "ticOLG"), x => x.Factor, icMissing);
            ReaderResult personGain = ic == null ? icMissing : ic.PersonGain.ToReaderResult();
            ReaderResult nativePeople = PeopleDensity(sensible, latent, personGain);
            Scalar("4a.people", Keys.Space(space.Guid) + ".people", people, nativePeople, identity);

            Scalar("4c", Keys.Space(space.Guid) + ".ic.lightingRadProp", Fraction(space, "LightingRadiantProportion"), ic == null ? icMissing : ic.LightingRadProp.ToReaderResult(), identity);
            Scalar("4c", Keys.Space(space.Guid) + ".ic.occupantRadProp", Fraction(space, "OccupancyRadiantProportion"), ic == null ? icMissing : ic.OccupantRadProp.ToReaderResult(), identity);
            Scalar("4c", Keys.Space(space.Guid) + ".ic.equipmentRadProp", Fraction(space, "EquipmentRadiantProportion"), ic == null ? icMissing : ic.EquipmentRadProp.ToReaderResult(), identity);

            if (ic != null)
            {
                double perPerson = Param(space, "SupplyAirFlowPerPerson");
                Supporting.Add(Keys.Space(space.Guid) + ": freshAirRate (S07 8a, info) " + ic.FreshAirRate + " l/s/p vs SupplyAirFlowPerPerson x 1000 = " + Num(perPerson * 1000) + "; personGain " + ic.PersonGain);
            }
        }

        private static ExpectedValue Fraction(ExpectedSpace space, string name)
        {
            double value = Param(space, name);
            return double.IsNaN(value) ? ExpectedValue.Unspecified("not authored") : value < 0 || value > 1 ? ExpectedValue.Invalid("a fraction outside 0..1") : ExpectedValue.FromValue(value);
        }

        private static ReaderResult PeopleDensity(ReaderResult sensible, ReaderResult latent, ReaderResult personGain)
        {
            foreach (ReaderResult r in new[] { sensible, latent, personGain })
            {
                if (r.Kind == ReaderKind.Unreadable) return ReaderResult.Unreadable(r.Detail);
            }

            foreach (ReaderResult r in new[] { sensible, latent, personGain })
            {
                if (r.Kind == ReaderKind.Absent) return ReaderResult.Absent(r.Detail);
                if (r.Kind == ReaderKind.NaN) return ReaderResult.NotANumber(r.Detail);
            }

            if (personGain.Value == 0)
            {
                return ReaderResult.Absent("personGain is 0: the native model states no per-person basis");
            }

            return ReaderResult.FromValue((sensible.Value + latent.Value) / personGain.Value);
        }

        // ---- schedules ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// The native value of a profile's shape in SAM hour <paramref name="hour"/> (0..8759), through the committed slot
        /// conventions: value profiles hold one value, hourly profiles hourlyValues[hour % 24 + offset], yearly profiles the
        /// GetYearlyValues() element at CLR index hour + offset. Function profiles are not readable as values.
        /// </summary>
        private ReaderResult Shape(NativeProfile profile, int hour, ReaderResult[] dayMask, out string encoding)
        {
            encoding = profile?.Type;
            if (profile == null) return ReaderResult.Absent("no native profile");
            if (profile.Kind != FieldKind.Finite) return profile.Kind == FieldKind.Unreadable ? ReaderResult.Unreadable(profile.Detail) : ReaderResult.Absent(profile.Detail);
            if (dayMask != null && dayMask[hour / 24] != null) return dayMask[hour / 24];

            switch (profile.TypeCode)
            {
                case 1:
                    return profile.Value.ToReaderResult();

                case 2:
                    // The profile is present, so a slot the reader holds no value for is unread, never native absence.
                    int slot = hour % 24 + Rules.Offset(CalibrationRules.HourlySlot, 1, out _);
                    return profile.Hourly != null && profile.Hourly.TryGetValue(slot, out NativeField field) ? (field?.ToReaderResult() ?? ReaderResult.Unreadable("hourly slot " + slot + " not read")) : ReaderResult.Unreadable("hourly slot " + slot + " not read");

                case 3:
                    int offset = Rules.Offset(CalibrationRules.YearlyIndex, 1, out _);
                    string domain = AnnualDomainProblem(profile.Yearly, offset, "the yearly profile");
                    return domain != null ? ReaderResult.Unreadable(domain) : (profile.Yearly ?? NativeSeries.Absent()).At(hour + offset).ToReaderResult();

                default:
                    return ReaderResult.Unreadable("a TAS function profile (" + profile.Type + "): its schedule is not readable as hourly values");
            }
        }

        /// <summary>
        /// Why a read native annual array is outside the calibrated domain (N-5: CLR index = SAM hour + offset, lower bound =
        /// offset, exactly 8760 elements), or null when it is inside it or was not read. Outside the domain no element can be
        /// attributed to a SAM hour: the array is never compared as a prefix, truncated or padded.
        /// </summary>
        private static string AnnualDomainProblem(NativeSeries series, int offset, string what)
        {
            if (series == null || !series.IsRead || (series.Values.Length == 8760 && series.LowerBound == offset))
            {
                return null;
            }

            return what + " has " + series.Values.Length + " elements at lower bound " + series.LowerBound + "; the calibrated domain (N-5) is 8760 elements at lower bound " + offset + ", so it is not compared, truncated or padded";
        }

        private List<ReaderResult> NativeHours(NativeProfile profile, ReaderResult missing, ReaderResult[] dayMask, Func<ReaderResult, ReaderResult> scale, out string encoding)
        {
            encoding = profile?.Type ?? "none";
            List<ReaderResult> result = new List<ReaderResult>(8760);
            for (int h = 0; h < 8760; h++)
            {
                ReaderResult shape = profile == null ? missing : Shape(profile, h, dayMask, out _);
                result.Add(scale == null ? shape : scale(shape));
            }

            return result;
        }

        private string ShapeConventions(NativeProfile profile)
        {
            if (profile?.TypeCode == 2) return Conventions(CalibrationRules.HourlySlot);
            if (profile?.TypeCode == 3) return Conventions(CalibrationRules.YearlyIndex);
            return null;
        }

        private static List<ExpectedValue> ExpectedHours(ExpectedProfile profile, double scale, string unspecified)
        {
            if (profile == null)
            {
                return Enumerable.Repeat(ExpectedValue.Unspecified(unspecified), 8760).ToList();
            }

            if (!profile.Expanded.IsOk)
            {
                return null;
            }

            return profile.Expanded.Hours.Select(x => ExpectedValue.FromValue(x * scale)).ToList();
        }

        private void HourlySeries(string quantityId, string key, ExpectedProfile expected, double scale, string unspecified, List<ReaderResult> native, IdentityStatus identity, string convention, string encoding, ModelInterpretation interpretation = ModelInterpretation.NotRequired)
        {
            if (expected != null && !expected.Expanded.IsOk)
            {
                // A schedule S07 cannot expand: 8784 values are an UNSUPPORTED-MAPPING (never truncated), anything else S-INVALID.
                QuantityInput prototype = Input(quantityId, key, null, null, identity, HourAlignment(), ContractListing.None, convention, false, interpretation);
                Results.Add(EQ.EvaluateScheduleExpansion(expected.Expanded, prototype, Configuration));
                return;
            }

            SeriesOf(quantityId, key, ExpectedHours(expected, scale, unspecified), native, identity, HourAlignment(), convention, encoding, interpretation);
        }

        private void CompareThermostat(ExpectedSpace space, NativeInternalCondition ic, ReaderResult icMissing, IdentityStatus identity, ReaderResult[] dayMask)
        {
            foreach (string slot in ThermostatSlots)
            {
                NativeProfile profile = ThermostatProfile(ic, slot);
                ExpectedProfile expected = space.Profiles.TryGetValue(slot, out ExpectedProfile p) ? p : null;
                ReaderResult factor = ProfileField(profile, x => x.Factor, icMissing);
                List<ReaderResult> native = NativeHours(profile, icMissing, dayMask, shape => EQ.Multiply(shape, factor), out string encoding);
                HourlySeries(slot == "ticLL" || slot == "ticUL" ? "5.temperature" : "5.humidity", Keys.Profile(space.Guid, slot, "hourly"), expected, 1, "no SAM " + slot + " profile", native, identity, ShapeConventions(profile), encoding + " x factor");
            }
        }

        private void CompareSchedules(ExpectedSpace space, NativeInternalCondition ic, ReaderResult icMissing, IdentityStatus identity, ReaderResult[] dayMask)
        {
            foreach (string slot in ScheduleSlots)
            {
                // A slot without a SAM profile is S-UNSPEC for every hour: the core decides it against the native schedule
                // (absent, zero, non-zero or unreadable) with its unchanged rules; it is never left out of the comparison.
                NativeProfile profile = GainProfile(ic, slot);
                ExpectedProfile expected = ProfileFor(space, slot);
                string unspecified = "no SAM " + slot + " profile";
                List<ReaderResult> native = NativeHours(profile, icMissing, dayMask, null, out string encoding);
                HourlySeries("6.hourly", Keys.Profile(space.Guid, slot, "hourly"), expected, 1, unspecified, native, identity, ShapeConventions(profile), encoding);

                // EFLH: supporting evidence only (S07 section 7); it never stands in for the hourly comparison.
                ExpectedValue eflh = expected == null ? ExpectedValue.Unspecified(unspecified) : expected.Expanded.IsOk ? EQ.ExpectedEquivalentFullLoadHours(expected.Expanded) : ExpectedValue.Invalid(expected.Expanded.Detail);
                Scalar("6.eflh", Keys.Profile(space.Guid, slot, "eflh"), eflh, Sum(native), identity, ShapeConventions(profile));
            }
        }

        private static ReaderResult Sum(List<ReaderResult> values)
        {
            double sum = 0;
            foreach (ReaderResult r in values)
            {
                if (r.Kind == ReaderKind.Unreadable) return ReaderResult.Unreadable(r.Detail);
                if (r.Kind == ReaderKind.Absent) return ReaderResult.Absent(r.Detail);
                if (r.Kind == ReaderKind.NaN) return ReaderResult.NotANumber(r.Detail);
                sum += r.Value;
            }

            return ReaderResult.FromValue(sum);
        }

        private void CompareInfiltrationAndVentilation(ExpectedSpace space, NativeZone zone, NativeInternalCondition ic, ReaderResult icMissing, IdentityStatus identity, ReaderResult[] dayMask, ModelInterpretation volumeInterpretation)
        {
            ReaderResult volume = Field(zone?.Volume, zone);
            double geometricVolume = space.ShellVolume?.State == SamInputState.Value ? space.ShellVolume.Value : double.NaN;

            // 7a, 7b. The writer skips infiltration without a profile (S07 7a): a stated ACH then meets the TAS default.
            double ach = Param(space, "InfiltrationAirChangesPerHour");
            ReaderResult factor = ProfileField(GainProfile(ic, "ticI"), x => x.Factor, icMissing);
            Scalar("7a", Keys.Profile(space.Guid, "ticI", "factor"), ExpectedValue.FromValue(ach), factor, identity);
            Scalar("7b", Keys.Profile(space.Guid, "ticI", "flow"), EQ.ExpectedInfiltrationFlow(ach, geometricVolume), EQ.Multiply(factor, volume, 1 / SecondsPerHour), identity, null, volumeInterpretation);

            // 8a: the requirement crossed in the [SAM_META_V1] carrier. It proves crossing only, never delivery.
            Dictionary<string, ReaderResult> carrier = Carrier(zone);
            Scalar("8a", Keys.Space(space.Guid) + ".ventilation.flow", ExpectedValue.FromValue(Param(space, "SupplyAirFlow")), carrier["flow"], identity);
            Scalar("8a", Keys.Space(space.Guid) + ".ventilation.flowPerArea", ExpectedValue.FromValue(Param(space, "SupplyAirFlowPerArea")), carrier["flowPerArea"], identity);
            Scalar("8a", Keys.Space(space.Guid) + ".ventilation.flowPerPerson", ExpectedValue.FromValue(Param(space, "SupplyAirFlowPerPerson")), carrier["flowPerPerson"], identity);
            Scalar("8a", Keys.Space(space.Guid) + ".ventilation.airChangesPerHour", ExpectedValue.FromValue(Param(space, "SupplyAirChangesPerHour")), carrier["airChangesPerHour"], identity);

            // 8b: the delivery, per the D-F2 state of the frozen model.
            VentilationRequirement requirement = new VentilationRequirement { Flow = Param(space, "SupplyAirFlow"), FlowPerArea = Param(space, "SupplyAirFlowPerArea"), FlowPerPerson = Param(space, "SupplyAirFlowPerPerson"), AirChangesPerHour = Param(space, "SupplyAirChangesPerHour") };
            ExpectedProfile ventilationProfile = ProfileFor(space, "ticV");
            VentilationState state = EQ.ClassifyVentilation(requirement, ventilationProfile != null);
            ExpectedValue required = EQ.ExpectedRequiredSupplyFlow(requirement, space.StoredArea, Peoples(space), geometricVolume);
            NativeProfile ticV = GainProfile(ic, "ticV");
            ReaderResult ticVFactor = ProfileField(ticV, x => x.Factor, icMissing);
            string key = Keys.Profile(space.Guid, "ticV", "delivered");
            Supporting.Add(Keys.Space(space.Guid) + ": D-F2 state " + state + "; required supply " + required + "; native ticV " + (ticV == null ? "none" : ticV.Type + " factor " + ticV.Factor + " value " + ticV.Value));

            List<ReaderResult> delivered = NativeHours(ticV, icMissing, dayMask, shape => EQ.Multiply(EQ.Multiply(shape, ticVFactor), volume, 1 / SecondsPerHour), out string encoding);
            if (state == VentilationState.V2 && required.State == SamInputState.Value && ventilationProfile != null)
            {
                HourlySeries("8b", key, ventilationProfile, required.Value, null, delivered, identity, ShapeConventions(ticV), encoding + " x factor x zone.volume / 3600", volumeInterpretation);
                return;
            }

            // V0, V1 and the undefined state: one scalar against the native peak delivered flow (Tas intent: no ticV).
            ReaderResult peak = Peak(delivered);
            Results.Add(EQ.Evaluate(Input("8b", key, EQ.ExpectedDeliveredOutdoorAir(state, required), peak, identity, HourAlignment(), ContractListing.None, ShapeConventions(ticV), false, volumeInterpretation), Configuration));
        }

        private static ReaderResult Peak(List<ReaderResult> values)
        {
            double peak = double.NegativeInfinity;
            foreach (ReaderResult r in values)
            {
                if (r.Kind == ReaderKind.Unreadable) return ReaderResult.Unreadable(r.Detail);
                if (r.Kind == ReaderKind.NaN) return ReaderResult.NotANumber(r.Detail);
                if (r.HasNumber) peak = System.Math.Max(peak, r.Value);
            }

            return double.IsNegativeInfinity(peak) ? ReaderResult.Absent("no native ventilation hour") : ReaderResult.FromValue(peak);
        }

        private static Dictionary<string, ReaderResult> Carrier(NativeZone zone)
        {
            Dictionary<string, ReaderResult> result = new Dictionary<string, ReaderResult>(StringComparer.Ordinal);
            foreach (string basis in VentilationBases) result[basis] = ReaderResult.Absent("the [SAM_META_V1] carrier states no " + basis);
            if (zone == null)
            {
                foreach (string basis in VentilationBases) result[basis] = ReaderResult.Absent("no native zone");
                return result;
            }

            string segment = zone.Description?.Split(';').Select(x => x.Trim()).FirstOrDefault(x => x.StartsWith("[SAM_META_V1]=", StringComparison.Ordinal));
            if (segment == null)
            {
                return result;
            }

            try
            {
                System.Text.Json.Nodes.JsonObject json = System.Text.Json.Nodes.JsonNode.Parse(segment.Substring("[SAM_META_V1]=".Length)) as System.Text.Json.Nodes.JsonObject;
                System.Text.Json.Nodes.JsonObject ventilation = json?["ventilation"] as System.Text.Json.Nodes.JsonObject;
                foreach (string basis in VentilationBases)
                {
                    if (ventilation != null && ventilation.TryGetPropertyValue(basis, out System.Text.Json.Nodes.JsonNode node) && node != null)
                    {
                        result[basis] = ReaderResult.FromValue(node.GetValue<double>());
                    }
                }
            }
            catch (Exception exception)
            {
                foreach (string basis in VentilationBases) result[basis] = ReaderResult.Unreadable("the [SAM_META_V1] carrier does not parse: " + exception.GetType().Name);
            }

            return result;
        }

        private void RecordHdd(ExpectedSpace space, NativeZone zone)
        {
            NativeInternalCondition hdd = zone?.InternalConditions.Find(x => x.DayTypes.Count == 1 && x.DayTypes[0] == "HDD");
            if (hdd == null)
            {
                Supporting.Add(Keys.Space(space.Guid) + ": no HDD internal condition read");
                return;
            }

            string Profile(NativeProfile p) => p == null ? "none" : p.Slot + "=" + p.Type + " f " + p.Factor + " v " + p.Value;
            Supporting.Add(Keys.Space(space.Guid) + ": HDD internal condition (sizing day type only; S07 defines no expectation, not gated): " + string.Join("; ", hdd.GainProfiles.Values.Concat(hdd.ThermostatProfiles.Values).Select(Profile)));
        }

        // ---- native objects SAM never stated ----------------------------------------------------------------------------

        private void CompareUnmatched()
        {
            foreach (int z in Identity.UnmatchedZones)
            {
                NativeZone zone = Native.Zones[z];
                Results.Add(EQ.Evaluate(Input("1a", "native:zone[" + z.ToString(ci) + "]", ExpectedValue.Unspecified("no SAM space"), Field(zone.FloorArea, zone), IdentityStatus.Mismatch), Configuration));
            }

            foreach (ZoneMatch match in Identity.Zones.Where(x => x.ZoneIndex >= 0))
            {
                foreach (int s in match.UnmatchedSurfaces)
                {
                    NativeSurface surface = Native.Zones[match.ZoneIndex].Surfaces[s];
                    Results.Add(EQ.Evaluate(Input("1b", "native:zone[" + match.ZoneIndex.ToString(ci) + "].surface[" + s.ToString(ci) + "]", ExpectedValue.Unspecified("no SAM panel or aperture"), surface.Area.ToReaderResult(), IdentityStatus.Mismatch), Configuration));
                }
            }
        }

        // ---- weather and design days --------------------------------------------------------------------------------------

        private void CompareWeather()
        {
            ExpectedWeather e = Expected.Weather;
            NativeWeather w = Native.Weather;
            bool nativePresent = w != null && w.Kind == FieldKind.Finite;
            ReaderResult missing = w == null ? ReaderResult.Unreadable("weather not read") : w.Kind == FieldKind.Unreadable ? ReaderResult.Unreadable(w.Detail) : ReaderResult.Absent(w.Detail);
            ExpectedValue Of(double value) => e.Present ? ExpectedValue.FromValue(value) : ExpectedValue.Unspecified("no SAM weather");
            ReaderResult N(NativeField field) => nativePresent ? field.ToReaderResult() : missing;

            // One weather year on each side: the pairing is structural, not a writer name.
            IdentityStatus identity = IdentityStatus.Verified;
            Scalar("9", Keys.Weather("latitude"), Of(e.Latitude), N(w?.Latitude), identity);
            Scalar("9", Keys.Weather("longitude"), Of(e.Longitude), N(w?.Longitude), identity);
            Scalar("9", Keys.Weather("altitude"), Of(e.Elevation), N(w?.Altitude), identity);
            Scalar("9", Keys.Weather("timeZone"), Of(e.TimeZone), N(w?.TimeZone), identity);
            Scalar("9", Keys.Weather("building.latitude"), Of(e.Latitude), Native.Building?.Latitude?.ToReaderResult() ?? missing, identity);
            Scalar("9", Keys.Weather("building.longitude"), Of(e.Longitude), Native.Building?.Longitude?.ToReaderResult() ?? missing, identity);
            Scalar("9", Keys.Weather("building.timeZone"), Of(e.TimeZone), Native.Building?.TimeZone?.ToReaderResult() ?? missing, identity);
            Scalar("9.ground", Keys.Weather("groundTemperature"), Of(e.GroundTemperature), N(w?.GroundTemperature), identity);

            int offset = Rules.Offset(CalibrationRules.WeatherIndex, 1, out _);
            string convention = Conventions(CalibrationRules.WeatherIndex);
            foreach (KeyValuePair<int, Weather.WeatherDataType> item in ReadBackExpected.AnnualWeather)
            {
                string key = Keys.Weather("annual." + item.Value);
                if (!e.Present)
                {
                    Scalar("9", key, ExpectedValue.Unspecified("no SAM weather"), nativePresent && w.Annual.TryGetValue(item.Key, out NativeSeries present) && present.IsRead ? ReaderResult.FromValue(present.Values.Length) : missing, identity);
                    continue;
                }

                if (!e.Annual.TryGetValue(item.Key, out double[] values))
                {
                    // Not 365 days: never truncated or padded (owner leap-year policy).
                    Scalar("9", key, e.DayCount == 366 ? ExpectedValue.FromValue(8784) : ExpectedValue.Invalid("the SAM weather year has " + e.DayCount + " days"), ReaderResult.Absent("the Tas route keeps 365 days"), identity, null, ModelInterpretation.NotRequired, e.DayCount == 366 ? ContractListing.UnsupportedMapping : ContractListing.None);
                    continue;
                }

                NativeSeries series = nativePresent && w.Annual.TryGetValue(item.Key, out NativeSeries s) ? s : null;
                string domain = AnnualDomainProblem(series, offset, "the native annual array GetAnnualParameter(" + item.Key + ")");
                List<ReaderResult> native = Enumerable.Range(0, 8760).Select(h => series == null ? missing : domain != null ? ReaderResult.Unreadable(domain) : series.At(h + offset).ToReaderResult()).ToList();
                if (domain != null)
                {
                    Supporting.Add(key + ": " + domain);
                }

                SeriesOf("9", key, values.Select(ExpectedValue.FromValue).ToList(), native, identity, AlignmentStatus.Unverified, convention, "GetAnnualParameter(" + item.Key + ") at CLR index hour + " + offset);
            }
        }

        private void CompareDesignDays()
        {
            Supporting.Add("design days: expected role '" + Expected.DesignDayRole + "' - " + Expected.DesignDayRoleReason);
            CompareDesignDays("cooling", Expected.CoolingDesignDays, Native.CoolingDesignDays);
            CompareDesignDays("heating", Expected.HeatingDesignDays, Native.HeatingDesignDays);
        }

        private void CompareDesignDays(string role, List<ExpectedDesignDay> expected, List<NativeDesignDayGroup> native)
        {
            native = native ?? new List<NativeDesignDayGroup>();

            // One design day per role on each side is a structural pairing; more than one can only be paired by the writer's names.
            IdentityStatus identity = expected.Count == 1 && native.Count == 1 ? IdentityStatus.Verified : expected.Count == native.Count && expected.Select(x => x.Name).SequenceEqual(native.Select(x => x.Name)) ? IdentityStatus.WriterOnly : IdentityStatus.Mismatch;
            int offset = Rules.Offset(CalibrationRules.DesignDaySlot, 1, out _);
            string convention = Conventions(CalibrationRules.DesignDaySlot);

            for (int i = 0; i < expected.Count; i++)
            {
                ExpectedDesignDay e = expected[i];
                NativeDesignDayGroup group = i < native.Count ? native[i] : null;
                List<NativeDesignDay> days = group?.Days ?? new List<NativeDesignDay>();
                NativeDesignDay day = days.Count > 0 ? days[0] : null;
                ReaderResult missing = ReaderResult.Absent(group == null ? "no native " + role + " design day " + i : "the native design day has no day");

                // S07 10: the integer yearDay against the SAM date's day of year in its own year (not a reconstructed date).
                Scalar("10.yearDay", Keys.DesignDay(role, i, "yearDay"), EQ.ExpectedYearDay(e.Year, e.Month, e.Day), day == null ? missing : day.YearDay.ToReaderResult(), identity);
                foreach (KeyValuePair<string, Weather.WeatherDataType> item in ReadBackExpected.DesignDayWeather)
                {
                    Dictionary<int, NativeField> slots = day != null && day.Hours.TryGetValue(item.Key, out Dictionary<int, NativeField> s) ? s : null;
                    List<ReaderResult> values = Enumerable.Range(0, 24).Select(h => slots == null ? missing : slots.TryGetValue(h + offset, out NativeField f) ? f.ToReaderResult() : ReaderResult.Absent("slot not read")).ToList();
                    SeriesOf("10", Keys.DesignDay(role, i, item.Key), e.Hours[item.Key].Select(ExpectedValue.FromValue).ToList(), values, identity, AlignmentStatus.Unverified, convention, "WeatherDay." + item.Key + "[hour + " + offset + "]");
                }

                if (day != null)
                {
                    Supporting.Add(Keys.DesignDay(role, i, "native") + ": name '" + day.Name + "', repetitions " + day.Repetitions + ", day type " + day.DayType + " (reported, S07 10)");
                }

                // A SAM design day is one day (its repetitions are a field of that day). Every calibration run persisted one day
                // per design day; a further day in the group has no measured native meaning, so it is a native object SAM never
                // stated - never compared against the one SAM day, and never a pass.
                for (int j = 1; j < days.Count; j++)
                {
                    NativeDesignDay extra = days[j];
                    string key = Keys.DesignDay(role, i, "yearDay") + ".day[" + j.ToString(ci) + "].native-only";
                    Results.Add(EQ.Evaluate(Input("10.yearDay", key, ExpectedValue.Unspecified("SAM states one day per design day"), extra == null ? ReaderResult.Absent() : extra.YearDay.ToReaderResult(), IdentityStatus.Mismatch), Configuration));
                    Supporting.Add(Keys.DesignDay(role, i, "native") + ".day[" + j.ToString(ci) + "]: a further native day in the design day ('" + extra?.Name + "', yearDay " + extra?.YearDay + "); SAM states one day (unstated native object)");
                }
            }

            for (int i = expected.Count; i < native.Count; i++)
            {
                NativeDesignDay day = native[i].Days.FirstOrDefault();
                Results.Add(EQ.Evaluate(Input("10.yearDay", Keys.DesignDay(role, i, "yearDay") + ".native-only", ExpectedValue.Unspecified("SAM states no such design day"), day == null ? ReaderResult.Absent() : day.YearDay.ToReaderResult(), IdentityStatus.Mismatch), Configuration));
            }
        }

        private static string Num(double value)
        {
            return double.IsNaN(value) ? "NaN" : value.ToString("R", ci);
        }
    }
}
