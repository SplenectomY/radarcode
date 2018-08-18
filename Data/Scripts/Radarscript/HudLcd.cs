using System.Linq;
using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRageMath;

using Draygo.API;
using Cheetah.Networking;
using ProtoBuf;
using System;
using Sandbox.Game.EntityComponents;
using Cheetah.API;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;

namespace Cheetah.Radars
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), false)]
    public sealed class HudLcdDisplayer : MyGameLogicComponent
    {
        const long WORKSHOP_ID = 907384096;
        static HUDTextAPI TextAPI = null;
        public static bool Inited => TextAPI != null;
        public static bool APIAlive => TextAPI != null && TextAPI.Heartbeat;
        static bool InitedControls = false;

        public IMyTextPanel LCD { get; private set; } = null;
        bool LCDAlive => LCD != null && LCD.CubeGrid.Physics != null && LCD.IsWorking;

        HUDTextAPI.HUDMessage Display;
        AutoSet<bool> ShowTextOnHud;
        AutoSet<float> CoordX;
        AutoSet<float> CoordY;
        AutoSet<float> DisplaySize;

        /// <summary>
        /// This gets the block under local player's control. Note that this will be null when player is not controlling a block.
        /// </summary>
        IMyTerminalBlock Controlled => MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity as IMyTerminalBlock;
        bool HasPlayerOnGrid => Controlled != null && Controlled.CubeGrid == LCD.CubeGrid;
        bool HasPlayerInRelay => false;

        static void Initialize()
        {
            if (!Inited) TextAPI = new HUDTextAPI(WORKSHOP_ID);
            if (!InitedControls) InitControls();
        }
        #region Controls
        static void InitControls()
        {
            if (InitedControls) return;
            {
                var ControlShow = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyTextPanel>("ShowTextOnHud");
                ControlShow.Enabled = Block => APIAlive;
                ControlShow.Visible = Block => true;

                ControlShow.Title = MyStringId.GetOrCompute("Show Text on HUD");
                ControlShow.Tooltip = MyStringId.GetOrCompute("Shows public text on HUD interface.");
                ControlShow.OnText = MyStringId.GetOrCompute("On");
                ControlShow.OffText = MyStringId.GetOrCompute("Off");

                ControlShow.Getter = Block => LCDReturn(Block, LCD => LCD.ShowTextOnHud.Get(), false);
                ControlShow.Setter = (Block, Value) => LCDAction(Block, LCD => LCD.ShowTextOnHud.Set(Value));

                MyAPIGateway.TerminalControls.AddControl<IMyTextPanel>(ControlShow);
            }

            {
                var CoordXSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyTextPanel>("CoordX");
                CoordXSlider.Enabled = Block => APIAlive;
                CoordXSlider.Visible = Block => true;

                CoordXSlider.Title = MyStringId.GetOrCompute("Horizontal Position");
                CoordXSlider.Tooltip = MyStringId.GetOrCompute("Controls where to display the LCD text.");

                CoordXSlider.SetLimits(-1, 1);

                CoordXSlider.Getter = (Block) => LCDReturn(Block, LCD => LCD.CoordX.Get(), 0);
                CoordXSlider.Setter = (Block, Value) => LCDAction(Block, LCD => LCD.CoordX.Set(Value));
                CoordXSlider.Writer = (Block, Info) => Info.Append(Math.Round(LCDReturn(Block, LCD => LCD.CoordX.Get(), 0), 2));

                MyAPIGateway.TerminalControls.AddControl<IMyTextPanel>(CoordXSlider);
            }

            {
                var CoordYSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyTextPanel>("CoordY");
                CoordYSlider.Enabled = Block => APIAlive;
                CoordYSlider.Visible = Block => true;

                CoordYSlider.Title = MyStringId.GetOrCompute("Vertical Position");
                CoordYSlider.Tooltip = MyStringId.GetOrCompute("Controls where to display the LCD text.");

                CoordYSlider.SetLimits(-1, 1);

                CoordYSlider.Getter = (Block) => LCDReturn(Block, LCD => LCD.CoordY.Get(), 0);
                CoordYSlider.Setter = (Block, Value) => LCDAction(Block, LCD => LCD.CoordY.Set(Value));
                CoordYSlider.Writer = (Block, Info) => Info.Append(Math.Round(LCDReturn(Block, LCD => LCD.CoordY.Get(), 0), 2));

                MyAPIGateway.TerminalControls.AddControl<IMyTextPanel>(CoordYSlider);
            }

            {
                var DisplaySizeSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyTextPanel>("DisplaySize");
                DisplaySizeSlider.Enabled = Block => APIAlive;
                DisplaySizeSlider.Visible = Block => true;

                DisplaySizeSlider.Title = MyStringId.GetOrCompute("Display Size");
                DisplaySizeSlider.Tooltip = MyStringId.GetOrCompute("Controls size of text on HUD.");

                DisplaySizeSlider.SetLimits(0, 1);

                DisplaySizeSlider.Getter = (Block) => LCDReturn(Block, LCD => LCD.DisplaySize.Get(), 0);
                DisplaySizeSlider.Setter = (Block, Value) => LCDAction(Block, LCD => LCD.DisplaySize.Set(Value));
                DisplaySizeSlider.Writer = (Block, Info) => Info.Append(Math.Round(LCDReturn(Block, LCD => LCD.DisplaySize.Get(), 0), 2));

                MyAPIGateway.TerminalControls.AddControl<IMyTextPanel>(DisplaySizeSlider);
            }
            InitedControls = true;
        }

        public static void LCDAction(IMyTerminalBlock LCDBlock, Action<HudLcdDisplayer> Action)
        {
            try
            {
                HudLcdDisplayer LCD;
                if (!LCDBlock.TryGetComponent(out LCD)) return;
                Action(LCD);
            }
            catch { }
        }

        public static T LCDReturn<T>(IMyTerminalBlock LCDBlock, Func<HudLcdDisplayer, T> Getter, T Default = default(T))
        {
            try
            {
                HudLcdDisplayer LCD;
                if (!LCDBlock.TryGetComponent(out LCD)) return Default;
                return Getter(LCD);
            }
            catch { return Default; }
        }
        #endregion

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            LCD = Entity as IMyTextPanel;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            /*if (!LCD.HasComponent<MyModStorageComponent>())
            {
                LCD.Storage = new MyModStorageComponent();
                LCD.Components.Add(LCD.Storage);
                RadarCore.DebugWrite($"{LCD.CustomName}.Init()", "Block doesn't have a Storage component!", IsExcessive: false);
            }*/
        }

        private void LCD_OnMarkForClose(IMyEntity obj)
        {
            try
            {
                RadarCore.SaveUnregister(Save);
            }
            catch { }
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (!Inited) Initialize();
            RadarCore.SaveRegister(Save);
            LCD.OnMarkForClose += LCD_OnMarkForClose;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            ShowTextOnHud = new AutoSet<bool>(Entity, "ShowTextOnHud", false);
            CoordX = new AutoSet<float>(Entity, "CoordX", 0, x => x<=1 && x>=-1);
            CoordY = new AutoSet<float>(Entity, "CoordY", 0, x => x <= 1 && x >= -1);
            DisplaySize = new AutoSet<float>(Entity, "Size", 0, x => x <= 1 && x >= 0);
            Load();
        }

        public override void UpdateBeforeSimulation10()
        {
            try
            {
                if (!APIAlive || !LCDAlive || !ShowTextOnHud) return;
                if (!HasPlayerOnGrid && !HasPlayerInRelay) return;

                string LCDText = LCD.GetPublicText();

                var FontColor = LCD.GetValueColor("FontColor");
                Display = new HUDTextAPI.HUDMessage(Entity.EntityId, 10, new Vector2D(CoordX, CoordY), DisplaySize, true, false, Color.Black, $"<color={FontColor.R},{FontColor.G},{FontColor.B}>{LCDText}");
                TextAPI.Send(Display);
            }
            catch { }
        }

        #region Loading
        public void Load()
        {
            try
            {
                string Storage = null;
                if (MyAPIGateway.Utilities.GetVariable($"settings_{LCD.EntityId}", out Storage))
                {
                    byte[] Raw = Convert.FromBase64String(Storage);
                    try
                    {
                        RadarCore.DebugWrite($"{LCD.CustomName}.Load()", $"Loading settings. Raw data: {Raw.Count()} bytes", IsExcessive: false);
                        LCDPersistent persistent = MyAPIGateway.Utilities.SerializeFromBinary<LCDPersistent>(Raw);
                        ShowTextOnHud.Set(persistent.ShowTextOnHud);
                        CoordX.Set(persistent.CoordX);
                        CoordY.Set(persistent.CoordY);
                        DisplaySize.Set(persistent.DisplaySize);
                    }
                    catch (Exception Scrap)
                    {
                        RadarCore.LogError($"{LCD.CustomName}.Load()", Scrap);
                    }
                }
                else
                {
                    RadarCore.DebugWrite($"{LCD.CustomName}.Load()", "Storage access failed.", IsExcessive: false);
                }
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError($"{LCD.CustomName}.Load().AccessStorage", Scrap);
            }
        }

        public void Save()
        {
            try
            {
                //RadarCore.DebugWrite($"{RadarBlock.CustomName}.Save()", $"Saving... RadarPower: {Math.Round(RadarPower.Get())}; ShowMarkers: {ShowMarkers.Get()}; ShowRoids: {ShowRoids.Get()}");
                //RemoveGPSMarkers();
                LCDPersistent persistent;
                persistent.ShowTextOnHud = ShowTextOnHud.Get();
                persistent.CoordX = CoordX.Get();
                persistent.CoordY = CoordY.Get();
                persistent.DisplaySize = DisplaySize.Get();
                string Raw = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(persistent));
                MyAPIGateway.Utilities.SetVariable($"settings_{LCD.EntityId}", Raw);
                RadarCore.DebugWrite($"{LCD.CustomName}.Save()", "Saved to storage.", IsExcessive: false);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError($"{LCD.CustomName}.Save()", Scrap);
            }
        }

        [ProtoContract]
        struct LCDPersistent
        {
            [ProtoMember(1)]
            public bool ShowTextOnHud;
            [ProtoMember(2)]
            public float CoordX;
            [ProtoMember(3)]
            public float CoordY;
            [ProtoMember(4)]
            public float DisplaySize;
        }
        #endregion
    }
}
