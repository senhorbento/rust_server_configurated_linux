
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MiniCopter Lock", "Thisha", "0.1.5")]
    [Description("Gives players the ability to lock a minicopter")]
    class MiniCopterLock : RustPlugin
    {
        #region variables
        private const string keyLockPrefab = "assets/prefabs/locks/keylock/lock.key.prefab";
        private const string codeLockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        private const string effectDenied = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
        private const string effectDeployed = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";
        private const string keylockpermissionName = "minicopterlock.usekeylock";
        private const string codelockpermissionName = "minicopterlock.usecodelock";
        private const string kickpermissionName = "minicopterlock.kick";

        private const int doorkeyItemID = -1112793865;
        private const int keylockItemID = -850982208;
        private const int codelockItemID = 1159991980;

        private enum AllowedLockType { keylock, codelock, both};
        internal enum LockType { Keylock, Codelock, None};
        private enum PayType { Inventory, Resources, Free};

        private CooldownManager cooldownManager;
        #endregion variables

        #region localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission Key"] = "You are not allowed to add keylocks",
                ["No Permission Code"] = "You are not allowed to add codelocks",
                ["No Permission Kick"] = "You are not allowed to use the kick command",
                ["Cannot Afford"] = "You need a lock or the resources to craft one",
                ["Already Has Lock"] = "This minicopter already has a lock",
                ["Not A MiniCopter"] = "This entity is not a minicopter",
                ["Cooldown active"] = "You must wait approximately {0} seconds",
                ["Cannot have passengers"] = "Passengers must dismount first"
            }, this);
        }
        #endregion localization

        #region config
        private ConfigData config;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Sound Effects")]
            public bool SoundEffects = true;

            [JsonProperty(PropertyName = "Locks Are Free")]
            public bool LocksAreFree = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                    throw new Exception();

                SaveConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion config

        #region chatommands
        [ChatCommand("lockmini.key")]
        private void LockWithkeyLock1(BasePlayer player, string command, string[] args)
        {
            LockIt(player, LockType.Keylock);
        }

        [ChatCommand("lockmini.code")]
        private void LockWithCodeLock1(BasePlayer player, string command, string[] args)
        {
            LockIt(player, LockType.Codelock);
        }

        [ChatCommand("lockit.key")]
        private void LockWithkeyLock2(BasePlayer player, string command, string[] args)
        {
            LockIt(player, LockType.Keylock);
        }

        [ChatCommand("lockit.code")]
        private void LockWithCodeLock2(BasePlayer player, string command, string[] args)
        {
            LockIt(player, LockType.Codelock);
        }
        
        [ConsoleCommand("heli.kick")]
        private void KickPassenger(ConsoleSystem.Arg arg)
        {
            BasePlayer basePlayer = arg.Player();
            if (basePlayer == null)
                return;

            if (!permission.UserHasPermission(basePlayer.UserIDString, kickpermissionName))
            {
                basePlayer.ChatMessage(Lang("No Permission Kick", basePlayer.UserIDString));
                return;
            }

            if (basePlayer.isMounted)
            {
                BaseVehicle vehicle = basePlayer.GetMountedVehicle();
                MiniCopter miniCopter = vehicle.GetComponentInParent<MiniCopter>();
                if (miniCopter == null)
                    return;

                if (HasLock(miniCopter) == LockType.None)
                    return;

                if (basePlayer == vehicle.GetDriver())
                {
                    HasAnyAuthorizedMounted(miniCopter, basePlayer, true, true);
                }
            }
        }
        #endregion chatommands

        #region hooks
        private void Init()
        {
            permission.RegisterPermission(keylockpermissionName, this);
            permission.RegisterPermission(codelockpermissionName, this);
            permission.RegisterPermission(kickpermissionName, this);

            cooldownManager = new CooldownManager();
        }

        object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            MiniCopter miniCopter = entity.GetComponentInParent<MiniCopter>();
            if (miniCopter == null)
                return null;

            BaseLock baseLock = miniCopter.GetComponentInChildren<BaseLock>();
            if (baseLock == null)
                return null;

            if (!baseLock.IsLocked())
                return null;

            if (!HasAnyAuthorizedMounted(miniCopter, null, false, false))
            {
                if (PlayerIsAuthorized(player, miniCopter))
                {
                    return null;
                } 
                else
                {
                    if (config.SoundEffects)
                        Effect.server.Run(effectDenied, miniCopter.transform.position);

                    return true;
                }
            }
            
            return null;
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            MiniCopter miniCopter = entity.GetComponentInParent<MiniCopter>();
            if (miniCopter == null)
                return;

            if (HasLock(miniCopter) == LockType.None)
                return;

            if (PlayerIsAuthorized(player, miniCopter))
                HasAnyAuthorizedMounted(miniCopter, player, true, false);
        }

        object CanLock(BasePlayer player, KeyLock keyLock)
        {
            return CheckLock(player, keyLock, true);
        }

        object CanLock(BasePlayer player, CodeLock codeLock)
        {
            MiniCopter miniCopter = (codeLock.GetComponentInParent<MiniCopter>());
            if (miniCopter == null)
                return null;

            if ((miniCopter.HasAnyPassengers()) || (miniCopter.HasDriver()))
                DismountPlayers(miniCopter);

            return null;
        }

        object CanUnlock(BasePlayer player, KeyLock keyLock)
        {
            return CheckLock(player, keyLock, false);
        }

        object CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode)
        {
            MiniCopter miniCopter = (codeLock.GetComponentInParent<MiniCopter>());
            if (miniCopter == null)
                return null;

            if ((miniCopter.HasAnyPassengers()) || (miniCopter.HasDriver()))
                DismountPlayers(miniCopter);

            return null;
        }

        object CanPickupLock(BasePlayer player, BaseLock baseLock)
        {
            if (baseLock.GetComponentInParent<MiniCopter>() != null)
                if (config.LocksAreFree)
                {
                    baseLock.Kill();
                    return false;
                }

            return null;
        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            MiniCopter miniCopter = container.GetComponentInParent<MiniCopter>();
            if (miniCopter == null)
                return null;

            LockType lockType = HasLock(miniCopter);
            if (lockType != LockType.None)
            {
                switch (lockType)
                {
                    case LockType.Keylock:
                        {
                            KeyLock keyLock = miniCopter.GetComponentInChildren<KeyLock>();
                            if (!keyLock.IsLocked())
                                return null;

                            if (PlayerHasTheKey(player, Convert.ToInt32(miniCopter.net.ID)))
                                return null;

                            break;
                        }

                    case LockType.Codelock:
                        {
                            CodeLock codeLock = miniCopter.GetComponentInChildren<CodeLock>();
                            if (!codeLock.IsLocked())
                                return null;

                            if (codeLock.whitelistPlayers.Contains(player.userID))
                                return null;

                            break;
                        }
                }                
                
                if (config.SoundEffects)
                    Effect.server.Run(effectDenied, miniCopter.transform.position);
                
                return false;
            }

            return null;
        }

        object OnVehiclePush(BaseVehicle vehicle, BasePlayer player)
        {
            MiniCopter miniCopter = (vehicle.GetComponentInParent<MiniCopter>());
            if (miniCopter == null)
                return null;

            BaseLock baseLock = miniCopter.GetComponentInChildren<BaseLock>();
            if (baseLock == null)
                return null;

            if (!baseLock.IsLocked())
                return null;

            if (!PlayerIsAuthorized(player, miniCopter))
            {
                if (config.SoundEffects)
                    Effect.server.Run(effectDenied, miniCopter.transform.position);

                return vehicle;
            }

            return null;
        }
        #endregion hooks

        #region methods
        private void LockIt(BasePlayer player, LockType lockType)
        {
            switch (lockType)
            {
                case LockType.Keylock:
                    {
                        if (!permission.UserHasPermission(player.UserIDString, keylockpermissionName))
                        {
                            player.ChatMessage(Lang("No Permission Key", player.UserIDString));
                            return;
                        }
                        break;
                    }

                case LockType.Codelock:
                    {
                        if (!permission.UserHasPermission(player.UserIDString, codelockpermissionName))
                        {
                            player.ChatMessage(Lang("No Permission Code", player.UserIDString));
                            return;
                        }
                        break;
                    }
            }
            
            RaycastHit hit;
            if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
                return;

            BaseEntity minicopterEntity = hit.GetEntity();
            if (minicopterEntity is MiniCopter)
            {
                MiniCopter miniCopter = minicopterEntity.GetComponentInChildren<MiniCopter>();

                if ((miniCopter.HasAnyPassengers()) || (miniCopter.HasDriver()))
                {
                    player.ChatMessage(Lang("Cannot have passengers", player.UserIDString));
                    return;
                }
                
                if (HasLock(miniCopter) != LockType.None)
                {
                    player.ChatMessage(Lang("Already Has Lock", player.UserIDString));
                    return;
                }

                if (config.LocksAreFree == false)
                {
                    float secondsRemaining = 0;
                    if (PlayerHasCooldown(player.UserIDString, lockType, out secondsRemaining))
                    {
                        player.ChatMessage(Lang("Cooldown active", player.UserIDString, Math.Round(secondsRemaining).ToString()));
                        return;
                    }
                }

                PayType payType;
                if (CanAffordLock(player, lockType, out payType))
                {
                    if (lockType == LockType.Keylock)
                        AddKeylock(hit.GetEntity().GetComponent<MiniCopter>(), player);
                    else
                        AddCodelock(hit.GetEntity().GetComponent<MiniCopter>(), player, config.LocksAreFree);

                    PayForlock(player, lockType, payType);

                    cooldownManager.UpdateLastUsedForPlayer(player.UserIDString, lockType);
                }
                else
                    player.ChatMessage(Lang("Cannot Afford", player.UserIDString));
            }
            else
            {
                player.ChatMessage(Lang("Not A MiniCopter", player.UserIDString));
            }
        }

        private LockType HasLock(MiniCopter miniCopter)
        {
            if (miniCopter.GetComponentInChildren<KeyLock>())
                return LockType.Keylock;

            if (miniCopter.GetComponentInChildren<CodeLock>())
                return LockType.Codelock;

            return LockType.None;
        }

        private bool CanAffordLock(BasePlayer player, LockType lockType, out PayType payType)
        {
            payType = PayType.Inventory;

            if (config.LocksAreFree)
            {
                payType = PayType.Free;
                return true;
            }
                
            int itemID = 0;

            switch (lockType)
            {
                case LockType.Keylock:
                    itemID = keylockItemID;
                    break;

                case LockType.Codelock:
                    itemID = codelockItemID;
                    break;
            }

            if ((uint)player.inventory.GetAmount(itemID) >= 1)
            {
                payType = PayType.Inventory;
                return true;
            }
            
            if (player.inventory.crafting.CanCraft(ItemManager.FindBlueprint(ItemManager.FindItemDefinition(itemID)), 1, false))
            {
                payType = PayType.Resources;
                return true;
            }

            return false;
        }

        private void PayForlock(BasePlayer player, LockType lockType, PayType payType)
        {
            if (payType == PayType.Free)
                return;

            int itemID = keylockItemID;
            if (lockType == LockType.Codelock)
                itemID = codelockItemID;

            if (payType == PayType.Inventory)
            {
                player.inventory.Take(new List<Item>(), itemID, 1);
            }
            else
            {
                List<Item> items = new List<Item>();
                foreach (ItemAmount ingredient in ItemManager.FindBlueprint(ItemManager.FindItemDefinition(itemID)).ingredients)
                {
                    player.inventory.Take(items, ingredient.itemid, (int)ingredient.amount);
                    player.Command("note.inv", new object[] { itemID, ((int)ingredient.amount * -1f )});
                }
            }
        }
        
        private void AddKeylock(MiniCopter miniCopter, BasePlayer player)
        {
            BaseEntity ent = GameManager.server.CreateEntity(keyLockPrefab, miniCopter.transform.position);
            if (!ent)
                return;

            ent.Spawn();
            ent.SetParent(miniCopter);
            ent.transform.localEulerAngles = new Vector3(0, 180, 0);
            ent.transform.localPosition = new Vector3(0.27f, 0.67f, 0.1f);

            if (miniCopter.GetComponentInChildren<ScrapTransportHelicopter>() != null)
            {
                ent.transform.localEulerAngles = new Vector3(0, 0, 0);
                ent.transform.localPosition = new Vector3(-1.31f, 1.28f, 1.74f);
            }

            KeyLock keylock = ent.GetComponent<KeyLock>();
            keylock.keyCode = Convert.ToInt32(miniCopter.net.ID);
            keylock.OwnerID = player.userID;
            keylock.enableSaving = true;
            miniCopter.SetSlot(BaseEntity.Slot.Lock, ent);

            ent.SendNetworkUpdateImmediate();
            if (config.SoundEffects)
                Effect.server.Run(effectDeployed, ent.transform.position);
        }

        private void AddCodelock(MiniCopter miniCopter, BasePlayer player, bool isfree)
        {
            BaseEntity ent = GameManager.server.CreateEntity(codeLockPrefab, miniCopter.transform.position);
            if (!ent)
                return;

            ent.Spawn();
            ent.SetParent(miniCopter);
            ent.transform.localEulerAngles = new Vector3(0, 180, 0);
            ent.transform.localPosition = new Vector3(0.27f, 0.67f, 0.1f);

            if (miniCopter.GetComponentInChildren<ScrapTransportHelicopter>() != null) 
            {
                ent.transform.localEulerAngles = new Vector3(0, 0, 0);
                ent.transform.localPosition = new Vector3(-1.25f, 1.22f, 1.99f);   
            }

            CodeLock codelock = ent.GetComponent<CodeLock>();
            codelock.OwnerID = 0;
            codelock.enableSaving = true;
            miniCopter.SetSlot(BaseEntity.Slot.Lock, ent);

            ent.SendNetworkUpdateImmediate();
            if (config.SoundEffects)
                Effect.server.Run(effectDeployed, ent.transform.position);
        }

        private object CheckLock(BasePlayer player, KeyLock keyLock, bool forLocking)
        {
            MiniCopter miniCopter = (keyLock.GetComponentInParent<MiniCopter>());
            if (miniCopter == null)
                return null;

            if (forLocking)
            {
                if ((miniCopter.HasAnyPassengers()) || (miniCopter.HasDriver()))
                    DismountPlayers(miniCopter);
            }

            if (PlayerHasTheKey(player, Convert.ToInt32(miniCopter.net.ID)))
                return null;

            if (config.SoundEffects)
                Effect.server.Run(effectDenied, keyLock.transform.position);

            return false;
        }

        private bool HasAnyAuthorizedMounted(MiniCopter miniCopter, BasePlayer dismounted, bool kick, bool hardkick)
        {
            List<BaseVehicle.MountPointInfo>.Enumerator enumerator = miniCopter.mountPoints.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    BaseVehicle.MountPointInfo current = enumerator.Current;
                    if (!(current.mountable != null))
                    {
                        continue;
                    } 
                    else
                    {
                        BasePlayer player = current.mountable.GetMounted();
                        if (player == null)
                            continue;
                        else
                        {
                            if (player == dismounted)
                            {
                                continue;
                            }
                            else
                            {
                                if (hardkick)
                                {
                                    miniCopter.GetComponent<BaseMountable>().DismountPlayer(player);
                                    player.EnsureDismounted();
                                    continue;
                                }

                                if (!PlayerIsAuthorized(player, miniCopter))
                                {
                                    if (kick)
                                    {
                                        miniCopter.GetComponent<BaseMountable>().DismountPlayer(player);
                                        player.EnsureDismounted();
                                    }
                                }
                                else
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
        }

        private bool PlayerIsAuthorized(BasePlayer player, MiniCopter miniCopter)
        {
            LockType lockType = HasLock(miniCopter);

            switch (lockType)
            {
                case LockType.Keylock:
                    return (PlayerHasTheKey(player, Convert.ToInt32(miniCopter.net.ID)));

                case LockType.Codelock:
                    return (miniCopter.GetComponentInChildren<CodeLock>().whitelistPlayers.Contains(player.userID) || (miniCopter.GetComponentInChildren<CodeLock>().guestPlayers.Contains(player.userID)));
            }

            return true;
        }

        private bool PlayerHasTheKey(BasePlayer player, int keyCode)
        {
            foreach (Item item in player.inventory.containerMain.itemList)   
            {
                if (IsMatchingKey(item, keyCode))
                    return true;
            }
            
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (IsMatchingKey(item, keyCode))
                    return true;
            }
            
            return false;
        }

        private bool IsMatchingKey(Item item, int keyCode)
        {
            if (item.info.itemid == doorkeyItemID)
            {
                if (item.instanceData.dataInt == keyCode)
                    return true;
            }

            return false;
        }

        private void DismountPlayers(MiniCopter miniCopter)
        {
            List<BaseVehicle.MountPointInfo>.Enumerator enumerator = miniCopter.mountPoints.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    BaseVehicle.MountPointInfo current = enumerator.Current;
                    if (!(current.mountable != null))
                    {
                        continue;
                    }
                    else
                    {
                        BasePlayer player = current.mountable.GetMounted();
                        if (player == null)
                            continue;
                        else
                        {
                            miniCopter.GetComponent<BaseMountable>().DismountPlayer(player);
                            player.EnsureDismounted();
                        }
                    }
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
        }
        #endregion methods

        #region cooldown
        internal class CooldownManager
        {
            private readonly Dictionary<string, CooldownInfo> Cooldowns = new Dictionary<string, CooldownInfo>();

            public CooldownManager()
            {
                
            }

            private class CooldownInfo
            {
                public float CraftTime = Time.realtimeSinceStartup;
                public float CoolDown = 0;

                public CooldownInfo(float craftTime, float duration)
                {
                    CraftTime = craftTime;
                    CoolDown = duration;
                }
            }

            public void UpdateLastUsedForPlayer(string userID, LockType lockType)
            {
                string key = userID + "-" + lockType.ToString();
                float duration = 0;

                switch(lockType)
                {
                    case LockType.Keylock:
                        {
                            duration = ItemManager.FindBlueprint(ItemManager.FindItemDefinition(keylockItemID)).time;
                            break;
                        }

                    case LockType.Codelock:
                        {
                            duration = ItemManager.FindBlueprint(ItemManager.FindItemDefinition(codelockItemID)).time;
                            break;
                        }
                }
                
                if (Cooldowns.ContainsKey(key))
                {
                    Cooldowns[key].CraftTime = Time.realtimeSinceStartup;
                    Cooldowns[key].CoolDown = 10;
                }
                else
                {
                    CooldownInfo info = new CooldownInfo(Time.realtimeSinceStartup, duration);
                    Cooldowns.Add(key, info);
                }
            }

            public float GetSecondsRemaining(string userID, LockType lockType)
            {
                string key = userID + "-" + lockType.ToString();

                if (!Cooldowns.ContainsKey(key))
                    return 0;

                return Cooldowns[key].CraftTime + Cooldowns[key].CoolDown - Time.realtimeSinceStartup;
            }
        }

        private bool PlayerHasCooldown(string userID, LockType lockType, out float secondsRemaining)
        {
            secondsRemaining = (float) Math.Round(cooldownManager.GetSecondsRemaining(userID,lockType));
            
            if (secondsRemaining <= 0) 
                return false;
            
            return true;
        }
        #endregion cooldown

        #region helpers
        private static BasePlayer FindBasePlayer(Vector3 pos)
        {
            RaycastHit[] hits = UnityEngine.Physics.SphereCastAll(pos, 4f, Vector3.up);
            return (from hit in hits where hit.GetEntity()?.GetComponent<BasePlayer>() select hit.GetEntity()?.GetComponent<BasePlayer>()).FirstOrDefault();
        }

        private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);

        private LockType GetLockType(string allowedLockType)
        {
            if (allowedLockType.ToLower() == AllowedLockType.keylock.ToString().ToLower())
                return LockType.Keylock;
            else
                return LockType.Codelock;
        }
        
        private MiniCopter GetMiniCopter(BasePlayer player)
        {
            RaycastHit hit;
            if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
                return null;

            BaseEntity baseEntity = hit.GetEntity();
            if (baseEntity is MiniCopter)
            {
                return (baseEntity.GetComponentInParent<MiniCopter>());
            }
            else
            {
                player.ChatMessage(Lang("Not A MiniCopter", player.UserIDString));
                return null;
            }
        }
        #endregion helpers
    }
}
