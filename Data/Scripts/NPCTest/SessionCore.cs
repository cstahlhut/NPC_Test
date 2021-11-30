using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AiEnabled.API;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.Entity.UseObject;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum; // required for MyTransparentGeometry/MySimpleObjectDraw to be able to set blend type.

namespace Stollie.NPC_Test
{
    // This object is always present, from the world load to world unload.
    // NOTE: all clients and server run mod scripts, keep that in mind.
    // NOTE: this and gamelogic comp's update methods run on the main game thread, don't do too much in a tick or you'll lower sim speed.
    // NOTE: also mind allocations, avoid realtime allocations, re-use collections/ref-objects (except value types like structs, integers, etc).
    //
    // The MyUpdateOrder arg determines what update overrides are actually called.
    // Remove any method that you don't need, none of them are required, they're only there to show what you can use.
    // Also remove all comments you've read to avoid the overload of comments that is this file.
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class SessionCore : MySessionComponentBase
    {
        private static SessionCore Instance; // the only way to access session comp from other classes and the only accepted static field.
        
        private static int tickCounter = 0;
        private static int tickCounter10 = 0;
        private static int tickCounter100 = 0;
        private static int tickCounter500 = 0;
        private static int tickCounterAnimation = 0;

        private int botname = 1;
        private RemoteBotAPI remoteBotAPI;
        private bool start = false;

        public override void LoadData()
        {
            Instance = this;
            remoteBotAPI = new RemoteBotAPI();

        }
        
        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityRemove -= Entities_OnEntityRemove;
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

                if (!start == true)
                {
                    MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
                    MyAPIGateway.Entities.OnEntityRemove += Entities_OnEntityRemove;
                    start = true;
                }

                if (remoteBotAPI.CanSpawn)
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Ready to spawn...");
                }

                if (!remoteBotAPI.CanSpawn) return;

