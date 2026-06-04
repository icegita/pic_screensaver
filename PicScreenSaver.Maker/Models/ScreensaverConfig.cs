using Newtonsoft.Json;

namespace PicScreenSaver.Maker.Models
{
    public class ScreensaverConfig
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("displayDuration")]
        public double DisplayDuration { get; set; }

        [JsonProperty("transitionDuration")]
        public double TransitionDuration { get; set; }

        [JsonProperty("shuffleImages")]
        public bool ShuffleImages { get; set; }

        [JsonProperty("selectedEffects")]
        public string[] SelectedEffects { get; set; }

        [JsonProperty("imageCount")]
        public int ImageCount { get; set; }

        [JsonProperty("createdBy")]
        public string CreatedBy { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }
    }
}
