// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using TBD;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static TBD.ApertureType SetApertureType(this Building building, buildingElement buildingElement, ISingleOpeningProperties singleOpeningProperties, string name = null, int index = -1)
        {
            return SetApertureType(building, buildingElement, singleOpeningProperties, out string _, name, index);
        }

        /// <summary>
        /// Writes the aperture control (factor, discharge coefficient, day types and - where the opening
        /// properties carry one - an availability schedule) for one TBD building element.
        /// <para>
        /// <b>A schedule and a Function are not mutually exclusive.</b> When both are present,
        /// <c>profile.type</c> is <c>ticFunctionProfile</c> so the function governs the base opening curve,
        /// and the schedule stays assigned as an availability multiplier on top of it - the same combination
        /// <see cref="AssignApertureTypes(Building, buildingElement, IEnumerable{dayType}, ApertureConstruction)"/>
        /// already writes for its own day/night split. A schedule with no function is written as
        /// <c>ticValueProfile</c>, where the schedule's own values are the whole curve.
        /// </para>
        /// <para>
        /// <b>Write order matters, and the schedule goes LAST.</b> The profile's mode is established first
        /// (<c>value</c>, <c>factor</c>, <c>setbackValue</c>, <c>type</c>, <c>function</c>) and
        /// <c>profile.schedule</c> is assigned only after that. Previously <c>profile.type</c> was set to
        /// <c>ticFunctionProfile</c> AFTER the schedule had been assigned, which risked the mode change
        /// discarding the schedule reference. The assignment is then read straight back and its values
        /// re-verified, so a schedule-write failure and a profile-assignment failure report as two different
        /// things instead of both surfacing as "the schedule is wrong in TAS".
        /// </para>
        /// <para>
        /// <b><c>setbackValue</c> is part of the schedule's meaning.</b> A TBD schedule selects between the
        /// profile's own value/function for an ON hour and its <c>setbackValue</c> for an OFF hour, so an
        /// availability schedule is only meaningful alongside a setback of 0. It is written explicitly
        /// whenever a schedule is written, rather than relied on to be 0 already.
        /// </para>
        /// <para>
        /// <b>Reuse, never overwrite.</b> Schedules are matched by their 24 VALUES, not by name - see
        /// <see cref="Create.GetOrCreateSchedule(Building, DailyAvailabilitySchedule, out string)"/> - so
        /// repeated export reuses one building-level schedule instead of duplicating it. If the matched TBD
        /// aperture type already carries a schedule, that schedule is retained when its values equal the
        /// requested ones (behaviourally the same control), and the write is REFUSED when they differ: a
        /// differently-valued schedule may be user-authored control this method has no business erasing.
        /// Value equality is the test; the name is not.
        /// </para>
        /// <para>
        /// <b>What the refusals do and do not guarantee.</b> Every failure returns null with
        /// <paramref name="refusal"/> set, and the guarantee is specifically this: <b>a failed or
        /// incompatible schedule is never assigned, and an existing different-valued schedule is never
        /// overwritten.</b> No TBD schedule is created before the SAM-side source has been validated either,
        /// which matters because <c>TBD.Building</c> has no <c>RemoveSchedule</c> and a schedule created in
        /// error could not be withdrawn.
        /// </para>
        /// <para>
        /// <b>This method is NOT transactional, and must not be described as such.</b> By the time a refusal
        /// can fire, it may already have created the aperture type and written its <c>description</c>; and a
        /// refusal from the final assignment read-back comes after <c>dischargeCoefficient</c>, <c>value</c>,
        /// <c>factor</c>, <c>setbackValue</c>, <c>type</c> and <c>function</c> have been written. Schedule
        /// resolution is deliberately hoisted above all of those writes - it needs none of them - so an
        /// unusable source, a naming collision or a failed schedule write leaves the profile's existing mode
        /// untouched. Only the two assignment-verification refusals are unavoidably late, because they exist
        /// to check an assignment that has to have happened first.
        /// </para>
        /// </summary>
        /// <param name="refusal">Why nothing was written, or null on success.</param>
        public static TBD.ApertureType SetApertureType(this Building building, buildingElement buildingElement, ISingleOpeningProperties singleOpeningProperties, out string refusal, string name = null, int index = -1)
        {
            refusal = null;

            if(building == null || buildingElement == null || singleOpeningProperties == null)
            {
                return null;
            }

            string name_Temp = name;
            if(name_Temp == null)
            {
                name_Temp = buildingElement.name;
            }

            if(string.IsNullOrWhiteSpace(name_Temp))
            {
                return null;
            }

            if(index != -1)
            {
                name_Temp = string.Format("{0} {1}", name_Temp, index);
            }

            //Resolve the SAM-side schedule source first. This touches no COM at all, so an opening that
            //states an unusable schedule is refused before anything in the TBD has been created or changed.
            bool scheduleRequested = singleOpeningProperties.TryGetOpeningScheduleSource(out string name_Schedule, out int[] values_Schedule, out string refusal_Source);
            if (refusal_Source != null)
            {
                refusal = string.Format("Aperture type '{0}': {1}", name_Temp, refusal_Source);
                return null;
            }

            bool apertureType_Existed = building.ApertureType(name_Temp) != null;

            TBD.ApertureType result = building.ApertureType(name_Temp);
            if(result == null)
            {
                result = building.AddApertureType(null);
                if (result == null)
                {
                    return null;
                }

                result.name = name_Temp;
            }

            if (singleOpeningProperties.TryGetValue(OpeningPropertiesParameter.Description, out string description))
            {
                result.description = description;
            }

            dynamic @dynamic = result;

            profile profile = @dynamic.GetProfile();
            if (profile == null)
            {
                //Everything below writes through this profile, so there is nothing to write to. Reported
                //rather than thrown, in keeping with this method's other outcomes.
                refusal = string.Format("Aperture type '{0}' has no TBD profile to write the aperture control to.", name_Temp);
                return null;
            }

            //An aperture type that already carries a schedule is judged by that schedule's VALUES, before
            //anything is written. Equal values are the same control by another name and are retained;
            //different values belong to somebody else and are refused, not overwritten.
            schedule schedule_Resolved = null;
            if (scheduleRequested && apertureType_Existed)
            {
                schedule schedule_Existing = profile.schedule;
                if (schedule_Existing != null)
                {
                    int[] values_Existing = schedule_Existing.HourlyValues();
                    if (values_Existing != null && Query.ScheduleValuesEqual(values_Existing, values_Schedule))
                    {
                        schedule_Resolved = schedule_Existing;
                    }
                    else
                    {
                        refusal = string.Format("Aperture type '{0}' already carries a schedule ('{1}') whose 24 hourly values differ from the requested availability schedule, so it was left untouched rather than overwritten. Values, not names, decide whether an existing schedule is compatible.", name_Temp, schedule_Existing.name);
                        return null;
                    }
                }
            }

            //Resolved BEFORE the profile is touched. Nothing below this point is needed to resolve it, so
            //hoisting it here means an invalid source, a naming collision or a failed COM write leaves the
            //profile's existing mode, factor and function exactly as they were, instead of half-rewritten.
            if (scheduleRequested && schedule_Resolved == null)
            {
                //Validates, then reuses by value, then creates at most one schedule and verifies its write
                //by reading all 24 values back.
                schedule_Resolved = building.GetOrCreateSchedule(name_Schedule, values_Schedule, out string refusal_Schedule);
                if (schedule_Resolved == null)
                {
                    refusal = string.Format("Aperture type '{0}': {1}", name_Temp, refusal_Schedule ?? "no TBD schedule could be resolved.");
                    return null;
                }
            }

            @dynamic.dischargeCoefficient = System.Convert.ToSingle(singleOpeningProperties.GetDischargeCoefficient());

            profile.value = 1;
            profile.factor = System.Convert.ToSingle(singleOpeningProperties.GetFactor());

            if (singleOpeningProperties is PartOOpeningProperties partOOpeningProperties_Restriction && partOOpeningProperties_Restriction.OpeningRestriction == OpeningRestriction.AlwaysClosed)
            {
                //The opening takes no part in the overheating ventilation strategy. Zeroing the factor
                //multiplies out any function- or schedule-driven curve regardless of what else this opening
                //carries, without needing a second 24-hour zero schedule purely for symmetry.
                profile.factor = 0;
            }

            if (scheduleRequested)
            {
                //The schedule's OFF hours select this value, so an availability schedule requires it to be 0.
                profile.setbackValue = 0;
            }

            //The profile's mode is established BEFORE the schedule is assigned - see the remarks. A Function
            //claims the base curve; a schedule on its own is the curve.
            if (singleOpeningProperties.TryGetValue(OpeningPropertiesParameter.Function, out string function))
            {
                profile.type = ProfileTypes.ticFunctionProfile;
                profile.function = function;
            }
            else if (scheduleRequested)
            {
                //profile.type = ProfileTypes.ticHourlyFunctionProfile;  //TODO: 2023-04-19 To be implemented once Tas allows ticHourlyFunctionProfile or ticYearlyFunctionProfile
                profile.type = ProfileTypes.ticValueProfile;
            }

            if (scheduleRequested)
            {
                profile.schedule = schedule_Resolved;

                //Read the assignment back. This separates "the schedule did not persist its values" from
                //"the profile did not keep the schedule reference" - the second being what a mode change
                //after assignment could cause.
                schedule schedule_Assigned = profile.schedule;
                if (schedule_Assigned == null)
                {
                    refusal = string.Format("Aperture type '{0}': the TBD profile did not retain the assigned schedule '{1}'.", name_Temp, schedule_Resolved.name);
                    return null;
                }

                int[] values_Assigned = schedule_Assigned.HourlyValues();
                if (!Query.ScheduleValuesEqual(values_Assigned, values_Schedule))
                {
                    refusal = string.Format("Aperture type '{0}': the schedule assigned to the TBD profile ('{1}') does not read back the requested 24 hourly values.", name_Temp, schedule_Assigned.name);
                    return null;
                }
            }

            List<dayType> dayTypes = building.DayTypes();
            if (dayTypes != null)
            {
                dayTypes.RemoveAll(x => x.name.Equals("HDD") || x.name.Equals("CDD"));
                foreach (dayType dayType in dayTypes)
                    @dynamic.SetDayType(dayType, true);
            }

            if(buildingElement.ApertureTypes().Find(x => x.name == @dynamic.name) == null)
            {
                buildingElement.AssignApertureType(@dynamic);
            }

            return result;
        }
    }
}
