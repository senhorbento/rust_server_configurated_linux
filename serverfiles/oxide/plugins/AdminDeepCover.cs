using CompanionServer;
using ConVar;
using Facepunch;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Admin Deep Cover", "Dana", "2.2.7")]
    [Description("Hides the original identity of admins by masking their steam profiles")]

    public class AdminDeepCover : RustPlugin
    {
        #region Plugin References
        [PluginReference] Plugin BetterChat;
        #endregion

        #region Fields and Properties
        private DynamicConfigFile _pluginData;
        private AdminDeepCoverData _adminDeepCoverData;
        Configuration config;
        private static readonly string Perm = "admindeepcover.use";


        #endregion

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission(Perm, this);

            _pluginData = Interface.Oxide.DataFileSystem.GetFile("AdminDeepCover");
            _adminDeepCoverData = new AdminDeepCoverData();
            LoadData();
            PrintWarning("Initialized");
        }
        protected override void LoadDefaultMessages()
        {

            lang.RegisterMessages(new Dictionary<string, string>
            {
                [PluginMessages.NoPermission] = "<size=12>You don't have permission to use this command</size>",
                [PluginMessages.DeepCoverEnabled] = "<size=12>Admin deep cover has been <color=#00fa9a>Enabled</color></size>",
                [PluginMessages.DeepCoverDisabled] = "<size=12>Admin deep cover has been <color=#00fa9a>Disabled</color></size>",
                [PluginMessages.DeepCoverChanged] = "<size=12>Fake identity has changed to <color=#00fa9a>{0}</color></size>",
                [PluginMessages.RequestedFakeIdentifyNotFound] = "<size=12>Requested fake identity is not found</size>",
                [PluginMessages.NoFakeIdentitiesAvailable] = "<size=12>No fake identities available</size></size>",
                [PluginMessages.FakeIdentifyNotFound] = "<size=12>Fake identity is not found</size>",
                [PluginMessages.DataCorruptedUp] = "<size=12>Data is corrupt</size>",
            }, this);
        }
        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                var identify = GetIdentify(player);
                if (identify == null)
                    return;

                RemoveFakeIdentity(player, identify);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.ReconnectDeepCover)
            {
                OnPlayerRespawned(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var identify = GetIdentify(player);
            if (identify == null)
                return;
            RemoveFakeIdentity(player, identify, config.ReconnectDeepCover);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            var identify = GetIdentify(player);
            if (identify == null)
                return;
            if (!identify.IsEnabled)
            {
                if (IsIdentifyAvailable(identify))
                {
                    SetFakeIdentity(player, identify);
                    player.ChatMessage(Lang(PluginMessages.DeepCoverEnabled, player.UserIDString));
                }
                else
                {
                    Identify newIdentify;
                    if (config.ChangeIdentityInOrder)
                    {
                        newIdentify = GetAvailableIdentities().OrderBy(x => x.Profile).FirstOrDefault();
                    }
                    else
                    {
                        newIdentify = GetAvailableIdentities().GetRandom();
                    }
                    if (newIdentify == null)
                    {
                        player.ChatMessage(Lang(PluginMessages.DeepCoverDisabled, player.UserIDString));
                        return;
                    }
                    SetFakeIdentity(player, newIdentify);
                    player.ChatMessage(Lang(PluginMessages.DeepCoverChanged, player.UserIDString, newIdentify.Name));
                }
            }
            else
            {
                SetFakeIdentity(player, identify);
            }

        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (info == null || info.Initiator == null)
                return;

            var attacker = info.Initiator.ToPlayer();
            if (attacker == null)
                return;

            var identify = GetIdentify(attacker);
            if (identify == null)
                return;

            attacker.userID = identify.UserId;
            timer.Once(0.2f, () =>
            {
                if (attacker != null)
                    attacker.userID = identify.RestoreUserId;
            });
        }

        #region Chat Hooks
        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (plugins.Exists(nameof(BetterChat)))
                return null;

            var identify = GetIdentify(player);
            if (identify == null)
                return null;
            if (channel == Chat.ChatChannel.Team)
            {
                var userId = identify.RestoreUserId;
                var userName = identify.RestoreName;
                if (config.TeamChatRemainsDeepCover)
                {
                    userId = identify.UserId;
                    userName = identify.Name;
                }
                var formattedMessage = $"<color=#55aaff>{userName}</color>: <color=#ffffff>{message}</color>";
                RelationshipManager.PlayerTeam team = player.Team;
                if (team == null || team.members.Count == 0)
                {
                    return true;
                }

                team.BroadcastTeamChat(userId, userName, formattedMessage, "white");

                var onlineMemberConnections = team.GetOnlineMemberConnections();
                if (onlineMemberConnections != null)
                {
                    ConsoleNetwork.SendClientCommand(onlineMemberConnections, "chat.add", (int)channel, userId, formattedMessage);
                }
            }
            else
            {
                Server.Broadcast(message, $"<color=#5af>{identify.Name}</color>", identify.UserId);
            }
            return true;
        }

        private object OnUserChat(IPlayer player, string message)
        {
            if (!plugins.Exists(nameof(BetterChat)))
                return null;
            return IsDeepCovered(Convert.ToUInt64(player.Id)) ? true : (object)null;
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            object channel;
            if (!data.TryGetValue("ChatChannel", out channel) || !(channel is Chat.ChatChannel))
                return null;

            var chatChannel = (Chat.ChatChannel)channel;

            object player;
            if (!data.TryGetValue("Player", out player) || !(player is IPlayer))
                return null;

            var basePlayer = (BasePlayer)((IPlayer)player).Object;
            var identify = GetIdentify(basePlayer);
            if (identify == null)
                return null;

            object message;
            if (!data.TryGetValue("Message", out message) || message == null)
                return null;
            var formattedMessage = message.ToString();

            object username;
            if (!data.TryGetValue("UsernameSettings", out username) || !(username is Dictionary<string, object>))
                return null;
            var usernameSetting = (Dictionary<string, object>)username;

            object color;
            var usernameColor = "#55aaff";
            if (usernameSetting.TryGetValue("Color", out color) && color != null)
                usernameColor = color.ToString();

            var chatMessage = BetterChat?.Call("API_GetFormattedMessage", player, formattedMessage)?.ToString() ?? "";
            var consoleMessage = BetterChat?.Call("API_GetFormattedMessage", player, formattedMessage, true)?.ToString() ?? "";

            var userId = identify.UserId;
            var userName = identify.Name;
            if (chatChannel == Chat.ChatChannel.Team)
            {
                if (!config.TeamChatRemainsDeepCover)
                {
                    userId = identify.RestoreUserId;
                    userName = identify.RestoreName;

                    RemoveFakeIdentity(basePlayer, identify, true);
                    chatMessage = BetterChat?.Call("API_GetFormattedMessage", player, formattedMessage)?.ToString() ?? "";
                    consoleMessage = BetterChat?.Call("API_GetFormattedMessage", player, formattedMessage, true)?.ToString() ?? "";
                    SetFakeIdentity(basePlayer, identify);
                }
                RelationshipManager.PlayerTeam team = basePlayer.Team;
                if (team == null || team.members.Count == 0)
                    return null;

                team.BroadcastTeamChat(userId, userName, formattedMessage, usernameColor);

                List<Network.Connection> onlineMemberConnections = team.GetOnlineMemberConnections();
                if (onlineMemberConnections != null)
                {
                    ConsoleNetwork.SendClientCommand(onlineMemberConnections, "chat.add", new object[] { (int)chatChannel, userId.ToString(), chatMessage });
                }
            }
            else
            {
                object blocked;
                var blockedIds = new List<string>();
                if (data.TryGetValue("BlockedReceivers", out blocked) && blocked is List<string>)
                    blockedIds = (List<string>)blocked;

                foreach (BasePlayer p in BasePlayer.activePlayerList.Where(p => !blockedIds.Contains(p.UserIDString)))
                    p.SendConsoleCommand("chat.add", new object[] { (int)chatChannel, userId.ToString(), chatMessage });
            }

            Puts($"[{chatChannel}] {consoleMessage}");

            RCon.Broadcast(RCon.LogType.Chat, new Chat.ChatEntry
            {
                Channel = chatChannel,
                Message = consoleMessage,
                UserId = userId.ToString(),
                Username = userName,
                Color = usernameColor,
                Time = Epoch.Current
            });

            return true;
        }
        #endregion

        #endregion

        #region Commands
        [ConsoleCommand("deepcover")]
        private void ccmdDeepCover(ConsoleSystem.Arg arg)
            => cmdDeepCover((BasePlayer)arg.Connection.player, arg.cmd.FullName, arg.Args);

        [ChatCommand("deepcover")]
        private void cmdDeepCover(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, Perm))
            {
                player.ChatMessage(Lang(PluginMessages.NoPermission, player.UserIDString));
                return;
            }

            string profileString = null;
            int? profile = null;
            if (args.Length == 1)
            {
                profileString = args[0];
                if (!string.IsNullOrWhiteSpace(profileString))
                {
                    int output;
                    if (int.TryParse(profileString, out output))
                        profile = output;
                }
            }

            if (!IsDeepCovered(player.userID))
            {
                Identify identify = null;
                if (!config.ChangeIdentityInOrder && !profile.HasValue)
                {
                    identify = GetAvailableIdentities().GetRandom();
                }
                else
                {
                    if (profile.HasValue)
                    {
                        identify = GetAvailableIdentities().FirstOrDefault(x => x.Profile == profile);
                        if (identify == null)
                        {
                            player.ChatMessage(Lang(PluginMessages.RequestedFakeIdentifyNotFound, player.UserIDString));
                            return;
                        }
                    }
                    else
                    {
                        identify = GetAvailableIdentities().OrderBy(x => x.Profile).FirstOrDefault();
                        if (identify == null)
                        {
                            player.ChatMessage(Lang(PluginMessages.NoFakeIdentitiesAvailable, player.UserIDString));
                            return;
                        }
                    }
                }

                if (identify == null)
                {
                    player.ChatMessage(Lang(PluginMessages.FakeIdentifyNotFound, player.UserIDString));
                    return;
                }
                SetFakeIdentity(player, identify, false, true);
                player.ChatMessage(Lang(PluginMessages.DeepCoverEnabled, player.UserIDString));
            }
            else
            {
                if (profile.HasValue)
                {
                    var identify = GetAvailableIdentities().FirstOrDefault(x => x.Profile == profile);
                    if (identify == null)
                    {
                        player.ChatMessage(Lang(PluginMessages.RequestedFakeIdentifyNotFound, player.UserIDString));
                        return;
                    }

                    SetFakeIdentity(player, identify, true, true);
                    player.ChatMessage(Lang(PluginMessages.DeepCoverChanged, player.UserIDString, identify.Name));
                }
                else
                {
                    var identify = GetIdentify(player);
                    RemoveFakeIdentity(player, identify);
                    player.ChatMessage(Lang(PluginMessages.DeepCoverDisabled, player.UserIDString));
                }
            }
        }
        #endregion

        #region Methods
        private RestoreInfo GetIdentify(BasePlayer player)
        {
            RestoreInfo identify;
            if (_adminDeepCoverData.PlayerData.TryGetValue(player.userID, out identify) && identify != null && !identify.IsRemoved)
                return identify;

            return null;
        }

        private void SetFakeIdentity(BasePlayer player, Identify identify, bool isChange = false, bool sendDiscordNotification = false)
        {
            var playerId = player.userID;
            var playerName = player.displayName;
            var wasAdmin = player.IsAdmin;
            var currentGroups = permission.GetUserGroups(player.UserIDString);
            Player.Rename(player, identify.Name);

            if (player.IsAdmin && config.RemoveAdminFlag && !isChange)
            {
                ServerUsers.Set(player.userID, ServerUsers.UserGroup.None, "", "");
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.Connection.authLevel = 0;
            }
            if (config.IsDiscordEnabled && sendDiscordNotification)
            {
                SendDiscordMessage(player.userID, playerName, identify.UserId, identify.Name, GetGrid(player.ServerPosition));
            }

            RestoreInfo restoreInfo;
            if (!_adminDeepCoverData.PlayerData.TryGetValue(player.userID, out restoreInfo) || restoreInfo == null)
            {
                _adminDeepCoverData.PlayerData.Add(player.userID, new RestoreInfo
                {
                    IsEnabled = true,
                    IsRemoved = false,
                    Profile = identify.Profile,
                    RestoreName = playerName,
                    RestoreUserId = playerId,
                    WasAdmin = wasAdmin,
                    ChatGroup = identify.ChatGroup,
                    Groups = currentGroups,
                    Name = identify.Name,
                    UserId = identify.UserId,
                });
            }
            else
            {
                restoreInfo.IsEnabled = true;
                restoreInfo.IsRemoved = false;
                restoreInfo.Profile = identify.Profile;
                restoreInfo.Name = identify.Name;
                restoreInfo.UserId = identify.UserId;
                restoreInfo.ChatGroup = identify.ChatGroup;
                if (!isChange)
                {
                    restoreInfo.WasAdmin = wasAdmin;
                    restoreInfo.Groups = currentGroups;
                }
            }

            if (currentGroups != null)
                foreach (var oldGroup in currentGroups)
                {
                    permission.RemoveUserGroup(player.UserIDString, oldGroup);
                }
            permission.AddUserGroup(player.UserIDString, identify.ChatGroup);

            player.SendNetworkUpdateImmediate();
            SaveData();
        }

        private void RemoveFakeIdentity(BasePlayer player, RestoreInfo restoreInfo, bool tempRemove = false)
        {
            Player.Rename(player, restoreInfo.RestoreName);
            if (restoreInfo.WasAdmin)
            {
                ServerUsers.Set(player.userID, ServerUsers.UserGroup.Owner, "", "");
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.Connection.authLevel = 2;
            }

            if (tempRemove)
                restoreInfo.IsEnabled = false;
            else
                restoreInfo.IsRemoved = true;

            permission.RemoveUserGroup(player.UserIDString, restoreInfo.ChatGroup);
            if (restoreInfo.Groups != null)
                foreach (var oldGroup in restoreInfo.Groups)
                {
                    permission.AddUserGroup(player.UserIDString, oldGroup);
                }
            player.SendNetworkUpdateImmediate();
            SaveData();
        }

        private List<Identify> GetAvailableIdentities()
        {
            var identities = config.Identifies;
            var usedIdentities = _adminDeepCoverData.PlayerData.Where(x => x.Value != null && x.Value.IsEnabled && !x.Value.IsRemoved)
                .Select(x => x.Value).ToList();
            return identities.Where(x => usedIdentities.All(m => m.Profile != x.Profile)).ToList();
        }

        private bool IsIdentifyAvailable(RestoreInfo restoreInfo)
        {
            var usedIdentities = _adminDeepCoverData.PlayerData.Where(x => x.Value != null && x.Value.IsEnabled && !x.Value.IsRemoved)
                .Select(x => x.Value).ToList();
            return usedIdentities.All(x => x.Profile != restoreInfo.Profile);
        }
        private string GetGrid(Vector3 pos)
        {
            char letter = 'A';
            var x = Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) % 26;
            var count = Mathf.Floor(Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) / 26);
            var z = (Mathf.Floor(ConVar.Server.worldsize / 146.3f)) - Mathf.Floor((pos.z + (ConVar.Server.worldsize / 2)) / 146.3f);
            letter = (char)(letter + x);
            var secondLetter = count <= 0 ? string.Empty : ((char)('A' + (count - 1))).ToString();
            return $"{secondLetter}{letter}{z - 1}";
        }
        private void SendDiscordMessage(ulong playerId, string playerName, ulong fakeId, string fakeName, string grid)
        {
            if (string.IsNullOrWhiteSpace(config.DiscordWebHookUrl))
                return;

            var hexColorNumber = config.DiscordEmbedColor?.Replace("x", string.Empty);
            int color;
            if (!int.TryParse(hexColorNumber, NumberStyles.HexNumber, null, out color))
                color = 3092790;

            var mentions = "";
            if (config.DiscordRolesToMention != null)
                foreach (var roleId in config.DiscordRolesToMention)
                {
                    mentions += $"<@&{roleId}> ";
                }

            var contentBody = new WebHookContentBody
            {
                Content = $"{mentions}{config.DiscordMessage}"
            };
            var body = new WebHookEmbedBody
            {
                Embeds = new[]
                {
                    new WebHookEmbed
                    {
                        Description = string.Format(config.DiscordEmbedDescription, playerName, $"[{playerId}](https://steamcommunity.com/profiles/{playerId})" , fakeName, $"[{fakeId}](https://steamcommunity.com/profiles/{fakeId})" , grid),
                        Color = color
                    }
                }
            };
            webrequest.Enqueue(config.DiscordWebHookUrl, JsonConvert.SerializeObject(contentBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                (headerCode, headerResult) =>
                {
                    if (headerCode >= 200 && headerCode <= 204)
                    {
                        webrequest.Enqueue(config.DiscordWebHookUrl, JsonConvert.SerializeObject(body, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                            (code, result) => { }, this, RequestMethod.POST,
                            new Dictionary<string, string> { { "Content-Type", "application/json" } });
                    }
                }, this, RequestMethod.POST,
                new Dictionary<string, string> { { "Content-Type", "application/json" } });
        }

        private bool IsDeepCovered(ulong playerId)
        {
            RestoreInfo restoreInfo;
            if (!_adminDeepCoverData.PlayerData.TryGetValue(playerId, out restoreInfo) || restoreInfo == null)
            {
                return false;
            }
            return restoreInfo.IsEnabled && !restoreInfo.IsRemoved;
        }
        #endregion

        #region API
        private bool API_IsDeepCovered(BasePlayer player) => IsDeepCovered(player.userID);

        #endregion

        #region Helpers
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);
        private void SaveData()
        {
            _pluginData.WriteObject(_adminDeepCoverData);
        }

        private void LoadData()
        {
            try
            {
                _adminDeepCoverData = _pluginData.ReadObject<AdminDeepCoverData>();
                Puts("Data File Loaded");
            }
            catch
            {
                Puts("Couldn't load admin deep cover data, creating new datafile");
                _adminDeepCoverData = new AdminDeepCoverData();
                SaveData();
            }
        }

        #endregion

        #region Classes
        private static class PluginMessages
        {
            public const string NoPermission = "No Permission";
            public const string DeepCoverEnabled = "Deep Cover Enabled";
            public const string DeepCoverDisabled = "Deep Cover Disabled";
            public const string DeepCoverChanged = "Deep Cover Changed";
            public const string RequestedFakeIdentifyNotFound = "Requested Fake Identify Not Found";
            public const string NoFakeIdentitiesAvailable = "No Fake Identities Available";
            public const string FakeIdentifyNotFound = "Fake Identify Not Found";
            public const string DataCorruptedUp = "Data Corrupted Up";
        }
        private class AdminDeepCoverData
        {
            public Dictionary<ulong, RestoreInfo> PlayerData { get; set; } = new Dictionary<ulong, RestoreInfo>();
        }
        public class Identify
        {
            public int Profile { get; set; }
            public string Name { get; set; }
            public ulong UserId { get; set; }
            [JsonProperty("Better Chat Group")]
            public string ChatGroup { get; set; }
        }
        public class RestoreInfo : Identify
        {
            public bool IsEnabled { get; set; }
            public bool IsRemoved { get; set; }
            public bool WasAdmin { get; set; }
            public string[] Groups { get; set; }
            public string RestoreName { get; set; }
            public ulong RestoreUserId { get; set; }
        }
        private class Configuration
        {
            [JsonProperty("Change Identity In Order")]
            public bool ChangeIdentityInOrder { get; set; } = true;

            [JsonProperty("Remain Deep Covered After Reconnect")]
            public bool ReconnectDeepCover { get; set; } = true;

            [JsonProperty("Remain Deep Covered In Team Chat")]
            public bool TeamChatRemainsDeepCover { get; set; } = false;

            [JsonProperty("Remove Admin Flag When Deep Covered")]
            public bool RemoveAdminFlag { get; set; } = false;

            [JsonProperty(PropertyName = "Discord - Enabled")]
            public bool IsDiscordEnabled { get; set; } = false;

            [JsonProperty(PropertyName = "Discord - Webhook URL")]
            public string DiscordWebHookUrl { get; set; }

            [JsonProperty(PropertyName = "Discord - Embed Color")]
            public string DiscordEmbedColor { get; set; } = "#2F3136";

            [JsonProperty(PropertyName = "Discord - Message")]
            public string DiscordMessage { get; set; } = "Admin Deep Cover";

            [JsonProperty(PropertyName = "Discord - Embed - Description")]
            public string DiscordEmbedDescription { get; set; } =
                "{0} {1} has become deep covered\n\nIdentity Used\n{2} {3}\n\nLocation\n{4}";

            [JsonProperty(PropertyName = "Discord - Roles To Mention")]
            public List<string> DiscordRolesToMention { get; set; } = new List<string>();

            [JsonProperty("Fake Identities", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Identify> Identifies { get; set; } = new List<Identify>
            {
                new Identify
                {
                    Profile = 1,
                    Name = "garry",
                    UserId = 76561197960279927,
                    ChatGroup = "default"
                },
                new Identify
                {
                    Profile = 2,
                    Name = "Alistair",
                    UserId = 76561198240345356,
                    ChatGroup = "default"
                },
                new Identify
                {
                    Profile = 3,
                    Name = "Helk",
                    UserId = 76561197992746895,
                    ChatGroup = "default"
                },
                new Identify
                {
                    Profile = 4,
                    Name = "Holmzy",
                    UserId = 76561198002789398,
                    ChatGroup = "default"
                }
            };
        }

        private class WebHookEmbedBody
        {
            [JsonProperty(PropertyName = "embeds")]
            public WebHookEmbed[] Embeds;
        }

        private class WebHookContentBody
        {
            [JsonProperty(PropertyName = "content")]
            public string Content;
        }

        private class WebHookEmbed
        {
            [JsonProperty(PropertyName = "title")]
            public string Title;

            [JsonProperty(PropertyName = "type")]
            public string Type = "rich";

            [JsonProperty(PropertyName = "description")]
            public string Description;

            [JsonProperty(PropertyName = "color")]
            public int Color;

            [JsonProperty(PropertyName = "author")]
            public WebHookAuthor Author;

            [JsonProperty(PropertyName = "image")]
            public WebHookImage Image;

            [JsonProperty(PropertyName = "fields")]
            public List<WebHookField> Fields;

            [JsonProperty(PropertyName = "footer")]
            public WebHookFooter Footer;
        }
        private class WebHookAuthor
        {
            [JsonProperty(PropertyName = "name")]
            public string Name;

            [JsonProperty(PropertyName = "url")]
            public string AuthorUrl;

            [JsonProperty(PropertyName = "icon_url")]
            public string AuthorIconUrl;
        }
        private class WebHookImage
        {
            [JsonProperty(PropertyName = "proxy_url")]
            public string ProxyUrl;

            [JsonProperty(PropertyName = "url")]
            public string Url;

            [JsonProperty(PropertyName = "height")]
            public int? Height;

            [JsonProperty(PropertyName = "width")]
            public int? Width;
        }
        private class WebHookField
        {
            [JsonProperty(PropertyName = "name")]
            public string Name;

            [JsonProperty(PropertyName = "value")]
            public string Value;

            [JsonProperty(PropertyName = "inline")]
            public bool Inline;
        }
        private class WebHookFooter
        {
            [JsonProperty(PropertyName = "text")]
            public string Text;

            [JsonProperty(PropertyName = "icon_url")]
            public string IconUrl;

            [JsonProperty(PropertyName = "proxy_icon_url")]
            public string ProxyIconUrl;
        }

        #endregion
    }
}