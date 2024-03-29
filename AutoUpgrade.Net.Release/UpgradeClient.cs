﻿using AutoUpgrade.Net.Args;
using AutoUpgrade.Net.Core;
using AutoUpgrade.Net.Delegates;
using AutoUpgrade.Net.Json;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AutoUpgrade.Net.Release
{
    public class UpgradeClient
    {
        #region 事件
        /// <summary>
        /// 升级进度变化事件
        /// </summary>
        public event ProgressChangedHandler UpgradeProgressChanged;
        protected virtual void OnUpgradeProgressChanged(ProgressChangedArgs progressChangedArgs)
        {
            this.UpgradeProgressChanged?.Invoke(this, progressChangedArgs);
        }
        /// <summary>
        /// 升级速度变化事件
        /// </summary>
        public event SpeedChangedHandler UpgradeSpeedChanged;
        protected virtual void OnUpgradeSpeedChanged(SpeedChangedArgs speedChangedArgs)
        {
            this.UpgradeSpeedChanged?.Invoke(this, speedChangedArgs);
        }
        /// <summary>
        /// 升级完成事件
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="speed"></param>
        public event CompletedHandler UpgradeCompleted;
        protected virtual void OnUpgradeCompleted(CompletedArgs completedArgs)
        {
            this.UpgradeCompleted?.Invoke(this, completedArgs);
        }
        /// <summary>
        /// 升级错误事件
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="speed"></param>
        public event ErrorHandler UpgradeError;
        protected virtual void OnUpgradeError(ErrorArgs errorArgs)
        {
            this.UpgradeError?.Invoke(this, errorArgs);
        }
        #endregion
        private string url = string.Empty;
        private UploadClient clientUpload = null;
        private long totalProgress = 0;
        private long currentProgress = 0;
        public UpgradeClient(string url)
        {
            this.url = url;
            this.clientUpload = new UploadClient(url + "/upload", url + "/merge");
            this.clientUpload.UploadProgressChanged += (s, e) =>
            {
                currentProgress += e.Read;
                this.OnUpgradeProgressChanged(new ProgressChangedArgs(e.Read, currentProgress, totalProgress));
            };
        }
        /// <summary> 删除版本
        /// </summary>
        /// <param name="version">版本号</param>
        public async Task<bool> DeleteVersion(string version)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    HttpResponseMessage httpResponseMessage = await httpClient.DeleteAsync(this.url + "/deleteVersion?version=" + version);
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        JsonRespondResult respondResult = JsonConvert.DeserializeObject<JsonRespondResult>(await httpResponseMessage.Content.ReadAsStringAsync());
                        if (!respondResult.Result)
                        {
                            this.OnUpgradeError(new ErrorArgs(respondResult.Message));
                        }
                        return respondResult.Result;
                    }
                }
                catch (Exception ex)
                {
                    this.OnUpgradeError(new ErrorArgs(ex.Message));
                    return true;
                }
            }
            return false;
        }
        /// <summary> 新增版本
        /// </summary>
        /// <param name="version">版本号</param>
        public async Task<bool> CreateVersion(JsonReleaseVersion jsonReleaseVersion)
        {
            using (HttpClient client = new HttpClient(new HttpClientHandler() { UseCookies = false }))//若想手动设置Cookie则必须设置UseCookies = false
            {
                StringContent stringContent = new StringContent(JsonConvert.SerializeObject(jsonReleaseVersion));
                stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                try
                {
                    var result = await client.PostAsync(new Uri(this.url + "/createVersion"), stringContent);
                    if (result.IsSuccessStatusCode)
                    {
                        JsonRespondResult respondResult = JsonConvert.DeserializeObject<JsonRespondResult>(await result.Content.ReadAsStringAsync());
                        if (!respondResult.Result)
                        {
                            this.OnUpgradeError(new ErrorArgs(respondResult.Message));
                        }
                        return respondResult.Result;
                    }
                }
                catch (Exception ex)
                {
                    this.OnUpgradeError(new ErrorArgs(ex.Message));
                    return false;
                }
            }
            return true;
        }
        /// <summary> 删除版本
        /// </summary>
        /// <param name="version">版本号</param>
        public async Task<JsonReleaseVersion[]> GetVersionList()
        {
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(this.url + "/getVersionList");
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        JsonReleaseVersion[] jsonReleaseVersions = JsonConvert.DeserializeObject<JsonReleaseVersion[]>(await httpResponseMessage.Content.ReadAsStringAsync());
                        return jsonReleaseVersions;
                    }
                }
                catch (Exception ex)
                {
                    this.OnUpgradeError(new ErrorArgs(ex.Message));
                }
            }
            return null;
        }
        public async Task<bool> Upgrade(string root, JsonReleaseVersion jsonReleaseVersion)
        {
            this.currentProgress = 0;
            this.totalProgress = jsonReleaseVersion.Files.Sum(f => f.Length);
            bool success = true;
            foreach (JsonFileDetail jsonFileDetail in jsonReleaseVersion.Files)
            {
                if (!(success &= await this.clientUpload.UploadTaskAsync(Path.Combine(root, jsonFileDetail.Name), Path.Combine(jsonReleaseVersion.Version, jsonFileDetail.Name.Replace(Path.GetFileName(jsonFileDetail.Name), "")).Replace(Path.DirectorySeparatorChar, '/'))))
                {
                    break;
                }
            }
            if (await this.CreateVersion(jsonReleaseVersion))
            {
                this.OnUpgradeCompleted(new CompletedArgs(success));
                return success;
            }
            else
            {
                this.OnUpgradeCompleted(new CompletedArgs(false));
                return false;
            }
        }
        /// <summary>
        /// 更新升级程序
        /// </summary>
        /// <param name="upgradeProgramePath"></param>
        /// <returns></returns>
        public async Task<bool> UpdateUpgradePrograme(string upgradeProgramePath)
        {
            return await this.clientUpload.UploadTaskAsync(upgradeProgramePath);
        }
        /// <summary>
        /// 获取文件版本号
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public async Task<string> GetVersion(string fileName)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(this.url + "/getFileVersion?fileName=" + fileName);
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        JsonRespondResult respondResult = JsonConvert.DeserializeObject<JsonRespondResult>(await httpResponseMessage.Content.ReadAsStringAsync());
                        if (respondResult.Result)
                        {
                            return respondResult.Message;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.OnUpgradeError(new ErrorArgs(ex.Message));
                }
            }
            return null;
        }
    }
}
