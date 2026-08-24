using UnityEngine;

namespace MechStorm.Presentation.Tests
{
    internal static class TempBattleScenarioTestConfigFactory
    {
        private const string ValidConfigJson =
            "{\"_boardWidth\":10,\"_boardHeight\":10,\"_units\":[" +
            "{\"_unitId\":1,\"_faction\":0,\"_initialPosition\":{\"x\":1,\"y\":1}," +
            "\"_pilot\":{\"_id\":11,\"_name\":\"Pilot A1\",\"_maxActionPoint\":8}," +
            "\"_mech\":{\"_id\":21,\"_name\":\"Mech A1\",\"_maxDurability\":100,\"_moveRange\":4," +
            "\"_basicAttack\":{\"_actionPointCost\":1,\"_damage\":20,\"_minRange\":1,\"_maxRange\":2}}," +
            "\"_abilities\":[{\"_abilityId\":101,\"_name\":\"Ability A1\",\"_actionPointCost\":3," +
            "\"_damage\":30,\"_minRange\":1,\"_maxRange\":3,\"_targetRule\":0," +
            "\"_requiresBasicWeapon\":true}]}," +
            "{\"_unitId\":2,\"_faction\":0,\"_initialPosition\":{\"x\":1,\"y\":2}," +
            "\"_pilot\":{\"_id\":12,\"_name\":\"Pilot A2\",\"_maxActionPoint\":7}," +
            "\"_mech\":{\"_id\":22,\"_name\":\"Mech A2\",\"_maxDurability\":110,\"_moveRange\":5," +
            "\"_basicAttack\":{\"_actionPointCost\":2,\"_damage\":21,\"_minRange\":2,\"_maxRange\":3}}," +
            "\"_abilities\":[{\"_abilityId\":102,\"_name\":\"Ability A2\",\"_actionPointCost\":2," +
            "\"_damage\":31,\"_minRange\":2,\"_maxRange\":4,\"_targetRule\":0," +
            "\"_requiresBasicWeapon\":true}]}," +
            "{\"_unitId\":3,\"_faction\":1,\"_initialPosition\":{\"x\":8,\"y\":7}," +
            "\"_pilot\":{\"_id\":13,\"_name\":\"Pilot B1\",\"_maxActionPoint\":6}," +
            "\"_mech\":{\"_id\":23,\"_name\":\"Mech B1\",\"_maxDurability\":120,\"_moveRange\":3," +
            "\"_basicAttack\":{\"_actionPointCost\":1,\"_damage\":22,\"_minRange\":1,\"_maxRange\":1}}," +
            "\"_abilities\":[{\"_abilityId\":103,\"_name\":\"Ability B1\",\"_actionPointCost\":4," +
            "\"_damage\":32,\"_minRange\":1,\"_maxRange\":2,\"_targetRule\":0," +
            "\"_requiresBasicWeapon\":true}]}," +
            "{\"_unitId\":4,\"_faction\":1,\"_initialPosition\":{\"x\":8,\"y\":8}," +
            "\"_pilot\":{\"_id\":14,\"_name\":\"Pilot B2\",\"_maxActionPoint\":5}," +
            "\"_mech\":{\"_id\":24,\"_name\":\"Mech B2\",\"_maxDurability\":130,\"_moveRange\":2," +
            "\"_basicAttack\":{\"_actionPointCost\":3,\"_damage\":23,\"_minRange\":3,\"_maxRange\":4}}," +
            "\"_abilities\":[{\"_abilityId\":104,\"_name\":\"Ability B2\",\"_actionPointCost\":1," +
            "\"_damage\":33,\"_minRange\":2,\"_maxRange\":5,\"_targetRule\":0," +
            "\"_requiresBasicWeapon\":true}]}]}";

        public static TempBattleScenarioConfig CreateValid()
        {
            return CreateFromJson(ValidConfigJson);
        }

        public static TempBattleScenarioConfig CreateFromJson(string json)
        {
            var config = ScriptableObject.CreateInstance<TempBattleScenarioConfig>();
            JsonUtility.FromJsonOverwrite(json, config);
            return config;
        }
    }
}
