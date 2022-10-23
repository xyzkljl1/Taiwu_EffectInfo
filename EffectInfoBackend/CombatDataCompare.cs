using GameData.Common;
using GameData.DomainEvents;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Combat;
using GameData.Domains.CombatSkill;
using GameData.Domains.Item;
using GameData.Domains.SpecialEffect;
using GameData.GameDataBridge;
using GameData.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using static GameData.DomainEvents.Events;

namespace EffectInfo
{
    partial class EffectInfoBackend
    {
        public static readonly ushort MY_MAGIC_NUMBER_GetCombatCompareText = 7677;
        public static readonly List<string> HitTypeNames = new List<string> { "力道", "精妙", "迅疾", "动心" };
        public static readonly List<string> AvoidTypeNames = new List<string> { "卸力", "拆招", "闪避", "守心" };
        //战斗放技能时双方攻防命中对比
        //不分敌友，因为总是有一端显示攻击另一端显示防御
        //顺序:3命中3闪避2攻击(外内)2防御
        //TODO：
        public static List<string> Cached_CombatCompareText=new List<string> { "","","",
        "","","",
        "","",
        "",""};

        //CalcAddHitValueOnCast/CalcAddPenetrateResist/...
        //注意此处传入的field，常态的加值和战斗中施放造成的Attacker/Defender加值是不同的fieldId
        //由于不知道最终value是加还是乘还是什么，只包含3级信息，不打包成2级
        unsafe public static (int, string) GetCombatSkillAddFieldInfo(CombatSkill skill,int target_value,int fieldId)
        {
            var check_value = 0;
            var result = "";
            if (skill == null)
                return (check_value, result);
            if(skill.GetId().SkillTemplateId<0)
                return (check_value, result);
            Config.CombatSkillItem configData = Config.CombatSkill.Instance[skill.GetId().SkillTemplateId];
            //身法施放时加的命中
            //CalcAddHitValueOnCast
            if (fieldId==MyAffectedDataFieldIds.AttackerHitTechnique||fieldId==MyAffectedDataFieldIds.AttackerHitStrength
                || fieldId==MyAffectedDataFieldIds.AttackerHitSpeed || fieldId==MyAffectedDataFieldIds.AttackerHitMind)
            {
                int idx=fieldId-Min(MyAffectedDataFieldIds.AttackerHitTechnique,MyAffectedDataFieldIds.AttackerHitStrength,MyAffectedDataFieldIds.AttackerHitSpeed,MyAffectedDataFieldIds.AttackerHitMind);
                //完全不知道是什么id
                short propertyId=(short) (12 + idx);
                int gridBonus = skill.GetBreakoutGridCombatSkillPropertyBonus(propertyId, 0);

                check_value = configData.AddHitOnCast[idx] + gridBonus;
                result += ToInfoAdd("基础", configData.AddHitOnCast[idx], 3);
                result+=ToInfoAdd("突破",gridBonus, 3);
            }
            //护体施放时加的闪避
            //CalcAddAvoidValueOnCast
            else if (fieldId == MyAffectedDataFieldIds.DefenderAvoidTechnique || fieldId == MyAffectedDataFieldIds.DefenderAvoidStrength
                || fieldId == MyAffectedDataFieldIds.DefenderAvoidSpeed || fieldId == MyAffectedDataFieldIds.DefenderAvoidMind)
            {
                int idx = fieldId - Min(MyAffectedDataFieldIds.DefenderAvoidTechnique, MyAffectedDataFieldIds.DefenderAvoidStrength, MyAffectedDataFieldIds.DefenderAvoidSpeed, MyAffectedDataFieldIds.DefenderAvoidMind);
                short propertyId = (short)(20 + idx);
                int gridBonus = skill.GetBreakoutGridCombatSkillPropertyBonus(propertyId, 0);

                check_value = configData.AddAvoidOnCast[idx] + gridBonus;
                result += ToInfoAdd("基础", configData.AddAvoidOnCast[idx], 3);
                result += ToInfoAdd("突破", gridBonus, 3);
            }
            //护体施放时加的防御
            //CalcAddPenetrateResist
            else if (fieldId== MyAffectedDataFieldIds.DefenderPenetrateResistOuter || fieldId==MyAffectedDataFieldIds.DefenderPenetrateResistInner)
            {
                int idx = fieldId - Math.Min(MyAffectedDataFieldIds.DefenderPenetrateResistOuter, MyAffectedDataFieldIds.DefenderPenetrateResistInner);
                short propertyId = (short)(18 + idx);
                int gridBonus = skill.GetBreakoutGridCombatSkillPropertyBonus(propertyId, 0);

                var base_value = idx == 0 ? configData.AddOuterPenetrateResistOnCast : configData.AddInnerPenetrateResistOnCast;
                check_value = base_value + gridBonus;
                result += ToInfoAdd("基础", base_value, 3);
                result += ToInfoAdd("突破", gridBonus, 3);
            }
            //摧破的命中
            //CalcHitValue
            else if (fieldId == MyAffectedDataFieldIds.HitTechnique || fieldId == MyAffectedDataFieldIds.HitStrength
                || fieldId == MyAffectedDataFieldIds.HitSpeed || fieldId == MyAffectedDataFieldIds.HitMind)
            {
                int idx = fieldId - Min(MyAffectedDataFieldIds.HitTechnique, MyAffectedDataFieldIds.HitStrength, MyAffectedDataFieldIds.HitSpeed, MyAffectedDataFieldIds.HitMind);
                bool isMindHit = CombatSkillType.IsMindHitSkill(skill.GetId().SkillTemplateId);
                check_value = configData.TotalHit;
                result += ToInfoAdd("基础", check_value, 3);
                
                var gridBonus =skill.GetBreakoutGridCombatSkillPropertyBonus(30, 0);
                check_value+=gridBonus;
                result += ToInfoAdd("突破", gridBonus , 3);
                if (idx < 3 && !isMindHit)
                {
                    HitOrAvoidInts hitDistribution = skill.GetHitDistribution();
                    check_value = check_value * hitDistribution.Items[idx] / 100;
                    result += ToInfoPercent("命中分布", hitDistribution.Items[idx], 3);
                }
                else if (idx == 3 && isMindHit)
                    ;
                else
                {
                    check_value = 0;
                    result = "";
                }
            }
            //摧破的攻击
            //这里应该用Penetrate，但是因为没有需要与之区分的情况，偷了个懒
            //CalcPenetrations
            else if (fieldId == MyAffectedDataFieldIds.AttackerPenetrateInner || fieldId == MyAffectedDataFieldIds.AttackerPenetrateOuter)
            {
                int idx = fieldId - Math.Min(MyAffectedDataFieldIds.AttackerPenetrateInner, MyAffectedDataFieldIds.AttackerPenetrateOuter);
                int gridBonus = skill.GetBreakoutGridCombatSkillPropertyBonus(29, 0);

                var base_value = configData.Penetrate;
                check_value = base_value + gridBonus;
                result += ToInfoAdd("基础", base_value, 3);
                result += ToInfoAdd("突破", gridBonus, 3);
            }
            sbyte practiceLevel = skill.GetPracticeLevel();
            short power = skill.GetPower();
            check_value = check_value * practiceLevel / 100 * power / 100;
            result += ToInfoPercent("修习度", practiceLevel, 3);
            result += ToInfoPercent("威力", power, 3);
            //摧破的攻击
            //内外比例
            //CalcPenetrations
            if (fieldId == MyAffectedDataFieldIds.AttackerPenetrateInner || fieldId == MyAffectedDataFieldIds.AttackerPenetrateOuter)
            {
                var innerRatio=skill.GetInnerRatio();
                if (fieldId == MyAffectedDataFieldIds.AttackerPenetrateInner)
                {
                    check_value = check_value * innerRatio / 100;
                    result += ToInfoPercent("内外比例", innerRatio, 3);
                }
                else
                {
                    check_value =check_value- check_value * innerRatio / 100;
                    result += ToInfoPercent("内外比例", 100-innerRatio, 3);//实际和*(100-innerRatio)取整不同
                }
            }

            if(check_value!=target_value)
                result += ToInfo("不一致！如果没有影响数值的mod，请报数值bug","", 2);
            return (check_value, result);
        }
        public static (int, string) GetSpecialEffectModifyDataInfo(int charId, short combatSkillId, ushort fieldId, int check_value, int customParam0 = -1, int customParam1 = -1, int customParam2 = -1)
        {
            var result = "";
            AffectedDataKey dataKey = new AffectedDataKey(charId, fieldId, combatSkillId, customParam0, customParam1, customParam2);
            List<SpecialEffectBase> customEffectList = new List<SpecialEffectBase>();
            CallPrivateMethod(DomainManager.SpecialEffect, "CalcCustomModifyEffectList", new object[] { dataKey, customEffectList });
            foreach (var effect in customEffectList)
            {
                var name = GetSpecialEffectName(effect);
                check_value = effect.GetModifiedValue(dataKey, check_value);//里面是代码，并不能获得实际加成公式
                result += ToInfo("{name}",$"=>{check_value}",-2);
            }
            return (check_value,result);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(CombatDomain), "CalcAttackSkillDataCompare")]
        unsafe public static void CalcAttackSkillDataComparePatch(CombatDomain __instance,
            DamageCompareData ____damageCompareData,
            DataContext context, CombatCharacter attacker, CombatCharacter defender, short skillId)
        {
            for(int i= 0; i<Cached_CombatCompareText.Count; i++)
                Cached_CombatCompareText[i]= "";
            if (skillId < 0)//忽略不放技能的情况
                return;
            //前端根据需要显示属性，因此多余的不必清空
            GameData.Domains.Item.Weapon weapon = DomainManager.Item.GetElement_Weapons(__instance.GetUsingWeaponKey(attacker).Id);
            //命中回避有四项，实际最多使用三项，只记录实际使用的三项，类似DamageCompareData
            var hitTypes=new List<int>();
            if (!GameData.Domains.CombatSkill.CombatSkillType.IsMindHitSkill(skillId))
            {
                hitTypes.Add(2);
                hitTypes.Add(1);
                hitTypes.Add(0);
            }
            else
                hitTypes.Add(3);
            GameData.Domains.CombatSkill.CombatSkill attackSkill = DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(attacker.GetId(), skillId));
            bool isMindHit = GameData.Domains.CombatSkill.CombatSkillType.IsMindHitSkill(skillId);

