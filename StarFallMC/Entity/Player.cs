using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StarFallMC.Entity;

public class Player : INotifyPropertyChanged{
    private string _name;
    public string Name { get => _name; set => SetField(ref _name, value); }
    private string _skin;
    public string Skin { get => _skin; set => SetField(ref _skin, value); }
    private bool _isOnline;
    public bool IsOnline { get => _isOnline; set => SetField(ref _isOnline, value); }
    private string _uuid;
    public string UUID { get => _uuid; set => SetField(ref _uuid, value); }
    private string _refreshToken;
    public string RefreshToken { get => _refreshToken; set => SetField(ref _refreshToken, value); }
    private string _accessToken;
    public string AccessToken { get => _accessToken; set => SetField(ref _accessToken, value); }
    private string _onlineLable;
    public string OnlineLable { get => _onlineLable; set => SetField(ref _onlineLable, value); }
    
    
    public Player(){}
    
    public Player(string name, string skin, bool isOnline, string uuid) {
        Name = name;
        Skin = skin;
        IsOnline = isOnline;
        UUID = uuid;
        OnlineLable = isOnline ? "Visible" : "Hidden";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public override string ToString() {
        return $"Player(Name: {Name}, Skin: {Skin}, IsOnline: {IsOnline}, UUID: {UUID}, RefreshToken: {RefreshToken}, AccessToken: {AccessToken})";
    }
    
    public override bool Equals(object? obj) {
        if (obj is Player other) {
            return other.Name == Name &&
                   other.Skin == Skin &&
                   other.IsOnline == IsOnline &&
                   other.UUID == UUID &&
                   other.RefreshToken == RefreshToken &&
                   other.AccessToken == AccessToken;
        }
        return false;
    }
    public override int GetHashCode() {
        return HashCode.Combine(Name, Skin, IsOnline, UUID, RefreshToken, AccessToken);
    }
}