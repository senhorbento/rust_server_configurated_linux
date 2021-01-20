using Newtonsoft.Json;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Admin No Loot", "Dana", "0.1.3")]
    [Description("Protects admins' corpses and bodies from being looted.")]
    public class AdminNoLoot : RustPlugin
    {
        private PluginConfig _pluginConfig;
        public const string PermissionBypass = "adminnoloot.bypass";

        #region Hooks

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }
        protected override void LoadConfig()
        {
            var configPath = $"{Manager.ConfigPath}/{Name}.json";
            var newConfig = new DynamicConfigFile(configPath);
            if (!newConfig.Exists())
            {
                LoadDefaultConfig();
                newConfig.Save();
            }
            try
            {
                newConfig.Load();
            }
            catch (Exception ex)
            {
                RaiseError("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
                return;
            }

            newConfig.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = newConfig.ReadObject<PluginConfig>();
            if (_pluginConfig.Config == null)
            {
                _pluginConfig.Config = new AdminNoLootConfig
                {
                    ShowWarning = true
                };
            }
            newConfig.WriteObject(_pluginConfig);
            PrintWarning("Config Loaded");
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionBypass, this);
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { MessageManager.NoCorpseLootPermission, "You can't loot Admin corpse!" },
                { MessageManager.NoBagLootPermission, "You can't loot Admin bag!" },
                { MessageManager.NoBodyLootPermission, "You can't loot Admin body!" },
            }, this);
        }
        object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if (target.IsAdmin && target.userID != looter.userID && !permission.UserHasPermission(looter.UserIDString, PermissionBypass))
            {
                if (_pluginConfig.Config.ShowWarning)
                    looter.ChatMessage(lang.GetMessage(MessageManager.NoBodyLootPermission, this, looter.UserIDString));

                return false;
            }
            return null;
        }
        object CanLootEntity(BasePlayer player, DroppedItemContainer container)
        {
            var target = BasePlayer.FindAwakeOrSleeping(container.playerSteamID.ToString());
            if (target == null)
                return null;

            if (target.IsAdmin && target.userID != player.userID && !permission.UserHasPermission(player.UserIDString, PermissionBypass))
            {
                if (_pluginConfig.Config.ShowWarning)
                    player.ChatMessage(lang.GetMessage(MessageManager.NoBagLootPermission, this, player.UserIDString));

                return false;
            }
            return null;
        }
        object CanLootEntity(BasePlayer player, LootableCorpse corpse)
        {
            var target = BasePlayer.FindAwakeOrSleeping(corpse.playerSteamID.ToString());
            if (target == null)
                return null;

            if (target.IsAdmin && target.userID != player.userID && !permission.UserHasPermission(player.UserIDString, PermissionBypass))
            {
                if (_pluginConfig.Config.ShowWarning)
                    player.ChatMessage(lang.GetMessage(MessageManager.NoCorpseLootPermission, this, player.UserIDString));

                return false;
            }
            return null;
        }
        #endregion Hooks

        #region Classes

        public class MessageManager
        {
            public const string NoCorpseLootPermission = "NoCorpseLootPermission";
            public const string NoBagLootPermission = "NoBagLootPermission";
            public const string NoBodyLootPermission = "NoBodyLootPermission";
        }

        private class PluginConfig
        {
            public AdminNoLootConfig Config { get; set; }
        }
        private class AdminNoLootConfig
        {
            [JsonProperty(PropertyName = "Warning - Enabled")]
            public bool ShowWarning { get; set; }
        }
        #endregion Classes
    }
}