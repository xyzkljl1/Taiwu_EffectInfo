using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Character.AvatarSystem;
using GameData.Domains.Combat;
using GameData.Domains.CombatSkill;
using GameData.Domains.Item;
using GameData.Domains.SpecialEffect;
using GameData.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TaiwuModdingLib.Core.Plugin;

namespace EffectInfo
{
    [PluginConfig("EffectInfo", "xyzkljl1", "0.0.2.4-test")]
    public partial class EffectInfoBackend : TaiwuRemakePlugin
    {
        public static bool On;
        public static bool ShowUseless;
        public static int InfoLevel = 3;
        //已经实现获取信息方法的Field
        public static HashSet<int> MonitoredFieldIds =new HashSet<int>();

        //private static ;
        //FieldId(CharacterHelper.FieldIds)->属性信息
        //注意此FieldId和函数内获取信息用的AffectedDataHelper.FieldIds的不同
        private static Dictionary<int, string> Cache_FieldText = new Dictionary<int, string>();

        //propertyType和AffectedDataHelper.FieldIds很多name不一致,不做自动映射
        // private static Dictionary<int, ushort> Property2AffectedDataFieldId;
        private static int currentCharId = -1;
        Harmony harmony;
        public override void Dispose()
        {
            if (harmony != null)
                harmony.UnpatchSelf();

        }

        public override void Initialize()
        {
            AdaptableLog.Info("EffectInfo:Init");
            harmony = Harmony.CreateAndPatchAll(typeof(EffectInfoBackend));
            MyAffectedDataFieldIds.Init();
            MyFieldIds.Init();
            MonitoredFieldIds.Add(MyFieldIds.HitValues);
            MonitoredFieldIds.Add(MyFieldIds.AvoidValues);
            MonitoredFieldIds.Add(MyFieldIds.Attraction);
            MonitoredFieldIds.Add(MyFieldIds.MaxMainAttributes);
            MonitoredFieldIds.Add(MyFieldIds.Penetrations);
            MonitoredFieldIds.Add(MyFieldIds.PenetrationResists);
            MonitoredFieldIds.Add(MyFieldIds.RecoveryOfStanceAndBreath);
            MonitoredFieldIds.Add(MyFieldIds.MoveSpeed);
            MonitoredFieldIds.Add(MyFieldIds.RecoveryOfFlaw);
            MonitoredFieldIds.Add(MyFieldIds.CastSpeed);
            MonitoredFieldIds.Add(MyFieldIds.RecoveryOfBlockedAcupoint);
            MonitoredFieldIds.Add(MyFieldIds.WeaponSwitchSpeed);
            MonitoredFieldIds.Add(MyFieldIds.AttackSpeed);
            MonitoredFieldIds.Add(MyFieldIds.InnerRatio);
            MonitoredFieldIds.Add(MyFieldIds.RecoveryOfQiDisorder);
            MonitoredFieldIds.Add(MyFieldIds.RecoveryOfMainAttribute);
        }
        public override void OnModSettingUpdate()
        {
            DomainManager.Mod.GetSetting(ModIdStr, "On", ref On);
            DomainManager.Mod.GetSetting(ModIdStr, "ShowUseless", ref ShowUseless);
            DomainManager.Mod.GetSetting(ModIdStr, "InfoLevel", ref InfoLevel);
            AdaptableLog.Info(String.Format("EffectInfoBackend:Load Setting, EffectInfoBackend {0}", On ? "开启" : "关闭"));
        }

        /*
			GetXXXInfo是基于游戏内GetXXX函数改写：
                返回(3级)数值信息字符串
				出入参dirty_tag用于标记是否有至少一项非0修正
			PackGetXXXInfo基于GetXXXInfo改写：
				调用GetXXX原函数，结果加到出入参sum上
				然后返回2、3级信息字符串，包含GetXXXInfo的结果
				返回值不在GetXXXInfo中计算而是调用GetXXX原函数，是为了当计算式更改时更容易发现
				GetXXXInfo内并不会计算校验值，因为我懒
            CustomGetXXXInfo:类似上述函数，但是不对应游戏代码中的函数，是为了复用代码打包
                返回1级信息
                由于1级信息自己判断是否需要显示，无需传入dirty_tag
                出入参check_value
		 */
        //不知道SpecialEffectBase的名字如何获得
        //不好写pack，算了
        public static ValueTuple<string, string> GetTotalPercentModifyValueInfo(ref bool dirty_tag, int charId, short combatSkillId, ushort fieldId, int customParam0 = -1, int customParam1 = -1, int customParam2 = -1)
        {
            string result1 = "";
            string result2 = "";
            var __instance = DomainManager.SpecialEffect;
            var affectedData = GetPrivateValue<Dictionary<int, GameData.Domains.SpecialEffect.AffectedData>>(__instance, "_affectedDatas");

            ValueTuple<int, int> modifyValue = new ValueTuple<int, int>(0, 0);
            if (!affectedData.ContainsKey(charId))
                return new ValueTuple<string, string>(result1, result2);

            SpecialEffectList effectList = affectedData[charId].GetEffectList(fieldId);
            if (effectList != null)
            {
                AffectedDataKey dataKey = new AffectedDataKey(charId, fieldId, combatSkillId, customParam0, customParam1, customParam2);
                for (int i = 0; i < effectList.EffectList.Count; i++)
                {
                    SpecialEffectBase effect = effectList.EffectList[i];
                    if (effect.AffectDatas.ContainsKey(dataKey) && effect.AffectDatas[dataKey] == 2)
                    {
                        int value = effect.GetModifyValue(dataKey, 0);
                        if (value > modifyValue.Item1)
                        {
                            modifyValue.Item1 = value;
                            if (value != 0)
                            {
                                dirty_tag = true;
                                result1 = ToInfoAdd($"{effect.GetType().ToString()}(最高)", value, 3);
                            }
                        }
                        else if (value < modifyValue.Item2)
                        {
                            modifyValue.Item2 = value;
                            if (value != 0)
                            {
                                result2 = ToInfoAdd($"{effect.GetType().ToString()}(最低)", value, 3);
                                dirty_tag = true;
                            }
                        }
                    }
                }
            }
            return new ValueTuple<string, string>(result1, result2);
        }
        //获得装备信息
        private static string PackGetPropertyBonusOfEquipmentsInfo(ref int sum, ref bool dirty_tag, GameData.Domains.Character.Character character, ECharacterPropertyReferencedType propertyType, sbyte valueSumType = 0)
        {
            var value = CallPrivateMethod<int>(character, "GetPropertyBonusOfEquipments", new object[] { propertyType, valueSumType });
            sum += value;
            var tmp = ToInfoAdd("装备" + valueSumType2Text(valueSumType), value, 2);
            tmp += GetPropertyBonusOfEquipmentsInfo(ref dirty_tag, character, propertyType, valueSumType);
            return tmp;
        }
        private static string GetPropertyBonusOfEquipmentsInfo(ref bool dirty_tag, GameData.Domains.Character.Character character, ECharacterPropertyReferencedType propertyType, sbyte valueSumType = 0)
        {
            string result = "";
            foreach (ItemKey itemKey in character.GetEquipment())
                if (itemKey.IsValid())
                {
                    int value = DomainManager.Item.GetCharacterPropertyBonus(itemKey, propertyType);
                    if (valueSumType == 0 || valueSumType == 1 == value > 0)
                        if (value != 0)
                        {
                            string name = GetEquipmentName(itemKey);
                            result += ToInfoAdd(name, value, 3);
                            dirty_tag = true;
                        }
                }
            return result;
        }
        //特性
        private static string PackGetPropertyBonusOfFeaturesInfo(ref int sum,ref bool dirty_tag, Character character, ECharacterPropertyReferencedType propertyType, sbyte valueSumType = 0)
        {
            var value = CallPrivateMethod<int>(character, "GetPropertyBonusOfFeatures", new object[] { propertyType, valueSumType });
            sum +=value;
            var tmp = "";
            tmp += ToInfoAdd("特性"+valueSumType2Text(valueSumType), value, 2);
            tmp += GetPropertyBonusOfFeaturesInfo(ref dirty_tag, character, propertyType, valueSumType);
            return tmp;
        }
        private static string GetPropertyBonusOfFeaturesInfo(ref bool dirty_tag, Character character, ECharacterPropertyReferencedType propertyType, sbyte valueSumType = 0)
        {
            string result = "";
            foreach (var featureId in character.GetFeatureIds())
            {
                int value = Config.CharacterFeature.GetCharacterPropertyBonus(featureId, propertyType);
                if (valueSumType == 0 || valueSumType == 1 == value > 0)
                    if (value != 0)
                    {
                        result += ToInfoAdd(GetFeatureName(featureId), value, 3);
                        dirty_tag = true;
                    }
            }
            return result;
        }

