using UnityEngine;
using Oxide.Core.Configuration; 

namespace Oxide.Plugins
{
    [Info("Horse Seat", "Chokitu", "1.0.3")]
    [Description("Gives horses 2 seats")]
    public class HorseSeat : RustPlugin
    {
        #region Config
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                Enable2Seats = true
            };
        }

        private class PluginConfig
        {
            public bool Enable2Seats;
        }
        #endregion

        #region Oxide
        private void Init() => config = Config.ReadObject<PluginConfig>();

        private void OnEntitySpawned(RidableHorse entity)
        {
            if (entity == null || !config.Enable2Seats)
            {
                return;
            }

             NextTick(() => {
                entity.gameObject.AddComponent<AddSeats>();
            });
        }

        public class AddSeats : MonoBehaviour
        {

             public RidableHorse entity;

               void Awake()
             {
                 entity = GetComponent<RidableHorse>();
                 if (entity == null)
                 {
                     Destroy(this);
                     return;
                 }
                 
                var seat = GameManager.server.CreateEntity("assets/bundled/prefabs/static/chair.invisible.static.prefab", entity.transform.position, new Quaternion(), true);
                 if (seat == null) return;
                 seat.Spawn();
                 seat.SetParent(entity);
                 seat.transform.localPosition = new Vector3(0f, 1.2f, -0.7f);
                 seat.transform.Rotate(new Vector3(0.0f, 0.0f, 0.0f));
                 seat.SendNetworkUpdateImmediate(true);
             }
        }
        #endregion
    }
}
