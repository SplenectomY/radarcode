using Cheetah.API;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using Ingame = Sandbox.ModAPI.Ingame;

namespace Cheetah.Radars
{
    public class RadarMarkerModule : RadarModuleBase
    {
        public Dictionary<long, GPSMarker> RadarMarkers = new Dictionary<long, GPSMarker>();

        public RadarMarkerModule(MyRadar Radar) : base(Radar) { }

        public bool ShouldMarkerExist(Ingame.MyDetectedEntityInfo RadarInfo)
        {
            if (!Radar.IsWorking()) return false;
            if (!Radar.ShowMarkers) return false;
            if (!Radar.HasOwnerInRelay) return false;
            IMyEntity RadarEntity = MyAPIGateway.Entities.GetEntityById(RadarInfo.EntityId);
            RadarableGrid rgrid = null;
            bool HasMarker = false;
            if (RadarEntity.TryGetComponent(out rgrid))
            {
                if (Radar.ShowWorkingGridsOnly && !rgrid.IsWorkingGrid) return false;
                HasMarker = rgrid.MarkerRange >= Radar.Position.DistanceTo(RadarInfo.Position);
            }
            if (HasMarker) return true;
            if (RadarInfo.Relationship.IsFriendly() && Radar.ShowOnlyHostiles) return false;
            if (RadarInfo.Type == Ingame.MyDetectedEntityType.Asteroid && !Radar.ShowRoids) return false;
            if (RadarInfo.Type == Ingame.MyDetectedEntityType.FloatingObject && !Radar.ShowFloating) return false;

            return true;
        }

        public void RemoveGPSMarkers(bool PurgeAll = false)
        {
            List<long> RemoveKeys = new List<long>();

            foreach (var MarkerPair in RadarMarkers)
            {
                try
                {
                    if (PurgeAll)
                    {
                        MarkerPair.Value.Remove();
                        RadarCore.DebugWrite($"{RadarBlock.CustomName}.RemoveGPSMarkers()", $"Removing marker [{MarkerPair.Value.Name}/{MarkerPair.Value.Description}] -> purging all", true);
                        RemoveKeys.Add(MarkerPair.Key);
                    }
                    else
                    {
                        Ingame.MyDetectedEntityInfo RadarInfo = Radar.DetectedEntities.FirstOrDefault(x => x.EntityId == MarkerPair.Key);
                        if (!Radar.DetectedEntities.Any(x => x.EntityId == RadarInfo.EntityId) || !ShouldMarkerExist(RadarInfo))
                        {
                            MarkerPair.Value.Remove();
                            RadarCore.DebugWrite($"{RadarBlock.CustomName}.RemoveGPSMarkers()", $"Removing marker [{MarkerPair.Value.Name}/{MarkerPair.Value.Description}] -> entity is no longer detected", true);
                            RemoveKeys.Add(MarkerPair.Key);
                        }
                    }
                }
                catch (Exception Scrap)
                {
                    RadarCore.LogError(RadarBlock.CustomName, Scrap);
                }
            }
            RadarMarkers.RemoveAll(RemoveKeys);
        }

