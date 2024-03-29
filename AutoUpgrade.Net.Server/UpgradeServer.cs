﻿using AutoUpgrade.Net.Json;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AutoUpgrade.Net.Server
{
    public class UpgradeServer
    {
        private string upgradeRoot = string.Empty;
        /// <summary>
        /// 服务端升级
        /// </summary>
        /// <param name="downloadRoot">升级目录</param>
        public UpgradeServer(string upgradeRoot)
        {
            this.upgradeRoot = upgradeRoot;
        }
        private static string baseDirector = AppContext.BaseDirectory;
        #region const
        private const int BufferSize = 80 * 1024;
        #endregion
        /// <summary> 升级根目录
        /// </summary>
        private string UpgradeRoot
        {
            get
            {
                string downloadRoot = Path.Combine(baseDirector, this.upgradeRoot);
                if (!Directory.Exists(downloadRoot))
                {
                    Directory.CreateDirectory(downloadRoot);
                }
                return downloadRoot;
            }
        }
        /// <summary> 通过文件名获取服务器上的文件路径
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private string GetServerFilePath(string fileName)
        {
            return Path.Combine(UpgradeRoot, fileName);
        }
        public JsonRespondResult CreateVersion(JsonReleaseVersion jsonReleaseVersion)
        {
            JsonReleaseVersion[] jsonReleaseVersions = this.GetVersionList();
            if (jsonReleaseVersions.Length > 0)
            {
                jsonReleaseVersion += jsonReleaseVersions[jsonReleaseVersions.Length - 1];
            }
            string dir = GetServerFilePath(jsonReleaseVersion.Version);
            string path = dir + ".json";
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, JsonConvert.SerializeObject(jsonReleaseVersion));
                }
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                if (File.Exists(path) && Directory.Exists(dir))
                {
                    return new JsonRespondResult() { Message = "新增版本" + jsonReleaseVersion.Version };
                }
                return new JsonRespondResult() { Result = false, Message = "版本" + jsonReleaseVersion.Version + "新增失败" };
            }
            catch (Exception ex)
            {
                return new JsonRespondResult() { Result = false, Message = ex.Message };
            }
        }
        public JsonRespondResult DeleteVersion(string version)
        {
            string dir = GetServerFilePath(version);
            string path = dir + ".json";
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
                if (!File.Exists(path) && !Directory.Exists(dir))
                {
                    return new JsonRespondResult() { Message = "已删除版本" + version };
                }
                return new JsonRespondResult() { Result = false, Message = "版本" + version + "删除失败" };
            }
            catch (Exception ex)
            {
                return new JsonRespondResult() { Result = false, Message = ex.Message };
            }
        }
        public JsonRespondResult DeleteFile(string fileName)
        {
            string filePath = GetServerFilePath(fileName);
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                if (!File.Exists(filePath))
                {
                    return new JsonRespondResult() { Message = "已删除文件" + fileName };
                }
                return new JsonRespondResult() { Result = false, Message = "文件" + fileName + "删除失败" };
            }
            catch (Exception ex)
            {
                return new JsonRespondResult() { Result = false, Message = ex.Message };
            }
        }
        public JsonReleaseVersion[] GetVersionList()
        {
            string[] files = Directory.GetFiles(this.UpgradeRoot, "*.json");
            return files.Select(f => JsonConvert.DeserializeObject<JsonReleaseVersion>(File.ReadAllText(f))).OrderBy(v => new Version(v.Version)).ToArray();
        }
        public JsonReleaseVersion CheckVersion(string version)
        {
            if (Version.TryParse(version, out Version v))
            {
                string[] files = Directory.GetFiles(this.UpgradeRoot, "*.json");
                JsonReleaseVersion[] jsonReleaseVersions = files
                .OrderByDescending(f => Version.Parse(Path.GetFileNameWithoutExtension(f)))
                .TakeWhile((f) => Version.Parse(Path.GetFileNameWithoutExtension(f)) > v)
                .Select(f => JsonConvert.DeserializeObject<JsonReleaseVersion>(File.ReadAllText(f))).ToArray();
                if (jsonReleaseVersions.Length == 0) { return null; }
                string updateContent = string.Join("\r\n-----------------------------------\r\n"
                    , jsonReleaseVersions
                    .Reverse()
                    .Select(j => "V" + j.Version + "版本更新内容：\r\n" + j.UpdateContent));
                jsonReleaseVersions[0].Type = jsonReleaseVersions.Select(j => j.Type).Aggregate((t1, t2) => (t1 == JsonReleaseVersion.ReleaseType.Force || t2 == JsonReleaseVersion.ReleaseType.Force) ? JsonReleaseVersion.ReleaseType.Force : JsonReleaseVersion.ReleaseType.Choice);
                jsonReleaseVersions[0].UpdateContent = updateContent;
                return jsonReleaseVersions[0];
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// 是否可升级
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool Upgradeable(string version)
        {
            if (Version.TryParse(version, out Version v))
            {
                return Directory.GetFiles(this.UpgradeRoot, "*.json")
                    .Select(f => Version.Parse(Path.GetFileNameWithoutExtension(f)))
                    .OrderByDescending(vs => vs)
                .FirstOrDefault() > v;
            }
            else
            {
                return false;
            }
        }
        public JsonRespondResult GetFileVersion(string fileName)
        {
            string filePath = Path.Combine(this.UpgradeRoot, fileName);
            if (File.Exists(filePath))
            {
                return new JsonRespondResult() { Message = FileVersionInfo.GetVersionInfo(filePath).ProductVersion };
            }
            else
            {
                return new JsonRespondResult() { Result = false, Message = "无法找到文件" };
            }
        }
    }
}