        private static string PackGetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref int sum, ref bool dirty_tag, GameData.Domains.Character.Character character, ECharacterPropertyReferencedType propertyType, sbyte valueSumType = 0)
        {
            var value = (short)CallPrivateMethod<int>(character, "GetPropertyBonusOfCombatSkillEquippingAndBreakout", new object[] { propertyType, valueSumType });
            sum += value;

            var tmp = "";
            tmp += ToInfoAdd("功法"+ valueSumType2Text(valueSumType), value, 2);
            tmp += GetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref dirty_tag, character, propertyType, valueSumType);
            return tmp;
        }
        //获得combatSkill的信息
        private static string GetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref bool dirty_tag, GameData.Domains.Character.Character character, ECharacterPropertyReferencedType propertyType, sbyte valueSumType = 0)
        {
            string result = "";
            int id = ((character.GetSrcCharId() >= 0) ? character.GetSrcCharId() : character.GetId());
            Dictionary<short, GameData.Domains.CombatSkill.CombatSkill> charCombatSkills = DomainManager.CombatSkill.GetCharCombatSkills(id);
            foreach (short skillTemplateId in character.GetEquippedCombatSkills())
                if (skillTemplateId >= 0)
                    if (charCombatSkills.ContainsKey(skillTemplateId))
                    {
                        var combat_skill = charCombatSkills[skillTemplateId];
                        var value = combat_skill.GetCharPropertyBonus((short)propertyType, valueSumType);
                        var template_id = combat_skill.GetId().SkillTemplateId;
                        var cb_template = Config.CombatSkill.Instance[template_id];
                        var name = cb_template.Name;
                        if (value != 0)
                        {
                            result += ToInfoAdd(name, value, 3);
                            dirty_tag = true;
                        }
                    }
            return result;
        }
        public static string PackGetModifyValueInfo(ref int sum, ref bool dirty_tag, int charId, ushort fieldId, sbyte modifyType, int customParam0 = -1, int customParam1 = -1, int customParam2 = -1, sbyte valueSumType = 0)
        {
            var value = (short)DomainManager.SpecialEffect.GetModifyValue(charId, fieldId, (sbyte)modifyType, customParam0, customParam1, customParam2, valueSumType);
            sum += value;
            var tmp = "";
            //modifyType=1时获得的是乘算加值，在乘算项目下算作加/减值，所以还是显示为加/减值
            tmp += ToInfoAdd("效果" + valueSumType2Text(valueSumType), value, 2);
            tmp += GetModifyValueInfo(ref dirty_tag, charId, fieldId, valueSumType);
            return tmp;
        }
        //不知道SpecialEffectBase的名字如何获得
        public static string GetModifyValueInfo(ref bool dirty_tag, int charId, ushort fieldId, sbyte modifyType, int customParam0 = -1, int customParam1 = -1, int customParam2 = -1, sbyte valueSumType = 0)
        {
            return GetModifyValueInfo(ref dirty_tag, charId, -1, fieldId, modifyType, customParam0, customParam1, customParam2, valueSumType);
        }
        //不知道SpecialEffectBase的名字如何获得
        //DomainManager.SpecialEffect.GetModifyValue
        public static string GetModifyValueInfo(ref bool dirty_tag, int charId, short combatSkillId, ushort fieldId, sbyte modifyType, int customParam0 = -1, int customParam1 = -1, int customParam2 = -1, sbyte valueSumType = 0)
        {
            string result = "";
            var __instance = DomainManager.SpecialEffect;
            var affectedData = GetPrivateValue<Dictionary<int, GameData.Domains.SpecialEffect.AffectedData>>(__instance, "_affectedDatas");
            if (modifyType != 0 && modifyType != 1)
                return result;
            if (!affectedData.ContainsKey(charId))
                return result;
            SpecialEffectList effectList = affectedData[charId].GetEffectList(fieldId);
            int modifyValue = 0;
            if (effectList != null)
            {
                AffectedDataKey dataKey = new AffectedDataKey(charId, fieldId, combatSkillId, customParam0, customParam1, customParam2);
                for (int i = 0; i < effectList.EffectList.Count; i++)
                {
                    SpecialEffectBase effect = effectList.EffectList[i];
                    if (effect.AffectDatas.ContainsKey(dataKey) && modifyType == effect.AffectDatas[dataKey])
                    {
                        int value = effect.GetModifyValue(dataKey, modifyValue);//加值和当前值有关，所以必须计算总加值
                        if (valueSumType == 0 || valueSumType == 1 == value > 0)
                            if (value != 0)
                            {
                                modifyValue += value;
                                string name = "";
                                //有combatSkillId=-1和不为-1两种，如果为-1则无关combatSkill,effect的名字不知道如何获得
                                if (combatSkillId >= 0)
                                    name = GetCombatSkillName(combatSkillId);
                                else
                                    name = effect.GetType().ToString();
                                dirty_tag = true;
                                result += ToInfoAdd(name, value, 3);
                            }
                    }
                }
            }
            return result;
        }
        //获得食物信息
        //食物是从FoodItem的模板获取加值，而非调用ItemBase
        public unsafe static string PackGetCharacterPropertyBonusInfo(ref int sum, ref bool dirty_tag, EatingItems __instance, ECharacterPropertyReferencedType propertyType, sbyte valueSumType = 0)
        {
            var value = __instance.GetCharacterPropertyBonus(propertyType, valueSumType);
            sum += value;

            var tmp = "";
            tmp += ToInfoAdd("食物" + valueSumType2Text(valueSumType), value, 2);
            tmp += GetCharacterPropertyBonusInfo(ref dirty_tag, __instance, propertyType, valueSumType);
            return tmp;
        }
        public unsafe static string GetCharacterPropertyBonusInfo(ref bool dirty_tag, EatingItems __instance, ECharacterPropertyReferencedType propertyType, sbyte valueSumType = 0)
        {
            string result = "";
            for (int i = 0; i < 9; i++)
            {
                ItemKey itemKey = (ItemKey)__instance.ItemKeys[i];
                if (EatingItems.IsValid(itemKey))
                {
                    sbyte itemType = itemKey.ItemType;
                    int characterPropertyBonus = 0;
                    string tmp = "";
                    switch (itemType)
                    {
                        case 7:
                            {
                                characterPropertyBonus = Food.GetCharacterPropertyBonus(itemKey.TemplateId, propertyType);
                                var name = Config.Food.Instance[itemKey.TemplateId].Name;
                                tmp = ToInfoAdd(name, characterPropertyBonus, 3);
                            }
                            break;
                        case 8:
                            {
                                characterPropertyBonus = Medicine.GetCharacterPropertyBonus(itemKey.TemplateId, propertyType);
                                var name = Config.Medicine.Instance[itemKey.TemplateId].Name;
                                tmp = ToInfoAdd(name, characterPropertyBonus, 3);
                            }
                            break;
                        case 9:
                            {
                                characterPropertyBonus = TeaWine.GetCharacterPropertyBonus(itemKey.TemplateId, propertyType);
                                var name = Config.TeaWine.Instance[itemKey.TemplateId].Name;
                                tmp = ToInfoAdd(name, characterPropertyBonus, 3);
                            }
                            break;
                    }
                    if (valueSumType == 0 || valueSumType == 1 == characterPropertyBonus > 0)//我也不知道这个判断是什么意思
                        if (characterPropertyBonus != 0)
                        {
                            dirty_tag = true;
                            result += tmp;
                        }
                }
            }
            return result;
        }
        
        public static string PackGetCommonPropertyBonusInfo(ref int sum, ref bool dirty_tag, Character instance, ECharacterPropertyReferencedType propertyType, sbyte valueSumType = 0)
        {
            var value = (short)CallPrivateMethod<int>(instance, "GetCommonPropertyBonus", new object[] { propertyType, valueSumType });
            sum += value;

            var tmp = "";
            tmp += ToInfoAdd("属性"+ valueSumType2Text(valueSumType), value, 2);
            tmp += GetCommonPropertyBonusInfo(ref dirty_tag, instance, propertyType, valueSumType);
            return tmp;
        }
        public static string GetCommonPropertyBonusInfo(ref bool dirty_tag, Character instance, ECharacterPropertyReferencedType propertyType, sbyte valueSumType = 0)
        {
            string result = "";
            foreach (var feature_id in instance.GetFeatureIds())
            {
                int value = Config.CharacterFeature.GetCharacterPropertyBonus(feature_id, propertyType);
                if (valueSumType == 0 || valueSumType == 1 == value > 0)
                    if (value != 0)
                    {
                        dirty_tag = true;
                        result += ToInfoAdd(GetFeatureName(feature_id), value, 3);
                    }
            }
            foreach (var itemKey in instance.GetEquipment())
                if (itemKey.IsValid())
                {
                    int value = DomainManager.Item.GetCharacterPropertyBonus(itemKey, propertyType);
                    if (valueSumType == 0 || valueSumType == 1 == value > 0)
                        if (value != 0)
                        {
                            dirty_tag = true;
                            result += ToInfoAdd(GetEquipmentName(itemKey), value, 3);
                        }
                }
            result += GetCharacterPropertyBonusInfo(ref dirty_tag, instance.GetEatingItems(), propertyType, valueSumType);
            return result;
        }

        //不是源自游戏的GetXX函数
        //无dirty_tag
        //isAdd为true时只计算config为正的，否则只计算为负的
        public unsafe static string CustomGetNeiliAllocationInfo(ref int check_value,Character instance, ECharacterPropertyReferencedType propertyId,bool isAdd)
        {
            NeiliAllocation allocations = instance.GetNeiliAllocation();
            Config.NeiliTypeItem neiliTypeCfg = Config.NeiliType.Instance[instance.GetNeiliType()];         
            bool dirty_tag = false;
            var tmp = "";
            short value = 0;
            for (int allocationType = 0; allocationType < 4; allocationType++)
            {
                short allocation = allocations.Items[allocationType];
                short config = 0;
                short stepSize = 0;
                Config.NeiliAllocationEffectItem allocationCfg = Config.NeiliAllocationEffect.Instance[allocationType];
                //次要属性前两项
                if (propertyId== ECharacterPropertyReferencedType.RecoveryOfStance)
                {
                    stepSize = allocationCfg.RecoveryOfStanceAndBreath.Outer;
                    config = neiliTypeCfg.RecoveryOfStanceAndBreath.Outer;
                }
                else if (propertyId == ECharacterPropertyReferencedType.RecoveryOfBreath)
                {
                    stepSize = allocationCfg.RecoveryOfStanceAndBreath.Inner;
                    config = neiliTypeCfg.RecoveryOfStanceAndBreath.Inner;
                }
                //次要属性后八项
                else if (propertyId == ECharacterPropertyReferencedType.MoveSpeed)
                {
                    stepSize = allocationCfg.MoveSpeed;
                    config = neiliTypeCfg.MoveSpeed;
                }
                else if (propertyId == ECharacterPropertyReferencedType.RecoveryOfFlaw)
                {
                    stepSize = allocationCfg.RecoveryOfFlaw;
                    config = neiliTypeCfg.RecoveryOfFlaw;
                }
                else if (propertyId == ECharacterPropertyReferencedType.CastSpeed)
                {
                    stepSize = allocationCfg.CastSpeed;
                    config = neiliTypeCfg.CastSpeed;
                }
                else if (propertyId == ECharacterPropertyReferencedType.RecoveryOfBlockedAcupoint)
                {
                    stepSize = allocationCfg.RecoveryOfBlockedAcupoint;
                    config = neiliTypeCfg.RecoveryOfBlockedAcupoint;
                }
                else if (propertyId == ECharacterPropertyReferencedType.WeaponSwitchSpeed)
                {
                    stepSize = allocationCfg.WeaponSwitchSpeed;
                    config = neiliTypeCfg.WeaponSwitchSpeed;
                }
                else if (propertyId == ECharacterPropertyReferencedType.AttackSpeed)
                {
                    stepSize = allocationCfg.AttackSpeed;
                    config = neiliTypeCfg.AttackSpeed;
                }
                else if (propertyId == ECharacterPropertyReferencedType.InnerRatio)
                {
                    stepSize = allocationCfg.InnerRatio;
                    config = neiliTypeCfg.InnerRatio;
                }
                else if (propertyId == ECharacterPropertyReferencedType.RecoveryOfQiDisorder)
                {
                    stepSize = allocationCfg.RecoveryOfQiDisorder;
                    config = neiliTypeCfg.RecoveryOfQiDisorder;
                }
                //攻防四项
                else if (propertyId == ECharacterPropertyReferencedType.PenetrateOfInner)
                {
                    stepSize = allocationCfg.Penetrations.Inner;
                    config = neiliTypeCfg.Penetrations.Inner;
                }
                else if (propertyId == ECharacterPropertyReferencedType.PenetrateOfOuter)
                {
                    stepSize = allocationCfg.Penetrations.Outer;
                    config = neiliTypeCfg.Penetrations.Outer;
                }
                else if (propertyId == ECharacterPropertyReferencedType.PenetrateResistOfOuter)
                {
                    stepSize = allocationCfg.PenetrationResists.Outer;
                    config = neiliTypeCfg.PenetrationResists.Outer;
                }
                else if (propertyId == ECharacterPropertyReferencedType.PenetrateResistOfInner)
                {
                    stepSize = allocationCfg.PenetrationResists.Inner;
                    config = neiliTypeCfg.PenetrationResists.Inner;
                }
                //命中回避八项
                else if (propertyId<= ECharacterPropertyReferencedType.HitRateMind&&propertyId>= ECharacterPropertyReferencedType.HitRateStrength)
                {
                    int idx = propertyId - ECharacterPropertyReferencedType.HitRateStrength;
                    stepSize = allocationCfg.HitValues.Items[idx];
                    config = neiliTypeCfg.HitValues.Items[idx];                
                }
                else if (propertyId <= ECharacterPropertyReferencedType.AvoidRateMind && propertyId >= ECharacterPropertyReferencedType.AvoidRateStrength)
                {
                    int idx = propertyId - ECharacterPropertyReferencedType.AvoidRateStrength;
                    stepSize = allocationCfg.AvoidValues.Items[idx];
                    config = neiliTypeCfg.AvoidValues.Items[idx];
                }
                if (stepSize > 0 && allocation != 0)
                    if((isAdd&&config > 0)|| (config < 0&&!isAdd))//isAdd时只算正的，否则只算负的
                    {
                        value += (short)(config * (allocation / stepSize));
                        tmp += ToInfoAdd($"内力/{stepSize}", (allocation / stepSize), 2);
                        tmp += ToInfoMulti("倍率", config, 2);
                        dirty_tag = true;
                    }
            }
            check_value += value;
            if (ShowUseless || dirty_tag)
                return ToInfoAdd(isAdd?"内力加值":"内力减值", value, 1) + tmp;
            return "";
        }
        public unsafe static string CustomGetNeiliAllocationInfo(ref List<int> check_value, int check_value_idx, Character instance, ECharacterPropertyReferencedType propertyId, bool isAdd)
        {
            var tmp = check_value[check_value_idx];
            var res=CustomGetNeiliAllocationInfo(ref tmp, instance, propertyId, isAdd);
            check_value[check_value_idx] = tmp;
            return res;
        }
        //不需要dirty_tag
        public static string GetBaseCharmInfo(AvatarData instance)
        {
            string result = "";
            var _headAsset = AvatarManager.Instance.GetAsset(instance.AvatarId, EAvatarElementsType.Head, instance.HeadId);
            //返回特性带来的魅力加值和乘算，特性带来的乘算不影响基础魅力，只有加值影响，乘算在其它地方生效
            ValueTuple<double, double> featureCharm = CallPrivateMethod<ValueTuple<double, double>>(instance, "GetFeatureCharm", new object[] { });
            double feature1Charm = featureCharm.Item1;
            //double feature2CharmRate = featureCharm.Item2;
            {
                var value = CallPrivateMethod<double>(instance, "GetEyebrowsCharm", new object[] { });
                result += ToInfoAdd("眉毛", value, 3);
                result += ToInfoMulti("倍率", (double)GlobalConfig.Instance.EyebrowRatioInBaseCharm, 3);
            }
            {
                var value = CallPrivateMethod<double>(instance, "GetEyesCharm", new object[] { });
                result += ToInfoAdd("眼睛", value, 3);
                result += ToInfoMulti("倍率", (double)GlobalConfig.Instance.EyesRatioInBaseCharm, 3);
            }
            {
                var value = CallPrivateMethod<double>(instance, "GetNoseCharm", new object[] { });
                result += ToInfoAdd("鼻子", value, 3);
                result += ToInfoMulti("倍率", (double)GlobalConfig.Instance.NoseRatioInBaseCharm, 3);
            }
            {
                var value = CallPrivateMethod<double>(instance, "GetMouthCharm", new object[] { });
                result += ToInfoAdd("嘴巴", value, 3);
                result += ToInfoMulti("倍率", (double)GlobalConfig.Instance.MouthRatioInBaseCharm, 3);
            }
            result += ToInfoAdd("特性", feature1Charm, 3);
            _headAsset = null;
            return result;
        }
        //不需要dirty_tag
        public static string GetCharmInfo(Character instance, short physiologicalAge, short clothingDisplayId)
        {
            string result = "";
            var avatar = instance.GetAvatar();
            {
                double baseCharm = avatar.GetBaseCharm();
                result += ToInfoAdd("基础", baseCharm, 2);
                result += GetBaseCharmInfo(avatar);
            }

            var _headAsset = AvatarManager.Instance.GetAsset(avatar.AvatarId, EAvatarElementsType.Head, avatar.HeadId);
            SetPrivateField<sbyte>(avatar, "_eyesHeightIndex", -1);
            {
                var rate = CallPrivateMethod<double>(avatar, "CalCharmRate", new object[] { });
                result += ToInfoMulti("倍率", rate, 2);
            }
            {
                var value = CallPrivateMethod<double>(avatar, "GetWrinkleCharm", new object[] { physiologicalAge });
                result += ToInfoAdd("皱纹", value, 2);
            }
            {
                var value = CallPrivateMethod<double>(avatar, "GetClothCharm", new object[] { clothingDisplayId });
                result += ToInfoAdd("衣服", value, 2);
            }
            _headAsset = null;
            return result;
        }
        /* 获取Field信息
         * 命名必须严格为Get{FieldName}Info
         * 返回包含1级信息、check value等在内的全部信息
         */
        unsafe public static string GetAvoidValuesInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = new List<ECharacterPropertyReferencedType> {
                ECharacterPropertyReferencedType.AvoidRateStrength,
                ECharacterPropertyReferencedType.AvoidRateTechnique,
                ECharacterPropertyReferencedType.AvoidRateSpeed,
                ECharacterPropertyReferencedType.AvoidRateMind,
            };
            var fieldId = new List<ushort> {
                MyAffectedDataFieldIds.AvoidStrength,
                MyAffectedDataFieldIds.AvoidTechnique,
                MyAffectedDataFieldIds.AvoidSpeed,
                MyAffectedDataFieldIds.AvoidMind,
                MyAffectedDataFieldIds.AvoidCanChange,
                MyAffectedDataFieldIds.AvoidChangeEffectPercent,
            };
            Config.CharacterItem template = Config.Character.Instance[character.GetTemplateId()];
            var template_value = template.BaseAvoidValues;//此处是int下面是short
            return GetHitAvoidValuesImp(false, "__AvoidValue", template_value,fieldId, propertyType,__instance,character);
        }
        unsafe public static string GetHitAvoidValuesImp(bool isHit,string save_name, HitOrAvoidInts template_value,
            List<ushort> fieldId, List<ECharacterPropertyReferencedType> propertyType,
            CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            //和HitValue计算仅有SpecialEffect.GetTotalPercentModifyValue的部分不同
            const int CT = 4;

            List<string> result = new List<string>(CT);
            List<int> check_value = new List<int> { 0, 0, 0, 0 };
            bool dirty_tag = false;
            for (int i = 0; i < CT; ++i)
                result.Add("");
            short template_id = character.GetTemplateId();
            int charId = character.GetId();
            //基础值
            {
                for (int i = 0; i < CT; i++)
                {
                    if (ShowUseless || template_value.Items[i] != 0)
                        result[i] += ToInfoAdd("角色加值", template_value.Items[i], 1);
                    check_value[i] += template_value.Items[i];
                }
            }
            //基础加成
            var canAdd = new List<bool>();
            var canReduce = new List<bool>();
            var addEffectPercent = new List<int>();
            var reduceEffectPercent = new List<int>();
            NeiliAllocation allocations = character.GetNeiliAllocation();
            sbyte neiliType = character.GetNeiliType();
            Config.NeiliTypeItem neiliTypeCfg = Config.NeiliType.Instance[neiliType];
            HitOrAvoidShorts hitValuesCfg = neiliTypeCfg.HitValues;
            for (int i = 0; i < CT; i++)
            {
                canAdd.Add(DomainManager.SpecialEffect.ModifyData(charId, -1, (ushort)fieldId[CT], true, i, 0));
                canReduce.Add(DomainManager.SpecialEffect.ModifyData(charId, -1, (ushort)fieldId[CT], true, i, 1));
                addEffectPercent.Add(100 + DomainManager.SpecialEffect.GetModifyValue(charId, (ushort)fieldId[CT+1] , 0, i, 0, -1, 0));
                reduceEffectPercent.Add(100 + DomainManager.SpecialEffect.GetModifyValue(charId, (ushort)fieldId[CT+1] , 0, i, 1, -1, 0));
            }
            //属性
            {
                MainAttributes maxMainAttributes = character.GetMaxMainAttributes();
                for (int i = 0; i < CT; i++)
                    result[i] += ToInfoAdd("基础", 100, 1);
                result[0] += ToInfoAdd($"最大膂力/2", maxMainAttributes.Items[0] / 2, 1);
                result[1] += ToInfoAdd($"最大悟性/2", maxMainAttributes.Items[5] / 2, 1);
                result[2] += ToInfoAdd($"最大灵敏/2", maxMainAttributes.Items[1] / 2, 1);
                result[3] += ToInfoAdd($"最大定力/2", maxMainAttributes.Items[2] / 2, 1);
                check_value[0] += maxMainAttributes.Items[0] / 2 + 100;
                check_value[1] += maxMainAttributes.Items[5] / 2 + 100;
                check_value[2] += maxMainAttributes.Items[1] / 2 + 100;
                check_value[3] += maxMainAttributes.Items[2] / 2 + 100;
            }
            {
                //装备，食物，技能加值
                EatingItems eatingItems = character.GetEatingItems();
                for (int i = 0; i < CT; i++)
                    if (canAdd[i])
                    {
                        sbyte valueSumType = 1;//1代笔加值，2代表减值，0都包括
                        dirty_tag = false;
                        var tmp = "";
                        int sum = 0;
                        tmp += PackGetPropertyBonusOfEquipmentsInfo(ref sum, ref dirty_tag, character, propertyType[i], valueSumType);
                        tmp += PackGetCharacterPropertyBonusInfo(ref sum, ref dirty_tag, eatingItems, propertyType[i], valueSumType);
                        tmp += PackGetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref sum, ref dirty_tag, character, propertyType[i], valueSumType);
                        tmp += ToInfoPercent("乘算", addEffectPercent[i], 2);
                        int total = sum * addEffectPercent[i] / 100;
                        check_value[i] += total;
                        if (ShowUseless || dirty_tag)
                        {
                            result[i] += ToInfoAdd("属性加值", total, 1);
                            result[i] += tmp;
                        }
                    }
                //特殊效果加值
                for (sbyte i = 0; i < CT; i = (sbyte)(i + 1))
                    if (canAdd[i])
                    {
                        sbyte valueSumType = 1;
                        var base_value = DomainManager.SpecialEffect.GetModifyValue(charId, (ushort)fieldId[i], 0, -1, -1, -1, valueSumType);
                        int total = base_value * addEffectPercent[i] / 100;

                        dirty_tag = false;
                        var tmp = "";
                        tmp += ToInfoAdd("效果加值", total, 1);
                        check_value[i] += total;
                        tmp += ToInfoAdd("基础", base_value, 2);
                        tmp += GetModifyValueInfo(ref dirty_tag, charId, (ushort)fieldId[i], 0, -1, -1, -1, valueSumType);
                        tmp += ToInfoPercent("乘算", addEffectPercent[i], 2);
                        if (ShowUseless || dirty_tag)
                            result[i] += tmp;
                    }
            }
            {//内力
                for (int i = 0; i < CT; i++)
                    if (canAdd[i])
                        result[i] += CustomGetNeiliAllocationInfo(ref check_value, i, character, propertyType[i], true);
            }
            //战斗难度
            if (charId != DomainManager.Taiwu.GetTaiwuCharId())
            {
                byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
                short factor = Config.CombatDifficulty.Instance[combatDifficulty].HitValues;
                for (int i = 0; i < CT; i++)
                    if (EffectInfoBackend.ShowUseless || factor != 100)
                    {
                        result[i] += ToInfoPercent("战斗难度", factor, 1);
                        check_value[i] = check_value[i] * factor / 100;
                    }
            }
            else if (ShowUseless)
                for (int i = 0; i < CT; i++)
                    result[i] += ToInfo("战斗难度", "-", 1);
            {
                //装备，食物，技能减值
                EatingItems eatingItems = character.GetEatingItems();
                for (int i = 0; i < CT; i++)
                    if (canAdd[i])
                    {
                        sbyte valueSumType = 2;//1代笔加值，2代表减值，0都包括
                        dirty_tag = false;
                        var tmp = "";
                        int sum = 0;
                        tmp += PackGetPropertyBonusOfEquipmentsInfo(ref sum, ref dirty_tag, character, propertyType[i], valueSumType);
                        tmp += PackGetCharacterPropertyBonusInfo(ref sum, ref dirty_tag, eatingItems, propertyType[i], valueSumType);
                        tmp += PackGetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref sum, ref dirty_tag, character, propertyType[i], valueSumType);
                        tmp += ToInfoPercent("乘算", addEffectPercent[i], 2);
                        int total = sum * addEffectPercent[i] / 100;
                        check_value[i] += total;
                        if (ShowUseless || dirty_tag)
                        {
                            result[i] += ToInfoAdd("属性减值", total, 1);
                            result[i] += tmp;
                        }
                    }
                //特殊效果减值
                for (sbyte i = 0; i < CT; i = (sbyte)(i + 1))
                    if (canAdd[i])
                    {
                        sbyte valueSumType = 2;
                        var base_value = DomainManager.SpecialEffect.GetModifyValue(charId, (ushort)fieldId[i], 0, -1, -1, -1, valueSumType);
                        var total = base_value * reduceEffectPercent[i] / 100;

                        dirty_tag = false;
                        var tmp = "";
                        tmp += ToInfoAdd("效果减值", total, 1);
                        check_value[i] += total;
                        tmp += ToInfoAdd("基础", base_value, 2);
                        tmp += GetModifyValueInfo(ref dirty_tag, charId, (ushort)fieldId[i], 0, -1, -1, -1, valueSumType);
                        tmp += ToInfoPercent("乘算", reduceEffectPercent[i], 2);
                        if (ShowUseless || dirty_tag)
                            result[i] += tmp;
                    }
            }
            //内力减值
            for (int i = 0; i < CT; i++)
                if (canReduce[i])
                    result[i] += CustomGetNeiliAllocationInfo(ref check_value, i, character, propertyType[i], false);

            //最终倍率
            for (sbyte i = 0; i < CT; i = (sbyte)(i + 1))
            {
                int percent = 100;
                string tmp = "";
                dirty_tag = false;
                if (canAdd[i])
                {
                    sbyte valueSumType = 1;
                    var feture_value = CallPrivateMethod<int>(character, "GetPropertyBonusOfFeatures", new object[] { propertyType[i], valueSumType });
                    //modifyType是1获得乘算加值
                    var effect_value = DomainManager.SpecialEffect.GetModifyValue(charId, (ushort)fieldId[i], 1, -1, -1, -1, valueSumType);
                    tmp += ToInfoAdd("特性", feture_value, 2);
                    tmp += GetPropertyBonusOfFeaturesInfo(ref dirty_tag, character, propertyType[i], valueSumType);
                    tmp += ToInfoAdd("效果", effect_value, 2);
                    tmp += GetModifyValueInfo(ref dirty_tag, charId, (ushort)fieldId[i], 1, -1, -1, -1, valueSumType);
                    tmp += ToInfoPercent("乘算", addEffectPercent[i], 2);
                    percent += (feture_value + effect_value) * addEffectPercent[i] / 100;
                }
                if (canReduce[i])
                {
                    sbyte valueSumType = 2;
                    var feture_value = CallPrivateMethod<int>(character, "GetPropertyBonusOfFeatures", new object[] { propertyType[i], valueSumType });
                    var effect_value = DomainManager.SpecialEffect.GetModifyValue(charId, (ushort)fieldId[i], 1, -1, -1, -1, valueSumType);
                    tmp += ToInfoAdd("特性", feture_value, 2);
                    tmp += GetPropertyBonusOfFeaturesInfo(ref dirty_tag, character, propertyType[i], valueSumType);
                    tmp += ToInfoAdd("效果", effect_value, 2);
                    tmp += GetModifyValueInfo(ref dirty_tag, charId, (ushort)fieldId[i], 1, -1, -1, -1, valueSumType);
                    tmp += ToInfoPercent("乘算", reduceEffectPercent[i], 2);
                    percent += (feture_value + effect_value) * reduceEffectPercent[i] / 100;
                }
                if (ShowUseless || dirty_tag)
                {
                    result[i] += ToInfoPercent("整体乘算", percent, 1);
                    result[i] += tmp;
                }
                check_value[i] = check_value[i] * percent / 100;
            }
            //蜜汁倍率
            if(!isHit)
                for (sbyte i = 0; i < 4; i = (sbyte)(i + 1))
                {
                    ValueTuple<int, int> totalPercent = DomainManager.SpecialEffect.GetTotalPercentModifyValue(charId, -1, (ushort)fieldId[i]);
                    totalPercent.Item1 = (canAdd[i] ? (totalPercent.Item1 * addEffectPercent[i] / 100) : 0);
                    totalPercent.Item2 = (canReduce[i] ? (totalPercent.Item2 * reduceEffectPercent[i] / 100) : 0);
                    var value = (100 + totalPercent.Item1 + totalPercent.Item2);

                    var tmp = "";
                    dirty_tag = false;
                    check_value[i] = check_value[i] * value / 100;
                    tmp += ToInfoPercent("效果倍率", value, 1);
                    tmp += ToInfoAdd("基础", 100, 2);
                    tmp += ToInfoAdd("最高加值", totalPercent.Item1, 2);
                    tmp += ToInfoAdd("最低减值", totalPercent.Item2, 2);
                    if (ShowUseless || dirty_tag)
                        result[i] += tmp;
                }
            for (int i = 0; i < CT; i++)
            {
                if (ShowUseless || check_value[i] < GlobalConfig.Instance.MinValueOfAttackAndDefenseAttributes)
                    result[i] += ToInfo("下限", $">={GlobalConfig.Instance.MinValueOfAttackAndDefenseAttributes}", 1);
                if (check_value[i] < GlobalConfig.Instance.MinValueOfAttackAndDefenseAttributes)
                    check_value[i] = GlobalConfig.Instance.MinValueOfAttackAndDefenseAttributes;
            }
            //返回
            {
                string tmp = "";
                for (int i = 0; i < CT; ++i)
                    tmp += $"\n{result[i]}\n{ToInfoAdd("总和校验值", check_value[i], 1)}{save_name}{i}\n";
                return tmp;
            }
        }
        unsafe public static string GetHitValuesInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = new List<ECharacterPropertyReferencedType> {
                ECharacterPropertyReferencedType.HitRateStrength,
                ECharacterPropertyReferencedType.HitRateTechnique,
                ECharacterPropertyReferencedType.HitRateSpeed,
                ECharacterPropertyReferencedType.HitRateMind,
            };
            var fieldId = new List<ushort> {
                MyAffectedDataFieldIds.HitStrength,
                MyAffectedDataFieldIds.HitTechnique,
                MyAffectedDataFieldIds.HitSpeed,
                MyAffectedDataFieldIds.HitMind,
                MyAffectedDataFieldIds.HitCanChange,
                MyAffectedDataFieldIds.HitChangeEffectPercent,
            };
            Config.CharacterItem template = Config.Character.Instance[character.GetTemplateId()];
            var template_value = template.BaseHitValues;//此处是int下面是short
            return GetHitAvoidValuesImp(true,"__HitValue", template_value, fieldId, propertyType, __instance, character);
        }
        unsafe public static string GetAttractionInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            int check_value;
            string result = "";
            bool dirty_tag = false;
            ECharacterPropertyReferencedType propertyType = ECharacterPropertyReferencedType.Attraction;

            if (character.GetAgeGroup() != 2)
            {
                check_value = (int)GlobalConfig.Instance.ImmaturityAttraction;
                result += ToInfoAdd("未成年", check_value, 1);
            }
            else
            {
                short physiologicalAge = character.GetPhysiologicalAge();
                Config.CharacterItem template = Config.Character.Instance[character.GetTemplateId()];
                bool isFixed = character.IsCreatedWithFixedTemplate();
                if (isFixed)
                {
                    check_value = (int)template.BaseAttraction;
                    if (check_value != 0 || ShowUseless)
                        result += ToInfoAdd("固定角色基础", check_value, 1);
                }
                else
                {
                    short clothingDisplayId = character.GetClothingDisplayId();
                    //正式版这里clothingDisplayId转成byte明显是bug，而测试版修复了
                    check_value = (int)character.GetAvatar().GetCharm(physiologicalAge, clothingDisplayId);
                    if (check_value != 0 || ShowUseless)
                    {
                        result += ToInfoAdd("外观", check_value, 1);
                        result += GetCharmInfo(character, physiologicalAge, clothingDisplayId);
                    }
                }
                {
                    int value = 0;
                    dirty_tag = false;
                    var tmp = PackGetCommonPropertyBonusInfo(ref value, ref dirty_tag, character, propertyType, 0);
                    check_value += value;
                    if (ShowUseless || dirty_tag)
                    {
                        result += ToInfoAdd("属性加成", value, 1);
                        result += tmp;
                    }
                }
                {
                    var value = CallPrivateMethod<int>(character, "GetPropertyBonusOfCombatSkillEquippingAndBreakout", new object[] { propertyType, (sbyte)0 });
                    check_value += value;

                    var tmp = "";
                    dirty_tag = false;
                    tmp += ToInfoAdd("技能加成", value, 1);
                    tmp += GetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref dirty_tag, character, propertyType, (sbyte)0);
                    if (ShowUseless || dirty_tag)
                        result += tmp;
                }
                if (!character.GetEquipment()[4].IsValid() && !isFixed)
                {
                    check_value /= 2;
                    result += ToInfoMulti("光腚惩罚", 50, 1);
                }
            }
            if (check_value < 0)
            {
                check_value = 0;
                result += ToInfo("下限", ">=0", 1);
            }
            else if (check_value > 900)
            {
                check_value = 900;
                result += ToInfo("上限", "<=900", 1);
            }
            return $"\n{result}\n{ToInfoAdd("总和校验值", check_value, 1)}__Attraction\n";
        }
        unsafe public static string GetRecoveryOfStanceAndBreathInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            const int Inner = 1;
            const int Outter = 0;
            const int CT = 2;
            var result = new List<string> { "", "" };
            var check_value = new List<int> { 0, 0 };
            var propertyType = new List<ECharacterPropertyReferencedType> { ECharacterPropertyReferencedType.RecoveryOfStance, ECharacterPropertyReferencedType.RecoveryOfBreath };
            var fieldId = new List<ushort> { MyAffectedDataFieldIds.RecoveryOfStance,MyAffectedDataFieldIds.RecoveryOfBreath};
            bool dirty_tag = false;

            int charId = character.GetId();
            bool fixMaxValue = DomainManager.SpecialEffect.ModifyData(charId, -1, 23, false);
            bool fixMinValue = DomainManager.SpecialEffect.ModifyData(charId, -1, 24, false);
            if (fixMaxValue != fixMinValue)//同时固定最大最小值时会忽略固定效果
            {
                if (fixMaxValue)
                {
                    check_value[Outter] = check_value[Inner] = 500;
                    result[Inner] += ToInfoAdd("固定为最大", 500, 1);
                    result[Outter] += ToInfoAdd("固定为最大", 500, 1);
                }
                else
                {
                    check_value[Outter] = check_value[Inner] = 0;
                    result[Inner] += ToInfoAdd("固定为最小", 0, 1);
                    result[Outter] += ToInfoAdd("固定为最小", 0, 1);
                }
            }
            else
            {
                {
                    Config.CharacterItem template = Config.Character.Instance[character.GetTemplateId()];
                    check_value[Inner] = template.BaseRecoveryOfStanceAndBreath.Inner;
                    check_value[Outter] = template.BaseRecoveryOfStanceAndBreath.Outer;
                    for (int i = 0; i < CT; ++i)
                        result[i] += ToInfoAdd("基础", check_value[i], 1);
                }
                {//效果加值
                    var valueSumType = (sbyte)1;
                    for (int i = 0; i < CT; i++)
                    {
                        string tmp = "";
                        int total = 0;
                        dirty_tag = false;
                        tmp += PackGetCommonPropertyBonusInfo(ref total, ref dirty_tag, character, propertyType[i], valueSumType);
                        tmp += PackGetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref total, ref dirty_tag, character, propertyType[i], valueSumType);
                        tmp += PackGetModifyValueInfo(ref total, ref dirty_tag, charId, fieldId[i], 0, -1, -1, -1, valueSumType);
                        check_value[i] += (short)total;
                        if (ShowUseless || dirty_tag)
                        {
                            result[i] += ToInfoAdd("属性功法效果", total, 1);
                            result[i] += tmp;
                        }
                    }
                }
                //内力
                {
                    result[Inner] += CustomGetNeiliAllocationInfo(ref check_value, Inner, character, ECharacterPropertyReferencedType.RecoveryOfBreath, true);
                    result[Outter] += CustomGetNeiliAllocationInfo(ref check_value, Outter, character, ECharacterPropertyReferencedType.RecoveryOfStance, true);
                }
                //战斗难度
                if (charId != DomainManager.Taiwu.GetTaiwuCharId())
                {
                    byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
                    OuterAndInnerShorts factor = Config.CombatDifficulty.Instance[combatDifficulty].RecoveryOfStanceAndBreath;

                    result[Outter] += ToInfoPercent("战斗难度", factor.Outer, 1);
                    result[Inner] += ToInfoPercent("战斗难度", factor.Inner, 1);
                    check_value[Outter] = (check_value[Outter] * factor.Outer / 100);
                    check_value[Inner] = (check_value[Inner] * factor.Inner / 100);
                }
                else if(ShowUseless)
                    for(int i = 0; i < CT; i++)
                        result[i] += ToInfo("战斗难度", "对太吾无效", 1);
                {//效果减值
                    var valueSumType = (sbyte)2;
                    for (int i = 0; i < CT; i++)
                    {
                        string tmp = "";
                        int total = 0;
                        dirty_tag = false;
                        tmp += PackGetCommonPropertyBonusInfo(ref total, ref dirty_tag, character, propertyType[i], valueSumType);
                        tmp += PackGetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref total, ref dirty_tag, character, propertyType[i], valueSumType);
                        tmp += PackGetModifyValueInfo(ref total, ref dirty_tag, charId, fieldId[i], 0, -1, -1, -1, valueSumType);
                        check_value[i] += total;
                        if (ShowUseless || dirty_tag)
                        {
                            result[i] += ToInfoAdd("属性功法效果", total, 1);
                            result[i] += tmp;
                        }
                    }
                }
                {//内力减值
                    result[Inner] += CustomGetNeiliAllocationInfo(ref check_value, Inner, character, ECharacterPropertyReferencedType.RecoveryOfBreath, false);
                    result[Outter] += CustomGetNeiliAllocationInfo(ref check_value, Outter, character, ECharacterPropertyReferencedType.RecoveryOfStance, false);
                }
                //阈值
                {
                    for (int i = 0; i < CT; i++)
                        if (check_value[i] < GlobalConfig.Instance.MinAValueOfMinorAttributes)
                        {
                            check_value[i] = GlobalConfig.Instance.MinAValueOfMinorAttributes;
                            result[i] += ToInfo("下限", $">={check_value[i]}", 1);
                        }
                }
                for (int i = 0; i < CT; i++)
                {
                    {
                        var tmp = "";
                        dirty_tag = false;
                        int value = 100;
                        tmp += ToInfoPercent("基础", 100, 2);
                        tmp += PackGetModifyValueInfo(ref value, ref dirty_tag, charId, fieldId[i], (sbyte)1, -1, -1, -1, (sbyte)0);
                        check_value[i] = (check_value[i] * value / 100);
                        if (ShowUseless || dirty_tag)
                        {
                            result[i] += ToInfoPercent("效果乘算", value, 1);
                            result[i] += tmp;
                        }
                    }
                    {
                        var tmp = "";
                        dirty_tag = false;
                        int value = 100;
                        ValueTuple<int, int> totalPercent = DomainManager.SpecialEffect.GetTotalPercentModifyValue(charId, -1, fieldId[i]);
                        var tmp_pair = GetTotalPercentModifyValueInfo(ref dirty_tag, charId, -1, fieldId[i]);
                        value += totalPercent.Item1 + totalPercent.Item2;
                        tmp += ToInfoPercent("效果倍率", value, 1);
                        tmp += ToInfoAdd("基础", 100, 2);
                        tmp += ToInfoAdd("最高加值", totalPercent.Item1, 2);
                        tmp += tmp_pair.Item1;
                        tmp += ToInfoAdd("最低减值", totalPercent.Item2, 2);
                        tmp += tmp_pair.Item2;
                        check_value[i] = check_value[i] * value / 100;
                        if (ShowUseless || dirty_tag)
                            result[i] += tmp;
                    }
                    if(check_value[i]<0)
                    {
                        result[i] += ToInfo("不低于0", $"0", 1);
                        check_value[i] = 0;
                    }
                    if (check_value[i] > 500)
                    {
                        result[i] += ToInfo("不大于500", $"500", 1);
                        check_value[i] = 500;
                    }
                }
            }
            return $"\n{result[Outter]}\n{ToInfoAdd("总和校验值", check_value[Outter], 1)}__RecoveryOfStance\n"
                + $"\n{result[Inner]}\n{ToInfoAdd("总和校验值", check_value[Inner], 1)}__RecoveryOfBreath\n";
        }
        unsafe public static string GetMaxMainAttributesInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            const int CT = 6;
            var result = new List<string> { "", "","","","","" };
            var check_value = new List<int> { 0, 0,0,0,0,0 };
            var propertyType = new List<ECharacterPropertyReferencedType> { 
                ECharacterPropertyReferencedType.Strength,
                ECharacterPropertyReferencedType.Dexterity,
                ECharacterPropertyReferencedType.Concentration,
                ECharacterPropertyReferencedType.Vitality,
                ECharacterPropertyReferencedType.Energy,
                ECharacterPropertyReferencedType.Intelligence,
            };
            var fieldId = new List<ushort> {
                MyAffectedDataFieldIds.MaxStrength,
                MyAffectedDataFieldIds.MaxDexterity,
                MyAffectedDataFieldIds.MaxConcentration,
                MyAffectedDataFieldIds.MaxVitality,
                MyAffectedDataFieldIds.MaxEnergy,
                MyAffectedDataFieldIds.MaxIntelligence
            };
            bool dirty_tag = false;

            MainAttributes baseMainAttributes = character.GetBaseMainAttributes();
            for (int i = 0; i < CT; i++)
            {
                check_value[i] = (int)baseMainAttributes.Items[i];
                result[i] += ToInfoAdd("基础", check_value[i], 1);
            }
            for (int i = 0; i < CT; i++)
            {
                sbyte valueSumType = 0;
                int sum = 0;
                dirty_tag = false;
                var tmp = "";
                tmp += PackGetCommonPropertyBonusInfo(ref sum, ref dirty_tag, character, propertyType[i], valueSumType);
                tmp += PackGetModifyValueInfo(ref sum, ref dirty_tag, character.GetId(), fieldId[i], 0, -1, -1, -1, valueSumType);
                check_value[i] += sum;
                if (ShowUseless||dirty_tag)
                {
                    result[i] += ToInfoAdd("各种加成",sum,1);
                    result[i] += tmp;
                }
            }
            {//前世
                var preexistenceCharIds = character.GetPreexistenceCharIds();
                for (int i = 0; i < CT; ++i)
                {
                    int value =0;
                    var tmp = "";
                    dirty_tag = false;
                    for (int j = 0; j < preexistenceCharIds.Count; j++)
                    {
                        int preCharId = preexistenceCharIds.CharIds[j];
                        DeadCharacter preChar = DomainManager.Character.GetDeadCharacter(preCharId);
                        //因为游戏代码有bug，这里就是=，为了跟显示的数值一致所以也用=，实际应该是+=
                        value = preChar.BaseMainAttributes.Items[j]/10;
                        var name = preChar.FullName.GetName(preChar.Gender, DomainManager.World.GetCustomTexts());
                        tmp += ToInfoAdd($"{name.Item1}(仅最后生效)", value,2);
                        dirty_tag= true;
                    }
                    check_value[i] += value;
                    if (ShowUseless||dirty_tag)
                    {
                        result[i] += ToInfoAdd("前世属性/10",value,1);
                        result[i] += tmp;
                    }
                }
            }
            if (character.GetId() == DomainManager.Taiwu.GetTaiwuCharId())
            {
                MainAttributes samsaraPlatformAdd = DomainManager.Building.GetSamsaraPlatformAddMainAttributes();
                for (int i = 0; i < CT; i++)
                {
                    check_value[i] += samsaraPlatformAdd.Items[i];
                    result[i] += ToInfoAdd("轮回台", samsaraPlatformAdd.Items[i],1);
                }
            }
            {
                short physiologicalAge = character.GetPhysiologicalAge();
                short clampedAge = CallPrivateStaticMethod<short>(character,"GetClampedAgeOfAgeEffect",new object[] { physiologicalAge });
                MainAttributes ageInfluence = Config.AgeEffect.Instance[clampedAge].MainAttributes;
                for (int i=0;i<CT;++i)
                {
                    check_value[i] = check_value[i] * ageInfluence.Items[i] / 100;
                    result[i] += ToInfoPercent("年龄", ageInfluence.Items[i],1);
                }
            }
            for (int i = 0; i < CT; ++i)
            {
                if (ShowUseless || check_value[i] < GlobalConfig.Instance.MinValueOfMaxMainAttributes)
                    result[i] += ToInfo("不小于", $">={GlobalConfig.Instance.MinValueOfMaxMainAttributes}",1);
                if (check_value[i] < GlobalConfig.Instance.MinValueOfMaxMainAttributes)
                    check_value[i] = GlobalConfig.Instance.MinValueOfMaxMainAttributes;
            }
            {
                var tmp = "";
                for (int i = 0; i < CT; ++i)
                    tmp += result[i]+ToInfoAdd("总和校验值", check_value[i], 1)+$"__MainAttribute{i}\n";
                return tmp;
            }
        }
        unsafe public static string GetPenetrationsInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = new List<ECharacterPropertyReferencedType> { ECharacterPropertyReferencedType.PenetrateOfOuter, ECharacterPropertyReferencedType.PenetrateOfInner };
            var fieldId = new List<ushort> { MyAffectedDataFieldIds.PenetrateOuter, MyAffectedDataFieldIds.PenetrateInner };
            byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
            short factor = Config.CombatDifficulty.Instance[combatDifficulty].Penetrations;
            OuterAndInnerInts template_value = Config.Character.Instance[character.GetTemplateId()].BasePenetrations;
            return GetPenetrationsOrResistsInfoImplement(template_value,"__Penetration", factor,fieldId,propertyType,__instance,character);
        }
        unsafe public static string GetPenetrationsOrResistsInfoImplement(OuterAndInnerInts template,string save_key,short difficult_factor, List<ushort> fieldId, List<ECharacterPropertyReferencedType> propertyType, CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            //破体破气
            const int Inner = 1;
            const int Outter = 0;
            const int CT = 2;
            var result = new List<string> { "", "" };
            var check_value = new List<int> { 0, 0 };
            bool dirty_tag = false;
            int charId = character.GetId();
            MainAttributes maxMainAttributes = character.GetMaxMainAttributes();

            check_value[Outter] += template.Outer;
            result[Outter] += ToInfoAdd("角色", template.Outer, 1);
            check_value[Inner] += template.Inner;
            result[Inner] += ToInfoAdd("角色", template.Inner, 1);

            for (int i = 0; i < CT; i++)
            {
                check_value[i] += 100;
                result[i] += ToInfoAdd("基础", 100, 1);
            }
            check_value[Outter] += maxMainAttributes.Items[3] / 2;//Vitality
            result[Outter] += ToInfoAdd("体质/2", maxMainAttributes.Items[3] / 2, 1);
            check_value[Inner] += maxMainAttributes.Items[4] / 2;//Energy
            result[Inner] += ToInfoAdd("根骨/2", maxMainAttributes.Items[4] / 2, 1);

            for (int i = 0; i < CT; i++)
            {
                sbyte valueSumType = 1;
                dirty_tag = false;
                var tmp = "";
                var value = 0;
                tmp += PackGetPropertyBonusOfEquipmentsInfo(ref value, ref dirty_tag, character, propertyType[i], valueSumType);
                tmp += PackGetCharacterPropertyBonusInfo(ref value, ref dirty_tag, character.GetEatingItems(), propertyType[i], valueSumType);
                tmp += PackGetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref value, ref dirty_tag, character, propertyType[i], valueSumType);
                tmp += PackGetModifyValueInfo(ref value, ref dirty_tag, charId, fieldId[i], (sbyte)0, -1, -1, -1, valueSumType);
                check_value[i] += value;
                if (ShowUseless || dirty_tag)
                {
                    result[i] += ToInfoAdd("属性加成", value, 1);
                    result[i] += tmp;
                }
            }
            for (int i = 0; i < CT; i++)
                result[i] += CustomGetNeiliAllocationInfo(ref check_value, i, character, propertyType[i], true);
            if (charId != DomainManager.Taiwu.GetTaiwuCharId())
            {
                short factor = difficult_factor;
                for (int i = 0; i < CT; i++)
                {
                    check_value[i] = check_value[i] * factor / 100;
                    result[i] += ToInfoPercent("战斗难度", factor, 1);
                }
            }
            else if (ShowUseless)
                for (int i = 0; i < CT; i++)
                    result[i] += ToInfo("战斗难度", "-", 1);
            for (int i = 0; i < CT; i++)
            {
                sbyte valueSumType = 2;
                dirty_tag = false;
                var tmp = "";
                var value = 0;
                tmp += PackGetPropertyBonusOfEquipmentsInfo(ref value, ref dirty_tag, character, propertyType[i], valueSumType);
                tmp += PackGetCharacterPropertyBonusInfo(ref value, ref dirty_tag, character.GetEatingItems(), propertyType[i], valueSumType);
                tmp += PackGetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref value, ref dirty_tag, character, propertyType[i], valueSumType);
                tmp += PackGetModifyValueInfo(ref value, ref dirty_tag, charId, fieldId[i], (sbyte)0, -1, -1, -1, valueSumType);
                check_value[i] += value;
                if (ShowUseless || dirty_tag)
                {
                    result[i] += ToInfoAdd("属性加成", value, 1);
                    result[i] += tmp;
                }
            }
            for (int i = 0; i < CT; i++)
                result[i] += CustomGetNeiliAllocationInfo(ref check_value, i, character, propertyType[i], false);
            for (int i = 0; i < CT; i++)
            {
                sbyte valueSumType = 0;
                dirty_tag = false;
                var tmp = "";
                var value = 0;

                value = 100;
                tmp += ToInfoAdd("基础", 100, 2);
                tmp += PackGetPropertyBonusOfFeaturesInfo(ref value, ref dirty_tag, character, propertyType[i], valueSumType);
                tmp += PackGetModifyValueInfo(ref value, ref dirty_tag, charId, fieldId[i], (sbyte)1, -1, -1, -1, valueSumType);
                check_value[i] = check_value[i] * value / 100;
                if (ShowUseless || dirty_tag)
                    result[i] += ToInfoPercent("乘算", value, 1) + tmp;
            }

            for (int i = 0; i < CT; i++)
            {
                ValueTuple<int, int> totalPercent = DomainManager.SpecialEffect.GetTotalPercentModifyValue(charId, -1, fieldId[i]);
                var tmp_pair = GetTotalPercentModifyValueInfo(ref dirty_tag, charId, -1, fieldId[i]);
                var value = 100 + totalPercent.Item1 + totalPercent.Item2;
                check_value[i] = check_value[i] * value / 100;
                if (ShowUseless || dirty_tag)
                {
                    result[i] += ToInfoPercent("乘算", value, 1);
                    result[i] += ToInfoAdd("基础", 100, 2);
                    result[i] += ToInfoAdd("最高加成", totalPercent.Item1, 2);
                    result[i] += tmp_pair.Item1;
                    result[i] += ToInfoAdd("最低加成", totalPercent.Item2, 2);
                    result[i] += tmp_pair.Item2;
                }
            }
            for (int i = 0; i < CT; i++)
            {
                if (ShowUseless || check_value[i] < GlobalConfig.Instance.MinValueOfAttackAndDefenseAttributes)
                    result[i] += ToInfo("不小于", $">={GlobalConfig.Instance.MinValueOfAttackAndDefenseAttributes}", 1);
                if (check_value[i] < GlobalConfig.Instance.MinValueOfAttackAndDefenseAttributes)
                    check_value[i] = GlobalConfig.Instance.MinValueOfAttackAndDefenseAttributes;
            }
            return $"\n{result[Outter]}\n{ToInfoAdd("总和校验值", check_value[Outter], 1)}{save_key}{Outter}\n"
                    + $"\n{result[Inner]}\n{ToInfoAdd("总和校验值", check_value[Inner], 1)}{save_key}{Inner}\n";
        }
        unsafe public static string GetPenetrationResistsInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            //和破体破气完全一致
            var propertyType = new List<ECharacterPropertyReferencedType> { ECharacterPropertyReferencedType.PenetrateResistOfOuter, ECharacterPropertyReferencedType.PenetrateResistOfInner };
            var fieldId = new List<ushort> { MyAffectedDataFieldIds.PenetrateResistOuter, MyAffectedDataFieldIds.PenetrateResistInner };
            byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
            short factor = Config.CombatDifficulty.Instance[combatDifficulty].PenetrationResists;
            OuterAndInnerInts template_value = Config.Character.Instance[character.GetTemplateId()].BasePenetrations;
            return GetPenetrationsOrResistsInfoImplement(template_value,"__PenetrationResist", factor, fieldId, propertyType, __instance, character);
        }
        unsafe public static string GetRecoveryOfFlawInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = ECharacterPropertyReferencedType.RecoveryOfFlaw;
            ushort fieldId = MyAffectedDataFieldIds.RecoveryOfFlaw;
            byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
            short factor = Config.CombatDifficulty.Instance[combatDifficulty].RecoveryOfFlaw;
            return GetSecondaryAttributeInfoImplement("__RecoveryOfFlaw", factor, fieldId, propertyType, true, true, __instance, character);
        }
        //次要属性除了架势/提气恢复，其它计算代码差别很小
        unsafe public static string GetSecondaryAttributeInfoImplement(string save_key, short diffcult_factor,ushort fieldId, ECharacterPropertyReferencedType propertyType,bool canAdd,bool canReduce,
            CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var result = "";
            var check_value = 0;
            bool dirty_tag = false;

            int charId = character.GetId();
            bool fixMaxValue = DomainManager.SpecialEffect.ModifyData(charId, -1, 23, false);
            bool fixMinValue = DomainManager.SpecialEffect.ModifyData(charId, -1, 24, false);

            if (fixMaxValue != fixMinValue)//同时固定最大最小值时会忽略固定效果
            {
                if (fixMaxValue)
                {
                    check_value = check_value = 500;
                    result += ToInfoAdd("固定为最大", 500, 1);
                }
                else
                {
                    check_value = check_value = 0;
                    result += ToInfoAdd("固定为最小", 0, 1);
                    result += ToInfoAdd("固定为最小", 0, 1);
                }
            }
            else
            {
                {
                    Config.CharacterItem template = Config.Character.Instance[character.GetTemplateId()];
                    switch (propertyType)
                    {
                        case ECharacterPropertyReferencedType.MoveSpeed:
                            check_value = template.BaseMoveSpeed; break;
                        case ECharacterPropertyReferencedType.RecoveryOfFlaw:
                            check_value = template.BaseRecoveryOfFlaw; break;
                        case ECharacterPropertyReferencedType.CastSpeed:
                            check_value = template.BaseCastSpeed; break;
                        case ECharacterPropertyReferencedType.RecoveryOfBlockedAcupoint:
                            check_value = template.BaseRecoveryOfBlockedAcupoint; break;
                        case ECharacterPropertyReferencedType.WeaponSwitchSpeed:
                            check_value = template.BaseWeaponSwitchSpeed; break;
                        case ECharacterPropertyReferencedType.AttackSpeed:
                            check_value = template.BaseAttackSpeed; break;
                        case ECharacterPropertyReferencedType.InnerRatio:
                            check_value = template.BaseInnerRatio; break;
                        case ECharacterPropertyReferencedType.RecoveryOfQiDisorder:
                            check_value = template.BaseRecoveryOfQiDisorder; break;
                    }
                    result += ToInfoAdd("基础", check_value, 1);
                }
                {//效果加值
                    var valueSumType = (sbyte)1;
                    string tmp = "";
                    int total = 0;
                    dirty_tag = false;
                    if (canAdd)
                    {
                        tmp += PackGetCommonPropertyBonusInfo(ref total, ref dirty_tag, character, propertyType, valueSumType);
                        tmp += PackGetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref total, ref dirty_tag, character, propertyType, valueSumType);
                        tmp += PackGetModifyValueInfo(ref total, ref dirty_tag, charId, fieldId, 0, -1, -1, -1, valueSumType);
                        if(propertyType == ECharacterPropertyReferencedType.RecoveryOfQiDisorder)
                        {
                            var specifyBuildingEffect = DomainManager.Building.GetSpecifyBuildingEffect(character.GetLocation());
                            if(specifyBuildingEffect != null)
                            {
                                var value = specifyBuildingEffect.QiRecover;
                                if (value != 0)
                                    total += value;
                                tmp += ToInfoAdd("特殊建筑", value, 3);
                            }
                        }
                    }
                    check_value += (short)total;
                    if (ShowUseless || dirty_tag)
                    {
                        result += ToInfoAdd("属性加值", total, 1);
                        result += tmp;
                    }
                }
                //内力
                if (canAdd)
                    result += CustomGetNeiliAllocationInfo(ref check_value, character, propertyType, true);
                if (canAdd)
                {
                    if (DomainManager.Combat.IsCharInCombat(charId))
                    {
                        CombatCharacter combatChar = DomainManager.Combat.GetElement_CombatCharacterDict(charId);
                        short agileSkillId = combatChar.GetAffectingMoveSkillId();
                        if (agileSkillId >= 0)
                        {
                            GameData.Domains.CombatSkill.CombatSkill skill = DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(charId, agileSkillId));
                            var value = DomainManager.CombatSkill.GetCombatSkillCastAddMoveSpeed(skill, -1);
                            check_value += value;
                            result += ToInfoAdd("战斗技能", value, 1);
                            //GetCombatSkillCastAddMoveSpeedInfo很简单就不写个函数了
                            result += ToInfoAdd("基础", skill.GetAddMoveSpeedOnCast(), 2);
                            result += ToInfoPercent("挥发度", skill.GetPracticeLevel(), 2);
                            result += ToInfoPercent("威力(帕瓦！)", skill.GetPower(), 2);
                        }
                    }
                }
                //战斗难度
                if (charId != DomainManager.Taiwu.GetTaiwuCharId())
                {
                    result += ToInfoPercent("战斗难度", diffcult_factor, 1);
                    check_value = check_value * diffcult_factor / 100;
                }
                else if (ShowUseless)
                    result += ToInfo("战斗难度", "-", 1);
                {//效果减值
                    var valueSumType = (sbyte)2;
                    string tmp = "";
                    int total = 0;
                    dirty_tag = false;
                    if (canReduce)
                    {
                        tmp += PackGetCommonPropertyBonusInfo(ref total, ref dirty_tag, character, propertyType, valueSumType);
                        tmp += PackGetPropertyBonusOfCombatSkillEquippingAndBreakoutInfo(ref total, ref dirty_tag, character, propertyType, valueSumType);
                        tmp += PackGetModifyValueInfo(ref total, ref dirty_tag, charId, fieldId, 0, -1, -1, -1, valueSumType);
                    }
                    check_value += (short)total;
                    if (ShowUseless || dirty_tag)
                    {
                        result += ToInfoAdd("属性减值", total, 1);
                        result += tmp;
                    }
                }
                if (canReduce)
                    result += CustomGetNeiliAllocationInfo(ref check_value, character, ECharacterPropertyReferencedType.MoveSpeed, false);
                //阈值
                {
                    if (check_value < GlobalConfig.Instance.MinAValueOfMinorAttributes || ShowUseless)
                        result += ToInfo("下限", $">={GlobalConfig.Instance.MinAValueOfMinorAttributes}", 1);
                    if (check_value < GlobalConfig.Instance.MinAValueOfMinorAttributes)
                        check_value = GlobalConfig.Instance.MinAValueOfMinorAttributes;
                }
                {
                    {
                        var tmp = "";
                        dirty_tag = false;
                        int value = 100;
                        tmp += ToInfoAdd("基础", 100, 2);
                        if (canAdd)
                            tmp += PackGetModifyValueInfo(ref value, ref dirty_tag, charId, fieldId, (sbyte)1, -1, -1, -1, (sbyte)1);
                        if (canReduce)
                            tmp += PackGetModifyValueInfo(ref value, ref dirty_tag, charId, fieldId, (sbyte)1, -1, -1, -1, (sbyte)2);
                        check_value = (check_value * value / 100);
                        if (ShowUseless || dirty_tag)
                        {
                            result += ToInfoPercent("效果乘算", value, 1);
                            result += tmp;
                        }
                    }
                    {
                        var tmp = "";
                        dirty_tag = false;
                        int value = 100;
                        ValueTuple<int, int> totalPercent = DomainManager.SpecialEffect.GetTotalPercentModifyValue(charId, -1, fieldId);
                        var tmp_pair = GetTotalPercentModifyValueInfo(ref dirty_tag, charId, -1, fieldId);
                        tmp += ToInfoAdd("基础", 100, 2);
                        if (canAdd)
                        {
                            value += totalPercent.Item1;
                            tmp += ToInfoAdd("最高加值", totalPercent.Item1, 2);
                            tmp += tmp_pair.Item1;
                        }
                        if (canReduce)
                        {
                            value += totalPercent.Item2;
                            tmp += ToInfoAdd("最低减值", totalPercent.Item2, 2);
                            tmp += tmp_pair.Item2;
                        }
                        check_value = check_value * value / 100;
                        if (ShowUseless || dirty_tag)
                        {
                            result += ToInfoPercent("效果乘算", value, 1);
                            result += tmp;
                        }
                    }
                    if (check_value < 0)
                    {
                        result += ToInfo("不低于0", $"0", 1);
                        check_value = 0;
                    }
                    if (check_value > 500)
                    {
                        result += ToInfo("不大于500", $"500", 1);
                        check_value = 500;
                    }
                }
            }
            return $"\n{result}\n{ToInfoAdd("总和校验值", check_value, 1)}{save_key}\n";
        }
        unsafe public static string GetMoveSpeedInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = ECharacterPropertyReferencedType.MoveSpeed;
            ushort fieldId = MyAffectedDataFieldIds.MoveSpeed;
            bool canAdd = DomainManager.SpecialEffect.ModifyData(character.GetId(), -1, 60, true, 0);
            bool canReduce = DomainManager.SpecialEffect.ModifyData(character.GetId(), -1, 60, true, 1);
            byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
            short factor = Config.CombatDifficulty.Instance[combatDifficulty].MoveSpeed;
            return GetSecondaryAttributeInfoImplement("__MoveSpeed", factor, fieldId,propertyType,canAdd,canReduce,__instance,character);
        }
        unsafe public static string GetCastSpeedInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = ECharacterPropertyReferencedType.CastSpeed;
            ushort fieldId = MyAffectedDataFieldIds.CastSpeed;
            byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
            short factor = Config.CombatDifficulty.Instance[combatDifficulty].CastSpeed;
            return GetSecondaryAttributeInfoImplement("__CastSpeed", factor, fieldId, propertyType, true, true, __instance, character);
        }
        unsafe public static string GetRecoveryOfBlockedAcupointInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = ECharacterPropertyReferencedType.RecoveryOfBlockedAcupoint;
            ushort fieldId = MyAffectedDataFieldIds.RecoveryOfBlockedAcupoint;
            byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
            short factor = Config.CombatDifficulty.Instance[combatDifficulty].RecoveryOfBlockedAcupoint;
            return GetSecondaryAttributeInfoImplement("__RecoveryOfBlockedAcupoint", factor, fieldId, propertyType, true, true, __instance, character);
        }
        unsafe public static string GetWeaponSwitchSpeedInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = ECharacterPropertyReferencedType.WeaponSwitchSpeed;
            ushort fieldId = MyAffectedDataFieldIds.WeaponSwitchSpeed;
            byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
            short factor = Config.CombatDifficulty.Instance[combatDifficulty].WeaponSwitchSpeed;
            return GetSecondaryAttributeInfoImplement("__WeaponSwitchSpeed", factor, fieldId, propertyType, true, true, __instance, character);
        }
        unsafe public static string GetAttackSpeedInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = ECharacterPropertyReferencedType.AttackSpeed;
            ushort fieldId = MyAffectedDataFieldIds.AttackSpeed;
            byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
            short factor = Config.CombatDifficulty.Instance[combatDifficulty].AttackSpeed;
            return GetSecondaryAttributeInfoImplement("__AttackSpeed", factor, fieldId, propertyType, true, true, __instance, character);
        }
        unsafe public static string GetInnerRatioInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = ECharacterPropertyReferencedType.InnerRatio;
            ushort fieldId = MyAffectedDataFieldIds.InnerRatio;
            byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
            short factor = Config.CombatDifficulty.Instance[combatDifficulty].InnerRatio;
            return GetSecondaryAttributeInfoImplement("__InnerRatio", factor, fieldId, propertyType, true, true, __instance, character);
        }
        unsafe public static string GetRecoveryOfQiDisorderInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var propertyType = ECharacterPropertyReferencedType.RecoveryOfQiDisorder;
            ushort fieldId = MyAffectedDataFieldIds.RecoveryOfQiDisorder;
            byte combatDifficulty = DomainManager.World.GetCombatDifficulty();
            short factor = Config.CombatDifficulty.Instance[combatDifficulty].RecoveryOfQiDisorder;
            return GetSecondaryAttributeInfoImplement("__RecoveryOfQiDisorder", factor, fieldId, propertyType, true, true, __instance, character);
        }

        unsafe public static string GetRecoveryOfMainAttributeInfo(CharacterDomain __instance, GameData.Domains.Character.Character character)
        {
            var result = new List<string> { "", "", "", "", "", "" };
            var check_value = new List<int> { 0, 0, 0, 0, 0, 0 };
            
            MainAttributes maxMainAttributes = character.GetMaxMainAttributes();
            short physiologicalAge = character.GetPhysiologicalAge();
            int clampedAge = ((physiologicalAge <= 100) ? physiologicalAge : 100);
            MainAttributes ageInfluence = Config.AgeEffect.Instance[clampedAge].MainAttributesRecoveries;
            for (int i = 0; i < 6; i++)
            {
                check_value[i] = maxMainAttributes.Items[i] / 5;
                result[i] += ToInfoAdd("最大属性/5", check_value[i], 1);
                check_value[i] = check_value[i] * ageInfluence.Items[i] / 100;
                result[i] += ToInfoPercent($"{clampedAge}岁(最大100)", ageInfluence.Items[i], 1);
            }
            {
                var tmp = "";
                for(int i=0;i<6;++i)
                    tmp += result[i] + ToInfoAdd("总合校验值", check_value[i],1) +$"__RecoveryMainAttribute{i}\n";
                return tmp;
            }
        }
        /*注入*/
        public static void SaveInfo()
        {
            var dir = System.IO.Directory.GetCurrentDirectory();
            var path = String.Format("{0}\\..\\Mod\\EffectInfo\\Plugins\\Cache_GetCharacterAttribute.txt", dir);
            AdaptableLog.Info(String.Format("更新角色数据 {0}到{1}", currentCharId, path));
            var tmp = $"{currentCharId}\n";
            foreach (var pair in Cache_FieldText)
                tmp += pair.Value;
            File.WriteAllText(path, tmp);
        }
        //无论查看谁的信息都调用GetGroupCharDisplayDataList或GetCharacterDisplayDataList?
        //打开属性界面时调用GetGroupCharDisplayDataList?但是分配内力等操作只会触发CheckModified不会重新调用GetGroupCharDisplayDataList
        //修改属性一定会触发CheckModified，subId0是人物Id，subId1是fieldId
        [HarmonyPrefix,
        HarmonyPatch(typeof(CharacterDomain),
              "GetGroupCharDisplayDataList")]
        unsafe public static void GetGroupCharDisplayDataListPrePatch(
           CharacterDomain __instance, List<int> charIdList)
        {
            if (charIdList.Count != 1)
                return;
            if (charIdList[0] < 0)
                return;
            Character character = __instance.GetElement_Objects(charIdList[0]);
            if (character==null)
                return;
            currentCharId=charIdList[0];
            //更新所有monitoredfield
            foreach (var fieldId in MonitoredFieldIds)
            {
                Type type = typeof(EffectInfoBackend);
                MethodInfo method_info = type.GetMethod($"Get{MyFieldIds.FieldId2FieldName[fieldId]}Info", System.Reflection.BindingFlags.Static| BindingFlags.Public);
                if(method_info==null)
                {
                    AdaptableLog.Info($"Effect Info:Can't Find Get{MyFieldIds.FieldId2FieldName[fieldId]}Info");
                    continue;
                }
                Cache_FieldText[fieldId] = (string)method_info.Invoke(null, new object[] { __instance, character });
            }
            SaveInfo();
        }
        [HarmonyPrefix,HarmonyPatch(typeof(CharacterDomain),"GetCharacterDisplayDataList")]
        unsafe public static void GetCharacterDisplayDataPrePatch(CharacterDomain __instance, List<int> charIdList)
        {
            if (charIdList.Count != 1)
                return;
            if (charIdList[0] < 0)
                return;
            Character character = __instance.GetElement_Objects(charIdList[0]);
            if (character == null)
                return;
            currentCharId = charIdList[0];
            //更新所有monitoredfield
            foreach (var fieldId in MonitoredFieldIds)
            {
                Type type = typeof(EffectInfoBackend);
                MethodInfo method_info = type.GetMethod($"Get{MyFieldIds.FieldId2FieldName[fieldId]}Info", System.Reflection.BindingFlags.Static | BindingFlags.Public);
                if (method_info == null)
                {
                    AdaptableLog.Info($"Effect Info:Can't Find Get{MyFieldIds.FieldId2FieldName[fieldId]}Info");
                    continue;
                }
                Cache_FieldText[fieldId] = (string)method_info.Invoke(null, new object[] { __instance, character });
            }
            SaveInfo();
        }
        //每当可能有属性改变，都会触发这个函数
        [HarmonyPrefix,HarmonyPatch(typeof(CharacterDomain),"CheckModified_Objects")]
        unsafe public static void OnCharacterDataChangePatch(CharacterDomain __instance,int objectId, ushort fieldId, RawDataPool dataPool)
        {
            if (!On)
                return;
            if (objectId != currentCharId)
                return;
            Character character;
            var characters = GetPrivateValue<Dictionary<int, Character>>(__instance, "_objects");
            if (!characters.TryGetValue(objectId, out character))
                return ;
            var _dataStatesObjects = GetPrivateValue<ObjectCollectionDataStates>(__instance, "_dataStatesObjects");
            //由于更改CharaterId时立刻就会刷新所有field，所以这里可以使用跟CharacterDomain一样的逻辑判断是否要更新
            if (!_dataStatesObjects.IsModified(character.DataStatesOffset, fieldId))
                return;
            var field_name= MyFieldIds.FieldId2FieldName[fieldId];
            int _field_id = (int)fieldId;

            bool need_save = false;
            //非自定义Field调用$"Get{field_name}Info"更新信息，并记录到Cache中，并写入本地文件
            var char_name = character.GetFullName();
            if (MonitoredFieldIds.Contains(_field_id))
            {
                Type type = typeof(EffectInfoBackend);
                MethodInfo method_info = type.GetMethod($"Get{field_name}Info", System.Reflection.BindingFlags.Static | BindingFlags.Public);
                Cache_FieldText[_field_id] =(string)method_info.Invoke(null, new object[] { __instance, character });
                need_save= true;
            }
            //另外判断自定义Field是否要更新
            if (_field_id==MyFieldIds.CurrAge||_field_id==MyFieldIds.MaxMainAttributes)
            {
                Cache_FieldText[_field_id]=GetRecoveryOfMainAttributeInfo(__instance, character);
                need_save = true;
            }
            //保存到文件
            if(need_save)
                SaveInfo();
        }

    }
}
