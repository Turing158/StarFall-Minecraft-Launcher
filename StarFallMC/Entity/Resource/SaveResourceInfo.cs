using fNbt;

namespace StarFallMC.Entity.Resource;

public class SaveResourceInfo : SavesResource{
            public SaveResourceInfo(NbtCompound nbt, string dirName, string path, string refreshDate) 
                : base(nbt, dirName, path, refreshDate) {
            }

            public static SaveResourceInfo FromSavesResource(SavesResource baseObj) {
                return new SaveResourceInfo(baseObj.nbt, baseObj.DirName,baseObj.Path,baseObj.RefreshDate);
            }
            
            public string Difficulty {
                get => nbt.TryGet("Difficulty",out NbtByte difficulty) ?
                    difficulty.Value switch {
                        0 => "和平",
                        1 => "简单",
                        2 => "普通",
                        3 => "困难",
                        _ => string.Empty,
                    }:
                    string.Empty;
            }

            public bool DifficultyLock {
                get => nbt.TryGet("DifficultyLock",out NbtByte difficultyLock) ?
                    difficultyLock.Value == 1 : false;
            }
            
            public string GameType {
                get => nbt.TryGet("GameType",out NbtInt gameType) ?
                    gameType.Value switch {
                        0 => "生存",
                        1 => "创造",
                        2 => "冒险",
                        3 => "旁观者",
                        _ => string.Empty,
                    } : 
                    string.Empty;
            }
            
            public bool isRaining {
                get => nbt.TryGet("raining",out NbtByte raining) ?
                    raining.Value == 1 : false;
            }
            
            public bool isThundering {
                get => nbt.TryGet("thundering",out NbtByte thundering) ?
                    thundering.Value == 1 : false;
            }
            
             public string Weather {
                get => isRaining ? (isThundering ? "雷雨" : "雨天") : "晴天";
            }
             
             public string WeatherIcon {
                get => isRaining ? (isThundering ? "\ue600" : "\ue63a") : "\ue623";
            }

            private NbtCompound VersionTag {
                get => nbt.TryGet("Version",out NbtCompound versionTag) ? versionTag : null;
            }
            
            public string VersionName {
                get => VersionTag != null && VersionTag.TryGet("Name",out NbtString versionName) ? versionName.Value : string.Empty;
            }
            
            public bool VersionSnapshot {
                get => VersionTag != null && VersionTag.TryGet("Snapshot",out NbtByte snapshot) ? snapshot.Value == 1 : false;
            }
            
            public bool AllowCheats {
                get => nbt.TryGet("allowCommends",out NbtByte allowCheats) ?
                    allowCheats.Value == 1 : false;
            }

            public NbtCompound GameRuleTag {
                get => nbt.TryGet("GameRules",out NbtCompound gameRuleTag) ? gameRuleTag : null;
            }
            
            public bool KeepInventory {
                get => GameRuleTag != null && GameRuleTag.TryGet("keepInventory",out NbtString keepInventory) ?
                    keepInventory.Value == "true" : false;
            }
            
            public NbtCompound WorldGenSettings {
                get => nbt.TryGet("WorldGenSettings",out NbtCompound worldGenSetting) ? worldGenSetting : null;
            }
            
            public long Seed {
                get => WorldGenSettings != null && WorldGenSettings.TryGet("seed",out NbtLong seed) ? seed.Value : 0;
            }
            
            public bool SpawnBonusChest {
                get => WorldGenSettings != null && WorldGenSettings.TryGet("bonus_chest",out NbtByte spawnBonusChest) ?
                    spawnBonusChest.Value == 1 : false;
            }

            public override string ToString() {
                return $"SaveInfo:(WorldName:{WorldName},DirName:{DirName},Path:{Path},Icon:{Icon},RefreshDate:{RefreshDate},Difficulty:{Difficulty},GameType:{GameType},DifficultyLock:{DifficultyLock},isRaining:{isRaining},isThundering:{isThundering},VersionName:{VersionName},VersionSnapshot:{VersionSnapshot},AllowCheats:{AllowCheats},KeepInventory:{KeepInventory})";
            }
        }