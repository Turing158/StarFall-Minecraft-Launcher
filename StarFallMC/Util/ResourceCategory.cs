using StarFallMC.Entity.Enum;

namespace StarFallMC.Util;

public class ResourceCategory {
    
    public static List<string> ModCategories = new List<string> {
        "全部",
        "美食",
        "装饰", 
        "生物",
        "魔法",
        "支持库",
        "科技",
        "装备",
        "运输",
        "世界元素",
        "服务器",
        "存储",
        "实用",
        "冒险"
    };
    
    public static List<string> ModPackCategoriesInCurseForge = new List<string> {
        "全部",
        "多人",
        "硬核",
        "战斗",
        "任务",
        "科技",
        "魔法",
        "冒险",
        "探索",
        "小游戏",
        "科幻",
        "空岛",
        "原版改良",
        "FTB",
        "基于地图",
        "轻量",
        "大型",
    };

    public static List<string> ModPackCategoriesInModrinth = new List<string> {
        "全部",
        "多人",
        "硬核",
        "战斗",
        "任务",
        "水槽包",
        "轻量",
        "科技",
        "魔法",
        "冒险",
        "优化",
    };
    
    public static List<string> TexturePackCategoriesInCurseForge = new List<string> {
        "全部",
        "原版风",
        "写实风",
        "现代风",
        "中世纪",
        "蒸汽朋克",
        "含字体",
        "动态效果",
        "兼容 Mod",
        "数据包",
    };
    
    public static List<string> TexturePackCategoriesInModrinth = new List<string> {
        "全部",
        "简洁",
        "改良",
        "含声音",
        "含字体",
        "含模型",
        "含 UI",
        "含语言",
        "核心着色器",
        "兼容 Mod",
    };
    
    public static List<string> TexturePackResolutionCategoriesInCurseForge = new List<string> {
        "全部",
        "16x",
        "32x",
        "64x",
        "128x",
        "256x",
        "超高清",
    };
    
    public static List<string> TexturePackResolutionCategoriesInModrinth = new List<string> {
        "全部",
        "极简",
        "16x",
        "32x",
        "48x",
        "64x",
        "128x",
        "256x",
        "超高清",
    };
    
    public static List<string> ShaderPackCategoriesInCurseForge = new List<string> {
        "全部",
        "写实风",
        "幻想风",
        "原版风",
    };
    
    public static List<string> ShaderPackCategoriesInModrinth = new List<string> {
        "全部",
        "幻想风",
        "半写实风",
        "卡通风",
        "彩色光照",
        "路径追踪",
        "PBR",
        "反射",
        "Iris",
        "OptiFine",
        "原版可用",
    };
    
    public static List<string> DataPackCategoriesInCurseForge = new List<string> {
        "全部",
        "冒险",
        "幻想",
        "支持库",
        "魔法",
        "Mod 相关",
        "科技",
        "实用",
    };
    
    public static List<string> DataPackCategoriesInModrinth = new List<string> {
        "全部",
        "世界元素",
        "美食",
        "机制",
        "运输",
        "存储",
        "装饰",
        "生物",
        "装备",
        "服务器",
    };
    
    
    
