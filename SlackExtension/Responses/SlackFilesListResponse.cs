﻿using System;

using Newtonsoft.Json;

namespace SlackExtension.Responses
{
    public class SlackFilesListResponse
    {
        [JsonProperty("ok")]
        public Boolean Ok { get; set; }

        [JsonProperty("files")]
        public Models.SlackFile[] Files { get; set; }

        [JsonProperty("paging")]
        public Models.SlackPaging Paging { get; set; }

        [JsonProperty("error")]
        public String Error { get; set; }
    }
}
