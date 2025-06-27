namespace StarFallMC.Entity;

public class Player {
    public string Name { get; set; }
    public string Skin { get; set; }
    public bool IsOnline { get; set; }
    public string UUID { get; set; }
    public string RefreshAddress { get; set; }
    public string OnlineLable { get; set; }
    
    
    public Player(){}
    
    public Player(string name, string skin, bool isOnline, string uuid) {
        Name = name;
        Skin = skin;
        IsOnline = isOnline;
        UUID = uuid;
        OnlineLable = isOnline ? "Visible" : "Hidden";
    }

    public override string ToString() {
        return $"Player : {Name} ({UUID}) - Skin: {Skin}, Online: {IsOnline}";
    }
    
    public override bool Equals(object? obj) {
        if (obj is Player other) {
            return other.Name == Name && other.Skin == Skin && other.IsOnline == IsOnline && other.UUID == UUID;
        }
        return false;
    }
    public override int GetHashCode() {
        return HashCode.Combine(Name, Skin, IsOnline, UUID);
    }
}