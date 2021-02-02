using Oxide.Core;
using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Collections;
using System.Text;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("Teams Logger", "NickRimmer", "1.4")]
    [Description("Simple plugin to log team events")]
    public class TeamsLogger : RustPlugin
    {
        private PluginConfig _config;
        private DiscordComponent _discord;

        void Init()
        {
            _config = Config.ReadObject<PluginConfig>();

            if (IsFirstRun()) LogCurrentTeams();
            
            if (!string.IsNullOrEmpty(_config.DiscordHookUrl))
            {
                var loader = new GameObject("WebObject");
                _discord = loader.AddComponent<DiscordComponent>().Configure(_config.DiscordHookUrl);
            }
        }

        private void PrintToConsole(string message) => Puts(message);

        private void PrintToFile(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            message = $"{timestamp} | {message}";

            LogToFile("common", message, this);
        }

        private void PrintToDiscord(string message)
        {
            if(_discord == null)
            {
                PrintWarning("Discord wasn't configured, message can't be sent");
                return;
            }

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            message = $"{timestamp} | {message}";

            _discord.SendTextMessage(message);
        }

        #region Log exist teams on first run

        private bool IsFirstRun()
        {
            var dataFileName = $"{GetType().Name}.{nameof(PluginData)}";
            var data = Interface.uMod.DataFileSystem.ReadObject<PluginData>(dataFileName);

            if (data.FirstStart)
            {
                data.FirstStart = false;
                Interface.uMod.DataFileSystem.WriteObject(dataFileName, data);

                var timestamp = DateTime.Now.ToString("HH:mm:ss");

                return true;
            }

            return false;
        }

        private void LogCurrentTeams()
        {
            var teams = RelationshipManager.Instance.teams;
            foreach (var team in teams)
            {
                LogKey(
                    "CurrentTeam",
                    team.Key,
                    string.Join(", ", team.Value.members.Select(GetUserInfo))
                );
            }
        }

        private string GetUserInfo(ulong userId)
        {
            var user = covalence.Players.FindPlayerById(userId.ToString());
            return user == null
                ? userId.ToString()
                : $"{user.Name} ({userId})";
        }
        #endregion

        #region Team hooks

        void OnTeamInvite(BasePlayer inviter, BasePlayer target) => LogKey(
            "OnTeamInvite",
            inviter.Team.teamID,
            inviter.displayName,
            inviter.userID,
            target.displayName,
            target.userID);

        void OnTeamRejectInvite(BasePlayer rejector, RelationshipManager.PlayerTeam team) => LogKey(
            "OnTeamRejectInvite",
            team.teamID,
            rejector.displayName,
            rejector.userID);

        void OnTeamPromote(RelationshipManager.PlayerTeam team, BasePlayer newLeader) => LogKey(
            "OnTeamPromote",
            team.teamID,
            newLeader.displayName,
            newLeader.userID);

        void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player) => LogKey(
            "OnTeamLeave",
            team.teamID,
            player.displayName,
            player.userID,
            player.userID == team.teamLeader);

        void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player) => LogKey(
            "OnTeamKick",
            team.teamID,
            player.displayName,
            player.Team);

        void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player) => LogKey(
            "OnTeamAcceptInvite",
            team.teamID,
            player.displayName,
            player.userID);

        void OnTeamDisbanded(RelationshipManager.PlayerTeam team) => LogKey(
            "OnTeamDisbanded",
            team.teamID,
            team.GetLeader().displayName,
            team.teamLeader);

        /*private void OnTeamUpdate(ulong currentTeam, ulong newTeam, BasePlayer player) => LogKey(
            "OnTeamUpdate",
            newTeam,
            player.displayName,
            player.userID,
            currentTeam);*/

        private void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team) => LogKey(
            "OnTeamCreated",
            team.teamID,
            player.displayName,
            player.userID);

        #endregion

        #region Boring things

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new RuLocalization(), this, "ru");
            lang.RegisterMessages(new DefaultLocalization(), this);
        }
        protected override void LoadDefaultConfig() => Config.WriteObject(new PluginConfig(), true);

        private void LogKey(string key, params object[] args) =>
            LogMessage(this[key, args]);

        private void LogMessage(string message)
        {
            if (_config.PrintToConsole) PrintToConsole(message);
            if (_config.PrintToFile) PrintToFile(message);
            if (_config.PrintToDiscord) PrintToDiscord(message);
        }

        private string this[string key, params object[] args] => args?.Any() == true
            ? string.Format(lang.GetMessage(key, this), args)
            : lang.GetMessage(key, this);

        #endregion
    
        #region TeamsLogger.Localization

        private class DefaultLocalization : Dictionary<string, string>
        {
            public DefaultLocalization()
            {
                this["OnTeamCreated"] =      "Created {0}  | Player: '{1}' ({2}) created new team";
                this["OnTeamDisbanded"] =    "Disband {0}  | Team leader: '{1}' ({2})";
                this["OnTeamLeave"] =        "Leave {0}    | Player: '{1}' ({2}, is leader: {3})";
                this["OnTeamInvite"] =       "Invite {0}   | Player '{1}' ({2}) sent invite to '{3}' {4}";
                this["OnTeamRejectInvite"] = "Reject {0}   | Player: '{1}' ({2}) rejected invite";
                this["OnTeamPromote"] =      "Promote {0}  | Player: '{1}' ({2}) now is new leader";
                this["OnTeamKick"] =         "Kick {0}     | Player: '{1}' ({2}) was kicked";
                this["OnTeamAcceptInvite"] = "Accept {0}   | Player: '{1}' ({2}) accepted invite";
                //this["OnTeamUpdate"] =     "Update {0}   | Player: '{1}' ({2}) changed group {3} to {0}";
                this["CurrentTeam"] =        "History {0}  | Previously created team with members: {1}";
            }
        }
    
        private class RuLocalization : Dictionary<string, string>
        {
            public RuLocalization()
            {
                this["OnTeamCreated"] =      "Создана {0}     | Игрок: '{1}' ({2}) создал новую группу";
                this["OnTeamDisbanded"] =    "Удалена {0}     | Лидер: '{1}' ({2})";
                this["OnTeamLeave"] =        "Покинул {0}     | Игрок: '{1}' ({2}, лидер: {3})";
                this["OnTeamInvite"] =       "Приглашение {0} | Игрок '{1}' ({2}) отправил приглашение игроку '{3}' {4}";
                this["OnTeamRejectInvite"] = "Отказ {0}       | Игрок: '{1}' ({2}) отказался вступать в группу";
                this["OnTeamPromote"] =      "Лидер {0}       | Игрок: '{1}' ({2}) теперь новый лидер группы";
                this["OnTeamKick"] =         "Изгнан {0}      | Игрок: '{1}' ({2}) был выгнан из команды";
                this["OnTeamAcceptInvite"] = "Вступил {0}     | Игрок: '{1}' ({2}) принял приглашение вступить в группу";
                //this["OnTeamUpdate"] =     "Переход {0}     | Игрок: '{1}' ({2}) перешел из группы {3} в {0}";
                this["CurrentTeam"] =        "История {0}     | Ранее созданная группа с участниками: {1}";
            }
        }
    
        #endregion
    
        #region TeamsLogger.Models

        private class PluginConfig
        {
            [JsonProperty("Print logs to console")]
            public bool PrintToConsole { get; set; } = true;
    
            [JsonProperty("Print logs to file")]
            public bool PrintToFile { get; set; } = true;
    
            [JsonProperty("Print logs to Discord")]
            public bool PrintToDiscord { get; set; } = false;
    
            [JsonProperty("Discord hook url")] 
            public string DiscordHookUrl { get; set; }
        }
    
        private class PluginData
        {
            public bool FirstStart { get; set; } = true;
        }
    
        #endregion
    
        #region Shared.Components

        private class DiscordComponent : MonoBehaviour
        {
            private const float PostDelay = 1f;
    
            private readonly Queue<object> _queue = new Queue<object>();
            private string _url;
            private bool _busy = false;
    
            public DiscordComponent Configure(string url)
            {
                if (url == null) throw new ArgumentNullException(nameof(url));
                _url = url;
    
                return this;
            }
    
            public DiscordComponent SendTextMessage(string message, params object[] args)
            {
                message = args?.Any() == true ? string.Format(message, args) : message;
                return AddQueue(new MessageRequest(message));
            }
    
            #region Send requests to server
    
            private DiscordComponent AddQueue(object request)
            {
                _queue.Enqueue(request);
    
                if (!_busy)
                    StartCoroutine(ProcessQueue());
    
                return this;
            }
    
            private IEnumerator ProcessQueue()
            {
                if(_busy) yield break;
                _busy = true;
    
                while (_queue.Any())
                {
                    var request = _queue.Dequeue();
                    yield return ProcessRequest(request);
                }
    
                _busy = false;
            }
    
            private IEnumerator ProcessRequest(object request)
            {
                if (string.IsNullOrEmpty(_url))
                {
                    print("[ERROR] Discord webhook URL wasn't specified");
                    yield break;
                }
    
                var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                var uh = new UploadHandlerRaw(data) {contentType = "application/json"};
                var www = UnityWebRequest.Post(_url, UnityWebRequest.kHttpVerbPOST);
                www.uploadHandler = uh;
    
                yield return www.SendWebRequest();
    
                if (www.isNetworkError || www.isHttpError)
                    print($"ERROR: {www.error} | {www.downloadHandler?.text}");
    
                www.Dispose();
    
                // to avoid spam requests to Discord
                yield return new WaitForSeconds(PostDelay);
            }
    
            #endregion
    
            #region Requests
    
            private class MessageRequest
            {
                [JsonProperty("content")]
                public string Content { get; set; }
    
                public MessageRequest(string content)
                {
                    if (content == null) throw new ArgumentNullException(nameof(content));
                    Content = content;
                }
            }
    
            #endregion
        }
    
        #endregion
    }
}
