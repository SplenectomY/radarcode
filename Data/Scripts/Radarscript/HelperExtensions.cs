using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Cheetah.API
{
    public static class OwnershipTools
    {
        public static long PirateID
        {
            get
            {
                return MyVisualScriptLogicProvider.GetPirateId();
            }
        }

        public static bool IsOwnedByPirates(this IMyTerminalBlock Block)
        {
            return Block.OwnerId == PirateID;
        }

        public static bool IsOwnedByNPC(this IMyTerminalBlock Block)
        {
            if (Block.IsOwnedByPirates()) return true;
            IMyFaction Faction;
            if (MyAPIGateway.Session.Factions.Factions.Values.Any(x => x.IsMember(Block.OwnerId), out Faction))
            {
                return Faction.IsEveryoneNpc();
            }
            return false;
        }

        public static bool IsPirate(this IMyCubeGrid Grid)
        {
            return Grid.BigOwners.Contains(PirateID);
        }

        /*public static bool IsNPC(this IMyCubeGrid Grid)
        {
            if (Grid.IsPirate()) return true;
            if (Grid.BigOwners.Count == 0) return false;
            return AISessionCore.NPCIDs.Contains(Grid.BigOwners.First());
        }*/
    }

    public static class TerminalExtensions
    {
        public static IMyGridTerminalSystem GetTerminalSystem(this IMyCubeGrid Grid)
        {
            return MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Grid);
        }

        public static List<T> GetBlocksOfType<T>(this IMyGridTerminalSystem Term, Func<T, bool> collect = null) where T : class, Sandbox.ModAPI.Ingame.IMyTerminalBlock
        {
            if (Term == null) throw new Exception("GridTerminalSystem is null!");
            List<T> TermBlocks = new List<T>();
            Term.GetBlocksOfType(TermBlocks, collect);
            return TermBlocks;
        }

        public static List<T> GetWorkingBlocks<T>(this IMyCubeGrid Grid, bool OverrideEnabledCheck = false, Func<T, bool> collect = null) where T : class, IMyTerminalBlock
        {
            if (Grid == null) return new List<T>();
            List<IMySlimBlock> slimBlocks = new List<IMySlimBlock>();
            List<T> Blocks = new List<T>();
            Grid.GetBlocks(slimBlocks, (x) => x != null && x is T && (!OverrideEnabledCheck ? (x as IMyTerminalBlock).IsWorking : (x as IMyTerminalBlock).IsFunctional));

            if (slimBlocks.Count == 0) return new List<T>();
            foreach (var _block in slimBlocks)
                if (collect == null || collect(_block as T)) Blocks.Add(_block as T);

            return Blocks;
        }

        public static void Trigger(this IMyTimerBlock Timer)
        {
            Timer.GetActionWithName("TriggerNow").Apply(Timer);
        }
    }

    public static class VectorExtensions
    {
        public static float DistanceTo(this Vector3D From, Vector3D To)
        {
            return (float)(To - From).Length();
        }

        public static Vector3D LineTowards(this Vector3D From, Vector3D To, double Length)
        {
            return From + (Vector3D.Normalize(To - From) * Length);
        }

        public static Vector3D InverseVectorTo(this Vector3D From, Vector3D To, double Length)
        {
            return From + (Vector3D.Normalize(From - To) * Length);
        }

        public static bool Intersect(this BoundingSphereD Sphere, ref LineD line, out LineD intersectedLine)
        {
            var ray = new RayD(line.From, line.Direction);

            double t1, t2;
            if (!Sphere.IntersectRaySphere(ray, out t1, out t2))
            {
                intersectedLine = line;
                return false;
            }

            t1 = Math.Max(t1, 0);
            t2 = Math.Min(t2, line.Length);

            intersectedLine.From = line.From + line.Direction * t1;
            intersectedLine.To = line.From + line.Direction * t2;
            intersectedLine.Direction = line.Direction;
            intersectedLine.Length = t2 - t1;

            return true;
        }
    }

    public static class GamelogicHelpers
    {
        public static ComponentType GetComponent<ComponentType>(this IMyEntity Entity) where ComponentType : MyEntityComponentBase
        {
            if (Entity == null || Entity.Components == null) return null;
            return Entity.Components.Has<ComponentType>() ? Entity.Components.Get<ComponentType>() : Entity.GameLogic.GetAs<ComponentType>();
        }

        public static bool TryGetComponent<ComponentType>(this IMyEntity Entity, out ComponentType Component) where ComponentType : MyEntityComponentBase
        {
            Component = GetComponent<ComponentType>(Entity);
            return Component != null;
        }

        public static bool HasComponent<ComponentType>(this IMyEntity Entity) where ComponentType : MyEntityComponentBase
        {
            var Component = GetComponent<ComponentType>(Entity);
            return Component != null;
        }

        /// <summary>
        /// (c) Phoera
        /// </summary>
        public static T EnsureComponent<T>(this IMyEntity entity) where T : MyEntityComponentBase, new()
        {
            return EnsureComponent(entity, () => new T());
        }
        /// <summary>
        /// (c) Phoera
        /// </summary>
        public static T EnsureComponent<T>(this IMyEntity entity, Func<T> factory) where T : MyEntityComponentBase
        {
            T res;
            if (entity.TryGetComponent(out res))
                return res;
            res = factory();
            if (res is MyGameLogicComponent)
            {
                if (entity.GameLogic?.GetAs<T>() == null)
                {
                    //"Added as game logic".ShowNotification();
                    entity.AddGameLogic(res as MyGameLogicComponent);
                    (res as MyGameLogicComponent).Init((MyObjectBuilder_EntityBase)null);
                }
            }
            else
            {
                //"Added as component".ShowNotification();
                entity.Components.Add(res);
                res.Init(null);
            }
            return res;
        }

        public static void EnsureName(this IMyEntity Entity)
        {
            if (!string.IsNullOrWhiteSpace(Entity.Name)) return;
            Entity.Name = $"Entity_{Entity.EntityId}";
            MyAPIGateway.Entities.SetEntityName(Entity);
        }
        public static void AddGameLogic(this IMyEntity entity, MyGameLogicComponent logic)
        {
            var comp = entity.GameLogic as MyCompositeGameLogicComponent;
            if (comp != null)
            {
                entity.GameLogic = MyCompositeGameLogicComponent.Create(new List<MyGameLogicComponent>(2) { comp, logic }, entity as MyEntity);
            }
            else if (entity.GameLogic != null)
            {
                entity.GameLogic = MyCompositeGameLogicComponent.Create(new List<MyGameLogicComponent>(2) { entity.GameLogic as MyGameLogicComponent, logic }, entity as MyEntity);
            }
            else
            {
                entity.GameLogic = logic;
            }
        }
    }

    public static class GridExtensions
    {
        public static bool IsFunctionalGrid(this IMyCubeGrid Grid)
        {
            List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
            Grid.GetTerminalSystem().GetBlocksOfType(Blocks, x => x.IsFunctional);
            bool HasPower = Blocks.Any(x => (x as IMyReactor)?.IsWorking == true || (x as IMyBatteryBlock)?.CurrentStoredPower > 0f);

            return Blocks.Any(x => x is IMyShipController) && HasPower && (Grid.IsStatic || (Blocks.Any(x => x is IMyGyro) && Blocks.Any(x => x is IMyThrust)));
        }

        public static bool IsWorkingGrid(this IMyCubeGrid Grid, bool AllowStations = true, bool AllowUnmanned = true, bool CheckForThrustersInAllDirections = false)
        {
            try
            {
                if (!Grid.InScene) return false;
                //if (Grid.IsTrash()) return false;
                if (!AllowStations && Grid.IsStatic) return false;
                if (!Grid.HasPower()) return false;
                if (AllowUnmanned && !Grid.HasController()) return false;
                if (!AllowUnmanned && !Grid.HasCockpit()) return false;
                if (!Grid.IsStatic && !Grid.HasGyros()) return false;
                if (!CheckForThrustersInAllDirections && !Grid.IsStatic && !Grid.HasAnyThrusters()) return false;
                if (CheckForThrustersInAllDirections && !Grid.IsStatic && !Grid.HasThrustersInEveryDirection()) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Removes "dead" block references from a block list.
        /// </summary>
        /// <param name="StrictCheck">Performs x.IsLive(Strict == true). Generates 2 object builders per every block in list.</param>
        public static void Purge<T>(this IList<T> Enum, bool StrictCheck = false) where T: IMySlimBlock
        {
            Enum = Enum.Where(x => x.IsLive(StrictCheck)).ToList();
        }

        /// <summary>
        /// Removes "dead" block references from a block list.
        /// </summary>
        /// <param name="StrictCheck">Performs x.IsLive(Strict == true). Generates 2 object builders per every block in list.</param>
        public static void PurgeInvalid<T>(this IList<T> Enum, bool StrictCheck = false) where T : IMyCubeBlock
        {
            Enum = Enum.Where(x => x.IsLive(StrictCheck)).ToList();
        }

        public static void GetBlocks(this IMyCubeGrid Grid, HashSet<IMySlimBlock> blocks, Func<IMySlimBlock, bool> collect = null)
        {
            List<IMySlimBlock> cubes = new List<IMySlimBlock>();
            if (blocks == null) blocks = new HashSet<IMySlimBlock>(); else blocks.Clear();
            Grid.GetBlocks(cubes, collect);
            foreach (var block in cubes)
                blocks.Add(block);
        }

        /// <summary>
        /// Check if the given block is a "live" existing block, or a "zombie" reference left after a dead and removed block.
        /// </summary>
        /// <param name="StrictCheck">Performs strict check (checks if block in same place is of same typeid+subtypeid). Generates 2 object builders.</param>
        public static bool IsLive(this IMySlimBlock Block, bool StrictCheck = false)
        {
            if (Block == null) return false;
            if (Block.FatBlock != null && Block.FatBlock.Closed) return false;
            if (Block.IsDestroyed) return false;
            var ThereBlock = Block.CubeGrid.GetCubeBlock(Block.Position);
            if (ThereBlock == null) return false;
            var Builder = Block.GetObjectBuilder();
            var ThereBuilder = ThereBlock.GetObjectBuilder();
            return Builder.TypeId == ThereBuilder.TypeId && Builder.SubtypeId == ThereBuilder.SubtypeId;
        }

        /// <summary>
        /// Check if the given block is a "live" existing block, or a "zombie" reference left after a dead and removed block.
        /// </summary>
        /// <param name="StrictCheck">Performs strict check (checks if block in same place is of same typeid+subtypeid). Generates 2 object builders.</param>
        public static bool IsLive(this IMyCubeBlock Block, bool StrictCheck = false)
        {
            if (Block == null) return false;
            if (Block.Closed) return false;
            if (Block.SlimBlock?.IsDestroyed != false) return false;
            var ThereBlock = Block.CubeGrid.GetCubeBlock(Block.Position);
            if (ThereBlock == null) return false;
            var Builder = Block.GetObjectBuilder();
            var ThereBuilder = ThereBlock.GetObjectBuilder();
            return Builder.TypeId == ThereBuilder.TypeId && Builder.SubtypeId == ThereBuilder.SubtypeId;
        }

        public static float GetBaseMass(this IMyCubeGrid Grid)
        {
            float baseMass, totalMass;
            (Grid as MyCubeGrid).GetCurrentMass(out baseMass, out totalMass);
            return baseMass;
        }

        public static int GetTotalMass(this IMyCubeGrid Grid)
        {
            return (Grid as MyCubeGrid).GetCurrentMass();
        }

        public static bool HasPower(this IMyCubeGrid Grid)
        {
            foreach (IMySlimBlock Reactor in Grid.GetWorkingBlocks<IMyReactor>())
            {
                if (Reactor != null && Reactor.FatBlock.IsWorking) return true;
            }
            foreach (IMySlimBlock Battery in Grid.GetWorkingBlocks<IMyBatteryBlock>())
            {
                if ((Battery as IMyBatteryBlock).CurrentStoredPower > 0f) return true;
            }

            return false;
        }

        public static float GetCurrentReactorPowerOutput(this IMyCubeGrid Grid)
        {
            List<IMyReactor> Reactors = new List<IMyReactor>();
            Grid.GetTerminalSystem().GetBlocksOfType(Reactors, x => x.IsWorking);
            if (Reactors.Count == 0) return 0;

            float SummarizedOutput = 0;
            foreach (var Reactor in Reactors)
                SummarizedOutput += Reactor.CurrentOutput;

            return SummarizedOutput;
        }

        public static float GetMaxReactorPowerOutput(this IMyCubeGrid Grid)
        {
            List<IMyReactor> Reactors = new List<IMyReactor>();
            Grid.GetTerminalSystem().GetBlocksOfType(Reactors, x => x.IsWorking);
            if (Reactors.Count == 0) return 0;

            float SummarizedOutput = 0;
            foreach (var Reactor in Reactors)
                SummarizedOutput += Reactor.MaxOutput;

            return SummarizedOutput;
        }

        public static bool HasCockpit(this IMyCubeGrid Grid)
        {
            return Grid.GetTerminalSystem().GetBlocksOfType<IMyCockpit>().Any(x => x.IsFunctional);
        }

        public static bool HasController(this IMyCubeGrid Grid)
        {
            return Grid.GetTerminalSystem().GetBlocksOfType<IMyShipController>().Any(x => x.IsFunctional);
        }

        public static bool HasGyros(this IMyCubeGrid Grid)
        {
            return Grid.GetWorkingBlocks<IMyGyro>().Count > 0;
        }

        public static bool IsGrid(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo Info)
        {
            return Info.Type == Sandbox.ModAPI.Ingame.MyDetectedEntityType.SmallGrid || Info.Type == Sandbox.ModAPI.Ingame.MyDetectedEntityType.LargeGrid;
        }

        public static HashSet<IMyVoxelMap> GetNearbyRoids(this IMyCubeGrid Grid, float Radius = 3000)
        {
            BoundingSphereD Sphere = new BoundingSphereD(Grid.GetPosition(), Radius);
            HashSet<IMyVoxelMap> Roids = new HashSet<IMyVoxelMap>();
            foreach(var entity in MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref Sphere))
            {
                if (entity is IMyVoxelMap && !(entity is MyPlanet)) Roids.Add(entity as IMyVoxelMap);
            }
            return Roids;
        }

        public static void DisableGyroOverride(this IMyCubeGrid Grid)
        {
            foreach (IMyGyro Gyro in Grid.GetWorkingBlocks<IMyGyro>())
            {
                Gyro.SetValueBool("Override", false);
            }
        }

        public static bool HasAnyThrusters(this IMyCubeGrid Grid)
        {
            return Grid.GetWorkingBlocks<IMyThrust>().Count > 0;
        }

        public static bool HasThrustersInEveryDirection(this IMyCubeGrid Grid, IMyCockpit _cockpit = null)
        {
            IMyCockpit Cockpit = _cockpit != null ? _cockpit : GetFirstCockpit(Grid);
            if (Cockpit == null) return false;
            List<IMyThrust> Thrusters = Grid.GetWorkingBlocks<IMyThrust>();
            if (Thrusters.Count < 6) return false; // There physically can't be a thruster in every direction

            bool HasForwardThrust = false;
            bool HasBackwardThrust = false;
            bool HasUpThrust = false;
            bool HasDownThrust = false;
            bool HasLeftThrust = false;
            bool HasRightThrust = false;

            foreach (IMyThrust Thruster in Grid.GetWorkingBlocks<IMyThrust>())
            {
                if (Thruster.WorldMatrix.Forward == Cockpit.WorldMatrix.Forward) HasForwardThrust = true;
                else if (Thruster.WorldMatrix.Forward == Cockpit.WorldMatrix.Backward) HasBackwardThrust = true;
                else if (Thruster.WorldMatrix.Forward == Cockpit.WorldMatrix.Up) HasUpThrust = true;
                else if (Thruster.WorldMatrix.Forward == Cockpit.WorldMatrix.Down) HasDownThrust = true;
                else if (Thruster.WorldMatrix.Forward == Cockpit.WorldMatrix.Left) HasLeftThrust = true;
                else if (Thruster.WorldMatrix.Forward == Cockpit.WorldMatrix.Right) HasRightThrust = true;
            }

            return HasForwardThrust && HasBackwardThrust && HasUpThrust && HasDownThrust && HasLeftThrust && HasRightThrust;
        }

        public static IMyCockpit GetFirstCockpit(this IMyCubeGrid Grid)
        {
            return Grid.GetWorkingBlocks<IMyCockpit>()[0];
        }

        public static bool Has<T>(this IMyCubeGrid Grid) where T : class, IMyTerminalBlock
        {
            return Grid.GetWorkingBlocks<T>().Count > 0;
        }
    }

    public static class TypeHelpers
    {
        /// <summary>
        /// Checks if the given object is of given type.
        /// </summary>
        public static bool IsOfType<T>(this object Object, out T Casted) where T : class
        {
            Casted = Object as T;
            return Casted != null;
        }

        public static bool IsOfType<T>(this object Object) where T : class
        {
            return Object is T;
        }

        public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> Dict, TKey Key, TValue Value)
        {
            if (Dict.ContainsKey(Key)) Dict[Key] = Value;
            else Dict.Add(Key, Value);
        }

        public static void RemoveAll<TKey, TValue>(this Dictionary<TKey, TValue> Dict, IEnumerable<TKey> RemoveKeys)
        {
            if (RemoveKeys == null || RemoveKeys.Count() == 0) return;
            foreach (TKey Key in RemoveKeys)
                Dict.Remove(Key);
        }

        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> Filter, out TSource First)
        {
            if (source == null || source.Count() == 0)
            {
                First = default(TSource);
                return false;
            }
            First = source.FirstOrDefault(Filter);
            return !First.Equals(default(TSource));
        }

        public static HashSet<T> ToHashSet<T>(this ICollection<T> Enum)
        {
            var Hashset = new HashSet<T>(Enum);
            if (Hashset.Count > 0 && Enum.Count > 0) return Hashset;
            foreach (var Item in Enum)
                Hashset.Add(Item);
            return Hashset;
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> Enum)
        {
            var Hashset = new HashSet<T>(Enum);
            if (Hashset.Count > 0 && Enum.Count() > 0) return Hashset;
            foreach (var Item in Enum)
                Hashset.Add(Item);
            return Hashset;
        }

        /// <summary>
        /// Sorts out an enumerable into lists of different types using a single loop.
        /// <para />
        /// This method is suited for 2 types.
        /// </summary>
        public static void SortByType<TI, TO1, TO2>(this IEnumerable<TI> Collection, ICollection<TO1> Type1, ICollection<TO2> Type2) where TI : class where TO1 : class, TI where TO2 : class, TI
        {
            foreach (TI Item in Collection)
            {
                TO1 Type1Item = Item as TO1;
                TO2 Type2Item = Item as TO2;
                if (Type1Item != null) Type1.Add(Type1Item);
                if (Type2Item != null) Type2.Add(Type2Item);
            }
        }

        /// <summary>
        /// Sorts out an enumerable into lists of different types using a single loop.
        /// <para />
        /// This method is suited for 3 types.
        /// </summary>
        public static void SortByType<TI, TO1, TO2, TO3>(this IEnumerable<TI> Collection, ICollection<TO1> Type1, ICollection<TO2> Type2, ICollection<TO3> Type3) where TI : class where TO1 : class, TI where TO2 : class, TI where TO3 : class, TI
        {
            foreach (TI Item in Collection)
            {
                TO1 Type1Item = Item as TO1;
                TO2 Type2Item = Item as TO2;
                TO3 Type3Item = Item as TO3;
                if (Type1Item != null) Type1.Add(Type1Item);
                if (Type2Item != null) Type2.Add(Type2Item);
                if (Type3Item != null) Type3.Add(Type3Item);
            }
        }

        /// <summary>
        /// Sorts out an enumerable into lists of different types using a single loop.
        /// <para />
        /// This method is suited for 4 types.
        /// </summary>
        public static void SortByType<TI, TO1, TO2, TO3, TO4>(this IEnumerable<TI> Collection, ICollection<TO1> Type1, ICollection<TO2> Type2, ICollection<TO3> Type3, ICollection<TO4> Type4) where TI : class where TO1 : class, TI where TO2 : class, TI where TO3 : class, TI where TO4 : class, TI
        {
            foreach (TI Item in Collection)
            {
                TO1 Type1Item = Item as TO1;
                TO2 Type2Item = Item as TO2;
                TO3 Type3Item = Item as TO3;
                TO4 Type4Item = Item as TO4;
                if (Type1Item != null) Type1.Add(Type1Item);
                if (Type2Item != null) Type2.Add(Type2Item);
                if (Type3Item != null) Type3.Add(Type3Item);
                if (Type4Item != null) Type4.Add(Type4Item);
            }
        }

        public static IList<T> Except<T>(this IList<T> Enum, T Exclude)
        {
            Enum.Remove(Exclude);
            return Enum;
        }

        public static List<T> Except<T>(this List<T> Enum, T Exclude)
        {
            Enum.Remove(Exclude);
            return Enum;
        }

        public static HashSet<T> Except<T>(this HashSet<T> Enum, T Exclude)
        {
            Enum.Remove(Exclude);
            return Enum;
        }
    }


    public static class GeneralExtensions
    {
        public static Vector3 GetGravity(this MyPlanet Planet, Vector3D Position)
        {
            var GravGen = Planet.Components.Get<MyGravityProviderComponent>();
            return GravGen.GetWorldGravity(Position);
        }

        public static bool IsInGravity(this MyPlanet Planet, Vector3D Position)
        {
            var GravGen = Planet.Components.Get<MyGravityProviderComponent>();
            return GravGen.IsPositionInRange(Position);
        }

        public static bool IsAllied(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo Info)
        {
            return Info.Relationship == MyRelationsBetweenPlayerAndBlock.Owner || Info.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare;
        }

        public static Color GetRelationshipColor(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo Info)
        {
            Color retval = Color.Black;
            switch (Info.Relationship)
            {
                case MyRelationsBetweenPlayerAndBlock.Owner:
                    retval = Color.LightBlue;
                    break;

                case MyRelationsBetweenPlayerAndBlock.Neutral:
                    retval = Color.White;
                    break;

                case MyRelationsBetweenPlayerAndBlock.FactionShare:
                    retval = Color.DarkGreen;
                    break;

                case MyRelationsBetweenPlayerAndBlock.Enemies:
                    retval = Color.Red;
                    break;

                case MyRelationsBetweenPlayerAndBlock.NoOwnership:
                    retval = Color.Gray;
                    break;
            }
            return retval;
        }

        public static MyDataReceiver AsNetworker(this IMyRadioAntenna Antenna)
        {
            return Antenna.Components.Get<MyDataReceiver>();
        }

        public static Sandbox.ModAPI.Ingame.MyDetectedEntityInfo Rename(this Sandbox.ModAPI.Ingame.MyDetectedEntityInfo Info, string Name)
        {
            return new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(Info.EntityId, Name, Info.Type, Info.HitPosition, Info.Orientation, Info.Velocity, Info.Relationship, Info.BoundingBox, Info.TimeStamp);
        }

        /// <summary>
        /// Returns world speed cap, in m/s.
        /// </summary>
        public static float GetSpeedCap(this IMyShipController ShipController)
        {
            if (ShipController.CubeGrid.GridSizeEnum == MyCubeSize.Small) return MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;
            if (ShipController.CubeGrid.GridSizeEnum == MyCubeSize.Large) return MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed;
            return 100;
        }

        /// <summary>
        /// Returns world speed cap ratio to default cap of 100 m/s.
        /// </summary>
        public static float GetSpeedCapRatioToDefault(this IMyShipController ShipController)
        {
            return ShipController.GetSpeedCap() / 100;
        }

        public static string Line(this string Str, int LineNumber, string NewlineStyle = "\r\n")
        {
            return Str.Split(NewlineStyle.ToCharArray())[LineNumber];
        }

        public static List<IMyFaction> GetFactions(this IMyFactionCollection FactionCollection)
        {
            List<IMyFaction> AllFactions = new List<IMyFaction>();

            foreach (var FactionBuilder in FactionCollection.GetObjectBuilder().Factions)
            {
                IMyFaction Faction = null;
                Faction = FactionCollection.TryGetFactionById(FactionBuilder.FactionId);
                if (Faction != null) AllFactions.Add(Faction);
            }

            return AllFactions;
        }

        public static bool IsShared(this MyRelationsBetweenPlayerAndBlock Relations)
        {
            return Relations == MyRelationsBetweenPlayerAndBlock.Owner || Relations == MyRelationsBetweenPlayerAndBlock.FactionShare;
        }
    }

    public static class StringExt
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (maxLength < 1) return "";
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length <= maxLength) return value;

            return value.Substring(0, maxLength);
        }
    }
}