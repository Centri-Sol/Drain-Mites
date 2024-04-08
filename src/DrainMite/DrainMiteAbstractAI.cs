using System.Collections.Generic;
using UnityEngine;

namespace DrainMites.DrainMite;

public class DrainMiteAbstractAI : AbstractCreatureAI, IOwnAnAbstractSpacePathFinder
{

    private struct CoordinateAndFloat
    {
        public WorldCoordinate coord;

        public float flt;

        public CoordinateAndFloat(WorldCoordinate coord, float flt)
        {
            this.coord = coord;
            this.flt = flt;
        }
    }

    public class DrainmiteSwarm
    {

        public List<AbstractCreature> members;
        public AbstractCreature leader;

        public HSLColor color;

        public virtual bool Active
        {
            get
            {
                if (members.Count > 0)
                {
                    return (leader.abstractAI as DrainMiteAbstractAI).swarm == this;
                }
                return false;
            }
        }

        public virtual bool StayInSwarm
        {
            get
            {
                if (leader is not null &&
                    leader.state.alive)
                {
                    return true;
                }
                return false;
            }
        }

        public DrainmiteSwarm(AbstractCreature leader)
        {
            this.leader = leader;
            members = new List<AbstractCreature> { leader };
            color = new HSLColor(Random.value, 0.25f, 0.4f);
        }

