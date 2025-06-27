using Newtonsoft.Json.Linq;
using StarFallMC.Entity;

namespace StarFallMC.Util;

public class LoginUtil {
    public readonly static string clientId = "11c8deff-d17f-44ae-b8cf-068200d155a8";
        
    
        //获取Microsoft Device Code
        public static async Task<HttpResult> GetMicrosoftDeviceCode() {
            Console.WriteLine("获取Microsoft Device Code");
            Dictionary<string,Object> args = new () {
                {"client_id",clientId},
                {"scope","XboxLive.signin offline_access"}
            };
            return await HttpRequestUtil.Post("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode",args,null,HttpRequestUtil.RequestDataType.Form).ConfigureAwait(true);
        }

        //获取Microsoft Token
        public static async Task<HttpResult> GetMicrosoftToken(string deviceCode) {
            Console.WriteLine("获取Microsoft Token");
            Dictionary<string,Object> args = new () {
                {"client_id",clientId},
                {"device_code",deviceCode},
                {"grant_type","urn:ietf:params:oauth:grant-type:device_code"}
            };
            return await HttpRequestUtil.Post("https://login.microsoftonline.com/consumers/oauth2/v2.0/token",args,null,HttpRequestUtil.RequestDataType.Form).ConfigureAwait(true);
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
                // Console.WriteLine(jo["Token"].ToString());
                // Console.WriteLine(jo["DisplayClaims"]["xui"][0]["uhs"].ToString());
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
                // Console.WriteLine(jo["Token"].ToString());
                // Console.WriteLine(jo["DisplayClaims"]["xui"][0]["uhs"].ToString());
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
                // Console.WriteLine(jo["access_token"]);
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
                // Console.WriteLine(jo["id"]);
                // Console.WriteLine(jo["name"]);
                // Console.WriteLine(jo["skins"][0]["url"]);
                return r.Content;
            }
            Console.WriteLine(r.ErrorMessage);
            return "";
        }
    }