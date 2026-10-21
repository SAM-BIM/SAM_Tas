// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using TBD;

namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>
    /// The S08 independent read-back of a persisted TBD (and its T3D): raw COM fields through TAS's own 0-based
    /// <c>Get*(index)</c> accessors, each with its reader state. It deliberately uses none of the code under test -
    /// no <c>SAMTBDDocument</c> (it creates a missing file and ignores the open result), no <c>ToSAM</c> converter, no
    /// SAM_Tas query or writer cache - and it never saves. Run it in a fresh process (<c>readback-read</c>), after
    /// the process that wrote the files has exited.
    /// </summary>
    public static class TbdReadBack
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        // The TBD profile slots S07 reads, by TBD.Profiles name (values from the interop enum).
        private static readonly Profiles[] gainSlots = { Profiles.ticOSG, Profiles.ticOLG, Profiles.ticLG, Profiles.ticESG, Profiles.ticELG, Profiles.ticI, Profiles.ticV, Profiles.ticCOG };
        private static readonly Profiles[] thermostatSlots = { Profiles.ticLL, Profiles.ticUL, Profiles.ticHLL, Profiles.ticHUL };

        public static NativeSnapshot Read(string path_TBD, string path_T3D)
        {
            NativeSnapshot snapshot = new NativeSnapshot();
            ReaderInfo info = snapshot.Reader;
            info.ProcessId = Process.GetCurrentProcess().Id;
            info.StartedUtc = DateTime.UtcNow.ToString("o", ci);
            info.Machine = Environment.MachineName;
            info.ReaderAssembly = typeof(TbdReadBack).Assembly.Location;
            info.ReaderAssemblySha256 = SnapshotJson.Sha256(info.ReaderAssembly);
            info.TbdPath = path_TBD;
            info.T3dPath = path_T3D;

            // Existence first: a TBD document can create a file at a missing path, and that must never pass as a read.
            if (string.IsNullOrWhiteSpace(path_TBD) || !File.Exists(path_TBD))
            {
                snapshot.Errors.Add("TBD missing: " + path_TBD);
                return snapshot;
            }

            info.TbdSha256Before = SnapshotJson.Sha256(path_TBD);

            TBDDocument document = null;
            try
            {
                document = new TBDDocument();
                info.TbdOpenResult = document.openReadOnly(path_TBD);
                if (info.TbdOpenResult == 0)
                {
                    snapshot.Errors.Add("TBD openReadOnly returned 0 for " + path_TBD);
                    return snapshot;
                }

                Building building = document.Building;
                if (building == null)
                {
                    snapshot.Errors.Add("TBD has no Building");
                    return snapshot;
                }

                snapshot.Building = ReadBuilding(building);
                snapshot.Calendar = ReadCalendar(building);
                snapshot.Weather = ReadWeather(building);
                ReadDesignDays(building, snapshot);
                ReadBuildingElements(building, snapshot);
                ReadApertureTypes(building, snapshot);
                ReadZones(building, snapshot);

                for (int i = 0; ; i++)
                {
                    TBD.InternalCondition ic = Try(() => building.GetIC(i));
                    if (ic == null) break;
                    snapshot.InternalConditionNames.Add(Try(() => ic.name));
                }

                for (int i = 0; ; i++)
                {
                    IZAM izam = Try(() => building.GetIZAM(i));
                    if (izam == null) break;
                    snapshot.IzamNames.Add(Try(() => izam.name) ?? "(unnamed)");
                }

                for (int i = 0; ; i++)
                {
                    ZoneGroup zoneGroup = Try(() => building.GetZoneGroup(i));
                    if (zoneGroup == null) break;
                    snapshot.ZoneGroupCount++;
                }
            }
            catch (Exception exception)
            {
                snapshot.Errors.Add("TBD read failed: " + (exception.InnerException ?? exception).GetType().Name + ": " + (exception.InnerException ?? exception).Message);
            }
            finally
            {
                if (document != null)
                {
                    try { document.close(); } catch { }
                    Marshal.FinalReleaseComObject(document);
                }
            }

            info.TbdSha256After = SnapshotJson.Sha256(path_TBD);
            if (info.TbdSha256Before != info.TbdSha256After)
            {
                snapshot.Errors.Add("the TBD changed while it was read (sha256 before " + info.TbdSha256Before + ", after " + info.TbdSha256After + ")");
            }

            snapshot.T3D = ReadT3D(path_T3D, info);
            if (info.T3dSha256Before != null && info.T3dSha256Before != info.T3dSha256After)
            {
                // Infrastructure, as for the TBD: its T3D field checks are informational, a changed file is not.
                snapshot.Errors.Add("the T3D changed while it was read (sha256 before " + info.T3dSha256Before + ", after " + info.T3dSha256After + ")");
            }

            return snapshot;
        }

        // ---- building, calendar, weather, design days ------------------------------------------------------------------

        private static NativeBuilding ReadBuilding(Building b)
        {
            return new NativeBuilding
            {
                Name = Try(() => b.name),
                Latitude = Field(() => b.latitude),
                Longitude = Field(() => b.longitude),
                TimeZone = Field(() => b.timeZone),
                Year = Field(() => b.year),
                NorthAngle = Field(() => b.northAngle)
            };
        }

        private static NativeCalendar ReadCalendar(Building b)
        {
            NativeCalendar result = new NativeCalendar();
            TBD.Calendar calendar = Try(() => b.GetCalendar());
            if (calendar == null)
            {
                result.Detail = "GetCalendar returned null";
                return result;
            }

            result.StartDay = Field(() => calendar.startDay);

            // dayTypes(0) returned null on the first probe, so the index base is not assumed: scan a fixed range, keep the index.
            for (int i = 0; i <= 32; i++)
            {
                dayType dayType = Try(() => calendar.dayTypes(i));
                if (dayType == null) continue;
                result.DayTypes.Add(i.ToString(ci) + ":" + Try(() => dayType.name));
            }

            // The index base of days(i) is a native question (N-6): record 0..367 as read, nulls and failures included.
            for (int i = 0; i <= 367; i++)
            {
                NativeCalendarDay day = new NativeCalendarDay { Index = i };
                try
                {
                    TBD.day d = calendar.days(i);
                    if (d == null)
                    {
                        day.Detail = "null";
                    }
                    else
                    {
                        day.DayType = d.dayType?.name;
                    }
                }
                catch (Exception exception)
                {
                    day.Detail = "unreadable: " + (exception.InnerException ?? exception).GetType().Name;
                }

                result.Days.Add(day);
            }

            return result;
        }

        private static NativeWeather ReadWeather(Building b)
        {
            WeatherYear w;
            try
            {
                w = b.GetWeatherYear();
            }
            catch (Exception exception)
            {
                return new NativeWeather { Kind = FieldKind.Unreadable, Detail = exception.GetType().Name };
            }

            if (w == null)
            {
                return new NativeWeather { Kind = FieldKind.Absent, Detail = "GetWeatherYear returned null" };
            }

            NativeWeather result = new NativeWeather
            {
                Kind = FieldKind.Finite,
                Name = Try(() => w.name),
                Latitude = Field(() => w.latitude),
                Longitude = Field(() => w.longitude),
                Altitude = Field(() => w.altitude),
                TimeZone = Field(() => w.timeZone),
                Year = Field(() => w.year),
                GroundTemperature = Field(() => w.groundTemperature)
            };

            for (int p = 0; p <= 6; p++)
            {
                result.Annual[p] = Series(() => w.GetAnnualParameter(p));
            }

            return result;
        }

        private static void ReadDesignDays(Building b, NativeSnapshot snapshot)
        {
            for (int i = 0; ; i++)
            {
                CoolingDesignDay c = Try(() => b.GetCoolingDesignDay(i));
                if (c == null) break;
                NativeDesignDayGroup group = new NativeDesignDayGroup { Index = i, Name = Try(() => c.name) };
                for (int j = 0; j < 400; j++)
                {
                    TBD.DesignDay d = Try(() => c.GetDesignDay(j));
                    if (d == null) break;
                    group.Days.Add(ReadDesignDay(d, j));
                }

                snapshot.CoolingDesignDays.Add(group);
            }

            for (int i = 0; ; i++)
            {
                HeatingDesignDay h = Try(() => b.GetHeatingDesignDay(i));
                if (h == null) break;
                NativeDesignDayGroup group = new NativeDesignDayGroup { Index = i, Name = Try(() => h.name) };
                for (int j = 0; j < 400; j++)
                {
                    TBD.DesignDay d = Try(() => h.GetDesignDay(j));
                    if (d == null) break;
                    group.Days.Add(ReadDesignDay(d, j));
                }

                snapshot.HeatingDesignDays.Add(group);
            }
        }

        private static NativeDesignDay ReadDesignDay(TBD.DesignDay d, int index)
        {
            NativeDesignDay result = new NativeDesignDay
            {
                Index = index,
                Name = Try(() => d.name),
                YearDay = Field(() => d.yearDay),
                Repetitions = Field(() => d.repetitions),
                DayType = Try(() => d.GetDayType()?.name)
            };

            WeatherDay w = Try(() => d.GetWeatherDay());
            if (w == null)
            {
                return result;
            }

            result.Hours["dryBulb"] = Slots(i => w.dryBulb[i]);
            result.Hours["humidity"] = Slots(i => w.humidity[i]);
            result.Hours["globalRadiation"] = Slots(i => w.globalRadiation[i]);
            result.Hours["diffuseRadiation"] = Slots(i => w.diffuseRadiation[i]);
            result.Hours["cloudCover"] = Slots(i => w.cloudCover[i]);
            result.Hours["windSpeed"] = Slots(i => w.windSpeed[i]);
            result.Hours["windDirection"] = Slots(i => w.windDirection[i]);
            return result;
        }

        // ---- elements, constructions, aperture types -------------------------------------------------------------------

        private static void ReadBuildingElements(Building b, NativeSnapshot snapshot)
        {
            for (int i = 0; ; i++)
            {
                buildingElement be = Try(() => b.GetBuildingElement(i));
                if (be == null) break;

                NativeBuildingElement e = new NativeBuildingElement
                {
                    Index = i,
                    Guid = Try(() => be.GUID),
                    Name = Try(() => be.name),
                    BEType = Try(() => be.BEType, -1),
                    Width = Field(() => be.width),
                    Ground = Field(() => be.ground),
                    Ghost = Field(() => be.ghost)
                };

                TBD.Construction c = null;
                try
                {
                    c = be.GetConstruction();
                }
                catch (Exception exception)
                {
                    e.ConstructionDetail = "unreadable: " + exception.GetType().Name;
                }

                if (c == null)
                {
                    e.ConstructionDetail = e.ConstructionDetail ?? "absent";
                }
                else
                {
                    e.ConstructionName = Try(() => c.name);
                    e.ConstructionProperties["externalSolarAbsorptanceExtSurf"] = Field(() => c.externalSolarAbsorptanceExtSurf);
                    e.ConstructionProperties["externalSolarAbsorptanceIntSurf"] = Field(() => c.externalSolarAbsorptanceIntSurf);
                    e.ConstructionProperties["internalSolarAbsorptanceIntSurf"] = Field(() => c.internalSolarAbsorptanceIntSurf);
                    e.ConstructionProperties["internalSolarAbsorptanceExtSurf"] = Field(() => c.internalSolarAbsorptanceExtSurf);
                    e.ConstructionProperties["externalEmissivity"] = Field(() => c.externalEmissivity);
                    e.ConstructionProperties["internalEmissivity"] = Field(() => c.internalEmissivity);
                    e.ConstructionProperties["conductance"] = Field(() => c.conductance);
                    e.ConstructionProperties["type"] = Field(() => (int)c.type);

                    // TAS counts a construction's layers from 1.
                    for (int j = 1; j < 100; j++)
                    {
                        material m = Try(() => c.materials(j));
                        if (m == null) break;
                        e.Layers.Add(new NativeLayer
                        {
                            Index = j,
                            Name = Try(() => m.name),
                            Type = Field(() => m.type),
                            MaterialWidth = Field(() => c.materialWidth[j]),
                            Width = Field(() => m.width),
                            Conductivity = Field(() => m.conductivity),
                            Density = Field(() => m.density),
                            SpecificHeat = Field(() => m.specificHeat),
                            ExternalSolarReflectance = Field(() => m.externalSolarReflectance),
                            InternalSolarReflectance = Field(() => m.internalSolarReflectance),
                            ExternalLightReflectance = Field(() => m.externalLightReflectance),
                            InternalLightReflectance = Field(() => m.internalLightReflectance),
                            ExternalEmissivity = Field(() => m.externalEmissivity),
                            InternalEmissivity = Field(() => m.internalEmissivity),
                            SolarTransmittance = Field(() => m.solarTransmittance),
                            LightTransmittance = Field(() => m.lightTransmittance),
                            ConvectionCoefficient = Field(() => m.convectionCoefficient),
                            DynamicViscosity = Field(() => m.dynamicViscosity)
                        });
                    }
                }

                for (int j = 0; j < 64; j++)
                {
                    TBD.ApertureType at = Try(() => be.GetApertureType(j));
                    if (at == null) break;
                    e.ApertureTypeNames.Add(Try(() => at.name));
                }

                snapshot.BuildingElements.Add(e);
            }
        }

        private static void ReadApertureTypes(Building b, NativeSnapshot snapshot)
        {
            for (int i = 0; ; i++)
            {
                TBD.ApertureType at = Try(() => b.GetApertureType(i));
                if (at == null) break;
                NativeApertureType result = new NativeApertureType
                {
                    Index = i,
                    Name = Try(() => at.name),
                    DischargeCoefficient = Field(() => at.dischargeCoefficient),
                    Sheltered = Field(() => at.sheltered),
                    Profile = ReadProfile(() => at.GetProfile(), "ApertureType")
                };

                for (int j = 0; j < 64; j++)
                {
                    dayType dt = Try(() => at.GetDayType(j));
                    if (dt == null) break;
                    result.DayTypes.Add(Try(() => dt.name));
                }

                snapshot.ApertureTypes.Add(result);
            }
        }

        // ---- zones -----------------------------------------------------------------------------------------------------

        private static void ReadZones(Building b, NativeSnapshot snapshot)
        {
            for (int i = 0; ; i++)
            {
                zone z = Try(() => b.GetZone(i));
                if (z == null) break;

                NativeZone result = new NativeZone
                {
                    Index = i,
                    Name = Try(() => z.name),
                    Description = Try(() => z.description),
                    Guid = Try(() => z.GUID),
                    FloorArea = Field(() => z.floorArea),
                    Volume = Field(() => z.volume),
                    External = Field(() => z.external),
                    SizeHeating = Field(() => z.sizeHeating),
                    SizeCooling = Field(() => z.sizeCooling),
                    MaxHeatingLoad = Field(() => z.maxHeatingLoad),
                    MaxCoolingLoad = Field(() => z.maxCoolingLoad)
                };

                for (int k = 0; k < 64; k++)
                {
                    TBD.InternalCondition ic = Try(() => z.GetIC(k));
                    if (ic == null) break;
                    result.InternalConditions.Add(ReadInternalCondition(ic, k));
                }

                for (int j = 0; ; j++)
                {
                    zoneSurface zs = Try(() => z.GetSurface(j));
                    if (zs == null) break;
                    result.Surfaces.Add(ReadSurface(zs, j));
                }

                snapshot.Zones.Add(result);
            }
        }

        private static NativeInternalCondition ReadInternalCondition(TBD.InternalCondition ic, int index)
        {
            NativeInternalCondition result = new NativeInternalCondition
            {
                Index = index,
                Name = Try(() => ic.name),
                Description = Try(() => ic.description)
            };

            for (int d = 0; d < 64; d++)
            {
                dayType dt = Try(() => ic.GetDayType(d));
                if (dt == null) break;
                result.DayTypes.Add(Try(() => dt.name));
            }

            InternalGain gain = Try(() => ic.GetInternalGain());
            if (gain == null)
            {
                result.Detail = "GetInternalGain returned null";
            }
            else
            {
                result.LightingRadProp = Field(() => gain.lightingRadProp);
                result.OccupantRadProp = Field(() => gain.occupantRadProp);
                result.EquipmentRadProp = Field(() => gain.equipmentRadProp);
                result.PersonGain = Field(() => gain.personGain);
                result.FreshAirRate = Field(() => gain.freshAirRate);
                foreach (Profiles slot in gainSlots)
                {
                    result.GainProfiles[slot.ToString()] = ReadProfile(() => gain.GetProfile((int)slot), slot.ToString());
                }
            }

            Thermostat thermostat = Try(() => ic.GetThermostat());
            if (thermostat == null)
            {
                result.Detail = (result.Detail == null ? string.Empty : result.Detail + "; ") + "GetThermostat returned null";
            }
            else
            {
                foreach (Profiles slot in thermostatSlots)
                {
                    result.ThermostatProfiles[slot.ToString()] = ReadProfile(() => thermostat.GetProfile((int)slot), slot.ToString());
                }
            }

            return result;
        }

        private static NativeProfile ReadProfile(Func<profile> get, string slot)
        {
            profile p;
            try
            {
                p = get();
            }
            catch (Exception exception)
            {
                return new NativeProfile { Slot = slot, Kind = FieldKind.Unreadable, Detail = exception.GetType().Name };
            }

            if (p == null)
            {
                return new NativeProfile { Slot = slot, Kind = FieldKind.Absent, Detail = "GetProfile returned null" };
            }

            NativeProfile result = new NativeProfile
            {
                Slot = slot,
                Kind = FieldKind.Finite,
                Name = Try(() => p.name),
                Function = Try(() => p.function),
                Factor = Field(() => p.factor),
                Value = Field(() => p.value),
                SetbackValue = Field(() => p.setbackValue),
                Hourly = Slots(i => p.hourlyValues[i]),
                Yearly = Series(() => p.GetYearlyValues())
            };

            try
            {
                ProfileTypes type = p.type;
                result.Type = type.ToString();
                result.TypeCode = (int)type;
            }
            catch (Exception exception)
            {
                result.Type = "(unreadable: " + exception.GetType().Name + ")";
                result.TypeCode = -1;
            }

            return result;
        }

        private static NativeSurface ReadSurface(zoneSurface zs, int index)
        {
            NativeSurface result = new NativeSurface
            {
                Index = index,
                Number = Field(() => zs.number),
                Type = Try(() => zs.type.ToString()),
                Area = Field(() => zs.area),
                InternalArea = Field(() => zs.internalArea),
                Orientation = Field(() => zs.orientation),
                Inclination = Field(() => zs.inclination),
                Reversed = Field(() => zs.reversed),
                Guid = Try(() => zs.GUID)
            };

            buildingElement be = Try(() => zs.buildingElement);
            if (be != null)
            {
                result.BuildingElementGuid = Try(() => be.GUID);
                result.BuildingElementName = Try(() => be.name);
                result.BEType = Try(() => be.BEType, -1);
            }

            zoneSurface link = Try(() => zs.linkSurface);
            if (link != null)
            {
                result.LinkSurfaceGuid = Try(() => link.GUID);
                result.LinkZoneGuid = Try(() => link.zone?.GUID);
            }

            for (int m = 0; m < 64; m++)
            {
                RoomSurface rs = Try(() => zs.GetRoomSurface(m));
                if (rs == null) break;

                NativeRoomSurface room = new NativeRoomSurface { Area = Field(() => rs.area) };
                try
                {
                    Perimeter perimeter = rs.GetPerimeter();
                    if (perimeter == null)
                    {
                        room.Detail = "GetPerimeter returned null";
                    }
                    else
                    {
                        room.Outer = Points(perimeter.GetFace());
                        for (int h = 0; h < 256; h++)
                        {
                            Polygon hole = perimeter.GetHole(h);
                            if (hole == null) break;
                            room.Holes.Add(Points(hole));
                        }
                    }
                }
                catch (Exception exception)
                {
                    room.Detail = "perimeter unreadable: " + exception.GetType().Name;
                }

                result.RoomSurfaces.Add(room);
            }

            return result;
        }

        private static List<double[]> Points(Polygon polygon)
        {
            if (polygon == null)
            {
                return null;
            }

            List<double[]> result = new List<double[]>();
            for (int i = 0; i < 4096; i++)
            {
                TasPoint point = polygon.GetPoint(i);
                if (point == null) break;
                result.Add(new double[] { point.x, point.y, point.z });
            }

            return result;
        }

        // ---- the T3D: the frame fields TAS keeps only there (T-FRAME uncertainty 4) ------------------------------------

        private static NativeT3D ReadT3D(string path_T3D, ReaderInfo info)
        {
            if (string.IsNullOrWhiteSpace(path_T3D))
            {
                return new NativeT3D { Kind = FieldKind.Absent, Detail = "no T3D path given" };
            }

            if (!File.Exists(path_T3D))
            {
                return new NativeT3D { Kind = FieldKind.Absent, Detail = "T3D missing: " + path_T3D };
            }

            info.T3dSha256Before = SnapshotJson.Sha256(path_T3D);
            NativeT3D result = new NativeT3D { Kind = FieldKind.Finite };
            TAS3D.T3DDocument document = null;
            try
            {
                document = new TAS3D.T3DDocument();
                if (!document.Open(path_T3D))
                {
                    return new NativeT3D { Kind = FieldKind.Unreadable, Detail = "T3D Open returned false" };
                }

                TAS3D.Building b = document.Building;

                // TAS3D collections count from 1.
                for (int i = 1; i < 100000; i++)
                {
                    TAS3D.window w = Try(() => b.GetWindow(i));
                    if (w == null) break;
                    result.Windows.Add(new NativeT3DWindow
                    {
                        Index = i,
                        Name = Try(() => w.name),
                        Description = Try(() => w.description),
                        FramePerc = Field(() => w.framePerc),
                        IsPercFrame = Field(() => w.isPercFrame ? 1 : 0),
                        FrameWidth = Field(() => w.frameWidth),
                        FrameDepth = Field(() => w.frameDepth),
                        Width = Field(() => w.width),
                        Height = Field(() => w.height),
                        Transparent = Field(() => w.transparent ? 1 : 0),
                        IsUsed = Field(() => w.isUsed)
                    });
                }

                for (int i = 1; i < 100000; i++)
                {
                    TAS3D.Element e = Try(() => b.GetElement(i));
                    if (e == null) break;
                    result.Elements.Add(new NativeT3DElement
                    {
                        Index = i,
                        Name = Try(() => e.name),
                        BEType = Field(() => e.BEType),
                        Width = Field(() => e.width),
                        ZoneFloorArea = Field(() => e.zoneFloorArea ? 1 : 0),
                        Ghost = Field(() => e.ghost ? 1 : 0),
                        Ground = Field(() => e.ground ? 1 : 0)
                    });
                }

                for (int i = 1; i < 100000; i++)
                {
                    TAS3D.Zone z = Try(() => b.GetZone(i));
                    if (z == null) break;
                    result.Zones.Add(new NativeT3DZone
                    {
                        Index = i,
                        Name = Try(() => z.name),
                        Description = Try(() => z.description),
                        FloorArea = Field(() => z.floorArea),
                        Volume = Field(() => z.volume),
                        IsUsed = Field(() => z.isUsed)
                    });
                }
            }
            catch (Exception exception)
            {
                result.Kind = FieldKind.Unreadable;
                result.Detail = "T3D read failed: " + (exception.InnerException ?? exception).GetType().Name;
            }
            finally
            {
                if (document != null)
                {
                    try { document.Close(); } catch { }
                    Marshal.FinalReleaseComObject(document);
                }
            }

            info.T3dSha256After = SnapshotJson.Sha256(path_T3D);
            if (info.T3dSha256Before != info.T3dSha256After)
            {
                result.Kind = FieldKind.Unreadable;
                result.Detail = "the T3D changed while it was read";
            }

            return result;
        }

        // ---- reader primitives: a throw is Unreadable, never a number ---------------------------------------------------

        private static T Try<T>(Func<T> get, T fallback = default)
        {
            try { return get(); } catch { return fallback; }
        }

        private static NativeField Field(Func<double> get)
        {
            try
            {
                return NativeField.Of(get());
            }
            catch (Exception exception)
            {
                return NativeField.Unreadable((exception.InnerException ?? exception).GetType().Name + ": " + (exception.InnerException ?? exception).Message);
            }
        }

        private static Dictionary<int, NativeField> Slots(Func<int, double> get)
        {
            Dictionary<int, NativeField> result = new Dictionary<int, NativeField>();
            for (int i = 0; i <= 25; i++)
            {
                int slot = i;
                result[slot] = Field(() => get(slot));
            }

            return result;
        }

        private static NativeSeries Series(Func<object> get)
        {
            object value;
            try
            {
                value = get();
            }
            catch (Exception exception)
            {
                return NativeSeries.Unreadable((exception.InnerException ?? exception).GetType().Name);
            }

            if (value == null)
            {
                return NativeSeries.Absent("null");
            }

            if (!(value is Array array) || array.Rank != 1)
            {
                return NativeSeries.Unreadable("not a one-dimensional array: " + value.GetType().FullName);
            }

            double[] values = new double[array.Length];
            int lowerBound = array.GetLowerBound(0);
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = System.Convert.ToDouble(array.GetValue(lowerBound + i), ci);
            }

            return NativeSeries.Of(values, lowerBound);
        }
    }
}
