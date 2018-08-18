using Sandbox.ModAPI;

namespace Cheetah.Radars
{
    public abstract class RadarModuleBase
    {
        public MyRadar Radar { get; private set; }
        public IMyUpgradeModule RadarBlock => Radar.RadarBlock;

        public RadarModuleBase(MyRadar Radar)
        {
            this.Radar = Radar;
        }
    }
}
