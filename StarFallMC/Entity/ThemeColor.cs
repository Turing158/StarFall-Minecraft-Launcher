namespace StarFallMC.Entity;

public class ThemeColor {
    public string Primary { get; set; } = "#D7C6C5";
    public string Primary_1 { get; set; } = "#C4ABAA";
    public string Primary_2 { get; set; } = "#B2908F";
    public string Primary_3 { get; set; } = "#957A78";
    public string Primary_4 { get; set; } = "#513938";
    public string Secondary { get; set; } = "#264446";
    public string Secondary_1 { get; set; } = "#113032";
    public string Secondary_2 { get; set; } = "#001D1F";

    public ThemeColor(string primary, string primary1, string primary2, string primary3, string primary4, string secondary, string secondary1, string secondary2) {
        Primary = primary;
        Primary_1 = primary1;
        Primary_2 = primary2;
        Primary_3 = primary3;
        Primary_4 = primary4;
        Secondary = secondary;
        Secondary_1 = secondary1;
        Secondary_2 = secondary2;
    }
}