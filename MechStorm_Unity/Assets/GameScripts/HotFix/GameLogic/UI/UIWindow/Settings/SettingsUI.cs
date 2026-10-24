using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	[Window(UILayer.UI, location : "SettingsUI")]
	public partial class SettingsUI
	{
		#region 事件

		private async partial UniTaskVoid OnClickCloseBtn()
		{
			await UniTask.Yield();
		}

		private async partial UniTaskVoid OnClickGameplayBtn()
		{
			await UniTask.Yield();
		}

		private async partial UniTaskVoid OnClickGraphicsBtn()
		{
			await UniTask.Yield();
		}

		private async partial UniTaskVoid OnClickAudioBtn()
		{
			await UniTask.Yield();
		}

		private async partial UniTaskVoid OnClickControlsBtn()
		{
			await UniTask.Yield();
		}

		#endregion
	}
}
