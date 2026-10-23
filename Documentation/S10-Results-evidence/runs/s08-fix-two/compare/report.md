# S10 Tas results round-trip: BLOCKED

Rows 95: PASS 94, FAIL 0, BLOCKED 1. Exit 11.
TSD sha256 `8109d47c92f8de65f8bf7e934e9fb43caa78cb04b717037c86fb80deef28efdc`, TBD sha256 `85216403e95869a72c02f60317498fee6d5ee8f7c4faf760afcb75dd760c1db3` (design-day coverage read `85216403e95869a72c02f60317498fee6d5ee8f7c4faf760afcb75dd760c1db3`), rules sha256 `766a88cdbf1294992c2b089502ad2027d6645674f514628c0a039d16a9e42c40`.

## Calibration (units, signs, interval, shapes, identity)

| Check | Status | Answer |
|---|---|---|
| S10-C1 Is the TSD a full-year simulation (days 1..365)? | verified | days 1..365 |
| S10-C2 Which CLR index of a TSD annual array holds SAM hour h (N-5)? | verified | CLR index = h + 1 (lower bound 1, 8760 elements) |
| S10-C8 Are the TSD zones uniquely identified (GUID and name) and matched to TBD zones? | verified | 2 zone(s), unique names and GUIDs |
| S10-C3 Are the annual arrays complete hourly series (24 finite values per simulated day, 1 h each) at the calibrated lower bound? | verified | hourly, 1 h per element, 8760 elements |
| S10-C4 Are the plant load arrays non-negative magnitudes? | verified | heatingLoad, coolingLoad, heatingProfile and coolingProfile are >= 0 everywhere |
| S10-C5 Do the load arrays close the zone's sensible load breakdown as heating - cooling + terms = 0 (W, heating = plant input, cooling = its negative)? | verified | closes in every zone and hour: the loads are W, heating is plant input and cooling its negative |
| S10-C6 Is the building heating/cooling profile the sum of the zone loads (so it is in W)? | verified | both profiles equal the sum of the zone loads: W |
| S10-C10 Does every TBD design-day group have exactly one TSD design-day data set of the same role and exact name, with no other data set (absence accepted only when the completely read TBD holds no group)? | verified | the TSD design-day data sets correspond one to one, by role and exact name, to the TBD design-day groups |
| S10-C7 Is every design-day data set in the measured calendar representation (one TBD design day of year day d; every series 8760 elements at the N-5 lower bound; complete finite values, loads non-negative, at CLR (d - 1) x 24 + 1 .. d x 24; Tas's -1 at every other element)? | verified | every design-day data set is calendar-shaped: 8760 elements at lower bound 1, populated exactly at its TBD year day, -1 elsewhere |
| S10-C9 Is the design-day index and day-origin convention established from evidence (N-5 lower bound; the TBD year day's block of CLR indices, S10-C7; Tas's peak index = CLR index of an in-day maximum; hour 0 at the block's first index from the TBD gain schedule)? | unresolved | Heating: no independent time signal (no hour-varying TBD gain schedule of the design days' day type with a TSD design-day gain array), so the hour of day of a design-day element is not established |

- S10-C1: firstDay Finite(1), lastDay Finite(365)
- S10-C2: calibration-rules.json 766a88cdbf1294992c2b089502ad2027d6645674f514628c0a039d16a9e42c40: N-5.alignment verified, N-5.weatherIndex verified offset 1; S08 P-ALIGN TSD: externalTemperature lower bound 1, length 8760, pulses aligned with weather shift 0
- S10-C8: TSD zone 1 'FixA' {424293C1-D7B7-4B95-BDAB-D7C31F517F42}: 1 TBD zone(s) with that GUID; TSD zone 2 'FixB' {D267F93B-CB70-4981-9095-8298DEF76FBB}: 1 TBD zone(s) with that GUID
- S10-C3: expected per array: 8760 = 24 x 365 elements at lower bound 1, every element a finite number; Tas Theory Manual: the simulation is a series of hourly snapshots, each array element one hour
- S10-C5: Tas Theory Manual, Sensible Load Breakdown: a balanced account of the zone's sensible heat flows in W, plant input positive for heating and negative for cooling; tolerance per hour 0.01 x sum(|terms|) + 0.5 W; terms solarGain, lightingGain, occupantSensibleGain, equipmentSensibleGain, infVentGain, airMovementGain, buildingHeatTransfer, externalConductionOpaque, externalConductionGlazing, AHUGain; zone 'FixA': 0 hour(s) outside tolerance; largest residual -0.0410919189453125 W at hour 7707 (sum |terms| 413.44380950927734 W); hours with heating > 0: 2044, cooling > 0: 1669, both: 0; roles swapped would violate 3655 hour(s); max |AHUGain| 0 W; zone 'FixB': 0 hour(s) outside tolerance; largest residual -0.193603515625 W at hour 3041 (sum |terms| 273.38304901123047 W); hours with heating > 0: 4225, cooling > 0: 485, both: 0; roles swapped would violate 4693 hour(s); max |AHUGain| 0 W
- S10-C6: building heatingProfile vs sum of zone heatingLoad: 0 hour(s) outside 1E-06 x sum + 1E-06 W; largest difference -6.103515625E-05 W; building coolingProfile vs sum of zone coolingLoad: 0 hour(s) outside 1E-06 x sum + 1E-06 W; largest difference -6.103515625E-05 W
- S10-C10: Cooling: TBD groups ['Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' (1 day(s))]; TSD data sets ['Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad']; Heating: TBD groups ['Budapest.Met.Center ANN HTG 100% CONDS DB' (1 day(s))]; TSD data sets ['Budapest.Met.Center ANN HTG 100% CONDS DB']
- S10-C7: representation measured on the S10 revision-2 acc1 s08-fix-box TSD (both roles, five series each): populated exactly at the TBD yearDay's 24 CLR indices, -1 elsewhere; the year day is the TBD design day's yearDay (raw TBD read, tbd-snapshot.json), matched to the coverage group by role and exact name; Cooling design-day data set 0 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad': TBD yearDay 182 -> CLR 4345..4368; 10 series in 2 zone(s), each 8760 at lower bound 1 with 24 values inside the day and -1 at the other 8736; Heating design-day data set 0 'Budapest.Met.Center ANN HTG 100% CONDS DB': TBD yearDay 39 -> CLR 913..936; 10 series in 2 zone(s), each 8760 at lower bound 1 with 24 values inside the day and -1 at the other 8736
- S10-C9: annual GetPeakZoneGains layout: value row 1, index row 2 (rows from the table's lower bound) fit all 4 positive annual zone peak(s) with index = CLR index (N-5); Cooling: 2 design-day zone peak(s) found in GetPeakZoneGains at value row 1, index row 2 with index = CLR index of an in-day maximum; Cooling 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' zone 'FixA' lightingGain vs TBD ticLG of 'FixA' (CDD): fits at offset(s) [0]; Cooling 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' zone 'FixA' equipmentSensibleGain vs TBD ticESG of 'FixA' (CDD): fits at offset(s) [0]; Cooling 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' zone 'FixB' lightingGain vs TBD ticLG of 'FixB' (CDD): fits at offset(s) [0]; Heating: 2 design-day zone peak(s) found in GetPeakZoneGains at value row 1, index row 2 with index = CLR index of an in-day maximum

## Not PASS (1)

| Row | Class | Expected | Actual | Status | Reason |
|---|---|---|---|---|---|
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|designDayPeak.hourOfDay (FixB) | Hour | BLOCKED | - | BLOCKED | design-day hour of day unresolved (S10-C9): Heating: no independent time signal (no hour-varying TBD gain schedule of the design days' day type with a TSD design-day gain array), so the hour of day of a design-day element is not established |

## Observations (not gated)
- model Heating: max 1698.2847900390625 W at 1 tied hour(s), first 919
- model Cooling: max 1186.4725341796875 W at 1 tied hour(s), first 4381
- zone 'FixA' Heating annual: max 1072.0504150390625 W, 66 tied hour(s), earliest 7, all 7,31,55,79,127,175,199,223,271,295,319,367,391,415,463,487,511,535,559,583
- GetPeakZoneGains annual Heating zone 'FixA' (bounds 0,0, column 0): Unreadable [not a number (String)] | Finite(1072.0504150390625) | Finite(8); reference max 1072.0504150390625, positions + 1 8,32,56,80,128,176,200,224,272,296
- GetPeakZoneGains Heating design day 'Budapest.Met.Center ANN HTG 100% CONDS DB' zone 'FixA' (bounds 0,0, column 0): Unreadable [not a number (String)] | Finite(1072.011962890625) | Finite(913); reference max 1072.011962890625, positions + 1 913,914,915,916,917,918,919,920,921,922
- zone 'FixA' Heating design day: max 1072.011962890625 W in 'Budapest.Met.Center ANN HTG 100% CONDS DB', 24 tied position(s)
- zone 'FixA' Heating governing basis: annual
- zone 'FixA' Cooling annual: max 1064.852783203125 W, 1 tied hour(s), earliest 4381
- GetPeakZoneGains annual Cooling zone 'FixA' (bounds 0,0, column 0): Unreadable [not a number (String)] | Finite(1064.852783203125) | Finite(4382); reference max 1064.852783203125, positions + 1 4382
- GetPeakZoneGains Cooling design day 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' zone 'FixA' (bounds 0,0, column 0): Unreadable [not a number (String)] | Finite(1117.6485595703125) | Finite(4358); reference max 1117.6485595703125, positions + 1 4358
- zone 'FixA' Cooling design day: max 1117.6485595703125 W in 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad', 1 tied position(s)
- zone 'FixA' Cooling governing basis: design day
- zone 'FixB' Heating annual: max 626.234375 W, 14 tied hour(s), earliest 726, all 726,750,774,798,822,846,870,894,918,919,942,8430,8454,8478
- GetPeakZoneGains annual Heating zone 'FixB' (bounds 0,0, column 1): Unreadable [not a number (String)] | Finite(626.234375) | Finite(727); reference max 626.234375, positions + 1 727,751,775,799,823,847,871,895,919,920
- GetPeakZoneGains Heating design day 'Budapest.Met.Center ANN HTG 100% CONDS DB' zone 'FixB' (bounds 0,0, column 1): Unreadable [not a number (String)] | Finite(622.7639770507812) | Finite(913); reference max 622.7639770507812, positions + 1 913,915,917,919,921,923,925,927,929,931
- zone 'FixB' Heating design day: max 622.7639770507812 W in 'Budapest.Met.Center ANN HTG 100% CONDS DB', 12 tied position(s)
- zone 'FixB' Heating governing basis: annual
- zone 'FixB' Cooling annual: max 154.81298828125 W, 1 tied hour(s), earliest 4385
- GetPeakZoneGains annual Cooling zone 'FixB' (bounds 0,0, column 1): Unreadable [not a number (String)] | Finite(154.81298828125) | Finite(4386); reference max 154.81298828125, positions + 1 4386
- GetPeakZoneGains Cooling design day 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' zone 'FixB' (bounds 0,0, column 1): Unreadable [not a number (String)] | Finite(211.9149627685547) | Finite(4362); reference max 211.9149627685547, positions + 1 4362
- zone 'FixB' Cooling design day: max 211.9149627685547 W in 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad', 1 tied position(s)
- zone 'FixB' Cooling governing basis: design day

## Coverage: every required row

| Row | Class | Expected | Actual | Status | Reason |
|---|---|---|---|---|---|
| SAM\|model\|Heating\|consumption | R2 | 1482.3062198656799 | 1482.3062198656799 (units Marked) | PASS | sum of the building heatingProfile (W x 1 h) / 1000 |
| SAM\|model\|Heating\|peakLoad | R1 | 1.6982847900390625 | 1.6982847900390625 (units Marked) | PASS | max of the building heatingProfile / 1000 |
| SAM\|model\|Heating\|peakHour | Hour | 919 | 919 | PASS | earliest maximum of the building heatingProfile (model tie rule; 1 tied) |
| B1a\|model\|Heating\|consumption | R2 | 1482.3062198656799 | 1482.3062198656799 | PASS | sum of the building heatingProfile (W x 1 h) / 1000 |
| B1a\|model\|Heating\|peakLoad | R1 | 1.6982847900390625 | 1.6982847900390625 | PASS | max of the building heatingProfile / 1000 |
| B1a\|model\|Heating\|peakHour | Hour | 919 | 919 | PASS | earliest maximum of the building heatingProfile (model tie rule; 1 tied) |
| SAM\|model\|Cooling\|consumption | R2 | 693.4221556667685 | 693.4221556667685 (units Marked) | PASS | sum of the building coolingProfile (W x 1 h) / 1000 |
| SAM\|model\|Cooling\|peakLoad | R1 | 1.1864725341796876 | 1.1864725341796876 (units Marked) | PASS | max of the building coolingProfile / 1000 |
| SAM\|model\|Cooling\|peakHour | Hour | 4381 | 4381 | PASS | earliest maximum of the building coolingProfile (model tie rule; 1 tied) |
| B1a\|model\|Cooling\|consumption | R2 | 693.4221556667685 | 693.4221556667685 | PASS | sum of the building coolingProfile (W x 1 h) / 1000 |
| B1a\|model\|Cooling\|peakLoad | R1 | 1.1864725341796876 | 1.1864725341796876 | PASS | max of the building coolingProfile / 1000 |
| B1a\|model\|Cooling\|peakHour | Hour | 4381 | 4381 | PASS | earliest maximum of the building coolingProfile (model tie rule; 1 tied) |
| SAM\|model\|-\|floorArea | R1 | 43.739999771118164 | 43.739999771118164 | PASS | sum of the TSD zone floorArea values |
| B1a\|model\|-\|floorArea | R1 | 43.739999771118164 | 43.739999771118164 | PASS | sum of the TSD zone floorArea values |
| SAM\|model\|-\|volume | R1 | 133.40699768066406 | 133.40699768066406 | PASS | sum of the TSD zone volume values |
| B1a\|model\|-\|volume | R1 | 133.40699768066406 | 133.40699768066406 | PASS | sum of the TSD zone volume values |
| SAM\|model\|-\|source | Text | 'SAM.Analytical.Tas' | 'SAM.Analytical.Tas' | PASS | the model result's Source is the Tas converter's assembly |
| SAM\|model\|-\|unitsState | Text | 'Marked' | 'Marked' | PASS | ResultUnitsVersion 1: consumption kWh, peaks kW |
| Identity\|model\|-\|samSpaceCount | Count | 2 | 2 | PASS | one SAM space per TSD zone |
| Identity\|model\|-\|b1aSpaceCount | Count | 2 | 2 | PASS | one B1a space per TSD zone |
| Identity\|model\|-\|unrelatedSpaceResults | Count | 0 | 0 | PASS | every space result is related to a space |
| Identity\|model\|-\|b1aResultSources | Text | 'SAM.Analytical.Tas' | 'SAM.Analytical.Tas' | PASS | the B1a result sources are exactly the Tas source |
| Identity\|model\|-\|b1aState | Text | 'Success' | 'Success' (provenance.state) | PASS | the B1a run state |
| Identity\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|-\|samSpace (FixA) | Count | 1 | 1 (SAM spaces named 'FixA') | PASS | exactly one SAM space with the zone's exact name |
| Identity\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|-\|b1aSpace (FixA) | Count | 1 | 1 (B1a spaces with GUID 5080c00000000000000000000000c0a1 and name 'FixA') | PASS | exactly one B1a space with that SAM space's GUID and name |
| Identity\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|-\|samSpace (FixB) | Count | 1 | 1 (SAM spaces named 'FixB') | PASS | exactly one SAM space with the zone's exact name |
| Identity\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|-\|b1aSpace (FixB) | Count | 1 | 1 (B1a spaces with GUID 5080c00000000000000000000000c0b1 and name 'FixB') | PASS | exactly one B1a space with that SAM space's GUID and name |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|tasResultCount (FixA) | Count | 1 | 1 | PASS | exactly one result of this load type, Source SAM.Analytical.Tas, related to the zone's SAM space |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|reference (FixA) | Text | '{424293C1-D7B7-4B95-BDAB-D7C31F517F42}' | '{424293C1-D7B7-4B95-BDAB-D7C31F517F42}' | PASS | the result's Reference is the TSD zone GUID |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|annualPeak.load (FixA) | R0 | 1072.0504150390625 | 1072.0504150390625 | PASS | max of the annual zone heatingLoad (8760 h) |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|annualPeak.hourOfYear (FixA) | Hour | one of 7,31,55,79,127,175,199,223,271,295,319,367,391,415,463,487,511,535,559,583,... | 7 | PASS | 0-based hour = position (CLR index - 1); 66 tied maximum/maxima, any accepted (S10 tie rule); observed is the earliest of 66 tied |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|annualPeak.hourOfDay (FixA) | Hour | one of 7,8 | 7 | PASS | hour of day of the tied maxima; observed is the earliest of 2 tied |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|designDayPeak.load (FixA) | R0 | 1072.011962890625 | 1072.011962890625 | PASS | max over the Heating design-day data sets of the zone's heatingLoad (1 data set(s) tied; the first wins) |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|designDayPeak.hourOfDay (FixA) | Hour | one of 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,... | 0 | PASS | all 24 hours of the TBD design day are tied maxima, so every hour of day is accepted whatever the within-day origin (S10-C9 origin unresolved: Heating: no independent time signal (no hour-varying TBD gain schedule of the design days' day type with a TSD design-day gain array), so the hour of day of a design-day element is not established); observed is the earliest of 24 tied |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|designDayPeak.name (FixA) | Text | 'Budapest.Met.Center ANN HTG 100% CONDS DB' | 'Budapest.Met.Center ANN HTG 100% CONDS DB' (no design-day name) | PASS | the winning data set's name |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|legacy.sizingMethod (FixA) | Text | 'Simulation' | 'Simulation' (no sizing method) | PASS | the annual peak governs |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|legacy.load (FixA) | R0 | 1072.0504150390625 | 1072.0504150390625 | PASS | the governing peak load |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|legacy.loadIndex (FixA) | Hour | one of 8,32,56,80,128,176,200,224,272,296,320,368,392,416,464,488,512,536,560,584,... | 8 | PASS | Tas's raw 1-based annual index = CLR index (N-5); 66 tied; observed is the earliest of 66 tied |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|designLoad (FixA) | R0 | 1072.0504150390625 | 1072.0504150390625 | PASS | TBD zone maxHeatingLoad (sizing) |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|unmetHours (FixA) | R0 | 40 | 40 | PASS | hours with TSD dryBulbTemp + 0.5 < TBD ticLL |
| B1a\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|peakLoad (FixA) | R0 | 1072.0504150390625 | 1072.0504150390625 | PASS | max of the annual zone heatingLoad (8760 h) |
| B1a\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|peakHour (FixA) | Hour | one of 7,31,55,79,127,175,199,223,271,295,319,367,391,415,463,487,511,535,559,583,... | 7 | PASS | 0-based hour = position (CLR index - 1); 66 tied maximum/maxima, any accepted (S10 tie rule); observed is the earliest of 66 tied |
| B1a\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|designLoad (FixA) | R0 | 1072.0504150390625 | 1072.0504150390625 | PASS | TBD zone maxHeatingLoad (sizing) |
| B1a\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Heating\|unmetHours (FixA) | R0 | 40 | 40 | PASS | hours with TSD dryBulbTemp + 0.5 < TBD ticLL |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|tasResultCount (FixA) | Count | 1 | 1 | PASS | exactly one result of this load type, Source SAM.Analytical.Tas, related to the zone's SAM space |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|reference (FixA) | Text | '{424293C1-D7B7-4B95-BDAB-D7C31F517F42}' | '{424293C1-D7B7-4B95-BDAB-D7C31F517F42}' | PASS | the result's Reference is the TSD zone GUID |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|annualPeak.load (FixA) | R0 | 1064.852783203125 | 1064.852783203125 | PASS | max of the annual zone coolingLoad (8760 h) |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|annualPeak.hourOfYear (FixA) | Hour | 4381 | 4381 | PASS | 0-based hour = position (CLR index - 1); 1 tied maximum/maxima |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|annualPeak.hourOfDay (FixA) | Hour | 13 | 13 | PASS | hour of day of the tied maxima |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|designDayPeak.load (FixA) | R0 | 1117.6485595703125 | 1117.6485595703125 | PASS | max over the Cooling design-day data sets of the zone's coolingLoad (1 data set(s) tied; the first wins) |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|designDayPeak.hourOfDay (FixA) | Hour | 13 | 13 | PASS | hour of day = (index - 1) mod 24, index = CLR index (S10-C9); 1 tied |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|designDayPeak.name (FixA) | Text | 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' | 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' (no design-day name) | PASS | the winning data set's name |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|legacy.sizingMethod (FixA) | Text | 'Cooling Design Day' | 'Cooling Design Day' (no sizing method) | PASS | the design day governs (annual peak not strictly larger) |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|legacy.load (FixA) | R0 | 1117.6485595703125 | 1117.6485595703125 | PASS | the governing peak load |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|legacy.loadIndex (FixA) | Hour | 4358 | 4358 | PASS | Tas's raw design-day index = CLR index (S10-C9); 1 tied |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|designLoad (FixA) | R0 | 1117.7353515625 | 1117.7353515625 | PASS | TBD zone maxCoolingLoad (sizing) |
| SAM\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|unmetHours (FixA) | R0 | 0 | 0 | PASS | hours with TSD dryBulbTemp - 0.5 > TBD ticUL |
| B1a\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|peakLoad (FixA) | R0 | 1064.852783203125 | 1064.852783203125 | PASS | max of the annual zone coolingLoad (8760 h) |
| B1a\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|peakHour (FixA) | Hour | 4381 | 4381 | PASS | 0-based hour = position (CLR index - 1); 1 tied maximum/maxima |
| B1a\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|designLoad (FixA) | R0 | 1117.7353515625 | 1117.7353515625 | PASS | TBD zone maxCoolingLoad (sizing) |
| B1a\|{424293C1-D7B7-4B95-BDAB-D7C31F517F42}\|Cooling\|unmetHours (FixA) | R0 | 0 | 0 | PASS | hours with TSD dryBulbTemp - 0.5 > TBD ticUL |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|tasResultCount (FixB) | Count | 1 | 1 | PASS | exactly one result of this load type, Source SAM.Analytical.Tas, related to the zone's SAM space |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|reference (FixB) | Text | '{D267F93B-CB70-4981-9095-8298DEF76FBB}' | '{D267F93B-CB70-4981-9095-8298DEF76FBB}' | PASS | the result's Reference is the TSD zone GUID |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|annualPeak.load (FixB) | R0 | 626.234375 | 626.234375 | PASS | max of the annual zone heatingLoad (8760 h) |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|annualPeak.hourOfYear (FixB) | Hour | one of 726,750,774,798,822,846,870,894,918,919,942,8430,8454,8478 | 726 | PASS | 0-based hour = position (CLR index - 1); 14 tied maximum/maxima, any accepted (S10 tie rule); observed is the earliest of 14 tied |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|annualPeak.hourOfDay (FixB) | Hour | one of 6,7 | 6 | PASS | hour of day of the tied maxima; observed is the earliest of 2 tied |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|designDayPeak.load (FixB) | R0 | 622.7639770507812 | 622.7639770507812 | PASS | max over the Heating design-day data sets of the zone's heatingLoad (1 data set(s) tied; the first wins) |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|designDayPeak.hourOfDay (FixB) | Hour | BLOCKED | - | BLOCKED | design-day hour of day unresolved (S10-C9): Heating: no independent time signal (no hour-varying TBD gain schedule of the design days' day type with a TSD design-day gain array), so the hour of day of a design-day element is not established |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|designDayPeak.name (FixB) | Text | 'Budapest.Met.Center ANN HTG 100% CONDS DB' | 'Budapest.Met.Center ANN HTG 100% CONDS DB' (no design-day name) | PASS | the winning data set's name |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|legacy.sizingMethod (FixB) | Text | 'Simulation' | 'Simulation' (no sizing method) | PASS | the annual peak governs |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|legacy.load (FixB) | R0 | 626.234375 | 626.234375 | PASS | the governing peak load |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|legacy.loadIndex (FixB) | Hour | one of 727,751,775,799,823,847,871,895,919,920,943,8431,8455,8479 | 727 | PASS | Tas's raw 1-based annual index = CLR index (N-5); 14 tied; observed is the earliest of 14 tied |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|designLoad (FixB) | R0 | 626.234375 | 626.234375 | PASS | TBD zone maxHeatingLoad (sizing) |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|unmetHours (FixB) | R0 | 0 | 0 | PASS | hours with TSD dryBulbTemp + 0.5 < TBD ticLL |
| B1a\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|peakLoad (FixB) | R0 | 626.234375 | 626.234375 | PASS | max of the annual zone heatingLoad (8760 h) |
| B1a\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|peakHour (FixB) | Hour | one of 726,750,774,798,822,846,870,894,918,919,942,8430,8454,8478 | 726 | PASS | 0-based hour = position (CLR index - 1); 14 tied maximum/maxima, any accepted (S10 tie rule); observed is the earliest of 14 tied |
| B1a\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|designLoad (FixB) | R0 | 626.234375 | 626.234375 | PASS | TBD zone maxHeatingLoad (sizing) |
| B1a\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Heating\|unmetHours (FixB) | R0 | 0 | 0 | PASS | hours with TSD dryBulbTemp + 0.5 < TBD ticLL |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|tasResultCount (FixB) | Count | 1 | 1 | PASS | exactly one result of this load type, Source SAM.Analytical.Tas, related to the zone's SAM space |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|reference (FixB) | Text | '{D267F93B-CB70-4981-9095-8298DEF76FBB}' | '{D267F93B-CB70-4981-9095-8298DEF76FBB}' | PASS | the result's Reference is the TSD zone GUID |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|annualPeak.load (FixB) | R0 | 154.81298828125 | 154.81298828125 | PASS | max of the annual zone coolingLoad (8760 h) |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|annualPeak.hourOfYear (FixB) | Hour | 4385 | 4385 | PASS | 0-based hour = position (CLR index - 1); 1 tied maximum/maxima |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|annualPeak.hourOfDay (FixB) | Hour | 17 | 17 | PASS | hour of day of the tied maxima |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|designDayPeak.load (FixB) | R0 | 211.9149627685547 | 211.9149627685547 | PASS | max over the Cooling design-day data sets of the zone's coolingLoad (1 data set(s) tied; the first wins) |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|designDayPeak.hourOfDay (FixB) | Hour | 17 | 17 | PASS | hour of day = (index - 1) mod 24, index = CLR index (S10-C9); 1 tied |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|designDayPeak.name (FixB) | Text | 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' | 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' (no design-day name) | PASS | the winning data set's name |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|legacy.sizingMethod (FixB) | Text | 'Cooling Design Day' | 'Cooling Design Day' (no sizing method) | PASS | the design day governs (annual peak not strictly larger) |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|legacy.load (FixB) | R0 | 211.9149627685547 | 211.9149627685547 | PASS | the governing peak load |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|legacy.loadIndex (FixB) | Hour | 4362 | 4362 | PASS | Tas's raw design-day index = CLR index (S10-C9); 1 tied |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|designLoad (FixB) | R0 | 211.9634246826172 | 211.9634246826172 | PASS | TBD zone maxCoolingLoad (sizing) |
| SAM\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|unmetHours (FixB) | R0 | 0 | 0 | PASS | hours with TSD dryBulbTemp - 0.5 > TBD ticUL |
| B1a\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|peakLoad (FixB) | R0 | 154.81298828125 | 154.81298828125 | PASS | max of the annual zone coolingLoad (8760 h) |
| B1a\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|peakHour (FixB) | Hour | 4385 | 4385 | PASS | 0-based hour = position (CLR index - 1); 1 tied maximum/maxima |
| B1a\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|designLoad (FixB) | R0 | 211.9634246826172 | 211.9634246826172 | PASS | TBD zone maxCoolingLoad (sizing) |
| B1a\|{D267F93B-CB70-4981-9095-8298DEF76FBB}\|Cooling\|unmetHours (FixB) | R0 | 0 | 0 | PASS | hours with TSD dryBulbTemp - 0.5 > TBD ticUL |