    public static string CurseForgeCategoriesParse(int id) {
        return id switch {
            436 => "美食",
            408 => "矿物|资源",
            425 => "杂项",
            427 => "热力膨胀",
            424 => "装饰",
            5299 => "教育",
            432 => "建筑工艺",
            413 => "处理",
            428 => "匠魂",
            423 => "地图|信息",
            429 => "工业",
            416 => "农业",
            412 => "科技",
            418 => "基因",
            409 => "地形结构",
            411 => "生物",
            419 => "魔法",
            426 => "插件",
            410 => "维度",
            434 => "装备",
            406 => "世界元素",
            435 => "服务器",
            415 => "物流|管道",
            4545 => "AE",
            414 => "运输",
            417 => "能源",
            407 => "生物群系",
            422 => "冒险",
            433 => "林业",
            421 => "支持库",
            420 => "存储",
            4558 => "红石",
            4485 => "血魔法",
            430 => "神秘时代",
            4843 => "自动化",
            4773 => "魔改配方",
            5191 => "实用",
            6145 => "空岛",
            6954 => "整合",
            6814 => "性能",
            6821 => "修复",
            9026 => "创造模式",
            // 整合包
            4484 => "多人",
            4479 => "硬核",
            4483 => "战斗",
            4478 => "任务",
            4472 => "科技",
            4473 => "魔法",
            4475 => "冒险",
            4476 => "探索",
            4477 => "小游戏",
            4474 => "科幻",
            4736 => "空岛",
            5128 => "原版改良",
            4487 => "FTB",
            4480 => "基于地图",
            4481 => "轻量",
            4482 => "大型",
            // 材质包
            393 => "16x",
            394 => "32x",
            395 => "64x",
            396 => "128x",
            397 => "256x",
            398 => "超高清",
            403 => "原版风",
            400 => "写实风",
            401 => "现代风",
            402 => "中世纪",
            399 => "蒸汽朋克",
            5244 => "含字体",
            404 => "动态效果",
            4465 => "兼容 Mod",
            5193 => "数据包",
            // 光影
            6553 => "写实风",
            6554 => "幻想风",
            6555 => "原版风",
            // 数据包
            6948 => "冒险",
            6949 => "幻想",
            6950 => "支持库",
            6952 => "魔法",
            6946 => "Mod 相关",
            6951 => "科技",
            6953 => "实用",
            _ => string.Empty
        };
    }

    public static int CurseForgeCategoriesToInt(string categoryChinese, ResourceType type) {
        if (type == ResourceType.Mod) {
            return categoryChinese switch {
                "美食" => 436,
                "矿物|资源" => 408,
                "杂项" => 425,
                "热力膨胀" => 427,
                "装饰" => 424,
                "教育" => 5299,
                "建筑工艺" => 432,
                "处理" => 413,
                "匠魂" => 428,
                "地图|信息" => 423,
                "工业" => 429,
                "农业" => 416,
                "科技" => 412,
                "基因" => 418,
                "地形结构" => 409,
                "生物" => 411,
                "魔法" => 419,
                "插件" => 426,
                "维度" => 410,
                "装备" => 434,
                "世界元素" => 406,
                "服务器" => 435,
                "物流|管道" => 415,
                "AE" => 4545,
                "运输" => 414,
                "能源" => 417,
                "生物群系" => 407,
                "冒险" => 422,
                "林业" => 433,
                "支持库" => 421,
                "存储" => 420,
                "红石" => 4558,
                "血魔法" => 4485,
                "神秘时代" => 430,
                "自动化" => 4843,
                "魔改配方" => 4773,
                "实用" => 5191,
                "空岛" => 6145,
                "整合" => 6954,
                "性能" => 6814,
                "修复" => 6821,
                "创造模式" => 9026,
                _ => -1 
            }; 
        }
        if (type == ResourceType.ModPack) {
            return categoryChinese switch {
                "多人" => 4484,
                "硬核" => 4479,
                "战斗" => 4483,
                "任务" => 4478,
                "科技" => 4472,
                "魔法" => 4473,
                "冒险" => 4475,
                "探索" => 4476,
                "小游戏" => 4477,
                "科幻" => 4474,
                "空岛" => 4736,
                "原版改良" => 5128,
                "FTB" => 4487,
                "基于地图" => 4480,
                "轻量" => 4481,
                "大型" => 4482,
                _ => -1
            };
        }

        if (type == ResourceType.TexturePack) {
            return categoryChinese switch {
                "原版风" => 403,
                "写实风" => 400,
                "现代风" => 401,
                "中世纪" => 402,
                "蒸汽朋克" => 399,
                "含字体" => 5244,
                "动态效果" => 404,
                "兼容 Mod" => 4465,
                "16x" => 393,
                "32x" => 394,
                "64x" => 395,
                "128x" => 396,
                "256x" => 397,
                "超高清" => 398,
                "数据包" => 5193,
                _ => -1
            };
        }

        if (type == ResourceType.ShaderPack) {
            return categoryChinese switch {
                "写实风" => 6553,
                "幻想风" => 6554,
                "原版风" => 6555,
                _ => -1
            };
        }

        if (type == ResourceType.DataPack) {
            return categoryChinese switch {
                "冒险" => 6948,
                "幻想" => 6949,
                "支持库" => 6950,
                "魔法" => 6952,
                "Mod 相关" => 6946,
                "科技" => 6951,
                "实用" => 6953,
                _ => -1
            };
        }
        return -1;
    }