            //由于计算完成时会RaiseDamageCompareDataCalcFinished触发一些技能的代码逻辑，需要用处理前的数值校验
            //由于原代码都打开了testDamage开关，而技能不会修改testDamageData，这里直接取TestDamageFormulaData的数值
            var myCompareData = new DamageCompareData();
            {
                TestDamageFormulaData damageData = __instance.GetTestDamageFormulaData();
                if (isMindHit)
                    for (int i = 0; i < 3; ++i)
                        myCompareData.HitValue[i] = damageData.HitAfterFlaw[i];
                else
                    myCompareData.HitValue[0] = damageData.HitAfterFlaw[3];
                if (isMindHit)
                    for (int i = 0; i < 3; ++i)
                        myCompareData.AvoidValue[i] = damageData.AvoidAfterC[i];
                else
                    myCompareData.AvoidValue[0] = damageData.AvoidAfterC[3];
                myCompareData.OuterAttackValue = damageData.PenetrateAfterC[0];
                myCompareData.InnerAttackValue = damageData.PenetrateAfterC[1];
                myCompareData.OuterDefendValue = damageData.PenetrateResistAfterC[0];
                myCompareData.InnerDefendValue = damageData.PenetrateResistAfterC[1];
            }
            HitOrAvoidInts skillHitDistribution = attackSkill.GetHitDistribution();
            //命中
            {
                HitOrAvoidInts characterHitValue = attacker.GetCharacter().GetHitValues();
                List<ushort> hitTypeCommonFieldId = new List<ushort> 
                { MyAffectedDataFieldIds.HitStrength,
                    MyAffectedDataFieldIds.HitTechnique,
                    MyAffectedDataFieldIds.HitSpeed,
                    MyAffectedDataFieldIds.HitMind };
                var hitTypeFieldId = new List<ushort> {
                MyAffectedDataFieldIds.AttackerHitStrength,
                MyAffectedDataFieldIds.AttackerHitTechnique,
                MyAffectedDataFieldIds.AttackerHitSpeed,
                MyAffectedDataFieldIds.AttackerHitMind,
                };
                var enemyHitTypeFieldId = new List<ushort> {
                MyAffectedDataFieldIds.DefenderHitStrength,
                MyAffectedDataFieldIds.DefenderHitTechnique,
                MyAffectedDataFieldIds.DefenderHitSpeed,
                MyAffectedDataFieldIds.DefenderHitMind,
                };
                foreach (var hitType in hitTypes)
                    if (hitType == 3 || skillHitDistribution.Items[hitType] > 0)
                    {
                        //CalcAttackSkillDataCompare完成后计算结果存入attacker.SkillHitValue和____damageCompareData(____damageCompareData又经过其它修改)
                        //attacker.SkillHitValue和____damageCompareData的命中回避只有三项,非心神顺序和hitType相反,心神固定为0
                        //这里用myCompareData和attacker.SkillHitValue效果一样
                        var result = GetCombatHitOrAvoidInfo(true, hitType==3? attacker.SkillHitValue[0]: attacker.SkillHitValue[2-hitType],
                            hitType,HitTypeNames[hitType], characterHitValue,
                            hitTypeFieldId, enemyHitTypeFieldId, hitTypeCommonFieldId,
                            weapon,
                            __instance, attacker, skillId);
                        int idx = hitType % 3;
                        Cached_CombatCompareText[idx] = result;
                    }
            }
            //回避
            {
                HitOrAvoidInts characterHitValue = defender.GetCharacter().GetAvoidValues();
                List<ushort> hitTypeCommonFieldId = new List<ushort>
                { MyAffectedDataFieldIds.AvoidStrength,
                    MyAffectedDataFieldIds.AvoidTechnique,
                    MyAffectedDataFieldIds.AvoidSpeed,
                    MyAffectedDataFieldIds.AvoidMind };
                var hitTypeFieldId = new List<ushort> {
                MyAffectedDataFieldIds.DefenderAvoidStrength,
                MyAffectedDataFieldIds.DefenderAvoidTechnique,
                MyAffectedDataFieldIds.DefenderAvoidSpeed,
                MyAffectedDataFieldIds.DefenderAvoidMind,
                };
                var enemyHitTypeFieldId = new List<ushort> {
                MyAffectedDataFieldIds.AttackerAvoidStrength,
                MyAffectedDataFieldIds.AttackerAvoidTechnique,
                MyAffectedDataFieldIds.AttackerAvoidSpeed,
                MyAffectedDataFieldIds.AttackerAvoidMind,
                };
                foreach (var hitType in hitTypes)
                    if (hitType == 3 || skillHitDistribution.Items[hitType] > 0)//游戏代码计算了没有命中分布的闪避项，实际是不需要的
                    {
                        var result = GetCombatHitOrAvoidInfo(false, hitType == 3 ? attacker.SkillAvoidValue[0] : attacker.SkillAvoidValue[2 - hitType],
                            hitType, AvoidTypeNames[hitType],characterHitValue,
                            hitTypeFieldId, enemyHitTypeFieldId, hitTypeCommonFieldId,
                            null,
                            __instance, defender, skillId);
                        int idx = hitType % 3 + 3;
                        Cached_CombatCompareText[idx] = result;
                    }
            }
            {
                //攻防
                var bodyPart = attacker.SkillAttackBodyPart;
                Cached_CombatCompareText[6] = GetPenetrateOrResistInfo(false, 0, myCompareData.OuterAttackValue, __instance, attacker, weapon, bodyPart, skillId, attackSkill.GetPenetrations().Outer);
                Cached_CombatCompareText[7] = GetPenetrateOrResistInfo(false, 1, myCompareData.InnerAttackValue, __instance, attacker, weapon, bodyPart, skillId, attackSkill.GetPenetrations().Inner);
                Cached_CombatCompareText[8] = GetPenetrateOrResistInfo(true,0, myCompareData.OuterDefendValue, __instance, defender, weapon, bodyPart, skillId, attackSkill.GetPenetrations().Outer);
                Cached_CombatCompareText[9] = GetPenetrateOrResistInfo(true,1, myCompareData.InnerDefendValue, __instance, defender, weapon, bodyPart, skillId, attackSkill.GetPenetrations().Inner);
            }
            //RaiseDamageCompareDataCalcFinished会触发各个功法的对应函数处理
            //这些函数里有诸如显示tips和改变距离等不能调用两次的部分，由于该Mod旨在显示数值，不想取代原本的计算过程，所以不能调用这些函数
            //只好比较最终处理后的结果，如果有变化则加上说明
            {
                string special_skills_text = ToInfoNote("这部分效果在最终阶段通过代码逻辑实现数值计算，较为复杂无法一一解析",1);
                Type type = typeof(Events);
                FieldInfo field_info = type.GetField("_handlersDamageCompareDataCalcFinished", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Instance);
                if(field_info != null)
                {
                    var handler = field_info.GetValue(null) as OnDamageCompareDataCalcFinished;
                    if (handler != null)
                    {
                        foreach (var invocation in handler.GetInvocationList())
                            if (invocation != null && invocation.Target != null)
                            {
                                //var func = (OnDamageCompareDataCalcFinished)invocation;
                                //func(context, attacker, defender, attacker.SkillAttackBodyPart, weapon, skillId, myCompareData);
                                var class_name = invocation.Target.ToString();
                                if (class_name.Contains('.'))
                                    class_name = class_name.Substring(class_name.LastIndexOf('.') + 1);
                                special_skills_text += ToInfo(class_name, "", 2);
                            }
                        if (!isMindHit)
                        {
                            for (int hitType = 0; hitType < 3; ++hitType)
                            {
                                var idx = 2 - hitType;
                                if (myCompareData.HitValue[idx] != ____damageCompareData.HitValue[idx])
                                    Cached_CombatCompareText[hitType] += ToInfo("复杂效果", $"=>{myCompareData.HitValue[idx]}", 1) + special_skills_text;
                                if (myCompareData.AvoidValue[idx] != ____damageCompareData.AvoidValue[idx])
                                    Cached_CombatCompareText[2 + hitType] += ToInfo("复杂效果", $"=>{myCompareData.AvoidValue[idx]}", 1) + special_skills_text;
                            }
                        }
                        else
                        {
                            if (myCompareData.HitValue[0] != ____damageCompareData.HitValue[0])
                                Cached_CombatCompareText[0] += ToInfo("复杂效果", $"=>{myCompareData.HitValue[0]}", 1) + special_skills_text;
                            if (myCompareData.AvoidValue[0] != ____damageCompareData.AvoidValue[0])
                                Cached_CombatCompareText[3] += ToInfo("复杂效果", $"=>{myCompareData.AvoidValue[0]}", 1) + special_skills_text;
                        }

                        if (myCompareData.OuterAttackValue != ____damageCompareData.OuterAttackValue)
                            Cached_CombatCompareText[6] += ToInfo("复杂效果", $"=>{myCompareData.OuterAttackValue}", 1) + special_skills_text;
                        if (myCompareData.InnerAttackValue != ____damageCompareData.InnerAttackValue)
                            Cached_CombatCompareText[7] += ToInfo("复杂效果", $"=>{myCompareData.InnerAttackValue}", 1) + special_skills_text;
                        if (myCompareData.OuterDefendValue != ____damageCompareData.OuterDefendValue)
                            Cached_CombatCompareText[8] += ToInfo("复杂效果", $"=>{myCompareData.OuterDefendValue}", 1) + special_skills_text;
                        if (myCompareData.InnerDefendValue != ____damageCompareData.InnerDefendValue)
                            Cached_CombatCompareText[9] += ToInfo("复杂效果", $"=>{myCompareData.InnerDefendValue}", 1) + special_skills_text;
                    }

                }
            }

        }
        //attacker.GetHitValue(weapon,hitType,attacker.SkillAttackBodyPart,hitValue.Items[hitType], skillId, true)
        //defender.GetAvoidValue(hitType, attacker.SkillAttackBodyPart, skillId, false, true);
        unsafe public static string GetCombatHitOrAvoidInfo(bool isHit,int target_value,
            int hitType,string hitTypeName,
            HitOrAvoidInts characterHitValue,
            List<ushort> hitTypeFieldId,List<ushort> enemyHitTypeFieldId, List<ushort> hitTypeCommonFieldId,
            Weapon weapon,
            CombatDomain __instance, CombatCharacter character, short attackSkillId)
        {
            //attacker.SkillAttackBodyPart指的是攻击敌人哪个部位
            CombatCharacter enemyChar = __instance.GetCombatCharacter(!character.IsAlly, true);
            var bodyPart = isHit ? character.SkillAttackBodyPart : enemyChar.SkillAttackBodyPart;
            bool dirty_tag = false;//没用，占位
            //testDamageData可以记录每个步骤完成时的数值以及部分加值，但是过于简略，不使用
            var id = character.GetId();
            int check_value = 0;//原代码hit就是用long计算，最后clamp到int，意义不明,但avoid是int，此处统统不管他，直接用int
            bool ignoreArmor=false;
            var result = "";
            if(bodyPart>=0)
                result += ToInfo($"攻击部位", $"{Config.BodyPart.Instance[bodyPart].Name}", 1);
            {
                check_value = characterHitValue.Items[hitType];
                result += ToInfoAdd($"面板{hitTypeName}", check_value, -1);
            }
            if (isHit)
            {//直接修改命中的特殊效果
                var tmp = "";
                (check_value, tmp) = GetSpecialEffectModifyDataInfo(id, attackSkillId, MyAffectedDataFieldIds.ReplaceCharHit, (int)check_value, hitType);
                result += ToInfo("特殊效果", $"=>{check_value}", -1);
                result += tmp;
            }
            else//无视闪避的特殊效果
                ignoreArmor = DomainManager.SpecialEffect.ModifyData(enemyChar.GetId(), attackSkillId, MyAffectedDataFieldIds.IgnoreArmorOnCalcAvoid, false);

            if (isHit&&character.GetAffectingMoveSkillId() >= 0)//身法？
            {
                var tmp = "";
                int total = 0;
                var skill_name = GetCombatSkillName(character.GetAffectingMoveSkillId());
                CombatSkill move_skill = DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(id, character.GetAffectingMoveSkillId()));
                HitOrAvoidInts addHitValues = move_skill.GetAddHitValueOnCast();
                total = GlobalConfig.Instance.AgileSkillBaseAddHit * move_skill.GetPracticeLevel() / 100 * move_skill.GetPower() / 100 * addHitValues.Items[hitType] / 100;
                tmp += ToInfoAdd("基础", GlobalConfig.Instance.AgileSkillBaseAddHit, -2);
                tmp += ToInfoPercent("修习度", move_skill.GetPracticeLevel(), -2);
                tmp += ToInfoPercent("威力", move_skill.GetPower(), -2);
                tmp += ToInfoPercent("功法", addHitValues.Items[hitType], -2);
                if(addHitValues.Items[hitType]!=0)
                    tmp += GetCombatSkillAddFieldInfo(move_skill, addHitValues.Items[hitType],hitTypeFieldId[hitType]).Item2;
                result += ToInfoAdd($"身法-{skill_name}", total, -1)
                    +tmp;
                check_value += total;
            }
            else if((!isHit)&&character.GetAffectingDefendSkillId() > 0)//防御技能
            {
                var tmp = "";
                int total = 0;
                var skill_name = GetCombatSkillName(character.GetAffectingDefendSkillId());
                CombatSkill defend_skill = DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(id, character.GetAffectingDefendSkillId()));
                HitOrAvoidInts addAvoidValues = defend_skill.GetAddAvoidValueOnCast();
                //注意此处乘法顺序和命中不同，影响取整
                total = GlobalConfig.Instance.DefendSkillBaseAddAvoid * addAvoidValues.Items[hitType] / 100 * defend_skill.GetPracticeLevel() / 100 * defend_skill.GetPower() / 100 ;
                tmp += ToInfoAdd("基础", GlobalConfig.Instance.DefendSkillBaseAddAvoid, -2);
                tmp += ToInfoPercent("修习度", defend_skill.GetPracticeLevel(), -2);
                tmp += ToInfoPercent("威力", defend_skill.GetPower(), -2);
                tmp += ToInfoPercent("功法", addAvoidValues.Items[hitType], -2);
                if (addAvoidValues.Items[hitType] != 0)
                    tmp += GetCombatSkillAddFieldInfo(defend_skill, addAvoidValues.Items[hitType], hitTypeFieldId[hitType]).Item2;
                result += ToInfoAdd($"护体-{skill_name}", total, -1)
                    +tmp;
                check_value += total;
            }
            //队友
            if (__instance.IsMainCharacter(character))
            {
                int[] charList = __instance.GetCharacterList(character.IsAlly);
                int total = 0;
                var cmdEffectPercentInfo = "";
                int cmdEffectPercent = 0;
                (cmdEffectPercent, cmdEffectPercentInfo) = GetModifyValueInfo(ref dirty_tag, id, (ushort)MyAffectedDataFieldIds.TeammateCmdEffect, (sbyte)0, 10, -1, -1, (sbyte)0, -3);
                cmdEffectPercent += 100;
                cmdEffectPercentInfo = ToInfoPercent($"效果乘算", cmdEffectPercent, -2)
                                    + ToInfoNote("实际是每个角色分别乘上倍率取整后再相加", -2)
                                    + ToInfoAdd("基础", 100, -3)
                                    + cmdEffectPercentInfo;

                var tmp = "";
                for (int i = 1; i < charList.Length && charList[i] >= 0; i++)
                {
                    CombatCharacter teammateChar = __instance.GetElement_CombatCharacterDict(charList[i]);
                    HitOrAvoidInts? teammateHitValues = null;
                    if (isHit&&teammateChar.GetExecutingTeammateCommand() == 10)
                        teammateHitValues = teammateChar.GetCharacter().GetHitValues();
                    else if((!isHit)&& teammateChar.GetExecutingTeammateCommand()==11)
                        teammateHitValues = teammateChar.GetCharacter().GetAvoidValues();
                    if(teammateHitValues != null)
                    {
                        var hitValues=teammateHitValues.Value;
                        var name = (CharacterDomain.GetRealName(teammateChar.GetCharacter())).surname;
                        int addValue = hitValues.Items[hitType] * teammateChar.ExecutingTeammateCommandCofig.IntArg / 100;
                        tmp += ToInfoAdd($"{name}", addValue, -2);
                        tmp += ToInfoAdd($"面板{hitTypeName}", hitValues.Items[hitType], -3);
                        tmp += ToInfoPercent($"迷之倍率:", teammateChar.ExecutingTeammateCommandCofig.IntArg, -3);
                        total += addValue * cmdEffectPercent / 100;
                    }
                }
                tmp += cmdEffectPercentInfo;
                result += ToInfoAdd("同道指令", total, -1);
                check_value += total;
                if (total != 0)
                    result += tmp;
            }
            //效果加值
            {
                int value = 0;
                result += PackGetModifyValueInfoS(ref value, ref dirty_tag,
                                            id, attackSkillId, hitTypeFieldId[hitType], 0, attackSkillId, -1, -1, 0, -1);
                check_value += value;
            }
            //放大
            {
                var value = isHit ? 150 : 105;
                check_value *= value;//没有除100，这之后都是以放大100倍的状态计算
                result += ToInfoMulti("放大", value, -1);
            }
            //乘算系数
            {
                int percent = 0;
                var tmp = "";

                if(isHit)//摧破+武器
                {
                    CombatSkill attackSkill = DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(character.GetId(), attackSkillId));
                    HitOrAvoidInts skillHitValue = attackSkill.GetHitValue();
                    percent += 100 + skillHitValue.Items[hitType];
                    tmp += ToInfoAdd($"基础", 100, 2);
                    tmp += ToInfoAdd($"功法{hitTypeName}", skillHitValue.Items[hitType], 2);
                    //注意FieldId的区别，身法是获得OnCast时的AddHitValue，摧破是获得HitValue
                    if(skillHitValue.Items[hitType]!=0)
                        tmp += GetCombatSkillAddFieldInfo(DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(id, attackSkillId)), skillHitValue.Items[hitType], hitTypeCommonFieldId[hitType]).Item2;

                    //武器
                    if (attackSkillId < 0 || DomainManager.CombatSkill.GetSkillType(id, attackSkillId) != 5)
                    {
                        HitOrAvoidShorts weaponFactors = weapon.GetHitFactors();
                        var use_power = DomainManager.Character.GetItemUsePower(id, weapon.GetItemKey());
                        var value = weaponFactors.Items[hitType] *  use_power/ 100;
                        percent += value;
                        tmp += ToInfoAdd($"{weapon.GetName()}({use_power}%){hitTypeName}", value, -2);
                    }
                    else //腿法/空手？
                    {
                        ItemKey shoesKey = character.Armors[5];
                        GameData.Domains.Item.Armor shoes = (shoesKey.IsValid() ? DomainManager.Item.GetElement_Armors(shoesKey.Id) : null);
                        if (shoes != null && shoes.GetCurrDurability() > 0)
                        {
                            short shoesWeaponTemplateId = Config.Armor.Instance[shoesKey.TemplateId].RelatedWeapon;
                            ItemKey shoesWeaponKey = new ItemKey(0, 0, shoesWeaponTemplateId, -1);
                            HitOrAvoidShorts shoesFactors = Config.Weapon.Instance[shoesWeaponTemplateId].BaseHitFactors;
                            var use_power = DomainManager.Character.GetItemUsePower(id, shoesWeaponKey);
                            var value = shoesFactors.Items[hitType] *  use_power/ 100;
                            percent += value;
                            tmp += ToInfoAdd($"{Config.Weapon.Instance[shoesWeaponTemplateId].Name}({use_power}%){hitTypeName}", value, -2);
                        }
                        else
                        {
                            var _weapons = GetPrivateValue<ItemKey[]>(character, "_weapons");
                            GameData.Domains.Item.Weapon emptyHandWeapon = DomainManager.Item.GetElement_Weapons(_weapons[3].Id);
                            HitOrAvoidShorts emptyHandFactors = emptyHandWeapon.GetHitFactors();
                            var use_power = DomainManager.Character.GetItemUsePower(id, emptyHandWeapon.GetItemKey());
                            var value = emptyHandFactors.Items[hitType] * use_power / 100;
                            percent += value;
                            tmp += ToInfoAdd($"空手({use_power}%){hitTypeName}", value, -2);
                        }
                    }
                }
                else//防具
                {
                    percent += 100;
                    tmp += ToInfoAdd("基础", 100, 2); 
                    if (bodyPart >= 0 && !ignoreArmor)
                    {
                        ItemKey armorKey = character.Armors[bodyPart];
                        if (armorKey.IsValid())
                        {
                            GameData.Domains.Item.Armor armor = DomainManager.Item.GetElement_Armors(armorKey.Id);
                            if (armor.GetCurrDurability() > 0)
                            {
                                HitOrAvoidShorts armorFactors = armor.GetAvoidFactors();
                                var usePower = DomainManager.Character.GetItemUsePower(id, armorKey);
                                var value = armorFactors.Items[hitType] * usePower / 100;
                                percent += value;
                                tmp += ToInfoAdd($"{armor.GetName()}({usePower}%)",value,2);
                            }
                        }
                    }
                }
                {//效果加成
                    int value = 0;
                    sbyte valueSumType = 0;
                    {//我方
                        var value_text = "";
                        (value, value_text) = GetModifyValueInfoS(ref dirty_tag, id, attackSkillId, (ushort)hitTypeFieldId[hitType], 1, attackSkillId, character.PursueAttackCount, bodyPart, valueSumType, -3);
                        tmp += ToInfoAdd("我方效果" + valueSumType2Text(valueSumType), value, -2) + value_text;
                        percent += value;
                    }
                    {//敌方
                        var value_text = "";
                        (value, value_text) = GetModifyValueInfoS(ref dirty_tag, enemyChar.GetId(), attackSkillId, (ushort)enemyHitTypeFieldId[hitType], 1, attackSkillId, character.PursueAttackCount, bodyPart, valueSumType, -3);
                        tmp += ToInfoAdd("敌方效果" + valueSumType2Text(valueSumType), value, -2) + value_text;
                        percent += value;
                    }
                }
                result += ToInfoPercent($"乘算效果", percent, -1);
                result += tmp;
                check_value = check_value * percent / 100;
            }
            //不可叠加乘算
            {
                (string, string) tmp = ("", "");
                (int, int) value = (0, 0);
                {//我方
                    (int, int) modify_value = (0, 0);
                    (string, string) modify_text;
                    (modify_value, modify_text) = GetTotalPercentModifyValueInfo(ref dirty_tag, id, attackSkillId, hitTypeFieldId[hitType], bodyPart);
                    tmp.Item1 += modify_text.Item1;
                    value.Item1 = Math.Max(value.Item1, modify_value.Item1);
                    tmp.Item2 += modify_text.Item2;
                    value.Item2 = Math.Max(value.Item2, modify_value.Item2);
                }
                {//敌方
                    (int, int) modify_value = (0, 0);
                    (string, string) modify_text;
                    (modify_value, modify_text) = GetTotalPercentModifyValueInfo(ref dirty_tag, enemyChar.GetId(), attackSkillId, enemyHitTypeFieldId[hitType], bodyPart);
                    tmp.Item1 += modify_text.Item1;
                    value.Item1 = Math.Max(value.Item1, modify_value.Item1);
                    tmp.Item2 += modify_text.Item2;
                    value.Item2 = Math.Max(value.Item2, modify_value.Item2);
                }
                //同道指令
                {
                    int cmd = -1;
                    if (isHit && character.GetExecutingTeammateCommand() == 4)
                        cmd = character.GetExecutingTeammateCommand();
                    else if ((!isHit) && character.GetExecutingTeammateCommand()==5)
                        cmd = character.GetExecutingTeammateCommand();
                    if(cmd>=0)
                    {
                        int cmdAdd = DomainManager.SpecialEffect.GetModifyValue(__instance.GetMainCharacter(character.IsAlly).GetId(), (ushort)MyAffectedDataFieldIds.TeammateCmdEffect
                                                                            , (sbyte)0, cmd, -1, -1, (sbyte)0);
                        var cmdAddText = ToInfoAdd("同道指令", cmdAdd, 3);
                        //放不下了，就不展开了
                        /*
                        (cmdAdd, cmdAddText) = GetModifyValueInfo(ref dirty_tag, __instance.GetMainCharacter(attacker.IsAlly).GetId()
                                                                ,(ushort)MyAffectedDataFieldIds.TeammateCmdEffect, (sbyte)0, 4, -1, -1, (sbyte)0
                                                                ,3);*/
                        value.Item1 = Math.Max(value.Item1, cmdAdd);
                        tmp.Item1 += cmdAddText;
                    }

                }
                int percent = 100 + value.Item1 + value.Item2;
                result += ToInfoPercent("不叠加乘算", percent, 1);
                result += ToInfoAdd("基础", 100, 2);
                result += ToInfoAdd("加值(取高)", value.Item1, 2);
                result += tmp.Item1;
                result += ToInfoAdd("减值(取低)", value.Item2, 2);
                result += tmp.Item2;
                check_value = check_value * percent / 100;
            }
            //变招*1.4,技能不用算
            //破绽
            if(isHit&&bodyPart >= 0)
            {
                int flawCount = enemyChar.GetFlawCount()[bodyPart];
                flawCount += DomainManager.SpecialEffect.GetModifyValue(id, attackSkillId, 94, 0, bodyPart, -1, -1, 0);
                int value = 100 + 40 * flawCount;
                var name = Config.BodyPart.Instance[bodyPart].Name;
                result += ToInfoPercent($"{name}破绽*40+100", value, 1);
                check_value = check_value * value / 100;
            }
            check_value /= 100;
            result += ToInfoDivision("显示", 100, 1);
            result += ToInfoNote("仅用于显示，计算时没有除100", 1);
            result += ToInfoAdd("总合校验值", check_value, 1);
            if(check_value!= target_value/100)
            {
                result += ToInfo("不一致！！！", "", 1);
                result += ToInfoNote("如果没有影响数值的mod，请报数值bug",1);
            }
            return result;
        }
        //attacker.GetPenetrate(false, weapon, bodyPart, skillId, skill.GetPenetrations().Outer, true);
        //defender.GetPenetrateResist(false, weapon, bodyPart, skillId, false, true);
        //idx:外0 内1
        unsafe public static string GetPenetrateOrResistInfo(bool isResist,int idx,int target_value,
                                                    CombatDomain _combatDomain,
                                                    CombatCharacter character,
                                                    Weapon weapon, sbyte bodyPart, short attackSkillId, int skillAddPercent)
        {
            bool isOutter = idx == 0;
            var result = "";
            int check_value;
            bool dirty_tag = false;//没用
            if (isResist)
                check_value = isOutter ? character.GetCharacter().GetPenetrationResists().Outer : character.GetCharacter().GetPenetrationResists().Inner;
            else
                check_value = isOutter ? character.GetCharacter().GetPenetrations().Outer : character.GetCharacter().GetPenetrations().Inner;
            if(bodyPart>=0)
                result += ToInfo($"攻击部位",$"{Config.BodyPart.Instance[bodyPart].Name}", 1);
            result += ToInfoAdd("面板",check_value, 1);

            List<ushort> peneFieldId = new List<ushort>();
            List<ushort> enemyPeneFieldId = new List<ushort>();
            if(isResist)
            {
                peneFieldId.Add(MyAffectedDataFieldIds.DefenderPenetrateResistOuter);
                peneFieldId.Add(MyAffectedDataFieldIds.DefenderPenetrateResistInner);
                enemyPeneFieldId.Add(MyAffectedDataFieldIds.AttackerPenetrateResistOuter);
                enemyPeneFieldId.Add(MyAffectedDataFieldIds.AttackerPenetrateResistInner);
            }
            else
            {
                peneFieldId.Add(MyAffectedDataFieldIds.AttackerPenetrateOuter);
                peneFieldId.Add(MyAffectedDataFieldIds.AttackerPenetrateInner);
                enemyPeneFieldId.Add(MyAffectedDataFieldIds.DefenderPenetrateOuter);
                enemyPeneFieldId.Add(MyAffectedDataFieldIds.DefenderPenetrateInner);
            }

            string propertyName = "";
            if (isResist)
                propertyName = isOutter ? "御体" : "御气";
            else
                propertyName = isOutter ? "破体" : "破气";
            CombatCharacter enemyChar = _combatDomain.GetCombatCharacter(!character.IsAlly, true);
            CombatCharacter attacker = isResist ? enemyChar : character;
            bool ignoreArmor = DomainManager.SpecialEffect.ModifyData(attacker.GetId(), attackSkillId, 87, false);
            bool isLegSkill = attackSkillId >= 0 && DomainManager.CombatSkill.GetSkillType(attacker.GetId(), attackSkillId) == 5;
            var _id = character.GetId();

            if (!isResist)
            {
                int total_value = 0;
                var tmp = "";
                //武器
                int usePower = 0;
                int penetration = 0;
                int innerRatio = 0;
                string weaponName = "";

                if (!isLegSkill)
                {
                    usePower = DomainManager.Character.GetItemUsePower(_id, weapon.GetItemKey());
                    penetration = weapon.GetPenetrationFactor();
                    weaponName = weapon.GetName();
                    innerRatio = character.GetCombatWeaponData().GetInnerRatio();
                }
                else
                {
                    ItemKey shoesKey = character.Armors[5];
                    GameData.Domains.Item.Armor shoes = (shoesKey.IsValid() ? DomainManager.Item.GetElement_Armors(shoesKey.Id) : null);
                    if (shoes != null && shoes.GetCurrDurability() > 0)
                    {
                        short shoesWeaponTemplateId = Config.Armor.Instance[shoesKey.TemplateId].RelatedWeapon;
                        ItemKey shoesWeaponKey = new ItemKey(0, 0, shoesWeaponTemplateId, -1);
                        Config.WeaponItem shoesWeaponConfig = Config.Weapon.Instance[shoesWeaponTemplateId];
                        usePower = DomainManager.Character.GetItemUsePower(_id, shoesWeaponKey);
                        penetration = shoesWeaponConfig.BasePenetrationFactor;
                        weaponName = shoesWeaponConfig.Name;
                        innerRatio = shoesWeaponConfig.DefaultInnerRatio;
                    }
                    else
                    {
                        GameData.Domains.Item.Weapon emptyHandWeapon = DomainManager.Item.GetElement_Weapons(character.GetWeapons()[3].Id);
                        usePower = DomainManager.Character.GetItemUsePower(_id, emptyHandWeapon.GetItemKey());
                        penetration = emptyHandWeapon.GetPenetrationFactor();
                        weaponName = "空手";
                        innerRatio = character.GetCombatWeaponData(3).GetInnerRatio();
                    }
                }
                total_value = penetration * usePower / 100;
                tmp += ToInfoAdd($"{weaponName}({usePower}%)-{propertyName}", total_value, 2);
                tmp += ToInfoPercent($"内外比例", isOutter ? 100 - innerRatio : innerRatio, 2);
                if(isOutter)//注意取整，不能写成先减再乘
                    total_value = total_value -total_value * innerRatio / 100;
                else
                    total_value =total_value *innerRatio/ 100;

                //武器坚韧低于防具破刃时,在内外比例之后计算
                if (bodyPart >= 0 && !ignoreArmor)
                {
                    ItemKey armorKey = enemyChar.Armors[bodyPart];
                    GameData.Domains.Item.Armor armor = (armorKey.IsValid() ? DomainManager.Item.GetElement_Armors(armorKey.Id) : null);
                    int weaponEquipDefense = _combatDomain.GetWeaponEquiDefense(character, weapon, attackSkillId);
                    int armorEquipAttack = _combatDomain.GetArmorEquiAttack(enemyChar, armor);
                    if (armorEquipAttack > weaponEquipDefense)
                    {
                        //注意取整，不能直接把x-x*factor改成x*(1-factor)的形式
                        int factor = Math.Min(20 + 10 * armorEquipAttack / Math.Max(weaponEquipDefense, 1), 100);
                        int value = -total_value * factor/100;
                        tmp += ToInfoAdd("坚韧<破刃", value, 2);
                        tmp += ToInfoAdd($"当前{propertyName}", total_value, 3);
                        tmp += ToInfoPercent($"20+10*破刃/武器坚韧", factor, 3);
                        total_value +=value;
                    }
                }
                result += ToInfoAdd($"武器",total_value, 1)
                        +tmp;
                check_value = check_value + total_value;
            }
            else
            {
                if (bodyPart >= 0 && !ignoreArmor)
                {
                    int total_value = 0;
                    var tmp = "";
                    ItemKey armorKey = character.Armors[bodyPart];
                    GameData.Domains.Item.Armor armor = (armorKey.IsValid() ? DomainManager.Item.GetElement_Armors(armorKey.Id) : null);
                    if (armor != null)
                    {
                        OuterAndInnerShorts penetrationResistFactors = armor.GetPenetrationResistFactors();
                        var use_power = DomainManager.Character.GetItemUsePower(_id, armorKey);
                        total_value += (isOutter ? penetrationResistFactors.Outer : penetrationResistFactors.Inner) *use_power/ 100;
                        tmp += ToInfoAdd($"{armor.GetName()}({use_power}%)",total_value,2);
                    }
                    int weaponEquipAttack = _combatDomain.GetWeaponEquiAttack(enemyChar, weapon, attackSkillId);
                    int armorEquipDefense = _combatDomain.GetArmorEquiDefense(character, armor);
                    if (weaponEquipAttack > armorEquipDefense)
                    {
                        //注意取整，不能直接把x-x*factor改成x*(1-factor)的形式
                        int factor = Math.Min(20 + 10 * weaponEquipAttack / Math.Max(armorEquipDefense, 1), 100);
                        int value = -total_value * factor / 100;
                        tmp += ToInfoAdd("坚韧<破甲", value, 2);
                        tmp += ToInfoAdd($"当前{propertyName}", total_value, 3);
                        tmp += ToInfoPercent($"20+10*武器破甲/护甲坚韧", factor, 3);
                        total_value += value;
                    }
                    result += ToInfoAdd($"护甲", total_value, 1)
                            + tmp;
                    check_value = check_value + total_value;
                }
            }
            //防御功法
            //UpdateDamageCompareData中调用GetPenetrateResist时，ignoreDefendSkill恒为false
            if (isResist)
                if (character.GetAffectingDefendSkillId() >= 0)//&& !ignoreDefendSkill
                {
                    GameData.Domains.CombatSkill.CombatSkill defendSkill = DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(_id, character.GetAffectingDefendSkillId()));
                    OuterAndInnerInts addPenetrateResists = defendSkill.GetAddPenetrateResist();
                    var skill_name = GetCombatSkillName(character.GetAffectingDefendSkillId());
                    var factor = isOutter ? addPenetrateResists.Outer : addPenetrateResists.Inner;
                    var value = GlobalConfig.Instance.DefendSkillBaseAddPenetrateResist
                                    * defendSkill.GetPracticeLevel() / 100 
                                    * defendSkill.GetPower() / 100 
                                    * factor / 100;
                    var tmp = "";
                    tmp += ToInfoAdd("基础", GlobalConfig.Instance.DefendSkillBaseAddPenetrateResist, 2);
                    tmp += ToInfoPercent("修习度", defendSkill.GetPracticeLevel(), 2);
                    tmp += ToInfoPercent("威力", defendSkill.GetPower(), 2);
                    tmp += ToInfoPercent("功法", factor, 2);
                    if(factor!=0)
                        tmp += GetCombatSkillAddFieldInfo(defendSkill, factor, peneFieldId[idx]).Item2;
                    result += ToInfoAdd($"护体-{skill_name}", value,1)+tmp;
                    check_value += value;
                }
            {
                var value = isResist ? 105 : 150;
                check_value *= value;//没有除100，这之后都是以放大100倍的状态计算
                result += ToInfoMulti("放大", value, -1);
            }
            {//效果加成
                int total_value = 100;
                var tmp = "";
                tmp += ToInfoAdd("基础", total_value, 2);
                if (!isResist)
                {
                    total_value += skillAddPercent;
                    tmp += ToInfoAdd("摧破功法", skillAddPercent, 2);
                    //此处传入的fieldId是带Attacker的，因为没有常态的破体加成需要与其区分
                    if(skillAddPercent!=0)
                        tmp += GetCombatSkillAddFieldInfo(DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(_id, attackSkillId)), skillAddPercent, peneFieldId[idx]).Item2;
                }
                else
                {
                    //毒
                    if (isResist)
                    {
                        int poisonType = -1;
                        if (isOutter && character.PoisonOverflow(4))
                            poisonType = 4;
                        else if ((!isOutter) && character.PoisonOverflow(5))
                            poisonType = 5;
                        if(poisonType >=0)
                        {
                            var name = Config.Poison.Instance[poisonType].Name;
                            //GetPoison和GetCurrentPoison不同？
                            int base_value = isOutter? Config.Poison.Instance[(sbyte)poisonType].ReduceOuterResist: Config.Poison.Instance[(sbyte)poisonType].ReduceInnerResist;
                            int factor = Math.Min(character.GetPoison().Items[poisonType] / character.GetPoisonResist().Items[poisonType], 3);
                            int value = -base_value* factor;
                            tmp += ToInfoAdd("中毒",value,2);
                            tmp += ToInfoAdd("基础", base_value, 3);
                            tmp += ToInfoMulti("中毒/毒抗(中毒/毒抗不小于)", factor, 3);
                            total_value += value;
                        }
                    }
                }
                sbyte valueSumType = 0;
                {//我方
                    var value_text = "";
                    int value = 0;
                    (value, value_text) = GetModifyValueInfoS(ref dirty_tag, _id, attackSkillId, peneFieldId[idx], 1, attackSkillId, character.PursueAttackCount, bodyPart, valueSumType, -3);
                    tmp += ToInfoAdd("我方效果" + valueSumType2Text(valueSumType), value, 2) + value_text;
                    total_value += value;
                }
                {//敌方
                    var value_text = "";
                    int value=0;
                    (value, value_text) = GetModifyValueInfoS(ref dirty_tag, enemyChar.GetId(), attackSkillId, enemyPeneFieldId[idx], 1, attackSkillId, character.PursueAttackCount, bodyPart, valueSumType, -3);
                    tmp += ToInfoAdd("敌方效果" + valueSumType2Text(valueSumType), value, 2) + value_text;
                    total_value += value;
                }

                result += ToInfoPercent("效果加成", total_value, 1)
                    + tmp;
                check_value = check_value * total_value / 100;
            }
            //不叠加效果加成
            //不可叠加乘算
            {
                (string, string) tmp = ("", "");
                (int, int) value = (0, 0);
                {//我方
                    (int, int) modify_value = (0, 0);
                    (string, string) modify_text;
                    (modify_value, modify_text) = GetTotalPercentModifyValueInfo(ref dirty_tag, _id, attackSkillId, peneFieldId[idx], bodyPart);
                    tmp.Item1 += modify_text.Item1;
                    value.Item1 = Math.Max(value.Item1, modify_value.Item1);
                    tmp.Item2 += modify_text.Item2;
                    value.Item2 = Math.Max(value.Item2, modify_value.Item2);
                }
                {//敌方
                    (int, int) modify_value = (0, 0);
                    (string, string) modify_text;
                    (modify_value, modify_text) = GetTotalPercentModifyValueInfo(ref dirty_tag, enemyChar.GetId(), attackSkillId, enemyPeneFieldId[idx], bodyPart);
                    tmp.Item1 += modify_text.Item1;
                    value.Item1 = Math.Max(value.Item1, modify_value.Item1);
                    tmp.Item2 += modify_text.Item2;
                    value.Item2 = Math.Max(value.Item2, modify_value.Item2);
                }
                //同道指令
                {
                    int cmd = -1;
                    if ((!isResist) && character.GetExecutingTeammateCommand() == 4)
                        cmd = character.GetExecutingTeammateCommand();
                    else if (isResist && character.GetExecutingTeammateCommand() == 5)
                        cmd = character.GetExecutingTeammateCommand();
                    if (cmd >= 0)
                    {
                        int cmdAdd = DomainManager.SpecialEffect.GetModifyValue(_combatDomain.GetMainCharacter(character.IsAlly).GetId(), (ushort)MyAffectedDataFieldIds.TeammateCmdEffect
                                                                            , (sbyte)0, cmd, -1, -1, (sbyte)0);
                        var cmdAddText = ToInfoAdd("同道指令", cmdAdd, 3);
                        //放不下了，就不展开了
                        /*
                        (cmdAdd, cmdAddText) = GetModifyValueInfo(ref dirty_tag, __instance.GetMainCharacter(attacker.IsAlly).GetId()
                                                                ,(ushort)MyAffectedDataFieldIds.TeammateCmdEffect, (sbyte)0, 4, -1, -1, (sbyte)0
                                                                ,3);*/
                        value.Item1 = Math.Max(value.Item1, cmdAdd);
                        tmp.Item1 += cmdAddText;
                    }

                }
                int percent = 100 + value.Item1 + value.Item2;
                result += ToInfoPercent("不叠加乘算", percent, 1);
                result += ToInfoAdd("基础", 100, 2);
                result += ToInfoAdd("加值(取高)", value.Item1, 2);
                result += tmp.Item1;
                result += ToInfoAdd("减值(取低)", value.Item2, 2);
                result += tmp.Item2;
                check_value = check_value * percent / 100;
            }
            check_value /= 100;
            result += ToInfoDivision("显示", 100, 1);
            result += ToInfoNote("仅用于显示，计算时没有除100", 1);
            result += ToInfoAdd("总合校验值",check_value,1);
            if (check_value != target_value/100)
            {
                result += ToInfo("不一致！！！", "", 1);
                result += ToInfoNote("如果没有影响数值的mod，请报数值bug", 1);
            }
            return result;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CombatDomain), "CallMethod")]
        public static bool CombatDomainCallMethodPatch(CombatDomain __instance, int __result,
                    Operation operation, RawDataPool argDataPool, RawDataPool returnDataPool, DataContext context)
        {
            if (!On)
                return true;
            //因为我已经学会熟练使用MethodCall了，就不需要写文件了
            if (operation.MethodId == MY_MAGIC_NUMBER_GetCombatCompareText)
            {
                __result = GameData.Serializer.Serializer.Serialize(Cached_CombatCompareText, returnDataPool);
                return false;
            }
            return true;
        }

    }
}
