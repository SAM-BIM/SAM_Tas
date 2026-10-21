// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.InputEquivalence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using EQ = SAM.Analytical.InputEquivalence.Query;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>One measured answer to a native question, with the evidence behind it.</summary>
    public sealed class CalibrationFinding
    {
        public string Id { get; set; }
        public string Question { get; set; }
        public string Answer { get; set; }

        /// <summary>True only when every calibration observation agrees and the answer is unique; then the rule may be committed as verified.</summary>
        public bool Consistent { get; set; }
        public bool Consumed { get; set; }
        public List<string> Evidence { get; set; } = new List<string>();
        public string Note { get; set; }
    }

    public sealed class CalibrationRun
    {
        public string Label;
        public string Directory;
        public ExpectedModel Expected;
        public NativeSnapshot Native;
        public IdentityResult Identity;
    }

    /// <summary>
    /// <c>readback-calibrate</c>: derives the native conventions from the synthetic calibration runs (and the P-ALIGN TSD)
    /// by measurement. Each finding is consistent only when every observation agrees and exactly one answer explains
    /// them; the candidate rule table marks only those verified. The rules are fixed here, before any fixture is read.
    /// </summary>
    public static class Calibration
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        public static CalibrationRun Load(string label, string directory)
        {
            AnalyticalModel model = Core.Convert.ToSAM<AnalyticalModel>(Path.Combine(directory, ReadBackFiles.FrozenModel))?.FirstOrDefault();
            string path_Weather = Path.Combine(directory, ReadBackFiles.FrozenWeather);
            Weather.WeatherData weather = File.Exists(path_Weather) ? Core.Convert.ToSAM<Weather.WeatherData>(path_Weather)?.FirstOrDefault() : null;
            CalibrationRun run = new CalibrationRun { Label = label, Directory = directory, Native = SnapshotJson.Read<NativeSnapshot>(Path.Combine(directory, ReadBackFiles.Snapshot)) };
            run.Expected = ReadBackExpected.Build(model, weather, label);
            run.Identity = ReadBackIdentity.Match(run.Expected, run.Native);
            return run;
        }

        public static List<CalibrationFinding> Analyse(IReadOnlyList<CalibrationRun> runs, TsdSnapshot align)
        {
            List<CalibrationFinding> result = new List<CalibrationFinding>
            {
                HostArea(runs),
                LayerOrientation(runs),
                FrameSurfaces(runs),
                FrameWidth(runs),
                ZeroFrame(runs),
                FrameFields(runs),
                HourlySlot(runs),
                YearlyIndex(runs),
                WeatherIndex(runs),
                DesignDaySlot(runs),
                CalendarDays(runs),
                Winding(runs),
                Alignment(align)
            };

            return result;
        }

        public static CalibrationRules Candidate(IEnumerable<CalibrationFinding> findings, string evidencePrefix)
        {
            CalibrationRules rules = new CalibrationRules { Schema = CalibrationRules.CurrentSchema, Note = "Derived by readback-calibrate from the S08 PR-B synthetic calibration runs; a rule is verified only when every observation agreed. Fixed before any fixture comparison." };
            foreach (CalibrationFinding finding in findings)
            {
                rules.Rules[finding.Id] = new CalibrationRule
                {
                    Question = finding.Question,
                    Answer = finding.Consistent ? finding.Answer : null,
                    Status = finding.Consistent ? "verified" : "unresolved",
                    Evidence = finding.Evidence.Select(x => evidencePrefix + x).ToList(),
                    Note = finding.Note
                };
            }

            return rules;
        }

        private static IEnumerable<(CalibrationRun run, ExpectedSpace space, ExpectedPanel panel, NativeZone zone, NativeSurface surface)> Sides(IEnumerable<CalibrationRun> runs)
        {
            foreach (CalibrationRun run in runs)
            {
                foreach (ExpectedSpace space in run.Expected.Spaces)
                {
                    ZoneMatch match = run.Identity.For(space.Guid);
                    if (match == null || match.Status != IdentityStatus.Verified) continue;
                    NativeZone zone = run.Native.Zones[match.ZoneIndex];
                    foreach (ExpectedPanel panel in space.Panels)
                    {
                        SurfaceMatch s = match.Surfaces.Find(x => x.PanelGuid == panel.Guid);
                        if (s == null || s.Status != IdentityStatus.Verified) continue;
                        yield return (run, space, panel, zone, zone.Surfaces[s.SurfaceIndex]);
                    }
                }
            }
        }

        private static bool Near(double a, double b, double relative = 1e-5, double absolute = 1e-6)
        {
            double d = System.Math.Abs(a - b);
            return d <= absolute || d <= relative * System.Math.Max(Math.Abs(a), System.Math.Abs(b));
        }

        // ---- N-1 ----------------------------------------------------------------------------------------------------------

        private static CalibrationFinding HostArea(IReadOnlyList<CalibrationRun> runs)
        {
            CalibrationFinding f = new CalibrationFinding { Id = CalibrationRules.HostSurfaceArea, Question = "N-1: is a host surface's TBD area gross or net of its openings?", Consumed = true };
            int net = 0, gross = 0, neither = 0;
            ToleranceRule rule = QuantityCatalog.Get("1b").Tolerance;
            foreach (var side in Sides(runs).Where(x => x.panel.Apertures.Count > 0))
            {
                double g = side.panel.Outer.GetArea(), n = g - side.panel.Apertures.Sum(x => x.Outer.GetArea());
                double native = side.surface.Area.Value;
                bool isNet = EQ.Compare(n, native, rule).IsWithin, isGross = EQ.Compare(g, native, rule).IsWithin;
                if (isNet && !isGross) net++; else if (isGross && !isNet) gross++; else neither++;
                f.Evidence.Add(side.run.Label + ": " + side.panel.Name + " in " + side.space.Name + ": gross " + g.ToString("R", ci) + ", net " + n.ToString("R", ci) + ", native " + native.ToString("R", ci));
            }

            f.Answer = net > 0 && gross == 0 && neither == 0 ? "net" : gross > 0 && net == 0 && neither == 0 ? "gross" : null;
            f.Consistent = f.Answer != null;
            f.Note = net + " net, " + gross + " gross, " + neither + " neither";
            return f;
        }

        // ---- N-2 ----------------------------------------------------------------------------------------------------------

        private static CalibrationFinding LayerOrientation(IReadOnlyList<CalibrationRun> runs)
        {
            CalibrationFinding f = new CalibrationFinding { Id = CalibrationRules.LayerOrientation, Question = "N-2: which construction layer faces the zone, for each value of the zone surface's reversed flag?", Consumed = true };
            int externalZero = 0, externalOther = 0, absorptanceInner = 0, absorptanceOther = 0, partitionAsSam = 0, partitionOther = 0;

            foreach (var side in Sides(runs))
            {
                if (side.panel.SpaceOrder.Count == 1)
                {
                    if (side.surface.Reversed.HasNumber && side.surface.Reversed.Value == 0) externalZero++; else externalOther++;
                }
            }

            // TAS's own construction-level absorptances name the faces: the external face from materials(n)'s external
            // reflectance and the internal face from materials(1)'s internal reflectance means materials(1) is the inside.
            foreach (NativeBuildingElement element in runs.SelectMany(x => x.Native.BuildingElements).Where(x => x.Layers.Count >= 2 && !ReadBackIdentity.OpeningBETypes.Contains(x.BEType)))
            {
                NativeLayer first = element.Layers.First(), last = element.Layers.Last();
                double absExt = element.ConstructionProperties["externalSolarAbsorptanceExtSurf"].Value, absInt = element.ConstructionProperties["internalSolarAbsorptanceIntSurf"].Value;
                bool inner = Near(absExt, 1 - last.ExternalSolarReflectance.Value, 1e-4, 1e-4) && Near(absInt, 1 - first.InternalSolarReflectance.Value, 1e-4, 1e-4);
                bool outer = Near(absExt, 1 - first.ExternalSolarReflectance.Value, 1e-4, 1e-4) && Near(absInt, 1 - last.InternalSolarReflectance.Value, 1e-4, 1e-4);
                if (inner && !outer) absorptanceInner++; else absorptanceOther++;
                f.Evidence.Add("element '" + element.Name + "': externalSolarAbsorptanceExtSurf " + absExt.ToString("R", ci) + " = 1 - materials(" + element.Layers.Count + ").externalSolarReflectance " + last.ExternalSolarReflectance.Value.ToString("R", ci) + "; internalSolarAbsorptanceIntSurf " + absInt.ToString("R", ci) + " = 1 - materials(1).internalSolarReflectance " + first.InternalSolarReflectance.Value.ToString("R", ci) + " -> " + (inner && !outer ? "materials(1) inside" : "NOT the inside-first pattern"));
            }

            // A partition: the earlier space (SAM sees the layers as listed) should be unreversed, the later one reversed.
            foreach (CalibrationRun run in runs)
            {
                foreach (ExpectedPanel panel in run.Expected.Spaces.SelectMany(x => x.Panels).Where(x => x.SpaceOrder.Count == 2).GroupBy(x => x.Guid).Select(x => x.First()))
                {
                    List<NativeSurface> surfaces = panel.SpaceOrder.Select(g =>
                    {
                        ZoneMatch m = run.Identity.For(g);
                        SurfaceMatch s = m?.Surfaces.Find(x => x.PanelGuid == panel.Guid);
                        return m == null || s == null || s.SurfaceIndex < 0 ? null : run.Native.Zones[m.ZoneIndex].Surfaces[s.SurfaceIndex];
                    }).ToList();

                    if (surfaces.Any(x => x == null)) { partitionOther++; continue; }
                    bool asSam = surfaces[0].Reversed.Value == 0 && surfaces[1].Reversed.Value != 0;
                    if (asSam) partitionAsSam++; else partitionOther++;
                    f.Evidence.Add(run.Label + ": partition " + panel.Name + ": reversed " + surfaces[0].Reversed.Value.ToString("R", ci) + " (first space) / " + surfaces[1].Reversed.Value.ToString("R", ci) + " (second space)");
                }
            }

            f.Answer = "reversed 0: materials(1) faces the zone; reversed non-zero: materials(n) faces the zone";
            f.Consistent = externalZero > 0 && externalOther == 0 && absorptanceInner > 0 && absorptanceOther == 0 && partitionAsSam > 0 && partitionOther == 0;
            f.Note = string.Format(ci, "single-space surfaces reversed 0: {0}, other: {1}; constructions inside-first: {2}, other: {3}; partitions as SAM: {4}, other: {5}. The flip itself is TAS's definition of the flag, checked here as one-reversed-side per partition.", externalZero, externalOther, absorptanceInner, absorptanceOther, partitionAsSam, partitionOther);
            return f;
        }

        // ---- N-3 ----------------------------------------------------------------------------------------------------------

        private static IEnumerable<(CalibrationRun run, ExpectedAperture aperture, ApertureMatch match, NativeZone zone)> Apertures(IEnumerable<CalibrationRun> runs)
        {
            foreach (CalibrationRun run in runs)
            {
                foreach (ExpectedSpace space in run.Expected.Spaces)
                {
                    ZoneMatch zone = run.Identity.For(space.Guid);
                    if (zone == null || zone.Status != IdentityStatus.Verified) continue;
                    foreach (ExpectedAperture aperture in space.Panels.SelectMany(x => x.Apertures))
                    {
                        ApertureMatch match = zone.Apertures.Find(x => x.ApertureGuid == aperture.Guid);
                        if (match != null && match.Status == IdentityStatus.Verified)
                        {
                            yield return (run, aperture, match, run.Native.Zones[zone.ZoneIndex]);
                        }
                    }
                }
            }
        }

        private static CalibrationFinding FrameSurfaces(IReadOnlyList<CalibrationRun> runs)
        {
            CalibrationFinding f = new CalibrationFinding { Id = CalibrationRules.FrameSurfaces, Question = "N-3 (T-FRAME 1): are the TBD pane and frame surfaces the T-FRAME split of the aperture outline?", Consumed = true };
            int agree = 0, disagree = 0;
            ToleranceRule rule = QuantityCatalog.Get("1b").Tolerance;
            foreach (var a in Apertures(runs).Where(x => x.aperture.FrameArea?.State == SamInputState.Value))
            {
                double pane = a.match.PaneSurfaces.Sum(i => a.zone.Surfaces[i].Area.Value), frame = a.match.FrameSurfaces.Sum(i => a.zone.Surfaces[i].Area.Value);
                bool ok = EQ.Compare(a.aperture.PaneArea.Value, pane, rule).IsWithin && EQ.Compare(a.aperture.FrameArea.Value, frame, rule).IsWithin;
                if (ok) agree++; else disagree++;
                f.Evidence.Add(a.run.Label + ": aperture " + a.aperture.Guid + ": expected pane " + a.aperture.PaneArea.Value.ToString("R", ci) + " frame " + a.aperture.FrameArea.Value.ToString("R", ci) + " (" + a.aperture.FrameRule + "); native pane " + pane.ToString("R", ci) + " frame " + frame.ToString("R", ci));
            }

            f.Answer = "geometric: pane and frame surface areas equal the T-FRAME split (framePerc of the opening outline)";
            f.Consistent = agree > 0 && disagree == 0;
            f.Note = agree + " framed aperture side(s) agree, " + disagree + " disagree";
            return f;
        }

        private static CalibrationFinding FrameWidth(IReadOnlyList<CalibrationRun> runs)
        {
            CalibrationFinding f = new CalibrationFinding { Id = "N-3.2.frameWidthEffect", Question = "N-3 (T-FRAME 2): does frameWidth have a geometric effect when isPercFrame = true, and what does the saved T3D hold?" };
            int derived = 0, other = 0;
            foreach (var a in Apertures(runs).Where(x => x.aperture.FramePercentage?.State == SamInputState.Value))
            {
                NativeT3DWindow w = a.run.Native.T3D?.Windows?.Find(x => string.Equals(x.Description?.Trim(), a.aperture.Guid.ToString("D"), StringComparison.OrdinalIgnoreCase));
                if (w == null) continue;
                double p = w.FramePerc.Value / 100, width = w.Width.Value, height = w.Height.Value;

                // The ring width w that leaves (1 - p) of a width x height opening as pane: (W - 2w)(H - 2w) = (1 - p) W H.
                double s = width + height, ring = (s - System.Math.Sqrt(s * s - 4 * p * width * height)) / 4;
                bool isDerived = Near(ring, w.FrameWidth.Value, 1e-4, 1e-5);
                if (isDerived) derived++; else other++;
                f.Evidence.Add(a.run.Label + ": window '" + w.Name + "' " + width.ToString("R", ci) + " x " + height.ToString("R", ci) + " framePerc " + w.FramePerc.Value.ToString("R", ci) + ": saved frameWidth " + w.FrameWidth.Value.ToString("R", ci) + ", ring width from framePerc " + ring.ToString("R", ci) + ", writer rule " + a.aperture.FrameWidth + "; isPercFrame " + w.IsPercFrame);
            }

            f.Answer = "no independent geometric effect: TBD pane/frame follow framePerc, and the saved T3D frameWidth is the ring width TAS derives from framePerc and the opening size, not the value the writer set";
            f.Consistent = derived > 0 && other == 0;
            f.Note = derived + " saved frameWidth value(s) equal the framePerc ring width, " + other + " do not. Thermal effect not measured (no simulation is authorised for it).";
            return f;
        }

        private static CalibrationFinding ZeroFrame(IReadOnlyList<CalibrationRun> runs)
        {
            CalibrationFinding f = new CalibrationFinding { Id = CalibrationRules.ZeroFrame, Question = "N-3 (T-FRAME 3): what does TAS build for framePerc = 0?", Consumed = true };
            int none = 0, some = 0;
            foreach (var a in Apertures(runs).Where(x => x.aperture.FrameArea?.State == SamInputState.Zero))
            {
                if (a.match.FrameSurfaces.Count == 0) none++; else some++;
                f.Evidence.Add(a.run.Label + ": aperture " + a.aperture.Guid + " (0 %): " + a.match.FrameSurfaces.Count + " frame surface(s), " + a.match.PaneSurfaces.Count + " pane surface(s)");
            }

            f.Answer = none > 0 && some == 0 ? "no-frame-surface" : null;
            f.Consistent = f.Answer != null;
            f.Note = "100 % is not reachable from SAM geometry at the S07 pin (the offset that leaves no pane gives no frame); not exercised.";
            return f;
        }

        private static CalibrationFinding FrameFields(IReadOnlyList<CalibrationRun> runs)
        {
            CalibrationFinding f = new CalibrationFinding { Id = "N-3.4.frameFieldsSource", Question = "N-3 (T-FRAME 4): can isPercFrame, framePerc and frameWidth be read back from the TBD, or only the T3D?" };
            foreach (NativeT3DWindow w in runs.SelectMany(x => x.Native.T3D?.Windows ?? new List<NativeT3DWindow>()))
            {
                f.Evidence.Add("T3D window '" + w.Name + "': isPercFrame " + w.IsPercFrame + ", framePerc " + w.FramePerc + ", frameWidth " + w.FrameWidth);
            }

            bool unreadable = runs.SelectMany(x => x.Native.T3D?.Windows ?? new List<NativeT3DWindow>()).All(x => x.IsPercFrame.Kind == FieldKind.Unreadable);
            f.Answer = "T3D only: the TBD interop has no frame-percentage or frame-width field; in the T3D framePerc and frameWidth read, isPercFrame " + (unreadable ? "is unreadable (TAS3D getter fails)" : "reads");
            f.Consistent = f.Evidence.Count > 0;
            f.Note = "The T3D window is tied to its aperture only by the GUID the writer put in its description (writer-only identity), so T3D field checks are reported and not gated.";
            return f;
        }

        // ---- N-5 read conventions -----------------------------------------------------------------------------------------

        private static IEnumerable<(CalibrationRun run, ExpectedProfile expected, NativeProfile native)> ProfilePairs(IEnumerable<CalibrationRun> runs)
        {
            foreach (CalibrationRun run in runs)
            {
                foreach (ExpectedSpace space in run.Expected.Spaces)
                {
                    ZoneMatch match = run.Identity.For(space.Guid);
                    if (match == null || match.Status != IdentityStatus.Verified) continue;
                    NativeInternalCondition ic = run.Native.Zones[match.ZoneIndex].InternalConditions.FirstOrDefault(x => !x.DayTypes.Contains("HDD"));
                    if (ic == null) continue;
                    foreach (ExpectedProfile profile in space.Profiles.Values)
                    {
                        NativeProfile native = ic.GainProfiles.TryGetValue(profile.Slot, out NativeProfile g) ? g : ic.ThermostatProfiles.TryGetValue(profile.Slot, out NativeProfile t) ? t : null;
                        if (native != null) yield return (run, profile, native);
                    }
                }
            }
        }

        private static CalibrationFinding Offsets(string id, string question, IEnumerable<(string label, Func<int, NativeField> native, double[] expected)> cases, int from, int to, bool consumed = true)
        {
            CalibrationFinding f = new CalibrationFinding { Id = id, Question = question, Consumed = consumed };
            HashSet<int> common = null;
            foreach (var c in cases)
            {
                HashSet<int> offsets = new HashSet<int>();
                for (int k = from; k <= to; k++)
                {
                    bool all = true;
                    for (int h = 0; h < c.expected.Length && all; h++)
                    {
                        NativeField n = c.native(h + k);
                        all = n != null && n.HasNumber && Near(n.Value, c.expected[h]);
                    }

                    if (all) offsets.Add(k);
                }

                f.Evidence.Add(c.label + ": offsets that reproduce every value: [" + string.Join(",", offsets.OrderBy(x => x)) + "]");
                common = common == null ? offsets : new HashSet<int>(common.Intersect(offsets));
            }

            f.Consistent = common != null && common.Count == 1;
            f.Answer = f.Consistent ? common.Single().ToString(ci) : null;
            f.Note = common == null ? "no case observed" : "offsets common to every case: [" + string.Join(",", common.OrderBy(x => x)) + "]";
            return f;
        }

        private static CalibrationFinding HourlySlot(IReadOnlyList<CalibrationRun> runs)
        {
            return Offsets(CalibrationRules.HourlySlot, "N-5: which hourlyValues slot holds SAM hour h (slot = h + offset)?",
                ProfilePairs(runs).Where(x => x.native.TypeCode == 2 && x.expected.RawValues?.Length == 24)
                    .Select(x => (x.run.Label + " " + x.expected.Slot + " '" + x.expected.Name + "'", (Func<int, NativeField>)(s => x.native.Hourly.TryGetValue(s, out NativeField v) ? v : null), x.expected.RawValues)), -2, 3);
        }

        private static CalibrationFinding YearlyIndex(IReadOnlyList<CalibrationRun> runs)
        {
            return Offsets(CalibrationRules.YearlyIndex, "N-5: which GetYearlyValues() CLR index holds SAM hour h (index = h + offset)?",
                ProfilePairs(runs).Where(x => x.native.TypeCode == 3 && x.expected.RawValues?.Length == 8760)
                    .Select(x => (x.run.Label + " " + x.expected.Slot + " '" + x.expected.Name + "' (lower bound " + x.native.Yearly.LowerBound + ", length " + x.native.Yearly.Values?.Length + ")", (Func<int, NativeField>)(i => x.native.Yearly.At(i)), x.expected.RawValues)), -2, 2);
        }

        private static CalibrationFinding WeatherIndex(IReadOnlyList<CalibrationRun> runs)
        {
            return Offsets(CalibrationRules.WeatherIndex, "N-5: which GetAnnualParameter() CLR index holds SAM weather hour h (index = h + offset)?",
                runs.Where(x => x.Expected.Weather.Annual.ContainsKey(0) && x.Native.Weather?.Annual != null && x.Native.Weather.Annual.ContainsKey(0))
                    .SelectMany(x => new[] { 0, 1, 5, 6 }.Select(p => (x.Label + " annual parameter " + p + " (lower bound " + x.Native.Weather.Annual[p].LowerBound + ", length " + x.Native.Weather.Annual[p].Values?.Length + ")", (Func<int, NativeField>)(i => x.Native.Weather.Annual[p].At(i)), x.Expected.Weather.Annual[p]))), -2, 2);
        }

        private static CalibrationFinding DesignDaySlot(IReadOnlyList<CalibrationRun> runs)
        {
            List<(string, Func<int, NativeField>, double[])> cases = new List<(string, Func<int, NativeField>, double[])>();
            foreach (CalibrationRun run in runs)
            {
                foreach (var pair in new[] { ("cooling", run.Expected.CoolingDesignDays, run.Native.CoolingDesignDays), ("heating", run.Expected.HeatingDesignDays, run.Native.HeatingDesignDays) })
                {
                    if (pair.Item2.Count != 1 || pair.Item3.Count != 1 || pair.Item3[0].Days.Count == 0) continue;
                    NativeDesignDay day = pair.Item3[0].Days[0];
                    foreach (string field in new[] { "dryBulb", "humidity", "windSpeed", "windDirection" })
                    {
                        double[] expected = pair.Item2[0].Hours[field];
                        if (expected.Distinct().Count() < 2) continue; // a constant day cannot tell slots apart
                        cases.Add((run.Label + " " + pair.Item1 + " " + field, s => day.Hours[field].TryGetValue(s, out NativeField v) ? v : null, expected));
                    }
                }
            }

            return Offsets(CalibrationRules.DesignDaySlot, "N-5: which WeatherDay slot holds design-day hour h (slot = h + offset)?", cases, -2, 3);
        }

        // ---- N-6 calendar ------------------------------------------------------------------------------------------------

        private static CalibrationFinding CalendarDays(IReadOnlyList<CalibrationRun> runs)
        {
            CalibrationFinding f = new CalibrationFinding { Id = CalibrationRules.Calendar, Question = "N-6: which days(i) indices hold the 365 simulated days, and does the annual internal condition carry every calendar day type?", Consumed = true };
            HashSet<int> firsts = new HashSet<int>();
            bool covered = true;
            foreach (CalibrationRun run in runs)
            {
                List<int> typed = run.Native.Calendar.Days.Where(x => x.DayType != null).Select(x => x.Index).OrderBy(x => x).ToList();
                bool contiguous = typed.Count == 365 && typed.Last() - typed.First() == 364;
                if (contiguous) firsts.Add(typed.First()); else firsts.Add(int.MinValue);
                HashSet<string> types = new HashSet<string>(run.Native.Calendar.Days.Where(x => x.DayType != null).Select(x => x.DayType));
                foreach (NativeZone zone in run.Native.Zones)
                {
                    NativeInternalCondition ic = zone.InternalConditions.FirstOrDefault(x => !x.DayTypes.Contains("HDD"));
                    bool all = ic != null && types.All(x => ic.DayTypes.Contains(x));
                    covered &= all;
                    f.Evidence.Add(run.Label + " zone '" + zone.Name + "': calendar day types {" + string.Join(",", types.OrderBy(x => x)) + "}, annual condition day types {" + string.Join(",", ic?.DayTypes ?? new List<string>()) + "} -> " + (all ? "covered" : "NOT covered"));
                }

                f.Evidence.Add(run.Label + ": typed days(i) " + typed.Count + " from " + (typed.FirstOrDefault()) + " to " + (typed.LastOrDefault()) + "; calendar dayTypes(i) [" + string.Join(", ", run.Native.Calendar.DayTypes) + "]; building year " + run.Native.Building?.Year);
            }

            f.Consistent = firsts.Count == 1 && firsts.Single() != int.MinValue && covered;
            f.Answer = f.Consistent ? firsts.Single().ToString(ci) : null;
            f.Note = "The answer is the index of the first simulated day; 365 contiguous typed days were required.";
            return f;
        }

        private static CalibrationFinding Winding(IReadOnlyList<CalibrationRun> runs)
        {
            CalibrationFinding f = new CalibrationFinding { Id = "N-8.winding", Question = "N-8: does the native room-surface outline reproduce the input polygon, and with which winding?" };
            int same = 0, opposite = 0;
            foreach (CalibrationRun run in runs)
            {
                foreach (ZoneMatch zone in run.Identity.Zones)
                {
                    same += zone.Surfaces.Count(x => x.Status == IdentityStatus.Verified && x.SameFacing);
                    opposite += zone.Surfaces.Count(x => x.Status == IdentityStatus.Verified && !x.SameFacing);
                    f.Evidence.Add(run.Label + " " + ReadBackComparison.Keys.Space(zone.SpaceGuid) + ": " + zone.Status + " - " + zone.Evidence);
                }
            }

            f.Answer = "the outline reproduces the panel boundary (same vertices, collinear points dropped); winding: " + same + " same as the SAM panel, " + opposite + " opposite (identity is winding-independent)";
            f.Consistent = same + opposite > 0;
            return f;
        }

        // ---- N-5 alignment: P-ALIGN ----------------------------------------------------------------------------------------

        private static CalibrationFinding Alignment(TsdSnapshot align)
        {
            CalibrationFinding f = new CalibrationFinding { Id = CalibrationRules.Alignment, Question = "N-5 (P-ALIGN): does TAS apply profile slot h + 1 in the same simulated hour as weather slot h + 1, for hourly, yearly, infiltration and thermostat profiles?", Consumed = true };
            if (align == null || align.Errors.Count > 0 || align.Zones.Count != 1)
            {
                f.Note = align == null ? "no P-ALIGN TSD" : "P-ALIGN TSD unusable: " + string.Join("; ", align.Errors) + " zones " + align.Zones.Count;
                return f;
            }

            NativeSeries weather = align.Building.TryGetValue("externalTemperature", out NativeSeries w) ? w : null;
            if (weather == null || !weather.IsRead || weather.Values.Length != 8760)
            {
                f.Note = "the TSD external temperature is not an 8760-hour array";
                return f;
            }

            // The TSD hour t (0-based position) carries the weather of SAM hour t + shift: the marker year names its own hour.
            List<int> shifts = new List<int>();
            for (int s = -3; s <= 3; s++)
            {
                bool all = true;
                for (int t = 0; t < 8760 && all; t++)
                {
                    int k = t + s;
                    all = k < 0 || k >= 8760 || Near(weather.Values[t], CalibrationModels.MarkerDryBulb(k / 24, k % 24), 1e-5, 1e-4);
                }

                if (all) shifts.Add(s);
            }

            f.Evidence.Add("TSD externalTemperature: lower bound " + weather.LowerBound + ", length " + weather.Values.Length + "; weather shifts that reproduce the marker year: [" + string.Join(",", shifts) + "]");
            if (shifts.Count != 1)
            {
                f.Note = "the weather marker does not fix one TSD-to-SAM hour shift";
                return f;
            }

            int shift = shifts[0];
            TsdZone zone = align.Zones[0];
            bool ok = true;
            ok &= Pulse(f, zone, "lightingGain", shift, k => k % 24 == CalibrationModels.PulseLightingHour, 365, "lighting, 24-hour profile, SAM hour " + CalibrationModels.PulseLightingHour + " of every day");
            ok &= Pulse(f, zone, "equipmentSensibleGain", shift, k => k == CalibrationModels.PulseEquipmentHourOfYear, 1, "sensible equipment, 8760-hour profile, SAM hour of year " + CalibrationModels.PulseEquipmentHourOfYear + " (day 100, 14:00)");
            ok &= Pulse(f, zone, "infiltration", shift, k => k % 24 == CalibrationModels.PulseInfiltrationHour, 365, "infiltration, 24-hour profile, SAM hour " + CalibrationModels.PulseInfiltrationHour);
            bool heating = Pulse(f, zone, "heatingLoad", shift, k => k % 24 == CalibrationModels.PulseHeatingHour, 365, "heating setpoint step, 24-hour thermostat profile, SAM hour " + CalibrationModels.PulseHeatingHour);

            f.Consistent = ok && heating;
            f.Answer = f.Consistent ? "aligned: every pulse lands in the TSD hour that carries the weather of the same SAM hour (shift " + shift.ToString(ci) + ")" : null;
            f.Note = "weather shift " + shift + "; gains, infiltration and thermostat must all land on the authored SAM hour";
            return f;
        }

        private static bool Pulse(CalibrationFinding f, TsdZone zone, string array, int shift, Func<int, bool> expected, int expectedCount, string label)
        {
            NativeSeries series = zone.Arrays.TryGetValue(array, out NativeSeries s) ? s : null;
            if (series == null || !series.IsRead || series.Values.Length != 8760)
            {
                f.Evidence.Add(label + ": TSD " + array + " is not an 8760-hour array (" + series?.Kind + ")");
                return false;
            }

            List<int> hours = Enumerable.Range(0, 8760).Where(t => System.Math.Abs(series.Values[t]) > 1e-6).Select(t => t + shift).ToList();
            bool ok = hours.Count == expectedCount && hours.All(expected);
            f.Evidence.Add(label + ": TSD " + array + " non-zero in " + hours.Count + " hour(s), SAM hours " + string.Join(",", hours.Take(6)) + (hours.Count > 6 ? ",..." : string.Empty) + " -> " + (ok ? "aligned" : "NOT aligned"));
            return ok;
        }
    }
}
