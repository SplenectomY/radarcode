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
    public static class Controls
    {
        public static bool IsRadar(IMyTerminalBlock Radar)
        {
            try
            {
                bool isradar = Radar.HasComponent<MyRadar>();
                //DebugWrite("IsRadar", $"Block {Radar.CustomName} asked; result={isradar}", IsExcessive: true);
                return isradar;
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("IsRadar", Scrap);
                return false;
            }
        }

        public static void RadarAction(IMyTerminalBlock RadarBlock, Action<MyRadar> Action)
        {
            try
            {
                MyRadar Radar;
                if (!RadarBlock.TryGetComponent(out Radar)) return;
                Action(Radar);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("RadarAction", Scrap);
                return;
            }
        }

        public static T RadarReturn<T>(IMyTerminalBlock RadarBlock, Func<MyRadar, T> Getter, T Default = default(T))
        {
            try
            {
                MyRadar Radar;
                if (!RadarBlock.TryGetComponent(out Radar)) return Default;
                return Getter(Radar);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("RadarReturn", Scrap);
                return Default;
            }
        }

        public static bool InitedRadarControls { get; private set; } = false;
        public static void InitRadarControls()
        {
            if (InitedRadarControls) return;
            #region Printer
            try
            {
                var EntityPrinter = MyAPIGateway.TerminalControls.CreateProperty<HashSet<Ingame.MyDetectedEntityInfo>, IMyUpgradeModule>("RadarData");
                EntityPrinter.Enabled = IsRadar;
                EntityPrinter.Getter = Block => RadarReturn(Block, Radar => new HashSet<Ingame.MyDetectedEntityInfo>(Radar.DetectedEntities));
                EntityPrinter.Setter = (Block, trash) =>
                {
                    if (!IsRadar(Block)) throw new Exception("Block isn't a Radar.");
                    throw new Exception("The RadarData property is read-only.");
                };
                if (MyAPIGateway.Session.IsServer) MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(EntityPrinter);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.EntityPrinter", Scrap);
            }
            #endregion

            #region CanScan
            try
            {
                var CanScanAction = MyAPIGateway.TerminalControls.CreateProperty<Func<Ingame.MyDetectedEntityInfo, bool>, IMyUpgradeModule>("CanScan");
                CanScanAction.Enabled = IsRadar;
                CanScanAction.Getter = Block =>
                {
                    MyRadar Radar;
                    if (Block.TryGetComponent(out Radar))
                    {
                        return Radar.ScanModule.CanScan;
                    }
                    return null;
                };
                CanScanAction.Setter = (Block, trash) =>
                {
                    if (!IsRadar(Block)) throw new Exception("Block isn't a Radar.");
                    throw new Exception("The ScanTarget property is a function and cannot be set.");
                };
                if (MyAPIGateway.Session.IsServer) MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(CanScanAction);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.CanScanTarget", Scrap);
            }
            #endregion

            #region ScanTarget
            try
            {
                var ScanAction = MyAPIGateway.TerminalControls.CreateProperty<Func<Ingame.MyDetectedEntityInfo, List<Dictionary<string, string>>>, IMyUpgradeModule>("ScanTarget");
                ScanAction.Enabled = IsRadar;
                ScanAction.Getter = Block =>
                {
                    MyRadar Radar;
                    if (Block.TryGetComponent(out Radar))
                    {
                        return Radar.ScanModule.ScanTarget;
                    }
                    return null;
                };
                ScanAction.Setter = (Block, trash) =>
                {
                    if (!IsRadar(Block)) throw new Exception("Block isn't a Radar.");
                    throw new Exception("The ScanTarget property is a function and cannot be set.");
                };
                if (MyAPIGateway.Session.IsServer) MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(ScanAction);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.ScanTarget", Scrap);
            }
            #endregion

            #region Power Slider
            try
            {
                var PowerSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyUpgradeModule>("RadarPower");
                PowerSlider.Enabled = IsRadar;
                PowerSlider.Visible = IsRadar;

                PowerSlider.Title = MyStringId.GetOrCompute("Radar Power");
                PowerSlider.Tooltip = MyStringId.GetOrCompute("Supplied power determines how much power is used in Active Radar mode.");

                PowerSlider.SetLogLimits((Block) => 800, (Block) => RadarReturn(Block, Radar => Radar.MaxPower, 800));

                PowerSlider.Getter = (Block) => RadarReturn(Block, Radar => Radar.RadarPower.Get(), 800);
                PowerSlider.Setter = (Block, Value) => RadarAction(Block, Radar => Radar.RadarPower.Set(Value));
                PowerSlider.Writer = (Block, Info) => RadarAction(Block, Radar => { if (Radar.ActiveRadar) Info.Append($"{Math.Round(Radar.RadarPower.Get())} kW"); else Info.Append($"PASSIVE (800 kW)"); });

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(PowerSlider);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.PowerSlider", Scrap);
            }
            #endregion

            #region Active Mode
            try
            {
                var ActiveMode = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyUpgradeModule>("ActiveMode");
                ActiveMode.Enabled = Block => IsRadar(Block);
                ActiveMode.Visible = Block => IsRadar(Block);

                ActiveMode.Title = MyStringId.GetOrCompute("Active Mode");
                ActiveMode.Tooltip = MyStringId.GetOrCompute("Enables Radar's Active Mode. In Active Mode, Radar will actively scan its surroundings with high-power radiowaves.\nThis allows to vastly improve detection ratio, but also makes you visible to other Radars.");
                ActiveMode.OnText = MyStringId.GetOrCompute("ACT");
                ActiveMode.OffText = MyStringId.GetOrCompute("PSV");

                ActiveMode.Getter = Block => RadarReturn(Block, Radar => Radar.ActiveRadar, false);
                ActiveMode.Setter = (Block, Value) => RadarAction(Block, Radar => Radar.ActiveRadar.Set(Value));

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(ActiveMode);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.ActiveMode", Scrap);
            }
            #endregion

            #region Show Markers
            try
            {
                var ShowMarkers = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyUpgradeModule>("ShowMarkers");
                ShowMarkers.Enabled = Block => IsRadar(Block);
                ShowMarkers.Visible = Block => IsRadar(Block);

                ShowMarkers.Title = MyStringId.GetOrCompute("Show Markers");
                ShowMarkers.Tooltip = MyStringId.GetOrCompute("If you are within the antenna network associated with your Radar,\nit will show detected entities as GPS markers.");
                ShowMarkers.OnText = MyStringId.GetOrCompute("On");
                ShowMarkers.OffText = MyStringId.GetOrCompute("Off");

                ShowMarkers.Getter = Block => RadarReturn(Block, Radar => Radar.ShowMarkers.Get(), false);
                ShowMarkers.Setter = (Block, Value) => RadarAction(Block, Radar => Radar.ShowMarkers.Set(Value));

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(ShowMarkers);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.ShowMarkers", Scrap);
            }
            #endregion

            #region Show Roids
            try
            {
                var ShowRoids = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyUpgradeModule>("ShowRoids");
                ShowRoids.Enabled = Block => IsRadar(Block);
                ShowRoids.Visible = Block => IsRadar(Block);

                ShowRoids.Title = MyStringId.GetOrCompute("Show Asteroids");
                ShowRoids.Tooltip = MyStringId.GetOrCompute("If enabled, Radar will show detected asteroids.");
                ShowRoids.OnText = MyStringId.GetOrCompute("On");
                ShowRoids.OffText = MyStringId.GetOrCompute("Off");

                ShowRoids.Getter = Block => RadarReturn(Block, Radar => Radar.ShowRoids.Get(), false);
                ShowRoids.Setter = (Block, Value) => RadarAction(Block, Radar => Radar.ShowRoids.Set(Value));

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(ShowRoids);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.ShowRoids", Scrap);
            }
            #endregion

            #region Show Only Working Grids
            try
            {
                var ShowOnlyHostiles = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyUpgradeModule>("ShowWorkingGridsOnly");
                ShowOnlyHostiles.Enabled = Block => IsRadar(Block);
                ShowOnlyHostiles.Visible = Block => IsRadar(Block);

                ShowOnlyHostiles.Title = MyStringId.GetOrCompute("Show Working Grids Only");
                ShowOnlyHostiles.Tooltip = MyStringId.GetOrCompute("If enabled, the Radar will filter out non-functional grids.\nFor grid to be considered functional, it must have a functional ship controller, a power source and either be stationary\nor have at least one gyro and one thruster.");
                ShowOnlyHostiles.OnText = MyStringId.GetOrCompute("On");
                ShowOnlyHostiles.OffText = MyStringId.GetOrCompute("Off");

                ShowOnlyHostiles.Getter = Block => RadarReturn(Block, Radar => Radar.ShowWorkingGridsOnly.Get(), false);
                ShowOnlyHostiles.Setter = (Block, Value) => RadarAction(Block, Radar => Radar.ShowWorkingGridsOnly.Set(Value));

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(ShowOnlyHostiles);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.ShowOnlyHostiles", Scrap);
            }
            #endregion

            #region Show Only Hostiles
            try
            {
                var ShowOnlyHostiles = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyUpgradeModule>("ShowOnlyHostiles");
                ShowOnlyHostiles.Enabled = Block => IsRadar(Block);
                ShowOnlyHostiles.Visible = Block => IsRadar(Block);

                ShowOnlyHostiles.Title = MyStringId.GetOrCompute("Show Only Hostiles");
                ShowOnlyHostiles.Tooltip = MyStringId.GetOrCompute("If enabled, markers are limited only to hostile targets.");
                ShowOnlyHostiles.OnText = MyStringId.GetOrCompute("On");
                ShowOnlyHostiles.OffText = MyStringId.GetOrCompute("Off");

                ShowOnlyHostiles.Getter = Block => RadarReturn(Block, Radar => Radar.ShowOnlyHostiles.Get(), false);
                ShowOnlyHostiles.Setter = (Block, Value) => RadarAction(Block, Radar => Radar.ShowOnlyHostiles.Set(Value));

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(ShowOnlyHostiles);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.ShowOnlyHostiles", Scrap);
            }
            #endregion

            #region Show Floating
            try
            {
                var ShowFloating = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyUpgradeModule>("ShowFloating");
                ShowFloating.Enabled = Block => IsRadar(Block);
                ShowFloating.Visible = Block => IsRadar(Block);

                ShowFloating.Title = MyStringId.GetOrCompute("Show Floating Objects");
                ShowFloating.Tooltip = MyStringId.GetOrCompute("If enabled, all floating objects will be shown.");
                ShowFloating.OnText = MyStringId.GetOrCompute("On");
                ShowFloating.OffText = MyStringId.GetOrCompute("Off");

                ShowFloating.Getter = Block => RadarReturn(Block, Radar => Radar.ShowFloating.Get(), false);
                ShowFloating.Setter = (Block, Value) => RadarAction(Block, Radar => Radar.ShowFloating.Set(Value));

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(ShowFloating);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.ShowOnlyHostiles", Scrap);
            }
            #endregion

            #region IsScanReady
            try
            {
                var IsScanReady = MyAPIGateway.TerminalControls.CreateProperty<bool, IMyUpgradeModule>("IsScanReady");
                IsScanReady.Enabled = IsRadar;
                IsScanReady.Getter = Block => RadarReturn(Block, Radar => Radar.ScanModule.IsScanReady);
                IsScanReady.Setter = (Block, trash) =>
                {
                    if (!IsRadar(Block)) throw new Exception("Block isn't a Radar.");
                    throw new Exception("The RadarData property is read-only.");
                };
                if (MyAPIGateway.Session.IsServer) MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(IsScanReady);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.EntityPrinter", Scrap);
            }
            #endregion

            #region Timestamp
            try
            {
                var Timestamp = MyAPIGateway.TerminalControls.CreateProperty<long, IMyProgrammableBlock>("CurrentTime");
                Timestamp.Enabled = Block => true;
                Timestamp.Getter = Block =>
                {
                    var Info = Sandbox.Game.Entities.MyDetectedEntityInfoHelper.Create(Block.CubeGrid as VRage.Game.Entity.MyEntity, Block.OwnerId);
                    return Info.TimeStamp;
                };
                Timestamp.Setter = (Block, trash) =>
                {
                    throw new Exception("The CurrentTime property is read-only.");
                };
                MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(Timestamp);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError("InitControls.Timestamp", Scrap);
            }
            #endregion

            RadarCore.DebugWrite("InitControls", "Controls inited.");
            InitedRadarControls = true;
        }
    }
}
