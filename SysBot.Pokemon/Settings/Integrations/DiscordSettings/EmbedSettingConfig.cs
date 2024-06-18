using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using PKHeX.Core;


namespace SysBot.Pokemon;

public partial class DiscordSettings
{
    // must be set to ExpandableObjectConverter class，otherwise "setting" panel will not recognize reason being:
    // The original EmbedSettingConfig, is a class object type for transferring between codes and
    // "setting" by itself cannot directly support class object types, so it must be converted to a type in order to be recognized.
    // Note: if you attempt to complie the line [TypeConverter(typeof(ExpandableObjectConverter))], EmbedSettingConfig's option will become unusable
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class EmbedSettingConfig
    {
        // Build MoveEmoji
        [Description("Use EmojiCode")]
        public bool UseMoveEmoji { get; set; } = false;
        
        [Description("Set EmojiCode")]
        public List<MoveEmojiConfig> MoveEmojiConfigs { get; set; } = new();

        public EmbedSettingConfig()
        {
            MoveEmojiConfigs = [
                new MoveEmojiConfig("Normal",0),
                new MoveEmojiConfig("Fighting",1),
                new MoveEmojiConfig("Flying",2),
                new MoveEmojiConfig("Poison",3),
                new MoveEmojiConfig("Ground",4),
                new MoveEmojiConfig("Rock",5),
                new MoveEmojiConfig("Bug",6),
                new MoveEmojiConfig("Ghost",7),
                new MoveEmojiConfig("Steel",8),
                new MoveEmojiConfig("Fire",9),
                new MoveEmojiConfig("Water",10),
                new MoveEmojiConfig("Grass",11),
                new MoveEmojiConfig("Electric",12),
                new MoveEmojiConfig("Psychic",13),
                new MoveEmojiConfig("Ice",14),
                new MoveEmojiConfig("Dragon",15),
                new MoveEmojiConfig("Dark",16),
                new MoveEmojiConfig("Fairy",17),
                new MoveEmojiConfig("Stellar",99),
            ];
        }

        // Build TeraTypeEmoji
        [Description("Use TeraTypeEmoji")]
        public bool TeraTypeEmoji { get; set; } = false;        

        // Build GenderEmoji
        [Description("Use GenderEmoji")]
        public bool GenderEmoji { get; set; } = false;        
        [Description("Set GenderEmoji")]
        public List<GenderEmojiConfig> GenderEmojiConfig { get; set; } = [
                new GenderEmojiConfig("Male"),
                new GenderEmojiConfig("Female"),
                new GenderEmojiConfig("NoGender"),
            ];
        
        // public IEnumerator<MoveEmojiConfig> GetEnumerator() => MoveEmojiConfigs.GetEnumerator();
        // public IEnumerable<string> Summarize() => MoveEmojiConfigs.Select(z => z.ToString());
        public override string ToString() => "Discord Embed Integration Settings";        
    }
  
    

}
