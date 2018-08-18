using Cheetah.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Ingame = Sandbox.ModAPI.Ingame;

namespace Cheetah.Radars
{
    public class AdvTurretBase : MyGameLogicComponent
    {
        public IMyLargeTurretBase Turret => Entity as IMyLargeTurretBase;
        Vector3D TurretPosition => Turret.GetPosition();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (!InitedTurretControls) InitTurretControls();
        }

        public static bool InitedTurretControls { get; protected set; } = false;
        public static void InitTurretControls()
        {
            if (InitedTurretControls) return;
            if (!MyAPIGateway.Session.IsServer) return;
            var GetTarget = MyAPIGateway.TerminalControls.CreateProperty<Ingame.MyDetectedEntityInfo, IMyLargeTurretBase>("CurrentTarget");
            GetTarget.Enabled = Block =>
            {
                IMyLargeTurretBase Turret = Block as IMyLargeTurretBase;
                return Turret.AIEnabled;
            };
            GetTarget.Getter = Block =>
            {
                IMyLargeTurretBase Turret = Block as IMyLargeTurretBase;
                if (!Turret.HasTarget) return new Ingame.MyDetectedEntityInfo();
                Ingame.MyDetectedEntityInfo Target = Sandbox.Game.Entities.MyDetectedEntityInfoHelper.Create(Turret.Target as VRage.Game.Entity.MyEntity, Block.OwnerId, null);
                return Target;
            };
            GetTarget.Setter = (Block, trash) =>
            {
                throw new Exception("The CurrentTarget property is read-only. Invoke the TrackTarget property-function to set a target.");
            };
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(GetTarget);

            var SetTarget = MyAPIGateway.TerminalControls.CreateProperty<Func<Ingame.MyDetectedEntityInfo, long, bool>, IMyLargeTurretBase>("TrackSubtarget");
            SetTarget.Enabled = Block =>
            {
                IMyLargeTurretBase Turret = Block as IMyLargeTurretBase;
                return Turret.AIEnabled;
            };
            SetTarget.Getter = Block =>
            {
                try
                {
                    AdvTurretBase Turret;
                    if (Block.TryGetComponent(out Turret))
                    {
                        return Turret.TrackTargetAction;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception Scrap)
                {
                    throw new Exception($"Exception is SetTarget.Getter: {Scrap.Message}");
                }
            };
            SetTarget.Setter = (Block, trash) =>
            {
                throw new Exception("The TrackSubtarget property is a function and cannot be set.");
            };
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(SetTarget);

            var SetTargetPickSubtarget = MyAPIGateway.TerminalControls.CreateProperty<Func<Ingame.MyDetectedEntityInfo, bool>, IMyLargeTurretBase>("TrackTarget");
            SetTargetPickSubtarget.Enabled = Block =>
            {
                IMyLargeTurretBase Turret = Block as IMyLargeTurretBase;
                return Turret.AIEnabled;
            };
            SetTargetPickSubtarget.Getter = Block =>
            {
                AdvTurretBase Turret;
                if (Block.TryGetComponent(out Turret))
                {
                    return Turret.TrackTargetPickSubtargetAction;
                }
                else
                {
                    return null;
                }
            };
            SetTargetPickSubtarget.Setter = (Block, trash) =>
            {
                throw new Exception("The TrackTarget property is a function and cannot be set.");
            };
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(SetTargetPickSubtarget);

            var ResetTargetingProp = MyAPIGateway.TerminalControls.CreateProperty<Action, IMyLargeTurretBase>("ResetTargeting");
            ResetTargetingProp.Enabled = Block =>
            {
                IMyLargeTurretBase Turret = Block as IMyLargeTurretBase;
                return Turret.AIEnabled;
            };
            ResetTargetingProp.Getter = Block =>
            {
                AdvTurretBase Turret;
                if (Block.TryGetComponent(out Turret))
                {
                    return Turret.ResetTargeting;
                }
                else
                {
                    return null;
                }
            };
            ResetTargetingProp.Setter = (Block, trash) =>
            {
                throw new Exception("The ResetTargeting property is a function and cannot be set.");
            };
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(ResetTargetingProp);
            InitedTurretControls = true;
        }

        public Func<Ingame.MyDetectedEntityInfo, long, bool> TrackTargetAction => TrackTarget;
        protected bool TrackTarget(Ingame.MyDetectedEntityInfo Target, long SubtargetID)
        {
            if (TurretPosition.DistanceTo(Target.Position) > Turret.Range) return false;
            IMyCubeGrid Grid = MyAPIGateway.Entities.GetEntityById(Target.EntityId) as IMyCubeGrid;
            if (Grid == null) return false;
            IMyTerminalBlock Block = null;
            List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
            Grid.GetTerminalSystem().GetBlocks(Blocks);
            Block = Blocks.FirstOrDefault(x => x.EntityId == SubtargetID);
            if (Block == null || TurretPosition.DistanceTo(Block.GetPosition()) > Turret.Range) return false;
            Turret.TrackTarget(Block);
            return true;
        }

        public Func<Ingame.MyDetectedEntityInfo, bool> TrackTargetPickSubtargetAction => TrackTargetPickSubtarget;
        protected bool TrackTargetPickSubtarget(Ingame.MyDetectedEntityInfo Target)
        {
            if (TurretPosition.DistanceTo(Target.Position) > Turret.Range) return false;
            if (!Target.IsGrid())
            {
                IMyEntity TargetEntity = MyAPIGateway.Entities.GetEntityById(Target.EntityId);
                if (TurretPosition.DistanceTo(TargetEntity.GetPosition()) > Turret.Range) return false;
                Turret.TrackTarget(TargetEntity);
                return true;
            }
            IMyCubeGrid Grid = MyAPIGateway.Entities.GetEntityById(Target.EntityId) as IMyCubeGrid;
            if (Grid == null) return false;
            List<IMyTerminalBlock> TermBlocks = new List<IMyTerminalBlock>();
            IMyGridTerminalSystem Term = Grid.GetTerminalSystem();
            if (Term == null) return false;
            Term.GetBlocks(TermBlocks);
            TermBlocks.RemoveAll(x => !x.IsFunctional);
            if (!TermBlocks.Any())
            {
                Turret.TrackTarget(Grid);
                return true;
            }

            var PrioritizedBlocks = TermBlocks.OrderByDescending(x => PriorityIndex(x)).ThenBy(x => DistanceSq(x.GetPosition()));
            Turret.TrackTarget(PrioritizedBlocks.First());
            return true;
        }

        public void ResetTargeting()
        {
            Turret.ResetTargetingToDefault();
        }

        float DistanceSq(Vector3D Point)
        {
            return (float)Vector3D.DistanceSquared(TurretPosition, Point);
        }

        int PriorityIndex(IMyTerminalBlock Block)
        {
            IMyDecoy Decoy;
            if (Block.IsOfType(out Decoy))
            {
                // A turret doesn't know what kind of destructive device it can be
                return 5000;
            }

            IMySmallGatlingGun Gun;
            IMySmallMissileLauncher Launcher;
            if (Block.IsOfType(out Launcher) || Block.IsOfType(out Gun))
            {
                Vector3D GunToMe = Vector3D.Normalize(TurretPosition - Block.GetPosition());
                Vector3D GunForward = Vector3D.Normalize(Block.WorldMatrix.Forward);

                if (Vector3D.Dot(GunToMe, GunForward) >= 0.7f)
                    return 1100;
                else return 300;
            }

            IMyLargeTurretBase Turret;
            if (Block.IsOfType(out Turret))
            {
                int TurretIndex = 100;
                if (Turret.HasComponent<AdvTurretMissile>()) TurretIndex = 800;
                if (Turret.HasComponent<AdvTurretGatling>()) TurretIndex = 500;
                if (Turret.HasComponent<AdvTurretInterior>()) TurretIndex = 100;
                if (Turret.Target.GetTopMostParent() != this.Turret.GetTopMostParent()) TurretIndex /= 2;
                return TurretIndex;
            }

            IMyJumpDrive Drive;
            if (Block.IsOfType(out Drive))
            {
                if (Drive.Status == Ingame.MyJumpDriveStatus.Jumping) return 2000;
                if (Drive.Status == Ingame.MyJumpDriveStatus.Ready) return 400;
                return 200;
            }

            IMyWarhead Warhead;
            if (Block.IsOfType(out Warhead))
            {
                return 1800;
            }

            IMyShipController Controller;
            if (Block.IsOfType(out Controller))
            {
                if (Controller.IsUnderControl) return 480; else return 100;
            }

            return 10;
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeGatlingTurret), false)]
    public class AdvTurretGatling : AdvTurretBase { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeMissileTurret), false)]
    public class AdvTurretMissile : AdvTurretBase { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_InteriorTurret), false)]
    public class AdvTurretInterior : AdvTurretBase { }
}