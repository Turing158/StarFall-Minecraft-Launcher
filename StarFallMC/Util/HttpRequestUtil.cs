using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using StarFallMC.Entity;

namespace StarFallMC.Util;

public class HttpRequestUtil {
    public readonly static HttpClient client = new ();
    
    private static CancellationTokenSource globalCts = new ();
    private static readonly object _lock = new ();
    public enum RequestDataType{
        Query,
        Form,
        Json,
    }
    
    public static async Task<HttpResult> Get(string url,Dictionary<string,Object> args = null,Dictionary<string,string> headers = null,RequestDataType dataType = RequestDataType.Query) {
        string queryString = "";
        if (args != null && args.Count > 0) {
            queryString += "?";
            foreach (var i in args) {
                queryString += $"{i.Key}={i.Value}&";
            }
        }
        var cts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token);
        try {
            var req = new HttpRequestMessage(HttpMethod.Get, url + queryString);
            if (headers != null && headers.Count > 0) {
                foreach (var i in headers) {
                    req.Headers.Add(i.Key, i.Value);
                }
            }
            var resp = await client.SendAsync(req, cts.Token);
            var content = await resp.Content.ReadAsStringAsync();
            
            return HttpResult.Success(resp,content,"Get");
        }
        catch (TaskCanceledException e) when (cts.IsCancellationRequested) {
            return HttpResult.Cancel("Get");
        }
        catch (HttpRequestException e) {
            return HttpResult.RequestError(e,"Get");
        }
        catch (TaskCanceledException e) {
            return HttpResult.Timeout("Get");
        }
        catch (Exception e) {
            return HttpResult.UnknownError(e,"Get");
        }
        
    }


    public static async Task<HttpResult> Post(string url,Dictionary<string,Object> args,Dictionary<string, string> headers = null,RequestDataType dataType = RequestDataType.Json) {
        HttpContent content;
        if (dataType == RequestDataType.Json) {
            var json = JsonConvert.SerializeObject(args);
            content = new StringContent(json,Encoding.UTF8,"application/json");
        }
        else {
            var formData = args
                .Select(kv => new KeyValuePair<string, string>(
                    kv.Key, 
                    kv.Value?.ToString() ?? ""
                ))
                .ToList();
            content = new FormUrlEncodedContent(formData);
        }
        var cts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token);
        try {
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            if (headers != null && headers.Count > 0) {
                foreach (var i in headers) {
                    req.Headers.Add(i.Key,i.Value);
                }
            }
            var resp = await client.SendAsync(req,cts.Token);
            var respContent = await resp.Content.ReadAsStringAsync();
            return HttpResult.Success(resp, respContent,"Post");
        }
        catch (TaskCanceledException e) when (cts.IsCancellationRequested) {
            return HttpResult.Cancel("Post");
        }
        catch (HttpRequestException e){
            return HttpResult.RequestError(e,"Post");
        }
        catch (TaskCanceledException e) {
            return HttpResult.Timeout("Post");
        }
        catch (Exception e) {
            return HttpResult.UnknownError(e,"Post");
        }
    }
    
    public static void StopRequest() {
        lock (_lock) {
            globalCts.Cancel();
            globalCts.Dispose();
            globalCts = new CancellationTokenSource();
        }
    }
}