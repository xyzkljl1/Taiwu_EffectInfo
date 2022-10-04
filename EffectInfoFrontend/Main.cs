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
using UnityEngine;

namespace EffectInfo
{
    //使用Unity Engine.Debug输出日志，通过UnityExplorer插件(F7激活)可以游戏内实时查看日志
    [PluginConfig("EffectInfo", "xyzkljl1", "0.0.2.4-test")]
    public partial class EffectInfoFrontend : TaiwuRemakePlugin
    {
        //有的属性不在property里,为了省事，视作在ECharacterPropertyReferencedType后面的enum
        public enum CustomPropertyIdEnum
        {
            RecoverMainAttribute0 = ECharacterPropertyReferencedType.Count + 1,
            RecoverMainAttribute1 = ECharacterPropertyReferencedType.Count + 2,
            RecoverMainAttribute2 = ECharacterPropertyReferencedType.Count + 3,
            RecoverMainAttribute3 = ECharacterPropertyReferencedType.Count + 4,
            RecoverMainAttribute4 = ECharacterPropertyReferencedType.Count + 5,
            RecoverMainAttribute5 = ECharacterPropertyReferencedType.Count + 6,
        }
        public static bool On = false;
        public static bool IsClean=true;//临时 
        public static int InfoLevel = 3;
        public static int currentCharId=-1;
        public static bool duringSkillBreakPlateUpdate=false;
        public static DateTime lastUpdate=DateTime.MinValue;
        Harmony harmony;
        //public ModMono modMono;
        //property id 到mousetip的映射
        public static Dictionary<short, MouseTipDisplayer> mouseTipDisplayers = new Dictionary<short, MouseTipDisplayer>();
        public static Dictionary<short, string> originalText = new Dictionary<short, string>();
        public static Dictionary<string, short> MyKey2PropertyValue = new Dictionary<string,short> {
            {"__Attraction",(short)ECharacterPropertyReferencedType.Attraction },

            {"__MainAttribute0",(short)ECharacterPropertyReferencedType.Strength },
            {"__MainAttribute1",(short)ECharacterPropertyReferencedType.Dexterity },
            {"__MainAttribute2",(short)ECharacterPropertyReferencedType.Concentration },
            {"__MainAttribute3",(short)ECharacterPropertyReferencedType.Vitality },
            {"__MainAttribute4",(short)ECharacterPropertyReferencedType.Energy },
            {"__MainAttribute5",(short)ECharacterPropertyReferencedType.Intelligence },

            {"__HitValue0",(short)ECharacterPropertyReferencedType.HitRateStrength },
            {"__HitValue1",(short)ECharacterPropertyReferencedType.HitRateTechnique },
            {"__HitValue2",(short)ECharacterPropertyReferencedType.HitRateSpeed },
            {"__HitValue3",(short)ECharacterPropertyReferencedType.HitRateMind},
            {"__AvoidValue0",(short)ECharacterPropertyReferencedType.AvoidRateStrength},
            {"__AvoidValue1",(short)ECharacterPropertyReferencedType.AvoidRateTechnique },
            {"__AvoidValue2",(short)ECharacterPropertyReferencedType.AvoidRateSpeed },
            {"__AvoidValue3",(short)ECharacterPropertyReferencedType.AvoidRateMind },

            {"__RecoveryOfStance",(short)ECharacterPropertyReferencedType.RecoveryOfStance },
            {"__RecoveryOfBreath",(short)ECharacterPropertyReferencedType.RecoveryOfBreath },
            {"__Penetration0",(short)ECharacterPropertyReferencedType.PenetrateOfOuter },
            {"__Penetration1",(short)ECharacterPropertyReferencedType.PenetrateOfInner },
            {"__PenetrationResist0",(short)ECharacterPropertyReferencedType.PenetrateResistOfOuter },
            {"__PenetrationResist1",(short)ECharacterPropertyReferencedType.PenetrateResistOfInner },
            {"__MoveSpeed",(short)ECharacterPropertyReferencedType.MoveSpeed },
            {"__CastSpeed",(short)ECharacterPropertyReferencedType.CastSpeed },
            {"__RecoveryOfFlaw",(short)ECharacterPropertyReferencedType.RecoveryOfFlaw },
            {"__RecoveryOfBlockedAcupoint",(short)ECharacterPropertyReferencedType.RecoveryOfBlockedAcupoint },
            {"__WeaponSwitchSpeed",(short)ECharacterPropertyReferencedType.WeaponSwitchSpeed },
            {"__AttackSpeed",(short)ECharacterPropertyReferencedType.AttackSpeed },
            {"__InnerRatio",(short)ECharacterPropertyReferencedType.InnerRatio },
            {"__RecoveryOfQiDisorder",(short)ECharacterPropertyReferencedType.RecoveryOfQiDisorder },

            {"__RecoveryMainAttribute0",(short)CustomPropertyIdEnum.RecoverMainAttribute0 },
            {"__RecoveryMainAttribute1",(short)CustomPropertyIdEnum.RecoverMainAttribute1 },
            {"__RecoveryMainAttribute2",(short)CustomPropertyIdEnum.RecoverMainAttribute2 },
            {"__RecoveryMainAttribute3",(short)CustomPropertyIdEnum.RecoverMainAttribute3 },
            {"__RecoveryMainAttribute4",(short)CustomPropertyIdEnum.RecoverMainAttribute4 },
            {"__RecoveryMainAttribute5",(short)CustomPropertyIdEnum.RecoverMainAttribute5 },
        };
        public override void Dispose()
        {
            if (harmony != null)
                harmony.UnpatchSelf();

        }

