return {
	Title = "显示效果信息",
	Version = "2022.9.29",
	BackendPlugins = { 
		[1] = "EffectInfoBackend.dll",
	},
	FrontendPlugins = { 
		[1] = "EffectInfoFrontend.dll",
	},
	Author = "xyzkljl1",
	Description = "啦啦啦",
	["DefaultSettings"] = {
		[1] = {
			["Key"] = "On",
			["SettingType"] = "Toggle",
			["DisplayName"] = "显示详细数值",
			["Description"] = "(仅对太吾生效)开启时在人物界面显示属性数值的详细信息",
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
			["DisplayName"] = "显示未激活的加成",
			["Description"] = "开启时，即使某项加值为空，也显示出来",
			["DefaultValue"] = true,
			},
		},
}