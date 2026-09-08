using System.Collections.ObjectModel;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC.Services;

public interface IGameSettingsState
{
    int CurrentJavaVersionIndex { get; set; }
    ObservableCollection<JavaItem> JavaVersions { get; set; }
    bool AutoMemoryDisable { get; set; }
    int MemoryValue { get; set; }
    bool IsIsolation { get; set; }
    bool IsFullScreen { get; set; }
    string GameWidth { get; set; }
    string GameHeight { get; set; }
    string WindowTitle { get; set; }
    string CustomInfo { get; set; }
    bool JvmExtraAreaEnable { get; set; }
    string JvmExtra { get; set; }
    string GameTailArgs { get; set; }
}

public interface IPlayerState
{
    string VerifyCode { get; set; }
    Player CurrentPlayer { get; set; }
    ObservableCollection<Player> Players { get; set; }
}

public interface IGameSelectionState
{
    MinecraftItem CurrentGame { get; set; }
    DirItem CurrentDir { get; set; }
    ObservableCollection<MinecraftItem> Games { get; set; }
    ObservableCollection<DirItem> Dirs { get; set; }
    string RenameVersionText { get; set; }
    string RenameVersionTips { get; set; }
}

internal static class ApplicationState
{
    public static GameSettingsState GameSettings { get; private set; } = new();

    public static PlayerState Players { get; private set; } = new();

    public static GameSelectionState GameSelection { get; private set; } = new();

    public static void UseGameSettings(IGameSettingsState state) => GameSettings = state as GameSettingsState ?? CopyToGameSettings(state);

    public static void UsePlayers(IPlayerState state) => Players = state as PlayerState ?? CopyToPlayers(state);

    public static void UseGameSelection(IGameSelectionState state) => GameSelection = state as GameSelectionState ?? CopyToGameSelection(state);

    public static void LoadFromProperties()
    {
        GameSettings = new GameSettingsState();
        Players = new PlayerState();
        GameSelection = new GameSelectionState();

        PropertiesUtil.LoadGameSettingArgs(GameSettings);
        PropertiesUtil.LoadPlayerManage(Players);
        PropertiesUtil.LoadSelectGameArgs(GameSelection);
    }

    public static void UpdateGameSettings(IGameSettingsState source)
    {
        CopyGameSettings(source, GameSettings);
    }

    public static void UpdatePlayers(IPlayerState source)
    {
        Players.CurrentPlayer = source.CurrentPlayer;
        Players.Players = new ObservableCollection<Player>(source.Players ?? []);
    }

    public static void UpdateGameSelection(IGameSelectionState source)
    {
        GameSelection.CurrentGame = source.CurrentGame;
        GameSelection.CurrentDir = source.CurrentDir;
        GameSelection.Dirs = new ObservableCollection<DirItem>(source.Dirs ?? []);
        GameSelection.Games = new ObservableCollection<MinecraftItem>(source.Games ?? []);
    }

    private static void CopyGameSettings(IGameSettingsState source, IGameSettingsState target)
    {
        target.CurrentJavaVersionIndex = source.CurrentJavaVersionIndex;
        target.JavaVersions = new ObservableCollection<JavaItem>(source.JavaVersions ?? []);
        target.AutoMemoryDisable = source.AutoMemoryDisable;
        target.MemoryValue = source.MemoryValue;
        target.IsIsolation = source.IsIsolation;
        target.IsFullScreen = source.IsFullScreen;
        target.GameWidth = source.GameWidth;
        target.GameHeight = source.GameHeight;
        target.WindowTitle = source.WindowTitle;
        target.CustomInfo = source.CustomInfo;
        target.JvmExtraAreaEnable = source.JvmExtraAreaEnable;
        target.JvmExtra = source.JvmExtra;
        target.GameTailArgs = source.GameTailArgs;
    }

    private static GameSettingsState CopyToGameSettings(IGameSettingsState source)
    {
        var target = new GameSettingsState();
        CopyGameSettings(source, target);
        return target;
    }

    private static PlayerState CopyToPlayers(IPlayerState source)
    {
        var target = new PlayerState { CurrentPlayer = source.CurrentPlayer, VerifyCode = source.VerifyCode };
        target.Players = new ObservableCollection<Player>(source.Players ?? []);
        return target;
    }

