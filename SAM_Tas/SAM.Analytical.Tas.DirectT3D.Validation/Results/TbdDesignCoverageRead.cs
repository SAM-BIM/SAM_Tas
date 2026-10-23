// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using TBD;

namespace SAM.Analytical.Tas.DirectT3D.Validation.Results
{
    /// <summary>
    /// S10 R2: which design-day groups the TBD holds, read with TBD's own COM API in a fresh process, read-only (sha256
    /// before and after). It is the independent expectation of the TSD design-day data sets. The merged S08 reader is not
    /// reused for this: its enumeration treats an exception as the end of the list, so an unreadable group would look like
    /// a missing one. Here a null ends a list and an exception is recorded, so unreadable coverage always fails closed.
    /// </summary>
    public static class TbdDesignCoverageRead
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        /// <summary>Upper bounds of the enumerations; reaching one is recorded as an error, never as a complete list.</summary>
        public const int MaxGroups = 1000, MaxDays = 400;

        public static TbdDesignCoverage Read(string path)
        {
            TbdDesignCoverage result = new TbdDesignCoverage
            {
                Path = path,
                ProcessId = Process.GetCurrentProcess().Id,
                StartedUtc = DateTime.UtcNow.ToString("o", ci),
                ReaderAssembly = typeof(TbdDesignCoverageRead).Assembly.Location,
            };
            result.ReaderAssemblySha256 = SnapshotJson.Sha256(result.ReaderAssembly);

            // Existence first: a TBD document can create a file at a missing path.
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                result.Errors.Add("TBD missing: " + path);
                return result;
            }

            result.Sha256Before = SnapshotJson.Sha256(path);
            TBDDocument document = null;
            try
            {
                document = new TBDDocument();
                if (document.openReadOnly(path) == 0)
                {
                    result.Errors.Add("TBD openReadOnly returned 0");
                    return result;
                }

                Building building = document.Building;
                if (building == null)
                {
                    result.Errors.Add("TBD has no Building");
                    return result;
                }

                Groups(result, ResultReference.Cooling, i => building.GetCoolingDesignDay(i), x => ((CoolingDesignDay)x).name, (x, j) => ((CoolingDesignDay)x).GetDesignDay(j));
                Groups(result, ResultReference.Heating, i => building.GetHeatingDesignDay(i), x => ((HeatingDesignDay)x).name, (x, j) => ((HeatingDesignDay)x).GetDesignDay(j));
            }
            catch (Exception exception)
            {
                result.Errors.Add("TBD read failed: " + Message(exception));
            }
            finally
            {
                if (document != null)
                {
                    try { document.close(); } catch { }
                    Marshal.FinalReleaseComObject(document);
                }
            }

            result.Sha256After = SnapshotJson.Sha256(path);
            if (result.Sha256After != result.Sha256Before)
            {
                result.Errors.Add("the TBD changed during the read-only read (sha256 " + result.Sha256Before + " -> " + result.Sha256After + ")");
            }

            return result;
        }

        private static void Groups(TbdDesignCoverage result, string role, Func<int, object> get, Func<object, string> name, Func<object, int, TBD.DesignDay> day)
        {
            for (int i = 0; ; i++)
            {
                if (i == MaxGroups)
                {
                    result.Errors.Add(role + " design-day groups: more than " + MaxGroups + " (enumeration not complete)");
                    return;
                }

                object group;
                try
                {
                    group = get(i);
                }
                catch (Exception exception)
                {
                    result.Errors.Add(role + " design-day group " + i + " could not be read: " + Message(exception));
                    return;
                }

                if (group == null) return;

                DesignCoverageGroup item = new DesignCoverageGroup { Role = role, Index = i };
                try
                {
                    item.Name = name(group);
                }
                catch (Exception exception)
                {
                    item.Detail = "name unreadable: " + Message(exception);
                }

                for (int j = 0; ; j++)
                {
                    if (j == MaxDays)
                    {
                        item.Detail = (item.Detail == null ? string.Empty : item.Detail + "; ") + "more than " + MaxDays + " design days (enumeration not complete)";
                        break;
                    }

                    TBD.DesignDay d;
                    try
                    {
                        d = day(group, j);
                    }
                    catch (Exception exception)
                    {
                        item.Detail = (item.Detail == null ? string.Empty : item.Detail + "; ") + "design day " + j + " unreadable: " + Message(exception);
                        break;
                    }

                    if (d == null) break;
                    item.Days.Add(Day(d, j));
                }

                result.Groups.Add(item);
            }
        }

        private static DesignCoverageDay Day(TBD.DesignDay d, int index)
        {
            DesignCoverageDay result = new DesignCoverageDay { Index = index };
            try
            {
                result.Name = d.name;
                dayType dayType = d.GetDayType();
                result.DayType = dayType?.name;
                if (dayType == null) result.Detail = "no day type";
            }
            catch (Exception exception)
            {
                result.Detail = "name or day type unreadable: " + Message(exception);
            }

            try
            {
                result.Repetitions = NativeConvert.Field(d.repetitions);
            }
            catch (Exception exception)
            {
                result.Repetitions = NativeField.Unreadable(Message(exception));
            }

            return result;
        }

        private static string Message(Exception exception)
        {
            Exception inner = exception.InnerException ?? exception;
            return inner.GetType().Name + ": " + inner.Message;
        }
    }
}
