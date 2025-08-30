namespace StarFallMC.Entity;

public class NavigationItem {
    public string Name { get; set; }
    public string Path { get; set; }
    public string Type { get; set; }

    public NavigationItem() {
    }

    public NavigationItem(string name) {
        Name = name;
    }

    public NavigationItem(string name, string path) {
        Name = name;
        Path = path;
    }
}