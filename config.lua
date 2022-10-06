return {
	Source = 1,
	Cover = "cover.jpg",
	FrontendPlugins = 
	{
		[1] = "EffectInfoFrontend.dll"
	},
	Description = "1.5,对应游戏v0.0.25.1\n 源代码：https://github.com/xyzkljl1/Taiwu_EffectInfo\n用于在人物/建筑/读书菜单部分数字的悬浮tips上显示数值详细信息，目前支持的：\n1.人物界面：右侧的全部属性、魅力(左侧魅力图标)\n2.建筑管理界面：资源(药材/金铁等)、道具收入数值\n3.读书界面：读书效率(画面中间上方书本描述)\n\n说明：\n1.数值分为123级\n一般而言，每个1级数值都是下方所有2级数值的从上到下计算的总合(指计算后的结果，并不一定都是加法)，2级数值是下方3级数值的总合，如果不是，则百分百是bug\n例外的是最下方的\"校验值\"，它是所有1级数值的总合，它应该和游戏面板上显示数值一致，如果不一致则百分百是bug(但是一致不能说明百分百正确)\n3级数值前面有//，这么做是为了和2级数值区分开\n\n2.每一项下的数值遵循从上至下计算的原则，乘除法均不能随意调换位置\n例如你看到某一项目下是：\nAAA +10\nBBB +5\nCCC X2\nDDD -8\nEEE  /10\n则最终数值为(((+10+5)x2)-8)/10\n\n3.写成整数的均在每次计算后取整，写成小数的计算时不取整\n例如\nA +10\nB /3\nC X10\n因为10/3=3,3*10=30,最终为30\nA +10.0\nB /3.0\nC X10.0\n最终为33\n\n4.会略微改变魅力说明文字的排版以防止浮动窗口太长\n\n5.\"属性\"加值/\"效果\"加值中的“属性”/“效果”并没有严格的意义\n\n6.参考书的\"奖励类型\"是指每本书固定对应一种生活技能，如果参考书是该技能的书则有奖励\n\"同类型\"是指参考书和当前读的书是同种技能的书则有奖励\n\n7.轮回只算了一世的属性奖励、练功房对读书效率无加成，这是游戏自身的bug\n本mod也会显示但不计算这些加值，以最终获得跟游戏内面板相同的校验值\n可以使用我的\"个人临时bug修复\"mod修复练功房问题\n\n8.目前读到末页的书/读完的书效率显示可能和游戏的面板数值不同，这是游戏自身的bug\n可以用我的\"个人临时bug修复\"mod修复该问题\n\n9.武器防具的基础破体/御体等只在攻击时有效，不影响面板，因此也不会出现在该mod显示的详细数值中\n",
	Title = "洞察~数值解析",
	BackendPlugins = 
	{
		[1] = "EffectInfoBackend.dll"
	},
	FileId = 2871662978,
	DefaultSettings = 
	{
		[1] = 
		{
			DisplayName = "显示详细数值",
			Description = "开启时在人物界面显示属性数值的详细信息",
			SettingType = "Toggle",
			DefaultValue = true,
			Key = "On"
		},
		[2] = 
		{
			StepSize = 1,
			MaxValue = 3,
			MinValue = 1,
			DisplayName = "信息等级",
			Description = "等级越高显示信息越详细",
			SettingType = "Slider",
			DefaultValue = 3,
			Key = "InfoLevel"
		},
		[3] = 
		{
			DisplayName = "显示冗余信息",
			Description = "开启时，显示更多为0的加成",
			SettingType = "Toggle",
			DefaultValue = true,
			Key = "ShowUseless"
		}
	},
	Version = "1.5",
	Author = "xyzkljl1"
}