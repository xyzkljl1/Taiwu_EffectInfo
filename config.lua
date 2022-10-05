return {
	Source = 1,
	FrontendPlugins = 
	{
		[1] = "EffectInfoFrontend.dll"
	},
	Description = "1.3,对应游戏v0.0.25.1,用于在人物/建筑/读书菜单部分属性的悬浮tips上显示数值详细信息",
	Title = "洞察",
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
	Version = "1.2",
	Author = "xyzkljl1"
}