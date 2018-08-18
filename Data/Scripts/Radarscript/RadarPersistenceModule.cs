using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Linq;

namespace Cheetah.Radars
{
    public class RadarPersistenceModule : RadarModuleBase
    {
        public RadarPersistenceModule(MyRadar Radar) : base(Radar) { }

        public virtual void Load()
        {
            try
            {
                string Storage = null;
                if (MyAPIGateway.Utilities.GetVariable($"settings_{Radar.Entity.EntityId}", out Storage))
                {
                    byte[] Raw = Convert.FromBase64String(Storage);
                    try
                    {
                        RadarCore.DebugWrite($"{RadarBlock.CustomName}.Load()", $"Loading settings. Raw data: {Raw.Count()} bytes", IsExcessive: false);
                        RadarPersistent persistent = MyAPIGateway.Utilities.SerializeFromBinary<RadarPersistent>(Raw);
                        Radar.RadarPower.Set(persistent.RadarPower);
                        Radar.ShowMarkers.Set(persistent.ShowMarkers);
                        Radar.ShowOnlyHostiles.Set(persistent.ShowOnlyHostiles);
                        Radar.ActiveRadar.Set(persistent.ActiveRadar);
                        Radar.ShowRoids.Set(persistent.ShowRoids);
                        Radar.ShowWorkingGridsOnly.Set(persistent.ShowWorkingGridsOnly);
                        Radar.ShowFloating.Set(persistent.ShowFloating);
                        if (Radar.RadarPower.Get() == 0) Radar.RadarPower.Set(800);
                    }
                    catch (Exception Scrap)
                    {
                        RadarCore.LogError($"{RadarBlock.CustomName}.Load()", Scrap);
                    }
                }
                else
                {
                    RadarCore.DebugWrite($"{RadarBlock.CustomName}.Load()", "Storage access failed.", IsExcessive: true);
                }
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError($"{RadarBlock.CustomName}.Load().AccessStorage", Scrap);
            }
        }

        public virtual void Save()
        {
            try
            {
                RadarPersistent persistent;
                persistent.ShowMarkers = Radar.ShowMarkers.Get();
                persistent.ShowOnlyHostiles = Radar.ShowOnlyHostiles.Get();
                persistent.RadarPower = Radar.RadarPower.Get();
                persistent.ActiveRadar = Radar.ActiveRadar.Get();
                persistent.ShowRoids = Radar.ShowRoids.Get();
                persistent.ShowWorkingGridsOnly = Radar.ShowWorkingGridsOnly.Get();
                persistent.ShowFloating = Radar.ShowFloating.Get();
                string Raw = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(persistent));
                MyAPIGateway.Utilities.SetVariable($"settings_{RadarBlock.EntityId}", Raw);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError($"{RadarBlock.CustomName}.Save()", Scrap);
            }
        }

        [ProtoContract]
        public struct RadarPersistent
        {
            [ProtoMember(1)]
            public bool ShowMarkers;
            [ProtoMember(2)]
            public bool ShowOnlyHostiles;
            [ProtoMember(3)]
            public float RadarPower;
            [ProtoMember(4)]
            public bool ActiveRadar;
            [ProtoMember(5)]
            public bool ShowRoids;
            [ProtoMember(6)]
            public bool ShowWorkingGridsOnly;
            [ProtoMember(7)]
            public bool ShowFloating;
        }
    }
}
