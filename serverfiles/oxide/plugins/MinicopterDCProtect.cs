using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Mini Copter DC Protect", "NooBlet", "0.1.3")]
    [Description("Protects player minicopters from crashing if they disconnect")]
    public class MinicopterDCProtect : CovalencePlugin
    {

        private static int _layerMask = LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World");

        #region Hooks

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!player.isMounted) return;
            if (player.GetMountedVehicle() is MiniCopter)
                TPplayerandCopter(player, player.GetMountedVehicle() as MiniCopter);
            if (player.GetMountedVehicle() is ScrapTransportHelicopter)
                TPplayerandCopter(player, player.GetMountedVehicle() as ScrapTransportHelicopter);
        }

        #endregion Hooks

        #region Methods
        private void TPplayerandCopter(BasePlayer player, BaseHelicopterVehicle copter)
        {            
            var playergroundloc = FindLowestTpPoint(player.transform.position);
            Teleport(player, playergroundloc);
            var heligroundloc = new Vector3(player.transform.position.x + 1f, player.transform.position.y + 2f, player.transform.position.z + 1f);            
            copter.transform.position = FindclosestLand(heligroundloc);
        }

        private void Teleport(BasePlayer player, Vector3 target)
        {
            float currenthealth = player.health;
            player.health = 10000f;
            player.Teleport(target);
            player.health = currenthealth;
        }

        private Vector3 FindLowestTpPoint(Vector3 loc)
        {
            Vector3 location = loc;
            RaycastHit hit = new RaycastHit();

            if (UnityEngine.Physics.Raycast(loc, Vector3.down, out hit, float.MaxValue, _layerMask))
            {
                location = hit.point;
                if (TerrainMeta.WaterMap.GetHeight(location) > TerrainMeta.HeightMap.GetHeight(location))
                {
                    location = FindclosestLand(location);
                }
            }
            return location;
        }

        Vector3 FindclosestLand(Vector3 loc)
        {
            for (int i = 0; i < 500; i++)
            {
                if (loc.x < 0) { loc.x += 4f; } else { loc.x -= 4f; }
                if (loc.z < 0) { loc.z += 4f; } else { loc.z -= 4f; }
                if (TerrainMeta.WaterMap.GetHeight(loc) < TerrainMeta.HeightMap.GetHeight(loc))
                {
                    if (loc.x < 0) { loc.x += 2f; } else { loc.x -= 2f; }
                    if (loc.z < 0) { loc.z += 2f; } else { loc.z -= 2f; }
                    break;
                }
            }
            return loc;
        }

        #endregion Methods
    }
}