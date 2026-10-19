using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	public partial class ActionControlItem
	{
		#region 事件

		private async partial UniTaskVoid OnClickConfirmBtn()
		{
			await UniTask.Yield();
		}

		#endregion
	}
}
