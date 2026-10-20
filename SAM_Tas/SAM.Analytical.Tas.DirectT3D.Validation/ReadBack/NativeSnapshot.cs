// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.InputEquivalence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests. It touches no TAS type.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>
    /// The five reader outcomes of S07 section 6.1 (reader precondition), in a serialisable form. They mirror
    /// <see cref="ReaderKind"/> one to one and never stand in for each other: a field that threw is
    /// <see cref="Unreadable"/>, never NaN or zero; an object TAS reports as missing is <see cref="Absent"/>.
    /// </summary>
    public enum FieldKind
    {
        Absent,
        Unreadable,
        NaN,
        Zero,
        Finite
    }

    /// <summary>One raw native number exactly as the reader found it. Text fields use <see cref="Text"/>.</summary>
    public sealed class NativeField
    {
        public FieldKind Kind { get; set; }
        public double Value { get; set; } = double.NaN;
        public string Text { get; set; }
        public string Detail { get; set; }

        public static NativeField Of(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return new NativeField { Kind = FieldKind.NaN, Detail = double.IsInfinity(value) ? "infinite" : null };
            }

            return new NativeField { Kind = value == 0 ? FieldKind.Zero : FieldKind.Finite, Value = value };
        }

        public static NativeField OfText(string text)
        {
            return text == null ? Absent("null text") : new NativeField { Kind = FieldKind.Finite, Text = text };
        }

        public static NativeField Absent(string detail = null)
        {
            return new NativeField { Kind = FieldKind.Absent, Detail = detail };
        }

        public static NativeField Unreadable(string detail)
        {
            return new NativeField { Kind = FieldKind.Unreadable, Detail = detail };
        }

        [JsonIgnore]
        public bool HasNumber => Kind == FieldKind.Zero || Kind == FieldKind.Finite;

        public ReaderResult ToReaderResult()
        {
            switch (Kind)
            {
                case FieldKind.Absent:
                    return ReaderResult.Absent(Detail);

                case FieldKind.Unreadable:
                    return ReaderResult.Unreadable(Detail ?? "unreadable");

                case FieldKind.NaN:
                    return ReaderResult.NotANumber(Detail);

                default:
                    return ReaderResult.FromValue(Value);
            }
        }

        public override string ToString()
        {
            return Text != null ? Kind + "('" + Text + "')" : HasNumber ? Kind + "(" + Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ")" : Kind + (Detail == null ? string.Empty : " [" + Detail + "]");
        }
    }

    /// <summary>
    /// A raw native array exactly as TAS returned it: the CLR lower bound, the length and every element, so slot
    /// conventions are decided from evidence (calibration rules), never assumed here.
    /// </summary>
    public sealed class NativeSeries
    {
        /// <summary>Absent or Unreadable when there is no array; Finite when the array was read (its elements may still be NaN).</summary>
        public FieldKind Kind { get; set; }
        public int LowerBound { get; set; }
        public double[] Values { get; set; }
        public string Detail { get; set; }

        [JsonIgnore]
        public bool IsRead => Kind == FieldKind.Finite && Values != null;

        public static NativeSeries Absent(string detail = null)
        {
            return new NativeSeries { Kind = FieldKind.Absent, Detail = detail };
        }

        public static NativeSeries Unreadable(string detail)
        {
            return new NativeSeries { Kind = FieldKind.Unreadable, Detail = detail };
        }

        public static NativeSeries Of(double[] values, int lowerBound)
        {
            return new NativeSeries { Kind = FieldKind.Finite, Values = values, LowerBound = lowerBound };
        }

        /// <summary>The element whose CLR index is <paramref name="clrIndex"/> as a reader result; out of range is Absent.</summary>
        public NativeField At(int clrIndex)
        {
            if (Kind == FieldKind.Unreadable)
            {
                return NativeField.Unreadable(Detail);
            }

            if (!IsRead)
            {
                return NativeField.Absent(Detail ?? "no array");
            }

            int i = clrIndex - LowerBound;
            if (i < 0 || i >= Values.Length)
            {
                return NativeField.Absent("index " + clrIndex + " outside the native array (lower bound " + LowerBound + ", length " + Values.Length + ")");
            }

            return NativeField.Of(Values[i]);
        }
    }

    public sealed class NativeProfile
    {
        /// <summary>The TBD profile slot (TBD.Profiles name, e.g. ticLG) the profile was read from.</summary>
        public string Slot { get; set; }
        public FieldKind Kind { get; set; }
        public string Detail { get; set; }
        public string Type { get; set; }
        public int TypeCode { get; set; }
        public NativeField Factor { get; set; }
        public NativeField Value { get; set; }
        public NativeField SetbackValue { get; set; }
        public string Function { get; set; }
        public string Name { get; set; }

        /// <summary>hourlyValues read at the slots 0..25 (an unreadable slot is recorded as such), so the slot convention is evidence.</summary>
        public Dictionary<int, NativeField> Hourly { get; set; }

        /// <summary>GetYearlyValues() exactly as returned. Read for every profile type; its values are only meaningful for a yearly type.</summary>
        public NativeSeries Yearly { get; set; }
    }

    public sealed class NativeInternalCondition
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> DayTypes { get; set; } = new List<string>();
        public string Detail { get; set; }

        public NativeField LightingRadProp { get; set; }
        public NativeField OccupantRadProp { get; set; }
        public NativeField EquipmentRadProp { get; set; }
        public NativeField PersonGain { get; set; }
        public NativeField FreshAirRate { get; set; }
        public Dictionary<string, NativeProfile> GainProfiles { get; set; } = new Dictionary<string, NativeProfile>();
        public Dictionary<string, NativeProfile> ThermostatProfiles { get; set; } = new Dictionary<string, NativeProfile>();
    }

    public sealed class NativeRoomSurface
    {
        public NativeField Area { get; set; }

        /// <summary>The perimeter face as read (x, y, z per point), or null when the perimeter could not be read.</summary>
        public List<double[]> Outer { get; set; }
        public List<List<double[]>> Holes { get; set; } = new List<List<double[]>>();
        public string Detail { get; set; }
    }

    public sealed class NativeSurface
    {
        public int Index { get; set; }
        public NativeField Number { get; set; }
        public string Type { get; set; }
        public NativeField Area { get; set; }
        public NativeField InternalArea { get; set; }
        public NativeField Orientation { get; set; }
        public NativeField Inclination { get; set; }
        public NativeField Reversed { get; set; }

        /// <summary>The native GUID of the building element; a link inside the TBD, never an identity to a SAM object.</summary>
        public string BuildingElementGuid { get; set; }
        public string BuildingElementName { get; set; }
        public int BEType { get; set; } = -1;
        public string LinkZoneGuid { get; set; }
        public string LinkSurfaceGuid { get; set; }
        public string Guid { get; set; }
        public List<NativeRoomSurface> RoomSurfaces { get; set; } = new List<NativeRoomSurface>();
    }

    public sealed class NativeZone
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Guid { get; set; }
        public NativeField FloorArea { get; set; }
        public NativeField Volume { get; set; }
        public NativeField External { get; set; }

        /// <summary>Plant sizing state TAS keeps on the zone; not S07 inputs, read for the Sizing=true diff and P-ALIGN.</summary>
        public NativeField SizeHeating { get; set; }
        public NativeField SizeCooling { get; set; }
        public NativeField MaxHeatingLoad { get; set; }
        public NativeField MaxCoolingLoad { get; set; }
        public List<NativeInternalCondition> InternalConditions { get; set; } = new List<NativeInternalCondition>();
        public List<NativeSurface> Surfaces { get; set; } = new List<NativeSurface>();
    }

    public sealed class NativeLayer
    {
        /// <summary>TAS's 1-based materials(j) index.</summary>
        public int Index { get; set; }
        public string Name { get; set; }
        public NativeField Type { get; set; }

        /// <summary>The construction's materialWidth[j]: the layer thickness in THIS construction.</summary>
        public NativeField MaterialWidth { get; set; }

        /// <summary>material.width: the material's own default width.</summary>
        public NativeField Width { get; set; }
        public NativeField Conductivity { get; set; }
        public NativeField Density { get; set; }
        public NativeField SpecificHeat { get; set; }
        public NativeField ExternalSolarReflectance { get; set; }
        public NativeField InternalSolarReflectance { get; set; }
        public NativeField ExternalLightReflectance { get; set; }
        public NativeField InternalLightReflectance { get; set; }
        public NativeField ExternalEmissivity { get; set; }
        public NativeField InternalEmissivity { get; set; }
        public NativeField SolarTransmittance { get; set; }
        public NativeField LightTransmittance { get; set; }
        public NativeField ConvectionCoefficient { get; set; }
        public NativeField DynamicViscosity { get; set; }
    }

    public sealed class NativeBuildingElement
    {
        public int Index { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public int BEType { get; set; }
        public NativeField Width { get; set; }
        public NativeField Ground { get; set; }
        public NativeField Ghost { get; set; }
        public string ConstructionName { get; set; }
        public string ConstructionDetail { get; set; }

        /// <summary>The construction's layers in TAS's materials(1..n) order, read through this element's GetConstruction().</summary>
        public List<NativeLayer> Layers { get; set; } = new List<NativeLayer>();

        /// <summary>TAS-computed construction-level surface properties, kept as evidence for the layer-orientation calibration.</summary>
        public Dictionary<string, NativeField> ConstructionProperties { get; set; } = new Dictionary<string, NativeField>();
        public List<string> ApertureTypeNames { get; set; } = new List<string>();
    }

    public sealed class NativeApertureType
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public NativeField DischargeCoefficient { get; set; }
        public NativeField Sheltered { get; set; }
        public NativeProfile Profile { get; set; }
        public List<string> DayTypes { get; set; } = new List<string>();
    }

    public sealed class NativeDesignDay
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public NativeField YearDay { get; set; }
        public NativeField Repetitions { get; set; }
        public string DayType { get; set; }

        /// <summary>The 24-hour arrays by WeatherDay field name, read at the slots 0..25.</summary>
        public Dictionary<string, Dictionary<int, NativeField>> Hours { get; set; } = new Dictionary<string, Dictionary<int, NativeField>>();
    }

    public sealed class NativeDesignDayGroup
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public List<NativeDesignDay> Days { get; set; } = new List<NativeDesignDay>();
    }

    public sealed class NativeWeather
    {
        public FieldKind Kind { get; set; }
        public string Detail { get; set; }
        public string Name { get; set; }
        public NativeField Latitude { get; set; }
        public NativeField Longitude { get; set; }
        public NativeField Altitude { get; set; }
        public NativeField TimeZone { get; set; }
        public NativeField Year { get; set; }
        public NativeField GroundTemperature { get; set; }

        /// <summary>GetAnnualParameter(i) for i = 0..6, exactly as returned.</summary>
        public Dictionary<int, NativeSeries> Annual { get; set; } = new Dictionary<int, NativeSeries>();
    }

    public sealed class NativeCalendarDay
    {
        public int Index { get; set; }
        public string DayType { get; set; }
        public string Detail { get; set; }
    }

    public sealed class NativeCalendar
    {
        public List<string> DayTypes { get; set; } = new List<string>();

        /// <summary>days(i) for i = 0..367 (null-terminated reads are recorded too), so the index base is evidence.</summary>
        public List<NativeCalendarDay> Days { get; set; } = new List<NativeCalendarDay>();
        public NativeField StartDay { get; set; }
        public string Detail { get; set; }
    }

    public sealed class NativeBuilding
    {
        public string Name { get; set; }
        public NativeField Latitude { get; set; }
        public NativeField Longitude { get; set; }
        public NativeField TimeZone { get; set; }
        public NativeField Year { get; set; }
        public NativeField NorthAngle { get; set; }
    }

    public sealed class NativeT3DWindow
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public NativeField FramePerc { get; set; }
        public NativeField IsPercFrame { get; set; }
        public NativeField FrameWidth { get; set; }
        public NativeField FrameDepth { get; set; }
        public NativeField Width { get; set; }
        public NativeField Height { get; set; }
        public NativeField Transparent { get; set; }
        public NativeField IsUsed { get; set; }
    }

    public sealed class NativeT3DElement
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public NativeField BEType { get; set; }
        public NativeField Width { get; set; }
        public NativeField ZoneFloorArea { get; set; }
        public NativeField Ghost { get; set; }
        public NativeField Ground { get; set; }
    }

    public sealed class NativeT3DZone
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public NativeField FloorArea { get; set; }
        public NativeField Volume { get; set; }
        public NativeField IsUsed { get; set; }
    }

    public sealed class NativeT3D
    {
        public FieldKind Kind { get; set; }
        public string Detail { get; set; }
        public List<NativeT3DWindow> Windows { get; set; } = new List<NativeT3DWindow>();
        public List<NativeT3DElement> Elements { get; set; } = new List<NativeT3DElement>();
        public List<NativeT3DZone> Zones { get; set; } = new List<NativeT3DZone>();
    }

    /// <summary>Who read what, when, and proof that the read left the persisted files untouched.</summary>
    public sealed class ReaderInfo
    {
        public int ProcessId { get; set; }
        public string StartedUtc { get; set; }
        public string Machine { get; set; }
        public string ReaderAssembly { get; set; }
        public string ReaderAssemblySha256 { get; set; }
        public string TbdPath { get; set; }
        public string TbdSha256Before { get; set; }
        public string TbdSha256After { get; set; }
        public int TbdOpenResult { get; set; }
        public string T3dPath { get; set; }
        public string T3dSha256Before { get; set; }
        public string T3dSha256After { get; set; }
    }

    /// <summary>
    /// Everything the independent read-back took from the persisted TBD (and T3D), as raw fields with their reader
    /// state. Built only by <c>TbdReadBack</c> in a fresh process; consumed COM-free by the comparison.
    /// </summary>
    public sealed class NativeSnapshot
    {
        public const string CurrentSchema = "s08-readback-snapshot/1";

        public string Schema { get; set; } = CurrentSchema;
        public ReaderInfo Reader { get; set; } = new ReaderInfo();

        /// <summary>Fatal reader problems (open, COM, licence). Any entry makes the run an infrastructure failure.</summary>
        public List<string> Errors { get; set; } = new List<string>();
        public NativeBuilding Building { get; set; }
        public NativeCalendar Calendar { get; set; }
        public NativeWeather Weather { get; set; }
        public List<NativeDesignDayGroup> CoolingDesignDays { get; set; } = new List<NativeDesignDayGroup>();
        public List<NativeDesignDayGroup> HeatingDesignDays { get; set; } = new List<NativeDesignDayGroup>();
        public List<NativeZone> Zones { get; set; } = new List<NativeZone>();
        public List<NativeBuildingElement> BuildingElements { get; set; } = new List<NativeBuildingElement>();
        public List<NativeApertureType> ApertureTypes { get; set; } = new List<NativeApertureType>();
        public List<string> InternalConditionNames { get; set; } = new List<string>();
        public List<string> IzamNames { get; set; } = new List<string>();
        public int ZoneGroupCount { get; set; }
        public NativeT3D T3D { get; set; }
    }

    public sealed class TsdZone
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Guid { get; set; }
        public Dictionary<string, NativeSeries> Arrays { get; set; } = new Dictionary<string, NativeSeries>();
    }

    /// <summary>The P-ALIGN result arrays of one TSD, read in a fresh process (<c>TsdReadBack</c>).</summary>
    public sealed class TsdSnapshot
    {
        public string Path { get; set; }
        public long Bytes { get; set; }
        public int ProcessId { get; set; }
        public string StartedUtc { get; set; }
        public string Sha256Before { get; set; }
        public string Sha256After { get; set; }
        public int FirstDay { get; set; }
        public int LastDay { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public Dictionary<string, NativeSeries> Building { get; set; } = new Dictionary<string, NativeSeries>();
        public List<TsdZone> Zones { get; set; } = new List<TsdZone>();
    }

    public static class SnapshotJson
    {
        public static JsonSerializerOptions Options { get; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new JsonStringEnumConverter() }
        };

        public static void Write<T>(T value, string path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
        }

        public static T Read<T>(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("missing " + path, path);
            }

            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }

        public static string Sha256(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
