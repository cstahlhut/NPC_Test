﻿using System;
using System.Collections.Concurrent;
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

        private int numberOfSeatBotsSpawned = 0;
        private int numberOfWalkingBotsSpawned = 0;
        private int maxNumberOfAllowedSeatBots = 10;
        private int maxNumberOfAllowedWalkingBots = 5;
        private int maxSpawnDistance = 50;

        private static int tickCounter = 0;
        private static int tickCounter10 = 0;
        private static int tickCounter100 = 0;
        private static int tickCounter500 = 0;
        private static int tickCounterAnimation = 0;

        private int botname = 1;
        private RemoteBotAPI remoteBotAPI;
        private bool start = false;
        private bool showCount = true;
        private bool stop = false;
        
        private Random random = new Random();

        private HashSet<IMyEntity> ents = new HashSet<IMyEntity>();
        private HashSet<IMyCubeGrid> grids = new HashSet<IMyCubeGrid>();
        private List<IMySlimBlock> gridBlocks = new List<IMySlimBlock>();
        
        private HashSet<IMyCockpit> seats = new HashSet<IMyCockpit>();
        private List<IMyUseObject> useObjs = new List<IMyUseObject>();

        ConcurrentDictionary<IMySlimBlock, IMyCharacter> seatsAndBots = new ConcurrentDictionary<IMySlimBlock, IMyCharacter>();
        ConcurrentDictionary<IMyCubeGrid, List<Vector3I>> gridSpawnPoints = new ConcurrentDictionary<IMyCubeGrid, List<Vector3I>>();

        private string[] allowedSeatTypes = new string[] { "LargeBlockDesk", "LargeBlockDeskCorner", "LargeBlockCouch", "LargeBlockCouchCorner", "PassengerSeatLarge",
            "PassengerSeatLarge", "LargeBlockBathroom", "LargeBlockToilet", "LargeBlockBathroomOpen", "LargeBlockBed" };

        public override void LoadData()
        {
            Instance = this;
            remoteBotAPI = new RemoteBotAPI();
            
        }
        
        protected override void UnloadData()
        {
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
            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList);
            foreach (var player in playerList)
            {
                if (player.IsBot)
                    player.Character.Close();
            }
            foreach (var b in seatsAndBots)
            {
                b.Value.Close();
            }
            
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

                if (!start && remoteBotAPI.CanSpawn && remoteBotAPI.Valid)
                {
                    MyAPIGateway.Entities.GetEntities(ents);
                    foreach (var ent in ents)
                    {
                        if (ent as IMyCubeGrid != null)
                        {
                            var grid = ent as IMyCubeGrid;
                            grids.Add(ent as IMyCubeGrid);
                            var spawnPoints = remoteBotAPI.GetAvailableGridNodes((MyCubeGrid)grid, maxNumberOfAllowedWalkingBots);
                            gridSpawnPoints.TryAdd(grid, spawnPoints);
                            grid.OnBlockAdded += Grid_OnBlockAdded;
                            grid.OnBlockRemoved += Grid_OnBlockRemoved;
                            gridBlocks.Clear();
                            grid.GetBlocks(gridBlocks);
                            foreach (var blk in gridBlocks)
                            {
                                var seat = blk.FatBlock as IMyCockpit;
                                if (seat != null)
                                {
                                    var blockId = seat?.BlockDefinition.SubtypeId;
                                    if (allowedSeatTypes.Contains(blockId) && !seats.Contains(seat))
                                    {
                                        //MyVisualScriptLogicProvider.SendChatMessage("Added" + blockId);
                                        seats.Add(seat);
                                    }
                                }
                            }
                        }
                    }

                    MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
                    MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
                    MyAPIGateway.Entities.OnEntityRemove += Entities_OnEntityRemove;
                    MyVisualScriptLogicProvider.SendChatMessage("Ready to spawn...");
                    start = true;
                }

                if (!remoteBotAPI.CanSpawn || !remoteBotAPI.Valid || stop == true) return;

                if (showCount)
                    MyVisualScriptLogicProvider.ShowNotification("Number of bots: " + (numberOfSeatBotsSpawned + numberOfWalkingBotsSpawned).ToString() , 1);

                try
                {
                    // Bot spawning
                    if (numberOfSeatBotsSpawned < maxNumberOfAllowedSeatBots && tickCounter100 == 100)
                    {
                        TrySeatBotOnGrid();
                    }

                    if (numberOfWalkingBotsSpawned < maxNumberOfAllowedWalkingBots && tickCounter100 == 100)
                    {
                        TrySpawnBotsOnGrid();
                    }

                    // Bot despawning
                    if (tickCounter100 == 100)
                    {
                        foreach (var seatBot in seatsAndBots)
                        {
                            if (Vector3D.Distance(seatBot.Key.FatBlock.GetPosition(), MyAPIGateway.Session.LocalHumanPlayer.Character.GetPosition()) > maxSpawnDistance)
                            {
                                var seat = seatBot.Key.FatBlock as IMyCockpit;
                                var pilot = seat.Pilot;
                                if (pilot != null)
                                {
                                    seat.RemovePilot();
                                    pilot.Close();
                                }
                                IMyCharacter botToRemove;
                                seatsAndBots.TryRemove(seatBot.Key, out botToRemove);
                                numberOfSeatBotsSpawned--;
                            }
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

        private void Grid_OnBlockRemoved(IMySlimBlock blk)
        {
            if (blk as IMyCockpit != null)
            {
                var cp = blk as IMyCockpit;
                if (seats.Contains(cp))
                {
                    seats.Remove(cp);
                }
            }
        }

        private void Grid_OnBlockAdded(IMySlimBlock blk)
        {
            if (blk as IMyCockpit != null)
            {
                var cp = blk as IMyCockpit;
                if (!seats.Contains(cp))
                {
                    seats.Add(cp);
                }
            }
        }

        private void Entities_OnEntityAdd(IMyEntity ent)
        {
            if (ent as IMyCubeGrid != null && (ent as IMyCubeGrid).Physics != null && !grids.Contains(ent as IMyCubeGrid))
            {
                MyVisualScriptLogicProvider.SendChatMessage("Grid Added");
                var grid = ent as IMyCubeGrid;
                grids.Add(ent as IMyCubeGrid);
                var spawnPoints = remoteBotAPI.GetAvailableGridNodes((MyCubeGrid)grid, maxNumberOfAllowedWalkingBots);
                gridSpawnPoints.TryAdd(grid, spawnPoints);
                gridBlocks.Clear();
                grid.GetBlocks(gridBlocks);
                foreach (var blk in gridBlocks)
                {
                    var seat = blk.FatBlock as IMyCockpit;
                    if (seat != null)
                    {
                        var blockId = seat?.BlockDefinition.SubtypeId;
                        if (allowedSeatTypes.Contains(blockId) && !seats.Contains(seat))
                        {
                            seats.Add(seat);
                        }
                    }
                }
            }
        }

        private void Entities_OnEntityRemove(IMyEntity ent)
        {
            try
            {
                if (ent as IMyCubeGrid != null)
                {
                    var grid = ent as IMyCubeGrid;
                    gridBlocks.Clear();
                    grid.GetBlocks(gridBlocks);
                    foreach (var blk in gridBlocks)
                    {
                        var seat = blk.FatBlock as IMyCockpit;
                        if (seat != null)
                        {
                            var blockId = seat?.BlockDefinition.SubtypeId;
                            if (seats.Contains(seat))
                            {
                                seats.Remove(seat);
                            }
                        }
                    }
                    grids.Remove(ent as IMyCubeGrid);
                    gridSpawnPoints.Keys.Remove(grid);
                }
            }
            catch (Exception e) // NOTE: never use try-catch for code flow or to ignore errors! catching has a noticeable performance impact.
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }

        }

        public void TrySeatBotOnGrid()
        {
            useObjs.Clear();
            var rndRed = random.Next(0, 255);
            var rndGreen = random.Next(0, 255);
            var rndBlue = random.Next(0, 255);
           
            foreach (var seat in seats)
            {
                var blockId = seat?.BlockDefinition.SubtypeId;
                if (seat == null || seat.Pilot != null ||
                    Vector3D.Distance(seat.GetPosition(), MyAPIGateway.Session.LocalHumanPlayer.Character.GetPosition()) > maxSpawnDistance ||
                    !allowedSeatTypes.Contains(blockId) || seat.CanControlShip)
                {
                    //MyVisualScriptLogicProvider.SendChatMessage("Skipping: " + seat.CustomName);
                    continue;
                }

                var localPlayerPosition = MyAPIGateway.Session.LocalHumanPlayer.Character.PositionComp.WorldMatrixRef;
                var grid = (MyCubeGrid)seat.CubeGrid;
                var placeToSpawn = new MyPositionAndOrientation(grid.PositionComp.GetPosition(), localPlayerPosition.Forward, localPlayerPosition.Up);
                if (localPlayerPosition != null && grid != null)
                {
                    IMyCharacter bot = remoteBotAPI.SpawnBot("Default_Astronaut", "Bot" + " " + botname.ToString(), placeToSpawn,
                        grid, "BRUISER", null, new Color(rndRed, rndGreen, rndBlue, 255));
                    
                    //MyVisualScriptLogicProvider.SendChatMessage("Spawned bot#: " + botname);
                    if (bot != null)
                    {
                        numberOfSeatBotsSpawned++;
                        botname++;
                        remoteBotAPI.SetBotTarget(bot.EntityId, null);
                        MyVisualScriptLogicProvider.SendChatMessage("Trying to spawn Bot into seat... " + seat.CustomName);
                        var useComp = seat.Components.Get<MyUseObjectsComponentBase>();
                        useComp?.GetInteractiveObjects(useObjs);
                        if (useObjs.Count > 0)
                        {
                            var useObj = useObjs[0];
                            useObj.Use(UseActionEnum.Manipulate, bot);
                            var radio = bot.Components.Get<MyDataBroadcaster>();
                            

                            //bot._pathCollection.CleanUp(true);
                            seatsAndBots.TryAdd(seat.SlimBlock, bot);
                            break;
                        }
                    }
                }
            }
        }

        private void TrySpawnBotsOnGrid()
        {
            foreach (var gsps in gridSpawnPoints)
            {
                var gspList = gsps.Value;
                var grid = gsps.Key;
                var localPlayerPosition = MyAPIGateway.Session.LocalHumanPlayer.Character.PositionComp.WorldMatrixRef;

                var rndRed = random.Next(0, 255);
                var rndGreen = random.Next(0, 255);
                var rndBlue = random.Next(0, 255);
                var randomSpawnPoint = random.Next(0, gspList.Count);

                if (Vector3D.Distance(grid.GetPosition(), MyAPIGateway.Session.LocalHumanPlayer.Character.GetPosition()) <= maxSpawnDistance)
                {
                    var placeToSpawn1 = grid.GridIntegerToWorld(gspList.ElementAtOrDefault(randomSpawnPoint - 1));
                    var placeToSpawn2 = new MyPositionAndOrientation(placeToSpawn1, localPlayerPosition.Forward, localPlayerPosition.Up);

                    IMyCharacter bot = remoteBotAPI.SpawnBot("Default_Astronaut", "Bot" + " " + botname.ToString(), placeToSpawn2,
                        (MyCubeGrid)grid, "BRUISER", null, new Color(rndRed, rndGreen, rndBlue, 255));

                    MyVisualScriptLogicProvider.SendChatMessage("Spawing on grid ... Bot: " + botname);
                    if (bot != null)
                    {
                        botname++;
                        numberOfWalkingBotsSpawned++; 
                        remoteBotAPI.SetBotTarget(bot.EntityId, null);
                    }
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
                    List<IMyPlayer> playerList = new List<IMyPlayer>();
                    MyAPIGateway.Players.GetPlayers(playerList);

                    foreach (var player in playerList)
                    {
                        if (player.IsBot)
                            player.Character.Close();
                    }
                    foreach (var b in seatsAndBots)
                    {
                        b.Value.Close();
                    }
                    numberOfSeatBotsSpawned = 0;
                    numberOfWalkingBotsSpawned = 0;
                }

                if (messageText == "show")
                {
                    showCount = true;
                }

                if (messageText == "hide")
                {
                    showCount = false;
                }

                if (messageText == "stop")
                {
                    stop = true;
                }

                if (messageText == "start")
                {
                    stop = false;
                }
            }
            catch (Exception e)
            {
                MyVisualScriptLogicProvider.SendChatMessage("Exception: " + e + " " + e.StackTrace);
            }

        }

    }
}