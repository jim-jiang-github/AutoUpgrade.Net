﻿using AutoUpgrade.Net.Args;
using AutoUpgrade.Net.Delegates;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AutoUpgrade.Net.Core
{
    /// <summary> 客户端下载
    /// </summary>
    public class DownloadClient
    {
        #region 事件
        /// <summary>
        /// 下载进度变化事件
        /// </summary>
        public event ProgressChangedHandler DownloadProgressChanged;
        protected virtual void OnDownloadProgressChanged(ProgressChangedArgs progressChangedArgs)
        {
            this.DownloadProgressChanged?.Invoke(this, progressChangedArgs);
        }
        /// <summary>
        /// 下载速度变化事件
        /// </summary>
        public event SpeedChangedHandler DownloadSpeedChanged;
        protected virtual void OnDownloadSpeedChanged(SpeedChangedArgs speedChangedArgs)
        {
            this.DownloadSpeedChanged?.Invoke(this, speedChangedArgs);
        }
        /// <summary>
        /// 下载完成事件
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="speed"></param>
        public event CompletedHandler DownloadCompleted;
        protected virtual void OnDownloadCompleted(CompletedArgs completedArgs)
        {
            this.DownloadCompleted?.Invoke(this, completedArgs);
        }
        /// <summary>
        /// 下载错误事件
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="speed"></param>
        public event ErrorHandler DownloadError;
        protected virtual void OnDownloadError(ErrorArgs errorArgs)
        {
            this.DownloadError?.Invoke(this, errorArgs);
        }
        #endregion
        #region 变量
        /// <summary> 下载的url
        /// </summary>
        private string downloadUrl = string.Empty;
        #endregion
        public DownloadClient(string downloadUrl)
        {
            this.downloadUrl = downloadUrl;
        }
        /// <summary>
        /// 断点续传下载
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<bool> ResumeDownload(string downloadPartPath)
        {
            using (DownloadFile downloadFile = DownloadFile.FromPartPath(downloadPartPath))
            {
                if (downloadFile == null) { return false; }
                return await DoDownloadFile(downloadFile);
            }
        }
        /// <summary>
        /// 根据url下载
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<bool> UrlDownload(string urlFileName, string savefilePath)
        {
            string downloadPartPath = savefilePath + DownloadFile.Ext;
            string downloadPartDir = Path.GetDirectoryName(downloadPartPath);
            if (downloadPartDir != string.Empty)
            {
                if (!Directory.Exists(downloadPartDir))
                {
                    Directory.CreateDirectory(downloadPartDir);
                }
            }
            string url = this.downloadUrl + "?fileName=" + urlFileName;
            using (DownloadFile downloadFile = DownloadFile.FromUrl(downloadPartPath, url))
            {
                return await DoDownloadFile(downloadFile);
            }
        }
        /// <summary>
        /// 执行DownloadFile
        /// </summary>
        /// <param name="downloadFile"></param>
        /// <returns></returns>
        public async Task<bool> DoDownloadFile(DownloadFile downloadFile)
        {
            using (HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) })
            {
                try
                {
                    httpClient.DefaultRequestHeaders.Range = new RangeHeaderValue(downloadFile.RangeBegin, null);
                    HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(downloadFile.URL, HttpCompletionOption.ResponseHeadersRead);
                    long? contentLength = httpResponseMessage.Content.Headers.ContentLength;
                    if (httpResponseMessage.Content.Headers.ContentRange != null) //如果为空，则说明服务器不支持断点续传
                    {
                        contentLength = httpResponseMessage.Content.Headers.ContentRange.Length;//服务器上的文件大小
                    }
                    long? length = (httpResponseMessage.Content.Headers.ContentRange == null ? //如果为空，则说明服务器不支持断点续传
                        httpResponseMessage.Content.Headers.ContentLength :
                        httpResponseMessage.Content.Headers.ContentRange.Length) ?? -1;//服务器上的文件大小
                    string md5 = downloadFile.MD5 ?? (httpResponseMessage.Content.Headers.ContentMD5 == null ? null : Convert.ToBase64String(httpResponseMessage.Content.Headers.ContentMD5));
                    if (downloadFile.DoLocal(length, md5))
                    {
                        using (Stream stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                        {
                            stream.ReadTimeout = 10 * 1000;
                            bool success = await Download(stream, downloadFile) & await downloadFile.Release();
                            this.OnDownloadCompleted(new CompletedArgs(success));
                            return success;
                        }
                    }
                    else
                    {
                        this.OnDownloadError(new ErrorArgs("服务器的上的版本已经变化"));
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    this.OnDownloadError(new ErrorArgs(ex.Message));
                    return false;
                }
            }
        }
        private async Task<bool> Download(Stream downloadStream, DownloadFile downloadFile)
        {
            int bufferSize = 81920; //缓存
            byte[] buffer = new byte[bufferSize];
            long position = downloadFile.RangeBegin;
            int readLength = 0;
            try
            {
                decimal downloadSpeed = 0;//下载速度
                var beginSecond = DateTime.Now.Second;//当前时间秒
                while ((readLength = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    position += readLength;
                    downloadSpeed += readLength;
                    await downloadFile.Write(buffer, 0, readLength);
                    var endSecond = DateTime.Now.Second;
                    if (endSecond != beginSecond)//计算速度
                    {
                        downloadSpeed = downloadSpeed / (endSecond - beginSecond);
                        this.OnDownloadSpeedChanged(new SpeedChangedArgs((float)(downloadSpeed / 1024)));
                        beginSecond = DateTime.Now.Second;
                        downloadSpeed = 0;//清空
                    }
                    this.OnDownloadProgressChanged(new ProgressChangedArgs(readLength, downloadFile.RangeBegin, downloadFile.Length));
                }
                return true;
            }
            catch (Exception ex)
            {
                this.OnDownloadError(new ErrorArgs(ex.Message));
            }
            return false;
        }
    }
}
