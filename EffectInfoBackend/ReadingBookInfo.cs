using GameData.Common;
using GameData.Domains;
using GameData.Domains.Building;
using GameData.Domains.Character;
using GameData.Domains.CombatSkill;
using GameData.Domains.Item;
using GameData.Domains.Taiwu;
using GameData.GameDataBridge;
using GameData.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectInfo
{
    public partial class EffectInfoBackend
    {
        public static readonly ushort MY_MAGIC_NUMBER_GetReadingEfficiency = 6724;
        public static readonly string PATH_GetReadingEfficiency = "\\Mod\\EffectInfo\\Plugins\\Cache_ReadingEfficiency.txt";
        //重载BuildingDomain的CallMethod响应供前端使用
        [HarmonyPrefix, HarmonyPatch(typeof(TaiwuDomain), "CallMethod")]
        public static bool BuildingDomainCallMethodPatch(TaiwuDomain __instance, int __result,
            Operation operation, RawDataPool argDataPool, RawDataPool returnDataPool, DataContext context)
        {
            if (!On)
                return true;
            if (operation.MethodId == MY_MAGIC_NUMBER_GetReadingEfficiency)
            {
                GetReadingEfficiencyInfo(__instance, context);
                __result = -1;//表示无返回值
                return false;
            }
            return true;
        }
        //TaiwuDomain.GetCurrReadingEfficiency
        public static void GetReadingEfficiencyInfo(TaiwuDomain __instance, DataContext context)
        {
            var result = "";
            var _curReadingBook = __instance.GetCurReadingBook();
            if (!_curReadingBook.IsValid())
            {
                result = "无";
            }
            else
            {
                var _readingBooks = GetPrivateValue<Dictionary<ItemKey, ReadingBookStrategies>>(__instance, "_readingBooks");
                ReadingBookStrategies strategies = _readingBooks[_curReadingBook];
                SkillBook book = DomainManager.Item.GetElement_SkillBooks(_curReadingBook.Id);
                byte readingPage;
                bool finished=false;
                if (book.IsCombatSkillBook())
                {
                    short skillTemplateId = book.GetCombatSkillTemplateId();
                    TaiwuCombatSkill combatSkill = CallPrivateMethod<TaiwuCombatSkill>(__instance, "GetTaiwuCombatSkill", new object[] { skillTemplateId });
                    readingPage = __instance.GetCurrentReadingPage(book, strategies, combatSkill);
                    if (readingPage >= 6)
                        finished = true;
                }
                else
                {
                    short skillTemplateId = book.GetLifeSkillTemplateId();
                    TaiwuLifeSkill lifeSkill = CallPrivateMethod<TaiwuLifeSkill>(__instance, "GetTaiwuLifeSkill", new object[] { skillTemplateId });
                    readingPage = __instance.GetCurrentReadingPage(book, strategies, lifeSkill);
                    if (readingPage >= 5)
                        finished = true;
                }
                if (!finished)
                {
                    int check_value = 0;
                    {//GetBaseReadingSpeed
                        var tmp = "";
                        //var value = (int)__instance.GetBaseReadingSpeed(readingPage);                        
                        var books = new List<KeyValuePair<string, int>>();
                        //仅当参考书和当前书是同种功法时，取当前书和参考书在该页的状态(正常、残缺、亡没)的最好状况计算基础速度
                        sbyte incompleteState = SkillBookStateHelper.GetPageIncompleteState(book.GetPageIncompleteState(), readingPage);
                        books.Add(new KeyValuePair<string, int>(book.GetName(), incompleteState));
                        int minIdx = 0;
                        foreach (var refBookKey in __instance.GetReferenceBooks())
                            if (refBookKey.IsValid() && refBookKey.TemplateId == book.GetTemplateId())
                            {
                                SkillBook refBook = DomainManager.Item.GetElement_SkillBooks(refBookKey.Id);
                                sbyte refBookPageState = SkillBookStateHelper.GetPageIncompleteState(refBook.GetPageIncompleteState(), readingPage);
                                books.Add(new KeyValuePair<string, int>(refBook.GetName(), refBookPageState));
                                if (refBookPageState >= 0 && refBookPageState < incompleteState)
                                    minIdx = books.Count - 1;
                            }
                        for (int i = 0; i < books.Count; ++i)
                        {
                            var value = books[i].Value >= 0 ? SkillBookPageIncompleteState.BaseReadingSpeed[(int)books[i].Value] : 0;
                            string status = "完整";
                            //0:完整,1:残缺,?:亡佚
                            if (books[i].Value == 1)
                                status = "残缺";
                            else if (books[i].Value == 2)
                                status = "亡佚";
                            if (i == minIdx)
                            {
                                result += ToInfoAdd($"第{readingPage}页状态(仅同名书有效)", value, -1);
                                tmp += ToInfoAdd($"{books[i].Key}-{status}(最高)", value, -2);
                                check_value = value;
                            }
                            else
                                tmp += ToInfoAdd($"{books[i].Key}-{books[i].Value}", value, -2);
                        }
                        result += tmp;
                    }
                    //GetReadingSpeedBonus
                    int bonus = 100;
                    result += GetReadingSpeedBonusInfo(ref bonus, __instance, readingPage, false);
                    check_value = check_value * bonus / 100;
                    result += ToInfoAdd("总合校验值", check_value, -1);
                    result = $"读书效率:{__instance.GetCurrReadingEfficiency(context)}%\n" + result;
                }
                else
                    result += ToInfo("已读完", "-", -1);
            }
            var path = $"{Directory.GetCurrentDirectory()}\\..{PATH_GetReadingEfficiency}";
            for (int i = 0; i < 5; ++i)
                try
                {
                    File.WriteAllText(path, result);
                    break;
                }
                catch (IOException)
                {
                    AdaptableLog.Info("EffectInfo:Write File Fail,Retrying...");
                    System.Threading.Tasks.Task.Delay(500);
                }
        }
        //check_value不返回
        public static unsafe string GetReadingSpeedBonusInfo(ref int check_value,TaiwuDomain __instance,byte curReadingPage, bool isInBattle = false)
        {
            var result = "";

            var _curReadingBook = __instance.GetCurReadingBook();
            var _readingBooks = GetPrivateValue<Dictionary<ItemKey, ReadingBookStrategies>>(__instance, "_readingBooks");
            ReadingBookStrategies strategies = _readingBooks[_curReadingBook];
            SkillBook book = DomainManager.Item.GetElement_SkillBooks(_curReadingBook.Id);

			int notReadPrePageCount = 0;

            var _taiwuChar = __instance.GetTaiwu();

            int featBonusSpeed = 0;
            var feat_result = "";

            int building_bonus = 0;

            var attainment_result = "";
            short skillAttainment=0;

            string sect_result = "";
            int sect_factor=100;
            if (SkillGroup.FromItemSubType(book.GetItemSubType()) == 0)//技艺书
            {        
				short skillTemplateId = book.GetLifeSkillTemplateId();
				List<GameData.Domains.Character.LifeSkillItem> learnedLifeSkills = _taiwuChar.GetLearnedLifeSkills();
				int learnedIndex = _taiwuChar.FindLearnedLifeSkillIndex(skillTemplateId);
				if (learnedIndex >= 0)
				{
					GameData.Domains.Character.LifeSkillItem skillItem = learnedLifeSkills[learnedIndex];
					for (byte i = 0; i < curReadingPage; i += 1)
                        if (!skillItem.IsPageRead(i))
							notReadPrePageCount++;
				}
				else
					notReadPrePageCount = (int)curReadingPage;

                skillAttainment = _taiwuChar.GetLifeSkillAttainment(book.GetLifeSkillType());
                attainment_result += ToInfoAdd($"{Config.LifeSkillType.Instance[book.GetLifeSkillType()].Name}造诣", skillAttainment, -3);

                if (_taiwuChar.GetFeatureIds().Contains(201))
                {
                    featBonusSpeed = 30;
                    feat_result += ToInfoAdd(Config.CharacterFeature.Instance[201].Name, 30, -3);
                }
				SpecifyBuildingEffect buildingEffect = DomainManager.Building.GetSpecifyBuildingEffect(_taiwuChar.GetLocation());
                if(buildingEffect != null)
                    building_bonus = buildingEffect.AddReadingLifeSkillBookEfficiency.Items[book.GetLifeSkillType()];
            }
			else
			{
				short skillTemplateId = book.GetCombatSkillTemplateId();
				Config.CombatSkillItem skillConfig = Config.CombatSkill.Instance[skillTemplateId];
				byte pageTypes = book.GetPageTypes();
				sbyte behaviorType = SkillBookStateHelper.GetOutlinePageType(pageTypes);
				sbyte direction = SkillBookStateHelper.GetNormalPageType(pageTypes, curReadingPage);
				byte readingInternalIndex = CombatSkillStateHelper.GetPageInternalIndex(behaviorType, direction, curReadingPage);

                if (_taiwuChar.GetLearnedCombatSkills().Contains(skillTemplateId))
				{
					GameData.Domains.CombatSkill.CombatSkill skillItem = DomainManager.CombatSkill.GetElement_CombatSkills(new CombatSkillKey(__instance.GetTaiwuCharId(), skillTemplateId));
					ushort readingState = skillItem.GetReadingState();
					if (CombatSkillStateHelper.IsPageRead(readingState, readingInternalIndex))
					{
                        check_value = 100;
                        result += ToInfoPercent("已读",100,1);
                        return result;
					}
					for (byte j = 0; j < curReadingPage; j += 1)
					{
						byte internalIndex = CombatSkillStateHelper.GetPageInternalIndex(behaviorType, SkillBookStateHelper.GetNormalPageType(pageTypes, j), j);
						if (!CombatSkillStateHelper.IsPageRead(readingState, internalIndex))
							notReadPrePageCount++;
					}
				}
				else
				{
					notReadPrePageCount = (int)curReadingPage;
				}
                //造诣
                {
                    skillAttainment = _taiwuChar.GetCombatSkillAttainment(book.GetCombatSkillType());
                    //造诣不足时尝试从替代的生活技能造诣中取最高
                    {
                        //skillAttainment = __instance.GetAttainmentWithSectApprovalBonus(skillConfig.SectId, skillAttainment, requiredAttainment);
                        short requiredAttainment = Config.SkillGradeData.Instance[book.GetGrade()].ReadingAttainmentRequirement;
                        var orgTemplateId = skillConfig.SectId;
                        short maxAttainment = skillAttainment;

                        var tmp_list = new List<string>();
                        tmp_list.Add(ToInfoAdd($"{Config.CombatSkillType.Instance[book.GetCombatSkillType()].Name}造诣", skillAttainment, -3));
                        int max_idx = 0;

                        if (skillAttainment < requiredAttainment && orgTemplateId != 0)
                        {
                            short settlementId = DomainManager.Organization.GetSettlementIdByOrgTemplateId(orgTemplateId);
                            short sectApprovingRate = DomainManager.Organization.GetElement_Sects(settlementId).CalcApprovingRate();
                            if (sectApprovingRate >= 300)
                            {
                                Config.SectApprovingEffectItem config = Config.SectApprovingEffect.Instance[(int)(orgTemplateId - 1)];
                                LifeSkillShorts attainments = _taiwuChar.GetLifeSkillAttainments();
                                foreach (sbyte lifeSkillType in config.RequirementSubstitutions)
                                {
                                    tmp_list.Add(ToInfoAdd($"{Config.LifeSkillType.Instance[lifeSkillType].Name}造诣", skillAttainment, -3));
                                    if (maxAttainment < attainments.Items[lifeSkillType])
                                    {
                                        maxAttainment = attainments.Items[lifeSkillType];
                                        max_idx = tmp_list.Count - 1;
                                    }
                                }
                            }
                        }
                        for (int i = 0; i < tmp_list.Count; i++)
                            if (tmp_list.Count == 1 || i != max_idx)
                                attainment_result += tmp_list[i];
                            else
                                attainment_result += tmp_list[i].Replace("造诣","造诣(最高)");
                        skillAttainment = maxAttainment;
                    }

                }
                if (_taiwuChar.GetFeatureIds().Contains(202))
                {
                    featBonusSpeed = 30;
                    feat_result += ToInfoAdd(Config.CharacterFeature.Instance[202].Name,30,-3);
                }
                //门派
                sbyte sectId = Config.CombatSkill.Instance[book.GetCombatSkillTemplateId()].SectId;
                sect_result = CalcReadingSpeedSectApprovalFactorInfo(_taiwuChar,out sect_factor, sectId, (sbyte)((curReadingPage == 0) ? direction : -1), (sbyte)(curReadingPage - 1), isInBattle);
			}
            //特性、建筑、参考书
            {
                int refBookBonusSpeed = 0;
                var refBook_result = CalcReferenceBooksBonusSpeedPercentInfo(ref refBookBonusSpeed, __instance, book);

                check_value = 100 + featBonusSpeed + building_bonus + refBookBonusSpeed;
                result += ToInfoAdd("基础", 100, -2);
                result += ToInfoAdd("特性", featBonusSpeed, -2);
                result += feat_result;
                result += ToInfoAdd("建筑", building_bonus, -2);
                //result += building_result;
                result += refBook_result;
            }
            {//造诣
                 //CalcReadingSpeedAttainmentFactor
                int value = (int)Config.SkillGradeData.Instance[book.GetGrade()].ReadingAttainmentRequirement;
                attainment_result += ToInfoDivision("品级", value, -3);
                value = 100 * (int)skillAttainment / value;
                attainment_result += ToInfoMulti("倍率", 100, -3);

                if (value > 1000)
                {
                    value = 1000;
                    attainment_result += ToInfo("上限", "<=1000", -3);
                }
                if (value < 10)
                {
                    value = 10;
                    attainment_result += ToInfo("下限", ">=10", -3);
                }
                attainment_result = ToInfoPercent("造诣品级", value, -2) + attainment_result;

                check_value = check_value * value / 100;
                result += attainment_result;
            }
            {
                //strategy
                int strategy_factor = 0;
                var strategy_result= GetPageReadingEfficiencyBonusInfo(ref strategy_factor, strategies,curReadingPage);
                check_value = check_value * strategy_factor/100;
            }
            {
                //门派
                check_value = check_value * sect_factor / 100;
                result += sect_result;
            }
            //未读页
            for (int k = 0; k < notReadPrePageCount; k++)
			{
                check_value = check_value / 2;
                result += ToInfoDivision("未读",2,-2);
			}
            if(check_value<0)
            {
                check_value = 0;
                result += ToInfoMin("下限", 0, -2);
            }
            result = ToInfoPercent("效率加成",check_value,-1)+result;
            return result;
        }
        public static string CalcReadingSpeedSectApprovalFactorInfo(Character _taiwuChar, out int factor,sbyte orgTemplateId, sbyte combatSkillDirection, sbyte pageId, bool isInBattle)
        {
            var result = "";
            if (orgTemplateId == 0)
            {
                factor = 100;
                result += ToInfoPercent("无门派",factor,-2);
                return result;
            }
            short settlementId = DomainManager.Organization.GetSettlementIdByOrgTemplateId(orgTemplateId);
            short sectApprovingRate = DomainManager.Organization.GetElement_Sects(settlementId).CalcApprovingRate();
            if (sectApprovingRate >= 300)//门派支持
            {
                Config.SectApprovingEffectItem config = Config.SectApprovingEffect.Instance[orgTemplateId - 1];
                //性格
                short behaviorTypeBonus = config.BehaviorTypeBonuses[_taiwuChar.GetBehaviorType()];
                result += ToInfoMulti("性格", behaviorTypeBonus, -3);
                //字决(用/修等)
                int directionBonus;
                if(combatSkillDirection>=0)
                {
                    directionBonus = config.CombatSkillDirectionBonuses[combatSkillDirection];
                    result += ToInfoMulti("字诀",directionBonus,-3);
                }
                else
                {
                    directionBonus = 100;
                    result += ToInfoMulti("总纲", directionBonus, -3);
                }
                //性别
                int genderBonus = ((pageId < 1) ? 100 : ((_taiwuChar.GetGender() == 1) ? config.PageBonusesOfMale[pageId - 1] : config.PageBonusesOfFemale[pageId - 1]));
                result += ToInfoMulti("性别", genderBonus, -3);

                factor = behaviorTypeBonus * directionBonus * genderBonus / 10000;
                result += ToInfo("倍率","/10000",-3);

                if (isInBattle)
                {
                    int battleGenderBonus = ((pageId < 1) ? 100 : ((_taiwuChar.GetGender() == 1) ? config.ActualCombatBonusOfMale : config.ActualCombatBonusOfFemale));
                    factor = factor * battleGenderBonus / 100;
                    result += ToInfoPercent("战斗性别奖励", battleGenderBonus, -3);
                }
            }
            else
            {
                factor = 100;
                result += ToInfoPercent("门派支持<300", factor, -3);
            }
            if (sectApprovingRate >= 400)
            {
                factor = factor * 125 / 100;
                result += ToInfoPercent("门派支持>400", 125, -3);
            }
            if (sectApprovingRate >= 600)
            {
                factor = factor * 125 / 100;
                result += ToInfoPercent("门派支持>600", 125, -3);
            }
            result =ToInfoPercent("门派",factor,-2)+ result;
            return result;
        }
        public static string CalcReferenceBooksBonusSpeedPercentInfo(ref int refBonusSpeed, TaiwuDomain __instance,GameData.Domains.Item.SkillBook book)
        {
            var result = "";

            refBonusSpeed = 20;
            result += ToInfoAdd("基础",20,-3);

            sbyte bookSkillType = ((book.GetItemSubType() == 1000) ? book.GetLifeSkillType() : book.GetCombatSkillType());
            List<short> bonusRefBookIds = book.GetReferenceBooksWithBonus();
            ItemKey[] referenceBooks = __instance.GetReferenceBooks();
            for (int i = 0; i < referenceBooks.Length; i++)
            {
                ItemKey refBookKey = referenceBooks[i];
                if (refBookKey.IsValid())
                {
                    GameData.Domains.Item.SkillBook refBook = DomainManager.Item.GetElement_SkillBooks(refBookKey.Id);
                    sbyte refBookGrade = refBook.GetGrade();
                    refBonusSpeed += (refBookGrade + 1) * 40;
                    result += ToInfoAdd("品级:(品级+1)*40", (refBookGrade + 1) * 40, -3);

                    sbyte refBookSkillType = ((refBook.GetItemSubType() == 1000) ? refBook.GetLifeSkillType() : refBook.GetCombatSkillType());
                    if (bonusRefBookIds != null && bonusRefBookIds.Contains(refBookKey.TemplateId))
                    {
                        refBonusSpeed += (refBookGrade + 1) * 20;
                        result += ToInfoAdd("奖励类型:(品级+1)*20", (refBookGrade + 1) * 20, -3);
                    }
                    if (refBook.GetItemSubType() == book.GetItemSubType() && refBookSkillType == bookSkillType)
                    {
                        refBonusSpeed += (refBookGrade + 1) * 40;
                        result += ToInfoAdd("同类型:(品级+1)*40", (refBookGrade + 1) * 40, -3);
                    }
                }
            }
            if(refBonusSpeed<0)
            {
                refBonusSpeed = 0;
                result += ToInfoMin("下限",0,-3);
            }
            result = ToInfoAdd("参考书", refBonusSpeed,-2) +result;
            return result;
        }

        public static unsafe string GetPageReadingEfficiencyBonusInfo(ref int efficiencyBonus, ReadingBookStrategies __instance,byte pageIndex)
        {
            var result = "";
            efficiencyBonus = 100;
            result += ToInfoAdd("基础", 100, -3);
            int curPageStartIndex = pageIndex * 3;
            for (int j = 0; j < curPageStartIndex; j++)
            {
                sbyte strategyId = __instance.StrategyIds[j];
                if (strategyId >= 0)
                {
                    if (strategyId >= Config.ReadingStrategy.Instance.Count)
                        return result;
                    var config = Config.ReadingStrategy.Instance[strategyId];
                    efficiencyBonus += config.FollowingPagesEfficiencyChange;
                    result += ToInfoAdd(config.Name, config.FollowingPagesEfficiencyChange, -3);
                }
            }
            for (int i = curPageStartIndex; i < curPageStartIndex + 3; i++)
            {
                sbyte strategyId = __instance.StrategyIds[i];
                if (strategyId >= Config.ReadingStrategy.Instance.Count)
                    return result;
                if (strategyId >= 0)
                {
                    efficiencyBonus += __instance.Bonus[i];
                    result += ToInfoAdd(Config.ReadingStrategy.Instance[strategyId].Name, __instance.Bonus[i], -3);
                }
            }
            result =ToInfoPercent("策略",efficiencyBonus,-2)+result;
            return result;
        }


    }
}
