using GameData.Domains;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
namespace EffectInfo
{
	//DomainHelper.DomainIds
	//为了避免其值随版本变化，启动时动态获取值
	public static class MyDomainIds
	{
		public static void Init()
		{
			foreach (var field in typeof(MyDomainIds).GetFields(BindingFlags.Static | BindingFlags.Public))
				if (field.FieldType == typeof(ushort))
				{
					if (DomainHelper.DomainName2DomainId.ContainsKey(field.Name))
						field.SetValue(null, DomainHelper.DomainName2DomainId[field.Name]);
				}
		}
		// Token: 0x0400421B RID: 16923
		public static  ushort Global = 0;

		// Token: 0x0400421C RID: 16924
		public static  ushort World = 0;

		// Token: 0x0400421D RID: 16925
		public static  ushort Map = 0;

		// Token: 0x0E RID: 16926
		public static  ushort Organization = 0;

		// Token: 0x0400421F RID: 16927
		public static  ushort Character = 0;

		// Token: 0x04004220 RID: 16928
		public static  ushort Taiwu = 0;

		// Token: 0x04004221 RID: 16929
		public static  ushort Item = 0;

		// Token: 0x04004222 RID: 16930
		public static  ushort CombatSkill = 0;

		// Token: 0x04004223 RID: 16931
		public static  ushort Combat = 0;

		// Token: 0x04004224 RID: 16932
		public static  ushort Building = 0;

		// Token: 0x04004225 RID: 16933
		public static  ushort Adventure = 0;

		// Token: 0x04004226 RID: 16934
		public static  ushort LegendaryBook = 0;

		// Token: 0x04004227 RID: 16935
		public static  ushort TaiwuEvent = 0;

		// Token: 0x04004228 RID: 16936
		public static  ushort LifeRecord = 0;

		// Token: 0x04004229 RID: 16937
		public static  ushort Merchant = 0;

		// Token: 0x0400422A RID: 16938
		public static  ushort TutorialChapter = 0;

		// Token: 0x0400422B RID: 16939
		public static  ushort Mod = 0;

		// Token: 0x0400422C RID: 16940
		public static  ushort SpecialEffect = 0;

		// Token: 0x0400422D RID: 16941
		public static  ushort Information = 0;

		// Token: 0x0400422E RID: 16942
		public static  int Count = 0;
	}

	public partial class EffectInfoFrontend {
		public static FieldType GetValue<FieldType>(object instance, string field_name, BindingFlags flags)
		{
			Type type = instance.GetType();
			FieldInfo field_info = type.GetField(field_name, flags);
			return (FieldType)field_info.GetValue(instance);
		}
		public static void CallPrivateMethod(object instance, string method_name, object[] paras)
		{
			Type type = instance.GetType();
			MethodInfo method_info = type.GetMethod(method_name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			var para_infos = method_info.GetParameters();
			if (paras.Length != paras.Length)
			{
				UnityEngine.Debug.Log($"EffectInfo失效:{method_name}");
				return;
			}
			for (int i = 0; i < para_infos.Length; i++)
				if (para_infos[i].ParameterType != paras[i].GetType())
				{
					UnityEngine.Debug.Log($"EffectInfo失效:{method_name}");
					return ;
				}
			method_info.Invoke(instance, paras);
		}
	}
}
