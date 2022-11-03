using GameData.Common;
using GameData.Domains;
using GameData.Domains.Building;
using GameData.Domains.Character;
using GameData.Domains.Item;
using GameData.Domains.Item.Display;
using GameData.Domains.Map;
using GameData.Domains.Organization;
using GameData.Domains.Organization.Display;
using GameData.Domains.SpecialEffect;
using GameData.GameDataBridge;
using GameData.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaiwuModdingLib.Core.Plugin;

namespace EffectInfo
{
    partial class EffectInfoBackend
    {
        public static readonly ushort MY_MAGIC_NUMBER_GetResourceOutput = 6723;
        public static readonly ushort MY_MAGIC_NUMBER_GetShopOutput = 6728;
        //重载BuildingDomain的CallMethod响应供前端使用
        //如果前端有回调，需要返回0而非-1
        [HarmonyPrefix, HarmonyPatch(typeof(BuildingDomain), "CallMethod")]
        public static bool BuildingDomainCallMethodPatch(BuildingDomain __instance,ref int __result,
            Operation operation, RawDataPool argDataPool, RawDataPool returnDataPool, DataContext context)
        {
            if(!On)
                return true;
            if(operation.MethodId== MY_MAGIC_NUMBER_GetResourceOutput)
            {
                int argsOffset = operation.ArgsOffset;
                int argsCount = operation.ArgsCount;
                if (argsCount != 1)
                {
                    AdaptableLog.Info("Effect Info:Unknown Fatal Error");
                    __result = 0;
                    return false;//由于是自定义key，不继续执行原函数
                }
                BuildingBlockKey para1 = default(BuildingBlockKey);
                argsOffset += GameData.Serializer.Serializer.Deserialize(argDataPool, argsOffset, ref para1);
                var text=GetBuildingResourceOutputInfo(__instance,para1);
                __result = GameData.Serializer.Serializer.Serialize(text, returnDataPool);
                return false;
            }
            else if (operation.MethodId == MY_MAGIC_NUMBER_GetShopOutput)
            {
                int argsOffset = operation.ArgsOffset;
                int argsCount = operation.ArgsCount;
                if (argsCount != 1)
                {
                    AdaptableLog.Info("Effect Info:Unknown Fatal Error");
                    __result = 0;//表示无返回值、可回调
                    return false;//由于是自定义key，不继续执行原函数
                }
                BuildingBlockKey para1 = default(BuildingBlockKey);
                argsOffset += GameData.Serializer.Serializer.Deserialize(argDataPool, argsOffset, ref para1);
                var text=GetBuildingShopOutputInfo(__instance, para1);
                __result = GameData.Serializer.Serializer.Serialize(text, returnDataPool);
                return false;
            }
            return true;
        }
        //BuildingDomain.CalcResourceOutput
        //同一建筑产出不同种类资源(食物、木材等)用的公式是一样的，GetCollectBuildingResourceType返回的是Config中的常量而非根据界面上的选择变化？
        public static string GetBuildingResourceOutputInfo(BuildingDomain __instance, BuildingBlockKey blockKey)
        {
            var result = "";
            int check_value = 0;
            BuildingBlockData blockData;
            if (__instance.TryGetElement_BuildingBlocks(blockKey, out blockData))
            {
                int total_attainment = 0;
                var tmp = "";
                {
                    sbyte resourceType = __instance.GetCollectBuildingResourceType(blockKey);
                    sbyte lifeSkillType = __instance.GetLifeSkillByResourceType(resourceType);
                    var lifeSkillName = Config.LifeSkillType.Instance[lifeSkillType].Name;
                    CharacterList managerList;
                    DomainManager.Building.TryGetElement_ShopManagerDict(blockKey, out managerList);
                    for (int i = 0; i < managerList.GetCount(); i++)
                    {
                        int charId = managerList.GetCollection()[i];
                        if (charId >= 0 && DomainManager.Taiwu.CanWork(charId))
                        {

                            GameData.Domains.Character.Character manageChar = DomainManager.Character.GetElement_Objects(charId);
                            var name = (CharacterDomain.GetRealName(manageChar)).surname;
                            var value = __instance.BaseWorkContribution + manageChar.GetLifeSkillAttainment(lifeSkillType);
                            total_attainment += value;
                            tmp += ToInfoAdd($"{name}:{lifeSkillName}+{__instance.BaseWorkContribution}", value, -3);
                        }
                        else if(charId >= 0)
                            tmp += ToInfo("本月无法工作", "-", -3);
                    }
                    tmp += ToInfoMulti("倍率", 0.01, -3);
                    //此处CalcResourceChangeFunc的isAverage参数恒为false
                }
                if (total_attainment == 0)
                {
                    result += ToInfo("无产出", "=0", -1);//0产出时不加上tmp
                    check_value = 0;
                }
                else
                {
                    double tmp_check_value = 1.0 + 0.01 * (double)total_attainment;
                    result += ToInfoMulti("造诣效率", tmp_check_value, -1);
                    result += ToInfoAdd("基础", 1.0, -2);
                    result += ToInfoAdd("总造诣", 0.01 * (double)total_attainment, -2);
                    result += tmp;
                    tmp_check_value *= (double)(blockData.Level + 10);
                    result += ToInfoMulti("建筑等级+10", (double)(blockData.Level + 10), -1);

                    {//常数
                        double factor = (double)__instance.CalcResourceChangeFactor(blockData);
                        tmp_check_value *= factor;
                        result += ToInfoMulti("资源点系数", factor, -1);
                    }
                    //取整
                    check_value = (int)tmp_check_value;
                    {//周围资源点个数
                        int resource_block_count = 0;
                        Location location = DomainManager.Taiwu.GetTaiwuVillageLocation();
                        if (blockData.TemplateId >= 0)
                        {
                            Config.BuildingBlockItem configData = Config.BuildingBlock.Instance[blockData.TemplateId];
                            BuildingAreaData areaData = DomainManager.Building.GetElement_BuildingAreas(location);

                            List<short> neighborList = new List<short>();
                            areaData.GetNeighborBlocks(blockKey.BuildingBlockIndex, configData.Width, neighborList);
                            for (int i = 0; i < neighborList.Count; i++)
                            {
                                BuildingBlockKey tmp_blockKey = new BuildingBlockKey(location.AreaId, location.BlockId, neighborList[i]);
                                BuildingBlockData neighborBlockData;
                                if (__instance.TryGetElement_BuildingBlocks(tmp_blockKey, out neighborBlockData) && configData.DependBuildings.Contains(neighborBlockData.TemplateId))
                                    resource_block_count++;
                            }
                        }
                        result += ToInfoMulti("资源点个数",resource_block_count, -1);
                        check_value *= resource_block_count;
                    }
                    result += ToInfoAdd("总合校验值", check_value, -1);
                }
            }
            return result;
        }
        public unsafe static string GetCultureOrSafteyInfo(out int total,Config.BuildingBlockItem config)
        {
            var tmp = "";
            total = 100;
            if (config.RequireSafety != 0 || config.RequireCulture != 0)
            {
                tmp += ToInfoAdd("基础", 100,-3);
                List<SettlementDisplayData> settlements = DomainManager.Taiwu.GetAllVisitedSettlements();
                int require = 0;
                bool need_above_require = false;
                string require_name = "";
                if (config.RequireSafety != 0)
                {
                    if (settlements.Count > 10)
                        settlements.Sort((SettlementDisplayData l, SettlementDisplayData r) => DomainManager.Organization.GetSettlement(  (short)r.SettlementId).GetSafety() - DomainManager.Organization.GetSettlement((short)l.SettlementId).GetSafety() );
                    if(config.RequireSafety<0)
                        settlements.Reverse();
                    require = Math.Abs(config.RequireSafety);
                    need_above_require = config.RequireSafety > 0;
                    require_name = "安定";
                }
                else
                {
                    if (settlements.Count > 10)
                        settlements.Sort((SettlementDisplayData l, SettlementDisplayData r) => DomainManager.Organization.GetSettlement((short)r.SettlementId).GetCulture() - DomainManager.Organization.GetSettlement((short)l.SettlementId).GetCulture());
                    if (config.RequireCulture < 0)
                        settlements.Reverse();
                    require = Math.Abs(config.RequireCulture);
                    need_above_require = config.RequireCulture > 0;
                    require_name = "文化";
                }
                for (int i = 0; i < Math.Min(10, settlements.Count); i++)
                    if (settlements[i].SettlementId >= 0)
                    {
                        Settlement settlement = DomainManager.Organization.GetSettlement((short)settlements[i].SettlementId);
                        var settlement_name = "";
                        {
                            var location = settlement.GetLocation();
                            if (location.AreaId > 0)
                            {
                                //Config.MapState.Instance[s_id].Name 省份
                                //areaData.GetConfig().Name 地区
                                //因为太长就不显示省份了
                                var areaData = DomainManager.Map.GetElement_Areas(location.AreaId);
                                //var s_id = areaData.GetConfig().StateID;
                                //settlement_name = $"{Config.MapState.Instance[s_id].Name}-{areaData.GetConfig().Name}";
                                settlement_name = $"{areaData.GetConfig().Name}";
                            }
                            var org_id = settlement.GetOrgTemplateId();
                            if (org_id > 0)//市镇
                                settlement_name += $"-{Config.Organization.Instance[org_id].Name}";
                            if (settlement_name == "")
                                settlement_name = "未知";
                        }
                        var settlement_property = 0;
                        if (config.RequireSafety != 0)//Safety判断在前
                            settlement_property = settlement.GetSafety();
                        else
                            settlement_property = settlement.GetCulture();
                        var value = Math.Abs(settlement_property - require) / 5 + 5;
                        if (need_above_require && settlement_property > require)
                        {
                            total += value;
                            tmp += ToInfoAdd($"{settlement_name}({settlement_property}-{require})/5+5", value, -3);
                        }
                        else if (need_above_require == false && settlement_property < require)
                        {
                            total += value;
                            tmp += ToInfoAdd($"{settlement_name}({require}-{settlement_property})/5+5", value, -3);
                        }
                        else //无效
                            tmp += ToInfoAdd($"{settlement_name}{settlement_property}(不计)", 0, -3);
                    }
                var sign_str = need_above_require ? ">" : "<";
                var direction_str = need_above_require ? "前" : "后";
                if (config.RequireSafety != 0)
                    tmp = ToInfoPercent($"{require_name}{direction_str}10城镇({sign_str}{require}有效)", total, -2) + tmp;
                else
                    tmp = ToInfoPercent($"{require_name}{direction_str}10城镇({sign_str}{require}有效)", total, -2) + tmp;
            }
            return tmp;
        }

