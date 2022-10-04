return {
	Title = "洞察",
	Version = "v1.0.2",
	BackendPlugins = { 
		[1] = "EffectInfoBackend.dll",
	},
	FrontendPlugins = { 
		[1] = "EffectInfoFrontend.dll",
	},
	Author = "xyzkljl1",
	Description = "1.0.2,对应正式版游戏v0.0.20.1,用于在人物菜单属性的悬浮tips上显示数值详细信息",
	["DefaultSettings"] = {
		[1] = {
			["Key"] = "On",
			["SettingType"] = "Toggle",
			["DisplayName"] = "显示详细数值",
			["Description"] = "开启时在人物界面显示属性数值的详细信息",
			["DefaultValue"] = true,
			},
		[2] = {
			["Key"] = "InfoLevel",
			["SettingType"] = "Slider",
			["DisplayName"] = "信息等级",
			["Description"] = "等级越高显示信息越详细",
			["DefaultValue"] = 3,
			["MinValue"] = 1,
			["MaxValue"] = 3,
			["StepSize"] = 1,
			},
		[3] = {
			["Key"] = "ShowUseless",
			["SettingType"] = "Toggle",
			["DisplayName"] = "显示冗余信息",
			["Description"] = "开启时，显示更多为0的加成",
			["DefaultValue"] = true,
			},
		},
}