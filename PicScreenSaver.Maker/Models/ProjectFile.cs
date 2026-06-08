using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PicScreenSaver.Maker.Models
{
    public class ProjectFile
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("displayDuration")]
        public double DisplayDuration { get; set; }

        [JsonProperty("transitionDuration")]
        public double TransitionDuration { get; set; }

        [JsonProperty("quality")]
        public int Quality { get; set; }

        [JsonProperty("maxWidth")]
        public int MaxWidth { get; set; }

        [JsonProperty("shuffleImages")]
        public bool ShuffleImages { get; set; }

        [JsonProperty("selectedEffects")]
        public string[] SelectedEffects { get; set; }

        [JsonProperty("outputPath")]
        public string OutputPath { get; set; }

        [JsonProperty("saverName")]
        public string SaverName { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("autoInstall")]
        public bool AutoInstall { get; set; }

        [JsonProperty("imagePaths")]
        public List<string> ImagePaths { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("modifiedAt")]
        public string ModifiedAt { get; set; }
    }
}
