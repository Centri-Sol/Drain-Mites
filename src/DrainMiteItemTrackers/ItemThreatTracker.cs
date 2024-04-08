using System.Collections.Generic;
using UnityEngine;
using RWCustom;

namespace DrainMites;

public class ItemThreatTracker : AIModule
{
    public class ThreatPoint
    {

        public WorldCoordinate pos;

        public float severity;

        public ThreatPoint(WorldCoordinate pos, float severity)
        {
            this.pos = pos;
            this.severity = severity;
        }
    }

    public class ThreatItem
    {
        private ItemThreatTracker owner;
        private ThreatPoint threatPoint;
        public ItemTracker.ItemRepresentation itemRep;

        private float severity;
        public float CurrentThreat
        {
            get
            {
                if (itemRep.deleteMeNextFrame)
                {
                    return 0f;
                }
                return severity;
            }
        }

        public ThreatItem(ItemThreatTracker owner, ItemTracker.ItemRepresentation itemRep)
        {
            this.owner = owner;
            this.itemRep = itemRep;
            severity = owner.owner.ObjectRelationship(itemRep.representedItem).intensity/2f;
            if (owner.owner.ObjectRelationship(itemRep.representedItem).type == CreatureTemplate.Relationship.Type.Afraid)
            {
                severity += 0.5f;
            }
            threatPoint = owner.AddThreatPoint(itemRep.BestGuessForPosition(), severity);
        }

        public void Update()
        {
            severity = owner.owner.ObjectRelationship(itemRep.representedItem).intensity / 2f;
            if (owner.owner.ObjectRelationship(itemRep.representedItem).type == CreatureTemplate.Relationship.Type.Afraid)
            {
                severity += 0.5f;
            }
            itemRep.forgetCounter = 0;
            if (itemRep.BestGuessForPosition().room == owner.AI.creature.pos.room)
            {
                threatPoint.severity = CurrentThreat;
                threatPoint.pos = itemRep.BestGuessForPosition();
                return;
            }
            threatPoint.severity = 0f;
            int num = owner.AI.creature.Room.ExitIndex(itemRep.BestGuessForPosition().room);
            if (num > -1)
            {
                threatPoint.severity = CurrentThreat * 0.5f;
                threatPoint.pos = owner.AI.creature.Room.realizedRoom.ShortcutLeadingToNode(num).startCoord;
            }
        }

        public void Destroy(bool stopTracking)
        {
            owner.threatPoints.Remove(threatPoint);
            owner.threatItems.Remove(this);
        }
    }

    private AImap aiMap;

    public ITrackItemRelationships owner => AI as ITrackItemRelationships;
    private List<ThreatPoint> threatPoints;
    private List<ThreatItem> threatItems;
    public ItemTracker.ItemRepresentation mostThreateningItem;

    public WorldCoordinate savedFleeDest;
    public WorldCoordinate testFleeDest;

    private int antiFlickerCounter;

    private int resetCounter;

    private int maxRememberedItems;

    public float accessibilityConsideration;

    private float currentThreatLevel;

    private List<IntVector2> scratchPath;

    public int TotalTrackedThreatPoints => threatPoints.Count;
    public int TotalTrackedThreatItems => threatItems.Count;
    public float Panic => 1f - 1f / (currentThreatLevel + 1f);

    public override float Utility()
    {
        if (ModManager.MSC &&
            AI.creature.creatureTemplate.type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.ScavengerKing)
        {
            return 0f;
        }
        return Mathf.Clamp(Mathf.Lerp(-0.01f, 1.01f, currentThreatLevel), 0f, 1f);
    }

    public ItemThreatTracker(ArtificialIntelligence AI, int maxRememberedItems)
        : base(AI)
    {
        this.maxRememberedItems = maxRememberedItems;
        accessibilityConsideration = 5f;
        threatPoints = new List<ThreatPoint>();
        threatItems = new List<ThreatItem>();
    }

    public override void NewRoom(Room room)
    {
        aiMap = room.aimap;
        savedFleeDest = new WorldCoordinate(room.abstractRoom.index, -1, -1, Random.Range(0, room.abstractRoom.nodes.Length));
    }

