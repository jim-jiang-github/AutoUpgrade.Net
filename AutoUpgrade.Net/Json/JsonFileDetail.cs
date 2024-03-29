﻿namespace AutoUpgrade.Net.Json
{
    public class JsonFileDetail
    {
        /// <summary>
        /// 文件名
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 所在版本
        /// </summary>
        public string Version { get; set; }
        /// <summary>
        /// 文件MD5
        /// </summary>
        public string MD5 { get; set; }
        /// <summary>
        /// 文件MD5
        /// </summary>
        public long Length { get; set; }
        public JsonFileDetail() { }
        public override string ToString()
        {
            return this.Name + "|" + this.MD5;
        }
    }
}
