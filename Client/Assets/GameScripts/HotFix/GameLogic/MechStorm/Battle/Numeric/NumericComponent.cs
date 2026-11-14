using System;
using System.Collections.Generic;

namespace MechStorm.Battle.Numeric
{
    public enum NumericType
    {
        Max = 10000,

        Speed = 1000,
        SpeedBase = Speed * 10 + 1,
        SpeedAdd = Speed * 10 + 2,
        SpeedPct = Speed * 10 + 3,
        SpeedFinalAdd = Speed * 10 + 4,
        SpeedFinalPct = Speed * 10 + 5,

        Hp = 1001,
        HpBase = Hp * 10 + 1,

        MaxHp = 1002,
        MaxHpBase = MaxHp * 10 + 1,
        MaxHpAdd = MaxHp * 10 + 2,
        MaxHpPct = MaxHp * 10 + 3,
        MaxHpFinalAdd = MaxHp * 10 + 4,
        MaxHpFinalPct = MaxHp * 10 + 5,
    }

    // TODO: 该组件暂作为后续 Attribute / Modifier 阶段的候选实现，P0 不接入主战斗流程。
    // TODO: 接入前需要补齐属性枚举规划、取整策略、数值来源追踪、Modifier 生命周期和单元测试。
    public sealed class NumericComponent
    {
        private const int RATE_SCALE = 10000;

        private readonly Dictionary<int, int> _numericValues = new Dictionary<int, int>();

        public event Action<NumericType, int> NumericChanged;

        public float GetAsFloat(NumericType numericType)
        {
            return (float)GetByKey((int)numericType) / RATE_SCALE;
        }

        public int GetAsInt(NumericType numericType)
        {
            return GetByKey((int)numericType);
        }

        public void Set(NumericType numericType, float value)
        {
            this[numericType] = (int)Math.Round(value * RATE_SCALE, MidpointRounding.AwayFromZero);
        }

        public void Set(NumericType numericType, int value)
        {
            this[numericType] = value;
        }

        public int this[NumericType numericType]
        {
            get => GetByKey((int)numericType);
            set
            {
                var key = (int)numericType;
                var oldValue = GetByKey(key);
                if (oldValue == value)
                {
                    return;
                }

                _numericValues[key] = value;
                Update(numericType);
            }
        }

        private int GetByKey(int key)
        {
            _numericValues.TryGetValue(key, out var value);
            return value;
        }

        private void Update(NumericType numericType)
        {
            if (numericType == NumericType.Max)
            {
                return;
            }

            if (numericType < NumericType.Max)
            {
                NumericChanged?.Invoke(numericType, GetByKey((int)numericType));
                return;
            }

            var finalKey = (int)numericType / 10;
            var baseKey = finalKey * 10 + 1;
            var addKey = finalKey * 10 + 2;
            var pctKey = finalKey * 10 + 3;
            var finalAddKey = finalKey * 10 + 4;
            var finalPctKey = finalKey * 10 + 5;

            long value = GetByKey(baseKey) + GetByKey(addKey);
            value = ApplyRate(value, GetByKey(pctKey));
            value += GetByKey(finalAddKey);
            value = ApplyRate(value, GetByKey(finalPctKey));

            var finalValue = checked((int)value);
            if (GetByKey(finalKey) == finalValue)
            {
                return;
            }

            _numericValues[finalKey] = finalValue;
            NumericChanged?.Invoke((NumericType)finalKey, finalValue);
        }

        private static long ApplyRate(long value, int rate)
        {
            return value * (RATE_SCALE + rate) / RATE_SCALE;
        }
    }
}