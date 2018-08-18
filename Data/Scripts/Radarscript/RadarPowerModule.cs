using Cheetah.API;
using Cheetah.Networking;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Ingame = Sandbox.ModAPI.Ingame;

namespace Cheetah.Radars
{
    public class RadarPowerModule : RadarModuleBase
    {
        public static MyDefinitionId Electricity = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        public MyResourceSinkComponent MyRadarPowerSink;
        public float EffectiveRadarPower
        {
            get
            {
                try
                {
                    return Radar.RadarPower * MyRadarPowerSink.SuppliedRatioByType(Electricity);
                }
                catch { return 0; }
            }
        }

        public RadarPowerModule(MyRadar Radar) : base(Radar)
        {
            if (!RadarBlock.TryGetComponent(out MyRadarPowerSink))
            {
                MyRadarPowerSink = new MyResourceSinkComponent();
                MyResourceSinkInfo info = new MyResourceSinkInfo();
                info.ResourceTypeId = Electricity;
                MyRadarPowerSink.AddType(ref info);
                RadarBlock.Components.Add(MyRadarPowerSink);
            }
        }

        public void InitResourceSink()
        {
            try
            {
                MyRadarPowerSink.SetMaxRequiredInputByType(Electricity, 100);
                MyRadarPowerSink.SetRequiredInputFuncByType(Electricity, PowerConsumptionFunc);
                MyRadarPowerSink.SetRequiredInputByType(Electricity, PowerConsumptionFunc());
                MyRadarPowerSink.Update();
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError(RadarBlock.CustomName, Scrap);
            }
        }

        public float PowerConsumptionFunc()
        {
            try
            {
                if (!Radar.IsWorking()) return 0;
                //if (RadarBlock.IsOwnedByNPC()) return (ActiveRadar ? (RadarPower.Get() > 0 ? RadarPower.Get() / 1000 : 0) : 0.800f) / RadarCore.AIOwnedRadarPowerConsumptionMultiplier;
                return (Radar.ActiveRadar ? (Radar.RadarPower.Get() > 0 ? Radar.RadarPower.Get() / 1000 : 0) : 0.800f);
            }
            catch
            {
                return 0f;
            }
        }
    }
}
