using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	public partial class CancleCommandItem
	{
		#region 事件

		private async partial UniTaskVoid OnClickCancelCommandBtn()
		{
			await UniTask.Yield();
		}

		#endregion
	}
}
