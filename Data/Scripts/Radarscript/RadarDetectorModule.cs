using Cheetah.API;
using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Cheetah.Radars
{
    public class RadarDetectorModule : RadarModuleBase
    {
        public RadarDetectorModule(MyRadar Radar) : base(Radar) { }

        /// <summary>
        /// Determines if a given entity is on a free line of sight.
        /// </summary>
        public bool IsInView(IMyEntity Target, out Vector3D? HitPosition)
        {
            HitPosition = null;
            try
            {
                float RayPower = Radar.ActiveRadar ? Radar.PowerModule.EffectiveRadarPower : 800;
                if (Radar.MyRadarGrid == null || Radar.MyRadarGrid.Grid == null)
                {
                    RadarCore.LogError("IsInView", new Exception("Radar's RadarableGrid is null!"), IsExcessive: true);
                    return false;
                }

                LineD LineRay = new LineD(Radar.Position, Target.GetPosition());
                if (LineRay.Length <= RadarCore.GuaranteedDetectionRange) return true;

                List<MyLineSegmentOverlapResult<MyEntity>> Overlaps = new List<MyLineSegmentOverlapResult<MyEntity>>();
                MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref LineRay, Overlaps);
                if (Overlaps == null || Overlaps.Count == 0) return false;

                var TargetTop = Target.GetTopMostParent();
                if (TargetTop == null)
                {
                    RadarCore.LogError("IsInView", new Exception("Target's topmost parent is null!"));
                    return false;
                }
                var RadarTop = Radar.MyRadarGrid.Grid.GetTopMostParent();
                if (TargetTop == null)
                {
                    RadarCore.LogError("IsInView", new Exception("Radar's topmost parent is null!"));
                    return false;
                }

                foreach (var Overlap in Overlaps)
                {
                    try
                    {
                        if (Overlap.Element == null || Overlap.Element.Physics == null) continue;
                        LineD Intersect;
                        var Entity = Overlap.Element as IMyEntity;
                        if (!Entity.WorldAABB.Valid)
                        {
                            RadarCore.DebugWrite("IsInView.Iterate", "Found an entity with invalid WorldAABB. Skipping.", IsExcessive: true);
                            continue;
                        }

                        if (Entity is IMyCubeGrid)
                        {
                            Entity.WorldAABB.Intersect(ref LineRay, out Intersect);
                        }
                        else
                        {
                            Entity.WorldVolume.Intersect(ref LineRay, out Intersect);
                        }

                        var OverlapTop = Entity.GetTopMostParent();
                        if (OverlapTop == null)
                        {
                            RadarCore.DebugWrite("IsInView.Iterate", "Found an entity with invalid topmost parent. Skipping.", IsExcessive: true);
                            continue;
                        }

                        if (OverlapTop is MyPlanet)
                        {
                            MyPlanet Planet = OverlapTop as MyPlanet;
                            if (Planet.HasAtmosphere)
                            {
                                BoundingSphereD Atmosphere = new BoundingSphereD(Planet.PositionComp.GetPosition(), Planet.AtmosphereRadius);
                                LineD AtmoIntersect;
                                Atmosphere.Intersect(ref LineRay, out AtmoIntersect);
                                float Diminish = (float)(AtmoIntersect.Length * RadarCore.AtmoRayDiminishingCoefficient);
                                RayPower -= Diminish;
                                if (RayPower <= 0) return false;
                            }
                            Vector3D TargetPos = Target.GetPosition();
                            if (Vector3D.DistanceSquared(Planet.GetClosestSurfacePointGlobal(ref TargetPos), TargetPos) < 1000 * 1000) return false;
                        }
                        else if (OverlapTop == TargetTop)
                        {
                            HitPosition = Intersect.From;
                            return true;
                        }
                        else if (OverlapTop == RadarTop)
                        {
                            if (OverlapTop.GetPosition().DistanceTo(Radar.Position) > 1000)
                            {
                                List<Vector3I> GridHits = new List<Vector3I>();
                                Radar.MyRadarGrid.Grid.RayCastCells(Radar.Position, LineRay.To, GridHits);
                                if (GridHits == null || GridHits.Count == 0) continue;
                                if (GridHits.Contains(Radar.GridPosition)) GridHits.Remove(Radar.GridPosition);
                                float Diminish = GridHits.Count * (Radar.MyRadarGrid.Grid.GridSizeEnum == MyCubeSize.Large ? 2.5f : 0.5f) * RadarCore.RayDiminishingCoefficient;
                                RayPower -= Diminish;
                                if (RayPower <= 0) return false;
                            }
                        }
                        else
                        {
                            float Diminish = (float)(Intersect.Length * RadarCore.RayDiminishingCoefficient);
                            RayPower -= Diminish;
                            if (RayPower <= 0) return false;
                        }
                    }
                    catch (Exception Scrap)
                    {
                        RadarCore.LogError("IsInView.Iterate", Scrap, IsExcessive: true);
                    }
                }
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("IsInView", Scrap);
            }
            return false;
        }

        public bool CanDetectUsingActiveRadar(RadarableGrid Grid)
        {
            if (!Radar.ActiveRadar) return false;
            float TotalPower = Radar.MyRadarGrid.TotalRadarPower;
            float Distance = Radar.Position.DistanceTo(Grid.Position);
            return Distance <= (TotalPower / 1000 * Grid.ActiveDetectionRate / 40) * RadarCore.RadarEfficiency;
        }

        public bool CanDetectByRadar(RadarableGrid Grid)
        {
            try
            {
                if (Grid.Grid.Physics == null) return false;
                float Distance = Radar.Position.DistanceTo(Grid.Position);

                return Grid.TotalRadarPower >= Distance * 1.5f;
            }
            catch (Exception Scrap)
            {
                RadarCore.DebugWrite($"{RadarBlock.CustomName}.CanDetectByRadar()", $"Crash: {Scrap.Message}", false);
                return false;
            }
        }

        public bool CanDetectByHeat(RadarableGrid Grid)
        {
            try
            {
                if (Grid.Grid.Physics == null) return false;
                float Distance = Radar.Position.DistanceTo(Grid.Position);

                return Grid.ReactorOutput / 5 >= Distance / 1000;
            }
            catch { return false; }
        }

        public bool CanDetectByGravity(RadarableGrid Grid)
        {
            try
            {
                if (Grid.IsInPlanetGravityWell) return false;
                if (Grid.ClosestRoids.Any(x => new BoundingSphereD(x.WorldVolume.Center, x.WorldVolume.Radius * 3).Contains(Grid.Grid.WorldVolume) == ContainmentType.Contains)) return false;
                return Grid.GravityDistortion >= Radar.Position.DistanceTo(Grid.Position);
            }
            catch { return false; }
        }
    }
}
