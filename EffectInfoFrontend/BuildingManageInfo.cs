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
	public partial class EffectInfoFrontend
    {
        public static readonly ushort MY_MAGIC_NUMBER_GetResourceOutput = 6723;
        public static readonly string PATH_GetResourceOutput = "\\Mod\\EffectInfo\\Plugins\\Cache_BuildingResource.txt";

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
            __instance.AsynchMethodCall(9, MY_MAGIC_NUMBER_GetResourceOutput, __instance.GetCurrentBuildingBlockKey(), delegate (int offset, RawDataPool dataPool)
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
                mouseTipDisplayer.NeedRefresh = true;
                UnityEngine.Debug.Log("Effect Info:Refresh Building resource output.");
            });

        }
    }
}