    public override void Update()
    {
        currentThreatLevel = ThreatOfArea(AI.creature.pos);
        if (currentThreatLevel <= 0f)
        {
            resetCounter++;
        }
        else
        {
            resetCounter = 0;
        }
        if (antiFlickerCounter > 0)
        {
            antiFlickerCounter--;
        }
        if (resetCounter < 200)
        {
            float threatToBeat = 0f;
            mostThreateningItem = null;
            for (int t = threatItems.Count - 1; t >= 0; t--)
            {
                threatItems[t].Update();
                if (threatItems[t].itemRep.deleteMeNextFrame)
                {
                    threatItems[t].Destroy(stopTracking: false);
                }
                else if (threatItems[t].CurrentThreat > threatToBeat)
                {
                    threatToBeat = threatItems[t].CurrentThreat;
                    mostThreateningItem = threatItems[t].itemRep;
                }
            }
            if (mostThreateningItem is not null &&
                mostThreateningItem.BestGuessForPosition().room == AI.creature.pos.room)
            {
                currentThreatLevel = Mathf.Pow(currentThreatLevel, 1f / (1f + threatToBeat * 3f));
            }
        }
        else if (resetCounter >= 200 && threatItems.Count > 0)
        {
            for (int t = threatItems.Count - 1; t >= 0; t--)
            {
                threatItems[t].Destroy(stopTracking: true);
            }
        }
    }