        public virtual void AddMember(AbstractCreature newMember)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] == newMember)
                {
                    return;
                }
            }
            (newMember.abstractAI as DrainMiteAbstractAI).swarm = this;
            members.Add(newMember);
            UpdateLeader();
        }

        public virtual void RemoveMember(AbstractCreature noLongerMember)
        {
            for (int i = members.Count - 1; i >= 0; i--)
            {
                if (members[i] == noLongerMember)
                {
                    (members[i].abstractAI as DrainMiteAbstractAI).swarm = null;
                    members.RemoveAt(i);
                }
            }
            UpdateLeader();
        }

        public virtual void Dissolve()
        {
            for (int i = members.Count - 1; i >= 0; i--)
            {
                (members[i].abstractAI as DrainMiteAbstractAI).swarm = null;
            }
            members.Clear();
            leader = null;
        }

        public virtual void UpdateLeader()
        {
            float brvToBeat = 0f;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].personality.bravery > brvToBeat &&
                    !members[i].state.dead)
                {
                    brvToBeat = members[i].personality.bravery;
                    leader = members[i];
                }
            }
        }

        public virtual void CommonMovement(int dstRoom, AbstractCreature notThisOne, bool onlyInRoom)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != notThisOne && (!onlyInRoom || members[i].pos.room == leader.pos.room))
                {
                    (members[i].abstractAI as DrainMiteAbstractAI).GoToRoom(dstRoom);
                }
            }
        }

        public virtual bool DoesDrainMiteWantToBeInSwarm(DrainMiteAbstractAI miteAI)
        {
            if (leader is not null &&
                miteAI.parent != leader &&
                members.Count > 3 + miteAI.parent.personality.nervous * 3.3f)
            {
                return false;
            }
            return true;
        }
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public int dontMigrate;

    public int freeze;
    public WorldCoordinate longTermMigration;

    public bool lastInOffscreenDen;

    public bool carryingFood;
    public NoHealthState state => parent.state as NoHealthState;
    public DrainmitesWorldAI worldAI;

    public DrainmiteSwarm swarm;
    public WorldCoordinate unreachableSquadLeaderPos;
    public virtual bool UnderSwarmLeaderControl
    {
        get
        {
            if (swarm is not null &&
                swarm.leader != parent)
            {
                return true;
            }
            return false;
        }
    }

    public int safariMigrationTime;

    public int MaxPulse => 80;
    public int colorPulse;

    public float sewageDrench = 1f;

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public DrainMiteAbstractAI(World world, AbstractCreature owner) : base(world, owner)
    {
        dontMigrate = Random.Range(400, 4800);
        longTermMigration = parent.pos;
        bool initiated = false;
        for (int i = 0; i < world.worldProcesses.Count; i++)
        {
            if (world.worldProcesses[i] is DrainmitesWorldAI dmwAI)
            {
                worldAI = dmwAI;
                worldAI.AddDrainMite(this);
                initiated = true;
            }
        }
        if (!initiated)
        {
            worldAI = new DrainmitesWorldAI(world);
            worldAI.AddDrainMite(this);
            world.AddWorldProcess(worldAI);
        }
        if (world.game.IsArenaSession &&
            parent.pos.room == world.offScreenDen.index)
        {
            freeze = Random.Range(40, 400);
        }
        lastInOffscreenDen = true;
    }
    public override void NewWorld(World newWorld)
    {
        base.NewWorld(newWorld);
        if (swarm is not null)
        {
            swarm.RemoveMember(parent);
        }
        bool initiated = false;
        for (int i = 0; i < newWorld.worldProcesses.Count; i++)
        {
            if (newWorld.worldProcesses[i] is DrainmitesWorldAI dmwAI)
            {
                worldAI = dmwAI;
                worldAI.AddDrainMite(this);
                initiated = true;
            }
        }
        if (!initiated)
        {
            worldAI = new DrainmitesWorldAI(newWorld);
            worldAI.AddDrainMite(this);
            newWorld.AddWorldProcess(worldAI);
        }
        longTermMigration = parent.pos;
    }

    //--------------------------------------------------------------------------------

    public override void Update(int time)
    {
        base.Update(time);
        if (sewageDrench != 0)
        {
            sewageDrench = Mathf.Clamp01(sewageDrench - 1/24000f);
        }
    }

    public override void AbstractBehavior(int time)
    {
        if (freeze > 0)
        {
            freeze -= time;
            return;
        }
        
        if (safariMigrationTime > 0)
        {
            safariMigrationTime--;
        }

        if (parent.pos.room == MigrationDestination.room && dontMigrate > 0)
        {
            dontMigrate -= time;
        }
        if (swarm is not null &&
            !swarm.DoesDrainMiteWantToBeInSwarm(this))
        {
            swarm.RemoveMember(parent);
        }

        bool SafariControlled = ModManager.MSC && parent.controlled;
        if (parent.realizedCreature is null)
        {
            if (path.Count > 0)
            {
                FollowPath(time);
                return;
            }

            if (MigrationDestination != longTermMigration && (!SafariControlled || safariMigrationTime > 0))
            {
                SetDestination(longTermMigration);
                if (!DoIHaveAPathToCoordinate(longTermMigration))
                {
                    longTermMigration = destination;
                }
            }
            if (!SafariControlled && (
                    (world.rainCycle.TimeUntilRain < 800 && !parent.nightCreature && !parent.ignoreCycle) ||
                    (parent.nightCreature && world.rainCycle.dayNightCounter < 600) ||
                    WantToGoHome()))
            {
                if (!denPosition.HasValue ||
                    !parent.pos.CompareDisregardingTile(denPosition.Value))
                {
                    GoToDen();
                }
                return;
            }
        }
        else
        if (!SafariControlled &&
            denPosition.HasValue &&
            !destination.CompareDisregardingTile(denPosition.Value) &&
            WantToGoHome())
        {
            SetDestination(denPosition.Value);
            return;
        }

        if (swarm is not null &&
            !swarm.StayInSwarm)
        {
            swarm.RemoveMember(parent);
        }

        if (parent.pos.room == world.offScreenDen.index)
        {
            InOffscreenDen();
        }
        else
        {
            if (SafariControlled)
            {
                return;
            }
            if (lastInOffscreenDen)
            {
                lastInOffscreenDen = false;
            }

            if (UnderSwarmLeaderControl &&
                (swarm.leader.pos.room != unreachableSquadLeaderPos.room || swarm.leader.pos.abstractNode != unreachableSquadLeaderPos.abstractNode) &&
                swarm.leader.pos.room != world.offScreenDen.index &&
                swarm.leader.pos.room != MigrationDestination.room &&
                swarm.leader.abstractAI.MigrationDestination.room != MigrationDestination.room &&
                TimeInfluencedRandomRoll(parent.personality.nervous, time))
            {
                unreachableSquadLeaderPos = swarm.leader.pos.WashTileData();
                GoToRoom(swarm.leader.pos.room);
            }
            else
            if ((path.Count == 0 || parent.realizedCreature is not null) && MigrationDestination.room == parent.pos.room)
            {
                float num = parent.personality.bravery * parent.personality.energy;
                if (UnderSwarmLeaderControl)
                {
                    num /= 3f;
                }
                if (swarm is not null &&
                    swarm.leader == parent)
                {
                    num = 0.8f;
                }
                if (dontMigrate < 1 &&
                    (swarm is null || swarm.leader.abstractAI.MigrationDestination.room == parent.pos.room) &&
                    TimeInfluencedRandomRoll(0.1f / Mathf.Lerp(100f, 2f, num), time))
                {
                    Migrate(num);
                }
                else
                if (TimeInfluencedRandomRoll(parent.personality.nervous * 0.1f, time) &&
                    parent.realizedCreature is null)
                {
                    RandomMoveWithinRoom();
                }
            }
        }

    }
    public virtual void Migrate(float roaming)
    {
        bool returnToDen = swarm is null || (Random.value < 0.1f && swarm.leader == parent);
        if (returnToDen)
        {
            SetDestination(denPosition.Value);
        }
        else
        {
            RandomMoveToOtherRoom((int)Mathf.Lerp(30f, 600f, roaming));
        }
        if (MigrationDestination.room != parent.pos.room &&
            swarm is not null &&
            swarm.leader == parent)
        {
            swarm.CommonMovement(MigrationDestination.room, parent, !returnToDen);
        }
        if (returnToDen && swarm is not null)
        {
            if (swarm.leader == parent)
            {
                swarm.Dissolve();
            }
            else
            {
                swarm.RemoveMember(parent);
            }
        }
        dontMigrate = Random.Range(400, 4800);
    }
    public virtual void GoToRoom(int destRoom)
    {
        if (MigrationDestination.room == destRoom ||
            parent.pos.room == destRoom ||
            (ModManager.MSC && (parent.world.GetAbstractRoom(destRoom).shelter || parent.world.GetAbstractRoom(destRoom).gate)))
        {
            return;
        }

        List<WorldCoordinate> list = new List<WorldCoordinate>();
        List<WorldCoordinate> list2 = new List<WorldCoordinate>();
        for (int i = 0; i < parent.world.GetAbstractRoom(destRoom).nodes.Length; i++)
        {
            if (parent.world.GetAbstractRoom(destRoom).nodes[i].type.Index == -1 ||
                !parent.creatureTemplate.mappedNodeTypes[parent.world.GetAbstractRoom(destRoom).nodes[i].type.Index])
            {
                continue;
            }
            bool foundExit = true;
            if (parent.world.GetAbstractRoom(destRoom).nodes[i].type != AbstractRoomNode.Type.RegionTransportation)
            {
                foundExit = false;
            }
            for (int j = 0; j < parent.world.GetAbstractRoom(destRoom).nodes.Length; j++)
            {
                if (foundExit)
                {
                    break;
                }
                if (parent.world.GetAbstractRoom(destRoom).nodes[j].type == AbstractRoomNode.Type.RegionTransportation &&
                    parent.world.GetAbstractRoom(destRoom).ConnectionAndBackPossible(j, i, parent.creatureTemplate))
                {
                    foundExit = true;
                }
            }
            if (foundExit)
            {
                list.Add(new WorldCoordinate(destRoom, -1, -1, i));
            }
            if (worldAI.floodFiller.IsNodeAccessible(destRoom, i))
            {
                list2.Add(new WorldCoordinate(destRoom, -1, -1, i));
            }
        }
        if (list.Count > 0)
        {
            SetDestination(list[Random.Range(0, list.Count)]);
            longTermMigration = destination;
        }
        else if (list2.Count > 0)
        {
            SetDestination(list2[Random.Range(0, list2.Count)]);
            longTermMigration = destination;
        }

        if (destRoom == world.offScreenDen.index &&
            MigrationDestination.room == world.offScreenDen.index &&
            longTermMigration.room == world.offScreenDen.index)
        {
            freeze = Random.Range(300, 500);
        }
    }
    public virtual bool WantToGoHome()
    {
        if (parent.pos.room == world.offScreenDen.index)
        {
            return false;
        }
        if (carryingFood)
        {
            return true;
        }
        return false;
    }

    public virtual WorldCoordinate RandomDestinationRoom()
    {
        List<CoordinateAndFloat> list = new List<CoordinateAndFloat>();
        float roomsTotalAttraction = 0f;
        for (int i = 0; i < world.NumberOfRooms; i++)
        {
            if ((ModManager.MSC && (world.GetAbstractRoom(i + world.firstRoomIndex).shelter || world.GetAbstractRoom(i + world.firstRoomIndex).gate)) ||
                !(world.GetAbstractRoom(i + world.firstRoomIndex).AttractionForCreature(parent.creatureTemplate.type) != AbstractRoom.CreatureRoomAttraction.Forbidden) ||
                !worldAI.floodFiller.IsRoomAccessible(i + world.firstRoomIndex) ||
                (ModManager.MSC && world.game.globalRain.DrainWorldPositionFlooded(new WorldCoordinate(i + world.firstRoomIndex, 0, 11, -1))))
            {
                continue;
            }
            float attrac = world.GetAbstractRoom(i + world.firstRoomIndex).SizeDependentAttractionValueForCreature(parent.creatureTemplate.type);
            int mappedNodes = 0;
            for (int j = 0; j < world.GetAbstractRoom(i + world.firstRoomIndex).nodes.Length; j++)
            {
                if (world.GetAbstractRoom(i + world.firstRoomIndex).nodes[j].type.Index != -1 &&
                    parent.creatureTemplate.mappedNodeTypes[world.GetAbstractRoom(i + world.firstRoomIndex).nodes[j].type.Index])
                {
                    mappedNodes++;
                }
            }
            for (int k = 0; k < world.GetAbstractRoom(i + world.firstRoomIndex).nodes.Length; k++)
            {
                if (world.GetAbstractRoom(i + world.firstRoomIndex).nodes[k].type.Index != -1 && parent.creatureTemplate.mappedNodeTypes[world.GetAbstractRoom(i + world.firstRoomIndex).nodes[k].type.Index])
                {
                    float finalAttrac = attrac / (float)mappedNodes;
                    list.Add(new CoordinateAndFloat(new WorldCoordinate(i + world.firstRoomIndex, -1, -1, k), finalAttrac));
                    roomsTotalAttraction += finalAttrac;
                }
            }
        }
        float randomizedAttraction = Random.value * roomsTotalAttraction;
        for (int i = 0; i < list.Count; i++)
        {
            if (randomizedAttraction < list[i].flt)
            {
                return list[i].coord;
            }
            randomizedAttraction -= list[i].flt;
        }

        return new WorldCoordinate(world.offScreenDen.index, -1, -1, 0);
    }
    public float CostAddOfNode(WorldCoordinate coordinate)
    {
        List<int> testRooms = new() { coordinate.room };
        testRooms.AddRange(world.GetAbstractRoom(testRooms[0]).connections);

        float nodeCost = 1000f;
        for (int r = 0; r < testRooms.Count; r++)
        {
            AbstractRoom room = world.GetAbstractRoom(testRooms[r]);
            if (room?.creatures is null)
            {
                continue;
            }

            nodeCost -= nodeCost * (room.AttractionValueForCreature(parent.creatureTemplate.type) - 0.5f);

            if (testRooms[r] == coordinate.room)
            {
                nodeCost -= 100f;
            }

            CreatureTemplate.Relationship relat;
            for (int c = 0; c < room.creatures.Count; c++)
            {
                relat = parent.creatureTemplate.CreatureRelationship(room.creatures[c].creatureTemplate);
                if (relat.type == CreatureTemplate.Relationship.Type.Eats)
                {
                    nodeCost -= 25f * relat.intensity;
                }
                if (relat.type == CreatureTemplate.Relationship.Type.Pack)
                {
                    nodeCost -= 40f * relat.intensity;
                }
            }
            for (int i = 0; i < world.game.Players.Count; i++)
            {
                if (world.game.Players[i].pos.room == testRooms[r])
                {
                    nodeCost += (testRooms[r] == coordinate.room) ? 100f : 50f;
                }
            }
        }
        return nodeCost;
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public virtual void SafariModeLongTermDestination()
    {
        List<WorldCoordinate> rooms = new ();
        WorldCoordinate[] regionAccessNodes = world.regionAccessNodes;
        for (int i = 0; i < regionAccessNodes.Length; i++)
        {
            WorldCoordinate nodeCoord = regionAccessNodes[i];
            if (!WantToGoHome() &&
                nodeCoord.room != parent.Room.index &&
                nodeCoord.room != world.offScreenDen.index)
            {
                rooms.Add(nodeCoord);
            }
            else if (WantToGoHome() && nodeCoord.room == world.offScreenDen.index)
            {
                rooms.Add(nodeCoord);
            }
        }
        if (rooms.Count > 0)
        {
            WorldCoordinate worldCoordinate = rooms[Random.Range(0, rooms.Count)];
            safariMigrationTime = 80;
            SetDestination(worldCoordinate);
            longTermMigration = worldCoordinate;
            dontMigrate = 400;
        }
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public virtual void InOffscreenDen()
    {
        if (!lastInOffscreenDen)
        {
            DropOffFood();
            freeze = Random.Range(200, 400);
            lastInOffscreenDen = true;
            return;
        }
        if (carryingFood)
        {
            DropOffFood();
        }
        if ((world.rainCycle.TimeUntilRain < 800 && !parent.nightCreature && !parent.ignoreCycle) ||
            (parent.nightCreature && world.rainCycle.dayNightCounter < 600))
        {
            freeze = 100;
        }
        else if (world.singleRoomWorld)
        {
            GoToRoom(0);
        }
        else if (UnderSwarmLeaderControl)
        {
            if (swarm.leader.abstractAI.MigrationDestination.room != world.offScreenDen.index)
            {
                GoToRoom(swarm.leader.abstractAI.MigrationDestination.room);
            }
            else if (swarm.leader.abstractAI.destination.room != world.offScreenDen.index)
            {
                GoToRoom(swarm.leader.abstractAI.destination.room);
            }
        }
        else
        {
            TryToStartSwarm();

            int room =
                longTermMigration.room != world.offScreenDen.index ?
                    longTermMigration.room :
                    RandomDestinationRoom().room;

            GoToRoom(room);
        }
    }

    public virtual void DropOffFood()
    {
        carryingFood = false;
        for (int i = parent.stuckObjects.Count - 1; i >= 0; i--)
        {
            if (parent.stuckObjects[i] is AbstractPhysicalObject.CreatureGripStick &&
                parent.stuckObjects[i].A == parent &&
                parent.stuckObjects[i].B != parent)
            {
                DropAndDestroy(parent.stuckObjects[i]);
            }
        }
    }

    public virtual void DropAndDestroy(AbstractPhysicalObject.AbstractObjectStick stick)
    {
        stick.Deactivate();
        stick.B.Destroy();
    }

    public virtual void TryToStartSwarm()
    {
        if ((parent.nightCreature && world.rainCycle.dayNightCounter < 600) ||
            (world.rainCycle.TimeUntilRain < 800 && !parent.nightCreature && !parent.ignoreCycle))
        {
            return;
        }
        int potentialPackMembers = 0;
        for (int i = 0; i < parent.Room.creatures.Count; i++)
        {
            if (parent.Room.creatures[i].creatureTemplate.type == TemplateType.DrainMite &&
                parent.Room.creatures[i] != parent &&
                (parent.Room.creatures[i].abstractAI as DrainMiteAbstractAI).WillingToJoinSwarm())
            {
                potentialPackMembers++;
            }
        }
        if (potentialPackMembers < 1)
        {
            return;
        }

        WorldCoordinate randomRoom = RandomDestinationRoom();
        if (!CanRoamThroughRoom(randomRoom.room) ||
            !worldAI.floodFiller.IsRoomAccessible(randomRoom.room))
        {
            return;
        }

        if (swarm is not null)
        {
            swarm.RemoveMember(parent);
        }
        swarm = new DrainmiteSwarm(parent);

        for (int i = 0; potentialPackMembers > 0 && i < parent.Room.creatures.Count; i++)
        {
            if (parent.Room.creatures[i].creatureTemplate.type == TemplateType.DrainMite &&
                parent.Room.creatures[i] != parent &&
                (parent.Room.creatures[i].abstractAI as DrainMiteAbstractAI).WillingToJoinSwarm() &&
                swarm.members.Count <= 3 + parent.Room.creatures[i].personality.nervous * 3.3f)
            {
                swarm.AddMember(parent.Room.creatures[i]);
                potentialPackMembers--;
            }
            if (swarm.members.Count > 3 + parent.personality.sympathy * 3.3f)
            {
                break;
            }
        }
        if (!swarm.StayInSwarm)
        {
            swarm = null;
        }

        SetDestination(randomRoom);
        longTermMigration = randomRoom;
        dontMigrate = Random.Range(400, 4800);

        if (swarm is null ||
            swarm.leader != parent)
        {
            return;
        }

        swarm.CommonMovement(randomRoom.room, parent, onlyInRoom: false);
        for (int m = 0; m < swarm.members.Count; m++)
        {
            if (swarm.members[m] != parent)
            {
                DrainMiteAbstractAI otherMiteAI = swarm.members[m].abstractAI as DrainMiteAbstractAI;
                otherMiteAI.freeze = m * 10 + Random.Range(0, 10);
                otherMiteAI.dontMigrate = Random.Range(400, 4800);
            }
        }
    }

    public virtual bool WillingToJoinSwarm()
    {
        if (freeze < 1 && (swarm is null || swarm.members.Count < 2))
        {
            return true;
        }
        return false;
    }

    public override void Die()
    {
        if (swarm is not null)
        {
            swarm.RemoveMember(parent);
        }
        base.Die();
    }

}