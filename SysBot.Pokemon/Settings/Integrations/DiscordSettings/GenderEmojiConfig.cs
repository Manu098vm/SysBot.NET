using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using PKHeX.Core;


namespace SysBot.Pokemon;

public partial class DiscordSettings
{
    // same logic as ExpandableObjectConverter in EmbedSettingConfig
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class GenderEmojiConfig
    {
        // This content is fixed and intentionally hidden to prevent modification
        [Browsable(false)]
        public string Gender { get; set; }
        
        [Description("EmojiCode")]
        public string EmojiCode { get; set; } = string.Empty;
        
        public override string ToString() => Gender;

        public GenderEmojiConfig(string Gender)
        {
            this.Gender = Gender;
        }
    }
}
