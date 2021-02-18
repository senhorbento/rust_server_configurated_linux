namespace Oxide.Plugins
{
    [Info("Runaway Boats", "0x89A", "1.1.0")]
    [Description("Stops boats from sailing away on dismount")]
    class RunawayBoats : RustPlugin
    {
        private bool withPassengers;
        private bool notDriver;

        const string canUse = "runawayboats.use";

        void Init() => permission.RegisterPermission(canUse, this);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                withPassengers = (bool)Config["Stop if boat has passengers"];
                notDriver = (bool)Config["Stop if dismounted player is not driver"];
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config["Stop if boat has passengers"] = false;
            Config["Stop if dismounted player is not driver"] = false;
        }

        object CanDismountEntity(BasePlayer player, BaseMountable mount)
        {
            MotorRowboat boat = mount.GetParentEntity() as MotorRowboat;
            if (boat != null) StopBoat(boat);
            return null;
        }

        void StopBoat(MotorRowboat boat)
        {
            NextTick(() =>
            {
                if (!boat.HasDriver() && !boat.HasAnyPassengers() || !boat.HasDriver() && boat.HasAnyPassengers() && withPassengers || boat.HasDriver() && notDriver) boat.EngineToggle(false);
            });
        }
    }
}

