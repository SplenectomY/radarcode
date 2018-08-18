using Cheetah.API;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;
using VRageMath;
using Ingame = Sandbox.ModAPI.Ingame;

namespace Cheetah.Radars
{
    public class GridScanModule : RadarModuleBase
    {
        DateTime LastScan;
        public bool IsScanReady => (DateTime.Now - LastScan) > TimeSpan.FromMilliseconds(1000 / 60 * 10);
        
        public GridScanModule(MyRadar Radar) : base(Radar)
        {
            LastScan = DateTime.Now - TimeSpan.FromSeconds(1);
        }

        public bool CanScan(Ingame.MyDetectedEntityInfo Target)
        {
            try
            {
                if (RadarCore.AllowScanningTargets == false)
                {
                    RadarCore.DebugWrite($"{RadarBlock.CustomName}.CanScan()", $"Scanning disabled in settings");
                    return false;
                }
                if (!Radar.IsWorking())
                {
                    RadarCore.DebugWrite($"{RadarBlock.CustomName}.CanScan()", $"Radar is disabled");
                    return false;
                }
                if (!IsScanReady)
                {
                    RadarCore.DebugWrite($"{RadarBlock.CustomName}.CanScan()", $"Scan cooldown not expired");
                    return false;
                }
                if (Target.IsEmpty())
                {
                    RadarCore.DebugWrite($"{RadarBlock.CustomName}.CanScan()", $"Target struct is empty");
                    return false;
                }
                if (!Radar.DetectedEntities.Any(x => x.EntityId == Target.EntityId))
                {
                    RadarCore.DebugWrite($"{RadarBlock.CustomName}.CanScan()", $"Target not found");
                    return false;
                }

                if (!Target.IsGrid())
                {
                    RadarCore.DebugWrite($"{RadarBlock.CustomName}.CanScan()", $"Target is not a grid");
                    return false;
                }
                IMyCubeGrid Grid = MyAPIGateway.Entities.GetEntityById(Target.EntityId) as IMyCubeGrid;
                if (Grid == null)
                {
                    RadarCore.DebugWrite($"{RadarBlock.CustomName}.CanScan()", $"Cannot resolve EntityID");
                    return false;
                }
                float Distance = Radar.Position.DistanceTo(Target.Position);
                float MaxScanDistance = 3000 / (float)Math.Pow(RadarCore.DecoyScanDisruptionCoefficient, Grid.AsRadarable().DecoysCount);
                if (Distance > RadarCore.GuaranteedDetectionRange && Distance > MaxScanDistance)
                {
                    RadarCore.DebugWrite($"{RadarBlock.CustomName}.CanScan()", $"Out of range: dist={Distance}; scanrange={MaxScanDistance}");
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public List<Dictionary<string, string>> ScanTarget(Ingame.MyDetectedEntityInfo Target)
        {
            if (CanScan(Target) == false) return null;
            List<Dictionary<string, string>> Scan = new List<Dictionary<string, string>>();
            List<IMyTerminalBlock> TargetBlocks = new List<IMyTerminalBlock>();
            IMyCubeGrid Grid = MyAPIGateway.Entities.GetEntityById(Target.EntityId) as IMyCubeGrid;
            Grid.GetTerminalSystem().GetBlocks(TargetBlocks);
            foreach (IMyTerminalBlock Block in TargetBlocks)
            {
                Scan.Add(ReadBlock(Block));
            }
            LastScan = DateTime.Now;
            return Scan;
        }

        Dictionary<string, string> ReadBlock(IMyTerminalBlock Block)
        {
            Dictionary<string, string> BlockReadout = new Dictionary<string, string>();
            if (Block == null) return BlockReadout;
            BlockReadout.Add("Type", Block.BlockDefinition.TypeId.ToString().Replace("MyObjectBuilder_", ""));
            BlockReadout.Add("Subtype", Block.BlockDefinition.SubtypeName);
            BlockReadout.Add("EntityID", Block.EntityId.ToString());
            BlockReadout.Add("Grid", Block.CubeGrid.EntityId.ToString());
            BlockReadout.Add("Enabled", (Block is IMyFunctionalBlock ? (Block as IMyFunctionalBlock).Enabled.ToString() : "null"));
            BlockReadout.Add("Functional", Block.IsFunctional.ToString());
            BlockReadout.Add("WorldPosition", Block.GetPosition().ToString());
            BlockReadout.Add("OwnerID", Block.OwnerId.ToString());
            BlockReadout.Add("Accessible", Block.HasPlayerAccess(RadarBlock.OwnerId).ToString());
            BlockReadout.Add("Occlusion", GetBlockOcclusion(Block).ToString());
            return BlockReadout;
        }

        int GetBlockOcclusion(IMyTerminalBlock Block)
        {
            int Occlusion = 0;
            List<Vector3I> BlockPositions = new List<Vector3I>();
            Block.CubeGrid.RayCastCells(Radar.Position, Block.GetPosition(), BlockPositions, havokWorld: true);
            foreach (var Pos in BlockPositions)
            {
                if (Block.CubeGrid.CubeExists(Pos)) Occlusion += 1;
            }
            return Occlusion;
        }
    }
}
