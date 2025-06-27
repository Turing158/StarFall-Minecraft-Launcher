using System.Net;
using System.Net.Http;

namespace StarFallMC.Entity;

public class HttpResult {
    public bool IsSuccess { get; set; }
            public HttpStatusCode StatusCode { get; set; }
            public string Content { get; set; }
            public string ErrorMessage { get; set; }
            public ErrorTypeEnum ErrorType { get; set; }
            
            public enum ErrorTypeEnum {
                None,
                HttpError,
                NetworkError,
                Cancel,
                Timeout,
                UnknownError
            }

            public static HttpResult Success(HttpResponseMessage resp,string content,string requestType) {
                return new HttpResult {
                    IsSuccess = resp.IsSuccessStatusCode,
                    StatusCode = resp.StatusCode,
                    Content = content,
                    ErrorMessage = resp.IsSuccessStatusCode
                        ? ""
                        : $"{requestType}请求错误\n错误状态码：{resp.StatusCode}\\n错误信息{resp.ReasonPhrase}",
                    ErrorType = resp.IsSuccessStatusCode
                        ? ErrorTypeEnum.None
                        : ErrorTypeEnum.HttpError
                };
            }

            
            
            public static HttpResult RequestError(HttpRequestException e,string requestType) {
                return new HttpResult {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    ErrorMessage = $"{requestType}网络请求失败: {e.Message}",
                    ErrorType = ErrorTypeEnum.NetworkError
                };
            }

            public static HttpResult Cancel(string requestType) {
                return new HttpResult {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.RequestTimeout,
                    ErrorMessage = $"{requestType}请求手动取消",
                    ErrorType = ErrorTypeEnum.Cancel
                };
            }
            
            public static HttpResult Timeout(string requestType) {
                return new HttpResult {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.RequestTimeout,
                    ErrorMessage = $"{requestType}请求超时",
                    ErrorType = ErrorTypeEnum.Timeout
                };
            }

            public static HttpResult UnknownError(Exception e,string requestType) {
                return new HttpResult {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    ErrorMessage = $"{requestType}未知错误: {e.Message}",
                    ErrorType = ErrorTypeEnum.UnknownError
                };
            }
}