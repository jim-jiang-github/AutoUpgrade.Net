﻿using System;
using System.Collections.Generic;
using System.Text;

namespace AutoUpgrade.Net.Json
{
    public class JsonRespondResult
    {
        public bool Result { get; set; } = true;
        public string Message { get; set; }
        public string[] Details { get; set; }
    }
}
