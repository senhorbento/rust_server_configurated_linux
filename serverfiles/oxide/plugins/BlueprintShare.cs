using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Blueprint Share", "c_creep", "1.2.4")]
    [Description("Allows players to share researched blueprints with their friends, clan or team")]

    class BlueprintShare : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin Clans, ClansReborn, Friends;

        private StoredData storedData;

        private bool clansEnabled = true, friendsEnabled = true, teamsEnabled = true, recycleEnabled = false;

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Prefix"] = "<color=#D85540>[Blueprint Share] </color>",
                ["ArgumentsError"] = "Error, incorrect arguments. Try /bs help.",
                ["Help"] = "<color=#D85540>Blueprint Share Help:</color>\n\n<color=#D85540>/bs toggle</color> - Toggles the sharing of blueprints.\n<color=#D85540>/bs share <player></color> - Shares your blueprints with other player.",
                ["ToggleOn"] = "You have enabled sharing blueprints.",
                ["ToggleOff"] = "You have disabled sharing blueprints.",
                ["NoPermission"] = "You don't have permission to use this command!",
                ["CannotShare"] = "You cannot share blueprints with this player because they aren't a friend or in the same clan or team!",
                ["NoTarget"] = "You didn't specifiy a player to share with!",
                ["TargetEqualsPlayer"] = "You cannot share blueprints with your self!",
                ["PlayerNotFound"] = "Couldn't find a player with that name!",
                ["MultiplePlayersFound"] = "Found multiple players with a similar name: {0}",
                ["ShareSuccess"] = "You shared {0} blueprints with {1}.",
                ["ShareFailure"] = "You don't have any new blueprints to share with {0}",
                ["ShareReceieve"] = "{0} has shared {1} blueprints with you.",
                ["Recycle"] = "You have kept the blueprint because no one learnt the blueprint.",
                ["ShareError"] = "An error occured while attempting to share items with another player"
            }, this);
        }

        private string GetLangValue(string key, string id = null, params object[] args)
        {
            var msg = lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadData();
            LoadDefaultConfig();

            permission.RegisterPermission("blueprintshare.toggle", this);
            permission.RegisterPermission("blueprintshare.share", this);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            var playerUID = player.UserIDString;

            if (!PlayerDataExists(playerUID))
            {
                CreateNewPlayerData(playerUID);
            }
        }

        private void OnItemAction(Item item, string action, BasePlayer player)
        {
            if (player != null)
            {
                if (action == "study")
                {
                    var itemShortName = item.blueprintTargetDef.shortname;

                    if (CanShareBlueprint(itemShortName, player))
                    {
                        item.Remove();
                    }
                }
            }
        }

        private void OnTechTreeNodeUnlocked(Workbench workbench, TechTreeData.NodeInstance node, BasePlayer player)
        {
            if (workbench != null)
            {
                if (node != null)
                {
                    if (player != null)
                    {
                        var itemShortName = node.itemDef.shortname;
                        
                        CanShareBlueprint(itemShortName, player);
                    }
                }
            }
        }

        #endregion

        #region General Methods

        private bool CanShareBlueprint(string itemShortName, BasePlayer player)
        {
            if (player == null) return false;
            if (string.IsNullOrEmpty(itemShortName)) return false;

            var playerUID = player.UserIDString;

            if (SharingEnabled(playerUID))
            {
                if (InTeam(player.userID) || InClan(player.userID) || HasFriends(player.userID))
                {
                    if (SomeoneWillLearnBlueprint(player, itemShortName))
                    {
                        TryInsertBlueprint(player, itemShortName);
                        ShareWithPlayers(player, itemShortName);
                        HandleAdditionalBlueprints(player, itemShortName);

                        return true;
                    }
                    else
                    {
                        if (recycleEnabled)
                        {
                            player.ChatMessage(GetLangValue("Recycle", playerUID));

                            return true;
                        }

                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                TryInsertBlueprint(player, itemShortName);

                return true;
            }
        }

        private void HandleAdditionalBlueprints(BasePlayer player, string itemShortName)
        {
            var additionalBlueprints = GetItemDefinition(itemShortName).Blueprint.additionalUnlocks;

            if (additionalBlueprints.Count > 0)
            {
                foreach (var blueprint in additionalBlueprints)
                {
                    var additionalItemShortName = blueprint.shortname;

                    if (!string.IsNullOrEmpty(additionalItemShortName))
                    {
                        if (SomeoneWillLearnBlueprint(player, additionalItemShortName))
                        {
                            ShareWithPlayers(player, additionalItemShortName);
                        }
                    }
                }
            }
        }

        private void ShareWithPlayers(BasePlayer sharer, string itemShortName)
        {
            if (sharer == null || string.IsNullOrEmpty(itemShortName)) return;

            var recipients = SelectSharePlayers(sharer);

            foreach (var recipient in recipients)
            {
                if (recipient != null)
                {
                    if (UnlockBlueprint(recipient, itemShortName))
                    {
                        TryInsertBlueprint(recipient, itemShortName);
                    }
                }
            }
        }

        private void ShareWithPlayer(BasePlayer sharer, BasePlayer recipient)
        {
            var sharerUID = sharer.UserIDString;
            var recipientUID = recipient.UserIDString;

            if (SameTeam(sharer, recipient) || SameClan(sharerUID, recipientUID) || AreFriends(sharerUID, recipientUID))
            {
                var itemShortNames = LoadBlueprints(sharerUID);

                if (itemShortNames != null)
                {
                    if (itemShortNames.Count > 0)
                    {
                        var learnedBlueprints = 0;

                        foreach (var itemShortName in itemShortNames)
                        {
                            if (recipient == null || string.IsNullOrEmpty(itemShortName)) return;

                            if (UnlockBlueprint(recipient, itemShortName))
                            {
                                TryInsertBlueprint(recipient, itemShortName);

                                learnedBlueprints++;
                            }
                        }

                        if (learnedBlueprints > 0)
                        {
                            sharer.ChatMessage(GetLangValue("Prefix", sharerUID) + GetLangValue("ShareSuccess", sharerUID, learnedBlueprints, recipient.displayName));

                            recipient.ChatMessage(GetLangValue("Prefix", recipientUID) + GetLangValue("ShareReceieve", recipientUID, sharer.displayName, learnedBlueprints));
                        }
                        else
                        {
                            sharer.ChatMessage(GetLangValue("Prefix", sharerUID) + GetLangValue("ShareFailure", sharerUID, recipient.displayName));
                        }
                    }
                    else
                    {
                        sharer.ChatMessage(GetLangValue("Prefix", sharerUID) + GetLangValue("ShareFailure", sharerUID, recipient.displayName));
                    }
                }
                else
                {
                    sharer.ChatMessage(GetLangValue("ShareError", sharerUID));
                }
            }
        }

        private bool UnlockBlueprint(BasePlayer player, string itemShortName)
        {
            if (player == null) return false;
            if (string.IsNullOrEmpty(itemShortName)) return false;

            var blueprintComponent = player.blueprints;

            if (blueprintComponent == null) return false;

            var itemDefinition = GetItemDefinition(itemShortName);

            if (itemDefinition == null) return false;

            if (blueprintComponent.HasUnlocked(itemDefinition)) return false;

            var soundEffect = new Effect("assets/prefabs/deployable/research table/effects/research-success.prefab", player.transform.position, Vector3.zero);

            if (soundEffect == null) return false;

            EffectNetwork.Send(soundEffect, player.net.connection);

            blueprintComponent.Unlock(itemDefinition);

            return true;
        }

        private bool SomeoneWillLearnBlueprint(BasePlayer sharer, string itemShortName)
        {
            if (sharer == null || string.IsNullOrEmpty(itemShortName)) return false;

            var players = SelectSharePlayers(sharer);

            if (players.Count > 0)
            {
                var blueprintItem = GetItemDefinition(itemShortName);

                if (blueprintItem != null)
                {
                    var counter = 0;

                    foreach (var player in players)
                    {
                        if (player != null)
                        {
                            if (!player.blueprints.HasUnlocked(blueprintItem))
                            {
                                counter++;
                            }
                        }
                    }

                    return counter > 0;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private List<BasePlayer> SelectSharePlayers(BasePlayer player)
        {
            var playersToShareWith = new List<BasePlayer>();

            var playerUID = player.userID;

            if (clansEnabled && (Clans != null || ClansReborn != null) && InClan(playerUID))
            {
                playersToShareWith.AddRange(GetClanMembers(playerUID));
            }

            if (friendsEnabled && Friends != null && HasFriends(playerUID))
            {
                playersToShareWith.AddRange(GetFriends(playerUID));
            }

            if (teamsEnabled && InTeam(playerUID))
            {
                playersToShareWith.AddRange(GetTeamMembers(playerUID));
            }

            return playersToShareWith;
        }

        private List<string> LoadBlueprints(string playerUID)
        {
            if (PlayerDataExists(playerUID))
            {
                return storedData.players[playerUID].learntBlueprints;
            }
            else
            {
                CreateNewPlayerData(playerUID);

                if (storedData.players[playerUID].learntBlueprints == null)
                {
                    return storedData.players[playerUID].learntBlueprints = new List<string>();
                }
                else
                {
                    return storedData.players[playerUID].learntBlueprints;
                }
            }
        }

        private void TryInsertBlueprint(BasePlayer player, string itemShortName)
        {
            var playerUID = player.UserIDString;

            if (PlayerDataExists(playerUID))
            {
                InsertBlueprint(playerUID, itemShortName);
            }
            else
            {
                CreateNewPlayerData(playerUID);

                InsertBlueprint(playerUID, itemShortName);
            }
        }

        private void InsertBlueprint(string playerUID, string itemShortName)
        {
            if (string.IsNullOrEmpty(playerUID) || string.IsNullOrEmpty(itemShortName)) return;

            if (!storedData.players[playerUID].learntBlueprints.Contains(itemShortName))
            {
                storedData.players[playerUID].learntBlueprints.Add(itemShortName);

                SaveData();
            }
        }

        #endregion

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config["ClansEnabled"] = clansEnabled = GetConfigValue("ClansEnabled", true);
            Config["FriendsEnabled"] = friendsEnabled = GetConfigValue("FriendsEnabled", true);
            Config["TeamsEnabled"] = teamsEnabled = GetConfigValue("TeamsEnabled", true);
            Config["RecycleBlueprints"] = recycleEnabled = GetConfigValue("RecycleBlueprints", false);

            SaveConfig();
        }

        private T GetConfigValue<T>(string name, T defaultValue)
        {
            return Config[name] == null ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion

        #region Friends Methods

        private bool HasFriends(ulong playerUID)
        {
            if (Friends == null) return false;

            var friendsList = Friends.Call<ulong[]>("GetFriends", playerUID);

            return friendsList != null && friendsList.Length != 0;
        }

        private List<BasePlayer> GetFriends(ulong playerUID)
        {
            var friendsList = new List<BasePlayer>();

            var friends = Friends.Call<ulong[]>("GetFriends", playerUID);

            foreach (var friendUID in friends)
            {
                var friend = RustCore.FindPlayerById(friendUID);

                friendsList.Add(friend);
            }

            return friendsList;
        }

        private bool AreFriends(string sharerUID, string playerUID) => Friends == null ? false : Friends.Call<bool>("AreFriends", sharerUID, playerUID);

        #endregion

        #region Clan Methods

        private bool InClan(ulong playerUID)
        {
            if (ClansReborn == null && Clans == null) return false;

            var clanName = Clans?.Call<string>("GetClanOf", playerUID);

            return clanName != null;
        }

        private List<BasePlayer> GetClanMembers(ulong playerUID)
        {
            var membersList = new List<BasePlayer>();

            var clanName = Clans?.Call<string>("GetClanOf", playerUID);

            if (!string.IsNullOrEmpty(clanName))
            {
                var clan = Clans?.Call<JObject>("GetClan", clanName);

                if (clan != null && clan is JObject)
                {
                    var members = clan.GetValue("members");

                    if (members != null)
                    {
                        foreach (var member in members)
                        {
                            ulong clanMemberUID;

                            if (!ulong.TryParse(member.ToString(), out clanMemberUID)) continue;

                            BasePlayer clanMember = RustCore.FindPlayerById(clanMemberUID);

                            membersList.Add(clanMember);
                        }
                    }
                }
            }
            return membersList;
        }

        private bool SameClan(string sharerUID, string playerUID) => ClansReborn == null && Clans == null ? false : (bool)Clans?.Call<bool>("IsClanMember", sharerUID, playerUID);

        #endregion

        #region Team Methods

        private bool InTeam(ulong playerUID)
        {
            var player = RustCore.FindPlayerById(playerUID);

            return player.currentTeam != 0;
        }

        private List<BasePlayer> GetTeamMembers(ulong playerUID)
        {
            var membersList = new List<BasePlayer>();

            var player = RustCore.FindPlayerById(playerUID);

            var teamMembers = player.Team.members;

            foreach (var teamMemberUID in teamMembers)
            {
                var teamMember = RustCore.FindPlayerById(teamMemberUID);

                if (teamMember != null)
                {
                    membersList.Add(teamMember);
                }
            }

            return membersList;
        }

        private bool SameTeam(BasePlayer sharer, BasePlayer player) => sharer.currentTeam == player.currentTeam;

        #endregion

        #region Utility Methods

        private BasePlayer FindPlayer(string playerName, BasePlayer player, string playerUID)
        {
            var targets = FindPlayers(playerName);

            if (targets.Count <= 0)
            {
                player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("PlayerNotFound", playerUID));

                return null;
            }

            if (targets.Count > 1)
            {
                player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("MultiplePlayersFound", playerUID));

                return null;
            }

            return targets.First();
        }

        private List<BasePlayer> FindPlayers(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return null;

            return BasePlayer.allPlayerList.Where(p => p && p.UserIDString == playerName || p.displayName.Contains(playerName, CompareOptions.OrdinalIgnoreCase)).ToList();
        }

        private ItemDefinition GetItemDefinition(string itemShortName)
        {
            if (string.IsNullOrEmpty(itemShortName)) return null;

            var itemDefinition = ItemManager.FindItemDefinition(itemShortName.ToLower());

            return itemDefinition;
        }

        #endregion

        #region Data

        private class StoredData
        {
            public Dictionary<string, PlayerData> players = new Dictionary<string, PlayerData>();
        }

        private class PlayerData
        {
            public bool sharingEnabled;

            public List<string> learntBlueprints;
        }

        private void CreateData()
        {
            storedData = new StoredData();

            SaveData();
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            else
            {
                CreateData();
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private bool PlayerDataExists(string playerUID) => storedData.players.ContainsKey(playerUID);

        private void CreateNewPlayerData(string playerUID)
        {
            storedData.players.Add(playerUID, new PlayerData
            {
                sharingEnabled = true,
                learntBlueprints = new List<string>()
            });

            SaveData();
        }

        #endregion

        #region Chat Commands

        [ChatCommand("bs")]
        private void ToggleCommand(BasePlayer player, string command, string[] args)
        {
            var playerUID = player.UserIDString;

            if (args.Length < 1)
            {
                player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("Help", playerUID));

                return;
            }

            switch (args[0].ToLower())
            {
                case "help":
                {
                    player.ChatMessage(GetLangValue("Help", playerUID));

                    break;
                }
                case "toggle":
                {
                    if (permission.UserHasPermission(playerUID, "blueprintshare.toggle"))
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue(SharingEnabled(playerUID) ? "ToggleOff" : "ToggleOn", playerUID));

                        if (storedData.players.ContainsKey(playerUID))
                        {
                            storedData.players[playerUID].sharingEnabled = !storedData.players[playerUID].sharingEnabled;

                            SaveData();
                        }
                    }
                    else
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("NoPermission", playerUID));
                    }

                    break;
                }
                case "share":
                {
                    if (permission.UserHasPermission(playerUID, "blueprintshare.share"))
                    {
                        if (args.Length == 2)
                        {
                            var target = FindPlayer(args[1], player, playerUID);

                            if (target == null) return;

                            if (target == player)
                            {
                                player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("TargetEqualsPlayer", playerUID));

                                return;
                            }

                            ShareWithPlayer(player, target);
                        }
                        else
                        {
                            player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("NoTarget", playerUID));

                            return;
                        }
                    }
                    else
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("NoPermission", playerUID));
                    }

                    break;
                }
                default:
                {
                    player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("ArgumentsError", playerUID));

                    break;
                }
            }
        }

        #endregion

        #region API

        private bool SharingEnabled(string playerUID) => storedData.players.ContainsKey(playerUID) ? storedData.players[playerUID].sharingEnabled : true;

        #endregion
    }
}