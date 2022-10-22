using GameData.Common;
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            HitOrAvoidInts skillHitDistribution = attackSkill.GetHitDistribution();
            //命中
            {
                HitOrAvoidInts characterHitValue = attacker.GetCharacter().GetHitValues();
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
                        //CalcAttackSkillDataCompare完成后计算结果存入attacker.SkillHitValue和____damageCompareData,只有三项,并且顺序和hitType相反                        
                        var result = GetCombatHitOrAvoidInfo(true, hitType==3? attacker.SkillHitValue[0]: attacker.SkillHitValue[2-hitType],
                            hitType,HitTypeNames[hitType], characterHitValue,
                            hitTypeFieldId, enemyHitTypeFieldId,
                            weapon,
                            __instance, attacker, skillId);
                        int idx = hitType % 3;
                        Cached_CombatCompareText[idx] = result;
                    }
            }
            //回避
            {
                HitOrAvoidInts characterHitValue = defender.GetCharacter().GetAvoidValues();
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
                            hitTypeFieldId, enemyHitTypeFieldId,
                            null,
                            __instance, defender, skillId);
                        int idx = hitType % 3 + 3;
                        Cached_CombatCompareText[idx] = result;
                    }
            }
            {
                var bodyPart = attacker.SkillAttackBodyPart;
                Cached_CombatCompareText[6] = GetPenetrateOrResistInfo(false, 0, ____damageCompareData.OuterAttackValue, __instance, attacker, weapon, bodyPart, skillId, attackSkill.GetPenetrations().Outer);
                Cached_CombatCompareText[7] = GetPenetrateOrResistInfo(false, 1, ____damageCompareData.InnerAttackValue, __instance, attacker, weapon, bodyPart, skillId, attackSkill.GetPenetrations().Inner);
                Cached_CombatCompareText[8] = GetPenetrateOrResistInfo(true,0, ____damageCompareData.OuterDefendValue, __instance, defender, weapon, bodyPart, skillId, attackSkill.GetPenetrations().Outer);
                Cached_CombatCompareText[9] = GetPenetrateOrResistInfo(true,1, ____damageCompareData.InnerDefendValue, __instance, defender, weapon, bodyPart, skillId, attackSkill.GetPenetrations().Inner);
            }
            //攻防
        }
        //attacker.GetHitValue(weapon,hitType,attacker.SkillAttackBodyPart,hitValue.Items[hitType], skillId, true)
        //defender.GetAvoidValue(hitType, attacker.SkillAttackBodyPart, skillId, false, true);
        unsafe public static string GetCombatHitOrAvoidInfo(bool isHit,int target_value,
            int hitType,string hitTypeName,
            HitOrAvoidInts characterHitValue,
            List<ushort> hitTypeFieldId,List<ushort> enemyHitTypeFieldId,
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
                var skill_name =GetCombatSkillName(character.GetAffectingMoveSkillId());
                CombatSkill move_skill = DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(id, character.GetAffectingMoveSkillId()));
                HitOrAvoidInts addHitValues = move_skill.GetAddHitValueOnCast();
                total = GlobalConfig.Instance.AgileSkillBaseAddHit * move_skill.GetPracticeLevel() / 100 * move_skill.GetPower() / 100 * addHitValues.Items[hitType] / 100;
                tmp += ToInfoAdd("基础", GlobalConfig.Instance.AgileSkillBaseAddHit, -2);
                tmp += ToInfoPercent("发挥度", move_skill.GetPracticeLevel(), -2);
                tmp += ToInfoPercent("威力", move_skill.GetPower(), -2);
                tmp += ToInfoPercent("功法", addHitValues.Items[hitType], -2);
                result += ToInfoAdd($"身法{skill_name}", total, -1)
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
                tmp += ToInfoPercent("发挥度", defend_skill.GetPracticeLevel(), -2);
                tmp += ToInfoPercent("威力", defend_skill.GetPower(), -2);
                tmp += ToInfoPercent("功法", addAvoidValues.Items[hitType], -2);
                result += ToInfoAdd($"护体{skill_name}", total, -1)
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
                //装备
                if(isHit)//武器
                {
                    CombatSkill attackSkill = DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(character.GetId(), attackSkillId));
                    HitOrAvoidInts skillHitValue = attackSkill.GetHitValue();
                    percent += 100 + skillHitValue.Items[hitType];//由于功法的提示里写的就是100+加成，为了便于理解把基础值和功法加成合到一起
                    tmp += ToInfoAdd($"功法{hitTypeName}", percent, -2);
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
                peneFieldId.Add(MyAffectedDataFieldIds.DefenderPenetrateOuter);
                peneFieldId.Add(MyAffectedDataFieldIds.DefenderPenetrateInner);
                enemyPeneFieldId.Add(MyAffectedDataFieldIds.AttackerPenetrateOuter);
                enemyPeneFieldId.Add(MyAffectedDataFieldIds.AttackerPenetrateInner);
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
                    var factor = isOutter ? addPenetrateResists.Outer : addPenetrateResists.Inner;
                    var value = GlobalConfig.Instance.DefendSkillBaseAddPenetrateResist
                                    * defendSkill.GetPracticeLevel() / 100 
                                    * defendSkill.GetPower() / 100 
                                    * factor / 100;
                    var tmp = "";
                    tmp += ToInfoAdd("基础", GlobalConfig.Instance.DefendSkillBaseAddPenetrateResist, 2);
                    tmp += ToInfoPercent("发挥", defendSkill.GetPracticeLevel(), 2);
                    tmp += ToInfoPercent("威力", defendSkill.GetPower(), 2);
                    tmp += ToInfoPercent("功法", factor, 2);

                    result += ToInfoAdd("护体功法",value,1);
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
                if(!isResist)
                {
                    total_value += skillAddPercent;
                    tmp += ToInfoAdd("摧破功法",total_value,2);
                }
                else
                {
                    tmp += ToInfoAdd("基础", total_value, 2);
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