        public override void Initialize()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(EffectInfoFrontend));
            //this.modMono = new GameObject("askldjaskldaskljd").AddComponent<ModMono>();
        }
        public override void OnModSettingUpdate()
        {
            //不需要showUseless
            ModManager.GetSetting(ModIdStr, "On", ref On);
            ModManager.GetSetting(ModIdStr, "InfoLevel", ref InfoLevel);
        }
        public static FieldType GetPrivateField<FieldType>(object instance, string field_name)
        {
            Type type = instance.GetType();
            FieldInfo field_info = type.GetField(field_name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (FieldType)field_info.GetValue(instance);
        }
        public static void SetPrivateField<FieldType>(object instance, string field_name, FieldType value)
        {
            Type type = instance.GetType();
            FieldInfo field_info = type.GetField(field_name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field_info.SetValue(instance, value);
        }
        public static void SetPublicField<FieldType>(object instance, string field_name, FieldType value)
        {
            Type type = instance.GetType();
            FieldInfo field_info = type.GetField(field_name, System.Reflection.BindingFlags.Instance);
            field_info.SetValue(instance, value);
        }
        //主属性恢复的mouseTip
        //同样会跑两遍，第二次恰好是我们需要的
        //获取原文本比较费劲，干脆放弃治疗不要了
        [HarmonyPostfix, HarmonyPatch(typeof(CharacterMainAttributeRecovery), MethodType.Constructor,new Type[] { typeof(Refers[])})]
        public static void CharacterMainAttributeRecoveryCtorPatch(CharacterMainAttributeRecovery __instance,Refers[] refersArray)
        {
            if (!On)
                return;
            for(int i=0;i<6;++i)
            {
                short propertyId = (short)(i + CustomPropertyIdEnum.RecoverMainAttribute0 - 0);
                MouseTipDisplayer mouseTipDisplayer=refersArray[i].CGet<MouseTipDisplayer>("MouseTip");
                if (mouseTipDisplayer != null && mouseTipDisplayer.PresetParam != null && mouseTipDisplayer.PresetParam.Length > 1)
                {
                    UnityEngine.Debug.Log($"EffectInfo:1记录mouseTipDisplayer {propertyId}");
                    originalText[propertyId] = "";//属性恢复的文本是动态的，此时尚未设置内容
//                    mouseTipDisplayer.PresetParam[0] = "asdsads";
                    mouseTipDisplayers[propertyId] = mouseTipDisplayer;
                }
            }
        }
        //记录SkillBreakPlate的初始化,因为此期间创建的mousetip是不需要的
        //是在UIManager.Instance.ShowUI(UIElement.SkillBreakPlate)时初始化的
        [HarmonyPrefix, HarmonyPatch(typeof(UIManager), "ShowUI")]
        public static void ShowUIPrePatch(UIElement elem)
        {
            if (!On)
                return;
            if(elem==UIElement.SkillBreakPlate)
            {
                UnityEngine.Debug.Log("EffectInfo:Enter SkillBreakPlate Init");
                duringSkillBreakPlateUpdate = true;
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(UIManager), "ShowUI")]
        public static void ShowUIPostPatch(UIElement elem)
        {
            if (!On)
                return;
            if (elem == UIElement.SkillBreakPlate)
            {
                UnityEngine.Debug.Log("EffectInfo:Leave SkillBreakPlate Init");
                duringSkillBreakPlateUpdate = false;
            }
        }

        //魅力的mouseTip
        //由于CharacterCharm控件出现在多个地方，要从上一级找
        //每次打开人物界面这个控件都会创建新的
        [HarmonyPostfix, HarmonyPatch(typeof(UI_CharacterMenuInfo), "InitCharacterUIElements")]
        public static void InitCharacterUIElementsPatch(UI_CharacterMenuInfo __instance)
        {
            if (!On)
                return;
            if (duringSkillBreakPlateUpdate)
                return;
            short propertyId = (short)ECharacterPropertyReferencedType.Attraction;
            var _uiElements=GetPrivateField<List<CharacterUIElement>>(__instance, "_uiElements");
            foreach(var _uiElement in _uiElements)
                if(_uiElement.GetType() == typeof(CharacterDetailInfo))
                {
                    var detail_info = (CharacterDetailInfo)_uiElement;
                    var character_charm=GetPrivateField<CharacterCharm>(detail_info, "_characterCharm");
                    var info_item = GetPrivateField<InfoItem>(character_charm, "_infoItem");
                    MouseTipDisplayer mouseTipDisplayer = info_item.GetMouseTip();
                    if (mouseTipDisplayer.PresetParam != null && mouseTipDisplayer.PresetParam.Length > 1)
                    {
                        UnityEngine.Debug.Log($"EffectInfo:3记录mouseTipDisplayer {propertyId}");
                        //因为原有文本太长影响观感，手动缩一下
                        //由于每次都会重新创建，不能从originalText获取
                        //这游戏更新文本从来不写公告，醉了
                        originalText[propertyId] = CharacterPropertyDisplay.Instance[propertyId].Desc.Replace("\n<SpName=mousetip_meili", "<SpName=mousetip_meili");
                        mouseTipDisplayers[propertyId] = mouseTipDisplayer;
                        //UnityEngine.Debug.Log($"{propertyId} {mouseTipDisplayer.PresetParam[0]}");
                    }
                    break;
                }
        }
        //记录AttributeItem构造时创建的mouseTip
        //初始化时会创建两个，第二个恰好是所需的
        //打开突破页面时，右侧有个和人物菜单右侧一样的控件(但是锁在伤病上)，又会触发一次，通过patch SkillBreakPlate避开
        [HarmonyPostfix, HarmonyPatch(typeof(AttributeItem), MethodType.Constructor, new Type[] { typeof(Refers), typeof(short), typeof(bool) })]
        public static void AttributeItemCtorPatch(Refers refers, short propertyId, bool bgActive)
        {
            if (!On)
                return; 
            if (duringSkillBreakPlateUpdate)
                return;

            if (!MyKey2PropertyValue.ContainsValue(propertyId))
                return;
            MouseTipDisplayer mouseTipDisplayer = refers.CGet<MouseTipDisplayer>("MouseTip");
            if (mouseTipDisplayer.PresetParam != null && mouseTipDisplayer.PresetParam.Length > 1)
            {
                UnityEngine.Debug.Log($"EffectInfo:4记录mouseTipDisplayer {propertyId}");
                originalText[propertyId] = CharacterPropertyDisplay.Instance[propertyId].Desc;
                mouseTipDisplayers[propertyId] = mouseTipDisplayer;
                //UnityEngine.Debug.Log($"{propertyId} {mouseTipDisplayer.PresetParam[0]}");
            }
        }
        //记录AttributeSlider构造时创建的mouseTip
        [HarmonyPostfix, HarmonyPatch(typeof(AttributeSlider), MethodType.Constructor, new Type[] { typeof(Refers), typeof(short), typeof(float) })]
        public static void AttributeSliderCtorPatch(Refers refers, short propertyId, float value)
        {
            if (!EffectInfoFrontend.On)
                return;
            if (duringSkillBreakPlateUpdate)
                return;
            if (!MyKey2PropertyValue.ContainsValue(propertyId))
                return;
            MouseTipDisplayer mouseTipDisplayer = refers.GetComponent<MouseTipDisplayer>();
            if (mouseTipDisplayer!=null && mouseTipDisplayer.PresetParam != null && mouseTipDisplayer.PresetParam.Length > 1)
            {
                UnityEngine.Debug.Log($"EffectInfo:5记录mouseTipDisplayer {propertyId}");
                originalText[propertyId] = CharacterPropertyDisplay.Instance[propertyId].Desc;
                mouseTipDisplayers[propertyId] = mouseTipDisplayer;
                //UnityEngine.Debug.Log($"{propertyId} {mouseTipDisplayer.PresetParam[0]}");
            }
        }

        public static void ReloadAllText()
        {
            if (!On)
                return;

            UnityEngine.Debug.Log("EffectInfo:Reload file\n");
            if (EffectInfoFrontend.currentCharId < 0)
                return;
            //property_id到文本的映射
            var property_text = new Dictionary<int, string>();
            //读取本地文件
            {
                //前端在根目录，后端在backend
                var dir = System.IO.Directory.GetCurrentDirectory();
                var path = String.Format("{0}\\Mod\\EffectInfo\\Plugins\\Cache_GetCharacterAttribute.txt", dir);
                //UnityEngine.Debug.Log(path);
                //UnityEngine.Debug.Log(File.Exists(path));
                if (!File.Exists(path))
                {
                    // File.WriteAllText(path + $"{test++}NoFile.txt", $"{DateTime.Now}" );
                    return;
                }
                var time = File.GetLastWriteTime(path);
                if (time <= lastUpdate)//文件未更新
                {
                    // File.WriteAllText(path + $"{test++}Old.txt", $"{DateTime.Now}");
                    return;
                }
                lastUpdate = time;
                try
                {
                    var lines = File.ReadAllLines(path);
                    if (lines.Length < 1)
                        throw new IOException("Invalid Effect Info Data");
                    int id = int.Parse(lines[0]);
                    if(id!=EffectInfoFrontend.currentCharId)
                        throw new IOException("Invalid Effect Info Data");
                    var tmp_text = "";
                    foreach (var line in lines)
                        if (line.StartsWith("__")) //一条属性结束
                        {
                            if (MyKey2PropertyValue.ContainsKey(line))
                                property_text[MyKey2PropertyValue[line]] = tmp_text;
                            tmp_text = "";
                        }
                        else if (line.Length > 0)
                        {
                            int level = 0;
                            if (line[0] >= '0' && line[0] <= '9')
                                level = Int32.Parse(line.Substring(0, 1));
                            //目前在后端已经处理，但是前端代码不需要改动
                            if (level <= InfoLevel)
                                tmp_text += $"{line.Substring(1)}\n";
                        }
                }
                catch (IOException e)
                {
                    property_text.Clear();//读取失败可能是文件正在被写入，视作旧信息已失效直接清空
                    return;
                }               
                // File.WriteAllText(path+$"{test++}.txt", $"{time}"+property_text[6]);
            }
            //修改mouseTip的文本
            foreach (var pair in EffectInfoFrontend.mouseTipDisplayers)
            {
                var propertyId = pair.Key;
                var mouseTipDisplayer = pair.Value;
                if (mouseTipDisplayer && mouseTipDisplayer.PresetParam != null && mouseTipDisplayer.PresetParam.Length > 1)
                    if (property_text.ContainsKey(propertyId) && originalText.ContainsKey(propertyId))
                    {
                        mouseTipDisplayer.PresetParam[1] = originalText[propertyId] + "\n" + property_text[propertyId];
                        mouseTipDisplayer.NeedRefresh = true;
                        IsClean = false;
                        //UnityEngine.Debug.Log($"ChangeEEEE{propertyId} {property_text[propertyId]}");
                    }
            }
        }
        // 打开属性界面时后端GetGroupCharDisplayDataList开始写文件，但是似乎后端完成前控件就会从其他地方(characteruielement.refresh:MonitorDataItem?)获得数据
        // 因此在显示tip时再加载
        [HarmonyPrefix, HarmonyPatch(typeof(MouseTipDisplayer),
              "ShowTips")]
        public static void ShowTipsPrePatch(MouseTipDisplayer __instance)
        {
            if (__instance.Type != TipType.Simple)
                return;
            if (!On)
                return;
            if (duringSkillBreakPlateUpdate)
                return;
            if (!mouseTipDisplayers.ContainsValue(__instance))
                return;            
            ReloadAllText();
        }
        // 每次切到属性页面都会触发SetCurrentCharacterId，此时不能保证后端写完文件
        [HarmonyPrefix, HarmonyPatch(typeof(CharacterAttributeDataView),
              "SetCurrentCharacterId")]
        public static void SetCurrentCharacterIdPrePatch(CharacterAttributeDataView __instance,int charId)
        {
            if (!On)
                return;
            EffectInfoFrontend.currentCharId = charId;
            lastUpdate = DateTime.MinValue;        
            UnityEngine.Debug.Log($"EffectInfo:切换到角色{charId}");
        }
    }
}
