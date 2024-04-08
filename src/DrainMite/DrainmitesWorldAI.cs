using Fisobs.Core;
using System.Collections.Generic;
using UnityEngine;

namespace DrainMites.DrainMite;

public class DrainmitesWorldAI : World.WorldProcess
{

    public class WorldFloodFiller
    {
        private World world;

        private List<WorldCoordinate> checkNext;

        private bool[][] nodesMatrix;

        private bool[] roomsMatrix;

        public bool finished;

        public bool IsRoomAccessible(WorldCoordinate node)
        {
            return IsRoomAccessible(node.room);
        }

        public bool IsRoomAccessible(int room)
        {
            return roomsMatrix[room - world.firstRoomIndex];
        }

        public bool IsNodeAccessible(WorldCoordinate node)
        {
            return IsNodeAccessible(node.room, node.abstractNode);
        }

        public bool IsNodeAccessible(int room, int node)
        {
            return nodesMatrix[room - world.firstRoomIndex][node];
        }

        public WorldFloodFiller(World world, WorldCoordinate startPosition)
        {
            this.world = world;
            nodesMatrix = new bool[world.NumberOfRooms][];
            for (int i = 0; i < world.NumberOfRooms; i++)
            {
                nodesMatrix[i] = new bool[world.GetAbstractRoom(i + world.firstRoomIndex).nodes.Length];
            }
            roomsMatrix = new bool[world.NumberOfRooms];
            checkNext = new List<WorldCoordinate>();
            for (int room = 0; room < world.NumberOfRooms; room++)
            {
                for (int node = 0; node < world.GetAbstractRoom(room + world.firstRoomIndex).nodes.Length; node++)
                {
                    if (world.GetAbstractRoom(room + world.firstRoomIndex).nodes[node].type == AbstractRoomNode.Type.RegionTransportation)
                    {
                        checkNext.Add(new WorldCoordinate(room + world.firstRoomIndex, -1, -1, node));
                        roomsMatrix[room] = true;
                        nodesMatrix[room][node] = true;
                    }
                }
            }

        }

        public void Update()
        {
            if (checkNext.Count < 1)
            {
                finished = true;
                return;
            }
            WorldCoordinate nodeCoord = checkNext[0];
            checkNext.RemoveAt(0);
            if (world.GetNode(nodeCoord).type == AbstractRoomNode.Type.Exit &&
                world.GetAbstractRoom(nodeCoord).connections[nodeCoord.abstractNode] > -1)
            {
                WorldCoordinate newNodeCoord = new (world.GetAbstractRoom(nodeCoord).connections[nodeCoord.abstractNode], -1, -1, world.GetAbstractRoom(world.GetAbstractRoom(nodeCoord).connections[nodeCoord.abstractNode]).ExitIndex(nodeCoord.room));
                if (!nodesMatrix[newNodeCoord.room - world.firstRoomIndex][newNodeCoord.abstractNode])
                {
                    checkNext.Add(newNodeCoord);
                    roomsMatrix[newNodeCoord.room - world.firstRoomIndex] = true;
                    nodesMatrix[newNodeCoord.room - world.firstRoomIndex][newNodeCoord.abstractNode] = true;
                }
            }
            for (int i = 0; i < world.GetAbstractRoom(nodeCoord).nodes.Length; i++)
            {
                if (!nodesMatrix[nodeCoord.room - world.firstRoomIndex][i] &&
                    i != nodeCoord.abstractNode && world.GetAbstractRoom(nodeCoord).ConnectionAndBackPossible(nodeCoord.abstractNode, i, StaticWorld.GetCreatureTemplate(TemplateType.DrainMite)))
                {
                    checkNext.Add(new WorldCoordinate(nodeCoord.room, -1, -1, i));
                    roomsMatrix[nodeCoord.room - world.firstRoomIndex] = true;
                    nodesMatrix[nodeCoord.room - world.firstRoomIndex][i] = true;
                }
            }
        }
    }

    public List<DrainMiteAbstractAI> drainMites;

    public WorldFloodFiller floodFiller;

    public DrainmitesWorldAI(World world)
        : base(world)
    {
        base.world = world;
        drainMites = new ();
        floodFiller = new WorldFloodFiller(world, new WorldCoordinate(world.offScreenDen.index, -1, -1, 0));
    }
    public void AddDrainMite(DrainMiteAbstractAI newMite)
    {
        for (int i = 0; i < drainMites.Count; i++)
        {
            if (drainMites[i] == newMite)
            {
                return;
            }
        }
        drainMites.Add(newMite);
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public override void Update()
    {
        if (!floodFiller.finished)
        {
            floodFiller.Update();
        }
        if (drainMites.Count == 0)
        {
            return;
        }
        DrainMiteAbstractAI drainMiteAbstractAI = drainMites[Random.Range(0, drainMites.Count)];
        if (drainMiteAbstractAI.parent.state.dead)
        {
            drainMites.Remove(drainMiteAbstractAI);
        }
    }

}
