using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vehicle Deployed Locks", "WhiteThunder", "1.0.0")]
    [Description("Allows players to deploy code locks and key locks to vehicles.")]
    internal class VehicleDeployedLocks : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Clans, Friends;

        private VehicleLocksConfig PluginConfig;

        private const string Permission_CodeLock_Free = "vehicledeployedlocks.codelock.free";
        private const string Permission_CodeLock_AllVehicles = "vehicledeployedlocks.codelock.allvehicles";
        private const string Permission_CodeLock_Chinook = "vehicledeployedlocks.codelock.chinook";
        private const string Permission_CodeLock_HotAirBalloon = "vehicledeployedlocks.codelock.hotairballoon";
        private const string Permission_CodeLock_Kayak = "vehicledeployedlocks.codelock.kayak";
        private const string Permission_CodeLock_MiniCopter = "vehicledeployedlocks.codelock.minicopter";
        private const string Permission_CodeLock_ModularCar = "vehicledeployedlocks.codelock.modularcar";
        private const string Permission_CodeLock_RHIB = "vehicledeployedlocks.codelock.rhib";
        private const string Permission_CodeLock_RidableHorse = "vehicledeployedlocks.codelock.ridablehorse";
        private const string Permission_CodeLock_Rowboat = "vehicledeployedlocks.codelock.rowboat";
        private const string Permission_CodeLock_ScrapHeli = "vehicledeployedlocks.codelock.scraptransport";
        private const string Permission_CodeLock_Sedan = "vehicledeployedlocks.codelock.sedan";

        private const string Permission_KeyLock_Free = "vehicledeployedlocks.keylock.free";
        private const string Permission_KeyLock_AllVehicles = "vehicledeployedlocks.keylock.allvehicles";
        private const string Permission_KeyLock_Chinook = "vehicledeployedlocks.keylock.chinook";
        private const string Permission_KeyLock_HotAirBalloon = "vehicledeployedlocks.keylock.hotairballoon";
        private const string Permission_KeyLock_Kayak = "vehicledeployedlocks.keylock.kayak";
        private const string Permission_KeyLock_MiniCopter = "vehicledeployedlocks.keylock.minicopter";
        private const string Permission_KeyLock_ModularCar = "vehicledeployedlocks.keylock.modularcar";
        private const string Permission_KeyLock_RHIB = "vehicledeployedlocks.keylock.rhib";
        private const string Permission_KeyLock_RidableHorse = "vehicledeployedlocks.keylock.ridablehorse";
        private const string Permission_KeyLock_Rowboat = "vehicledeployedlocks.keylock.rowboat";
        private const string Permission_KeyLock_ScrapHeli = "vehicledeployedlocks.keylock.scraptransport";
        private const string Permission_KeyLock_Sedan = "vehicledeployedlocks.keylock.sedan";

        private const string CodeLockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        private const string KeyLockPrefab = "assets/prefabs/locks/keylock/lock.key.prefab";

        private const string Prefab_CodeLock_DeployedEffect = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";
        private const string Prefab_CodeLock_DeniedEffect = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
        private const string Prefab_CodeLock_UnlockEffect = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";

        private const string RidableHorseParentBone = "Horse_RootBone";

        private const int CodeLockItemId = 1159991980;
        private const int KeyLockItemId = -850982208;

        private const float MaxDeployDistance = 3;

        private readonly Vector3 LockPosition_Chinook = new Vector3(-1.175f, 2, 6.5f);
        private readonly Vector3 LockPosition_HotAirBalloon = new Vector3(1.45f, 0.9f, 0);
        private readonly Vector3 LockPosition_Kayak = new Vector3(-0.43f, 0.2f, 0.2f);
        private readonly Vector3 LockPosition_MiniCopter = new Vector3(-0.15f, 0.7f, -0.1f);
        private readonly Vector3 LockPosition_VehicleModule = new Vector3(-0.9f, 0.35f, -0.5f);
        private readonly Vector3 LockPosition_RHIB = new Vector3(-0.68f, 2.00f, 0.7f);
        private readonly Vector3 LockPosition_RidableHorse = new Vector3(-0.6f, 0.35f, -0.1f);
        private readonly Vector3 LockPosition_RowBoat = new Vector3(-0.83f, 0.51f, -0.57f);
        private readonly Vector3 LockPosition_ScrapHeli = new Vector3(-1.25f, 1.22f, 1.99f);
        private readonly Vector3 LockPosition_Sedan = new Vector3(-1.09f, 0.79f, 0.5f);

        private readonly Quaternion LockRotation_Kayak = Quaternion.Euler(0, 90, 90);
        private readonly Quaternion LockRotation_RidableHorse = Quaternion.Euler(0, 95, 90);

        private CooldownManager CraftCodeLockCooldowns;
        private CooldownManager CraftKeyLockCooldowns;

        internal enum LockType { CodeLock, KeyLock }
        internal enum PayType { Item, Resources, Free }

        #endregion

        #region Hooks

        private void Init()
        {
            PluginConfig = Config.ReadObject<VehicleLocksConfig>();

            permission.RegisterPermission(Permission_CodeLock_Free, this);
            permission.RegisterPermission(Permission_CodeLock_AllVehicles, this);
            permission.RegisterPermission(Permission_CodeLock_Chinook, this);
            permission.RegisterPermission(Permission_CodeLock_HotAirBalloon, this);
            permission.RegisterPermission(Permission_CodeLock_Kayak, this);
            permission.RegisterPermission(Permission_CodeLock_MiniCopter, this);
            permission.RegisterPermission(Permission_CodeLock_ModularCar, this);
            permission.RegisterPermission(Permission_CodeLock_RHIB, this);
            permission.RegisterPermission(Permission_CodeLock_RidableHorse, this);
            permission.RegisterPermission(Permission_CodeLock_Rowboat, this);
            permission.RegisterPermission(Permission_CodeLock_ScrapHeli, this);
            permission.RegisterPermission(Permission_CodeLock_Sedan, this);

            permission.RegisterPermission(Permission_KeyLock_Free, this);
            permission.RegisterPermission(Permission_KeyLock_AllVehicles, this);
            permission.RegisterPermission(Permission_KeyLock_Chinook, this);
            permission.RegisterPermission(Permission_KeyLock_HotAirBalloon, this);
            permission.RegisterPermission(Permission_KeyLock_Kayak, this);
            permission.RegisterPermission(Permission_KeyLock_MiniCopter, this);
            permission.RegisterPermission(Permission_KeyLock_ModularCar, this);
            permission.RegisterPermission(Permission_KeyLock_RHIB, this);
            permission.RegisterPermission(Permission_KeyLock_RidableHorse, this);
            permission.RegisterPermission(Permission_KeyLock_Rowboat, this);
            permission.RegisterPermission(Permission_KeyLock_ScrapHeli, this);
            permission.RegisterPermission(Permission_KeyLock_Sedan, this);

            CraftKeyLockCooldowns = new CooldownManager(PluginConfig.CraftCooldownSeconds);
            CraftCodeLockCooldowns = new CooldownManager(PluginConfig.CraftCooldownSeconds);
        }

        private object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            // Don't lock taxi modules
            var carSeat = entity as ModularCarSeat;
            if (carSeat != null && !carSeat.associatedSeatingModule.DoorsAreLockable)
                return null;

            return CanPlayerInteractWithParentVehicle(player, entity);
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            // Don't lock taxi module shop fronts
            if (container is ModularVehicleShopFront)
                return null;

            return CanPlayerInteractWithParentVehicle(player, container);
        }

        private object CanLootEntity(BasePlayer player, ContainerIOEntity container) =>
            CanPlayerInteractWithParentVehicle(player, container);

        private object CanLootEntity(BasePlayer player, RidableHorse horse) =>
            CanPlayerInteractWithVehicle(player, horse);

        private object CanLootEntity(BasePlayer player, ModularCarGarage carLift)
        {
            if (carLift == null
                || PluginConfig.ModularCarSettings.AllowEditingWhileLockedOut
                || !carLift.PlatformIsOccupied)
                return null;

            return CanPlayerInteractWithVehicle(player, carLift.carOccupant);
        }

        private object OnHorseLead(RidableHorse horse, BasePlayer player) =>
            CanPlayerInteractWithVehicle(player, horse);

        private object OnHotAirBalloonToggle(HotAirBalloon hab, BasePlayer player) =>
            CanPlayerInteractWithVehicle(player, hab);

        private object OnSwitchToggle(ElectricSwitch electricSwitch, BasePlayer player)
        {
            if (electricSwitch == null)
                return null;

            var autoTurret = electricSwitch.GetParentEntity() as AutoTurret;
            if (autoTurret != null)
                return CanPlayerInteractWithParentVehicle(player, autoTurret);

            return null;
        }

        private object OnTurretAuthorize(AutoTurret entity, BasePlayer player) =>
            CanPlayerInteractWithParentVehicle(player, entity);

        private object OnTurretTarget(AutoTurret autoTurret, BasePlayer player)
        {
            if (autoTurret == null || player == null)
                return null;

            var turretParent = autoTurret.GetParentEntity();
            var vehicle = turretParent as BaseVehicle ?? (turretParent as BaseVehicleModule)?.Vehicle;
            if (vehicle == null)
                return null;

            var baseLock = GetVehicleLock(vehicle);
            if (baseLock == null)
                return null;

            if (CanPlayerBypassLock(player, baseLock))
                return false;

            return null;
        }

        private object CanSwapToSeat(BasePlayer player, ModularCarSeat carSeat)
        {
            // Don't lock taxi modules
            if (!carSeat.associatedSeatingModule.DoorsAreLockable)
                return null;

            return CanPlayerInteractWithParentVehicle(player, carSeat, provideFeedback: false);
        }

        private object CanPlayerInteractWithParentVehicle(BasePlayer player, BaseEntity entity, bool provideFeedback = true) =>
            CanPlayerInteractWithVehicle(player, GetParentVehicle(entity), provideFeedback);

        private object CanPlayerInteractWithVehicle(BasePlayer player, BaseCombatEntity vehicle, bool provideFeedback = true)
        {
            if (player == null || vehicle == null)
                return null;

            var baseLock = GetVehicleLock(vehicle);
            if (baseLock == null || !baseLock.IsLocked())
                return null;

            if (!CanPlayerBypassLock(player, baseLock))
            {
                if (provideFeedback)
                {
                    Effect.server.Run(Prefab_CodeLock_DeniedEffect, baseLock, 0, Vector3.zero, Vector3.forward);
                    ChatMessage(player, "Generic.Error.VehicleLocked");
                }

                return false;
            }

            if (provideFeedback && baseLock.IsLocked())
                Effect.server.Run(Prefab_CodeLock_UnlockEffect, baseLock, 0, Vector3.zero, Vector3.forward);

            return null;
        }

        // Handle the case where a cockpit is removed but the car remains
        // If a lock is present, either move the lock to another cockpit or destroy it
        private void OnEntityKill(VehicleModuleSeating seatingModule)
        {
            if (seatingModule == null || !seatingModule.HasADriverSeat())
                return;

            var car = seatingModule.Vehicle as ModularCar;
            if (car == null)
                return;

            var baseLock = seatingModule.GetComponentInChildren<BaseLock>();
            if (baseLock == null)
                return;

            baseLock.SetParent(null);
            NextTick(() =>
            {
                if (car == null)
                {
                    baseLock.Kill();
                }
                else
                {
                    var driverModule = FindFirstDriverModule(car);
                    if (driverModule == null)
                        baseLock.Kill();
                    else
                        baseLock.SetParent(driverModule);
                }
            });
        }

        // Allow players to deploy locks directly without any commands.
        object CanDeployItem(BasePlayer basePlayer, Deployer deployer, uint entityId)
        {
            if (basePlayer == null || deployer == null)
                return null;

            var deployable = deployer.GetDeployable();
            if (deployable == null)
                return null;

            var activeItem = basePlayer.GetActiveItem();
            if (activeItem == null)
                return null;

            var itemid = activeItem.info.itemid;

            LockType lockType;
            if (itemid == CodeLockItemId)
                lockType = LockType.CodeLock;
            else if (itemid == KeyLockItemId)
                lockType = LockType.KeyLock;
            else
                return null;

            var vehicle = GetVehicleFromEntity(BaseNetworkable.serverEntities.Find(entityId) as BaseEntity, basePlayer);
            if (vehicle == null)
                return null;

            string perm = null;
            Vector3 lockPosition = Vector3.zero;

            if (!IsSupportedVehicle(vehicle, lockType, ref perm, ref lockPosition))
                return null;

            var player = basePlayer.IPlayer;

            // Trick to make sure the replies are in chat instead of console.
            player.LastCommand = CommandType.Chat;
            PayType payType;
            if (!VerifyCanDeploy(player, vehicle, lockType, perm, out payType)
                || !VerifyDeployDistance(player, vehicle))
                return false;

            DeployLockForPlayer(vehicle, lockPosition, lockType, basePlayer, payType);
            return false;
        }

        #endregion

        #region API

        private CodeLock API_DeployCodeLock(BaseVehicle vehicle, BasePlayer player, bool isFree = true) =>
            DeployLockForAPI(vehicle, player, LockType.CodeLock, isFree) as CodeLock;

        private KeyLock API_DeployKeyLock(BaseVehicle vehicle, BasePlayer player, bool isFree = true) =>
            DeployLockForAPI(vehicle, player, LockType.KeyLock, isFree) as KeyLock;

        private bool API_CanPlayerDeployCodeLock(BasePlayer player, BaseVehicle vehicle) =>
            CanPlayerDeployLockForAPI(player, vehicle, LockType.CodeLock);

        private bool API_CanPlayerDeployKeyLock(BasePlayer player, BaseVehicle vehicle) =>
            CanPlayerDeployLockForAPI(player, vehicle, LockType.KeyLock);

        private bool API_CanAccessVehicle(BasePlayer player, BaseVehicle vehicle, bool provideFeedback = true) =>
            CanPlayerInteractWithVehicle(player, vehicle, provideFeedback) == null;

        #endregion

        #region Commands

        [Command("vehiclecodelock", "vcodelock", "vlock")]
        private void CodeLockCommand(IPlayer player, string cmd, string[] args) =>
            LockCommand(player, LockType.CodeLock);

        [Command("vehiclekeylock", "vkeylock")]
        private void KeyLockCommand(IPlayer player, string cmd, string[] args) =>
            LockCommand(player, LockType.KeyLock);

        private void LockCommand(IPlayer player, LockType lockType)
        {
            if (player.IsServer)
                return;

            var basePlayer = player.Object as BasePlayer;
            var vehicle = GetVehicleFromEntity(GetLookEntity(basePlayer, MaxDeployDistance), basePlayer);

            string perm = null;
            Vector3 lockPosition = Vector3.zero;

            if (vehicle == null || !IsSupportedVehicle(vehicle, lockType, ref perm, ref lockPosition))
            {
                ReplyToPlayer(player, "Deploy.Error.NoVehicleFound");
                return;
            }

            PayType payType;
            if (!VerifyCanDeploy(player, vehicle, lockType, perm, out payType))
                return;

            DeployLockForPlayer(vehicle, lockPosition, lockType, basePlayer, payType);
        }

        #endregion

        #region Helper Methods

        private BaseLock DeployLockForAPI(BaseVehicle vehicle, BasePlayer player, LockType lockType, bool isFree)
        {
            string perm = null;
            Vector3 lockPosition = Vector3.zero;

            if (vehicle == null
                || !IsSupportedVehicle(vehicle, lockType, ref perm, ref lockPosition)
                || vehicle.IsDead()
                || GetVehicleLock(vehicle) != null
                || !CanVehicleHaveALock(vehicle))
                return null;

            PayType payType;
            if (isFree)
                payType = PayType.Free;
            else if (!VerifyPlayerCanDeployLock(player.IPlayer, lockType, out payType))
                return null;

            if (DeployWasBlocked(vehicle, player, lockType))
                return null;

            return player != null
                ? DeployLockForPlayer(vehicle, lockPosition, lockType, player, payType)
                : DeployLock(vehicle, lockPosition, lockType);
        }

        private bool CanPlayerDeployLockForAPI(BasePlayer player, BaseVehicle vehicle, LockType lockType)
        {
            PayType payType;
            string perm = null;
            Vector3 lockPosition = Vector3.zero;

            return vehicle != null &&
                IsSupportedVehicle(vehicle, lockType, ref perm, ref lockPosition) &&
                !vehicle.IsDead() &&
                AllowedNoOwner(vehicle) &&
                AllowedDifferentOwner(player.IPlayer, vehicle) &&
                player.CanBuild() &&
                GetVehicleLock(vehicle) == null &&
                CanVehicleHaveALock(vehicle) &&
                CanPlayerAffordLock(player, lockType, out payType) &&
                !DeployWasBlocked(vehicle, player, lockType);
        }

        private bool DeployWasBlocked(BaseCombatEntity vehicle, BasePlayer player, LockType lockType)
        {
            var hookName = lockType == LockType.CodeLock ? "CanDeployVehicleCodeLock" : "CanDeployVehicleKeyLock";
            object hookResult = Interface.CallHook(hookName, vehicle, player);
            return hookResult is bool && (bool)hookResult == false;
        }

        private bool VerifyCanDeploy(IPlayer player, BaseCombatEntity vehicle, LockType lockType, string perm, out PayType payType)
        {
            var basePlayer = player.Object as BasePlayer;
            payType = PayType.Item;

            return VerifyPermissionToVehicleAndLockType(player, lockType, perm)
                && VerifyVehicleIsNotDead(player, vehicle)
                && VerifyNoOwnershipRestriction(player, vehicle)
                && VerifyCanBuild(player, vehicle)
                && VerifyVehicleHasNoLock(player, vehicle)
                && VerifyVehicleCanHaveALock(player, vehicle)
                && VerifyPlayerCanDeployLock(player, lockType, out payType)
                && (!(vehicle is BaseVehicle) || VerifyNotMounted(player, vehicle as BaseVehicle))
                && !DeployWasBlocked(vehicle, basePlayer, lockType);
        }

        private bool VerifyDeployDistance(IPlayer player, BaseCombatEntity vehicle)
        {
            if (vehicle.Distance(player.Object as BasePlayer) <= MaxDeployDistance)
                return true;

            ReplyToPlayer(player, "Deploy.Error.Distance");
            return false;
        }

        private bool VerifyPermissionToVehicleAndLockType(IPlayer player, LockType lockType, string vehicleSpecificPerm)
        {
            var allVehiclesPerm = lockType == LockType.CodeLock ? Permission_CodeLock_AllVehicles : Permission_KeyLock_AllVehicles;
            if (vehicleSpecificPerm != null && HasPermissionAny(player, allVehiclesPerm, vehicleSpecificPerm))
                return true;

            ReplyToPlayer(player, "Generic.Error.NoPermission");
            return false;
        }

        private bool VerifyVehicleIsNotDead(IPlayer player, BaseCombatEntity vehicle)
        {
            if (!vehicle.IsDead())
                return true;

            ReplyToPlayer(player, "Deploy.Error.VehicleDead");
            return false;
        }

        private bool VerifyNoOwnershipRestriction(IPlayer player, BaseCombatEntity vehicle)
        {
            if (!AllowedNoOwner(vehicle))
            {
                ReplyToPlayer(player, "Deploy.Error.NoOwner");
                return false;
            }

            if (!AllowedDifferentOwner(player, vehicle))
            {
                ReplyToPlayer(player, "Deploy.Error.DifferentOwner");
                return false;
            }

            return true;
        }

        private bool VerifyCanBuild(IPlayer player, BaseCombatEntity vehicle)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer.CanBuild() && basePlayer.CanBuild(vehicle.WorldSpaceBounds()))
                return true;

            ReplyToPlayer(player, "Generic.Error.BuildingBlocked");
            return false;
        }

        private bool VerifyVehicleHasNoLock(IPlayer player, BaseCombatEntity vehicle)
        {
            if (GetVehicleLock(vehicle) == null)
                return true;

            ReplyToPlayer(player, "Deploy.Error.HasLock");
            return false;
        }

        private bool VerifyVehicleCanHaveALock(IPlayer player, BaseCombatEntity vehicle)
        {
            if (CanVehicleHaveALock(vehicle))
                return true;

            ReplyToPlayer(player, "Deploy.Error.ModularCar.NoCockpit");
            return false;
        }

        private bool VerifyPlayerCanDeployLock(IPlayer player, LockType lockType, out PayType payType) =>
            VerifyPlayerCanAffordLock(player.Object as BasePlayer, lockType, out payType) && VerifyOffCooldown(player, lockType, payType);

        private bool VerifyPlayerCanAffordLock(BasePlayer player, LockType lockType, out PayType payType)
        {
            if (CanPlayerAffordLock(player, lockType, out payType))
                return true;

            var itemDefinition = GetLockItemDefinition(lockType);
            ChatMessage(player, "Deploy.Error.InsufficientResources", itemDefinition.displayName.translated);
            return false;
        }

        private bool VerifyOffCooldown(IPlayer player, LockType lockType, PayType payType)
        {
            if (payType != PayType.Resources)
                return true;

            var cooldownManager = lockType == LockType.CodeLock ? CraftCodeLockCooldowns : CraftKeyLockCooldowns;

            var secondsRemaining = cooldownManager.GetSecondsRemaining(player.Id);
            if (secondsRemaining <= 0)
                return true;

            ChatMessage(player.Object as BasePlayer, "Generic.Error.Cooldown", Math.Ceiling(secondsRemaining));
            return false;
        }

        private bool VerifyNotMounted(IPlayer player, BaseVehicle vehicle)
        {
            if (!vehicle.AnyMounted())
                return true;

            ReplyToPlayer(player, "Deploy.Error.Mounted");
            return false;
        }

        private BaseEntity GetLookEntity(BasePlayer player, float maxDistance)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return null;

            return hit.GetEntity();
        }

        private bool HasPermissionAny(IPlayer player, params string[] permissionNames)
        {
            foreach (var perm in permissionNames)
                if (player.HasPermission(perm))
                    return true;

            return false;
        }

        private string GetFreeLockPermission(LockType lockType) =>
            lockType == LockType.CodeLock ? Permission_CodeLock_Free : Permission_KeyLock_Free;

        private bool AllowedNoOwner(BaseCombatEntity vehicle) =>
            PluginConfig.AllowIfNoOwner
            || vehicle.OwnerID != 0;

        private bool AllowedDifferentOwner(IPlayer player, BaseCombatEntity vehicle) =>
            PluginConfig.AllowIfDifferentOwner
            || vehicle.OwnerID == 0
            || vehicle.OwnerID.ToString() == player.Id;

        private PayType DeterminePayType(IPlayer player, LockType lockType)
        {
            if (player.HasPermission(GetFreeLockPermission(lockType)))
                return PayType.Free;

            return GetPlayerLock(player.Object as BasePlayer, lockType) != null ? PayType.Item : PayType.Resources;
        }

        private bool CanVehicleHaveALock(BaseCombatEntity vehicle)
        {
            // Only modular cars have restrictions
            var car = vehicle as ModularCar;
            return car == null || CanCarHaveLock(car);
        }

        private bool CanPlayerAffordLock(BasePlayer player, LockType lockType, out PayType payType)
        {
            payType = DeterminePayType(player.IPlayer, lockType);
            if (payType != PayType.Resources)
                return true;

            var itemDefinition = GetLockItemDefinition(lockType);
            return player.inventory.crafting.CanCraft(itemDefinition, 1);
        }

        private int GetLockItemId(LockType lockType) =>
            lockType == LockType.CodeLock ? CodeLockItemId : KeyLockItemId;

        private ItemDefinition GetLockItemDefinition(LockType lockType) =>
            ItemManager.FindItemDefinition(GetLockItemId(lockType));

        private ItemBlueprint GetLockBlueprint(LockType lockType) =>
            ItemManager.FindBlueprint(GetLockItemDefinition(lockType));

        private Item GetPlayerLock(BasePlayer player, LockType lockType) =>
            player.inventory.FindItemID(GetLockItemId(lockType));

        private bool CanCarHaveLock(ModularCar car) =>
            FindFirstDriverModule(car) != null;

        private BaseCombatEntity GetParentVehicle(BaseEntity entity)
        {
            var parent = entity.GetParentEntity();
            if (parent is HotAirBalloon || parent is BaseVehicle)
                return parent as BaseCombatEntity;

            return (parent as BaseVehicleModule)?.Vehicle;
        }

        private VehicleModuleSeating FindFirstDriverModule(ModularCar car)
        {
            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                BaseVehicleModule module;
                if (car.TryGetModuleAt(socketIndex, out module))
                {
                    var seatingModule = module as VehicleModuleSeating;
                    if (seatingModule != null && seatingModule.HasADriverSeat())
                        return seatingModule;
                }
            }
            return null;
        }

        private BaseLock DeployLockForPlayer(BaseEntity vehicle, Vector3 lockPosition, LockType lockType, BasePlayer player, PayType payType)
        {
            var baseLock = DeployLock(vehicle, lockPosition, lockType, player.userID);
            if (baseLock == null)
                return null;

            // Allow other plugins to detect the code lock being deployed (e.g., auto lock)
            var lockItem = GetPlayerLock(player, lockType);
            if (lockItem != null)
                Interface.CallHook("OnItemDeployed", lockItem.GetHeldEntity(), vehicle);
            else
            {
                // Temporarily increase the player inventory capacity to ensure there is enough space
                player.inventory.containerMain.capacity++;
                var temporaryLockItem = ItemManager.CreateByItemID(CodeLockItemId);
                if (player.inventory.GiveItem(temporaryLockItem))
                {
                    Interface.CallHook("OnItemDeployed", temporaryLockItem.GetHeldEntity(), vehicle);
                    temporaryLockItem.RemoveFromContainer();
                }
                temporaryLockItem.Remove();
                player.inventory.containerMain.capacity--;
            }

            MaybeChargePlayerForLock(player, lockType, payType);
            return baseLock;
        }

        private void MaybeChargePlayerForLock(BasePlayer player, LockType lockType, PayType payType)
        {
            if (payType == PayType.Free)
                return;

            if (payType == PayType.Item)
            {
                var lockItemId = lockType == LockType.CodeLock ? CodeLockItemId : KeyLockItemId;

                // Prefer taking the item they are holding in case they are deploying directly.
                var heldItem = player.GetActiveItem();
                if (heldItem != null && heldItem.info.itemid == lockItemId)
                    heldItem.UseItem(1);
                else
                    player.inventory.Take(null, lockItemId, 1);

                player.Command("note.inv", lockItemId, -1);
                return;
            }

            foreach (var ingredient in GetLockBlueprint(lockType).ingredients)
            {
                player.inventory.Take(null, ingredient.itemid, (int)ingredient.amount);
                player.Command("note.inv", ingredient.itemid, -ingredient.amount);

                var cooldownManager = lockType == LockType.CodeLock ? CraftCodeLockCooldowns : CraftKeyLockCooldowns;
                cooldownManager.UpdateLastUsedForPlayer(player.UserIDString);
            }
        }

        private BaseLock DeployLock(BaseEntity vehicle, Vector3 lockPosition, LockType lockType, ulong ownerID = 0)
        {
            var parentToEntity = GetLockParent(vehicle);
            if (parentToEntity == null)
                return null;

            var lockPrefab = lockType == LockType.CodeLock ? CodeLockPrefab : KeyLockPrefab;

            var baseLock = GameManager.server.CreateEntity(lockPrefab, lockPosition, GetLockRotation(vehicle)) as BaseLock;
            if (baseLock == null)
                return null;

            if (baseLock is KeyLock)
                (baseLock as KeyLock).keyCode = UnityEngine.Random.Range(1, 100000);

            if (ownerID != 0)
                baseLock.OwnerID = ownerID;

            baseLock.SetParent(parentToEntity, GetParentBone(vehicle));
            baseLock.Spawn();
            vehicle.SetSlot(BaseEntity.Slot.Lock, baseLock);

            Effect.server.Run(Prefab_CodeLock_DeployedEffect, baseLock.transform.position);
            Interface.CallHook("OnVehicleLockDeployed", vehicle, baseLock);

            return baseLock;
        }

        private BaseEntity GetLockParent(BaseEntity entity)
        {
            var car = entity as ModularCar;
            if (car != null)
                return FindFirstDriverModule(car);

            return entity;
        }

        private Quaternion GetLockRotation(BaseEntity entity)
        {
            if (entity is RidableHorse)
                return LockRotation_RidableHorse;

            if (entity is Kayak)
                return LockRotation_Kayak;

            return Quaternion.identity;
        }

        private string GetParentBone(BaseEntity entity)
        {
            if (entity is RidableHorse)
                return RidableHorseParentBone;

            return null;
        }

        private bool IsSupportedVehicle(BaseEntity entity, LockType lockType, ref string perm, ref Vector3 lockPosition)
        {
            var ch47 = entity as CH47Helicopter;
            if (!ReferenceEquals(ch47, null))
            {
                perm = lockType == LockType.CodeLock ? Permission_CodeLock_Chinook : Permission_KeyLock_Chinook;
                lockPosition = LockPosition_Chinook;
                return true;
            }

            var hab = entity as HotAirBalloon;
            if (!ReferenceEquals(hab, null))
            {
                perm = lockType == LockType.CodeLock ? Permission_CodeLock_HotAirBalloon : Permission_KeyLock_HotAirBalloon;
                lockPosition = LockPosition_HotAirBalloon;
                return true;
            }

            var kayak = entity as Kayak;
            if (!ReferenceEquals(kayak, null))
            {
                perm = lockType == LockType.CodeLock ? Permission_CodeLock_Kayak : Permission_KeyLock_Kayak;
                lockPosition = LockPosition_Kayak;
                return true;
            }

            var ridableHorse = entity as RidableHorse;
            if (!ReferenceEquals(ridableHorse, null))
            {
                perm = lockType == LockType.CodeLock ? Permission_CodeLock_RidableHorse : Permission_KeyLock_RidableHorse;
                lockPosition = LockPosition_RidableHorse;
                return true;
            }

            // Must go before MiniCopter
            var scrapHeli = entity as ScrapTransportHelicopter;
            if (!ReferenceEquals(scrapHeli, null))
            {
                perm = lockType == LockType.CodeLock ? Permission_CodeLock_ScrapHeli : Permission_KeyLock_ScrapHeli;
                lockPosition = LockPosition_ScrapHeli;
                return true;
            }

            var minicopter = entity as MiniCopter;
            if (!ReferenceEquals(minicopter, null))
            {
                perm = lockType == LockType.CodeLock ? Permission_CodeLock_MiniCopter : Permission_KeyLock_MiniCopter;
                lockPosition = LockPosition_MiniCopter;
                return true;
            }

            var car = entity as ModularCar;
            if (!ReferenceEquals(car, null))
            {
                perm = lockType == LockType.CodeLock ? Permission_CodeLock_ModularCar : Permission_KeyLock_ModularCar;
                lockPosition = LockPosition_VehicleModule;
                return true;
            }

            // Must go before MotorRowboat
            var rhib = entity as RHIB;
            if (!ReferenceEquals(rhib, null))
            {
                perm = lockType == LockType.CodeLock ? Permission_CodeLock_RHIB : Permission_KeyLock_RHIB;
                lockPosition = LockPosition_RHIB;
                return true;
            }

            var rowboat = entity as MotorRowboat;
            if (!ReferenceEquals(rowboat, null))
            {
                perm = lockType == LockType.CodeLock ? Permission_CodeLock_Rowboat : Permission_KeyLock_Rowboat;
                lockPosition = LockPosition_RowBoat;
                return true;
            }

            var sedan = entity as BasicCar;
            if (!ReferenceEquals(sedan, null))
            {
                perm = lockType == LockType.CodeLock ? Permission_CodeLock_Sedan : Permission_KeyLock_Sedan;
                lockPosition = LockPosition_Sedan;
                return true;
            }

            return false;
        }

        private BaseCombatEntity GetVehicleFromEntity(BaseEntity entity, BasePlayer basePlayer)
        {
            if (entity == null)
                return null;

            if (entity is HotAirBalloon || entity is BaseVehicle)
                return entity as BaseCombatEntity;

            var module = entity as BaseVehicleModule;
            if (module != null)
                return module.Vehicle;

            var carLift = entity as ModularCarGarage;
            if (!ReferenceEquals(carLift, null))
                return carLift.carOccupant;

            var hitchTrough = entity as HitchTrough;
            if (!ReferenceEquals(hitchTrough, null))
                return GetClosestHorse(hitchTrough, basePlayer);

            return null;
        }

        private RidableHorse GetClosestHorse(HitchTrough hitchTrough, BasePlayer player)
        {
            var closestDistance = 1000f;
            RidableHorse closestHorse = null;

            for (var i = 0; i < hitchTrough.hitchSpots.Length; i++)
            {
                var hitchSpot = hitchTrough.hitchSpots[i];
                if (!hitchSpot.IsOccupied())
                    continue;

                var distance = Vector3.Distance(player.transform.position, hitchSpot.spot.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHorse = hitchSpot.horse.Get(serverside: true) as RidableHorse;
                }
            }

            return closestHorse;
        }

        private BaseLock GetVehicleLock(BaseEntity vehicle) =>
            vehicle.GetSlot(BaseEntity.Slot.Lock) as BaseLock;

        private bool CanPlayerBypassLock(BasePlayer player, BaseLock baseLock)
        {
            object hookResult = Interface.CallHook("CanUseLockedEntity", player, baseLock);
            if (hookResult is bool)
                return (bool)hookResult;

            return IsPlayerAuthorizedToLock(player, baseLock)
                || IsLockSharedWithPlayer(player, baseLock);
        }

        private bool IsPlayerAuthorizedToLock(BasePlayer player, BaseLock baseLock) =>
            (baseLock as KeyLock)?.HasLockPermission(player) ?? IsPlayerAuthorizedToCodeLock(player.userID, baseLock as CodeLock);

        private bool IsPlayerAuthorizedToCodeLock(ulong userID, CodeLock codeLock) =>
            codeLock.whitelistPlayers.Contains(userID)
            || codeLock.guestPlayers.Contains(userID);

        private bool IsLockSharedWithPlayer(BasePlayer player, BaseLock baseLock)
        {
            var ownerID = baseLock.OwnerID;
            if (ownerID == 0 || ownerID == player.userID)
                return false;

            // In case the owner was locked out for some reason
            var codeLock = baseLock as CodeLock;
            if (codeLock != null && !IsPlayerAuthorizedToCodeLock(ownerID, codeLock))
                return false;

            var sharingSettings = PluginConfig.SharingSettings;
            if (sharingSettings.Team && player.currentTeam != 0)
            {
                var team = RelationshipManager.Instance.FindTeam(player.currentTeam);
                if (team != null && team.members.Contains(ownerID))
                    return true;
            }

            if (sharingSettings.Friends && Friends != null)
            {
                var friendsResult = Friends.Call("HasFriend", baseLock.OwnerID, player.userID);
                if (friendsResult is bool && (bool)friendsResult)
                    return true;
            }

            if ((sharingSettings.Clan || sharingSettings.ClanOrAlly) && Clans != null)
            {
                var clanMethodName = sharingSettings.ClanOrAlly ? "IsMemberOrAlly" : "IsClanMember";
                var clanResult = Clans.Call(clanMethodName, ownerID.ToString(), player.UserIDString);
                if (clanResult is bool && (bool)clanResult)
                    return true;
            }

            return false;
        }

        #endregion

        #region Helper Classes

        internal class CooldownManager
        {
            private readonly Dictionary<string, float> CooldownMap = new Dictionary<string, float>();
            private readonly float CooldownDuration;

            public CooldownManager(float duration)
            {
                CooldownDuration = duration;
            }

            public void UpdateLastUsedForPlayer(string userID) =>
                CooldownMap[userID] = Time.realtimeSinceStartup;

            public float GetSecondsRemaining(string userID)
            {
                float duration;
                return CooldownMap.TryGetValue(userID, out duration)
                    ? duration + CooldownDuration - Time.realtimeSinceStartup
                    : 0;
            }
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig() => Config.WriteObject(new VehicleLocksConfig(), true);

        internal class VehicleLocksConfig
        {
            [JsonProperty("AllowIfDifferentOwner")]
            public bool AllowIfDifferentOwner = false;

            [JsonProperty("AllowIfNoOwner")]
            public bool AllowIfNoOwner = true;

            [JsonProperty("CraftCooldownSeconds")]
            public float CraftCooldownSeconds = 10;

            [JsonProperty("ModularCarSettings")]
            public ModularCarSettings ModularCarSettings = new ModularCarSettings();

            [JsonProperty("DefaultSharingSettings")]
            public SharingSettings SharingSettings = new SharingSettings();
        }

        internal class ModularCarSettings
        {
            [JsonProperty("AllowEditingWhileLockedOut")]
            public bool AllowEditingWhileLockedOut = true;
        }

        internal class SharingSettings
        {
            [JsonProperty("Clan")]
            public bool Clan = false;

            [JsonProperty("ClanOrAlly")]
            public bool ClanOrAlly = false;

            [JsonProperty("Friends")]
            public bool Friends = false;

            [JsonProperty("Team")]
            public bool Team = false;
        }

        #endregion

        #region Localization

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
            player.ChatMessage(string.Format(GetMessage(player.IPlayer, messageName), args));

        private string GetMessage(IPlayer player, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, player.Id);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Generic.Error.NoPermission"] = "You don't have permission to do that.",
                ["Generic.Error.BuildingBlocked"] = "Error: Cannot do that while building blocked.",
                ["Generic.Error.Cooldown"] = "Please wait <color=red>{0}s</color> and try again.",
                ["Generic.Error.VehicleLocked"] = "That vehicle is locked.",
                ["Deploy.Error.NoVehicleFound"] = "Error: No vehicle found.",
                ["Deploy.Error.VehicleDead"] = "Error: That vehicle is dead.",
                ["Deploy.Error.DifferentOwner"] = "Error: Someone else owns that vehicle.",
                ["Deploy.Error.NoOwner"] = "Error: You do not own that vehicle.",
                ["Deploy.Error.HasLock"] = "Error: That vehicle already has a lock.",
                ["Deploy.Error.InsufficientResources"] = "Error: Not enough resources to craft a {0}.",
                ["Deploy.Error.Mounted"] = "Error: That vehicle is currently occupied.",
                ["Deploy.Error.ModularCar.NoCockpit"] = "Error: That car needs a cockpit module to receive a lock.",
                ["Deploy.Error.Distance"] = "Error: Too far away."
            }, this, "en");
        }

        #endregion
    }
}