        //经营产出,实际是个整数，达到上限时产出一个物品
        public unsafe static string GetBuildingShopOutputInfo(BuildingDomain __instance, BuildingBlockKey blockKey)
        {
            var result = "";
            int check_value = 0;
            //注意区分GetBuildingAttainment\GetAttainmentOfBuilding\GetShopBuildingAttainment\GetShopBuildingMaxAttainment
            //可以产出多种资源的建筑，产出不同造诣时，使用的资质不同
            //这些建筑切换产出的资源，也           
            int shop_period = -1;
            BuildingBlockData blockData;
            if (__instance.TryGetElement_BuildingBlocks(blockKey, out blockData))
            {
                //经营进度 SerialUpdate、OfflineUpdateShopManagement
                var tmp = "";
                Config.BuildingBlockItem config = Config.BuildingBlock.Instance[blockData.TemplateId];
                bool use_max_combat_attainment = false;
                if (config.DependBuildings.Count > 0 && config.DependBuildings[0] == MyBuildingBlockDefKey.KungfuPracticeRoom && config.IsShop)//练功房附属建筑使用最大武学造诣
                    use_max_combat_attainment = true;
                //造诣 GetAttainmentOfBuilding(资源的记忆)或GetShopBuildingAttainment(建筑的技艺)
                {
                    sbyte lifeSkillType = Config.BuildingBlock.Instance[blockData.TemplateId].RequireLifeSkillType;
                    if (config.IsCollectResourceBuilding)
                    {
                        //产出资源(resourceType<6即常规六种资源)的经营建筑，使用产出的资源的技艺类型而非建筑的记忆类型(SerialUpdate->UpdateResourceBlock)
                        //官方前端统统使用的GetBuildingAttainment，即使用建筑的造诣显示，因此官方显示的效率可能不正确
                        sbyte resourceType = __instance.GetCollectBuildingResourceType(blockKey);
                        sbyte getResourceType = (sbyte)((resourceType < 6) ? resourceType : 5);
                        lifeSkillType = (sbyte)((resourceType < 6) ? Config.ResourceType.Instance[resourceType].LifeSkillType : 9);
                    }
                    var lifeSkillName = Config.LifeSkillType.Instance[lifeSkillType].Name;
                    CharacterList managerList;
                    DomainManager.Building.TryGetElement_ShopManagerDict(blockKey, out managerList);
                    for (int i = 0; i < managerList.GetCount(); i++)
                    {
                        int charId = managerList.GetCollection()[i];
                        if (charId >= 0 && DomainManager.Taiwu.CanWork(charId))
                        {
                            GameData.Domains.Character.Character manageChar = DomainManager.Character.GetElement_Objects(charId);
                            var name = (CharacterDomain.GetRealName(manageChar)).surname;
                            if (use_max_combat_attainment)
                            {
                                var _skillname = "";
                                var max_attainment = 0;
                                for (sbyte _skillType=0;_skillType<14;++_skillType)//醉了，被官方bug误导越界了，这里是武学造诣，应该是14
                                {
                                    var value = manageChar.GetCombatSkillAttainment(_skillType);
                                    if(value > max_attainment)
                                    {
                                        max_attainment=value;
                                        _skillname= Config.CombatSkillType.Instance[_skillType].Name;
                                    }
                                }
                                max_attainment += __instance.BaseWorkContribution;
                                check_value += max_attainment;
                                tmp += ToInfoAdd($"{name}:{_skillname}+{__instance.BaseWorkContribution}", max_attainment, -2);
                            }
                            else
                            {
                                var value = __instance.BaseWorkContribution + manageChar.GetLifeSkillAttainment(lifeSkillType);
                                check_value += value;
                                tmp += ToInfoAdd($"{name}:{lifeSkillName}+{__instance.BaseWorkContribution}", value, -2);
                            }
                        }
                        else if(charId >= 0)
                            tmp += ToInfo("本月无法工作", "-", -3);
                    }
                    //此处isAverage恒为false
                }
                tmp = ToInfoAdd("总造诣", check_value, -1)+tmp;
                if(blockData.TemplateId==MyBuildingBlockDefKey.BookCollectionRoom)//藏书阁的总进度是按书计算
                {
                    BuildingEarningsData earning_data=null;
                    bool canFix = false;
                    if(__instance.TryGetElement_CollectBuildingEarningsData(blockKey, out earning_data)
                        &&earning_data!=null
                        &&earning_data.FixBookInfoList.Count>0
                        &&earning_data.FixBookInfoList[0].IsValid())
                    {
                        ItemKey itemKey = earning_data.FixBookInfoList[0];
                        SkillBookPageDisplayData displyData = DomainManager.Item.GetSkillBookPagesInfo(itemKey);
                        if (displyData.CanFix())
                        {
                            canFix = true;
                            short needProgress = displyData.GetFixProgress().Item2;
                            var grade=ItemTemplateHelper.GetGrade(itemKey.ItemType, itemKey.TemplateId);
                            tmp += ToInfoDivision($"产出需求({9-grade}品)", needProgress, -1);
                            double double_check_value = (double)100.0 * (double)check_value / needProgress;
                            result = tmp + ToInfo("校验值(工作效率)", $"{double_check_value.ToString("f2")}%", -1);
                            if (check_value > 0)
                            {
                                shop_period = (needProgress + check_value - 1) / check_value;//向上取整
                                result += ToInfoNote("由于溢出进度不保留,效率会浪费一部分", -1)
                                         + ToInfoNote($"等效效率:{((double)100 / shop_period).ToString("f2")}%", -1);
                            }
                        }
                    }
                    if(!canFix)
                        result += tmp + ToInfo("校验值(工作效率)","-",-1)
                                + ToInfoNote("和书的品级有关", -2);
                }
                else
                {
                    tmp += ToInfoDivision("建筑产出需求", config.MaxProduceValue, -1);
                    double double_check_value = (double)100.0 * (double)check_value / config.MaxProduceValue;
                    result = tmp + ToInfo("校验值(工作效率)", $"{double_check_value.ToString("f2")}%", -1);
                    //每回合最多收获一次且溢出清灵
                    if (check_value > 0)
                    {
                        shop_period = (config.MaxProduceValue + check_value - 1) / check_value;//向上取整
                        result += ToInfoNote("由于溢出进度不保留,效率会浪费一部分", -1)
                                 + ToInfoNote($"等效效率:{((double)100 / shop_period).ToString("f2")}%", -1);
                    }
                }
                result += "\n";
                //成功率和基础产出
                if(config.SuccesEvent.Count > 0)
                {
                    Config.ShopEventItem shopEventConfig = Config.ShopEvent.Instance[config.SuccesEvent[0]];
                    var _shopManagerDict = GetPrivateValue<Dictionary<BuildingBlockKey, CharacterList>>(__instance, "_shopManagerDict");
                    if (blockData.TemplateId!=MyBuildingBlockDefKey.BookCollectionRoom)
                        if(config.IsShop&&_shopManagerDict.ContainsKey(blockKey))
                        {
                            //正常情况下下面的所有GetAttainmentOfBuilding/GetShopBuildingAttainment的结果应该跟计算效率时的总造诣一致，只是做了平均，保险起见仍然调用游戏内api获得造诣
                            if (shopEventConfig.ResourceGoods != -1)//直接收入资源类型的建筑.ResourceGoods=6为金钱，=7为威望
                            {
                                int success_rate = 0;
                                if (blockData.TemplateId == MyBuildingBlockDefKey.GamblingHouse)
                                {
                                    {//成功率
                                        tmp = "";
                                        int maxPersonalities = 0;
                                        CharacterList managerList;
                                        DomainManager.Building.TryGetElement_ShopManagerDict(blockKey, out managerList);
                                        for (int i = 0; i < managerList.GetCount(); i++)
                                        {
                                            int charId = managerList.GetCollection()[i];
                                            if (charId >= 0 && DomainManager.Taiwu.CanWork(charId))
                                            {
                                                GameData.Domains.Character.Character manageChar = DomainManager.Character.GetElement_Objects(charId);
                                                Personalities personalities = manageChar.GetPersonalities();
                                                int sum = 0;
                                                for (int j = 0; j < 7; j++)
                                                    sum += personalities.Items[j];
                                                if (sum > maxPersonalities)
                                                    maxPersonalities = sum;
                                                tmp += ToInfoAdd($"七元加和",sum,-3);
                                            }
                                            else if(charId >= 0)
                                                tmp += ToInfo("本月无法工作", "-", -3);
                                        }
                                        tmp = ToInfoAdd("最大七元",maxPersonalities,-2)+tmp;
                                        tmp += ToInfoDivision("倍率", 5,-2);
                                        success_rate = Math.Clamp(maxPersonalities / 5,0,100);
                                        result += ToInfoPercent("成功率",maxPersonalities/5,-1)+tmp;
                                    }
                                    result += "\n";
                                }
                                else if (blockData.TemplateId == MyBuildingBlockDefKey.Brothel)//青楼
                                {
                                    {//成功率
                                        tmp = "";
                                        int maxAttraction = 0;
                                        CharacterList managerList;
                                        DomainManager.Building.TryGetElement_ShopManagerDict(blockKey, out managerList);
                                        for (int i = 0; i < managerList.GetCount(); i++)
                                        {
                                            int charId = managerList.GetCollection()[i];
                                            if (charId >= 0 && DomainManager.Taiwu.CanWork(charId))
                                            {
                                                GameData.Domains.Character.Character manageChar = DomainManager.Character.GetElement_Objects(charId);
                                                Personalities personalities = manageChar.GetPersonalities();
                                                int attraction = manageChar.GetAttraction();
                                                if (attraction > maxAttraction)
                                                    maxAttraction = attraction;
                                                tmp += ToInfoAdd($"魅力", attraction, -3);
                                            }
                                            else if(charId >= 0)
                                                tmp += ToInfo("本月无法工作", "-", -3);
                                        }
                                        tmp = ToInfoAdd("最大魅力", maxAttraction, -2) + tmp;
                                        tmp += ToInfoDivision("倍率", 20, -2);
                                        success_rate=Math.Clamp(maxAttraction/20,0,100);
                                        result += ToInfoPercent("成功率", success_rate, -1) + tmp;
                                    }
                                    result += "\n";                                  
                                }
                                else
                                {
                                    //注意此处的平均是除mangerList.GetCount,即可能的managers数量，而非已有的manager数量，即总是除3
                                    int attainment_avg = __instance.GetBuildingAttainment(blockData, blockKey, true);
                                    {//成功率
                                        tmp = "";
                                        CharacterList managerList;
                                        DomainManager.Building.TryGetElement_ShopManagerDict(blockKey, out managerList);
                                        tmp = ToInfoAdd($"总造诣/{managerList.GetCount()}", attainment_avg, -2) + tmp;
                                        tmp += ToInfoDivision("倍率", 3, -2);
                                        success_rate=Math.Clamp(attainment_avg/3,0,100);
                                        result += ToInfoPercent("成功率", success_rate, -1) + tmp;
                                    }
                                    result += "\n";
                                }
                                //产出
                                {
                                    int attainment_total = __instance.GetBuildingAttainment(blockData, blockKey, false);
                                    var base_value = 1;
                                    tmp = "";
                                    base_value *= blockData.Level;
                                    tmp += ToInfoMulti("建筑等级", blockData.Level, -2);
                                    base_value *= attainment_total / 2 + 100;
                                    tmp += ToInfoMulti("总合造诣/2+100", attainment_total / 2 + 100, -2);
                                    //已访问城镇的安定/文化加成
                                    int settlement_total = 0;
                                    tmp += GetCultureOrSafteyInfo(out settlement_total, config);
                                    base_value *= settlement_total / 100;
                                    if (shopEventConfig.ResourceGoods == 7)//威望
                                    {
                                        base_value /=10;
                                        tmp += ToInfoDivision("倍率(威望)", 10, -2);
                                    }
                                    tmp += ToInfo("随机", "×80%~150%", -2);
                                    double expect = 0;
                                    if (blockData.TemplateId == MyBuildingBlockDefKey.GamblingHouse)
                                    {
                                        tmp += ToInfoMulti("如果成功", 3, -2);
                                        tmp += ToInfoDivision("如果失败", 3, -2);
                                        result += ToInfo("收入", $"{base_value * 80/3 / 100}~{base_value * 150 /3/ 100}/{base_value * 80*3 / 100}~{base_value * 150*3 / 100}", -1) + tmp;
                                        expect = base_value * (150 + 80) / 2 / 100 * ((double)success_rate*3+(100-success_rate)/3) / 100 / shop_period;
                                    }
                                    else if(blockData.TemplateId == MyBuildingBlockDefKey.Brothel)
                                    {
                                        tmp += ToInfoMulti("如果成功", 2, -2);
                                        tmp += ToInfoDivision("如果失败", 2, -2);
                                        result += ToInfo("收入", $"{base_value * 80 / 2/ 100}~{base_value * 150 / 2 / 100}/{base_value * 80*2 / 100}~{base_value * 150*2 / 100}", -1) + tmp;
                                        expect = base_value * (150 + 80) / 2 / 100 * ((double)success_rate * 2 + (100 - success_rate) / 2) / 100 / shop_period;
                                    }
                                    else
                                    {
                                        tmp += ToInfoMulti("如果成功", 1, -2);
                                        tmp += ToInfoMulti("如果失败", 0, -2);
                                        result += ToInfo("收入", $"0/{base_value * 80 / 100}~{base_value * 150 / 100}", -1) + tmp;
                                        expect = base_value * (150 + 80) / 2 / 100 * success_rate / 100 / shop_period;
                                    }
                                    if(expect>0)
                                    {
                                        result += "\n";
                                        result += ToInfoAdd("每月收入期望", expect, -1);
                                    }
                                }
                            }
                            else if(shopEventConfig.ItemList.Count > 0)//ItemList:宝井等资源建筑，ItemGradeProbList:不知道是什么
                            {
                                tmp = "";
                                double fail_chance=100.0;
                                int resourceAttainment;
                                if (config.IsCollectResourceBuilding)
                                    resourceAttainment = __instance.GetAttainmentOfBuilding(blockKey, true);//此处用的是/3的
                                else
                                    resourceAttainment = __instance.GetShopBuildingAttainment(blockData, blockKey, true);
                                int from = 0;
                                int end = shopEventConfig.ItemList.Count;
                                if (shopEventConfig.ResourceList.Count > 1)
                                {
                                    sbyte resourceType = __instance.GetCollectBuildingResourceType(blockKey);
                                    if (resourceType == shopEventConfig.ResourceList[0])
                                        end = shopEventConfig.ItemList.Count / 2 - 1;
                                    else
                                        from = shopEventConfig.ItemList.Count / 2;
                                }
                                var base_chance = (int)(blockData.Level * 2) + resourceAttainment / (int)__instance.AttainmentToProb;
                                tmp += ToInfo("基础概率",$"{base_chance}%",-2);
                                tmp += ToInfoNote("每个物品的相对概率=基础概率+物品概率加成", -2);
                                tmp += ToInfoAdd("造诣总合/3", resourceAttainment, -3);
                                tmp += ToInfoDivision("倍率", __instance.AttainmentToProb, -3);
                                tmp += ToInfoAdd("建筑等级*2", blockData.Level * 2, -3);

                                //药房的物品很多而且几率相同,为防止超出窗口合并
                                var itemLists = new Dictionary<int, string>();//Amount到物品名的映射
                                for (int i = from; i < end; i++)
                                {
                                    int item_chance = shopEventConfig.ItemList[i].Amount;
                                    int prob = Math.Clamp(item_chance + base_chance, 0, 100);

                                    short template_id = shopEventConfig.ItemList[i].TemplateId;
                                    sbyte type = shopEventConfig.ItemList[0].Type;//游戏代码type就是取的Item0，不知道是不是bug
                                    var item_name = ItemTemplateHelper.GetName(type, template_id);
                                    var item_grade = ItemTemplateHelper.GetGrade(type, template_id);
                                    var sign_str = shopEventConfig.ItemList[i].Amount >= 0 ? "+" : "";
                                    if(!itemLists.ContainsKey(item_chance))
                                        itemLists.Add(item_chance, $"{9 - item_grade}品-");
                                    itemLists[item_chance] +=$"{item_name}|";
                                    fail_chance *= 1 - (double)prob / 100.0;
                                }
                                foreach (var item_chance in itemLists.Keys)//去掉末尾的|
                                    itemLists[item_chance] = itemLists[item_chance].Substring(0, itemLists[item_chance].Length - 1);
                                if(itemLists.Count==end-from&&itemLists.Count<=6)//如果每种概率的物品只有一个且数量少，就计算绝对概率
                                {
                                    //想不通概率怎么算，只能枚举了
                                    int bits = itemLists.Count;
                                    int max_bit_flag = 1 << bits;
                                    var item_add_chances = itemLists.Keys.ToList<int>();//物品的amount,按itemLists.Keys顺序
                                    var item_abs_chances = new List<double>();//物品的绝对概率，同上顺序
                                    for (int i = 0; i < bits; i++)
                                        item_abs_chances.Add(0);
                                    for (int bit_flags=0;bit_flags<max_bit_flag;++bit_flags)
                                    {
                                        double situation_chance = 1.0;//该种情况的概率
                                        int ct = 0;
                                        for(int i=0;i<bits;++i)
                                        {
                                            var relative_chance= Math.Clamp(item_add_chances[i] + base_chance, 0, 100);
                                            situation_chance *= ((bit_flags >> i) & 1)==1 ? relative_chance : (100 - relative_chance);
                                            situation_chance /= (double)100;
                                            ct += (bit_flags >> i) & 1;
                                        }
                                        situation_chance /= ct;
                                        for (int i = 0; i < bits; ++i)//均分到每个物品的上
                                            if(((bit_flags >> i) & 1) == 1)
                                                item_abs_chances[i] += situation_chance;
                                    }
                                    for (int i = 0; i < bits; i++)
                                    {
                                        var amount=item_add_chances[i];
                                        var name = itemLists[amount];
                                        var relative_chance= Math.Clamp(amount + base_chance, 0, 100);
                                        double abs_chance =item_abs_chances[i]*100;
                                        if(relative_chance==0)
                                            tmp += ToInfo($"{name}({ToStringSign(amount)}%)", $"×0(0)%", -2);
                                        else
                                            tmp += ToInfo($"{name}({ToStringSign(amount)}%)", $"×{relative_chance}({abs_chance.ToString("f2")})%", -2);
                                    }
                                    result += ToInfoPercent("成功率", 100.0 - fail_chance, -1) + "\n"
                                        + ToInfo("物品相对概率(绝对概率)", "", -1)
                                        + ToInfoNote("绝对概率之和等于成功率",-1)
                                        + tmp;
                                }
                                else//否则弃疗
                                {
                                    foreach (var itemPair in itemLists)
                                    {
                                        int item_chance = itemPair.Key;
                                        int prob = Math.Clamp(item_chance + base_chance, 0, 100);
                                        tmp += ToInfoPercent($"{itemPair.Value}({ToStringSign(item_chance)}%)", prob, -2);
                                    }
                                    result += ToInfoPercent("成功率", 100.0 - fail_chance, -1) + "\n" 
                                        + ToInfo("物品相对概率", "", -1)
                                        + ToInfoNote("每个物品分别以相对概率决定是否入围，再在所有入围物品中以均等概率选一个作为最终产物，当且仅当入围物品为0时失败", -1)
                                        + tmp;
                                }
                            }
                            //招人建筑
                            //招人部分在AcceptBuildingBlockRecruitPeople，根据等级获得资质或魅力加成
                            else if (shopEventConfig.RecruitPeopleProb.Count > 0)
                            {
                                tmp = "";
                                int attainment = __instance.GetBuildingAttainment(blockData, blockKey, true);
                                int base_chance_min = attainment/__instance.AttainmentToProb+ blockData.Level+ shopEventConfig.RecruitPeopleProbAdd[0];
                                int base_chance_max = attainment / __instance.AttainmentToProb + blockData.Level+ shopEventConfig.RecruitPeopleProbAdd[1];
                                tmp += ToInfo("招募相对概率", "", -1);
                                tmp += ToInfoNote("每个物品分别以相对概率决定是否入围，再在所有入围物品中以均等概率选一个作为最终产物，当且仅当入围物品为0时失败", -1);
                                tmp += ToInfo("基础概率",$"{base_chance_min}~{base_chance_max}%", -2);
                                tmp += ToInfoNote("每个物品的相对概率=基础概率+物品概率加成", -2);
                                tmp += ToInfoNote("先随机出一个概率，再以此概率随机检测是否成功",-2);
                                tmp += ToInfoAdd("造诣总合/3", attainment, -3);
                                tmp += ToInfoDivision("倍率", __instance.AttainmentToProb, -3);
                                tmp += ToInfoAdd("建筑等级", blockData.Level, -3);
                                tmp += ToInfo("随机",$"{shopEventConfig.RecruitPeopleProbAdd[0]}~{ shopEventConfig.RecruitPeopleProbAdd[1]}" , -3);
                                //如同物品，每个等级以相对概率入围，最后选出一个等级，然后根据等级获得加成                               
                                for (int level = 0; level < shopEventConfig.RecruitPeopleProb.Count; level++)
                                {
                                    string title = "";
                                    //获得该等级的加成
                                    if (shopEventConfig.AttainmentFix.Count > 0)
                                    { 
                                        //该加成是正态随机资质时期望(40+0.25*x)的x部分，GenerateRandomAttributeValue
                                        //如果有其它效果导致获得加成，也会导致期望改变，此处忽略，资质范围[0-100]，此处不可能超出范围，忽略
                                        float mean = 40f + 25f * ((float)shopEventConfig.AttainmentFix[level] / 100f);
                                        title = $"{Config.LifeSkillType.Instance[config.RequireLifeSkillType].Name}期望:"
                                                + $"{mean.ToString("f2")}";
                                    }
                                    else if (shopEventConfig.CharacterPropertyFix == 101)
                                    {
                                        //同样不是加成，先完全随机再调整过去
                                        //AdjustToBaseCharm
                                        title = $"魅力趋近{shopEventConfig.CharacterPropertyFixNum[level]}";
                                    }
                                    int add_chance = shopEventConfig.RecruitPeopleProb[level];
                                    tmp += ToInfo($"{title}({ToStringSign(add_chance)}%)",$"{add_chance+base_chance_min}~{add_chance+base_chance_max}%",-2);
                                }
                                if (shopEventConfig.AttainmentFix.Count > 0)
                                {
                                    tmp += ToInfoNote("资质为正态随机,范围0~100", -2);
                                }
                                result += "\n"+tmp;
                            }

                        }
                }
            }
            return result;
        }
    }
}
