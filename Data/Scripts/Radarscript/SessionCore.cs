using Cheetah.API;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Ingame = Sandbox.ModAPI.Ingame;

namespace Cheetah.Radars
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class RadarCore : MySessionComponentBase
    {
        /// <summary>
        /// If true, allows some operations to throw exceptions for debug purposes.
        /// <para />
        /// WARNING: if those operations are called from unprotected (outside try..catch) code, they WILL crash the game!
        /// </summary>
        public static readonly bool AllowThrowingErrors = true;
        public static readonly bool Debug = true;
        public static readonly bool VerboseDebug = true;
        public static readonly bool AllowScanningTargets = true;
        /// <summary>
        /// This coefficient is the "stealth coefficient" added by every decoy to the ship.
        /// For ships, active detection range is basic detection range / decoyefficiency ^ numberofdecoys.
        /// </summary>
        public const float DecoyStealthCoefficient = 1.05f;
        public const float DecoyScanDisruptionCoefficient = 1.10f;
        public const int GuaranteedDetectionRange = 1000;
        public const int MaxVisibilityRange = 50000;
        public const int MaxScanningRange = 3000;
        public const float RayDiminishingCoefficient = 1000;
        public const float AtmoRayDiminishingCoefficient = 10;
        public const float AIOwnedRadarPowerConsumptionMultiplier = 0.1f;
        public const float RadarEfficiency = 1;

        public static readonly Guid StorageGuid = new Guid("9AE83342-1BC4-465F-842C-868804BEBF7B");

        static bool Inited = false;
        protected static readonly HashSet<Action> SaveActions = new HashSet<Action>();
        public static void SaveRegister(Action Proc) => SaveActions.Add(Proc);
        public static void SaveUnregister(Action Proc) => SaveActions.Remove(Proc);

        public override void UpdateBeforeSimulation()
        {
            if (!Inited) Init();
        }

        void Init()
        {
            if (Inited || MyAPIGateway.Session == null) return;
            PurgeGPSMarkers();
            try
            {
                Networking.Networker.Init(907384096);
                MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
            }
            catch (Exception Scrap)
            {
                LogError("Init", Scrap);
            }
            Inited = true;
        }

        private static void Entities_OnEntityAdd(IMyEntity obj)
        {
            IMyCubeGrid grid;
            if (!obj.IsOfType(out grid)) return;
            grid.EnsureComponent<RadarableGrid>();
        }

        public override void SaveData()
        {
            foreach (var Proc in SaveActions)
            {
                try
                {
                    Proc.Invoke();
                    var Target = (Proc.Target as MyGameLogicComponent);
                    DebugWrite($"SaveData()", $"Invoking save for {Target?.Entity?.DisplayName} ({Target.GetType().ToString()})");
                }
                catch (Exception Scrap)
                {
                    LogError($"SaveData().{(Proc.Target as MyGameLogicComponent)?.Entity?.DisplayName}", Scrap);
                }
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;
            PurgeGPSMarkers();
            Networking.Networker.Close();
        }

        void PurgeGPSMarkers()
        {
            try
            {
                List<IMyIdentity> Identities = new List<IMyIdentity>();
                MyAPIGateway.Players.GetAllIdentites(Identities);
                foreach (var Player in Identities)
                {
                    long ID = Player.IdentityId;
                    foreach (var Marker in MyAPIGateway.Session.GPS.GetGpsList(ID))
                    {
                        try
                        {
                            if (Marker.Description.Contains("RadarEntity"))
                                MyAPIGateway.Session.GPS.RemoveGps(ID, Marker);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static void DebugWrite(string Source, string Message, bool IsExcessive = false, string DebugPrefix = "RadarCore.")
        {
            try
            {
                if (Debug && (!IsExcessive || VerboseDebug))
                {
                    DebugHelper.DebugWrite($"{DebugPrefix}{Source}", $"Debug message: {Message}");
                }
            }
            catch { }
        }

        public static void LogError(string Source, Exception Scrap, bool IsExcessive = false, string DebugPrefix = "RadarCore.")
        {
            try
            {
                if (Debug && (!IsExcessive || VerboseDebug))
                {
                    DebugHelper.LogError($"{DebugPrefix}{Source}", Scrap);
                }
            }
            catch { }
        }

        public static byte[] StringToBytes(string str)
        {
            return Encoding.Convert(Encoding.Unicode, Encoding.UTF8, Encoding.Unicode.GetBytes(str));
        }

        public static string BytesToString(byte[] bytes)
        {
            return Encoding.Unicode.GetString(Encoding.Convert(Encoding.UTF8, Encoding.Unicode, bytes));
        }

        /// <summary>
        /// Returns max detection range by default rule.
        /// <para />
        /// Default rule is: Entity.WorldVolume.Radius * 400
        /// </summary>
        public static float VisibilityDistanceByDefaultRule(IMyEntity Entity)
        {
            try
            {
                return (float)Entity.GetTopMostParent().WorldVolume.Radius * 300;
            }
            catch (Exception Scrap)
            {
                LogError("VisibilityDistanceByDefaultRule", Scrap);
                return 0;
            }
        }

        public static float GetMassDistortion(IMyCubeGrid Grid)
        {
            return (float)Math.Pow(Math.Pow((Grid.GetTotalMass() / 1000) / (Math.Pow(Math.E, Math.E)), 3), 1 / 2) * 7;
        }
    }

    public static class DebugHelper
    {
        private static readonly List<int> AlreadyPostedMessages = new List<int>();

        public static void Print(string Source, string Message, bool AntiSpam = true)
        {
            string combined = Source + ": " + Message;
            int hash = combined.GetHashCode();

            if (!AlreadyPostedMessages.Contains(hash))
            {
                AlreadyPostedMessages.Add(hash);
                MyAPIGateway.Utilities.ShowMessage(Source, Message);
                VRage.Utils.MyLog.Default.WriteLine($"{Source}: {Message}");
                VRage.Utils.MyLog.Default.Flush();
            }
        }

        public static void DebugWrite(string Source, string Message, bool AntiSpam = true, bool ForceWrite = false)
        {
            if (RadarCore.Debug || ForceWrite) Print(Source, Message);
        }

        public static void LogError(string Source, Exception Scrap, bool AntiSpam = true, bool ForceWrite = false)
        {
            if (!RadarCore.Debug && !ForceWrite) return;
            Print(Source, $"{Scrap.Message}. {(Scrap.InnerException != null ? Scrap.InnerException.Message : "No additional info was given by the game :(")}");
        }
    }
}
