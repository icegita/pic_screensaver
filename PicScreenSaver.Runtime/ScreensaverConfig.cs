using System.Runtime.Serialization;

namespace PicScreenSaver.Runtime
{
    [DataContract]
    public class ScreensaverConfig
    {
        [DataMember(Name = "version")]
        public string Version { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "author")]
        public string Author { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "displayDuration")]
        public double DisplayDuration { get; set; }

        [DataMember(Name = "transitionDuration")]
        public double TransitionDuration { get; set; }

        [DataMember(Name = "shuffleImages")]
        public bool ShuffleImages { get; set; }

        [DataMember(Name = "selectedEffects")]
        public string[] SelectedEffects { get; set; }

        [DataMember(Name = "imageCount")]
        public int ImageCount { get; set; }

        [DataMember(Name = "createdBy")]
        public string CreatedBy { get; set; }

        [DataMember(Name = "createdAt")]
        public string CreatedAt { get; set; }
    }
}
