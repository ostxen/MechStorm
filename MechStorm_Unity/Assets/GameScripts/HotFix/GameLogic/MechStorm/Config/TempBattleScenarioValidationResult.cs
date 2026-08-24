using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MechStorm.Presentation
{
    /// <summary>
    /// 保存一次临时场景配置校验发现的全部错误。
    /// </summary>
    public sealed class TempBattleScenarioValidationResult
    {
        private readonly ReadOnlyCollection<string> _errors;

        public bool IsValid => _errors.Count == 0;
        public IReadOnlyList<string> Errors => _errors;
        public string ErrorMessage => string.Join(Environment.NewLine, _errors);

        internal TempBattleScenarioValidationResult(IEnumerable<string> errors)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            _errors = errors.ToList().AsReadOnly();
        }
    }
}
