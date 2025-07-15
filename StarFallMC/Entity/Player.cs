namespace StarFallMC.Entity;

public class Player {
    public string Name { get; set; }
    public string Skin { get; set; }
    public bool IsOnline { get; set; }
    public string UUID { get; set; }
    public string RefreshAddress { get; set; }
    public string AccessToken { get; set; }
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
        return $"Player(Name: {Name}, Skin: {Skin}, IsOnline: {IsOnline}, UUID: {UUID}, RefreshAddress: {RefreshAddress}, AccessToken: {AccessToken})";
    }
    
    public override bool Equals(object? obj) {
        if (obj is Player other) {
            return other.Name == Name &&
                   other.Skin == Skin &&
                   other.IsOnline == IsOnline &&
                   other.UUID == UUID &&
                   other.RefreshAddress == RefreshAddress &&
                   other.AccessToken == AccessToken;
        }
        return false;
    }
    public override int GetHashCode() {
        return HashCode.Combine(Name, Skin, IsOnline, UUID, RefreshAddress, AccessToken);
    }
}