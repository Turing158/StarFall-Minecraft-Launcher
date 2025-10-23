namespace StarFallMC.Util;

public class ResourceCategory {
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
            _ => string.Empty
        };
    }

    public static string ModrinthCategoriesParse(string name) {
        return name switch {
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
            _ => String.Empty
        };
    }
}