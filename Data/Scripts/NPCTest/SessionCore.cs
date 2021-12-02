using System;
using System.Collections.Generic;
using System.Linq;
using AiEnabled.API;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity.UseObject;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Stollie.NPC_Test
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class SessionCore : MySessionComponentBase
    {
        private static SessionCore Instance; // the only way to access session comp from other classes and the only accepted static field.

        private int numberOfBotsSpawned = 0;
        private int maxNumberOfAllowedBots = 5;
        private int maxSpawnDistance = 10;

        private static int tickCounter = 0;
        private static int tickCounter10 = 0;
        private static int tickCounter100 = 0;
        private static int tickCounter500 = 0;
        private static int tickCounterAnimation = 0;

        private int botname = 1;
        private RemoteBotAPI remoteBotAPI;
        private bool start = false;
        private IMyCubeGrid grid;

        private HashSet<IMyEntity> ents = new HashSet<IMyEntity>();
        private HashSet<IMyCubeGrid> grids = new HashSet<IMyCubeGrid>();

        private List<IMySlimBlock> seats = new List<IMySlimBlock>();
        private List<IMyUseObject> useObjs = new List<IMyUseObject>();

        private readonly string[] allowedSeatTypes = new string[] { "Desk", "Couch", "Toilet", "Bathroom", "PassengerSeat", "Bed" };

        public override void LoadData()
        {
            Instance = this;
            remoteBotAPI = new RemoteBotAPI();
        }
        
        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
            remoteBotAPI.Close();
            Instance = null; // important for avoiding this object to remain allocated in memory
        }

        public override void UpdateAfterSimulation()
        {
            try // example try-catch for catching errors and notifying player, use only for non-critical code!
            {
                if (!MyAPIGateway.Multiplayer.IsServer)
                    return;

                if (!start && remoteBotAPI.CanSpawn)
                {
                    MyAPIGateway.Entities.GetEntities(ents);
                    foreach (var ent in ents)
                    {
                        if (ent as IMyCubeGrid != null)
                            grids.Add(ent as IMyCubeGrid);
                    }

                    MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
                    MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
                    MyAPIGateway.Entities.OnEntityRemove += Entities_OnEntityRemove;
                    MyVisualScriptLogicProvider.SendChatMessage("Ready to spawn...");
                    start = true;
                }

                if (!remoteBotAPI.CanSpawn) return;

                try
                {
                    // Bot spawning
                    if (numberOfBotsSpawned < maxNumberOfAllowedBots && tickCounter100 == 100)
                    {
                        foreach (var grid in grids)
                        {
                            SpawnBot(grid);
                            numberOfBotsSpawned++;
                            botname++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Entity capture Error: " + ex.ToString());
                }

                if (tickCounter == 10)
                    tickCounter = 0;

                if (tickCounter10 == 10)
                    tickCounter10 = 0;

                if (tickCounter100 > 100)
                    tickCounter100 = 0;

                if (tickCounter500 > 500)
                    tickCounter500 = 0;

                if (tickCounterAnimation == 1025)
                    tickCounterAnimation = 0;

                tickCounter++;
                tickCounter10++;
                tickCounter100++;
                tickCounter500++;
                tickCounterAnimation++;
            }
            catch (Exception e) // NOTE: never use try-catch for code flow or to ignore errors! catching has a noticeable performance impact.
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        private void Entities_OnEntityAdd(IMyEntity ent)
        {
            if (ent as IMyCubeGrid != null)
            {
                ents.Add(ent as IMyCubeGrid);
            }
        }

        private void Entities_OnEntityRemove(IMyEntity ent)
        {
            if (ent as IMyCubeGrid != null) 
            {
                ents.Remove(ent as IMyCubeGrid);
            }
        }

        public void SpawnBot(IMyCubeGrid grid)
        {
            try
            {
                var localPlayerPosition = MyAPIGateway.Session.LocalHumanPlayer.Character.PositionComp.WorldMatrixRef;
                if (grid != null && grid as MyCubeGrid != null && localPlayerPosition != null)
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Spawning bot#: " + botname);
                    IMyCharacter newbot = remoteBotAPI.SpawnBot("Default_Astronaut", "Bot" + " " + botname.ToString(), new MyPositionAndOrientation(MyAPIGateway.Session.LocalHumanPlayer.Character.WorldAABB.Center +
                                MyAPIGateway.Session.LocalHumanPlayer.Character.WorldMatrix.Forward * 2, localPlayerPosition.Forward, localPlayerPosition.Up),
                    (MyCubeGrid)grid, "BRUISER", null, Color.White);
                    
                    if (newbot != null)
                        TrySeatBotOnGrid(newbot, grid);
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        public void TrySeatBotOnGrid(IMyCharacter bot, IMyCubeGrid grid)
        {

            seats.Clear();
            useObjs.Clear();

            grid.GetBlocks(seats, b => b.FatBlock is IMyCockpit);
            for (int i = seats.Count - 1; i >= 0; i--)
            {
                var seat = seats[i]?.FatBlock as IMyCockpit;
                var blockId = seat?.BlockDefinition.SubtypeId;
                var relationship = MyIDModule.GetRelationPlayerBlock(seat.OwnerId, bot.EntityId, MyOwnershipShareModeEnum.Faction);

                if (seat == null || seat.Pilot != null ||
                    Vector3D.Distance(seat.GetPosition(), MyAPIGateway.Session.LocalHumanPlayer.Character.GetPosition()) > maxSpawnDistance ||
                    !allowedSeatTypes.Any(s => blockId.Contains(s)))
                    continue;

                MyVisualScriptLogicProvider.SendChatMessage("Trying seat..." + seat.CustomName);
                var useComp = seat.Components.Get<MyUseObjectsComponentBase>();
                useComp?.GetInteractiveObjects(useObjs);
                if (useObjs.Count > 0)
                {
                    var useObj = useObjs[0];
                    useObj.Use(UseActionEnum.Manipulate, bot);
                    //bot._pathCollection.CleanUp(true);
                    remoteBotAPI.SetBotTarget(bot.EntityId, null);
                }
            }
        }

        private void OnMessageEntered(string messageText, ref bool sendToOthers)
        {
            try
            {
                sendToOthers = false;
                if (messageText == "del")
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Deleting..");
                    foreach (var ent in MyEntities.GetEntities().ToList())
                    {
                        IMyCharacter character = ent as IMyCharacter;
                        if (character != null && character.Name != null)
                            MyVisualScriptLogicProvider.SendChatMessage(character.Name.ToString());
                        if (character != null && (character.Name.Contains("Bot") || string.IsNullOrEmpty(character.Name)))
                        {
                            character.Close();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MyVisualScriptLogicProvider.SendChatMessage("Exception: " + e + " " + e.StackTrace);
            }

        }

    }
}