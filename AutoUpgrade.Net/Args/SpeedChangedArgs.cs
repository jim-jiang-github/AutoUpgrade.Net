﻿namespace AutoUpgrade.Net.Args
{
    public class SpeedChangedArgs
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="speed">下载速度（MB/S）</param>
        public SpeedChangedArgs(float speed)
        {
            Speed = speed;
        }
        /// <summary>
        /// 下载速度（MB/S）
        /// </summary>
        public float Speed { get; set; }
    }
}
