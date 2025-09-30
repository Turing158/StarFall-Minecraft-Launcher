using Newtonsoft.Json.Linq;
using StarFallMC.Entity;

namespace StarFallMC.Util;

public class LoginUtil {
        
    
        //获取Microsoft Device Code
        public static async Task<HttpResult> GetMicrosoftDeviceCode() {
            Console.WriteLine("获取Microsoft Device Code");
            Dictionary<string,Object> args = new () {
                {"client_id",KeyUtil.MICROSOFT_KEY_CLIENT_ID},
                {"scope","XboxLive.signin offline_access"}
            };
            return await HttpRequestUtil.Post("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode",args,null,HttpRequestUtil.RequestDataType.Form).ConfigureAwait(true);
        }

        //获取Microsoft Token
        public static async Task<HttpResult> GetMicrosoftToken(string deviceCode) {
            Console.WriteLine("获取Microsoft Token");
            Dictionary<string,Object> args = new () {
                {"client_id",KeyUtil.MICROSOFT_KEY_CLIENT_ID},
                {"device_code",deviceCode},
                {"grant_type","urn:ietf:params:oauth:grant-type:device_code"}
            };
            return await HttpRequestUtil.Post("https://login.microsoftonline.com/consumers/oauth2/v2.0/token",args,null,HttpRequestUtil.RequestDataType.Form).ConfigureAwait(true);
        }

        //刷新Microsoft Token
        public static async Task<Player> RefreshMicrosoftToken(Player player) {
            Console.WriteLine("刷新Microsoft Token");
            Dictionary<string,Object> args = new () {
                {"client_id",KeyUtil.MICROSOFT_KEY_CLIENT_ID},
                {"refresh_token",player.RefreshToken},
                {"grant_type","refresh_token"},
                {"scope","XboxLive.signin offline_access"}
            };
            var r =await HttpRequestUtil.Post("https://login.microsoftonline.com/consumers/oauth2/v2.0/token",args,null,HttpRequestUtil.RequestDataType.Form).ConfigureAwait(true);
            if(r.IsSuccess){
                JObject jo = JObject.Parse(r.Content);
                var refreshToken = jo["refresh_token"].ToString();
                var result = await GetXboxLiveToken(jo["access_token"].ToString()).ConfigureAwait(true);
                var jo2 = JObject.Parse(result);
                if (string.IsNullOrEmpty(result) || jo2["error"] == null) {
                    var newPlayer = new Player(
                        jo2["name"].ToString(), 
                        jo2["skins"][0]["url"].ToString(), 
                        true, 
                        jo2["id"].ToString()
                    );
                    newPlayer.RefreshToken = refreshToken;
                    newPlayer.AccessToken = jo2["access_token"].ToString();
                    RefreshPlayer(newPlayer);
                    return newPlayer;
                }
            }
            Console.WriteLine(r.ErrorMessage);
            return null;
        }

        public static void RefreshPlayer(Player player) {
            PlayerManage.ViewModel pmvm = PlayerManage.GetViewModel?.Invoke();
            if (pmvm != null) {
                var currentPlayer = pmvm.Players.First(i => i.UUID == player.UUID && i.IsOnline);
                var index = pmvm.Players.IndexOf(currentPlayer);
                if (index != -1) {
                    PlayerManage.GetViewModel.Invoke().Players[index] = player;
                }
                PlayerManage.SetPlayerListItem.Invoke(player);
            }
            else {
                PropertiesUtil.loadJson["player"]["player"] = JObject.FromObject(player);
                var objs = PropertiesUtil.loadJson["player"]["players"].ToObject<List<Player>>();
                int index = objs.FindIndex(i => i.UUID == player.UUID && i.IsOnline);
                if (index != -1) {
                    objs[index] = player;
                }
                PropertiesUtil.loadJson["player"]["players"] = JObject.FromObject(objs);
                Home.SetPlayer?.Invoke(player);
            }
        }
        
        //获取Xbox Live Token
        public static async Task<string> GetXboxLiveToken(string accessToken) {
            Console.WriteLine("获取Xbox Live Token");
            PlayerManage.SetLoadingText?.Invoke("获取Xbox Live Token中...");
            Dictionary<string,Object> args = new (){
                {"Properties",new Dictionary<string,Object> {
                    {"AuthMethod","RPS"},
                    {"SiteName","user.auth.xboxlive.com"},
                    {"RpsTicket",$"d={accessToken}"},
                } },
                {"RelyingParty","http://auth.xboxlive.com"},
                {"TokenType","JWT"},
            };
            var r = await HttpRequestUtil.Post("https://user.auth.xboxlive.com/user/authenticate",args).ConfigureAwait(true);
            if (r.IsSuccess) {
                JObject jo = JObject.Parse(r.Content);
                return await GetXSTSToken(jo["Token"].ToString(),jo["DisplayClaims"]["xui"][0]["uhs"].ToString()).ConfigureAwait(true);
            }
            Console.WriteLine(r.ErrorMessage);
            return "";
        }

        
        //获取XSTS Token
        private static async Task<string> GetXSTSToken(string xboxLiveToken,string uhs) {
            Console.WriteLine("获取XSTS Token");
            PlayerManage.SetLoadingText?.Invoke("获取XSTS Token中...");
            Dictionary<string,Object> args = new () {
                {"Properties",new Dictionary<string,Object> {
                    {"SandboxId","RETAIL"},
                    {"UserTokens",new List<string> {xboxLiveToken} },
                } },
                {"RelyingParty","rp://api.minecraftservices.com/"},
                {"TokenType","JWT"},
            };
            var r = await HttpRequestUtil.Post("https://xsts.auth.xboxlive.com/xsts/authorize",args);
            if (r.IsSuccess) {
                JObject jo = JObject.Parse(r.Content);
                return await GetMinecraftToken(jo["Token"].ToString(),jo["DisplayClaims"]["xui"][0]["uhs"].ToString()).ConfigureAwait(true);
            }
            Console.WriteLine(r.ErrorMessage);
            return "";
        }

        //获取Minecraft Token
        public static async Task<string> GetMinecraftToken(string XTSTtoken,string uhs) {
            Console.WriteLine("获取Minecraft Token");
            PlayerManage.SetLoadingText?.Invoke("获取Minecraft Token中...");
            Dictionary<string,Object> args = new () {
                    {"identityToken",$"XBL3.0 x={uhs};{XTSTtoken}"}
                };
            var r = await HttpRequestUtil.Post("https://api.minecraftservices.com/authentication/login_with_xbox",args).ConfigureAwait(true);
            if (r.IsSuccess) {
                JObject jo = JObject.Parse(r.Content);
                return await GetMinecraftInfo(jo["access_token"].ToString()).ConfigureAwait(true);
            }
            Console.WriteLine(r.ErrorMessage);
            return "";
        }

        //获取Minecraft用户信息
        public static async Task<string> GetMinecraftInfo(string accessToken) {
            Console.WriteLine("获取Minecraft用户信息");
            PlayerManage.SetLoadingText?.Invoke("获取Minecraft用户信息中...");
            Dictionary<string,string> headers = new () {
                {"Authorization",$"Bearer {accessToken}"}
            };
            var r = await HttpRequestUtil.Get("https://api.minecraftservices.com/minecraft/profile",null,headers).ConfigureAwait(true);
            if (r.IsSuccess) {
                JObject jo = JObject.Parse(r.Content);
                jo["access_token"] = accessToken;
                return jo.ToString();
            }
            Console.WriteLine(r.ErrorMessage);
            return "";
        }
    }