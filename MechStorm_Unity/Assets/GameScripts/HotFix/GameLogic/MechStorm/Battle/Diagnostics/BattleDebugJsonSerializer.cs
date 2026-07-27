using System;
using System.Collections.Generic;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Snapshots;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MechStorm.Battle.Diagnostics
{
    public static class BattleDebugJsonSerializer
    {
        private const int CurrentSchemaVersion = 5;
        private static readonly JsonSerializerSettings SerializerSettings = CreateSerializerSettings();

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
