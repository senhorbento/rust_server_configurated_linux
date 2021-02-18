using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Oxide.Plugins
{
    [Info("Tea Modifiers", "MJSU", "1.0.0")]
    [Description("Allows the modification of the buff from the team recipies")]
    internal class TeaModifiers : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config
        
        private readonly Hash<int, List<ModifierDefintion>> _defaultModifiers = new Hash<int, List<ModifierDefintion>>();
        #endregion

        #region Setup & Loading
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.Modifiers = config.Modifiers ?? new Hash<string, List<ModifierData>>();
            return config;
        }
        
        private void OnServerInitialized()
        {
            bool changed = false;
            foreach (ItemDefinition def in ItemManager.itemList)
            {
                ItemModConsumable consume = def.GetComponent<ItemModConsumable>();
                if (consume == null || consume.modifiers == null || consume.modifiers.Count == 0)
                {
                    continue;
                }

                //Backup modifiers
                _defaultModifiers[def.itemid] = consume.modifiers.Select(m => new ModifierDefintion
                {
                    duration = m.duration,
                    source = m.source,
                    type = m.type,
                    value = m.value
                }).ToList();

                //Load saved modifiers
                List<ModifierData> modifiers = _pluginConfig.Modifiers[def.shortname];
                
                //Currently no modifiers saved. Save them now.
                if (modifiers == null)
                {
                    modifiers = consume.modifiers.Select(m => new ModifierData
                    {
                        Duration = m.duration,
                        Source = m.source,
                        Type = m.type,
                        Amount = m.value
                    }).ToList();
                    _pluginConfig.Modifiers[def.shortname] = modifiers;
                    changed = true;
                }
                //Apply currently configured modifiers
                else
                {
                    consume.modifiers = modifiers.Select(m => new ModifierDefintion
                    {
                        duration = m.Duration,
                        source = m.Source,
                        type = m.Type,
                        value = m.Amount
                    }).ToList();
                }
            }

            //If changes occured update config
            if (changed)
            {
                Config.WriteObject(_pluginConfig);
            }
        }

        private void Unload()
        {
            foreach (ItemDefinition def in ItemManager.itemList)
            {
                ItemModConsumable consume = def.GetComponent<ItemModConsumable>();
                if (consume == null || consume.modifiers == null || consume.modifiers.Count == 0)
                {
                    continue;
                }

                consume.modifiers = _defaultModifiers[def.itemid];
            }
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Item Modifiers")]
            public Hash<string, List<ModifierData>> Modifiers { get; set; }
        }

        private class ModifierData
        {
            [JsonProperty(PropertyName = "Buff duration (Seconds)")]
            public float Duration { get; set; }
            
            [JsonProperty(PropertyName = "Buff amount")]
            public float Amount { get; set; }
            
            [JsonProperty(PropertyName = "Buff source")]
            [JsonConverter(typeof(StringEnumConverter))]
            public Modifier.ModifierSource Source { get; set; }
            
            [JsonProperty(PropertyName = "Buff type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public Modifier.ModifierType Type { get; set; }
        }
        #endregion
    }
}
