using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	public partial class SettingsChoiceItem
	{
		#region 事件

		private async partial UniTaskVoid OnClickPreviousBtn()
		{
			await UniTask.Yield();
		}

		private async partial UniTaskVoid OnClickNextBtn()
		{
			await UniTask.Yield();
		}

		#endregion
	}
}