    public static string ModrinthCategoriesParse(string name) {
        return name switch {
            // 混合
            "adventure" => "冒险",
            "cursed" => "诅咒",
            "decoration" => "装饰",
            "economy" => "经济",
            "equipment" => "装备",
            "food" => "美食",
            "game-mechanics" => "机制",
            "library" => "支持库",
            "magic" => "魔法",
            "management" => "管理",
            "minigame" => "小游戏",
            "mobs" => "生物",
            "optimization" => "优化",
            "social" => "服务器",
            "storage" => "存储",
            "technology" => "科技",
            "transportation" => "运输",
            "utility" => "实用",
            "worldgen" => "世界元素",
            "vanilla-like" => "原版风",
            "realistic" => "写实风",
            // 整合包
            "multiplayer" => "多人",
            "challenging" => "硬核",
            "combat" => "战斗",
            "quests" => "任务",
            "kitchen-sink" => "水槽包",
            "lightweight" => "轻量",
            // 材质包
            "simplistic" => "简洁",
            "tweaks" => "改良",
            "audio" => "含声音",
            "fonts" => "含字体",
            "models" => "含模型",
            "gui" => "含 UI",
            "locale" => "含语言",
            "core-shaders" => "核心着色器",
            "modded" => "兼容 Mod",
            // 材质分辨率
            "8x-" => "极简",
            "16x" => "16x",
            "32x" => "32x",
            "48x" => "48x",
            "64x" => "64x",
            "128x" => "128x",
            "256x" => "256x",
            "512x+" => "超高清",
            // 光影
            "fantasy" => "幻想风",
            "semi-realistic" => "半写实风",
            "cartoon" => "卡通风",
            "colored-lighting" => "彩色光照",
            "path-tracing" => "路径追踪",
            "pbr" => "PBR",
            "reflections" => "反射",
            "iris" => "Iris",
            "optifine" => "OptiFine",
            "vanilla" => "原版可用",
            // 光影负荷
            "potato" => "极低",
            "low" => "低",
            "medium" => "中",
            "high" => "高",
            _ => String.Empty
        };
    }

    public static string ModrinthCategoryToString(string categoryChinese) {
        return categoryChinese switch {
            // 混合
            "冒险" => "adventure",
            "诅咒" => "cursed",
            "装饰" => "decoration",
            "经济" => "economy",
            "装备" => "equipment",
            "美食" => "food",
            "机制" => "game-mechanics",
            "支持库" => "library",
            "魔法" => "magic",
            "管理" => "management",
            "小游戏" => "minigame",
            "生物" => "mobs",
            "优化" => "optimization",
            "服务器" => "social",
            "存储" => "storage",
            "科技" => "technology",
            "运输" => "transportation",
            "实用" => "utility",
            "世界元素" => "worldgen",
            "原版风" => "vanilla-like",
            "写实风" => "realistic",
            // 整合包
            "多人" => "multiplayer",
            "硬核" => "challenging",
            "战斗" => "combat",
            "任务" => "quests",
            "水槽包" => "kitchen-sink",
            "轻量" => "lightweight",
            // 材质包
            "简洁" => "simplistic",
            "改良" => "tweaks",
            "含声音" => "audio",
            "含字体" => "fonts",
            "含模型" => "models",
            "含 UI" => "gui",
            "含语言" => "locale",
            "核心着色器" => "core-shaders",
            "兼容 Mod" => "modded",
            // 材质分辨率
            "极简" => "8x-",
            "16x" => "16x",
            "32x" => "32x",
            "48x" => "48x",
            "64x" => "64x",
            "128x" => "128x",
            "256x" => "256x",
            "超高清" => "512x+",
            // 光影
            "幻想风" => "fantasy",
            "半写实风" => "semi-realistic",
            "卡通风" => "cartoon",
            "彩色光照" => "colored-lighting",
            "路径追踪" => "path-tracing",
            "PBR" => "pbr",
            "反射" => "reflections",
            "Iris" => "iris",
            "OptiFine" => "optifine",
            "原版可用" => "vanilla",
            _ => string.Empty
        };
    }
}