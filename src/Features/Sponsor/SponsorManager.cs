using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CheryTools;

public sealed class SponsorRecord
{
    public readonly string DisplayName;
    public readonly string BilibiliUid;
    public readonly string BilibiliUrl;
    public readonly string KeyHash;
    public readonly string[] Features;

    public SponsorRecord(
        string displayName,
        string bilibiliUid,
        string bilibiliUrl,
        string keyHash,
        params string[] features)
    {
        DisplayName = displayName;
        BilibiliUid = bilibiliUid;
        BilibiliUrl = bilibiliUrl;
        KeyHash = keyHash;
        Features = features ?? Array.Empty<string>();
    }

    public bool HasFeature(string feature)
    {
        if (string.IsNullOrEmpty(feature) || Features == null)
            return false;

        for (int i = 0; i < Features.Length; i++)
        {
            if (string.Equals(Features[i], feature, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public enum SponsorRegistryState
{
    NotLoaded,
    Loading,
    Ready,
    Failed
}

public static class SponsorManager
{
    public const string SponsorTitleFeature = "sponsor_title";
    public const string RegistryUrl = "https://www.cherysui.cn/sponsors.js";

    private const string RegistryAssignment = "window.CheryToolsSponsorRegistry =";
    private const string RegistryEndMarker = "/* CHERYTOOLS_REGISTRY_JSON_END */";
    private const int DownloadTimeoutMilliseconds = 8000;

    private static readonly object SyncRoot = new object();
    private static SponsorRecord[] _remoteSponsors = Array.Empty<SponsorRecord>();
    private static SponsorRegistryState _state = SponsorRegistryState.NotLoaded;
    private static string _statusMessage = "尚未同步赞助者信息。";
    private static int _registryRevision;
    private static string _registryUpdatedAt = string.Empty;
    private static bool _refreshInProgress;

    public static SponsorRegistryState State
    {
        get
        {
            lock (SyncRoot)
                return _state;
        }
    }

    public static string StatusMessage
    {
        get
        {
            lock (SyncRoot)
                return _statusMessage;
        }
    }

    public static int RegistryRevision
    {
        get
        {
            lock (SyncRoot)
                return _registryRevision;
        }
    }

    public static string RegistryUpdatedAt
    {
        get
        {
            lock (SyncRoot)
                return _registryUpdatedAt;
        }
    }

    public static void EnsureLoaded()
    {
        lock (SyncRoot)
        {
            if (_state != SponsorRegistryState.NotLoaded)
                return;
        }

        Refresh();
    }

    public static void Refresh()
    {
        lock (SyncRoot)
        {
            if (_refreshInProgress)
                return;

            _refreshInProgress = true;
            _remoteSponsors = Array.Empty<SponsorRecord>();
            _registryRevision = 0;
            _registryUpdatedAt = string.Empty;
            _state = SponsorRegistryState.Loading;
            _statusMessage = "正在同步赞助者信息……";
        }

        Task.Run((Action)DownloadAndApplyRegistry);
    }

    public static SponsorRecord[] GetSponsorsSnapshot()
    {
        lock (SyncRoot)
            return (SponsorRecord[])_remoteSponsors.Clone();
    }

    public static SponsorRecord FindByHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return null;

        string normalized = hash.Trim();
        lock (SyncRoot)
        {
            if (_state != SponsorRegistryState.Ready)
                return null;

            for (int i = 0; i < _remoteSponsors.Length; i++)
            {
                SponsorRecord sponsor = _remoteSponsors[i];
                if (string.Equals(sponsor.KeyHash, normalized, StringComparison.OrdinalIgnoreCase))
                    return sponsor;
            }
        }

        return null;
    }

    public static bool TryAuthenticate(string key, out SponsorRecord sponsor, out string hash)
    {
        sponsor = null;
        hash = string.Empty;
        if (string.IsNullOrWhiteSpace(key) || State != SponsorRegistryState.Ready)
            return false;

        hash = ComputeSha256(key.Trim());
        sponsor = FindByHash(hash);
        return sponsor != null;
    }

    public static string ComputeSha256(string value)
    {
        if (value == null)
            value = string.Empty;

        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }

    private static void DownloadAndApplyRegistry()
    {
        try
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
                // Older runtimes may not expose TLS flags consistently; WebClient will use its default.
            }

            string separator = RegistryUrl.IndexOf('?') >= 0 ? "&" : "?";
            string requestUrl = RegistryUrl + separator + "refresh=" + DateTime.UtcNow.Ticks;
            string script;
            using (TimeoutWebClient client = new TimeoutWebClient(DownloadTimeoutMilliseconds))
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.Accept] = "application/javascript, text/javascript, */*";
                client.Headers[HttpRequestHeader.UserAgent] = "CheryTools-SponsorRegistry/1.0";
                script = client.DownloadString(requestUrl);
            }

            SponsorRecord[] sponsors;
            int revision;
            string updatedAt;
            ParseRegistry(script, out sponsors, out revision, out updatedAt);

            lock (SyncRoot)
            {
                _remoteSponsors = sponsors;
                _registryRevision = revision;
                _registryUpdatedAt = updatedAt;
                _state = SponsorRegistryState.Ready;
                _statusMessage = "赞助者信息已同步。";
                _refreshInProgress = false;
            }
        }
        catch (Exception ex)
        {
            lock (SyncRoot)
            {
                // 不保留内置名单或上一次成功结果；远程验证失败时不授予赞助者状态。
                _remoteSponsors = Array.Empty<SponsorRecord>();
                _registryRevision = 0;
                _registryUpdatedAt = string.Empty;
                _state = SponsorRegistryState.Failed;
                _statusMessage = "赞助者信息同步失败：" + GetSafeErrorMessage(ex);
                _refreshInProgress = false;
            }
        }
    }

