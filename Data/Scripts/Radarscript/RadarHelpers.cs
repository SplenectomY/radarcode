using Cheetah.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Cheetah.Radars
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid), false)]
    public class RadarableGrid : MyGameLogicComponent
    {
        public IMyCubeGrid Grid { get; protected set; }
        public Vector3D Position => Grid.GetPosition();
        public float MarkerRange { get; protected set; }
        public bool HasMarker => MarkerRange > 0;
        public float TotalRadarPower { get; protected set; }
        public float ActiveDetectionRate
        {
            get
            {
                try
                {
                    float Distance = RadarCore.VisibilityDistanceByDefaultRule(Grid);
                    if (Distance == 0) return 0;
                    return Distance / (float)Math.Pow(RadarCore.DecoyStealthCoefficient, DecoysCount);
                }
                catch (Exception Scrap)
                {
                    Grid.LogError("GetBaseDetectionDistance", Scrap);
                    return 0;
                }
            }
        }
        /// <summary>
        /// Current reactor output, in MW.
        /// </summary>
        public float ReactorOutput { get; protected set; }
        /// <summary>
        /// Grid's total mass.
        /// </summary>
        public float Mass => Grid.Physics.Mass;
        public float GravityDistortion { get; protected set; }
        public int DecoysCount { get; protected set; }
        public string DisplayName => Grid.CustomName;
        public Vector3 Gravity { get; protected set; } = Vector3.Zero;
        public float GravityInG => Gravity != Vector3.Zero ? (Gravity.Length() / 9.81f) : 0;
        public List<IMyVoxelMap> ClosestRoids { get; protected set; } = new List<IMyVoxelMap>();
        public HashSet<AntennaComms> Antennae { get; protected set; } = new HashSet<AntennaComms>();
        public HashSet<IMyCubeGrid> RelayedGrids { get; protected set; } = new HashSet<IMyCubeGrid>();
        public HashSet<IMyCharacter> RelayedChars { get; protected set; } = new HashSet<IMyCharacter>();
        public MyPlanet ClosestPlanet { get; protected set; } = null;
        public bool IsInPlanetGravityWell => GravityInG >= 0.05;
        public bool IsWorkingGrid { get; protected set; }
        protected IMyGridTerminalSystem Term;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Grid = Entity as IMyCubeGrid;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            Term = Grid.GetTerminalSystem();
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            try
            {
                if (Grid.Physics == null) return;
                AssertMarkers();
                CalculateTotalPower();
                ReactorOutput = Grid.GetCurrentReactorPowerOutput();
                GetDecoyCount();
                GetRelays();
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError(Grid.DisplayName, Scrap, DebugPrefix: "RadarableGrid.Upd10|");
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            try
            {
                IsWorkingGrid = Grid.IsFunctionalGrid();
                GetCloseVoxels();
                CalculateGravityDistortion();
                ClosestPlanet = MyGamePruningStructure.GetClosestPlanet(Position);
                if (ClosestPlanet != null && ClosestPlanet.IsInGravity(Position))
                {
                    Gravity = ClosestPlanet.GetGravity(Position);
                }
                else
                {
                    Gravity = Vector3.Zero;
                    ClosestPlanet = null;
                }
                GetAntennae();
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError(Grid.DisplayName, Scrap, DebugPrefix: "RadarableGrid.Upd100|");
            }
        }

        void GetAntennae()
        {
            List<IMyRadioAntenna> AntennaeBlocks = new List<IMyRadioAntenna>();
            Term.GetBlocksOfType(AntennaeBlocks);
            Antennae = new HashSet<AntennaComms>();
            foreach (var AntennaBlock in AntennaeBlocks)
            {
                AntennaComms Antenna = AntennaBlock.GetComponent<AntennaComms>();
                if (Antenna != null) Antennae.Add(Antenna);
            }
        }

        void GetDecoyCount()
        {
            List<IMyDecoy> Decoys = new List<IMyDecoy>();
            Term.GetBlocksOfType(Decoys, x => x.IsWorking);
            DecoysCount = Decoys.Count;
        }

        void AssertMarkers()
        {
            try
            {
                List<IMyBeacon> Beacons = new List<IMyBeacon>();
                List<IMyRadioAntenna> Antennae = new List<IMyRadioAntenna>();
                Term.GetBlocksOfType(Beacons, x => x.IsWorking);
                Term.GetBlocksOfType(Antennae, x => x.IsWorking && x.IsBroadcasting);

                if (Beacons.Count == 0 && Antennae.Count == 0)
                {
                    MarkerRange = 0;
                    return;
                }

                float BeaconRange = 0;
                float AntennaRange = 0;

                if (Beacons.Count > 0)
                {
                    Beacons = Beacons.OrderByDescending(x => x.Radius).ToList();
                    BeaconRange = Beacons.First().Radius;
                }

                if (Antennae.Count > 0)
                {
                    Antennae = Antennae.OrderByDescending(x => x.Radius).ToList();
                    AntennaRange = Antennae.First().Radius;
                }

                MarkerRange = Math.Max(BeaconRange, AntennaRange);
                /*if (!HasMarker) DisplayName = "";
                else if (BeaconRange > AntennaRange) DisplayName = Beacons.First().CustomName;
                else if (AntennaRange > BeaconRange) DisplayName = Antennae.First().CustomName;*/
            }
            catch (Exception Scrap)
            {
                Grid.LogError("GetMarkerRange", Scrap);
            }
        }

        void GetRelays()
        {
            RelayedGrids = new HashSet<IMyCubeGrid>(Antennae.SelectMany(x => x.RelayedGrids));
            RelayedChars = new HashSet<IMyCharacter>(Antennae.SelectMany(x => x.RelayedChars));
        }

        void GetCloseVoxels()
        {
            List<MyVoxelBase> voxels = new List<MyVoxelBase>();
            BoundingSphereD LookupSphere = new BoundingSphereD(Position, Grid.WorldVolume.Radius * 8);
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref LookupSphere, voxels);
            ClosestRoids.Clear();
            voxels.ForEach(vxl => { if (!vxl.IsOfType<MyPlanet>()) ClosestRoids.Add(vxl as IMyVoxelMap); });
        }

        void CalculateTotalPower()
        {
            try
            {
                TotalRadarPower = 0;
                List<IMyTerminalBlock> Radars = new List<IMyTerminalBlock>();
                Term.GetBlocksOfType<IMyTerminalBlock>(Radars, x => Controls.IsRadar(x));

                foreach (IMyTerminalBlock RadarBlock in Radars)
                {
                    MyRadar Radar = RadarBlock.GetComponent<MyRadar>();
                    if (Radar == null || !Radar.ActiveRadar || !Radar.IsWorking()) continue;
                    TotalRadarPower += Radar.PowerModule.EffectiveRadarPower;
                }
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError(Grid.DisplayName + ".CalculateTotalPower", Scrap);
            }
        }

        void CalculateGravityDistortion()
        {
            const float G = 9.81f;
            if (Grid.Physics == null || Grid.IsStatic)
            {
                GravityDistortion = 0;
                return;
            }
            List<IMyGravityGeneratorBase> GravGens = new List<IMyGravityGeneratorBase>();
            Grid.GetTerminalSystem().GetBlocksOfType(GravGens, x => (x as IMyCubeBlock).IsWorking);
            float MassDistortion = RadarCore.GetMassDistortion(Grid);
            float ArtificialGravityDistortion = 0;

            if (GravGens.Count != 0)
            {
                foreach (var GravGen in GravGens)
                    ArtificialGravityDistortion += Math.Abs(GravGen.GravityAcceleration);

                ArtificialGravityDistortion = (ArtificialGravityDistortion / G * 1000) / 2;
            }

            GravityDistortion = MassDistortion + ArtificialGravityDistortion;
        }
    }

    public static class RadarExtensions
    {
        public static float DistanceTo(this MyRadar Radar, Vector3D To)
        {
            return Radar.Position.DistanceTo(To);
        }

        public static float DistanceTo(this MyRadar Radar, IMyEntity Target)
        {
            return Radar.Position.DistanceTo(Target.GetPosition());
        }

        public static float DistanceTo(this MyRadar Radar, RadarableGrid Target)
        {
            return Radar.Position.DistanceTo(Target.Position);
        }

        public static RadarableGrid AsRadarable(this IMyCubeGrid Grid)
        {
            return Grid.Components.Has<RadarableGrid>() ? Grid.Components.Get<RadarableGrid>() : Grid.GameLogic.GetAs<RadarableGrid>();
        }

        public static bool TryAsRadarable(this IMyCubeGrid Grid, out RadarableGrid Radarable)
        {
            Radarable = AsRadarable(Grid);
            return Radarable != null;
        }

        public static float GetRadarPower(this IMyCubeGrid Grid)
        {
            try
            {
                var Radars = new List<MyRadar>();
                var blocks = new List<IMyUpgradeModule>();
                Grid.GetTerminalSystem().GetBlocksOfType(blocks);
                var Radar = blocks.FirstOrDefault(x => Controls.IsRadar(x)).Components.Get<MyRadar>();
                return Radar != null ? Radar.TotalRadarPower : 0;
            }
            catch (Exception Scrap)
            {
                Grid.LogError("GetRadarPower", Scrap);
                return 0;
            }
        }

        /// <summary>
        /// Returns the distance from which the grid can be spotted by Radar via active detection.
        /// </summary>
        public static float GetBaseDetectionDistance(this IMyCubeGrid Grid)
        {
            try
            {
                float Distance = RadarCore.VisibilityDistanceByDefaultRule(Grid);
                if (Distance == 0) return 0;

                List<IMyDecoy> Decoys = new List<IMyDecoy>();
                Grid.GetTerminalSystem().GetBlocksOfType(Decoys, x => x.IsWorking);
                if (Decoys.Count == 0) return Distance;

                return Distance / (float)Math.Pow(RadarCore.DecoyStealthCoefficient, Decoys.Count);
            }
            catch (Exception Scrap)
            {
                Grid.LogError("GetBaseDetectionDistance", Scrap);
                return 0;
            }
        }

        /// <summary>
        /// Returns overall distance from which the grid can be spotted by Radar.
        /// </summary>
        public static float GetDetectionDistance(this IMyCubeGrid Grid)
        {
            return Math.Max(Grid.GetMarkerRange(), Grid.GetBaseDetectionDistance());
        }

        /// <summary>
        /// Returns overall distance from which the entity can be spotted by Radar.
        /// </summary>
        public static float GetDetectionRate(this IMyEntity Entity)
        {
            return RadarCore.VisibilityDistanceByDefaultRule(Entity);
        }

        /// <summary>
        /// Returns the distance from which the grid can be spotted by Radar via antenna/beacon.
        /// 0 if no broadcasting beacons/antennae are found.
        /// </summary>
        public static float GetMarkerRange(this IMyCubeGrid Grid)
        {
            try
            {
                var Term = Grid.GetTerminalSystem();
                List<IMyBeacon> Beacons = new List<IMyBeacon>();
                List<IMyRadioAntenna> Antennae = new List<IMyRadioAntenna>();
                Term.GetBlocksOfType(Beacons, x => x.IsWorking);
                Term.GetBlocksOfType(Antennae, x => x.IsWorking && x.IsBroadcasting);

                if (Beacons.Count == 0 && Antennae.Count == 0) return 0;

                float BeaconRange = 0;
                float AntennaRange = 0;

                if (Beacons.Count > 0)
                    BeaconRange = Beacons.Select(x => x.Radius).Max();

                if (Antennae.Count > 0)
                    AntennaRange = Antennae.Select(x => x.Radius).Max();

                return Math.Max(BeaconRange, AntennaRange);
            }
            catch (Exception Scrap)
            {
                GridDebugHelpers.LogError(Grid, "GetMarkerRange", Scrap);
                return 0;
            }
        }
    }

    public static class GridDebugHelpers
    {
        public static void DebugWrite(this IMyCubeGrid Grid, string Source, string Message)
        {
            if (RadarCore.Debug) MyAPIGateway.Utilities.ShowMessage(Grid.DisplayName, $"Debug message from '{Source}': {Message}");
        }

        public static void LogError(this IMyCubeGrid Grid, string Source, Exception Scrap)
        {
            string DisplayName = "";
            try
            {
                DisplayName = Grid.DisplayName;
            }
            finally
            {
                MyAPIGateway.Utilities.ShowMessage(DisplayName, $"Fatal error in '{Source}': {Scrap.Message}. {(Scrap.InnerException != null ? Scrap.InnerException.Message : "No additional info was given by the game :(")}");
            }
        }
    }
}
