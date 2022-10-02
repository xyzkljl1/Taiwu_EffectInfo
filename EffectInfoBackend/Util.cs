using GameData.Domains;
using GameData.Domains.Item;
using GameData.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaiwuModdingLib.Core.Plugin;

namespace EffectInfo
{
    public partial class EffectInfoBackend : TaiwuRemakePlugin
    {
		public static string valueSumType2Text(sbyte valueSumType)
        {
			if (valueSumType == 1)
				return "加值";
			if (valueSumType == 2)
				return "减值";
			return "";
        }
		public static string GetCombatSkillName(short combat_skill_id)
		{
			var charCombatSkills = DomainManager.CombatSkill.GetCharCombatSkills(combat_skill_id);
			if (charCombatSkills == null || !charCombatSkills.ContainsKey(combat_skill_id))
				return "";
			var combat_skill = charCombatSkills[combat_skill_id];
			var template_id = combat_skill.GetId().SkillTemplateId;
			var cb_template = Config.CombatSkill.Instance[template_id];
			return cb_template.Name;
		}
		public static FieldType GetPrivateValue<FieldType>(object instance, string field_name)
		{
			Type type = instance.GetType();
			FieldInfo field_info = type.GetField(field_name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			return (FieldType)field_info.GetValue(instance);
		}
		public static ReturnType CallPrivateStaticMethod<ReturnType>(object instance, string method_name, object[] paras)
		{
			Type type = instance.GetType();
			MethodInfo method_info = type.GetMethod(method_name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
			var para_infos = method_info.GetParameters();
			if (paras.Length != paras.Length)
			{
				AdaptableLog.Info($"EffectInfo失效:{method_name}");
				return (ReturnType)new object();
			}
			for (int i = 0; i < para_infos.Length; i++)
				if (para_infos[i].ParameterType != paras[i].GetType())
				{
					AdaptableLog.Info($"EffectInfo失效:{method_name} {para_infos[i].Name}");
					return (ReturnType)new object();
				}
			return (ReturnType)method_info.Invoke(instance, paras);
		}
		public static ReturnType CallPrivateMethod<ReturnType>(object instance, string method_name, object[] paras)
		{
			Type type = instance.GetType();
			MethodInfo method_info = type.GetMethod(method_name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			var para_infos = method_info.GetParameters();
			if (paras.Length != paras.Length)
			{
				AdaptableLog.Info($"EffectInfo失效:{method_name}");
				return (ReturnType)new object();
			}
			for (int i = 0; i < para_infos.Length; i++)
				if (para_infos[i].ParameterType != paras[i].GetType())
				{
					AdaptableLog.Info($"EffectInfo失效:{method_name} {para_infos[i].Name}");
					return (ReturnType)new object();
				}
			return (ReturnType)method_info.Invoke(instance, paras);
		}


		public static string GetEquipmentName(ItemKey itemKey)
		{
			ItemBase item = DomainManager.Item.GetBaseItem(itemKey);
			return item.GetName();
		}
		public static string GetFeatureName(short feature_id)
		{
			return Config.CharacterFeature.Instance.GetItem(feature_id).Name;
		}
		public static void SetPrivateField<FieldType>(object instance, string field_name, FieldType value)
		{
			Type type = instance.GetType();
			FieldInfo field_info = type.GetField(field_name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			field_info.SetValue(instance, value);
		}
		unsafe static string ToInfo(string title, string item, int msgLevel)
		{
			if (msgLevel > EffectInfoBackend.InfoLevel)
				return "";
			string result = $"{msgLevel} ";
			if (msgLevel == 1)
				result += $"<color=#pinkyellow>·{title}\t\t\t\t\t\t{item}</color>\n";//align会改变整行
			else if (msgLevel == 2)
				result += $"<color=#grey>\t·{title}\t\t\t\t\t{item}</color>\n";
			else
				result += $"<color=#grey>\t\t·{title}\t\t\t\t// {item}</color>\n";
			return result;
		}
		unsafe static string ToInfoAdd(string title, double value, int infoLevel)
		{
			if (value > 0)
				return ToInfo(title, $"+{value.ToString("f2")}", infoLevel);
			if (value < 0)
				return ToInfo(title, $"-{value.ToString("f2")}", infoLevel);
			return ToInfo(title, $"0", infoLevel);
		}
		unsafe static string ToInfoAdd(string title, int value, int infoLevel)
		{
			if (value > 0)
				return ToInfo(title, $"+{value}", infoLevel);
			if (value < 0)
				return ToInfo(title, $"{value}", infoLevel);
			return ToInfo(title, $"0", infoLevel);
		}
		unsafe static string ToInfoAdd(string title, short value, int infoLevel)
		{
			return ToInfoAdd(title, (int)value, infoLevel);
		}
		unsafe static string ToInfoMulti(string title, int value, int infoLevel)
		{
			return ToInfo(title, $"×{value}", infoLevel);
		}
		unsafe static string ToInfoMulti(string title, double value, int infoLevel)
		{
			return ToInfo(title, $"×{value.ToString("f2")}", infoLevel);
		}
		unsafe static string ToInfoPercent(string title, int value, int infoLevel)
		{
			return ToInfo(title, $"×{value}%", infoLevel);
		}


	}
}
