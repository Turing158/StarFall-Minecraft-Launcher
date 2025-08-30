using System.Windows.Controls;
using System.Windows.Media.Animation;
namespace StarFallMC.Component;

public partial class LittleTips : UserControl {
    
    private List<string> tips = new List<string> {
        "在Minecraft中，右键可以开门。",
        "干掉村民可以驯服铁傀儡。",
        "垂直挖矿法能够更快的挖到你想要的矿。",
        "拥抱一下僵尸村民可以把他娶回家。",
        "将地狱顶层的基岩破开可以找到更多远古残骸。",
        "在地狱极速下落记得使用落地水。",
        "地狱中睡觉可以快速返回主世界。",
        "在Minecraft中，你可以玩到Minecraft。",
        "在原版生存中，用萤石搭建与地狱门一样的框架可以传送到天堂。",
        "如果晚上怕冷，可以用打火石点着房子取暖。",
        "村民与僵尸非常的友好，记得给僵尸开门让僵尸给村民一个拥抱。"
    };
    private double duration = 8.0;
    
    
    private Storyboard ShowStoryboard;
    private Storyboard HideStoryboard;
    private Storyboard TipsStoryboard;

    private Timer changeTimer;
    private Timer textTimer;
    
    public LittleTips() {
        InitializeComponent();
        ShowStoryboard = (Storyboard)FindResource("Show");
        HideStoryboard = (Storyboard)FindResource("Hide");
        TipsStoryboard = (Storyboard)FindResource("TipsChange");
    }

    public void Show() {
        ShowStoryboard.Begin();
        getRandomTip();
        int dur = (int)duration*1000;
        changeTimer = new Timer(s => {
            this.Dispatcher.BeginInvoke(() => {
                TipsStoryboard.Begin();
                textTimer = new Timer(s => {
                    this.Dispatcher.BeginInvoke(() => {
                        getRandomTip();
                        textTimer.Dispose();
                    });
                },null,200,0);
            });
        },null,dur, dur);
    }

    public void Hide() {
        changeTimer?.Dispose();
        textTimer?.Dispose();
        HideStoryboard.Begin();
    }
    
    private void getRandomTip() {
        Random random = new Random();
        int index = random.Next(tips.Count);
        TipsText.Text = tips[index];
    }
}