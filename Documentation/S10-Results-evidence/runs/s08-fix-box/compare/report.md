# S10 Tas results round-trip: PASS

Rows 59: PASS 59, FAIL 0, BLOCKED 0. Exit 0.
TSD sha256 `047a51e97dfd816ce8753646ff7533f5aa7404029a2575fe3eb233dd223d721b`, TBD sha256 `fcc945cbdeb35283fddcc289fc7236e59476b149f2fddf05e9aaa26edfce00cc` (design-day coverage read `fcc945cbdeb35283fddcc289fc7236e59476b149f2fddf05e9aaa26edfce00cc`), rules sha256 `766a88cdbf1294992c2b089502ad2027d6645674f514628c0a039d16a9e42c40`.

## Calibration (units, signs, interval, shapes, identity)

| Check | Status | Answer |
|---|---|---|
| S10-C1 Is the TSD a full-year simulation (days 1..365)? | verified | days 1..365 |
| S10-C2 Which CLR index of a TSD annual array holds SAM hour h (N-5)? | verified | CLR index = h + 1 (lower bound 1, 8760 elements) |
| S10-C8 Are the TSD zones uniquely identified (GUID and name) and matched to TBD zones? | verified | 1 zone(s), unique names and GUIDs |
| S10-C3 Are the annual arrays complete hourly series (24 finite values per simulated day, 1 h each) at the calibrated lower bound? | verified | hourly, 1 h per element, 8760 elements |
| S10-C4 Are the plant load arrays non-negative magnitudes? | verified | heatingLoad, coolingLoad, heatingProfile and coolingProfile are >= 0 everywhere |
| S10-C5 Do the load arrays close the zone's sensible load breakdown as heating - cooling + terms = 0 (W, heating = plant input, cooling = its negative)? | verified | closes in every zone and hour: the loads are W, heating is plant input and cooling its negative |
| S10-C6 Is the building heating/cooling profile the sum of the zone loads (so it is in W)? | verified | both profiles equal the sum of the zone loads: W |
| S10-C10 Does every TBD design-day group have exactly one TSD design-day data set of the same role and exact name, with no other data set (absence accepted only when the completely read TBD holds no group)? | verified | the TSD design-day data sets correspond one to one, by role and exact name, to the TBD design-day groups |
| S10-C7 Is every design-day data set in the measured calendar representation (one TBD design day of year day d; every series 8760 elements at the N-5 lower bound; complete finite values, loads non-negative, at CLR (d - 1) x 24 + 1 .. d x 24; Tas's -1 at every other element)? | verified | every design-day data set is calendar-shaped: 8760 elements at lower bound 1, populated exactly at its TBD year day, -1 elsewhere |
| S10-C9 Is the design-day index and day-origin convention established from evidence (N-5 lower bound; the TBD year day's block of CLR indices, S10-C7; Tas's peak index = CLR index of an in-day maximum; hour 0 at the block's first index from the TBD gain schedule)? | unresolved | Heating: no independent time signal (no hour-varying TBD gain schedule of the design days' day type with a TSD design-day gain array), so the hour of day of a design-day element is not established |

- S10-C1: firstDay Finite(1), lastDay Finite(365)
- S10-C2: calibration-rules.json 766a88cdbf1294992c2b089502ad2027d6645674f514628c0a039d16a9e42c40: N-5.alignment verified, N-5.weatherIndex verified offset 1; S08 P-ALIGN TSD: externalTemperature lower bound 1, length 8760, pulses aligned with weather shift 0
- S10-C8: TSD zone 1 'FixBox' {25309C7B-8B43-47F8-AE63-3E235CFE2A7B}: 1 TBD zone(s) with that GUID
- S10-C3: expected per array: 8760 = 24 x 365 elements at lower bound 1, every element a finite number; Tas Theory Manual: the simulation is a series of hourly snapshots, each array element one hour
- S10-C5: Tas Theory Manual, Sensible Load Breakdown: a balanced account of the zone's sensible heat flows in W, plant input positive for heating and negative for cooling; tolerance per hour 0.01 x sum(|terms|) + 0.5 W; terms solarGain, lightingGain, occupantSensibleGain, equipmentSensibleGain, infVentGain, airMovementGain, buildingHeatTransfer, externalConductionOpaque, externalConductionGlazing, AHUGain; zone 'FixBox': 0 hour(s) outside tolerance; largest residual -0.03066873550415039 W at hour 7010 (sum |terms| 432.05929708480835 W); hours with heating > 0: 3487, cooling > 0: 1621, both: 0; roles swapped would violate 5064 hour(s); max |AHUGain| 0 W
- S10-C6: building heatingProfile vs sum of zone heatingLoad: 0 hour(s) outside 1E-06 x sum + 1E-06 W; largest difference 0 W; building coolingProfile vs sum of zone coolingLoad: 0 hour(s) outside 1E-06 x sum + 1E-06 W; largest difference 0 W
- S10-C10: Cooling: TBD groups ['Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' (1 day(s))]; TSD data sets ['Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad']; Heating: TBD groups ['Budapest.Met.Center ANN HTG 100% CONDS DB' (1 day(s))]; TSD data sets ['Budapest.Met.Center ANN HTG 100% CONDS DB']
- S10-C7: representation measured on the S10 revision-2 acc1 s08-fix-box TSD (both roles, five series each): populated exactly at the TBD yearDay's 24 CLR indices, -1 elsewhere; the year day is the TBD design day's yearDay (raw TBD read, tbd-snapshot.json), matched to the coverage group by role and exact name; Cooling design-day data set 0 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad': TBD yearDay 182 -> CLR 4345..4368; 5 series in 1 zone(s), each 8760 at lower bound 1 with 24 values inside the day and -1 at the other 8736; Heating design-day data set 0 'Budapest.Met.Center ANN HTG 100% CONDS DB': TBD yearDay 39 -> CLR 913..936; 5 series in 1 zone(s), each 8760 at lower bound 1 with 24 values inside the day and -1 at the other 8736
- S10-C9: annual GetPeakZoneGains layout: value row 1, index row 2 (rows from the table's lower bound) fit all 2 positive annual zone peak(s) with index = CLR index (N-5); Cooling: 1 design-day zone peak(s) found in GetPeakZoneGains at value row 1, index row 2 with index = CLR index of an in-day maximum; Cooling 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' zone 'FixBox' lightingGain vs TBD ticLG of 'FixBox' (CDD): fits at offset(s) [0]; Heating: 1 design-day zone peak(s) found in GetPeakZoneGains at value row 1, index row 2 with index = CLR index of an in-day maximum; Heating: the within-day origin is unresolved, but every positive design-day zone maximum of the role holds all 24 hours of its design day, so its hour-of-day rows are the same for every origin and are derived; only a role with a narrower tie set stays BLOCKED

## Not PASS (0)

| Row | Class | Expected | Actual | Status | Reason |
|---|---|---|---|---|---|

## Observations (not gated)
- model Heating: max 1428.4979248046875 W at 354 tied hour(s), first 7
- model Cooling: max 1606.56640625 W at 17 tied hour(s), first 3877
- zone 'FixBox' Heating annual: max 1428.4979248046875 W, 354 tied hour(s), earliest 7, all 7,8,9,10,11,31,32,33,34,35,36,41,55,56,57,79,80,103,104,127
- GetPeakZoneGains annual Heating zone 'FixBox' (bounds 0,0, column 0): Unreadable [not a number (String)] | Finite(1428.4979248046875) | Finite(8); reference max 1428.4979248046875, positions + 1 8,9,10,11,12,32,33,34,35,36
- GetPeakZoneGains Heating design day 'Budapest.Met.Center ANN HTG 100% CONDS DB' zone 'FixBox' (bounds 0,0, column 0): Unreadable [not a number (String)] | Finite(1428.449462890625) | Finite(913); reference max 1428.449462890625, positions + 1 913,914,915,916,917,918,919,920,921,922
- zone 'FixBox' Heating design day: max 1428.449462890625 W in 'Budapest.Met.Center ANN HTG 100% CONDS DB', 24 tied position(s)
- zone 'FixBox' Heating governing basis: annual
- zone 'FixBox' Cooling annual: max 1606.56640625 W, 17 tied hour(s), earliest 3877, all 3877,3878,3879,3880,3901,3902,3903,3904,4381,4382,4383,4384,4549,4550,4741,5655,5656
- GetPeakZoneGains annual Cooling zone 'FixBox' (bounds 0,0, column 0): Unreadable [not a number (String)] | Finite(1606.56640625) | Finite(3878); reference max 1606.56640625, positions + 1 3878,3879,3880,3881,3902,3903,3904,3905,4382,4383
- GetPeakZoneGains Cooling design day 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' zone 'FixBox' (bounds 0,0, column 0): Unreadable [not a number (String)] | Finite(1606.423828125) | Finite(4358); reference max 1606.423828125, positions + 1 4358
- zone 'FixBox' Cooling design day: max 1606.423828125 W in 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad', 1 tied position(s)
- zone 'FixBox' Cooling governing basis: annual

## Coverage: every required row

| Row | Class | Expected | Actual | Status | Reason |
|---|---|---|---|---|---|
| SAM\|model\|Heating\|consumption | R2 | 2037.1340381522775 | 2037.1340381522775 (units Marked) | PASS | sum of the building heatingProfile (W x 1 h) / 1000 |
| SAM\|model\|Heating\|peakLoad | R1 | 1.4284979248046874 | 1.4284979248046874 (units Marked) | PASS | max of the building heatingProfile / 1000 |
| SAM\|model\|Heating\|peakHour | Hour | 7 | 7 | PASS | earliest maximum of the building heatingProfile (model tie rule; 354 tied) |
| B1a\|model\|Heating\|consumption | R2 | 2037.1340381522775 | 2037.1340381522775 | PASS | sum of the building heatingProfile (W x 1 h) / 1000 |
| B1a\|model\|Heating\|peakLoad | R1 | 1.4284979248046874 | 1.4284979248046874 | PASS | max of the building heatingProfile / 1000 |
| B1a\|model\|Heating\|peakHour | Hour | 7 | 7 | PASS | earliest maximum of the building heatingProfile (model tie rule; 354 tied) |
| SAM\|model\|Cooling\|consumption | R2 | 994.3502346024811 | 994.3502346024811 (units Marked) | PASS | sum of the building coolingProfile (W x 1 h) / 1000 |
| SAM\|model\|Cooling\|peakLoad | R1 | 1.60656640625 | 1.60656640625 (units Marked) | PASS | max of the building coolingProfile / 1000 |
| SAM\|model\|Cooling\|peakHour | Hour | 3877 | 3877 | PASS | earliest maximum of the building coolingProfile (model tie rule; 17 tied) |
| B1a\|model\|Cooling\|consumption | R2 | 994.3502346024811 | 994.3502346024811 | PASS | sum of the building coolingProfile (W x 1 h) / 1000 |
| B1a\|model\|Cooling\|peakLoad | R1 | 1.60656640625 | 1.60656640625 | PASS | max of the building coolingProfile / 1000 |
| B1a\|model\|Cooling\|peakHour | Hour | 3877 | 3877 | PASS | earliest maximum of the building coolingProfile (model tie rule; 17 tied) |
| SAM\|model\|-\|floorArea | R1 | 26.8799991607666 | 26.8799991607666 | PASS | sum of the TSD zone floorArea values |
| B1a\|model\|-\|floorArea | R1 | 26.8799991607666 | 26.8799991607666 | PASS | sum of the TSD zone floorArea values |
| SAM\|model\|-\|volume | R1 | 76.60800170898438 | 76.60800170898438 | PASS | sum of the TSD zone volume values |
| B1a\|model\|-\|volume | R1 | 76.60800170898438 | 76.60800170898438 | PASS | sum of the TSD zone volume values |
| SAM\|model\|-\|source | Text | 'SAM.Analytical.Tas' | 'SAM.Analytical.Tas' | PASS | the model result's Source is the Tas converter's assembly |
| SAM\|model\|-\|unitsState | Text | 'Marked' | 'Marked' | PASS | ResultUnitsVersion 1: consumption kWh, peaks kW |
| Identity\|model\|-\|samSpaceCount | Count | 1 | 1 | PASS | one SAM space per TSD zone |
| Identity\|model\|-\|b1aSpaceCount | Count | 1 | 1 | PASS | one B1a space per TSD zone |
| Identity\|model\|-\|unrelatedSpaceResults | Count | 0 | 0 | PASS | every space result is related to a space |
| Identity\|model\|-\|b1aResultSources | Text | 'SAM.Analytical.Tas' | 'SAM.Analytical.Tas' | PASS | the B1a result sources are exactly the Tas source |
| Identity\|model\|-\|b1aState | Text | 'Success' | 'Success' (provenance.state) | PASS | the B1a run state |
| Identity\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|-\|samSpace (FixBox) | Count | 1 | 1 (SAM spaces named 'FixBox') | PASS | exactly one SAM space with the zone's exact name |
| Identity\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|-\|b1aSpace (FixBox) | Count | 1 | 1 (B1a spaces with GUID 5080c00000000000000000000000c001 and name 'FixBox') | PASS | exactly one B1a space with that SAM space's GUID and name |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|tasResultCount (FixBox) | Count | 1 | 1 | PASS | exactly one result of this load type, Source SAM.Analytical.Tas, related to the zone's SAM space |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|reference (FixBox) | Text | '{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}' | '{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}' | PASS | the result's Reference is the TSD zone GUID |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|annualPeak.load (FixBox) | R0 | 1428.4979248046875 | 1428.4979248046875 | PASS | max of the annual zone heatingLoad (8760 h) |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|annualPeak.hourOfYear (FixBox) | Hour | one of 7,8,9,10,11,31,32,33,34,35,36,41,55,56,57,79,80,103,104,127,... | 7 | PASS | 0-based hour = position (CLR index - 1); 354 tied maximum/maxima, any accepted (S10 tie rule); observed is the earliest of 354 tied |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|annualPeak.hourOfDay (FixBox) | Hour | one of 7,8,9,10,11,12,17,13,14,15,16,18 | 7 | PASS | hour of day of the tied maxima; observed is the earliest of 12 tied |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|designDayPeak.load (FixBox) | R0 | 1428.449462890625 | 1428.449462890625 | PASS | max over the Heating design-day data sets of the zone's heatingLoad (1 data set(s) tied; the first wins) |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|designDayPeak.hourOfDay (FixBox) | Hour | one of 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,... | 0 | PASS | all 24 hours of the TBD design day are tied maxima, so every hour of day is accepted whatever the within-day origin (S10-C9 origin unresolved: Heating: no independent time signal (no hour-varying TBD gain schedule of the design days' day type with a TSD design-day gain array), so the hour of day of a design-day element is not established); observed is the earliest of 24 tied |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|designDayPeak.name (FixBox) | Text | 'Budapest.Met.Center ANN HTG 100% CONDS DB' | 'Budapest.Met.Center ANN HTG 100% CONDS DB' (no design-day name) | PASS | the winning data set's name |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|legacy.sizingMethod (FixBox) | Text | 'Simulation' | 'Simulation' (no sizing method) | PASS | the annual peak governs |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|legacy.load (FixBox) | R0 | 1428.4979248046875 | 1428.4979248046875 | PASS | the governing peak load |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|legacy.loadIndex (FixBox) | Hour | one of 8,9,10,11,12,32,33,34,35,36,37,42,56,57,58,80,81,104,105,128,... | 8 | PASS | Tas's raw 1-based annual index = CLR index (N-5); 354 tied; observed is the earliest of 354 tied |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|designLoad (FixBox) | R0 | 1428.4979248046875 | 1428.4979248046875 | PASS | TBD zone maxHeatingLoad (sizing) |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|unmetHours (FixBox) | R0 | 292 | 292 | PASS | hours with TSD dryBulbTemp + 0.5 < TBD ticLL |
| B1a\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|peakLoad (FixBox) | R0 | 1428.4979248046875 | 1428.4979248046875 | PASS | max of the annual zone heatingLoad (8760 h) |
| B1a\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|peakHour (FixBox) | Hour | one of 7,8,9,10,11,31,32,33,34,35,36,41,55,56,57,79,80,103,104,127,... | 7 | PASS | 0-based hour = position (CLR index - 1); 354 tied maximum/maxima, any accepted (S10 tie rule); observed is the earliest of 354 tied |
| B1a\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|designLoad (FixBox) | R0 | 1428.4979248046875 | 1428.4979248046875 | PASS | TBD zone maxHeatingLoad (sizing) |
| B1a\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Heating\|unmetHours (FixBox) | R0 | 292 | 292 | PASS | hours with TSD dryBulbTemp + 0.5 < TBD ticLL |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|tasResultCount (FixBox) | Count | 1 | 1 | PASS | exactly one result of this load type, Source SAM.Analytical.Tas, related to the zone's SAM space |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|reference (FixBox) | Text | '{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}' | '{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}' | PASS | the result's Reference is the TSD zone GUID |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|annualPeak.load (FixBox) | R0 | 1606.56640625 | 1606.56640625 | PASS | max of the annual zone coolingLoad (8760 h) |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|annualPeak.hourOfYear (FixBox) | Hour | one of 3877,3878,3879,3880,3901,3902,3903,3904,4381,4382,4383,4384,4549,4550,4741,5655,5656 | 3877 | PASS | 0-based hour = position (CLR index - 1); 17 tied maximum/maxima, any accepted (S10 tie rule); observed is the earliest of 17 tied |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|annualPeak.hourOfDay (FixBox) | Hour | one of 13,14,15,16 | 13 | PASS | hour of day of the tied maxima; observed is the earliest of 4 tied |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|designDayPeak.load (FixBox) | R0 | 1606.423828125 | 1606.423828125 | PASS | max over the Cooling design-day data sets of the zone's coolingLoad (1 data set(s) tied; the first wins) |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|designDayPeak.hourOfDay (FixBox) | Hour | 13 | 13 | PASS | hour of day = (index - 1) mod 24, index = CLR index (S10-C9); 1 tied |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|designDayPeak.name (FixBox) | Text | 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' | 'Budapest.Met.Center ANN CLG 0% CONDS DB=>GRad' (no design-day name) | PASS | the winning data set's name |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|legacy.sizingMethod (FixBox) | Text | 'Simulation' | 'Simulation' (no sizing method) | PASS | the annual peak governs |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|legacy.load (FixBox) | R0 | 1606.56640625 | 1606.56640625 | PASS | the governing peak load |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|legacy.loadIndex (FixBox) | Hour | one of 3878,3879,3880,3881,3902,3903,3904,3905,4382,4383,4384,4385,4550,4551,4742,5656,5657 | 3878 | PASS | Tas's raw 1-based annual index = CLR index (N-5); 17 tied; observed is the earliest of 17 tied |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|designLoad (FixBox) | R0 | 1606.56640625 | 1606.56640625 | PASS | TBD zone maxCoolingLoad (sizing) |
| SAM\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|unmetHours (FixBox) | R0 | 9 | 9 | PASS | hours with TSD dryBulbTemp - 0.5 > TBD ticUL |
| B1a\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|peakLoad (FixBox) | R0 | 1606.56640625 | 1606.56640625 | PASS | max of the annual zone coolingLoad (8760 h) |
| B1a\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|peakHour (FixBox) | Hour | one of 3877,3878,3879,3880,3901,3902,3903,3904,4381,4382,4383,4384,4549,4550,4741,5655,5656 | 3877 | PASS | 0-based hour = position (CLR index - 1); 17 tied maximum/maxima, any accepted (S10 tie rule); observed is the earliest of 17 tied |
| B1a\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|designLoad (FixBox) | R0 | 1606.56640625 | 1606.56640625 | PASS | TBD zone maxCoolingLoad (sizing) |
| B1a\|{25309C7B-8B43-47F8-AE63-3E235CFE2A7B}\|Cooling\|unmetHours (FixBox) | R0 | 9 | 9 | PASS | hours with TSD dryBulbTemp - 0.5 > TBD ticUL |
