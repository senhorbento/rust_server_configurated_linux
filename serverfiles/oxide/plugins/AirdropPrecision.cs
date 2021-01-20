namespace Oxide.Plugins
{
    [Info("Airdrop Precision", "k1lly0u", "0.2.0")]
    public class AirdropPrecision : RustPlugin
    {
        #region Fields
        private const string CARGOPLANE_PREFAB = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        #endregion

        #region Oxide Hooks        
        private void OnExplosiveDropped(BasePlayer player, SupplySignal supplySignal) => OnExplosiveThrown(player, supplySignal);

        private void OnExplosiveThrown(BasePlayer player, SupplySignal supplySignal)
        {
            if (supplySignal == null)
                return;

            supplySignal.CancelInvoke(supplySignal.Explode);
            supplySignal.Invoke(() => Explode(supplySignal), 3f);
        }  
        
        private void Explode(SupplySignal supplySignal)
        {
            CargoPlane cargoPlane = GameManager.server.CreateEntity(CARGOPLANE_PREFAB) as CargoPlane;
            cargoPlane.InitDropPosition(supplySignal.transform.position);
            cargoPlane.Spawn();

            supplySignal.Invoke(supplySignal.FinishUp, 210f);
            supplySignal.SetFlag(BaseEntity.Flags.On, true);
            supplySignal.SendNetworkUpdateImmediate();
        }
        #endregion
    }
}

