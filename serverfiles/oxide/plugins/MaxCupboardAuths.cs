using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MaxCupboardAuths", "Krungh Crow", "1.1.1")]
    [Description("Limit how many tool cupboards each player can authorise to")]

    #region Changelogs and ToDo
    /**********************************************************************
    * 
    * Thx to redBDGR the original creator of this plugin
    * 
    *	1.1.1	:	Starting to maintain (krungh Crow)
    *			:	Updated the documentation
    *			:   Datafile is cleaned after a mapwipe
    *			:   Extra checks after de/auth and destroying a tc
    *			:   Added and changed permissions (admin/command)
    *			:   Added chat commands (/mca clearall)(/mca save)
    *
    *	ToDo	:	Convert to Covalence
    *				Optimise CFG Handling
    * 
    **********************************************************************/
    #endregion

    class MaxCupboardAuths : RustPlugin
    {
        private DynamicConfigFile MaxCupboardAuthsData;
        StoredData storedData;

        bool Changed = false;
        Dictionary<string, int> playerInfo = new Dictionary<string, int>();

        class StoredData
        {
            public Dictionary<string, int> MaxCupboardInfo = new Dictionary<string, int>();
        }

        public int MaxAllowedPerPlayer = 5;
        public int MaxAllowedPerCupboard = 5;
        public int MaxAllowedPerPlayerVip = 10;
        public int MaxAllowedPerCupboardVip = 10;
        public const string Admin_Perm = "maxcupboardauths.admin";
        public const string Cmd_Perm = "maxcupboardauths.command";
        public bool monitorByPlayer = true;
        public bool monitorByCupboard = true;

        void OnServerSave()
        {
            SaveData();
        }

        void SaveData()
        {
            storedData.MaxCupboardInfo = playerInfo;
            MaxCupboardAuthsData.WriteObject(storedData);
            //Puts("Savedata check");
        }

        void LoadData()
        {
            try
            {
                storedData = MaxCupboardAuthsData.ReadObject<StoredData>();
                playerInfo = storedData.MaxCupboardInfo;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void Unload() => SaveData();

        void OnNewSave(string filename)
        {
            Puts($"Mapwipe detected auths would be cleared now !!");
            storedData.MaxCupboardInfo.Clear();
            SaveData();
        }

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(Admin_Perm, this);
            permission.RegisterPermission(Cmd_Perm, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Auth Denied PLAYER"] = "You cannot authorise to this cupboard! you have already authorized to the maximum amount of cupboards!",
                ["Auth Denied CUPBOARD"] = "You cannot authorise to this cupboard! there are already the maximum amount of people authed to it!",
                ["NoPermission"] = "You cannot use this command without permissions!",
                ["InvalidInput"] = "Please enter a valid command!",
                ["CommandClearAll"] = "Cleared all Auth Data !",
                ["CommandSave"] = "Saved all Auths !",
            }, this);

            MaxCupboardAuthsData = Interface.Oxide.DataFileSystem.GetFile("MaxCupboardAuths");
            LoadData();
        }

        void LoadVariables()
        {
            MaxAllowedPerPlayer = Convert.ToInt32(GetConfig("Settings", "Max Auths Allowed Per Player", 5));
            MaxAllowedPerCupboard = Convert.ToInt32(GetConfig("Settings", "Max Auths Allowed Per Cupboard", 5));
            monitorByCupboard = Convert.ToBoolean(GetConfig("Settings", "Monitor Auths Per Cupboard", true));
            monitorByPlayer = Convert.ToBoolean(GetConfig("Settings", "Monitor Auths Per Player", true));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, Admin_Perm)) return null;
            if (monitorByPlayer)
            {
                if (playerInfo.ContainsKey(player.UserIDString))
                {
                    if (playerInfo[player.UserIDString] >= MaxAllowedPerPlayer)
                    {
                        player.ChatMessage(msg("Auth Denied PLAYER", player.UserIDString));
                        return true;
                    }
                    else
                    {
                        playerInfo[player.UserIDString] += 1;
                        SaveData();
                        return null;
                    }
                }
                else
                {
                    playerInfo.Add(player.UserIDString, 1);
                    SaveData();
                    return null;
                }
            }

            if (monitorByCupboard)
                if (privilege.authorizedPlayers.Count >= MaxAllowedPerCupboard)
                {
                    player.ChatMessage(msg("Auth Denied CUPBOARD", player.UserIDString));
                    return true;
                }
                else return null;
            return null;
        }

        object OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, Admin_Perm)) return null;
            if (!monitorByPlayer) return null;
            if (playerInfo.ContainsKey(player.UserIDString))
            {
                if (playerInfo[player.UserIDString] == 0)
                    return null;
                playerInfo[player.UserIDString] -= 1;
                SaveData();
                return null;
            }
            else return null;
        }
        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var cupboard = entity.GetComponent<BuildingPrivlidge>();
            if (cupboard)
                foreach (var player in cupboard.authorizedPlayers)
                {
                    var ppl = BasePlayer.Find(player.userid.ToString());
                    if (ppl)
                        OnCupboardDeauthorize(cupboard, ppl);
                }
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        #region Commands

        [ChatCommand("mca")]
        private void cmdMca(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Cmd_Perm))
            {
                player.ChatMessage(msg("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage(msg("InvalidInput", player.UserIDString));
            }
            else
            {
                if (args[0].ToLower() == "clearall")
                {
                    if (permission.UserHasPermission(player.UserIDString, Cmd_Perm))
                    {
                        storedData.MaxCupboardInfo.Clear();
                        SaveData();
                        player.ChatMessage(msg("CommandClearAll", player.UserIDString));
                    }
                }
                if (args[0].ToLower() == "save")
                {
                    if (permission.UserHasPermission(player.UserIDString, Cmd_Perm))
                    {
                        SaveData();
                        player.ChatMessage(msg("CommandSave", player.UserIDString));
                    }
                }
                else
                {
                    player.ChatMessage(msg("InvalidInput", player.UserIDString));
                }
            }
        }
        #endregion

    }
}