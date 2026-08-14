using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace CheryTools;

public sealed class ConfigShareResult
{
    public string Type = "";
    public string Code = "";
    public string ExpiresAt = "";
    public string Sha256 = "";
    public string FilePath = "";
    public long FileSize;
    public int FormatVersion;
    public int ExportWidth;
    public int ExportHeight;
}

public static class ConfigShareClient
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly ConcurrentQueue<Action> MainThreadActions = new ConcurrentQueue<Action>();
    private static int _activeOperations;

    public static bool IsBusy => Volatile.Read(ref _activeOperations) > 0;

    public static string ApiBaseUrl
    {
        get
        {
#if DEBUG
            return "http://localhost/CheryToolsHub";
#else
            return "https://cherytoolshub.adofaitools.top";
#endif
        }
    }

    public static bool TryNormalizeCode(string value, out string code)
    {
        code = (value ?? string.Empty).Trim().ToUpperInvariant();
        code = code.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
        if (code.Length != 6)
        {
            code = "";
            return false;
        }
        foreach (char character in code)
        {
            if (CodeAlphabet.IndexOf(character) < 0)
            {
                code = "";
                return false;
            }
        }
        return true;
    }

    public static void UploadPackage(string type, string packagePath, Action<ConfigShareResult, Exception> callback)
    {
        QueueOperation(() => UploadPackageInternal(type, packagePath), callback);
    }

    public static void DownloadPackage(string type, string code, Action<ConfigShareResult, Exception> callback)
    {
        QueueOperation(() => DownloadPackageInternal(type, code), callback);
    }

    public static void PumpMainThread()
    {
        while (MainThreadActions.TryDequeue(out Action action))
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Main.Logger?.Error("Config share callback failed: " + exception);
            }
        }
    }

    private static void QueueOperation(Func<ConfigShareResult> operation, Action<ConfigShareResult, Exception> callback)
    {
        Interlocked.Increment(ref _activeOperations);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                ConfigShareResult result = operation();
                Enqueue(() => callback?.Invoke(result, null));
            }
            catch (Exception exception)
            {
                Enqueue(() => callback?.Invoke(null, exception));
            }
            finally
            {
                Interlocked.Decrement(ref _activeOperations);
            }
        });
    }

    private static void Enqueue(Action action)
    {
        MainThreadActions.Enqueue(action);
    }

    private static ConfigShareResult UploadPackageInternal(string type, string packagePath)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("配置包不存在。", packagePath);
        }
        string normalizedType = NormalizeType(type);
        JObject data = SendMultipartUpload(normalizedType, packagePath);
        return ReadResult(data, normalizedType, "");
    }

    private static ConfigShareResult DownloadPackageInternal(string type, string code)
    {
        string normalizedType = NormalizeType(type);
        if (!TryNormalizeCode(code, out string normalizedCode))
        {
            throw new InvalidOperationException("配置码必须是 6 位大写字母和数字。 ");
        }

        JObject metadata = SendJsonRequest(
            "GET",
            ApiBaseUrl + "/configs/metadata.php?type=" + Uri.EscapeDataString(normalizedType) + "&code=" + Uri.EscapeDataString(normalizedCode),
            null
        );
        ConfigShareResult result = ReadResult(metadata, normalizedType, normalizedCode);
        string directory = Path.Combine(Path.GetTempPath(), "CheryTools_ConfigShare");
        Directory.CreateDirectory(directory);
        string outputPath = Path.Combine(directory, "CheryTools-" + normalizedType + "-" + normalizedCode + "-" + Guid.NewGuid().ToString("N") + "." + normalizedType);
        string checksumHeader;
        try
        {
            checksumHeader = DownloadFile(
                ApiBaseUrl + "/configs/download.php?type=" + Uri.EscapeDataString(normalizedType) + "&code=" + Uri.EscapeDataString(normalizedCode),
                outputPath
            );
        }
        catch
        {
            TryDelete(outputPath);
            throw;
        }

        string actualChecksum = ComputeSha256(outputPath);
        if (!string.Equals(actualChecksum, result.Sha256, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(checksumHeader) && !string.Equals(actualChecksum, checksumHeader, StringComparison.OrdinalIgnoreCase)))
        {
            TryDelete(outputPath);
            throw new InvalidDataException("下载文件 SHA-256 校验失败。 ");
        }
        result.FilePath = outputPath;
        result.FileSize = new FileInfo(outputPath).Length;
        return result;
    }

    private static string NormalizeType(string type)
    {
        string normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized != "cyt" && normalized != "ctkv" && normalized != "ctov")
        {
            throw new InvalidOperationException("配置类型不正确。 ");
        }
        return normalized;
    }

    private static JObject SendMultipartUpload(string type, string packagePath)
    {
        string boundary = "----CheryToolsBoundary" + Guid.NewGuid().ToString("N");
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ApiBaseUrl + "/configs/upload.php");
        request.Method = "POST";
        request.Timeout = 120000;
        request.ReadWriteTimeout = 120000;
        request.UserAgent = "CheryTools/" + BuildInfo.DisplayVersion;
        request.ContentType = "multipart/form-data; boundary=" + boundary;

        byte[] header = Encoding.UTF8.GetBytes(
            "--" + boundary + "\r\n" +
            "Content-Disposition: form-data; name=\"package\"; filename=\"" + Path.GetFileName(packagePath) + "\"\r\n" +
            "Content-Type: application/zip\r\n\r\n");
        byte[] footer = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");
        request.ContentLength = header.Length + new FileInfo(packagePath).Length + footer.Length;

        using (Stream stream = request.GetRequestStream())
        using (FileStream file = File.OpenRead(packagePath))
        {
            stream.Write(header, 0, header.Length);
            file.CopyTo(stream);
            stream.Write(footer, 0, footer.Length);
        }
        return ReadJsonResponse(request);
    }

    private static JObject SendJsonRequest(string method, string url, byte[] body)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = method;
        request.Timeout = 120000;
        request.ReadWriteTimeout = 120000;
        request.UserAgent = "CheryTools/" + BuildInfo.DisplayVersion;
        if (body != null)
        {
            request.ContentType = "application/json; charset=utf-8";
            request.ContentLength = body.Length;
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }
        }
        return ReadJsonResponse(request);
    }

    private static JObject ReadJsonResponse(HttpWebRequest request)
    {
        try
        {
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                return ParseEnvelope(reader.ReadToEnd());
            }
        }
        catch (WebException exception)
        {
            string message = exception.Message;
            if (exception.Response is HttpWebResponse response)
            {
                using (response)
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    try
                    {
                        JObject error = JObject.Parse(reader.ReadToEnd());
                        message = error["error"]?["message"]?.ToString() ?? message;
                    }
                    catch
                    {
                        // 保留网络错误原文。
                    }
                }
            }
            throw new InvalidOperationException(message, exception);
        }
    }

    private static JObject ParseEnvelope(string json)
    {
        JObject envelope = JObject.Parse(json);
        if (envelope["ok"]?.Value<bool>() != true)
        {
            throw new InvalidOperationException(envelope["error"]?["message"]?.ToString() ?? "服务器返回了错误。 ");
        }
        return (JObject)(envelope["data"] ?? new JObject());
    }

    private static ConfigShareResult ReadResult(JObject data, string type, string fallbackCode)
    {
        return new ConfigShareResult
        {
            Type = data["type"]?.ToString() ?? type,
            Code = data["code"]?.ToString() ?? fallbackCode,
            ExpiresAt = data["expiresAt"]?.ToString() ?? "",
            Sha256 = data["sha256"]?.ToString() ?? "",
            FileSize = data["fileSize"]?.Value<long>() ?? 0L,
            FormatVersion = data["formatVersion"]?.Value<int>() ?? 0,
            ExportWidth = data["exportWidth"]?.Value<int>() ?? 0,
            ExportHeight = data["exportHeight"]?.Value<int>() ?? 0,
        };
    }

    private static string DownloadFile(string url, string outputPath)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "GET";
        request.Timeout = 120000;
        request.ReadWriteTimeout = 120000;
        request.UserAgent = "CheryTools/" + BuildInfo.DisplayVersion;
        try
        {
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (FileStream output = File.Create(outputPath))
            {
                input.CopyTo(output);
                return response.Headers["X-Checksum-SHA256"] ?? "";
            }
        }
        catch (WebException exception)
        {
            string message = exception.Message;
            if (exception.Response is HttpWebResponse response)
            {
                using (response)
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    try
                    {
                        JObject error = JObject.Parse(reader.ReadToEnd());
                        message = error["error"]?["message"]?.ToString() ?? message;
                    }
                    catch
                    {
                        // 保留网络错误原文。
                    }
                }
            }
            throw new InvalidOperationException(message, exception);
        }
    }

    private static string ComputeSha256(string path)
    {
        using (SHA256 sha256 = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 临时文件删除失败不应覆盖原始下载错误。
        }
    }
}