                try
                {
                        // Bot spawning
                    if (numberOfBotsSpawned <= maxNumberOfAllowedBots)
                    {
                            //if (gridsAndSeatsToSpawnBotsIn[0].ShouldSpawn > 0 && gridsAndSeatsToSpawnBotsIn[0].Cockpit != null && gridsAndSeatsToSpawnBotsIn[0].Grid != null)
                            if (gridsSeatsToSpawnBotsIn[0].Cockpit != null && gridsSeatsToSpawnBotsIn[0].Grid != null &&
                                Vector3D.Distance(gridsSeatsToSpawnBotsIn[0].Cockpit.GetPosition(), MyVisualScriptLogicProvider.GetPlayersPosition()) <= seatSpawnRange)
                            {
                                SpawnBotInSeat(gridsSeatsToSpawnBotsIn[0].Grid, gridsSeatsToSpawnBotsIn[0].Cockpit);
                                gridsSeatsToSpawnBotsIn.Remove(gridsSeatsToSpawnBotsIn[0]);
                            }
                    }
                }
                catch (Exception ex)
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Entity capture Error: " + ex.ToString());
                }

                try
                {
                    if (tickCounterAnimation == 1025)
                    {
                        //foreach (var character in gridsAndSeatsAndCharactersSpawned.ToList())
                        //{
                        //    if (character.Character == null)
                        //        continue;

                        //    if (character.Character.InScene && character.Character.Name.Contains("Bot"))
                        //    {
                        //        rnd = random.Next(0, animations.Count()-2);
                        //        character.Character.TriggerCharacterAnimationEvent("emote", true);
                        //        character.Character.TriggerCharacterAnimationEvent(animations[rnd], true);
                        //    }
                        //}
                    }
                }
                catch (Exception e)
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Animation Error: " + e.ToString());
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

        private void Entities_OnEntityRemove(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;
            foreach (var gridsToSpawnIn in gridsSeatsToSpawnBotsIn.ToList())
            {
                if (gridsToSpawnIn.Grid == grid)
                {
                    gridsSeatsToSpawnBotsIn.Remove(gridsToSpawnIn);
                }
            }
        }

        public void SpawnBotInSeat(IMyCubeGrid grid, IMyCockpit seat)
        {
            try
            {
                var rndRed = random.Next(0, 255);
                var rndGreen = random.Next(0, 255);
                var rndBlue = random.Next(0, 255);

                var rndX = random.Next(0, 2);
                var rndY = random.Next(0, 10);
                var rndZ = random.Next(0, 10);
                var mat = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(MyAPIGateway.Session.Player.Character.WorldMatrix * -1));

                var spawnVariaton = random.Next(0, 1);
                var spawnType = "";

                if (spawnVariaton == 0)
                    spawnType = "Default_Astronaut";
                if (spawnVariaton == 1)
                    spawnType = "Police_Bot";
                if (spawnVariaton == 2)
                    spawnType = "Target_Dummy";
                if (spawnVariaton == 3)
                    spawnType = "Boss_Bot";

                //var localPlayerPosition = MyAPIGateway.Session.LocalHumanPlayer.Character.PositionComp.WorldMatrixRef;
                //if (localPlayerPosition != null)
                //{
                //    var ob = new MyObjectBuilder_Character()
                //    {
                //        Name = "Bot " + botname.ToString(),
                //        SubtypeName = "Default_Astronaut",
                //        CharacterModel = "Default_Astronaut",
                //        EntityId = 0,
                //        AIMode = true,
                //        JetpackEnabled = false,
                //        EnableBroadcasting = false,
                //        NeedsOxygenFromSuit = false,
                //        OxygenLevel = 1,
                //        MovementState = MyCharacterMovementEnum.Standing,
                //        PersistentFlags = MyPersistentEntityFlags2.InScene | MyPersistentEntityFlags2.Enabled,
                //        PositionAndOrientation = new MyPositionAndOrientation(MyAPIGateway.Session.LocalHumanPlayer.Character.WorldAABB.Center +
                //            MyAPIGateway.Session.LocalHumanPlayer.Character.WorldMatrix.Forward * 2, localPlayerPosition.Forward, localPlayerPosition.Up),
                //        Health = 1000,
                //        OwningPlayerIdentityId = MyAPIGateway.Session.LocalHumanPlayer.IdentityId,
                //        ColorMaskHSV = Color.White.ColorToHSV(),
                //    };

                //    var bot = MyEntities.CreateFromObjectBuilder(ob, true) as IMyCharacter;
                //    if (bot != null)
                //    {
                //        MyVisualScriptLogicProvider.SendChatMessage("Spawning into SEAT bot#: " + botname + " into " + seat.CustomName);
                //        bot.Save = false;
                //        bot.Synchronized = true;
                //        bot.Flags &= ~VRage.ModAPI.EntityFlags.NeedsUpdate100;

                //        //if (MyAPIGateway.Session.LocalHumanPlayer.Character.ControllerInfo.Controller != null)
                //        //    MyAPIGateway.Session.LocalHumanPlayer.Character.ControllerInfo.Controller.TakeControl(bot);

                //        MyEntities.Add((MyEntity)bot, true);


                //        if (seat != null)
                //            SeatBot(seat, bot);


                //        var gridSeatCharacter = new GridsAndSeatsAndCharacters(grid, seat, bot);
                //        gridsAndSeatsAndCharactersSpawned.Add(gridSeatCharacter);
                //        numberOfBotsSpawned++;
                //        botname++;
                //    }
                //}


                //var localPlayerPosition = MyAPIGateway.Session.LocalHumanPlayer.Character.PositionComp.WorldMatrixRef;
                //var blockId = seat?.BlockDefinition.SubtypeId;
                //if (grid != null && grid as MyCubeGrid != null && localPlayerPosition != null && seat != null &&
                //    seat.Pilot == null && allowedSeatTypes.Any(s => blockId.Contains(s)) && !seat.CanControlShip)
                //{
                //    MyVisualScriptLogicProvider.SendChatMessage("Spawning into SEAT bot#: " + botname + " into " + seat.CustomName);
                //    var bot = MyVisualScriptLogicProvider.SpawnBot("Default_Astronaut", seat.GetPosition(), localPlayerPosition.Forward, localPlayerPosition.Up, "Bot " + botname.ToString());
                //    var newbot = MyEntities.GetEntityById(bot) as IMyCharacter;
                //    if (newbot != null && seat != null)
                //    {
                //        newbot.Save = false;
                //        newbot.Synchronized = true;
                //        newbot.Flags &= ~VRage.ModAPI.EntityFlags.None;
                //        remoteBotAPI.SetBotTarget(newbot.EntityId, null);
                //        SeatBot(seat, newbot);

                //        var gridsSeatsAndCharactersSpawnedIn = new SpawnedInGridsSeatsCharacters(grid, seat, newbot);
                //        spawnedInGridsSeatsCharacters.Add(gridsSeatsAndCharactersSpawnedIn);
                //        numberOfBotsSpawned++;
                //        botname++;
                //    }
                //}


                var localPlayerPosition = MyAPIGateway.Session.LocalHumanPlayer.Character.PositionComp.WorldMatrixRef;
                var blockId = seat?.BlockDefinition.SubtypeId;
                if (grid != null && grid as MyCubeGrid != null && localPlayerPosition != null && seat != null &&
                    seat.Pilot == null && allowedSeatTypes.Any(s => blockId.Contains(s)) && !seat.CanControlShip)
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Spawning SEAT bot#: " + botname);
                    IMyCharacter newbot = remoteBotAPI.SpawnBot(spawnType, "Bot" + " " + botname.ToString(), new MyPositionAndOrientation(MyAPIGateway.Session.LocalHumanPlayer.Character.WorldAABB.Center +
                                MyAPIGateway.Session.LocalHumanPlayer.Character.WorldMatrix.Forward * 2, localPlayerPosition.Forward, localPlayerPosition.Up),
                    (MyCubeGrid)grid, "BRUISER", MyAPIGateway.Session.LocalHumanPlayer.IdentityId, new Color(rndRed, rndGreen, rndBlue, 255));

                    //IMyCharacter newbot = remoteBotAPI.SpawnBot(spawnType, "Bot" + " " + botname.ToString(), new MyPositionAndOrientation(mat),
                    //    (MyCubeGrid)grid, "BRUISER", null, new Color(rndRed, rndGreen, rndBlue, 255));

                    var controller1 = newbot as VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
                    var controller2 = newbot as Sandbox.Game.Entities.IMyControllableEntity;
                    controller1?.SwitchHelmet();
                    //controller2?.SwitchBroadcasting();

                    if (newbot != null && seat != null)
                    {
                        newbot.Save = false;
                        newbot.Synchronized = true;
                        
                        remoteBotAPI.SetBotTarget(newbot.EntityId, null);
                        //SeatBot(seat, newbot);

                        var gridsSeatsAndCharactersSpawnedIn = new SpawnedInGridsSeatsCharacters(grid, seat, newbot);
                        spawnedInGridsSeatsCharacters.Add(gridsSeatsAndCharactersSpawnedIn);
                        numberOfBotsSpawned++;
                        botname++;
                    }
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        public void SpawnBotWithoutSeat(IMyCubeGrid grid)
        {
            try
            {
                var rndRed = random.Next(0, 255);
                var rndGreen = random.Next(0, 255);
                var rndBlue = random.Next(0, 255);

                var rndX = random.Next(0, 2);
                var rndY = random.Next(0, 10);
                var rndZ = random.Next(0, 10);
                var mat = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(MyAPIGateway.Session.Player.Character.WorldMatrix * -1));

                var spawnVariaton = random.Next(0, 1);
                var spawnType = "";

                if (spawnVariaton == 0)
                    spawnType = "Astronaut_Default";
                if (spawnVariaton == 1)
                    spawnType = "Police_Bot";
                if (spawnVariaton == 2)
                    spawnType = "Target_Dummy";
                if (spawnVariaton == 3)
                    spawnType = "Boss_Bot";

                if (grid != null && grid as MyCubeGrid != null)                   
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Spawning on GRID bot#: " + botname);
                    placeToSpawnList.Clear();
                    remoteBotAPI.GetAvailableGridNodes(grid as MyCubeGrid, 1, null, true);
                    if (placeToSpawnList != null && placeToSpawnList.Count != 0)
                    {
                        int r = random.Next(placeToSpawnList.Count);
                        var placeToSpawn = grid.GridIntegerToWorld(placeToSpawnList.ElementAtOrDefault(0));
                        if (placeToSpawn != null)
                        {
                            var localPlayerPosition = MyAPIGateway.Session.LocalHumanPlayer.Character.PositionComp.WorldMatrixRef;
                            IMyCharacter newbot = remoteBotAPI.SpawnBot(spawnType, "Bot" + " " + botname.ToString(),
                                new MyPositionAndOrientation(placeToSpawn, localPlayerPosition.Forward, localPlayerPosition.Up),
                                 grid as MyCubeGrid, "BRUISER", null, new Color(rndRed, rndGreen, rndBlue, 255));

                            var controller1 = newbot as VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
                            var controller2 = newbot as Sandbox.Game.Entities.IMyControllableEntity;
                            controller1?.SwitchHelmet();
                            //controller2?.SwitchBroadcasting();

                            if (newbot != null)
                                remoteBotAPI.SetBotTarget(newbot.EntityId, null);

                            var gridSeatCharacter = new SpawnedInGridsSeatsCharacters(grid, null, newbot);
                            spawnedInGridsSeatsCharacters.Add(gridSeatCharacter);
                            numberOfBotsSpawned++;
                            botname++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        private void SeatBot(IMyCockpit seat, IMyCharacter character)
        {
            // This try/catch shouldnt exist but theres an problem with the main mod JTurp is aware of! Fixed in his next release.
            try
            {
                var blockId = seat?.BlockDefinition.SubtypeId;

                if (seat == null || seat.Pilot != null || !allowedSeatTypes.Any(s => blockId.Contains(s)) || seat.CanControlShip)
                    return;

                MyVisualScriptLogicProvider.SendChatMessage("Putting " + botname + " into " + seat.CustomName);
                
                List<IMyUseObject> useObjs = new List<IMyUseObject>();
                useObjs.Clear();
                var useComp = seat.Components.Get<MyUseObjectsComponentBase>();
                useComp?.GetInteractiveObjects(useObjs);
                
                if (useObjs.Count > 0)
                {
                    var useObj = useObjs[0];
                    useObj.Use(UseActionEnum.Manipulate, character);
                }
            }
            catch (Exception e)
            {
                MyVisualScriptLogicProvider.SendChatMessage("Seating Error: " + e.ToString());
            }
        }

        private void OnMessageEntered(string messageText, ref bool sendToOthers)
        {
            try
            {
                sendToOthers = false;
                if (messageText == "start")
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Starting...");
                    start = true;
                }

                if (messageText == "scount")
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Spawn list count: " + gridsSeatsToSpawnBotsIn.Count());
                }

                if (messageText == "list")
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Listing...");
                    HashSet<IMyEntity> ents2 = new HashSet<IMyEntity>();
                    MyAPIGateway.Entities.GetEntities(ents2);
                    foreach (var ent in ents2)
                    {
                        MyVisualScriptLogicProvider.SendChatMessage(ent.ToString());
                        //IMyCharacter character = ent as IMyCharacter;
                        //if (character != null && (character.Name.Contains("Bot") || string.IsNullOrEmpty(character.Name)))
                        //{
                        //    var newbot = character;
                        //    MyVisualScriptLogicProvider.SendChatMessage("BotName: " + character.Name.ToString());
                        //}
                    }
                }

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

                    foreach (var kvp in spawnedInGridsSeatsCharacters)
                    {
                        kvp.Character.Close();
                    }
                    numberOfBotsSpawned = spawnedInGridsSeatsCharacters.Count();
                }

                if (messageText == "seat")
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Trying to seat");
                    foreach (var kvp in spawnedInGridsSeatsCharacters)
                    {
                        remoteBotAPI.TrySeatBotOnGrid(kvp.Character.EntityId, kvp.Grid);
                        MyVisualScriptLogicProvider.SendChatMessage(kvp.Character.Name);
                    }
                }

                if (messageText == "n")
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Number of Bots: " + numberOfBotsSpawned.ToString());
                }
            }
            catch (Exception e)
            {
                MyVisualScriptLogicProvider.SendChatMessage("Exception: " + e + " " + e.StackTrace);
            }

        }

    }
}