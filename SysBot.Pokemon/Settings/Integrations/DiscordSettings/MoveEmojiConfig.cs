using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using PKHeX.Core;


namespace SysBot.Pokemon;

// DW
public partial class DiscordSettings
{
    // same logic as ExpandableObjectConverter in EmbedSettingConfig
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class MoveEmojiConfig
    {
        // [Browsable(false)],setting in invisible state
        // This item is used as an index and hidden to avoid cluttering the setting panel
        [Browsable(false)]
        public int MoveTypeValue { get; }

        // This content is fixed and intentionally hidden to prevent modification
        [Browsable(false)]
        public string MoveType { get; set; }
        
        [Description("EmojiCode")]
        public string EmojiCode { get; set; } = string.Empty;
        
        // specify the text content given for every MoveType in the setting panel
        // remove the correct code and replace with example code
        public override string ToString() => MoveType;
        // public override string ToString() => "Look, here;s the result!";
        
        public MoveEmojiConfig(string MoveType, int MoveTypeValue)
        {
            this.MoveType = MoveType;
            this.MoveTypeValue = MoveTypeValue;
        }
    }
}
