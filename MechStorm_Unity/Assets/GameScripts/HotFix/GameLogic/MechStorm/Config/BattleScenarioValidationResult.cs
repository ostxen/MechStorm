using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MechStorm.Config
{
    /// <summary>
    /// 保存一次场景配置校验发现的全部错误。
    /// </summary>
    public sealed class BattleScenarioValidationResult
    {
        private readonly ReadOnlyCollection<string> _errors;

        public bool IsValid => _errors.Count == 0;
        public IReadOnlyList<string> Errors => _errors;
        public string ErrorMessage => string.Join(Environment.NewLine, _errors);

        internal BattleScenarioValidationResult(IEnumerable<string> errors)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            _errors = errors.ToList().AsReadOnly();
        }
    }
}
