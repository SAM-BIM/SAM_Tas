// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>One native convention, decided from the synthetic calibration and committed BEFORE any fixture is compared.</summary>
    public sealed class CalibrationRule
    {
        public string Question { get; set; }

        /// <summary>The measured answer. Only meaningful when <see cref="Status"/> is <c>verified</c>.</summary>
        public string Answer { get; set; }

        /// <summary><c>verified</c> (measured on calibration evidence), <c>unresolved</c> or <c>provisional</c>. Only verified counts.</summary>
        public string Status { get; set; }
        public List<string> Evidence { get; set; } = new List<string>();
        public string Note { get; set; }
    }

    /// <summary>
    /// The committed calibration rule table (<c>calibration-rules.json</c>). A rule that is missing, unresolved or provisional
    /// never silently becomes the writer's convention: every quantity that depends on it stays unverified (NOT RUN at best).
    /// A fixture result that would need a rule change is reported as FAIL/BLOCKED, never fixed by editing a rule.
    /// </summary>
    public sealed class CalibrationRules
    {
        public const string CurrentSchema = "s08-calibration-rules/1";

        // The rule identifiers the comparison consumes.
        public const string HostSurfaceArea = "N-1.hostSurfaceArea";
        public const string LayerOrientation = "N-2.layerOrientation";
        public const string FrameSurfaces = "N-3.1.frameSurfaces";
        public const string ZeroFrame = "N-3.3.zeroPercentFrame";
        public const string HourlySlot = "N-5.hourlySlot";
        public const string YearlyIndex = "N-5.yearlyIndex";
        public const string WeatherIndex = "N-5.weatherIndex";
        public const string DesignDaySlot = "N-5.designDaySlot";
        public const string Alignment = "N-5.alignment";
        public const string Calendar = "N-6.calendar";

        public static IReadOnlyList<string> Consumed { get; } = new[] { HostSurfaceArea, LayerOrientation, FrameSurfaces, ZeroFrame, HourlySlot, YearlyIndex, WeatherIndex, DesignDaySlot, Alignment, Calendar };

        public string Schema { get; set; }
        public string Note { get; set; }
        public Dictionary<string, CalibrationRule> Rules { get; set; } = new Dictionary<string, CalibrationRule>(StringComparer.Ordinal);

        /// <summary>Where the table was loaded from and its sha256, recorded in every report.</summary>
        public string SourcePath { get; set; }
        public string SourceSha256 { get; set; }

        public static CalibrationRules Load(string path)
        {
            CalibrationRules result = JsonSerializer.Deserialize<CalibrationRules>(File.ReadAllText(path), SnapshotJson.Options);
            if (result == null || result.Schema != CurrentSchema)
            {
                throw new InvalidDataException("not a " + CurrentSchema + " table: " + path);
            }

            result.Rules = result.Rules ?? new Dictionary<string, CalibrationRule>(StringComparer.Ordinal);
            result.SourcePath = path;
            result.SourceSha256 = SnapshotJson.Sha256(path);
            return result;
        }

        /// <summary>An empty table: every rule unresolved.</summary>
        public static CalibrationRules None()
        {
            return new CalibrationRules { Schema = CurrentSchema, Note = "no rules: every native convention is unresolved" };
        }

        public bool IsVerified(string id)
        {
            return Rules.TryGetValue(id, out CalibrationRule rule) && rule != null && string.Equals(rule.Status, "verified", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(rule.Answer);
        }

        /// <summary>The verified answer, or null.</summary>
        public string Answer(string id)
        {
            return IsVerified(id) ? Rules[id].Answer : null;
        }

        /// <summary>A verified integer answer (a slot or index offset), or <paramref name="fallback"/> when the rule is not verified.</summary>
        public int Offset(string id, int fallback, out bool verified)
        {
            verified = IsVerified(id) && int.TryParse(Rules[id].Answer, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            return verified ? int.Parse(Rules[id].Answer, CultureInfo.InvariantCulture) : fallback;
        }
    }
}
