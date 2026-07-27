using System;
using System.Collections.Generic;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Snapshots;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MechStorm.Battle.Diagnostics
{
    /// <summary>
    /// 将当前战场快照和完整操作日志导出为便于人工检查的版本化 JSON。
    /// </summary>
    public static class BattleDebugJsonSerializer
    {
        // Schema 5 新增 BattleOutcome、胜方、全灭方和结束原因。
        private const int CurrentSchemaVersion = 5;
        private static readonly JsonSerializerSettings SerializerSettings = CreateSerializerSettings();

        /// <summary>
        /// 导出当前战场全貌以及此前每一次成功或失败的操作。
        /// </summary>
        public static string Serialize(BattleSnapshot snapshot, IReadOnlyList<BattleActionLog> actionLogs)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (actionLogs == null)
            {
                throw new ArgumentNullException(nameof(actionLogs));
            }

            var exportData = new BattleDebugExportData(CurrentSchemaVersion, snapshot, actionLogs);
            return JsonConvert.SerializeObject(exportData, Formatting.Indented, SerializerSettings);
        }

        private static JsonSerializerSettings CreateSerializerSettings()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Include,
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }

        private sealed class BattleDebugExportData
        {
            public int SchemaVersion { get; }

            public BattleSnapshot Snapshot { get; }

            public IReadOnlyList<BattleActionLog> ActionLogs { get; }

            public BattleDebugExportData(int schemaVersion, BattleSnapshot snapshot, IReadOnlyList<BattleActionLog> actionLogs)
            {
                SchemaVersion = schemaVersion;
                Snapshot = snapshot;
                ActionLogs = actionLogs;
            }
        }
    }
}
