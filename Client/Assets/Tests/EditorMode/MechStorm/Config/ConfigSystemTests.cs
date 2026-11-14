using System;
using System.Collections.Generic;
using System.IO;
using GameLogic;
using NUnit.Framework;
using UnityEngine;

namespace MechStorm.Config.Tests
{
    public sealed class ConfigSystemTests
    {
        private static byte[] ReadFixture(string name) =>
            File.ReadAllBytes(Path.Combine(Application.dataPath, "AssetRaw/Configs/bytes", name + ".bytes"));

        [Test]
        public void Create_DoesNotReadAnyTable()
        {
            int reads = 0;
            var tables = ConfigSystem.CreateTables(name => { reads++; return ReadFixture(name); });
            Assert.IsNotNull(tables);
            Assert.AreEqual(0, reads);
        }

        [Test]
        public void Access_ReadsOnlyRequestedTableAndCachesIt()
        {
            var names = new List<string>();
            var tables = ConfigSystem.CreateTables(name => { names.Add(name); return ReadFixture(name); });
            var first = tables.TbScenario;
            Assert.AreSame(first, tables.TbScenario);
            CollectionAssert.AreEqual(new[] { "battle_tbscenario" }, names);
            Assert.AreEqual(15, first.Get(1).BoardWidth);
        }

        [Test]
        public void Access_LoadsReferencesOnDemandWithoutReadingUnrelatedTables()
        {
            var names = new List<string>();
            var tables = ConfigSystem.CreateTables(name => { names.Add(name); return ReadFixture(name); });
            var mech = tables.TbMech.Get(1);
            CollectionAssert.AreEqual(new[] { "mech_tbmech", "mech_tbmechclass" }, names);
            Assert.AreSame(tables.TbMechClass.Get(1), mech.ClassId_Ref);
            Assert.AreEqual(2, names.Count);
        }

        [Test]
        public void Access_BattleBuilderLoadsAllRequiredTablesAndResolvesReferences()
        {
            var names = new HashSet<string>();
            var tables = ConfigSystem.CreateTables(name =>
            {
                Assert.IsTrue(names.Add(name), "重复读取：" + name);
                return ReadFixture(name);
            });
            Assert.AreEqual(4, new LubanBattleScenarioBuilder().Build(tables, 1).Units.Count);
            Assert.AreEqual(14, names.Count);
            Assert.AreEqual(11, tables.TbSkill.DataList.Count);
        }

        [TestCase(null)]
        [TestCase(0)]
        public void Access_MissingOrEmptyBytesFailsAndDoesNotRetry(int? size)
        {
            int reads = 0;
            var tables = ConfigSystem.CreateTables(name => { reads++; return size.HasValue ? new byte[size.Value] : null; });
            Assert.Throws<InvalidDataException>(() => _ = tables.TbScenario);
            Assert.Throws<InvalidOperationException>(() => _ = tables.TbScenario);
            Assert.AreEqual(1, reads);
        }

        [Test]
        public void Access_TruncatedDataFails()
        {
            var tables = ConfigSystem.CreateTables(name => new byte[] { 1 });
            Assert.Throws<Luban.SerializationException>(() => _ = tables.TbScenario);
            Assert.Throws<InvalidOperationException>(() => _ = tables.TbScenario);
        }

        [Test]
        public void Access_TrailingDataFails()
        {
            var tables = ConfigSystem.CreateTables(name =>
            {
                var bytes = ReadFixture(name);
                Array.Resize(ref bytes, bytes.Length + 1);
                return bytes;
            });
            StringAssert.Contains("battle_tbscenario", Assert.Throws<InvalidDataException>(() => _ = tables.TbScenario).Message);
        }

        [Test]
        public void Access_ReferenceLoadFailureInvalidatesPreviouslyCachedTables()
        {
            var tables = ConfigSystem.CreateTables(name => name == "mech_tbmechclass" ? null : ReadFixture(name));
            _ = tables.TbScenario;
            Assert.Throws<InvalidDataException>(() => _ = tables.TbMech);
            Assert.Throws<InvalidOperationException>(() => _ = tables.TbMech);
            Assert.Throws<InvalidOperationException>(() => _ = tables.TbScenario);
        }

        [Test]
        public void Create_RepeatedContainersAreIndependent()
        {
            var first = ConfigSystem.CreateTables(ReadFixture);
            var second = ConfigSystem.CreateTables(ReadFixture);
            Assert.AreNotSame(first.TbSkill.Get(1001), second.TbSkill.Get(1001));
            Assert.AreNotSame(first.TbMech.Get(1).ClassId_Ref, second.TbMech.Get(1).ClassId_Ref);
        }
    }
}
