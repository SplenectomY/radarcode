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
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LaserAntenna), false)]
    public class AntennaLaser : AntennaComms { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RadioAntenna), false)]
    public class AntennaRadio : AntennaComms { }

    public class AntennaComms : MyGameLogicComponent
    {
        public MyDataReceiver Antenna => Entity.GetComponent<MyDataReceiver>();
        public IMyTerminalBlock AntennaBlock => Entity as IMyTerminalBlock;
        public IMyCubeGrid AntennaGrid { get; protected set; }
        public IMyTerminalBlock Block => Entity as IMyTerminalBlock;
        List<MyDataReceiver> RelayedReceivers = new List<MyDataReceiver>();
        public HashSet<IMyCubeGrid> RelayedGrids { get; protected set; } = new HashSet<IMyCubeGrid>();
        public HashSet<Ingame.MyDetectedEntityInfo> RelayedGridsIngame { get; protected set; } = new HashSet<Ingame.MyDetectedEntityInfo>();
        public HashSet<IMyCharacter> RelayedChars { get; protected set; } = new HashSet<IMyCharacter>();
        public bool AllowReceive
        {
            get
            {
                if (SyncAllowReceive == null)
                {
                    RadarCore.LogError($"{AntennaBlock?.CustomData}.AllowReceive.Get", new Exception("SyncAllowReceive object is null"));
                    return false;
                }
                else return SyncAllowReceive.Get();
            }
            set
            {
                if (SyncAllowReceive == null)
                {
                    RadarCore.LogError($"{AntennaBlock?.CustomData}.AllowReceive.Get", new Exception("SyncAllowReceive object is null"));
                }
                else SyncAllowReceive.Set(value);
            }
        }
        AutoSet<bool> SyncAllowReceive;
        Queue<MyAntennaDatagram> Datagrams = new Queue<MyAntennaDatagram>();
        public int DatagramsCount => Datagrams.Count;
        List<string> Protocols = new List<string>();
        public bool UsesProtocols => Protocols.Any();
        public const int DatagramStorageSize = 64;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            if (!Entity.HasComponent<MyModStorageComponent>())
            {
                Entity.Storage = new MyModStorageComponent();
                Entity.Components.Add(Entity.Storage);
                RadarCore.DebugWrite($"{Block.CustomName}.Init()", "Block doesn't have a Storage component!", IsExcessive: false);
            }
        }

        [ProtoContract]
        public struct MyAntennaDatagram
        {
            [ProtoMember(1)]
            public string Data;
            [ProtoMember(2)]
            public string Protocol;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (AntennaBlock.CubeGrid.Physics == null)
            {
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                return;
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            if (AntennaBlock is IMyRadioAntenna && !InitedRadioAntennaControls) InitRadioAntennaControls();
            if (AntennaBlock is IMyLaserAntenna && !InitedLaserAntennaControls) InitLaserAntennaControls();
            SyncAllowReceive = new AutoSet<bool>(Entity, "Receive", true);
            Load();
            AntennaBlock.AppendingCustomInfo += AntennaBlock_AppendingCustomInfo;
            AntennaBlock.OnMarkForClose += AntennaBlock_OnMarkForClose;
            AntennaGrid = AntennaBlock.GetTopMostParent() as IMyCubeGrid;
            RadarCore.SaveRegister(Save);
        }

        private void AntennaBlock_OnMarkForClose(IMyEntity obj)
        {
            try
            {
                AntennaBlock.AppendingCustomInfo -= AntennaBlock_AppendingCustomInfo;
                AntennaBlock.OnMarkForClose -= AntennaBlock_OnMarkForClose;
                RadarCore.SaveUnregister(Save);
            }
            catch { }
        }

        private void AntennaBlock_AppendingCustomInfo(IMyTerminalBlock Block, StringBuilder Info)
        {
            AntennaComms CommComp = Block.GetComponent<AntennaComms>();
            if (CommComp == null) return;
            Info.AppendLine($"Relays ({CommComp.RelayedReceivers.Count}):");
            foreach (var Relayed in CommComp.RelayedReceivers.OrderBy(x => Vector3D.DistanceSquared(AntennaBlock.GetPosition(), x.Broadcaster.Entity.GetPosition())))
            {
                IMyTerminalBlock RelayBlock = Relayed.Entity as IMyTerminalBlock;
                IMyCharacter RelayedChar = Relayed.Entity as IMyCharacter;
                if (RelayBlock != null)
                {
                    Info.AppendLine($"{RelayBlock.CustomName} on {RelayBlock.CubeGrid.DisplayName} ({Math.Round(Vector3D.Distance(Block.GetPosition(), RelayBlock.GetPosition()))}m)");
                }
                else if (RelayedChar != null)
                {
                    Info.AppendLine($"{RelayedChar.DisplayName} ({Math.Round(Vector3D.Distance(Block.GetPosition(), RelayBlock.GetPosition()))}m)");
                }
            }
        }

        #region Init Controls
        public static bool InitedRadioAntennaControls { get; private set; } = false;
        static void InitRadioAntennaControls()
        {
            if (InitedRadioAntennaControls) return;
            AntennaControls.Broadcast<IMyRadioAntenna>();
            AntennaControls.Send<IMyRadioAntenna>();
            AntennaControls.AllowReceive<IMyRadioAntenna>();
            AntennaControls.AllowProtocol<IMyRadioAntenna>();
            AntennaControls.DisallowProtocol<IMyRadioAntenna>();
            AntennaControls.ClearProtocolList<IMyRadioAntenna>();
            AntennaControls.DatagramsStored<IMyRadioAntenna>();
            AntennaControls.AnyDatagrams<IMyRadioAntenna>();
            AntennaControls.ReadFirstDatagram<IMyRadioAntenna>();
            AntennaControls.ReadFirstDatagramByProtocol<IMyRadioAntenna>();
            AntennaControls.ClearDatagramStorage<IMyRadioAntenna>();
            AntennaControls.RelayedGrids<IMyRadioAntenna>();
            InitedRadioAntennaControls = true;
        }

        public static bool InitedLaserAntennaControls { get; private set; } = false;
        static void InitLaserAntennaControls()
        {
            if (InitedLaserAntennaControls) return;
            AntennaControls.Broadcast<IMyLaserAntenna>();
            AntennaControls.Send<IMyLaserAntenna>();
            AntennaControls.AllowReceive<IMyLaserAntenna>();
            AntennaControls.AllowProtocol<IMyLaserAntenna>();
            AntennaControls.DisallowProtocol<IMyLaserAntenna>();
            AntennaControls.ClearProtocolList<IMyLaserAntenna>();
            AntennaControls.DatagramsStored<IMyLaserAntenna>();
            AntennaControls.AnyDatagrams<IMyLaserAntenna>();
            AntennaControls.ReadFirstDatagram<IMyLaserAntenna>();
            AntennaControls.ReadFirstDatagramByProtocol<IMyLaserAntenna>();
            AntennaControls.ClearDatagramStorage<IMyLaserAntenna>();
            AntennaControls.RelayedGrids<IMyLaserAntenna>();
            InitedLaserAntennaControls = true;
        }
        #endregion

        public override void UpdateBeforeSimulation10()
        {
            try
            {
                UpdateRelays();
                AntennaBlock.RefreshCustomInfo();
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError(AntennaBlock.DisplayName + ".Update10().General", Scrap);
            }
        }

        void UpdateRelays()
        {
            IMyCubeGrid OwnGrid = AntennaBlock.GetTopMostParent() as IMyCubeGrid;
            RelayedReceivers.Clear();
            RelayedGridsIngame.Clear();
            RelayedChars.Clear();
            if (Antenna == null) RadarCore.LogError(AntennaBlock.DisplayName + ".Update10()", new Exception("Antenna == null"));
            if (Antenna.BroadcastersInRange == null) RadarCore.LogError(AntennaBlock.DisplayName + ".Update10()", new Exception("BroadcastersInRange == null"));
            if (Antenna.BroadcastersInRange == null) return;
            foreach (var Receiver in Antenna.BroadcastersInRange.Select(x => x.Entity.GetComponent<MyDataReceiver>()))
            {
                TryAddRelay(Receiver);
                if (Receiver?.BroadcastersInRange == null) continue;
                foreach (var RelayedReceiver in Receiver.BroadcastersInRange.Select(x => x.Entity.GetComponent<MyDataReceiver>()))
                {
                    TryAddRelay(RelayedReceiver);
                }
            }
        }

        void TryAddRelay(MyDataReceiver Receiver)
        {
            if (Receiver == null) return;
            if (Receiver == Antenna) return;
            if (RelayedReceivers.Contains(Receiver)) return;
            IMyTerminalBlock ReceiverBlock = (Receiver.Entity as IMyTerminalBlock);
            if (ReceiverBlock != null)
            {
                if (!ReceiverBlock.HasPlayerAccess(AntennaBlock.OwnerId)) return;
                if (!ReceiverBlock.IsWorking) return;
                IMyCubeGrid Grid = ReceiverBlock.CubeGrid.GetTopMostParent() as IMyCubeGrid;
                if (Grid == null || Grid == AntennaGrid) return;

                RelayedReceivers.Add(Receiver);
                if (!RelayedGrids.Any(x => x.EntityId == Grid.EntityId))
                {
                    RelayedGrids.Add(Grid);
                    RelayedGridsIngame.Add(MyDetectedEntityInfoHelper.Create(Grid as MyEntity, AntennaBlock.OwnerId));
                }
            }
            else
            {
                IMyCharacter Char = Receiver.Entity as IMyCharacter;
                if (Char != null)
                {
                    if (Char.IsPlayer && !Char.IsDead && AntennaBlock.HasPlayerAccess(Char.ControllerInfo.ControllingIdentityId)) RelayedChars.Add(Char);
                }
            }
        }

        #region Loading
        public virtual void Load()
        {
            try
            {
                string Storage = null;
                if (MyAPIGateway.Utilities.GetVariable($"settings_{Entity.EntityId}", out Storage))
                {
                    byte[] Raw = Convert.FromBase64String(Storage);
                    try
                    {
                        AntennaPersistent persistent = MyAPIGateway.Utilities.SerializeFromBinary<AntennaPersistent>(Raw);
                        if (persistent.Datagrams != null) Datagrams = new Queue<MyAntennaDatagram>(persistent.Datagrams);
                        if (persistent.Protocols != null) Protocols = persistent.Protocols;
                        SyncAllowReceive.Set(persistent.Receive);
                    }
                    catch (Exception Scrap)
                    {
                        RadarCore.LogError($"{Block.CustomName}.Load()", Scrap);
                    }
                }
                else
                {
                    RadarCore.DebugWrite($"{Block.CustomName}.Load()", "Storage access failed.", IsExcessive: true);
                }
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError($"{Block.CustomName}.Load().AccessStorage", Scrap);
            }
        }

        public virtual void Save()
        {
            try
            {
                AntennaPersistent persistent;
                persistent.Datagrams = Datagrams.ToList();
                persistent.Protocols = Protocols;
                persistent.Receive = AllowReceive;
                string Raw = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(persistent));
                MyAPIGateway.Utilities.SetVariable($"settings_{Block.EntityId}", Raw);
            }
            catch (Exception Scrap)
            {
                RadarCore.LogError($"{Block.CustomName}.Save()", Scrap);
            }
        }
        #endregion

        public void Send(string Data, string Protocol)
        {
            if (Data.Length >= 4096) return;
            MyAntennaDatagram Datagram;
            Datagram.Data = Data;
            Datagram.Protocol = Protocol;
            foreach (var Receiver in RelayedReceivers)
            {
                AntennaComms RelayedAntenna;
                if (Receiver.Entity.TryGetComponent(out RelayedAntenna) && RelayedAntenna.Block.CubeGrid.GetTopMostParent() != Block.CubeGrid.GetTopMostParent())
                {
                    RelayedAntenna.Receive(Datagram);
                }
            }
        }

        public void Broadcast(string Data)
        {
            Send(Data, null);
        }

        public void AllowProtocol(string Protocol)
        {
            if (Protocol == null || Protocol.Length > 50) return;
            if (!Protocols.Contains(Protocol)) Protocols.Add(Protocol);
        }

        public void DisallowProtocol(string Protocol)
        {
            if (Protocol == null || Protocol.Length > 50) return;
            if (Protocols.Contains(Protocol)) Protocols.Remove(Protocol);
        }

        public void ClearProtocolList()
        {
            Protocols.Clear();
        }

        public void Receive(MyAntennaDatagram Datagram)
        {
            if (!Block.IsWorking) return;
            if (!AllowReceive) return;
            if (Datagram.Protocol != null && UsesProtocols && !Protocols.Contains(Datagram.Protocol)) return;
            if (Datagrams.Count >= DatagramStorageSize) Datagrams.Dequeue();
            Datagrams.Enqueue(Datagram);
        }

        public string ReadFirstDatagram()
        {
            if (!Datagrams.Any()) return null;
            return Datagrams.Dequeue().Data;
        }

        public string ReadFirstDatagram(string Protocol)
        {
            if (!Datagrams.Any()) return null;
            if (!AnyDatagrams(Protocol)) return null;

            MyAntennaDatagram Datagram = Datagrams.First(x => x.Protocol == Protocol);
            List<MyAntennaDatagram> RemainingDatagrams = new List<MyAntennaDatagram>(Datagrams);
            RemainingDatagrams.Remove(Datagram);
            Datagrams = new Queue<MyAntennaDatagram>(RemainingDatagrams);
            return Datagram.Data;
        }

        public bool AnyDatagrams(string Protocol)
        {
            return Datagrams.Any(x => x.Protocol == Protocol);
        }

        public void ClearDatagramStorage()
        {
            Datagrams.Clear();
        }
    }

    class AntennaControls
    {
        public static void Broadcast<T>() where T : IMyTerminalBlock
        {
            var Broadcast = MyAPIGateway.TerminalControls.CreateProperty<Action<string>, T>("Broadcast");
            Broadcast.Enabled = Block => Block.HasComponent<AntennaComms>();
            Broadcast.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.Broadcast;
                return null;
            };
            Broadcast.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(Broadcast);
        }

        public static void Send<T>() where T : IMyTerminalBlock
        {
            var Send = MyAPIGateway.TerminalControls.CreateProperty<Action<string, string>, T>("Send");
            Send.Enabled = Block => Block.HasComponent<AntennaComms>();
            Send.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.Send;
                return null;
            };
            Send.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(Send);
        }

        public static void AllowProtocol<T>() where T : IMyTerminalBlock
        {
            var AllowProtocol = MyAPIGateway.TerminalControls.CreateProperty<Action<string>, T>("AllowProtocol");
            AllowProtocol.Enabled = Block => Block.HasComponent<AntennaComms>();
            AllowProtocol.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.AllowProtocol;
                return null;
            };
            AllowProtocol.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(AllowProtocol);
        }

        public static void DisallowProtocol<T>() where T : IMyTerminalBlock
        {
            var DisallowProtocol = MyAPIGateway.TerminalControls.CreateProperty<Action<string>, T>("DisallowProtocol");
            DisallowProtocol.Enabled = Block => Block.HasComponent<AntennaComms>();
            DisallowProtocol.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.DisallowProtocol;
                return null;
            };
            DisallowProtocol.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(DisallowProtocol);
        }

        public static void ClearProtocolList<T>() where T : IMyTerminalBlock
        {
            var ClearProtocolList = MyAPIGateway.TerminalControls.CreateProperty<Action, T>("ClearProtocolList");
            ClearProtocolList.Enabled = Block => Block.HasComponent<AntennaComms>();
            ClearProtocolList.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.ClearProtocolList;
                return null;
            };
            ClearProtocolList.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(ClearProtocolList);
        }

        public static void ReadFirstDatagram<T>() where T : IMyTerminalBlock
        {
            var ReadFirstDatagram = MyAPIGateway.TerminalControls.CreateProperty<Func<string>, T>("ReadFirstDatagram");
            ReadFirstDatagram.Enabled = Block => Block.HasComponent<AntennaComms>();
            ReadFirstDatagram.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.ReadFirstDatagram;
                return null;
            };
            ReadFirstDatagram.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(ReadFirstDatagram);
        }

        public static void ReadFirstDatagramByProtocol<T>() where T : IMyTerminalBlock
        {
            var ReadFirstDatagramByProtocol = MyAPIGateway.TerminalControls.CreateProperty<Func<string, string>, T>("ReadFirstDatagramByProtocol");
            ReadFirstDatagramByProtocol.Enabled = Block => Block.HasComponent<AntennaComms>();
            ReadFirstDatagramByProtocol.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.ReadFirstDatagram;
                return null;
            };
            ReadFirstDatagramByProtocol.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(ReadFirstDatagramByProtocol);
        }

        public static void ClearDatagramStorage<T>() where T : IMyTerminalBlock
        {
            var ClearDatagramStorage = MyAPIGateway.TerminalControls.CreateProperty<Action, T>("ClearDatagramStorage");
            ClearDatagramStorage.Enabled = Block => Block.HasComponent<AntennaComms>();
            ClearDatagramStorage.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.ClearDatagramStorage;
                return null;
            };
            ClearDatagramStorage.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(ClearDatagramStorage);
        }

        public static void AllowReceive<T>() where T : IMyTerminalBlock
        {
            var AllowReceive = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>("AllowReceive");
            AllowReceive.Enabled = Block => Block.HasComponent<AntennaComms>();
            AllowReceive.SupportsMultipleBlocks = true;
            AllowReceive.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.AllowReceive;
                return false;
            };
            AllowReceive.Setter = (Block, NewSetting) =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) Antenna.AllowReceive = NewSetting;
            };
            AllowReceive.Title = VRage.Utils.MyStringId.GetOrCompute("Receiver");
            AllowReceive.Tooltip = VRage.Utils.MyStringId.GetOrCompute("Allows this antenna to receive datagrams and put them in storage.\nIf disabled, antenna will only work as relay, which reduces server load.");
            AllowReceive.OnText = VRage.Utils.MyStringId.GetOrCompute("On");
            AllowReceive.OffText = VRage.Utils.MyStringId.GetOrCompute("Off");
            MyAPIGateway.TerminalControls.AddControl<T>(AllowReceive);
        }

        public static void AnyDatagrams<T>() where T : IMyTerminalBlock
        {
            var AnyDatagrams = MyAPIGateway.TerminalControls.CreateProperty<Func<string, bool>, T>("AnyDatagrams");
            AnyDatagrams.Enabled = Block => Block.HasComponent<AntennaComms>();
            AnyDatagrams.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.AnyDatagrams;
                return null;
            };
            AnyDatagrams.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(AnyDatagrams);
        }

        public static void DatagramsStored<T>() where T : IMyTerminalBlock
        {
            var DatagramsStored = MyAPIGateway.TerminalControls.CreateProperty<int, T>("DatagramsStored");
            DatagramsStored.Enabled = Block => Block.HasComponent<AntennaComms>();
            DatagramsStored.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return Antenna.DatagramsCount;
                return 0;
            };
            DatagramsStored.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(DatagramsStored);
        }

        public static void RelayedGrids<T>() where T: IMyTerminalBlock
        {
            var RelayedGrids = MyAPIGateway.TerminalControls.CreateProperty<List<Ingame.MyDetectedEntityInfo>, T>("RelayedGrids");
            RelayedGrids.Enabled = Block => Block.HasComponent<AntennaComms>();
            RelayedGrids.Getter = Block =>
            {
                AntennaComms Antenna;
                if (Block.TryGetComponent(out Antenna)) return new List<Ingame.MyDetectedEntityInfo>(Antenna.RelayedGridsIngame);
                return null;
            };
            RelayedGrids.Setter = (Block, trash) => { throw new Exception("This property is read-only."); };
            MyAPIGateway.TerminalControls.AddControl<T>(RelayedGrids);
        }
    }

    [ProtoContract]
    public struct AntennaPersistent
    {
        [ProtoMember(1)]
        public List<AntennaComms.MyAntennaDatagram> Datagrams;
        [ProtoMember(2)]
        public List<string> Protocols;
        [ProtoMember(3)]
        public bool Receive;
    }
}