    private static GameSelectionState CopyToGameSelection(IGameSelectionState source)
    {
        var target = new GameSelectionState { CurrentGame = source.CurrentGame, CurrentDir = source.CurrentDir };
        target.Games = new ObservableCollection<MinecraftItem>(source.Games ?? []);
        target.Dirs = new ObservableCollection<DirItem>(source.Dirs ?? []);
        return target;
    }
}

public sealed class GameSettingsState : ObservableState, IGameSettingsState
{
    private int _currentJavaVersionIndex;
    private ObservableCollection<JavaItem> _javaVersions = [];
    private bool _autoMemoryDisable;
    private int _memoryValue;
    private bool _isIsolation = true;
    private bool _isFullScreen;
    private string _gameWidth = "854";
    private string _gameHeight = "480";
    private string _windowTitle = string.Empty;
    private string _customInfo = "StarFallMC";
    private bool _jvmExtraAreaEnable;
    private string _jvmExtra = string.Empty;
    private string _gameTailArgs = string.Empty;

    public int CurrentJavaVersionIndex
    {
        get => _currentJavaVersionIndex;
        set => SetField(ref _currentJavaVersionIndex, value);
    }

    public ObservableCollection<JavaItem> JavaVersions
    {
        get => _javaVersions;
        set => SetField(ref _javaVersions, value);
    }

    public bool AutoMemoryDisable
    {
        get => _autoMemoryDisable;
        set => SetField(ref _autoMemoryDisable, value);
    }

    public int MemoryValue
    {
        get => _memoryValue;
        set => SetField(ref _memoryValue, value);
    }

    public bool IsIsolation
    {
        get => _isIsolation;
        set => SetField(ref _isIsolation, value);
    }

    public bool IsFullScreen
    {
        get => _isFullScreen;
        set => SetField(ref _isFullScreen, value);
    }

    public string GameWidth
    {
        get => _gameWidth;
        set => SetField(ref _gameWidth, value);
    }

    public string GameHeight
    {
        get => _gameHeight;
        set => SetField(ref _gameHeight, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        set => SetField(ref _windowTitle, value);
    }

    public string CustomInfo
    {
        get => _customInfo;
        set => SetField(ref _customInfo, value);
    }

    public bool JvmExtraAreaEnable
    {
        get => _jvmExtraAreaEnable;
        set => SetField(ref _jvmExtraAreaEnable, value);
    }

    public string JvmExtra
    {
        get => _jvmExtra;
        set => SetField(ref _jvmExtra, value);
    }

    public string GameTailArgs
    {
        get => _gameTailArgs;
        set => SetField(ref _gameTailArgs, value);
    }
}

public sealed class PlayerState : ObservableState, IPlayerState
{
    private string _verifyCode = "加载中...";
    private Player _currentPlayer = new();
    private ObservableCollection<Player> _players = [];

    public string VerifyCode
    {
        get => _verifyCode;
        set => SetField(ref _verifyCode, value);
    }

    public Player CurrentPlayer
    {
        get => _currentPlayer;
        set => SetField(ref _currentPlayer, value);
    }

    public ObservableCollection<Player> Players
    {
        get => _players;
        set => SetField(ref _players, value);
    }
}

public sealed class GameSelectionState : ObservableState, IGameSelectionState
{
    private MinecraftItem _currentGame = new();
    private DirItem _currentDir = new();
    private ObservableCollection<MinecraftItem> _games = [];
    private ObservableCollection<DirItem> _dirs = [];
    private string _renameVersionText = string.Empty;
    private string _renameVersionTips = string.Empty;

    public MinecraftItem CurrentGame
    {
        get => _currentGame;
        set => SetField(ref _currentGame, value);
    }

    public DirItem CurrentDir
    {
        get => _currentDir;
        set => SetField(ref _currentDir, value);
    }

    public ObservableCollection<MinecraftItem> Games
    {
        get => _games;
        set => SetField(ref _games, value);
    }

    public ObservableCollection<DirItem> Dirs
    {
        get => _dirs;
        set => SetField(ref _dirs, value);
    }

    public string RenameVersionText
    {
        get => _renameVersionText;
        set => SetField(ref _renameVersionText, value);
    }

    public string RenameVersionTips
    {
        get => _renameVersionTips;
        set => SetField(ref _renameVersionTips, value);
    }
}