    private static void ParseRegistry(
        string script,
        out SponsorRecord[] sponsors,
        out int revision,
        out string updatedAt)
    {
        if (string.IsNullOrWhiteSpace(script))
            throw new InvalidOperationException("服务器返回了空数据。");

        int assignmentIndex = script.IndexOf(RegistryAssignment, StringComparison.Ordinal);
        if (assignmentIndex < 0)
            throw new InvalidOperationException("赞助者数据格式不受支持。");

        int jsonStart = assignmentIndex + RegistryAssignment.Length;
        int endMarkerIndex = script.IndexOf(RegistryEndMarker, jsonStart, StringComparison.Ordinal);
        if (endMarkerIndex < 0)
            throw new InvalidOperationException("赞助者数据缺少结束标记。");

        string json = script.Substring(jsonStart, endMarkerIndex - jsonStart).Trim();
        if (json.EndsWith(";", StringComparison.Ordinal))
            json = json.Substring(0, json.Length - 1).TrimEnd();

        JObject root = JObject.Parse(json);
        int schemaVersion = root.Value<int?>("schemaVersion") ?? 0;
        if (schemaVersion != 1)
            throw new InvalidOperationException("赞助者数据版本不兼容。");

        revision = root.Value<int?>("revision") ?? 0;
        if (revision <= 0)
            throw new InvalidOperationException("赞助者数据修订号无效。");

        updatedAt = (root.Value<string>("updatedAt") ?? string.Empty).Trim();
        JArray sponsorArray = root["sponsors"] as JArray;
        if (sponsorArray == null)
            throw new InvalidOperationException("赞助者列表不存在。");

        List<SponsorRecord> parsed = new List<SponsorRecord>(sponsorArray.Count);
        HashSet<string> hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JToken token in sponsorArray)
        {
            JObject item = token as JObject;
            if (item == null || item.Value<bool?>("enabled") == false)
                continue;

            string name = (item.Value<string>("name") ?? string.Empty).Trim();
            string uid = (item.Value<string>("uid") ?? string.Empty).Trim();
            string url = (item.Value<string>("url") ?? string.Empty).Trim();
            string keyHash = (item.Value<string>("keyHash") ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uid))
                throw new InvalidOperationException("赞助者记录缺少名称或 UID。");
            if (!IsSha256Hex(keyHash) || !hashes.Add(keyHash))
                throw new InvalidOperationException("赞助者 Key Hash 无效或重复。");

            Uri parsedUrl;
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsedUrl) || parsedUrl.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("赞助者链接必须使用 HTTPS。");

            JArray featureArray = item["features"] as JArray;
            List<string> features = new List<string>();
            if (featureArray != null)
            {
                foreach (JToken featureToken in featureArray)
                {
                    string feature = (featureToken.Value<string>() ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(feature))
                        features.Add(feature);
                }
            }

            parsed.Add(new SponsorRecord(name, uid, url, keyHash, features.ToArray()));
        }

        sponsors = parsed.ToArray();
    }

    private static bool IsSha256Hex(string value)
    {
        if (value == null || value.Length != 64)
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!isHex)
                return false;
        }
        return true;
    }

    private static string GetSafeErrorMessage(Exception ex)
    {
        WebException webException = ex as WebException;
        if (webException != null)
        {
            if (webException.Status == WebExceptionStatus.Timeout)
                return "连接超时。";
            return "网络连接不可用（" + webException.Status + "）。";
        }

        return string.IsNullOrWhiteSpace(ex.Message) ? "未知错误。" : ex.Message;
    }

    private sealed class TimeoutWebClient : WebClient
    {
        private readonly int _timeoutMilliseconds;

        public TimeoutWebClient(int timeoutMilliseconds)
        {
            _timeoutMilliseconds = timeoutMilliseconds;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request == null)
                return null;

            request.Timeout = _timeoutMilliseconds;
            HttpWebRequest httpRequest = request as HttpWebRequest;
            if (httpRequest != null)
            {
                httpRequest.ReadWriteTimeout = _timeoutMilliseconds;
                httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
            return request;
        }
    }
}
