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
        public static SessionCore Instance; // the only way to access session comp from other classes and the only accepted static field.

        private bool init = false;
        public static float tickCounterAnimation = 0.0f;
        public static int tickCounter10 = 0;
        private int botname = 1;
        public RemoteBotAPI remoteBotAPI;
        
        private bool setBotSpawnCount;
        public List<IMySlimBlock> gridBlocks = new List<IMySlimBlock>();
        
        public HashSet<IMyCharacter> characters = new HashSet<IMyCharacter>();
        private List<IMyCockpit> botSeats = new List<IMyCockpit>();
        private string[] animations = new string[] {"Stretching",  "CheckWrist", "FacePalm", "Cold", "passengerseat_small"};
        private string[] allowedSeatTypes = new string[] { "Desk", "Couch", "Toilet", "Bathroom", "PassengerSeat", "Bed"};

        internal static readonly Random random = new Random();
        internal int rnd;
        private int numberOfBotsToSpawn = 0;
        private bool setBotSpawned;
        private int tickCounter100 = 0;
        private int tickCounter500 = 0;
        private bool spawningBots = false;
        private Dictionary<IMyCockpit, IMyCubeGrid> gridsAndCockpits = new Dictionary<IMyCockpit, IMyCubeGrid>();

        public override void LoadData()
        {
            Instance = this;
            remoteBotAPI = new RemoteBotAPI();
            MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
        }
        protected override void UnloadData()
        {
            Instance = null; // important for avoiding this object to remain allocated in memory
            foreach (var ent in MyEntities.GetEntities().ToList())
            {
                if (ent.EntityId == MyAPIGateway.Session.Player.Character.EntityId)
                    continue;

                IMyCharacter character = ent as IMyCharacter;
                if (character != null && (character.Name.Contains("Bot") || string.IsNullOrEmpty(character.Name)))
                {
                    character.Kill();
                    character.Close();
                }
            }
            remoteBotAPI.Close();
            MyAPIGateway.Utilities.MessageEntered -= onMessageEntered;
        }

        private void Entities_OnEntityAdd(IMyEntity obj)
        {
            if (obj as IMyCharacter != null)
            {
                var character = obj as IMyCharacter;
                if (!characters.Contains(character))
                    characters.Add(character);
                //MyVisualScriptLogicProvider.SendChatMessage("Added: " + character.Name);
            }
        }

        public override void UpdateAfterSimulation()
        {
            try // example try-catch for catching errors and notifying player, use only for non-critical code!
            {
                if (!init && remoteBotAPI.CanSpawn == true)
                {
                    MyAPIGateway.Utilities.MessageEntered += onMessageEntered;
                    foreach (var ent in MyEntities.GetEntities())
                    {
                        if (ent as IMyCharacter != null && !characters.Contains(ent as IMyCharacter))
                        {
                            var character = ent as IMyCharacter;
                            characters.Add(character);
                        }
                    }
                    
                    MyVisualScriptLogicProvider.SendChatMessage("Ready to spawn...");
                    init = true;
                }

                if (!init) return;

                try
                {
                    if (tickCounter100 == 10 && spawningBots == false)
                    {
                        var entitiesNearPlayer = MyVisualScriptLogicProvider.GetEntitiesInSphere(MyVisualScriptLogicProvider.GetPlayersPosition(), 15.0f);
                        foreach (var ent in entitiesNearPlayer)
                        {
                            var ent2 = MyVisualScriptLogicProvider.GetEntityById(ent);
                            if (ent2 as IMyCubeGrid != null && (ent2 as IMyCubeGrid)?.Physics != null)
                            {
                                var grid = ent2 as IMyCubeGrid;
                                List<IMySlimBlock> detectedGridCockpits = new List<IMySlimBlock>();
                                grid?.GetBlocks(detectedGridCockpits, b => b.FatBlock is IMyCockpit);
                                Dictionary<IMyCubeGrid, IMyCockpit> gridCockpits = new Dictionary<IMyCubeGrid, IMyCockpit>();

                                foreach (var gcpit in detectedGridCockpits)
                                {
                                    var seat = gcpit.FatBlock as IMyCockpit;
                                    var blockId = seat?.BlockDefinition.SubtypeId;

                                    if (seat == null || seat.Pilot != null || !allowedSeatTypes.Any(s => blockId.Contains(s)) || seat.CanControlShip)
                                        continue;

                                    if (!gridsAndCockpits.ContainsKey(seat))
                                        gridsAndCockpits.Add(seat, grid);
                                    
                                    spawningBots = true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Entity capture Error: " + ex.ToString());
                }

                try
                {
                    if (spawningBots == true)
                    {
                        foreach (var gridCockpit in gridsAndCockpits.ToList())
                        {
                            if (tickCounter100 == 100)
                            {
                                SpawnBot(gridCockpit.Value, gridCockpit.Key);
                                tickCounter100++;
                                gridsAndCockpits.Remove(gridCockpit.Key);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Spawn Exception: " + e.ToString());
                }

                try
                {
                    if (tickCounterAnimation == 1025)
                    {
                        foreach (var character in characters.ToList())
                        {
                            if (character.InScene && character.Name.Contains("Bot") && character != null)
                            {
                                rnd = random.Next(0, animations.Count()-2);
                                character.TriggerCharacterAnimationEvent("emote", true);
                                character.TriggerCharacterAnimationEvent(animations[rnd], true);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Animation Error: " + e.ToString());
                }
                
                if (tickCounter10 == 10)
                    tickCounter10 = 0;

                if (tickCounter100 > 100)
                    tickCounter100 = 0;

                if (tickCounterAnimation == 1025)
                    tickCounterAnimation = 0;

                tickCounter10++;
                tickCounter100++;
                tickCounterAnimation++;
            }
            catch (Exception e) // NOTE: never use try-catch for code flow or to ignore errors! catching has a noticeable performance impact.
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        public void SpawnBot(IMyCubeGrid grid, IMyCockpit seat)
        {
            try
            {
                MyVisualScriptLogicProvider.SendChatMessage("Spawning bot#: " + botname);
                var rndRed = random.Next(0, 255);
                var rndGreen = random.Next(0, 255);
                var rndBlue = random.Next(0, 255);

                var rndX = random.Next(0, 2);
                var rndY = random.Next(0, 10);
                var rndZ = random.Next(0, 10);
                var mat = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(MyAPIGateway.Session.Player.Character.WorldMatrix * -1));
                //mat.Translation = new Vector3D(rndX, 0, 0);

                var spawnVariaton = random.Next(0, 4);
                var spawnType = "";

                if (spawnVariaton == 0)
                    spawnType = "Astronaut_Default";
                if (spawnVariaton == 1)
                    spawnType = "Police_Bot";
                if (spawnVariaton == 2)
                    spawnType = "Target_Dummy";
                if (spawnVariaton == 3)
                    spawnType = "Boss_Bot";

                IMyCharacter newbot = remoteBotAPI.SpawnBot(spawnType, "Bot" + " " + botname.ToString(), new MyPositionAndOrientation(mat),
                (MyCubeGrid)grid, "BRUISER", null, new Color(rndRed, rndGreen, rndBlue, 255));

                var controller1 = newbot as VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
                var controller2 = newbot as Sandbox.Game.Entities.IMyControllableEntity;
                controller1?.SwitchHelmet();
                //controller2?.SwitchBroadcasting();

                if (newbot != null)
                {
                    remoteBotAPI.SetBotTarget(newbot.EntityId, null);
                }

                if (!characters.Contains(newbot))
                    characters.Add(newbot);

                SeatBot(newbot, seat);

                botname++;
                spawningBots = false;
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        private void SeatBot(IMyCharacter character, IMyCockpit seat)
        {
            // This try/catch shouldnt exist but theres an problem with the main mod JTurp is aware of! Fixed in his next release.
            try
            {
                MyVisualScriptLogicProvider.SendChatMessage("Seating: " + character.DisplayName);
                var blockId = seat?.BlockDefinition.SubtypeId;

                if (seat == null || seat.Pilot != null || !allowedSeatTypes.Any(s => blockId.Contains(s)) || seat.CanControlShip ||
                    character.ControllerInfo.Controller.ControlledEntity.Entity as IMyCockpit != null)
                    return;

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

        private void onMessageEntered(string messageText, ref bool sendToOthers)
        {
            try
            {
                sendToOthers = false;
                if (messageText == "go")
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Executing Animation...");
                    //bool flag = MyAPIGateway.Session.ControlledObject is MyCockpit;
                    //IMyCharacter myCharacter = flag ? ((MyShipController)MyAPIGateway.Session.ControlledObject).Pilot : MyAPIGateway.Session.LocalHumanPlayer.Character;
                    //if (myCharacter != null)
                    //{
                    //    myCharacter.TriggerCharacterAnimationEvent("emote", true);
                    //    myCharacter.TriggerCharacterAnimationEvent("Wave", true);
                    //}

                    foreach (var character in characters.ToList())
                    {
                        if (character == MyAPIGateway.Session.LocalHumanPlayer.Character)
                            continue;
                        character.TriggerCharacterAnimationEvent("emote", true);
                        character.TriggerCharacterAnimationEvent("Wave", true);
                    }
                }

                if (messageText == "id")
                {
                    var foundId = MyAPIGateway.Session?.Factions.TryGetFactionByTag("JKHP").FounderId;
                    var foundIdentId = MyAPIGateway.Players.TryGetIdentityId((ulong)foundId);
                    MyVisualScriptLogicProvider.SendChatMessage("Local: " + MyAPIGateway.Session.LocalHumanPlayer.IdentityId.ToString());
                    MyVisualScriptLogicProvider.SendChatMessage("Player: " + MyAPIGateway.Session.Player.IdentityId.ToString());
                    MyVisualScriptLogicProvider.SendChatMessage("Faction: " + foundIdentId.ToString());
                }

                if (messageText == "sb")
                {
                    if (MyAPIGateway.Session.IsServer)
                    {
                        numberOfBotsToSpawn = 3;
                        MyVisualScriptLogicProvider.SendChatMessage(numberOfBotsToSpawn.ToString());
                    }
                }

                if (messageText == "list")
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Listing...");
                    foreach (var character in characters.ToList())
                    {
                        if (character.Name.Contains("Bot"))
                            MyVisualScriptLogicProvider.SendChatMessage(character.Name);
                    }
                }

                if (messageText == ("del"))
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Deleting...");
                    foreach (var ent in MyEntities.GetEntities().ToList())
                    {
                        if (ent.EntityId == MyAPIGateway.Session.Player.Character.EntityId)
                            continue;

                        IMyCharacter character = ent as IMyCharacter;
                        if (character != null && (character.Name.Contains("Bot") || string.IsNullOrEmpty(character.Name)))
                        {
                            if (character.Name != null)
                                MyVisualScriptLogicProvider.SendChatMessage("Closing: " + character.Name);
                            else
                                MyVisualScriptLogicProvider.SendChatMessage("Closing null name");

                            character.DoDamage(1000, MyDamageType.Bullet, true);
                            //character.Kill();
                            //character.Close();
                            characters.Remove(character);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MyVisualScriptLogicProvider.SendChatMessage("Exception: " + e + " " + e.StackTrace);
            }

        }
               
        public override void Draw()
        {
            // gets called 60 times a second after all other update methods, regardless of framerate, game pause or MyUpdateOrder.
            // NOTE: this is the only place where the camera matrix (MyAPIGateway.Session.Camera.WorldMatrix) is accurate, everywhere else it's 1 frame behind.
        }

        public override void SaveData()
        {
            // executed AFTER world was saved
        }

        public override MyObjectBuilder_SessionComponent GetObjectBuilder()
        {
            // executed during world save, most likely before entities.

            return base.GetObjectBuilder(); // leave as-is.
        }

        public override void UpdatingStopped()
        {
            // executed when game is paused
        }
    }
}