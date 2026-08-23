// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProfileDefinition = SAM.Analytical.Tas.ProfileDefinition;
using ProfileReuseIndex = SAM.Analytical.Tas.ProfileReuseIndex;
using TasQuery = SAM.Analytical.Tas.Query;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// <b>The TBD -&gt; SAM profile seam: what makes two imported profiles the same reusable library definition,
    /// the deterministic naming that follows, and the guarantee that every rewritten reference still resolves.</b>
    /// <para>
    /// A SAM <c>Profile</c> is a library-level REUSABLE DEFINITION - a native SAM model already shares one across
    /// every <c>InternalCondition</c> that references it. The TBD import instead minted one per internal-condition
    /// slot and named it <c>"{internal condition} [{profile}]"</c>, so a two-zone building carrying one activity
    /// produced two copies of every schedule. The decision that governs whether an import creates forty-two
    /// profiles or twenty is therefore <b>definition equality</b>: category plus complete flattened values, and
    /// nothing about a zone.
    /// </para>
    /// <para>
    /// <b>Why these tests need no installed TAS.</b> Everything that decides is COM-free:
    /// <see cref="ProfileDefinition"/>, <see cref="ProfileReuseIndex"/>, <c>TasQuery.ProfileSignature</c>,
    /// <c>TasQuery.ProfileName</c> and <c>TasQuery.ProfileCategory</c> name no TAS COM type. What genuinely needs
    /// COM is reading the values out of a <c>TBD.profile</c>, and the registration API takes those values as
    /// plain doubles for exactly that reason - so the whole reuse and naming decision is exercised here, and the
    /// COM layer above it is a one-line read per slot.
    /// </para>
    /// </summary>
    [TestFixture]
    public class ProfileDefinitionReuseTests
    {
        // TBD.Profiles slot numbers. Named here rather than referenced, so this project keeps needing no
        // Interop.TBD reference; the values are pinned against the interop metadata by
        // Slots_ProductionSlotTable_MatchesTheseNumbers.
        private const int ticUL = 1;
        private const int ticLL = 2;
        private const int ticHUL = 5;
        private const int ticHLL = 6;
        private const int ticI = 7;
        private const int ticV = 8;
        private const int ticLG = 9;
        private const int ticOSG = 10;
        private const int ticOLG = 11;
        private const int ticESG = 12;
        private const int ticELG = 13;
        private const int ticCOG = 18;

        private const string Infiltration = "Infiltration";
        private const string Lighting = "Lighting";
        private const string Occupancy = "Occupancy";
        private const string EquipmentSensible = "Equipment Sensible";
        private const string EquipmentLatent = "Equipment Latent";
        private const string Pollutant = "Pollutant";
        private const string Cooling = "Cooling";
        private const string Heating = "Heating";
        private const string Humidification = "Humidification";
        private const string Dehumidification = "Dehumidification";

        // =================================================================================================
        // Builders
        // =================================================================================================

        /// <summary>Run-length expansion, so a 24-value profile reads as the shape it is.</summary>
        private static double[] Values(params double[] runs)
        {
            List<double> result = new List<double>();
            for (int i = 0; i < runs.Length; i += 2)
            {
                for (int j = 0; j < (int)runs[i + 1]; j++)
                {
                    result.Add(runs[i]);
                }
            }

            return result.ToArray();
        }

        private static double[] Flat(double value, int count)
        {
            return Enumerable.Repeat(value, count).ToArray();
        }

        private static ProfileDefinition Definition(string category, params double[] values)
        {
            return new ProfileDefinition(category, values);
        }

        /// <summary>The legacy name the import gives a profile it is NOT sharing.</summary>
        private static string Legacy(string internalConditionName, string profileName)
        {
            return string.Format("{0} [{1}]", internalConditionName, profileName);
        }

        private static bool Register(ProfileReuseIndex profileReuseIndex, string internalConditionName, int slot, string category, string sourceName, double[] values)
        {
            return profileReuseIndex.Register(internalConditionName, slot, category, values, sourceName, Legacy(internalConditionName, sourceName));
        }

        private static ProfileLibrary Library(ProfileReuseIndex profileReuseIndex)
        {
            ProfileLibrary result = new ProfileLibrary("Test");
            profileReuseIndex.Profiles.ForEach(x => result.Add(x));

            return result;
        }

        // =================================================================================================
        // The ModelA-Tas fixture, as data
        // =================================================================================================

        /// <summary>
        /// One TBD internal-condition profile slot, as the import reads it.
        /// </summary>
        private sealed class Slot
        {
            public Slot(string internalConditionName, int slot, string category, string sourceName, double[] values)
            {
                InternalConditionName = internalConditionName;
                Index = slot;
                Category = category;
                SourceName = sourceName;
                ProfileValues = values;
            }

            public string InternalConditionName { get; }
            public int Index { get; }
            public string Category { get; }
            public string SourceName { get; }
            public double[] ProfileValues { get; }
        }

        /// <summary>
        /// The eleven slots a non-HDD <c>ModelA-Tas</c> cell carries, with the values read out of the fixture.
        /// </summary>
        private static List<Slot> Slots_Cell(string name)
        {
            return new List<Slot>
            {
                new Slot(name, ticI,   Infiltration,      "Constant",            Flat(1.0, 24)),
                new Slot(name, ticLG,  Lighting,          "8to18",               Values(0, 7, 0.5, 1, 1, 10, 0.5, 1, 0, 5)),
                new Slot(name, ticOLG, Occupancy,         "8to19",               Values(0, 7, 0.5, 1, 1, 11, 0.5, 1, 0, 4)),
                new Slot(name, ticOSG, Occupancy,         "8to19",               Values(0, 7, 0.5, 1, 1, 11, 0.5, 1, 0, 4)),
                new Slot(name, ticESG, EquipmentSensible, "8to19",               Values(0, 7, 0.5, 1, 1, 11, 0.5, 1, 0, 4)),
                new Slot(name, ticELG, EquipmentLatent,   "OFF",                 Flat(0.0, 24)),
                new Slot(name, ticCOG, Pollutant,         "OFF",                 Flat(0.0, 24)),
                new Slot(name, ticUL,  Cooling,           "CLG_7to19_25",        Values(28, 7, 25, 12, 28, 5)),
                new Slot(name, ticLL,  Heating,           "HTG_7to19_21",        Values(16, 7, 21, 12, 16, 5)),
                new Slot(name, ticHLL, Humidification,    "No Humidification",   Flat(0.0, 24)),
                new Slot(name, ticHUL, Dehumidification,  "No Dehumidification", Flat(100.0, 24)),
            };
        }

        /// <summary>
        /// The eleven slots an HDD sizing variant carries - all single-value profiles, and the source of both
        /// name collisions in the fixture (<c>Infiltration::Constant</c> and <c>Heating::HTG_7to19_21</c>, each
        /// wanted by a one-value definition AND by a 24-value one).
        /// </summary>
        private static List<Slot> Slots_Cell_HDD(string name)
        {
            return new List<Slot>
            {
                new Slot(name, ticI,   Infiltration,      "Constant",                 new[] { 0.20000000298023224 }),
                new Slot(name, ticLG,  Lighting,          "Lighting Gain",            new[] { 0.0 }),
                new Slot(name, ticOLG, Occupancy,         "Occupancy Latent Gain",    new[] { 0.0 }),
                new Slot(name, ticOSG, Occupancy,         "Occupancy Sensible Gain",  new[] { 0.0 }),
                new Slot(name, ticESG, EquipmentSensible, "Equipment Sensible Gain",  new[] { 0.0 }),
                new Slot(name, ticELG, EquipmentLatent,   "Equipment Latent Gain",    new[] { 0.0 }),
                new Slot(name, ticCOG, Pollutant,         "Pollutant Generation",     new[] { 0.0 }),
                new Slot(name, ticUL,  Cooling,           "Upper Limit",              new[] { 150.0 }),
                new Slot(name, ticLL,  Heating,           "HTG_7to19_21",             new[] { 21.0 }),
                new Slot(name, ticHLL, Humidification,    "Humidity Lower Limit",     new[] { 0.0 }),
                new Slot(name, ticHUL, Dehumidification,  "Humidity Upper Limit",     new[] { 100.0 }),
            };
        }

        /// <summary>
        /// Every profile slot <c>ModelA-Tas.sam</c>'s four internal conditions carry: 44 slots, which the legacy
        /// import turned into 42 library entries (the two lost pairs are each cell's occupancy sensible and
        /// latent slots, which share a source name and so silently overwrote one another by library key).
        /// </summary>
        private static List<Slot> Slots_ModelA()
        {
            List<Slot> result = new List<Slot>();
            result.AddRange(Slots_Cell("Cell 1"));
            result.AddRange(Slots_Cell("Cell 2"));
            result.AddRange(Slots_Cell_HDD("Cell 1 - HDD"));
            result.AddRange(Slots_Cell_HDD("Cell 2 - HDD"));

            return result;
        }

        private static ProfileReuseIndex Index(IEnumerable<Slot> slots)
        {
            ProfileReuseIndex result = new ProfileReuseIndex();
            foreach (Slot slot in slots)
            {
                Register(result, slot.InternalConditionName, slot.Index, slot.Category, slot.SourceName, slot.ProfileValues);
            }

            result.Resolve();

            return result;
        }

        // =================================================================================================
        // 1-8. Equality
        // =================================================================================================

        [Test]
        public void Equality_SameCategorySameValues_IsOneDefinition()
        {
            ProfileDefinition a = Definition(Heating, Values(16, 7, 21, 12, 16, 5));
            ProfileDefinition b = Definition(Heating, Values(16, 7, 21, 12, 16, 5));

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a.CompareTo(b), Is.Zero);

            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "IC A", ticLL, Heating, "HTG", Values(16, 7, 21, 12, 16, 5));
            Register(profileReuseIndex, "IC B", ticLL, Heating, "HTG", Values(16, 7, 21, 12, 16, 5));
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.DefinitionCount, Is.EqualTo(1));
        }

        [Test]
        public void Equality_SameCategoryDifferentValues_AreTwoDefinitions()
        {
            ProfileDefinition a = Definition(Heating, Values(16, 7, 21, 12, 16, 5));
            ProfileDefinition b = Definition(Heating, Values(16, 7, 22, 12, 16, 5));

            Assert.That(a, Is.Not.EqualTo(b));

            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "IC A", ticLL, Heating, "HTG", Values(16, 7, 21, 12, 16, 5));
            Register(profileReuseIndex, "IC B", ticLL, Heating, "HTG", Values(16, 7, 22, 12, 16, 5));
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.DefinitionCount, Is.EqualTo(2));
        }

        [Test]
        public void Equality_SameValuesDifferentCategory_AreTwoDefinitions()
        {
            ProfileDefinition a = Definition(Heating, Flat(1.0, 24));
            ProfileDefinition b = Definition(Cooling, Flat(1.0, 24));

            Assert.That(a, Is.Not.EqualTo(b));

            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "IC A", ticLL, Heating, "Constant", Flat(1.0, 24));
            Register(profileReuseIndex, "IC A", ticUL, Cooling, "Constant", Flat(1.0, 24));
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.DefinitionCount, Is.EqualTo(2));

            //Category scopes the name claim set, exactly as the ProfileLibrary key "{Category}::{Name}" does,
            //so both keep the bare source name.
            Assert.That(profileReuseIndex.GetProfileName(Heating, Flat(1.0, 24)), Is.EqualTo("Constant"));
            Assert.That(profileReuseIndex.GetProfileName(Cooling, Flat(1.0, 24)), Is.EqualTo("Constant"));
        }

        [Test]
        public void Equality_DifferentSourceNamesSameCategoryAndValues_IsOneDefinition()
        {
            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "IC A", ticLL, Heating, "Setpoint A", Flat(21.0, 24));
            Register(profileReuseIndex, "IC B", ticLL, Heating, "Setpoint B", Flat(21.0, 24));
            Register(profileReuseIndex, "IC C", ticLL, Heating, "Setpoint C", Flat(21.0, 24));
            profileReuseIndex.Resolve();

            //The name is metadata; it takes no part in identity.
            Assert.That(profileReuseIndex.DefinitionCount, Is.EqualTo(1));
            Assert.That(profileReuseIndex.Profiles.Count, Is.EqualTo(1));

            string name = profileReuseIndex.GetProfileName("IC A", ticLL);
            Assert.That(profileReuseIndex.GetProfileName("IC B", ticLL), Is.EqualTo(name));
            Assert.That(profileReuseIndex.GetProfileName("IC C", ticLL), Is.EqualTo(name));
        }

        [Test]
        public void Equality_OneBitApart_AreTwoDefinitions()
        {
            double value = 0.1;
            double neighbour = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + 1);

            Assert.That(neighbour, Is.Not.EqualTo(value), "The neighbouring bit pattern must be a different double for this test to mean anything.");

            ProfileDefinition a = Definition(Lighting, value);
            ProfileDefinition b = Definition(Lighting, neighbour);

            //No tolerance: both sides come from the same TAS read, so a tolerance could only merge two
            //profiles the model states as different.
            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(TasQuery.ProfileSignature(a), Is.Not.EqualTo(TasQuery.ProfileSignature(b)));
        }

        [Test]
        public void Equality_NegativeZero_IsZero()
        {
            ProfileDefinition a = Definition(Lighting, 0.0, 1.0);
            ProfileDefinition b = Definition(Lighting, -0.0, 1.0);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()), "Equal definitions must hash alike or the dictionary contract is broken.");
            Assert.That(TasQuery.ProfileSignature(a), Is.EqualTo(TasQuery.ProfileSignature(b)));

            //And the stored value is the positive zero, not the negative one it came in as.
            Assert.That(BitConverter.DoubleToInt64Bits(b.Values[0]), Is.EqualTo(BitConverter.DoubleToInt64Bits(0.0)));

            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "IC A", ticLG, Lighting, "OFF", new[] { 0.0, 1.0 });
            Register(profileReuseIndex, "IC B", ticLG, Lighting, "OFF", new[] { -0.0, 1.0 });
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.DefinitionCount, Is.EqualTo(1));
        }

        [Test]
        public void Equality_NaN_IsDeterministic()
        {
            //Two different NaN payloads. Under raw IEEE-754 neither equals itself, let alone the other.
            double nan_A = double.NaN;
            double nan_B = BitConverter.Int64BitsToDouble(0x7FF8000000000001L);

            Assume.That(double.IsNaN(nan_B));
            Assume.That(BitConverter.DoubleToInt64Bits(nan_A), Is.Not.EqualTo(BitConverter.DoubleToInt64Bits(nan_B)));

            ProfileDefinition a = Definition(Lighting, nan_A, 1.0);
            ProfileDefinition b = Definition(Lighting, nan_B, 1.0);

            Assert.That(a, Is.EqualTo(a), "A definition carrying a NaN must at least equal itself.");
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(TasQuery.ProfileSignature(a), Is.EqualTo(TasQuery.ProfileSignature(b)));

            //Deterministic across repeated construction, which is what a persisted name discriminator needs.
            Assert.That(TasQuery.ProfileSignatureHash(Definition(Lighting, double.NaN)), Is.EqualTo(TasQuery.ProfileSignatureHash(Definition(Lighting, double.NaN))));
        }

        [Test]
        public void Equality_ValueCountParticipatesInIdentity()
        {
            //A one-value profile and a 24-value profile of the same number are different shapes, and TAS
            //writes them back as different profile types.
            ProfileDefinition one = Definition(Infiltration, 1.0);
            ProfileDefinition twentyFour = Definition(Infiltration, Flat(1.0, 24));

            Assert.That(one, Is.Not.EqualTo(twentyFour));
            Assert.That(one.Count, Is.EqualTo(1));
            Assert.That(twentyFour.Count, Is.EqualTo(24));

            //And a shorter prefix of the same values is a different definition too.
            Assert.That(Definition(Infiltration, Flat(1.0, 23)), Is.Not.EqualTo(twentyFour));

            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "IC A", ticI, Infiltration, "Constant", new[] { 1.0 });
            Register(profileReuseIndex, "IC B", ticI, Infiltration, "Constant", Flat(1.0, 24));
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.DefinitionCount, Is.EqualTo(2));
        }

        [Test]
        public void Equality_ZeroLengthDefinition_IsNotReusable()
        {
            //A TAS function profile reads back with no values at all, so its flattened form is an incomplete
            //representation of it - merging by that would be unsafe. It keeps today's per-internal-condition
            //import instead.
            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();

            Assert.That(Register(profileReuseIndex, "IC A", ticLG, Lighting, "Daylight", new double[0]), Is.False);
            Assert.That(Register(profileReuseIndex, "IC B", ticLG, Lighting, "Daylight", new double[0]), Is.False);
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.DefinitionCount, Is.Zero, "A zero-length profile is never a shared definition.");

            //Both still reach the library, under exactly the names today's import gives them.
            Assert.That(profileReuseIndex.GetProfileName("IC A", ticLG), Is.EqualTo("IC A [Daylight]"));
            Assert.That(profileReuseIndex.GetProfileName("IC B", ticLG), Is.EqualTo("IC B [Daylight]"));

            ProfileLibrary profileLibrary = Library(profileReuseIndex);
            Assert.That(profileLibrary.GetProfiles().Count, Is.EqualTo(2));
            Assert.That(profileLibrary.GetProfile("IC A [Daylight]", ProfileType.Lighting), Is.Not.Null);
            Assert.That(profileLibrary.GetProfile("IC B [Daylight]", ProfileType.Lighting), Is.Not.Null);
        }

        // =================================================================================================
        // 9-16. Deterministic naming
        // =================================================================================================

        [Test]
        public void Naming_SameNameSameDefinition_KeepsTheSourceName()
        {
            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "Cell 1", ticLL, Heating, "HTG_7to19_21", Flat(21.0, 24));
            Register(profileReuseIndex, "Cell 2", ticLL, Heating, "HTG_7to19_21", Flat(21.0, 24));
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.Profiles.Count, Is.EqualTo(1));

            //The underscores in a real TAS setpoint name survive: the name base normalises whitespace and
            //control characters only.
            Assert.That(profileReuseIndex.Profiles[0].Name, Is.EqualTo("HTG_7to19_21"));
        }

        [Test]
        public void Naming_DifferentNamesSameDefinition_TakesTheOrdinalSmallest()
        {
            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "IC A", ticOSG, Occupancy, "Occupancy Sensible Gain", new[] { 0.0 });
            Register(profileReuseIndex, "IC A", ticOLG, Occupancy, "Occupancy Latent Gain", new[] { 0.0 });
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.Profiles.Count, Is.EqualTo(1));
            Assert.That(profileReuseIndex.Profiles[0].Name, Is.EqualTo("Occupancy Latent Gain"));
        }

        [Test]
        public void Naming_SameNameDifferentDefinitions_DiscriminatesDeterministically()
        {
            ProfileDefinition one = Definition(Infiltration, 0.2);
            ProfileDefinition twentyFour = Definition(Infiltration, Flat(1.0, 24));

            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "Cell 1", ticI, Infiltration, "Constant", Flat(1.0, 24));
            Register(profileReuseIndex, "Cell 1 - HDD", ticI, Infiltration, "Constant", new[] { 0.2 });
            profileReuseIndex.Resolve();

            List<Profile> profiles = profileReuseIndex.Profiles;
            Assert.That(profiles.Count, Is.EqualTo(2), "Two definitions wanting one name must stay two definitions.");

            List<string> names = profiles.ConvertAll(x => x.Name);
            Assert.That(names.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(2), "Neither definition may be dropped or overwritten.");

            //Claim order is ProfileDefinition.CompareTo - category, then value count, then value bits - so the
            //one-value definition claims the bare name and the 24-value one is discriminated. Never the other
            //way round, whatever order the building was walked in.
            Assert.That(profileReuseIndex.GetProfileName(Infiltration, new[] { 0.2 }), Is.EqualTo("Constant"));
            Assert.That(profileReuseIndex.GetProfileName(Infiltration, Flat(1.0, 24)), Is.EqualTo("Constant_" + TasQuery.ProfileSignatureHash(twentyFour)));

            Assert.That(TasQuery.ProfileSignatureHash(one), Is.Not.EqualTo(TasQuery.ProfileSignatureHash(twentyFour)));
        }

        [Test]
        public void Naming_FirstDiscriminator_IsTheSignatureHash()
        {
            ProfileDefinition profileDefinition = Definition(Heating, Flat(21.0, 24));

            string hash = TasQuery.ProfileSignatureHash(profileDefinition);
            Assert.That(hash, Is.Not.Null.And.Length.EqualTo(8));
            Assert.That(hash, Is.EqualTo(TasQuery.ProfileSignatureHash(Definition(Heating, Flat(21.0, 24)))), "The discriminator must be stable across repeated construction.");

            string name = TasQuery.ProfileName(new HashSet<string>(StringComparer.Ordinal) { "Setpoint" }, profileDefinition, "Setpoint");
            Assert.That(name, Is.EqualTo("Setpoint_" + hash));
        }

        [Test]
        public void Naming_CollisionAfterTheFirstDiscriminator_ExtendsDeterministically()
        {
            ProfileDefinition profileDefinition = Definition(Heating, Flat(21.0, 24));
            string hash = TasQuery.ProfileSignatureHash(profileDefinition);

            //Both the base and its signature-qualified form are already taken - reachable when the bounded
            //fingerprint collides, or when another definition's own preferred name is that exact string.
            HashSet<string> claimed = new HashSet<string>(StringComparer.Ordinal) { "Setpoint", "Setpoint_" + hash };

            string name = TasQuery.ProfileName(claimed, profileDefinition, "Setpoint");
            Assert.That(name, Is.EqualTo("Setpoint_" + hash + "_2"), "The extension must be deterministic, not a refusal and not an overwrite.");

            claimed.Add(name);
            Assert.That(TasQuery.ProfileName(claimed, profileDefinition, "Setpoint"), Is.EqualTo("Setpoint_" + hash + "_3"));

            //Repeating the same question against the same claim set gives the same answer.
            Assert.That(TasQuery.ProfileName(claimed, profileDefinition, "Setpoint"), Is.EqualTo("Setpoint_" + hash + "_3"));
        }

        [Test]
        public void Naming_ReversedRegistrationOrder_GivesIdenticalNames()
        {
            List<Slot> slots = Slots_ModelA();

            Dictionary<string, string> forward = Names(Index(slots));

            List<Slot> reversed = new List<Slot>(slots);
            reversed.Reverse();
            Dictionary<string, string> backward = Names(Index(reversed));

            Assert.That(backward, Is.EqualTo(forward));

            //And an order that interleaves the internal conditions rather than merely reversing them.
            List<Slot> interleaved = slots.OrderBy(x => x.Index).ThenBy(x => x.InternalConditionName, StringComparer.Ordinal).ToList();
            Assert.That(Names(Index(interleaved)), Is.EqualTo(forward));
        }

        [Test]
        public void Naming_RepeatedBuild_GivesIdenticalNames()
        {
            Dictionary<string, string> first = Names(Index(Slots_ModelA()));
            Dictionary<string, string> second = Names(Index(Slots_ModelA()));

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Naming_OrderingIsOrdinal_NotCultureAware()
        {
            //"Z" sorts BEFORE "a" ordinally (0x5A < 0x61) and AFTER it under every common culture. Pinning the
            //ordinal answer is what stops a name persisted into a model depending on the machine's locale.
            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "IC A", ticLL, Heating, "a schedule", Flat(21.0, 24));
            Register(profileReuseIndex, "IC B", ticLL, Heating, "Z schedule", Flat(21.0, 24));
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.Profiles.Count, Is.EqualTo(1));
            Assert.That(profileReuseIndex.Profiles[0].Name, Is.EqualTo("Z schedule"));

            //Case is significant too - two names differing only in case are two candidates, not one.
            ProfileReuseIndex profileReuseIndex_Case = new ProfileReuseIndex();
            Register(profileReuseIndex_Case, "IC A", ticLL, Heating, "setpoint", Flat(21.0, 24));
            Register(profileReuseIndex_Case, "IC B", ticLL, Heating, "SETPOINT", Flat(21.0, 24));
            profileReuseIndex_Case.Resolve();

            Assert.That(profileReuseIndex_Case.Profiles[0].Name, Is.EqualTo("SETPOINT"));
        }

        [Test]
        public void Naming_NameBase_NormalisesWhitespaceAndKeepsUnderscores()
        {
            Assert.That(TasQuery.ProfileNameBase("  HTG_7to19_21  "), Is.EqualTo("HTG_7to19_21"));
            Assert.That(TasQuery.ProfileNameBase("No   Humidification"), Is.EqualTo("No Humidification"));
            Assert.That(TasQuery.ProfileNameBase(null), Is.EqualTo(TasQuery.ProfileNameBase_Default));
            Assert.That(TasQuery.ProfileNameBase("   "), Is.EqualTo(TasQuery.ProfileNameBase_Default));
            Assert.That(TasQuery.ProfileNameBase("A\tB"), Is.EqualTo("A B"));
            Assert.That(TasQuery.ProfileNameBase(new string('x', TasQuery.ProfileNameBaseLimit + 40)).Length, Is.EqualTo(TasQuery.ProfileNameBaseLimit));

            //Two names that normalise alike are one candidate, so the definitions behind them stay separate by
            //discriminator rather than by merging.
            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "IC A", ticLL, Heating, "Set  point", Flat(21.0, 24));
            Register(profileReuseIndex, "IC B", ticLL, Heating, " Set point ", Flat(22.0, 24));
            profileReuseIndex.Resolve();

            List<string> names = profileReuseIndex.Profiles.ConvertAll(x => x.Name);
            Assert.That(names.Count, Is.EqualTo(2));
            Assert.That(names.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(2));
            Assert.That(names, Has.Member("Set point"));
        }

        // =================================================================================================
        // 17-21. Reference integrity
        // =================================================================================================

        [Test]
        public void References_EverySlotResolvesToExactlyOneLibraryDefinition()
        {
            List<Slot> slots = Slots_ModelA();
            ProfileReuseIndex profileReuseIndex = Index(slots);
            ProfileLibrary profileLibrary = Library(profileReuseIndex);

            foreach (Slot slot in slots)
            {
                string name = profileReuseIndex.GetProfileName(slot.InternalConditionName, slot.Index);
                Assert.That(name, Is.Not.Null.And.Not.Empty, "Slot " + slot.InternalConditionName + "/" + slot.Index + " resolved to nothing.");

                List<Profile> matches = profileLibrary.GetProfiles().FindAll(x => string.Equals(x.Name, name, StringComparison.Ordinal) && string.Equals(x.Category, slot.Category, StringComparison.Ordinal));
                Assert.That(matches.Count, Is.EqualTo(1), "Slot " + slot.InternalConditionName + "/" + slot.Index + " must name exactly one library definition, found " + matches.Count + ".");

                //18. the resolved category is the slot's category...
                Assert.That(matches[0].Category, Is.EqualTo(slot.Category));

                //19. ...and the resolved values are the slot's complete values, not a truncation or a summary.
                Assert.That(matches[0].GetValues(), Is.EqualTo(slot.ProfileValues));
            }
        }

        [Test]
        public void References_ResolveThroughTheSameLookupSAMItselfUses()
        {
            //The export reads a reference back with InternalCondition.GetProfile(profileType, profileLibrary),
            //which filters by ProfileType rather than by the raw category string. A name that resolves by
            //category but not by profile type would still be dangling at export time.
            List<Slot> slots = Slots_ModelA();
            ProfileReuseIndex profileReuseIndex = Index(slots);
            ProfileLibrary profileLibrary = Library(profileReuseIndex);

            foreach (Slot slot in slots)
            {
                string name = profileReuseIndex.GetProfileName(slot.InternalConditionName, slot.Index);

                InternalCondition internalCondition = new InternalCondition(slot.InternalConditionName);
                ProfileType profileType = Core.Query.Enum<ProfileType>(slot.Category);
                Assume.That(profileType, Is.Not.EqualTo(ProfileType.Undefined));

                internalCondition.SetProfileName(profileType, name);

                Profile profile = internalCondition.GetProfile(profileType, profileLibrary, false);
                Assert.That(profile, Is.Not.Null, "Reference '" + name + "' did not resolve as a " + profileType + " profile.");
                Assert.That(profile.GetValues(), Is.EqualTo(slot.ProfileValues));
            }
        }

        [Test]
        public void References_TemplateInternalConditionResolvesThroughTheSameIndex()
        {
            //The uncovered path: an internal condition that no zone owns, imported by
            //Modify.AddUnusedInternalConditions. Its slots must resolve against the SAME index the library was
            //built from, or the templates keep legacy references the library no longer carries.
            List<Slot> slots = new List<Slot>();
            slots.AddRange(Slots_Cell("Cell 1"));

            const string template = "Unassigned Activity";
            slots.AddRange(Slots_Cell(template));

            ProfileReuseIndex profileReuseIndex = Index(slots);
            ProfileLibrary profileLibrary = Library(profileReuseIndex);

            //The template carries the same activity as the zone-owned condition, so it adds no definitions at all.
            Assert.That(profileReuseIndex.DefinitionCount, Is.EqualTo(Index(Slots_Cell("Cell 1")).DefinitionCount));

            foreach (Slot slot in slots.FindAll(x => x.InternalConditionName == template))
            {
                string name = profileReuseIndex.GetProfileName(template, slot.Index);
                Assert.That(name, Is.Not.Null);
                Assert.That(name, Does.Not.Contain(template), "A shared profile name may never carry an internal condition's identity.");
                Assert.That(profileLibrary.GetProfiles().FindAll(x => x.Name == name && x.Category == slot.Category).Count, Is.EqualTo(1));

                //And it is the very same entry the zone-owned condition references.
                Assert.That(profileReuseIndex.GetProfileName("Cell 1", slot.Index), Is.EqualTo(name));
            }
        }

        [Test]
        public void References_AmbiguousSlotKeyAnswersNothing_AndTheDefinitionalLookupCoversBothSides()
        {
            //A slot key is (internal condition name, slot), and a NAME is not an identity. Two TBD internal
            //conditions sharing a name and disagreeing on a slot must not make that key answer for one of them -
            //that would be a wrong reference on the other. It stops answering instead.
            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "Duplicate", ticLL, Heating, "Setpoint A", Flat(21.0, 24));
            Register(profileReuseIndex, "Duplicate", ticLL, Heating, "Setpoint B", Flat(19.0, 24));
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.DefinitionCount, Is.EqualTo(2), "Both definitions must still exist.");
            Assert.That(profileReuseIndex.GetProfileName("Duplicate", ticLL), Is.Null, "An ambiguous slot key must answer nothing rather than answer wrongly.");

            //And the definitional lookup - which the conversion falls back to - answers correctly for each.
            Assert.That(profileReuseIndex.GetProfileName(Heating, Flat(21.0, 24)), Is.EqualTo("Setpoint A"));
            Assert.That(profileReuseIndex.GetProfileName(Heating, Flat(19.0, 24)), Is.EqualTo("Setpoint B"));
        }

        [Test]
        public void References_SlotThatIsSharedOnOneConditionAndZeroLengthOnAnother_AnswersNothing()
        {
            ProfileReuseIndex profileReuseIndex = new ProfileReuseIndex();
            Register(profileReuseIndex, "Duplicate", ticLG, Lighting, "8to18", Flat(1.0, 24));
            Register(profileReuseIndex, "Duplicate", ticLG, Lighting, "Daylight", new double[0]);
            profileReuseIndex.Resolve();

            Assert.That(profileReuseIndex.GetProfileName("Duplicate", ticLG), Is.Null);

            //The shared definition resolves by definition; the zero-length one falls through to its legacy name,
            //and the library carries both entries so neither reference dangles.
            Assert.That(profileReuseIndex.GetProfileName(Lighting, Flat(1.0, 24)), Is.EqualTo("8to18"));
            Assert.That(profileReuseIndex.GetProfileName(Lighting, new double[0]), Is.Null);

            List<string> names = Library(profileReuseIndex).GetProfiles().ConvertAll(x => x.Name);
            Assert.That(names, Is.EquivalentTo(new[] { "8to18", "Duplicate [Daylight]" }));
        }

        [Test]
        public void References_AllTBDInternalConditionConversionPathsAcceptTheIndex()
        {
            //Structural, and deliberately so: the whole scheme fails silently if ONE conversion path is left
            //without the index, and the failure surfaces as a dangling reference rather than an exception.
            AssertHasProfileReuseIndexParameter(typeof(Analytical.Tas.Convert), "ToSAM", "TBD.InternalCondition");
            AssertHasProfileReuseIndexParameter(typeof(Analytical.Tas.Convert), "ToSAM", "TBD.zone");
            AssertHasProfileReuseIndexParameter(typeof(Analytical.Tas.Convert), "ToSAM", "TBD.Building");
            AssertHasProfileReuseIndexParameter(typeof(Analytical.Tas.Convert), "ToSAM_ProfileLibrary", "TBD.Building");
            AssertHasProfileReuseIndexParameter(typeof(Analytical.Tas.Modify), "AddUnusedInternalConditions", "SAM.Analytical.AdjacencyCluster");
        }

        [Test]
        public void References_VentilationSlotIsNotCollected_SoItsKnownDanglingReferenceIsUnchanged()
        {
            //A pre-existing defect this work deliberately does NOT fix: the import writes
            //InternalConditionParameter.VentilationProfileName but has never emitted the ticV profile behind it.
            //Pinning it here is what tells a future reader that a dangling ventilation reference is the baseline
            //and not a regression this change introduced.
            IEnumerable<string> slots = ProductionSlotNames("ProfileSlots_InternalGain").Concat(ProductionSlotNames("ProfileSlots_Thermostat")).ToList();

            Assert.That(slots, Has.No.Member("ticV"));
            Assert.That(slots, Is.EquivalentTo(new[] { "ticI", "ticLG", "ticOLG", "ticOSG", "ticESG", "ticELG", "ticCOG", "ticUL", "ticLL", "ticHLL", "ticHUL" }));

            //So an index built over a building never answers for a ventilation profile, and the conversion keeps
            //the legacy name it has always written.
            ProfileReuseIndex profileReuseIndex = Index(Slots_ModelA());
            Assert.That(profileReuseIndex.GetProfileName("Ventilation", Flat(1.0, 24)), Is.Null);
            Assert.That(profileReuseIndex.GetProfileName("Cell 1", ticV), Is.Null);
            Assert.That(Library(profileReuseIndex).GetProfiles().FindAll(x => x.ProfileType == ProfileType.Ventilation), Is.Empty);
        }

        // =================================================================================================
        // The ModelA-Tas regression
        // =================================================================================================

        [Test]
        public void ModelA_FortyTwoProfilesCollapseToTwenty()
        {
            List<Slot> slots = Slots_ModelA();
            Assert.That(slots.Count, Is.EqualTo(44), "Four internal conditions x eleven collected slots.");

            //What the legacy import produced: one profile per slot, named "{internal condition} [{profile}]",
            //silently collapsed by the library key "{Category}::{Name}" - which is how 44 slots became the 42
            //entries ModelA-Tas.sam holds.
            HashSet<string> legacyKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Slot slot in slots)
            {
                legacyKeys.Add(slot.Category + "::" + Legacy(slot.InternalConditionName, slot.SourceName));
            }

            Assert.That(legacyKeys.Count, Is.EqualTo(42), "ModelA-Tas.sam holds 42 profiles.");

            ProfileReuseIndex profileReuseIndex = Index(slots);
            ProfileLibrary profileLibrary = Library(profileReuseIndex);

            Assert.That(profileReuseIndex.DefinitionCount, Is.EqualTo(20), "42 profiles are 20 distinct (Category, flattened Values) definitions.");
            Assert.That(profileLibrary.GetProfiles().Count, Is.EqualTo(20));

            //Behavioural, not a hard-coded list: every library entry is a distinct definition, and every
            //definition has exactly one entry.
            List<Profile> profiles = profileLibrary.GetProfiles();
            Assert.That(profiles.Select(x => new ProfileDefinition(x.Category, x.GetValues())).Distinct().Count(), Is.EqualTo(profiles.Count));
            Assert.That(profiles.Select(x => x.Category + "::" + x.Name).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(profiles.Count));

            //And no shared name carries a zone or internal-condition identity any more.
            foreach (Profile profile in profiles)
            {
                Assert.That(profile.Name, Does.Not.Contain("Cell 1"));
                Assert.That(profile.Name, Does.Not.Contain("Cell 2"));
                Assert.That(profile.Name, Does.Not.Contain("["));
            }
        }

        [Test]
        public void ModelA_TheTwoNameCollisionsAreDiscriminated()
        {
            ProfileReuseIndex profileReuseIndex = Index(Slots_ModelA());

            //Both collisions come from a one-value HDD sizing profile and a 24-value design profile sharing a
            //source name. Neither definition is dropped and neither name is reused.
            AssertCollision(profileReuseIndex, Infiltration, "Constant", new[] { 0.20000000298023224 }, Flat(1.0, 24));
            AssertCollision(profileReuseIndex, Heating, "HTG_7to19_21", new[] { 21.0 }, Values(16, 7, 21, 12, 16, 5));

            //Every other group keeps its bare source name - the discriminator is the exception, not the rule.
            Assert.That(profileReuseIndex.GetProfileName(Lighting, Values(0, 7, 0.5, 1, 1, 10, 0.5, 1, 0, 5)), Is.EqualTo("8to18"));
            Assert.That(profileReuseIndex.GetProfileName(Cooling, Values(28, 7, 25, 12, 28, 5)), Is.EqualTo("CLG_7to19_25"));
            Assert.That(profileReuseIndex.GetProfileName(Occupancy, new[] { 0.0 }), Is.EqualTo("Occupancy Latent Gain"));
            Assert.That(profileReuseIndex.GetProfileName(Dehumidification, Flat(100.0, 24)), Is.EqualTo("No Dehumidification"));
        }

        // =================================================================================================
        // Helpers
        // =================================================================================================

        private static void AssertCollision(ProfileReuseIndex profileReuseIndex, string category, string sourceName, double[] values_One, double[] values_Many)
        {
            string name_One = profileReuseIndex.GetProfileName(category, values_One);
            string name_Many = profileReuseIndex.GetProfileName(category, values_Many);

            Assert.That(name_One, Is.Not.Null);
            Assert.That(name_Many, Is.Not.Null);
            Assert.That(name_One, Is.Not.EqualTo(name_Many), category + "::" + sourceName + " is two definitions and must be two names.");

            Assert.That(name_One, Is.EqualTo(sourceName), "Claim order is by value count, so the one-value definition keeps the bare name.");
            Assert.That(name_Many, Is.EqualTo(sourceName + "_" + TasQuery.ProfileSignatureHash(new ProfileDefinition(category, values_Many))));
        }

        /// <summary>Every slot's resolved name, keyed by internal condition and slot - the whole naming outcome.</summary>
        private static Dictionary<string, string> Names(ProfileReuseIndex profileReuseIndex)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Slot slot in Slots_ModelA())
            {
                result[slot.InternalConditionName + "/" + slot.Index] = profileReuseIndex.GetProfileName(slot.InternalConditionName, slot.Index);
            }

            return result;
        }

        /// <summary>
        /// The <c>TBD.Profiles</c> members of one of <c>Query</c>'s production slot tables, by name. Read
        /// reflectively so this project still needs no Interop.TBD reference of its own.
        /// </summary>
        private static List<string> ProductionSlotNames(string fieldName)
        {
            FieldInfo fieldInfo = typeof(Analytical.Tas.Query).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(fieldInfo, Is.Not.Null, "Query." + fieldName + " no longer exists - the slot table this test pins has moved or been renamed.");

            List<string> result = new List<string>();
            foreach (object entry in (IEnumerable)fieldInfo.GetValue(null))
            {
                result.Add(entry.GetType().GetProperty("Key").GetValue(entry).ToString());
            }

            return result;
        }

        private static void AssertHasProfileReuseIndexParameter(Type type, string methodName, string firstParameterTypeName)
        {
            List<MethodInfo> methodInfos = type
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(x => x.Name == methodName)
                .Where(x => x.GetParameters().Length != 0 && x.GetParameters()[0].ParameterType.FullName == firstParameterTypeName)
                .ToList();

            Assert.That(methodInfos, Is.Not.Empty, type.Name + "." + methodName + "(" + firstParameterTypeName + ", ...) no longer exists.");
            Assert.That(
                methodInfos.Any(x => x.GetParameters().Any(y => y.ParameterType == typeof(ProfileReuseIndex))),
                Is.True,
                type.Name + "." + methodName + "(" + firstParameterTypeName + ", ...) takes no ProfileReuseIndex, so that conversion path cannot share the model's profile definitions.");
        }
    }
}
