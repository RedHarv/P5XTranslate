using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;

namespace P5XTranslateDL;

[BepInPlugin(pluginGuid, pluginName, pluginVersion)]
public class Plugin : BasePlugin
{
    public const string pluginGuid = "P5XTranslateDL";
    public const string pluginName = "P5X Translate DL";
    public const string pluginVersion = "0.3";

    private ConfigEntry<string> dlVersion;

    public override void Load()
    {
        Log.LogInfo($"Plugin {pluginGuid} is loaded!");
        dlVersion = Config.Bind("Downloader Version", "dlVer", "22", "");

        // Block here until the whole update chain (including P5XDL.exe) has
        // finished, instead of firing it off and letting Load() return
        // immediately. That way BepInEx can't move on to loading the next
        // plugin - e.g. AutoTranslator - until the translation files on disk
        // are fully up to date.
        Task.Run(GetFromGitAsync).GetAwaiter().GetResult();
    }

    private void RunFileUpdate()
    {
        Process firstProc = new();
        firstProc.StartInfo.FileName = @Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\P5XDL.exe";
        firstProc.EnableRaisingEvents = true;
        firstProc.Start();
        firstProc.WaitForExit();
    }

    private async Task GetFromGitAsync()
    {
        var pathString = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";
        var fileString = "https://api.github.com/repos/JayJay34/P5XTranslate/releases/latest";

        using var client = new HttpClient();
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", "Other");
            var content = await client.GetStringAsync(fileString);
            var jObj = JsonNode.Parse(content);

            foreach (var obj in jObj["assets"].AsArray())
            {
                if (obj["name"].ToString() == "P5XDL.zip" && dlVersion.Value != obj["node_id"].ToString())
                {
                    var fileDownload = await client.GetByteArrayAsync(obj["browser_download_url"].ToString());
                    await File.WriteAllBytesAsync(pathString + obj["name"].ToString(), fileDownload);
                    await Task.Run(() => ZipFile.ExtractToDirectory(@pathString + obj["name"].ToString(), pathString, true));
                    File.Delete(pathString + "P5XDL.zip");
                    dlVersion.Value = obj["node_id"].ToString();
                    Config.Save();
                }
            }
        }
        RunFileUpdate();
    }
}
