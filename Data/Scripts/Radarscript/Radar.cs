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
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, new string[] { "Radar_Dome_Large", "Radar_Dome_Small", "Radar_Dome_Mini", "Radar_Dome_Tiny" })]
    public class MyRadar : MyGameLogicComponent
    {
        #region Definitions
        public IMyUpgradeModule RadarBlock;
        public RadarableGrid MyRadarGrid => RadarBlock.CubeGrid.AsRadarable();
        protected IMyGridTerminalSystem Term;
        public long RadarID => RadarBlock.EntityId;
        /// <summary>
        /// Radar's world position.
        /// </summary>
        public Vector3D Position => RadarBlock.GetPosition();
        public Vector3I GridPosition => RadarBlock.Position;
        
        public HashSet<Ingame.MyDetectedEntityInfo> DetectedEntities = new HashSet<Ingame.MyDetectedEntityInfo>();

        public float TotalRadarPower { get; protected set; }
        public float MaxPower { get; protected set; }
        public AutoSet<float> RadarPower { get; protected set; }
        public AutoSet<bool> ActiveRadar { get; protected set; }
        public AutoSet<bool> ShowMarkers { get; protected set; }
        public AutoSet<bool> ShowRoids { get; protected set; }
        public AutoSet<bool> ShowOnlyHostiles { get; protected set; }
        public AutoSet<bool> ShowFloating { get; protected set; }
        public AutoSet<bool> ShowWorkingGridsOnly { get; protected set; }
        public bool HasOwnerInRelay { get; protected set; }

        
        public IMyPlayer OwnerPlayer;
        public IMyEntity OwnerEntity => OwnerPlayer?.Controller?.ControlledEntity?.Entity;
        public long OwnerID => RadarBlock.OwnerId;
        public List<IMyGps> OwnerGPSes => MyAPIGateway.Session.GPS.GetGpsList(OwnerID);
        IMyHudNotification TestNote;

        public GridScanModule ScanModule { get; protected set; }
        public RadarMarkerModule MarkerModule { get; protected set; }
        public RadarDetectorModule DetectorModule { get; protected set; }
        public RadarPersistenceModule PersistenceModule { get; protected set; }
        public RadarPowerModule PowerModule { get; protected set; }
        #endregion
        //Debug.Enabled = true;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            base.Init(objectBuilder);
            RadarBlock = Entity as IMyUpgradeModule;

            ScanModule = new GridScanModule(this);
            MarkerModule = new RadarMarkerModule(this);
            DetectorModule = new RadarDetectorModule(this);
            PersistenceModule = new RadarPersistenceModule(this);
            PowerModule = new RadarPowerModule(this);
            RadarCore.SaveRegister(PersistenceModule.Save);
            
            /*if (!RadarBlock.HasComponent<MyModStorageComponent>())
            {
                RadarBlock.Storage = new MyModStorageComponent();
                RadarBlock.Components.Add(RadarBlock.Storage);
                RadarCore.DebugWrite($"{RadarBlock.CustomName}.Init()", "Block doesn't have a Storage component!", IsExcessive: false);
            }*/
        }

        #region Loading stuff
        protected void GetMaxPower()
        {
            try
            {
                IMyUpgradeModule block = Entity as IMyUpgradeModule;
                List<MyUpgradeModuleInfo> Info;
                block.GetUpgradeList(out Info);
                MaxPower = Info.First(x => x.UpgradeType == "Radar").Modifier;
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError($"{RadarBlock.CustomName}.Load().GetMaxPower", Scrap);
            }
        }

        void OnMarkForClose(IMyEntity Entity)
        {
            try
            {
                RadarCore.SaveUnregister(PersistenceModule.Save);
                MarkerModule.RemoveGPSMarkers(true);
                RadarBlock.AppendingCustomInfo -= RadarBlock_AppendingCustomInfo;
                RadarBlock.OnMarkForClose -= OnMarkForClose;
            }
            catch { }
        }
        #endregion

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if (RadarBlock.CubeGrid.Physics == null)
                {
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    return;
                }

                if (!Networker.Inited) Networker.Init(907384096);
                if (!Controls.InitedRadarControls) Controls.InitRadarControls();

                NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
                GetMaxPower();
                Term = RadarBlock.CubeGrid.GetTerminalSystem();

                RadarPower = new AutoSet<float>(RadarBlock, "Power", 1000, x => x >= 0 && x <= MaxPower);
                ShowMarkers = new AutoSet<bool>(RadarBlock, "ShowMarkers", true, null);
                ShowRoids = new AutoSet<bool>(RadarBlock, "ShowRoids", true, null);
                ActiveRadar = new AutoSet<bool>(RadarBlock, "Active", false, null);
                ShowOnlyHostiles = new AutoSet<bool>(RadarBlock, "OnlyHostiles", true, null);
                ShowFloating = new AutoSet<bool>(RadarBlock, "Floating", false, null);
                ShowWorkingGridsOnly = new AutoSet<bool>(RadarBlock, "ShowWorkingGridsOnly", false, null);

                PowerModule.InitResourceSink();
                PersistenceModule.Load();
                RadarBlock.AppendingCustomInfo += RadarBlock_AppendingCustomInfo;
                RadarBlock.OnMarkForClose += OnMarkForClose;
                Debug.Write($"Added radar {RadarBlock.EntityId}");
                if (RadarCore.Debug) TestNote = MyAPIGateway.Utilities.CreateNotification($"{RadarBlock.CustomName}: enabled;", int.MaxValue, "Green");

                if (RadarCore.Debug) TestNote.Show();

                RadarCore.DebugWrite("Testing", "TESTING", true);

                //MyRadarGrid = RadarBlock.CubeGrid.Components.Get<RadarableGrid>();
                if (MyRadarGrid == null)
                {
                    throw new Exception($"{RadarBlock.CustomName}.MyRadarGrid is null!");
                }
            }
            catch { }
        }

        private void RadarBlock_AppendingCustomInfo(IMyTerminalBlock Block, StringBuilder Info)
        {
            Info.AppendLine($"Detected targets: {DetectedEntities.Count}");
            if (RadarCore.Debug) Info.AppendLine($"Self detection rate: {Math.Round(MyRadarGrid.ActiveDetectionRate, 2)}");
        }

        int scanx = 0;

        public override void UpdateBeforeSimulation10()
        {
            try
            {
                
                scanx++;
                if(scanx == 6){
                    scanx = 0;
                    MarkerModule.RemoveGPSMarkers();
                    DetectedEntities.Clear();
                    if (RadarBlock.CubeGrid.Physics == null) return;
                    //System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                    //watch.Start();
                    PowerModule.MyRadarPowerSink.Update();
                    if (IsWorking())
                    {
                        OwnerPlayer = GetOwnerPlayer();
                        FindOwnerInRelayNetwork();
                        CalculateTotalPower();
                        RadarBlock.RefreshCustomInfo();
                        PerformScan();
                    }
                    else
                    {
                        HasOwnerInRelay = false;
                        //if (RadarCore.Debug) TestNote.Hide();
                    }
                    //watch.Stop();
                    //MyAPIGateway.Utilities.ShowMessage(RadarBlock.CustomName, $"Scan took {(Math.Round(watch.ElapsedTicks * 1000f / System.Diagnostics.Stopwatch.Frequency, 2))}");
                }
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError(RadarBlock.CustomName, Scrap);
            }
        }

        /*public override void UpdatingStopped()
        {
            TotalRadarPower = 0;
            DetectedEntities.Clear();
            RemoveGPSMarkers(true);
        }*/

        public bool IsWorking()
        {
            if(RadarBlock.Closed || RadarBlock.MarkedForClose)
            {
                MarkForClose();
                return false;
            }
            
            RadarCore.DebugWrite($"{RadarBlock.CustomName}", $"Functional={RadarBlock.IsFunctional};Enabled={RadarBlock.Enabled};PowerAvailable={PowerModule.MyRadarPowerSink.IsPowerAvailable(RadarPowerModule.Electricity, 0.8f)}", true);
            return RadarBlock.IsFunctional && RadarBlock.Enabled && PowerModule.MyRadarPowerSink.IsPowerAvailable(RadarPowerModule.Electricity, 0.8f);
        }

        void CalculateTotalPower()
        {
            try
            {
                TotalRadarPower = 0;
                Term.GetBlocksOfType<IMyUpgradeModule>(collect: x => Controls.IsRadar(x) && x.IsWorking).ForEach(x => TotalRadarPower += Controls.RadarReturn(x, r => r.PowerModule.EffectiveRadarPower));
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError(RadarBlock.CustomName + ".CalculateTotalPower", Scrap);
            }
        }

        protected void FindOwnerInRelayNetwork()
        {
            HasOwnerInRelay = false;
            if (OwnerID == 0) return;
            if (OwnerEntity == null) return;
            IMyShipController SC;
            if (OwnerEntity.IsOfType(out SC) && SC.CubeGrid == RadarBlock.CubeGrid)
            {
                HasOwnerInRelay = true;
                return;
            }
            HasOwnerInRelay = MyRadarGrid.RelayedChars.Any(x => x.ControllerInfo.ControllingIdentityId == OwnerID);
        }

        public void PerformScan()
        {
            try
            {
                if (RadarBlock == null || !IsWorking() || !RadarBlock.IsVisible()) return;

                //DetectedEntities is cleared in UpdateBeforeSim10
                if (OwnerPlayer.Controller.ControlledEntity is IMyCharacter && OwnerPlayer.GetPosition().DistanceTo(Position) <= RadarCore.GuaranteedDetectionRange)
                {
                    DetectedEntities.Add(MyDetectedEntityInfoHelper.Create(OwnerPlayer.Controller.ControlledEntity as MyEntity, RadarBlock.OwnerId, null));
                }

                BoundingSphereD RadarSphere = new BoundingSphereD(Position, RadarCore.MaxVisibilityRange);
                List<IMyEntity> EntitiesAround = new List<IMyEntity>();
                EntitiesAround = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref RadarSphere);
                HashSet<IMyCubeGrid> Grids = new HashSet<IMyCubeGrid>();
                HashSet<IMyEntity> OtherEntities = new HashSet<IMyEntity>();
                EntitiesAround.SortByType(Grids, OtherEntities);

                foreach (IMyCubeGrid Grid in Grids)
                {
                    try
                    {
                        ScanGrid(Grid.AsRadarable());
                    }
                    catch { }
                }

                foreach (IMyEntity Entity in OtherEntities)
                {
                    if (Entity.IsOfType<IMyCubeGrid>()) continue;
                    try
                    {
                        ScanEntity(Entity);
                    }
                    catch { }
                }

                if (RadarCore.Debug) TestNote.Text = $"{RadarBlock.CustomName}: detected {DetectedEntities.Count} objects; RadarPower: {Math.Round(RadarPower / 1000)} MW";
            }
            catch { }
        }

        protected void ScanGrid(RadarableGrid RGrid)
        {
            //if (DetectedEntities.Any(x => x.EntityId == RGrid.Grid.EntityId)) return;

            float Distance = this.DistanceTo(RGrid);

            if (Distance <= RadarCore.GuaranteedDetectionRange)
            {
                AddEntity(RGrid.Grid, RGrid.Position);
                return;
            }

            Vector3D? Hit;

            if (!DetectorModule.IsInView(RGrid.Grid, out Hit))
            {
                RadarCore.DebugWrite($"{RadarBlock.CustomName}.ScanGrid({RGrid.DisplayName})", "discarded: invisible by ray", true);
                return;
            }

            RadarCore.DebugWrite($"{RadarBlock.CustomName}.ScanGrid({RGrid.DisplayName})", $"Grid rate={RGrid.ActiveDetectionRate}, rate/dist={Math.Round(MyRadarGrid.TotalRadarPower / Position.DistanceTo(RGrid.Position), 2)}", true);

            float RayPower = ActiveRadar ? PowerModule.EffectiveRadarPower : 800;
            if (RGrid.HasMarker && RGrid.MarkerRange >= Distance)
            {
                AddEntity(RGrid.Grid, null);
                return;
            }
            else if (DetectorModule.CanDetectUsingActiveRadar(RGrid) || DetectorModule.CanDetectByRadar(RGrid) || DetectorModule.CanDetectByHeat(RGrid) || DetectorModule.CanDetectByGravity(RGrid))
            {
                AddEntity(RGrid.Grid, RGrid.Position);
                return;
            }
        }

        protected void ScanEntity(IMyEntity Entity)
        {
            //if (DetectedEntities.Any(x => x.EntityId == Entity.EntityId)) return;
            float Distance = this.DistanceTo(Entity);

            Vector3D? Hit;
            if (!DetectorModule.IsInView(Entity, out Hit)) return;

            float RayPower = ActiveRadar ? PowerModule.EffectiveRadarPower : 800;
            if (Distance <= RadarCore.GuaranteedDetectionRange || RayPower >= Distance) AddEntity(Entity, Hit);
        }

        protected void AddEntity(IMyEntity RadarEntity, Vector3D? Hit)
        {
            Ingame.MyDetectedEntityInfo RadarInfo = MyDetectedEntityInfoHelper.Create(RadarEntity as MyEntity, RadarBlock.OwnerId, Hit);
            bool AddMarker = DetectedEntities.Add(RadarInfo) & MarkerModule.ShouldMarkerExist(RadarInfo);

            if (!AddMarker || MarkerModule.RadarMarkers.ContainsKey(RadarInfo.EntityId)) return;

            IMyVoxelMap voxel;
            if (RadarEntity is IMyCubeGrid)
            {
                string GridName = (RadarEntity as IMyCubeGrid).CustomName;
                MarkerModule.AddGPSMarker(RadarInfo.Rename(GridName), GridName);
            }
            else if (RadarEntity.IsOfType(out voxel))
            {
                string VoxelName = (!voxel.StorageName.StartsWith("Asteroid_") ? voxel.StorageName : "Asteroid");
                MarkerModule.AddGPSMarker(RadarInfo, VoxelName);
            }
            else
            {
                MarkerModule.AddGPSMarker(RadarInfo, RadarEntity.DisplayName);
            }
        }

        protected IMyPlayer GetOwnerPlayer()
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, x => x.IdentityId == RadarBlock.OwnerId);
            if (players.Count == 0) return null;
            return players[0] as IMyPlayer;
        }
    }

    public static class Debug
    {
        public static bool Enabled { get; set; }

        public static void Write(string msg)
        {
            if (!RadarCore.Debug)
                return;

            MyAPIGateway.Utilities.ShowMessage("Radar", msg);
        }

        public static void ForceWrite(string msg)
        {
            MyAPIGateway.Utilities.ShowMessage("RADAR CRITICAL", msg);
        }
    }
}