    public WorldCoordinate FleeTo(WorldCoordinate occupyTile, int reevalutaions, int maximumDistance, bool considerLeavingRoom)
    {
        return FleeTo(occupyTile, reevalutaions, maximumDistance, considerLeavingRoom, considerGoingHome: false);
    }
    public WorldCoordinate FleeTo(WorldCoordinate occupyTile, int maxReevalutaions, int maximumDistance, bool considerLeavingRoom, bool considerGoingHome)
    {
        maxReevalutaions = AI.creature.world.game.pathfinderResourceDivider.RequestPathfinderUpdates(maxReevalutaions);
        if (!AI.pathFinder.CoordinateViable(occupyTile))
        {
            for (int i = 0; i < 4; i++)
            {
                if (AI.pathFinder.CoordinateViable(occupyTile + Custom.fourDirections[i]))
                {
                    occupyTile += Custom.fourDirections[i];
                    break;
                }
            }
        }
        int reevalutaions = 0;
        float posThreat = EvaluateFlightDestThreat(occupyTile, testFleeDest, maximumDistance, ref scratchPath);
        for (int j = 0; j < maxReevalutaions * 5; j++)
        {
            WorldCoordinate randomPos = new WorldCoordinate(occupyTile.room, occupyTile.x + Random.Range(0, maximumDistance) * (Random.value >= 0.5f ? 1 : -1), occupyTile.y + Random.Range(0, maximumDistance) * (Random.value >= 0.5f ? 1 : -1), -1);
            randomPos.x = Custom.IntClamp(randomPos.x, 0, AI.creature.Room.realizedRoom.TileWidth - 1);
            randomPos.y = Custom.IntClamp(randomPos.y, 0, AI.creature.Room.realizedRoom.TileHeight - 1);
            if (testFleeDest.Tile.FloatDist(randomPos.Tile) > 3f && aiMap.WorldCoordinateAccessibleToCreature(randomPos, AI.creature.creatureTemplate))
            {
                float otherPosThreat = EvaluateFlightDestThreat(occupyTile, randomPos, maximumDistance, ref scratchPath);
                if (otherPosThreat < posThreat)
                {
                    testFleeDest = randomPos;
                    posThreat = otherPosThreat;
                }
                reevalutaions++;
            }
            if (reevalutaions >= maxReevalutaions)
            {
                break;
            }
        }
        if (antiFlickerCounter < 1 && savedFleeDest != testFleeDest &&
            posThreat < EvaluateFlightDestThreat(occupyTile, savedFleeDest, maximumDistance, ref scratchPath) - 0.5f * Mathf.InverseLerp(1f, 0.5f, currentThreatLevel))
        {
            savedFleeDest = testFleeDest;
            antiFlickerCounter = 20;
        }
        if (considerLeavingRoom)
        {
            int destNode = -1;
            int distTOBeat = int.MaxValue;
            int randomNode = AI.creature.Room.NodesRelevantToCreature(AI.creature.creatureTemplate);
            for (int i = 0; i < randomNode; i++)
            {
                int exitDist = AI.creature.Room.realizedRoom.aimap.ExitDistanceForCreatureAndCheckNeighbours(occupyTile.Tile, i, AI.creature.creatureTemplate);
                int node = AI.creature.Room.CreatureSpecificToCommonNodeIndex(i, AI.creature.creatureTemplate);
                if (AI.pathFinder is not null &&
                    AI.timeInRoom < 100 &&
                    AI.pathFinder.forbiddenEntrance.abstractNode == node)
                {
                    exitDist = -1;
                }
                if (AI.creature.Room.nodes[node].type == AbstractRoomNode.Type.Exit && AI.creature.Room.connections[node] > -1 && mostThreateningItem != null && mostThreateningItem.BestGuessForPosition().room == AI.creature.Room.connections[node])
                {
                    exitDist = -1;
                }
                if (exitDist > 0)
                {
                    int exitDistForFly = AI.creature.Room.realizedRoom.aimap.ExitDistanceForCreatureAndCheckNeighbours(occupyTile.Tile, i, StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Fly));
                    if (exitDistForFly > 5)
                    {
                        for (int l = 0; l < threatPoints.Count; l++)
                        {
                            if (threatPoints[l].pos.room != AI.creature.Room.index)
                            {
                                continue;
                            }
                            int otherExitDistForFly = AI.creature.Room.realizedRoom.aimap.ExitDistanceForCreatureAndCheckNeighbours(threatPoints[l].pos.Tile, i, StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Fly));
                            if (otherExitDistForFly > 0)
                            {
                                otherExitDistForFly += 10;
                                if (otherExitDistForFly < exitDistForFly)
                                {
                                    exitDist = -1;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (exitDist > 0 && exitDist < distTOBeat &&
                    ((AI.creature.Room.nodes[node].type == AbstractRoomNode.Type.Exit && AI.creature.Room.connections[node] > -1) || (AI.creature.Room.nodes[node].borderExit && considerGoingHome) || (AI.creature.Room.nodes[node].type == AbstractRoomNode.Type.RegionTransportation && considerGoingHome)))
                {
                    destNode = node;
                    distTOBeat = exitDist;
                }
            }
            if (destNode > -1 && distTOBeat < maximumDistance)
            {
                if (AI.creature.Room.nodes[destNode].type == AbstractRoomNode.Type.Exit)
                {
                    AbstractRoom abstractRoom = AI.creature.world.GetAbstractRoom(AI.creature.Room.connections[destNode]);
                    int exitNode = abstractRoom.ExitIndex(AI.creature.Room.index);
                    int abstractNode = exitNode;
                    int connectLengthToBeat = int.MinValue;
                    for (int m = 0; m < abstractRoom.nodes.Length; m++)
                    {
                        if (abstractRoom.ConnectionAndBackPossible(exitNode, m, AI.creature.creatureTemplate) &&
                            abstractRoom.ConnectionLength(exitNode, m, AI.creature.creatureTemplate) > connectLengthToBeat)
                        {
                            abstractNode = m;
                            connectLengthToBeat = abstractRoom.ConnectionLength(exitNode, m, AI.creature.creatureTemplate);
                        }
                    }
                    return new WorldCoordinate(abstractRoom.index, -1, -1, abstractNode);
                }
                if (AI.creature.Room.nodes[destNode].type == AbstractRoomNode.Type.RegionTransportation ||
                    AI.creature.Room.nodes[destNode].type == AbstractRoomNode.Type.SeaExit ||
                    AI.creature.Room.nodes[destNode].type == AbstractRoomNode.Type.SideExit ||
                    AI.creature.Room.nodes[destNode].type == AbstractRoomNode.Type.SkyExit)
                {
                    return new WorldCoordinate(AI.creature.world.offScreenDen.index, -1, -1, 0);
                }
            }
        }
        return savedFleeDest;
    }

    public List<IntVector2> GenerateRandomPath(WorldCoordinate occupyTile, int length)
    {
        if (!AI.pathFinder.CoordinateViable(occupyTile))
        {
            return null;
        }
        Room realizedRoom = AI.creature.Room.realizedRoom;
        IntVector2 tile = occupyTile.Tile;
        List<IntVector2> list = new List<IntVector2>();
        IntVector2 intVector = tile;
        for (int i = 0; i < length; i++)
        {
            IntVector2 intVector2 = intVector;
            float num = 0f;
            for (int j = 0; j < realizedRoom.aimap.getAItile(intVector).outgoingPaths.Count; j++)
            {
                MovementConnection movementConnection = realizedRoom.aimap.getAItile(intVector).outgoingPaths[j];
                if (!AI.creature.creatureTemplate.ConnectionResistance(movementConnection.type).Allowed || !AI.creature.creatureTemplate.AccessibilityResistance(realizedRoom.aimap.getAItile(movementConnection.destinationCoord).acc).Allowed)
                {
                    continue;
                }
                float num2 = Random.value;
                if (!(num2 > num))
                {
                    continue;
                }
                for (int k = 0; k < list.Count; k++)
                {
                    if (!(num2 > 0f))
                    {
                        break;
                    }
                    if (list[k] == movementConnection.DestTile)
                    {
                        num2 = 0f;
                    }
                }
                if (num2 > num)
                {
                    intVector2 = movementConnection.DestTile;
                    num = num2;
                }
            }
            if (!(num > 0f))
            {
                break;
            }
            list.Add(intVector);
            intVector = intVector2;
        }
        return list;
    }

    private float EvaluateFlightDestThreat(WorldCoordinate occupyTile, WorldCoordinate coord, int maximumDistance, ref List<IntVector2> scratchPath)
    {
        if (coord.room != occupyTile.room)
        {
            return float.MaxValue;
        }
        if (Custom.ManhattanDistance(coord, occupyTile) >= maximumDistance * 2)
        {
            return float.MaxValue;
        }
        if (!AI.pathFinder.CoordinateViable(coord))
        {
            return float.MaxValue;
        }
        int num = AI.creature.Room.realizedRoom.RayTraceTilesList(occupyTile.x, occupyTile.y, coord.x, coord.y, ref scratchPath);
        bool flag = true;
        for (int i = 0; i < num && flag; i++)
        {
            if (!aiMap.TileAccessibleToCreature(scratchPath[i], AI.creature.creatureTemplate))
            {
                flag = false;
            }
        }
        if (flag)
        {
            return ThreatOfPath(scratchPath, num);
        }
        bool flag2 = false;
        num = QuickConnectivity.QuickPath(AI.creature.Room.realizedRoom, AI.creature.creatureTemplate, occupyTile.Tile, coord.Tile, maximumDistance * 2, flag2 ? 100 : 500, inOpenMedium: true, ref scratchPath);
        return ThreatOfPath(scratchPath, num);
    }

    private float ThreatOfPath(List<IntVector2> path, int pathCount)
    {
        if (path == null || pathCount == 0)
        {
            return float.MaxValue;
        }
        float num = 0f;
        for (int i = 0; i < pathCount; i++)
        {
            num += ThreatOfTile(AI.creature.Room.realizedRoom.GetWorldCoordinate(path[i]));
            for (int j = 0; j < threatPoints.Count; j++)
            {
                ThreatPoint threatPoint = threatPoints[j];
                if (threatPoint.pos.Tile == path[i])
                {
                    num += 10f * threatPoint.severity;
                }
            }
        }
        if (pathCount < 2 && ThreatOfTile(AI.creature.Room.realizedRoom.GetWorldCoordinate(path[pathCount - 1])) < 0.5f)
        {
            num += 100f;
        }
        if (ThreatOfArea(AI.creature.Room.realizedRoom.GetWorldCoordinate(path[pathCount - 1])) > ThreatOfArea(AI.creature.Room.realizedRoom.GetWorldCoordinate(path[0])))
        {
            num += 1000f;
        }
        num /= (float)pathCount;
        return num + ThreatOfArea(AI.creature.Room.realizedRoom.GetWorldCoordinate(path[pathCount - 1]));
    }

    public int FindMostAttractiveExit()
    {
        if (aiMap is null)
        {
            return -1;
        }
        float distToBeat = float.MinValue;
        int exitNode = -1;
        for (int i = 0; i < AI.creature.Room.nodes.Length; i++)
        {
            if (!AI.creature.creatureTemplate.mappedNodeTypes[(int)AI.creature.Room.nodes[i].type] || (!(AI.creature.Room.nodes[i].type != AbstractRoomNode.Type.Exit) && AI.creature.Room.connections[i] <= -1))
            {
                continue;
            }
            int exitDist = aiMap.ExitDistanceForCreature(AI.creature.pos.Tile, AI.creature.Room.CommonToCreatureSpecificNodeIndex(i, AI.creature.creatureTemplate), AI.creature.creatureTemplate);
            float dist = 0f;
            for (int j = 0; j < threatPoints.Count; j++)
            {
                ThreatPoint threatPoint = threatPoints[j];
                if (threatPoint.pos.room == AI.creature.pos.room)
                {
                    int threatExitDist = aiMap.ExitDistanceForCreature(threatPoint.pos.Tile, AI.creature.Room.CommonToCreatureSpecificNodeIndex(i, AI.creature.creatureTemplate), AI.creature.creatureTemplate);
                    if (threatExitDist < exitDist)
                    {
                        dist = -1f;
                        break;
                    }
                    dist += (float)(threatExitDist - exitDist);
                }
            }
            float nodeThreat = ThreatOfArea(aiMap.room.LocalCoordinateOfNode(i));
            dist /= nodeThreat;
            if (AI.creature.Room.nodes[i].type != AbstractRoomNode.Type.Exit)
            {
                dist = float.MinValue;
            }
            if (dist > distToBeat)
            {
                distToBeat = dist;
                exitNode = i;
            }
        }
        if (distToBeat >= 0f)
        {
            return exitNode;
        }
        return -1;
    }

    public ThreatPoint AddThreatPoint(WorldCoordinate pos, float severity)
    {
        ThreatPoint threatPoint = new ThreatPoint(pos, severity);
        threatPoints.Add(threatPoint);
        return threatPoint;
    }

    public void RemoveThreatPoint(ThreatPoint tp)
    {
        for (int i = threatPoints.Count - 1; i >= 0; i--)
        {
            if (threatPoints[i] == tp)
            {
                threatPoints.RemoveAt(i);
            }
        }
    }

    public void AddThreatItem(ItemTracker.ItemRepresentation itemRep)
    {
        foreach (ThreatItem threatCreature2 in threatItems)
        {
            if (threatCreature2.itemRep == itemRep)
            {
                return;
            }
        }
        threatItems.Add(new ThreatItem(this, itemRep));
        resetCounter = 0;
        if (threatItems.Count <= maxRememberedItems)
        {
            return;
        }
        float num = float.MaxValue;
        ThreatItem threatCreature = null;
        foreach (ThreatItem threatCreature3 in threatItems)
        {
            if (threatCreature3.CurrentThreat < num)
            {
                num = threatCreature3.CurrentThreat;
                threatCreature = threatCreature3;
            }
        }
        threatCreature?.Destroy(stopTracking: true);
    }

    public void RemoveThreatItem(AbstractPhysicalObject item)
    {
        for (int i = threatItems.Count - 1; i >= 0; i--)
        {
            if (threatItems[i].itemRep.representedItem == item)
            {
                threatItems[i].Destroy(stopTracking: false);
                break;
            }
        }
    }

    public ThreatItem GetThreatItem(AbstractPhysicalObject item)
    {
        for (int i = threatItems.Count - 1; i >= 0; i--)
        {
            if (threatItems[i].itemRep.representedItem == item)
            {
                return threatItems[i];
            }
        }
        return null;
    }

    public float ThreatOfArea(WorldCoordinate coord)
    {
        float threat = 0f;
        for (int i = 0; i < 9; i++)
        {
            threat += ThreatOfTile(WorldCoordinate.AddIntVector(coord, Custom.eightDirectionsAndZero[i]));
        }
        return threat / 9f;
    }

    public float ThreatOfTile(WorldCoordinate coord)
    {
        if (coord.room != AI.creature.pos.room)
        {
            return 0f;
        }
        float threat = 0f;
        for (int i = 0; i < threatPoints.Count; i++)
        {
            ThreatPoint threatPoint = threatPoints[i];
            if (threatPoint.pos.room == AI.creature.pos.room)
            {
                float threatOfPoint = Mathf.Sqrt(Mathf.Pow(coord.Tile.x - threatPoint.pos.Tile.x, 2f) + Mathf.Pow(coord.Tile.y - threatPoint.pos.Tile.y, 2f));
                threatOfPoint = Mathf.Pow(threatOfPoint, 1.25f);
                threatOfPoint = Mathf.Pow(threatPoint.severity, 1.5f) * 10f / Mathf.Max(1f, threatOfPoint);
                threatOfPoint *= Mathf.Lerp(1f, aiMap.getAItile(coord).visibility / (float)(aiMap.width * aiMap.height), Mathf.InverseLerp(15f, 25f, threatOfPoint));
                threat += threatOfPoint;
            }
        }
        return threat;
    }
}
