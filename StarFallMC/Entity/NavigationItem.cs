using System.Collections;

namespace StarFallMC.Entity;

public class NavigationItem {
    public string Name { get; set; }
    public string Path { get; set; }
    public string Type { get; set; }
    public double FontSize { get; set; } = -1;
    public int ChildrenIndex { get; set; }
    public IList Children { get; set; }
    
    
    

    public NavigationItem() {
    }

    public NavigationItem(string name) {
        Name = name;
    }

    public NavigationItem(string name, string path) {
        Name = name;
        Path = path;
    }

    public NavigationItem(string name, int childrenIndex, IList children) {
        Name = name;
        ChildrenIndex = childrenIndex;
        Children = children;
    }

    public NavigationItem(string name, int fontSize, int childrenIndex, IList children) {
        Name = name;
        FontSize = fontSize;
        ChildrenIndex = childrenIndex;
        Children = children;
    }

    public NavigationItem(string name, string path, double fontSize) {
        Name = name;
        Path = path;
        FontSize = fontSize;
    }

    public NavigationItem(string name, string path, double fontSize, int childrenIndex, IList children) {
        Name = name;
        Path = path;
        FontSize = fontSize;
        ChildrenIndex = childrenIndex;
        Children = children;
    }

    public NavigationItem(string name, string path, int childrenIndex, IList children) {
        Name = name;
        Path = path;
        ChildrenIndex = childrenIndex;
        Children = children;
    }
}