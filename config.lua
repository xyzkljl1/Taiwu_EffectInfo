return {
	Source = 1,
	FileId = 2875465713,
	Cover = "cover.jpg",
	FrontendPlugins = 
	{
		[1] = "EffectInfoFrontend.dll"
	},
	Description = [[1.997,对应游戏v0.0.32
源代码：https://github.com/xyzkljl1/Taiwu_EffectInfo
用于在人物/建筑/读书菜单部分数字的悬浮tips上显示数值详细信息，目前支持的：
1.人物界面：右侧的全部属性、魅力(属性左侧魅力图标)、生育力(魅力上方年龄图标)
2.建筑管理界面：资源(药材/金铁等)收入、经营进度、(部分建筑的)经营成功率及物品/招募概率
3.读书界面：读书效率(画面中间上方书本描述)

意见和提问请发到讨论区！！评论区无法单独回复
该mod旨在解释数值的计算方式，需要显示各种隐藏属性的请去找隔壁的更多信息mod
该mod用于正式版游戏，不保证兼容测试分支

说明：
1.数值分为123级
一般而言，每个1级数值都是下方所有2级数值的从上到下计算的总合(指计算后的结果，并不一定都是加法)，2级数值是下方3级数值的总合，如果不是，则百分百是bug
例外的是最下方的"校验值"，它是所有1级数值的总合，它应该和游戏面板上显示数值一致，如果不一致则百分百是bug(但是一致不能说明百分百正确)
3级数值前面有//，这么做是为了和2级数值区分开

2.每一项下的数值遵循从上至下计算的原则，乘除法均不能随意调换位置，写了"如果XXX"的只有在满足条件时计算
例如你看到某一项目下是：
AAA +10
BBB +5
CCC X2
DDD -8
EEE  /10
则最终数值为(((+10+5)x2)-8)/10

3.写成整数的均在每次计算后取整，写成小数的计算时不取整
例如
A +10
B /3
C X10
因为10/3=3,3*10=30,最终为30
A +10.0
B /3.0
C X10.0
最终为33
另外，建筑经营效率写成小数百分比因为小数部分有效，移动速度等写成整数百分比因为小数部分无效
小数写成两位是为了简洁，并不代表只有两位有效

4.该mod会略微改变魅力说明文字的排版以防止浮动窗口太长

5."属性加值"/"效果加值"中:
一般而言"属性"更偏向于常驻的属性增加，"效果"更偏向于临时的buff，但是二者并不表示某种严格的意义，请不要想太多

6.参考书的"奖励类型"是指每本书固定对应一种生活技能，如果参考书是该技能的书则有奖励
"同类型"是指参考书和当前读的书是同种技能的书则有奖励

7.门派的正逆练研读速度只对总纲生效,这是游戏自身的bug
本mod会显示正确数值以供参考，但仍使用错误的数值计算，以获得和游戏内相同的结果
可以使用我的"个人临时bug修复"mod修复门派效果问题

8.武器防具的基础破体/御体等只在攻击时有效，不影响面板，因此也不会出现在该mod显示的详细数值中
有的加成写成拼音，是因为我暂时还没找到合适的方法将其还原为汉字

9.知客亭和镖局的经营进度和收益使用的是造诣总合，而成功率使用的是总造诣/3，是因为游戏内就是这么算的
经常出现"总造诣/3",而不是"平均造诣"，是因为游戏内的"平均"就是除最大人数而非当前人数(八成是bug)

10.建筑获得道具的相对概率，意思是每个道具分别用此概率检测是否入围，然后入围的所有道具以均等机会选出一个作为最终产出
该概率反应的是各个道具相对产出的可能性，所以叫相对概率；当且仅当没有道具入围时，出现失败事件
药房显示N种道具，后面显示一个相对概率，意思是其中的每种道具各自有这么多相对概率入围，为了防止窗口太长合并到一起显示
一个道具的绝对概率，就是当经营进度满时获得这个道具的概率，所有道具的绝对概率之和+失败率=100%
招募建筑招人类似，但是招募到每个等级的角色的概率在一定范围内随机，先随机出一个概率，再随机检验该概率是否通过

11.当前版本产出资源(金铁等)的经营类建筑(每月读条加百分比的都时经营类建筑)，游戏内显示的工作效率可能不正确，是游戏的bug，请以实际过月时增加的进度为准

12.可以产出多种资源的建筑，不同资源所需的技艺可能是不同的，更改产出资源时界面应当刷新，但是游戏有bug不会刷新导致显示的数值可能有问题，该mod帮他刷新了
该功能只有在启动游戏时就开启该mod的情况下才生效

13.招人建筑招募到的角色，有且仅有一项资质/魅力获得加成
如果有资质加成，根据这个加成获得资质期望，再用该期望正态随机获得实际资质
该MOD显示的是无其它效果影响时，该角色的资质期望的平均值(因为加成有一定随机，所以期望也在一定范围内随机，所以显示的是期望的平均值)，创建角色过程中可能因为其它随机因素导致该期望改变
该角色其它资质的平均值是40
如果有魅力加成，则先完全随机出魅力，再向目标魅力调整
]],
	Title = "洞察~数值解析",
	BackendPlugins = 
	{
		[1] = "EffectInfoBackend.dll"
	},
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
	Version = "1.997",
	Author = "xyzkljl1"
}