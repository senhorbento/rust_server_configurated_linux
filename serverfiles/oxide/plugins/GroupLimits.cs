using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Group Limits", "misticos", "3.0.1")]
    [Description("Prevent rulebreakers from breaking group limits on your server and notify your staff")]
    class GroupLimits : CovalencePlugin
    {
        #region Variables

        [PluginReference("DiscordMessages")]
        private Plugin _discordMessages = null;

        private const string PermissionIgnore = "grouplimits.ignore";

        private static GroupLimits _ins;

        #endregion

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Limit> Limits = new List<Limit> {new Limit()};

            [JsonProperty(PropertyName = "Log Format")]
            public string LogFormat =
                "[{time}] {id} ({name}) authorized on {shortname}/{entid} ({type}) at ({position})";

            public class Limit
            {
                [JsonProperty(PropertyName = "Type Name")]
                public string Name = "Any";

                [JsonProperty(PropertyName = "Max Authorized")]
                public int MaxAuthorized = 3;

                [JsonProperty(PropertyName = "Shortnames", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> Shortnames = new List<string> {"global"};

                [JsonProperty(PropertyName = "Disable For Decaying Structures")]
                public bool NoDecaying = true;

                [JsonProperty(PropertyName = "Notify Player")]
                public bool NotifyPlayer = true;

                [JsonProperty(PropertyName = "Notify Owner")]
                public bool NotifyOwner = true;

                [JsonProperty(PropertyName = "Enforce")]
                public bool Enforce = false;

                [JsonProperty(PropertyName = "Deauthorize")]
                public bool Deauthorize = true;

                [JsonProperty(PropertyName = "Deauthorize All")]
                public bool DeauthorizeAll = false;

                [JsonProperty(PropertyName = "Discord")]
                public Discord Webhook = new Discord();

                [JsonProperty(PropertyName = "Log To File")]
                public bool File = false;

                public static Limit Find(string shortname)
                {
                    var cLimit = (Limit) null;
                    foreach (var limit in _ins._config.Limits)
                    {
                        if (limit.Shortnames.Contains("global"))
                            cLimit = limit;

                        if (limit.Shortnames.Contains(shortname))
                            return limit;
                    }

                    return cLimit;
                }

                public class Discord
                {
                    [JsonProperty(PropertyName = "Webhook")]
                    public string Webhook = string.Empty;

                    [JsonProperty(PropertyName = "Inline")]
                    public bool Inline = true;

                    [JsonProperty(PropertyName = "Title")]
                    public string Title = "Group Limit: Exceeded or deauthorized";

                    [JsonProperty(PropertyName = "Color")]
                    public int Color = 0;

                    [JsonProperty(PropertyName = "Player Title")]
                    public string PlayerTitle = "Player";

                    [JsonProperty(PropertyName = "Player")]
                    public string Player = "{name}/{id}";

                    [JsonProperty(PropertyName = "Authed Title")]
                    public string AuthedTitle = "Authorized Players";

                    [JsonProperty(PropertyName = "Authed")]
                    public string Authed = "{list}";

                    [JsonProperty(PropertyName = "Authed Entry")]
                    public string AuthedEntry = "{name}/{id}";

                    [JsonProperty(PropertyName = "Authed Separator")]
                    public string AuthedSeparator = "\n";

                    [JsonProperty(PropertyName = "Entity Title")]
                    public string EntityTitle = "Entity";

                    [JsonProperty(PropertyName = "Entity")]
                    public string Entity = "{shortname}/{id} ({type})";

                    [JsonProperty(PropertyName = "Position Title")]
                    public string PositionTitle = "Position";

                    [JsonProperty(PropertyName = "Position")]
                    public string Position = "teleportpos {position}";
                }
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Hooks

        private void Init()
        {
            _ins = this;

            permission.RegisterPermission(PermissionIgnore, this);
        }

        private void Unload()
        {
            _ins = null;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Notify: Player", "You are trying to exceed the group limit on our server."},
                {
                    "Notify: Owner",
                    "{name} tried to exceed the group limit on your entity at {position}. (Type: {type})"
                },
                {"Notify: Deauthorize Player", "One person was deauthorized, try to authorize again if you were not."},
                {
                    "Notify: Deauthorize Owner",
                    "{name} tried to authorize on your entity. One person was deauthorized on your entity at {position}. (Type: {type})"
                }
            }, this);
        }

        private object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (privilege.authorizedPlayers.Count == 0)
                return null;
            
            if (player.IPlayer.HasPermission(PermissionIgnore))
                return null;

            var limit = Configuration.Limit.Find(privilege.ShortPrefabName);
            if (limit == null)
                return null;

            privilege.authorizedPlayers.RemoveAll(x => x.userid == player.userID);
            if (privilege.authorizedPlayers.Count < limit.MaxAuthorized)
                return null;

            if (limit.NoDecaying && IsDecaying(privilege))
                return null;

            var authed = privilege.authorizedPlayers.Select(x => x.userid);
            if (limit.Deauthorize)
            {
                // Make sure we send authed players before clearing
                authed = authed.ToArray();

                if (limit.DeauthorizeAll)
                    privilege.authorizedPlayers.Clear();
                else
                    privilege.authorizedPlayers.RemoveAt(0);

                privilege.SendNetworkUpdate();
            }

            Notify(limit, privilege, player, authed, limit.Deauthorize);

            if (limit.Enforce)
                return true;

            return null;
        }

        private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (codeLock.whitelistPlayers.Count == 0 && codeLock.guestCode.Length == 0)
                return null;
            
            if (player.IPlayer.HasPermission(PermissionIgnore))
                return null;

            var isCodeAdmin = codeLock.code == code;
            var isCodeGuest = codeLock.guestCode == code;
            if (!isCodeAdmin && !isCodeGuest)
                return null;

            var limit = Configuration.Limit.Find(codeLock.ShortPrefabName);
            if (limit == null)
                return null;

            if (codeLock.guestPlayers.Count + codeLock.whitelistPlayers.Count < limit.MaxAuthorized)
                return null;

            var entity = codeLock.GetParentEntity();
            if (entity == null || !entity.IsValid())
                return null;

            if (limit.NoDecaying && IsDecaying(entity.GetBuildingPrivilege()))
                return null;

            var authed = codeLock.whitelistPlayers.Union(codeLock.guestPlayers);
            if (limit.Deauthorize)
            {
                // Make sure we send authed players before clearing
                authed = authed.ToArray();
                
                if (isCodeAdmin && codeLock.whitelistPlayers.Count > 0)
                {
                    if (limit.DeauthorizeAll)
                        codeLock.whitelistPlayers.Clear();
                    else
                        codeLock.whitelistPlayers.RemoveAt(0);
                }

                if (isCodeGuest && codeLock.guestPlayers.Count > 0)
                {
                    if (limit.DeauthorizeAll)
                        codeLock.guestPlayers.Clear();
                    else
                        codeLock.guestPlayers.RemoveAt(0);
                }

                codeLock.SendNetworkUpdate();
            }

            Notify(limit, entity, player, authed, limit.Deauthorize);

            if (limit.Enforce)
                return true;

            return null;
        }

        private object OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            if (turret.authorizedPlayers.Count == 0)
                return null;
            
            if (player.IPlayer.HasPermission(PermissionIgnore))
                return null;

            var limit = Configuration.Limit.Find(turret.ShortPrefabName);
            if (limit == null)
                return null;

            turret.authorizedPlayers.RemoveAll(x => x.userid == player.userID);
            if (turret.authorizedPlayers.Count < limit.MaxAuthorized)
                return null;

            if (limit.NoDecaying && IsDecaying(turret.GetBuildingPrivilege()))
                return null;

            var authed = turret.authorizedPlayers.Select(x => x.userid);
            if (limit.Deauthorize)
            {
                // Make sure we send authed players before clearing
                authed = authed.ToArray();

                if (limit.DeauthorizeAll)
                    turret.authorizedPlayers.Clear();
                else
                    turret.authorizedPlayers.RemoveAt(0);
            }

            Notify(limit, turret, player, authed, limit.Deauthorize);

            if (limit.Enforce)
                return true;

            return null;
        }

        #endregion

        #region Helpers

        private void Notify(Configuration.Limit limit, BaseEntity entity, BasePlayer basePlayer,
            IEnumerable<ulong> authed, bool deauth)
        {
            if (limit.NotifyPlayer)
            {
                var player = basePlayer?.IPlayer;
                if (player != null && player.IsConnected)
                {
                    player.Message(GetMsg(deauth ? "Notify: Deauthorize Player" : "Notify: Player", player.Id));
                }
            }

            if (limit.NotifyOwner)
            {
                var player = players.FindPlayerById(entity.OwnerID.ToString());
                if (player != null && player.IsConnected)
                {
                    var sb = new StringBuilder(
                        GetMsg(deauth ? "Notify: Deauthorize Owner" : "Notify: Owner", player.Id));

                    sb.Replace("{position}", entity.transform.position.ToString());
                    sb.Replace("{type}", limit.Name);
                    sb.Replace("{name}", basePlayer?.displayName ?? "Unknown");

                    player.Message(sb.ToString());
                }
            }

            NotifyLog(limit, entity, basePlayer);
            NotifyDiscord(limit, entity, basePlayer, authed);
        }

        private void NotifyLog(Configuration.Limit limit, BaseNetworkable entity, BasePlayer player)
        {
            if (!limit.File)
                return;

            var builder = new StringBuilder(_config.LogFormat);
            builder.Replace("{time}", DateTime.Now.ToLongTimeString());
            builder.Replace("{name}", player?.displayName ?? "Unknown");
            builder.Replace("{id}", player?.UserIDString ?? "0");
            builder.Replace("{shortname}", entity.ShortPrefabName);
            builder.Replace("{entid}", entity.net.ID.ToString());
            builder.Replace("{type}", limit.Name);
            builder.Replace("{position}", FormattedCoordinates(entity.transform.position));

            LogToFile("Log", builder.ToString(), this);
        }

        private void NotifyDiscord(Configuration.Limit limit, BaseNetworkable entity, BasePlayer player,
            IEnumerable<ulong> authedPlayers)
        {
            var discord = limit.Webhook;
            if (string.IsNullOrEmpty(discord.Webhook) || _discordMessages == null || !_discordMessages.IsLoaded)
                return;

            var builder = new StringBuilder();
            foreach (var authed in authedPlayers)
            {
                if (builder.Length != 0)
                    builder.Append(discord.AuthedSeparator);

                builder.Append(discord.AuthedEntry);
                builder.Replace("{id}", authed.ToString());
                builder.Replace("{name}", players.FindPlayerById(authed.ToString())?.Name ?? "Unknown");
            }

            var list = builder.ToString();
            builder.Clear().Append(discord.Authed).Replace("{list}", list);

            object fields = new[]
            {
                new
                {
                    name = discord.PlayerTitle,
                    value = discord.Player.Replace("{name}", player?.displayName ?? "Unknown")
                        .Replace("{id}", player?.UserIDString ?? "0"),
                    inline = discord.Inline
                },
                new
                {
                    name = discord.AuthedTitle,
                    value = builder.ToString(),
                    inline = discord.Inline
                },
                new
                {
                    name = discord.EntityTitle,
                    value = discord.Entity.Replace("{shortname}", entity.ShortPrefabName)
                        .Replace("{id}", entity.net.ID.ToString()).Replace("{type}", limit.Name),
                    inline = discord.Inline
                },
                new
                {
                    name = discord.PositionTitle,
                    value = discord.Position.Replace("{position}", FormattedCoordinates(entity.transform.position)),
                    inline = discord.Inline
                }
            };

            _discordMessages.CallHook("API_SendFancyMessage", discord.Webhook, discord.Title, discord.Color,
                JsonConvert.SerializeObject(fields));
        }

        private string FormattedCoordinates(Vector3 pos) => $"{pos.x},{pos.y},{pos.z}";

        private bool IsDecaying(BuildingPrivlidge privilege) =>
            privilege == null || privilege.GetProtectedMinutes(true) <= 0;

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}