        public void AddGPSMarker(Ingame.MyDetectedEntityInfo RadarScan, string EntityName, bool DontAddIfDetectedByRadioMarker = true)
        {
            try
            {
                try
                {
                    //RadarCore.DebugWrite($"{Radar.CustomName}", $"Trying to add marker for entity {RadarScan.Type.ToString()} {RadarScan.Name}");
                    if (Radar.OwnerID == 0) return;
                    if (!Radar.HasOwnerInRelay) return;
                    if (Radar.OwnerGPSes.Any(x => x.Description == $"RadarEntity {RadarScan.EntityId}")) return;
                    if (Radar.OwnerEntity?.GetTopMostParent()?.EntityId == RadarScan.EntityId) return;
                    if (Radar.MyRadarGrid.RelayedGrids.Any(x => x.EntityId == RadarScan.EntityId)) return;
                    if (DontAddIfDetectedByRadioMarker && RadarScan.HitPosition == null) return;
                }
                catch (Exception Scrap)
                {
                    RadarCore.LogError("Radar.AddGPSMarker.GetOwnerPlayer", Scrap);
                    return;
                }

                if (RadarMarkers.ContainsKey(RadarScan.EntityId)) return;
                IMyEntity AttachTo = MyAPIGateway.Entities.GetEntityById(RadarScan.EntityId);
                if (AttachTo == null) return;
                StringBuilder MarkerName = new StringBuilder();
                if (RadarScan.Type == Ingame.MyDetectedEntityType.Asteroid && EntityName == "Asteroid")
                {
                    MarkerName.Append("[Asteroid]");
                }
                else if (RadarScan.IsGrid() && (EntityName.StartsWith("Large Grid") || EntityName.StartsWith("Small Grid")))
                {
                    if (RadarScan.Type == Ingame.MyDetectedEntityType.LargeGrid)
                        MarkerName.Append("[Large Grid]");
                    else
                        MarkerName.Append("[Small Grid]");
                }
                else
                {
                    MarkerName.Append($"[{RadarScan.Type.ToString()}{(RadarScan.IsAllied() ? $" | {RadarScan.Name.Truncate(50)}" : $"{(!string.IsNullOrWhiteSpace(EntityName) ? " | " + EntityName.Truncate(50) : "")}")}]");
                }
                GPSMarker Marker = GPSMarker.Create(AttachTo, MarkerName.ToString(), $"RadarEntity {RadarScan.EntityId}", RadarScan.GetRelationshipColor(), Radar.OwnerID);
                RadarMarkers.Add(RadarScan.EntityId, Marker);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("Radar.AddGPSMarker", Scrap);
            }
        }

        public class GPSMarker
        {
            public IMyEntity AttachedEntity { get; private set; }
            public string Name { get; private set; }
            public string Description { get; private set; }
            public long OwnerPlayerID { get; private set; }
            public bool Valid { get; private set; }
            public Color GPSColor { get; private set; }
            private GPSMarker() { }
            public static GPSMarker Create(IMyEntity AttachTo, string Name, string Description, Color MarkerColor, long PlayerID)
            {
                if (AttachTo == null) return null;
                GPSMarker Marker = new GPSMarker();
                Marker.Name = Name;
                Marker.Description = Description;
                Marker.GPSColor = MarkerColor;
                Marker.OwnerPlayerID = PlayerID;
                AttachTo.EnsureName();

                MyVisualScriptLogicProvider.AddGPSToEntity(AttachTo.Name, Name, Description, MarkerColor, PlayerID);
                Marker.Valid = true;
                return Marker;
            }

            public void Remove()
            {
                try
                {
                    if (AttachedEntity == null)
                    {
                        RadarCore.DebugWrite($"GPSMarker[{Name}].Remove", "AttachedEntity is already removed");
                    }
                    try
                    {
                        MyVisualScriptLogicProvider.RemoveGPSFromEntity(AttachedEntity.Name, Name, Description, OwnerPlayerID);
                    }
                    catch (Exception Scrap)
                    {
                        RadarCore.LogError($"GPSMarker[{Name}].Remove_VSLP", Scrap);
                    }
                    List<IMyGps> GPSes = MyAPIGateway.Session.GPS.GetGpsList(OwnerPlayerID);
                    IMyGps OurGPS;
                    if (GPSes.Any(x => x.Name == Name && x.Description == Description, out OurGPS))
                        MyAPIGateway.Session.GPS.RemoveGps(OwnerPlayerID, OurGPS);
                    Valid = false;
                }
                catch (Exception Scrap)
                {
                    RadarCore.LogError($"GPSMarker[{Name}].Remove", Scrap);
                }
            }
        }
    }
}
