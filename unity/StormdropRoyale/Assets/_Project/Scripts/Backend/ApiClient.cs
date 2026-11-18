using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Stormdrop
{
    public class ApiException : Exception
    {
        public readonly long Status;
        public ApiException(long status, string message) : base(message) { Status = status; }
    }

    /// <summary>Minimal async JSON client for the Stormdrop backend (UnityWebRequest + Newtonsoft).</summary>
    public static class ApiClient
    {
        public static string BaseUrl = "https://your-project.vercel.app/api";
        public static string SessionToken;     // player session (phones)
        public static string ServerSecret;     // dedicated servers only

        public static Task<T> Get<T>(string path) => Send<T>("GET", path, null);
        public static Task<T> Post<T>(string path, object body = null) => Send<T>("POST", path, body ?? new { });
        public static Task<T> Put<T>(string path, object body) => Send<T>("PUT", path, body);
        public static Task<T> Patch<T>(string path, object body) => Send<T>("PATCH", path, body);
        public static Task<T> Delete<T>(string path) => Send<T>("DELETE", path, null);

        static async Task<T> Send<T>(string method, string path, object body)
        {
            using (var req = new UnityWebRequest(BaseUrl.TrimEnd('/') + path, method))
            {
                if (body != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body)));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 20;
                if (!string.IsNullOrEmpty(SessionToken)) req.SetRequestHeader("Authorization", "Bearer " + SessionToken);
                if (!string.IsNullOrEmpty(ServerSecret)) req.SetRequestHeader("X-Server-Secret", ServerSecret);
                req.SetRequestHeader("X-Client-Version", GameConstants.Version);

                var tcs = new TaskCompletionSource<bool>();
                req.SendWebRequest().completed += _ => tcs.TrySetResult(true);
                await tcs.Task;

                string text = req.downloadHandler.text;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    string msg = req.error;
                    try { msg = (string)JObject.Parse(text)["error"] ?? msg; } catch { }
                    if (req.responseCode == 0) msg = "No connection. Check your internet and try again.";
                    throw new ApiException(req.responseCode, $"{msg} ({req.responseCode})");
                }
                return string.IsNullOrEmpty(text) ? default : JsonConvert.DeserializeObject<T>(text);
            }
        }
    }
}
