using GameData.Common;
using GameData.Domains;
using GameData.Domains.Building;
using GameData.Domains.Character;
using GameData.Domains.Item;
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
        public static readonly string PATH_GetResourceOutput = "\\Mod\\EffectInfo\\Plugins\\Cache_BuildingResource.txt";
        //重载BuildingDomain的CallMethod响应供前端使用
        [HarmonyPrefix, HarmonyPatch(typeof(BuildingDomain), "CallMethod")]
        public static bool BuildingDomainCallMethodPatch(BuildingDomain __instance,int __result,
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
                    __result = -1;//表示无返回值
                    return false;//由于是自定义key，不继续执行原函数
                }
                BuildingBlockKey para1 = default(BuildingBlockKey);
                argsOffset += GameData.Serializer.Serializer.Deserialize(argDataPool, argsOffset, ref para1);
                GetBuildingResourceOutputInfo(__instance,para1);
                __result = -1;//表示无返回值
                return false;
            }
            return true;
        }
        //BuildingDomain.CalcResourceOutput
        //同一建筑产出不同种类资源(食物、木材等)用的公式是一样的，GetCollectBuildingResourceType返回的是Config中的常量而非根据界面上的选择变化？
        public static void GetBuildingResourceOutputInfo(BuildingDomain __instance, BuildingBlockKey blockKey)
        {
            var result = "";
            int check_value=0;
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
                            var value = __instance.BaseWorkContribution + manageChar.GetLifeSkillAttainment(lifeSkillType);
                            total_attainment += value;
                            tmp += ToInfoAdd($":{lifeSkillName}+{__instance.BaseWorkContribution}", value, -3);
                        }
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
                    double tmp_check_value =1.0 + 0.01* (double)total_attainment;
                    result += ToInfoMulti("造诣效率", tmp_check_value, -1);
                    result += ToInfoAdd("基础", 1.0, 2);
                    result += ToInfoMulti("总造诣", 0.01 * (double)total_attainment, -2);
                    result += tmp;
                    tmp_check_value *= (double)(blockData.Level + 10);
                    result += ToInfoMulti("建筑等级+10", (double)(blockData.Level + 10), -1);
                    
                    var factor=__instance.CalcResourceChangeFactor(blockData);
                    tmp_check_value *= factor;
                    result += ToInfoMulti("依赖建筑", factor, -1);
                    check_value = (int)tmp_check_value;

                    result += ToInfoAdd("总合校验值",check_value, -1);
                }
            }
            var path = $"{Directory.GetCurrentDirectory()}\\..{PATH_GetResourceOutput}";
            File.WriteAllText(path, result);
        }
    }
}
