using TaiwuModdingLib.Core.Plugin;
using HarmonyLib;
using GameData.Domains.SpecialEffect;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using GameData.Domains;
using System.Reflection;
using UICommon.Character;
using CharacterDataMonitor;
using Config;
using System.IO;
using UICommon.Character.Elements;
using System.Threading;
using TMPro;
using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using GameData.Utilities;

namespace EffectInfo
{
    /*
	public class ModMono : MonoBehaviour
	{
		// Token: 0x06000011 RID: 17 RVA: 0x000025F3 File Offset: 0x000007F3
		private void Awake()
		{
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}

		// Token: 0x06000012 RID: 18 RVA: 0x0000261A File Offset: 0x0000081A
		private void OnDestroy()
		{
		}

		// Token: 0x06000013 RID: 19 RVA: 0x00002638 File Offset: 0x00000838
		public void Update()
        {
            List<RaycastResult> _raycastResults = new List<RaycastResult>();
            GameObject _currMouseOverObj = null;
            PointerEventData _pointerEventData = new PointerEventData(EventSystem.current);
            if (Input.GetKeyDown(KeyCode.A))
            {
                Vector2 screenMousePos = UIManager.Instance.UiCamera.ScreenToViewportPoint(Input.mousePosition);
                GameObject hitObj = null;
                if (screenMousePos.x >= 0f && screenMousePos.x <= 1f && screenMousePos.y >= 0f && screenMousePos.y <= 1f)
                {
                    _pointerEventData.position = Input.mousePosition;
                    EventSystem.current.RaycastAll(_pointerEventData, _raycastResults);
                    if (_raycastResults.Count > 0)
                    {
                        hitObj = _raycastResults[0].gameObject;
                    }
                }
                bool mouseTipDisplayerActive = hitObj != null && hitObj.GetComponent<MouseTipDisplayer>() != null && hitObj.GetComponent<MouseTipDisplayer>().enabled;
                if (hitObj != _currMouseOverObj)
                {
                    UnityEngine.Debug.Log($"YYY:{hitObj.name} {hitObj.GetType().Name} {mouseTipDisplayerActive} ");
                    _currMouseOverObj = hitObj;
                }
            }
            //Input.GetKeyDown(this.main.key_switchAutoMove);
        }


		// Token: 0x04000019 RID: 25
		private int pressKeyCounterInc = 0;

		// Token: 0x0400001A RID: 26
		private int pressKeyCounterDec = 0;
	}*/
	public partial class EffectInfoFrontend
    {
        public static readonly ushort MY_MAGIC_NUMBER_GetResourceOutput = 6723;
        public static readonly string PATH_GetResourceOutput = "\\Mod\\EffectInfo\\Plugins\\Cache_BuildingResource.txt";
        public static bool added = false;

        //创建mouseTip并更新信息
        //在MouseTipManager中持续监视最上方的GameObject,如果这个GameObject下挂了MouseTipDisplayer类型的Component就会显示mouseTip
        [HarmonyPrefix, HarmonyPatch(typeof(UI_BuildingManage),
                  "SetResourceInfo")]
        public static void SetResourceInfoPrePatch(UI_BuildingManage __instance)
        {

            if (!On)
                return;
            var _shopInfoPage = GetPrivateField<Refers>(__instance, "_shopInfoPage");
            if (!_shopInfoPage)
                return;
            //整个资源产出附近最上方的控件都是这个ResourceOutput
            GameObject gameobject = _shopInfoPage.CGet<GameObject>("ResourceOutput");
            if (!gameobject)
                return;
            var mouseTipDisplayer=gameobject.GetComponent<MouseTipDisplayer>();
            if(mouseTipDisplayer is null)
            {
                mouseTipDisplayer = gameobject.AddComponent<MouseTipDisplayer>();
                mouseTipDisplayer.IsLanguageKey = false;
                mouseTipDisplayer.enabled = true;
                mouseTipDisplayer.Type = TipType.Simple;
                mouseTipDisplayer.PresetParam = new string[2]
                {
                        "资源产出",
                        ""
                };
            }
            __instance.AsynchMethodCall(9, 6723, __instance.GetCurrentBuildingBlockKey(), delegate (int offset, RawDataPool dataPool)
            {
                //跟CharacterAttribute不一样，这个是响应回调，不需要检查更新时间
                //前端在根目录，后端在backend
                var path = $"{Directory.GetCurrentDirectory()}{PATH_GetResourceOutput}";
                //UnityEngine.Debug.Log(path);
                var text = "";
                try
                {
                    if (File.Exists(path))
                        text = File.ReadAllText(path);
                }
                catch (IOException)
                {
                }
                mouseTipDisplayer.PresetParam[1] = text;
                UnityEngine.Debug.Log("Effect Info:Refresh Building resource output.");
            });

        }
    }
}
