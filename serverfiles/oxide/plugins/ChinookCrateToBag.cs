using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info( "Chinook Crate To Bag", "Waggy", "1.0.1" )]
    [Description( "Chinook crates drop loot in a bag a configurable amount of time after hacking finishes" )]

    class ChinookCrateToBag : CovalencePlugin
    {
        #region Hooks

        void OnCrateHackEnd( HackableLockedCrate crate )
        {
            timer.In( config.timeToWait * 60, () => 
            {
                if ( crate != null )
                {
                    crate.inventory.Drop( "assets/prefabs/misc/item drop/item_drop.prefab", crate.GetDropPosition(), crate.transform.rotation );
                    crate.Kill();
                }
            } );
        }

        #endregion

        #region Config and Lang

        private ConfigData config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject( new ConfigData(), true );
        }

        private void Init()
        {
            config = Config.ReadObject<ConfigData>();
        }

        private new void SaveConfig()
        {
            Config.WriteObject( config, true );
        }

        public class ConfigData
        {
            [JsonProperty( "Time to Wait Before Dropping Bag (in minutes)" )]
            public float timeToWait = 15f;
        }

        #endregion
    }
}