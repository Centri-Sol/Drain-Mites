using System;
using System.Collections.Generic;
using UnityEngine;
using RWCustom;

namespace DrainMites;

public class ItemFoodTracker : AIModule
{
    public class TrackedFood
    {
        public ItemFoodTracker owner;
        public ItemTracker.ItemRepresentation itemRep;

        public float Reachability
        {
            get
            {
                if (itemRep.BestGuessForPosition().room == owner.AI.creature.pos.room &&
                    owner.AI.creature.Room.realizedRoom is not null &&
                    owner.AI.creature.Room.realizedRoom.GetTile(itemRep.BestGuessForPosition()).Solid)
                {
                    return 0f;
                }
                float reachability = (owner.giveUpOnUnreachablePrey < 0) ? 1f : (Mathf.InverseLerp(owner.giveUpOnUnreachablePrey, 0, unreachableCounter) * Mathf.InverseLerp(200, 100, atPositionButCantSeeCounter));
                if (owner.AI.creature.world.GetAbstractRoom(itemRep.BestGuessForPosition()).AttractionValueForCreature(owner.AI.creature.creatureTemplate.type) < owner.AI.creature.world.GetAbstractRoom(owner.AI.creature.pos).AttractionValueForCreature(owner.AI.creature.creatureTemplate.type))
                {
                    reachability *= 0.5f;
                }
                return reachability;
            }
        }
        public int unreachableCounter;
        public int atPositionButCantSeeCounter;
        public WorldCoordinate lastBestGuessPos;
        public float EstimatedChanceOfFinding
        {
            get
            {
                float lastSeenFac = (10 + itemRep.TicksSinceSeen) / 4f;
                if (itemRep.VisualContact)
                {
                    return 1f;
                }
                if (lastSeenFac < 45f)
                {
                    return Mathf.Clamp(1f / (0f - (1f + Mathf.Pow(2.71828175f, 0f - (lastSeenFac / 12f - 5f)))) + 1.007f, 0f, 1f);
                }
                return 1f / (lastSeenFac - 10f);
            }
        }

        private float intensity;
        public float CurrentIntensity
        {
            get
            {
                if (itemRep.deleteMeNextFrame)
                {
                    return 0f;
                }
                return intensity;
            }
        }

        public TrackedFood(ItemFoodTracker owner, ItemTracker.ItemRepresentation itemRep)
        {
            this.owner = owner;
            this.itemRep = itemRep;
            intensity = owner.owner.ObjectRelationship(itemRep.representedItem).intensity / 2f;
            if (owner.owner.ObjectRelationship(itemRep.representedItem).type == CreatureTemplate.Relationship.Type.Eats)
            {
                intensity += 0.5f;
            }
        }

        public void Update()
        {
            intensity = owner.owner.ObjectRelationship(itemRep.representedItem).intensity / 2f;
            if (owner.owner.ObjectRelationship(itemRep.representedItem).type == CreatureTemplate.Relationship.Type.Eats)
            {
                intensity += 0.5f;
            }
            if (itemRep.BestGuessForPosition().room == owner.AI.creature.pos.room)
            {
                intensity = CurrentIntensity;
                return;
            }
            intensity = 0f;
            int num = owner.AI.creature.Room.ExitIndex(itemRep.BestGuessForPosition().room);
            if (num > -1)
            {
                intensity = CurrentIntensity * 0.5f;
            }

            if (owner.AI.pathFinder is not null &&
                owner.AI.pathFinder.DoneMappingAccessibility)
            {
                WorldCoordinate worldCoordinate = itemRep.BestGuessForPosition();
                bool reachable = owner.AI.pathFinder.CoordinateReachableAndGetbackable(worldCoordinate);
                if (!reachable)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (reachable)
                        {
                            break;
                        }
                        reachable = owner.AI.pathFinder.CoordinateReachableAndGetbackable(worldCoordinate + Custom.fourDirections[i]);
                    }
                }
                if (reachable)
                {
                    unreachableCounter = 0;
                }
                else
                {
                    unreachableCounter++;
                }
            }
            if (lastBestGuessPos == itemRep.BestGuessForPosition() &&
                owner.AI.creature.pos.room == itemRep.BestGuessForPosition().room &&
                owner.AI.creature.pos.Tile.FloatDist(itemRep.BestGuessForPosition().Tile) < 5f &&
                owner.AI.pathFinder is not null && owner.AI.pathFinder.GetDestination.room == itemRep.BestGuessForPosition().room &&
                owner.AI.pathFinder.GetDestination.Tile.FloatDist(itemRep.BestGuessForPosition().Tile) < 5f &&
                owner.AI.creature.Room.realizedRoom is not null && owner.AI.creature.Room.realizedRoom.VisualContact(owner.AI.creature.pos, itemRep.BestGuessForPosition()))
            {
                atPositionButCantSeeCounter += 5;
            }
            else
            {
                atPositionButCantSeeCounter--;
            }
            atPositionButCantSeeCounter = Custom.IntClamp(atPositionButCantSeeCounter, 0, 200);
            lastBestGuessPos = itemRep.BestGuessForPosition();
        }

        public bool PathFinderCanGetToPrey()
        {
            WorldCoordinate wc = itemRep.BestGuessForPosition();
            for (int i = 0; i < 9; i++)
            {
                if (owner.AI.pathFinder.CoordinateReachable(WorldCoordinate.AddIntVector(wc, Custom.eightDirectionsAndZero[i])) &&
                    owner.AI.pathFinder.CoordinatePossibleToGetBackFrom(WorldCoordinate.AddIntVector(wc, Custom.eightDirectionsAndZero[i])))
                {
                    return true;
                }
            }
            for (int j = 0; j < 4; j++)
            {
                if (owner.AI.pathFinder.CoordinateReachable(WorldCoordinate.AddIntVector(wc, Custom.fourDirections[j] * 2)) &&
                    owner.AI.pathFinder.CoordinatePossibleToGetBackFrom(WorldCoordinate.AddIntVector(wc, Custom.fourDirections[j] * 2)))
                {
                    return true;
                }
            }
            return false;
        }

        public float Attractiveness()
        {
            float intensity = owner.owner.ObjectRelationship(itemRep.representedItem).intensity;
            WorldCoordinate worldCoordinate = itemRep.BestGuessForPosition();
            float distEst = owner.DistanceEstimation(owner.AI.creature.pos, worldCoordinate);
            distEst = Mathf.Pow(distEst, 1.5f);
            distEst = Mathf.Lerp(distEst, 1f, 0.5f);
            if (owner.AI.pathFinder is not null)
            {
                if (!owner.AI.pathFinder.CoordinateReachable(worldCoordinate))
                {
                    intensity /= 2f;
                }
                if (!owner.AI.pathFinder.CoordinatePossibleToGetBackFrom(worldCoordinate))
                {
                    intensity /= 2f;
                }
                if (!PathFinderCanGetToPrey())
                {
                    intensity /= 2f;
                }
            }
            intensity *= EstimatedChanceOfFinding;
            intensity *= Reachability;
            if (itemRep.representedItem.realizedObject is not null &&
                itemRep.representedItem.realizedObject.grabbedBy.Count > 0)
            {
                intensity = (owner.AI.creature.creatureTemplate.TopAncestor() != itemRep.representedItem.realizedObject.grabbedBy[0].grabber.abstractCreature.creatureTemplate.TopAncestor()) ?
                    (intensity * owner.AI.creature.creatureTemplate.interestInOtherCreaturesCatches) :
                    (intensity * owner.AI.creature.creatureTemplate.interestInOtherAncestorsCatches);
            }
            if (worldCoordinate.room != owner.AI.creature.pos.room)
            {
                intensity *= Mathf.InverseLerp(0f, 0.5f, owner.AI.creature.world.GetAbstractRoom(worldCoordinate).AttractionValueForCreature(owner.AI.creature.creatureTemplate.type));
            }
            intensity /= distEst;
            if (owner.AI.creature.Room.world.game.IsStorySession &&
                itemRep.representedItem.type == AbstractPhysicalObject.AbstractObjectType.DataPearl)
            {
                intensity /= 10f;
            }
            return intensity;
        }
    }

    public ITrackItemRelationships owner => AI as ITrackItemRelationships;
    private List<TrackedFood> food;
    private TrackedFood focusItem;

    private int maxRememberedItems;
    private float persistanceBias;

    private float sureToGetFoodDistance;
    private float sureToLoseFoodDistance;

    public float frustration;
    private float frustrationSpeed;

    public int giveUpOnUnreachablePrey = 400;

    public AImap aimap
    {
        get
        {
            if (AI.creature.realizedCreature.room is null)
            {
                return null;
            }
            return AI.creature.realizedCreature.room.aimap;
        }
    }

    public int TotalTrackedFood => food.Count;

    public ItemTracker.ItemRepresentation MostAttractiveItem
    {
        get
        {
            if (focusItem is not null)
            {
                return focusItem.itemRep;
            }
            return null;
        }
    }

    public ItemTracker.ItemRepresentation GetTrackedFood(int index)
    {
        return food[index].itemRep;
    }

    public override float Utility()
    {
        if (focusItem is null)
        {
            return 0f;
        }
        if (AI.creature.abstractAI.WantToMigrate &&
            focusItem.itemRep.BestGuessForPosition().room != AI.creature.abstractAI.MigrationDestination.room &&
            focusItem.itemRep.BestGuessForPosition().room != AI.creature.pos.room)
        {
            return 0f;
        }
        float utility = DistanceEstimation(AI.creature.pos, focusItem.itemRep.BestGuessForPosition(), AI.creature.creatureTemplate);
        utility = Mathf.Lerp(Mathf.InverseLerp(sureToLoseFoodDistance, sureToGetFoodDistance, utility), Mathf.Lerp(sureToGetFoodDistance, sureToLoseFoodDistance, 0.25f) / utility, 0.5f);
        utility *= Mathf.Pow(focusItem.CurrentIntensity, 0.75f);
        utility = Mathf.Min(utility, Mathf.Pow(focusItem.CurrentIntensity, 0.75f) * focusItem.Reachability);
        return utility;
    }

    public ItemFoodTracker(ArtificialIntelligence AI, int maxRememberedItems, float persistanceBias, float sureToGetFoodDistance, float sureToLoseFoodDistance, float frustrationSpeed)
        : base(AI)
    {
        this.maxRememberedItems = maxRememberedItems;
        this.persistanceBias = persistanceBias;
        this.sureToGetFoodDistance = sureToGetFoodDistance;
        this.sureToLoseFoodDistance = sureToLoseFoodDistance;
        this.frustrationSpeed = frustrationSpeed;
        food = new List<TrackedFood>();
    }

    public void AddFood(ItemTracker.ItemRepresentation newItem)
    {
        foreach (TrackedFood item in food)
        {
            if (item.itemRep == newItem)
            {
                return;
            }
        }
        food.Add(new TrackedFood(this, newItem));
        if (food.Count > maxRememberedItems)
        {
            float lowestAttrac = float.MaxValue;
            TrackedFood trackedPrey = null;
            foreach (TrackedFood item2 in food)
            {
                if (item2.Attractiveness() < lowestAttrac)
                {
                    lowestAttrac = item2.Attractiveness();
                    trackedPrey = item2;
                }
            }
            trackedPrey.itemRep.Destroy();
            food.Remove(trackedPrey);
        }
        Update();
    }

    public void ForgetPrey(AbstractPhysicalObject item)
    {
        for (int i = food.Count - 1; i >= 0; i--)
        {
            if (food[i].itemRep.representedItem == item)
            {
                food.RemoveAt(i);
            }
        }
    }

    public void ForgetAllFood()
    {
        food.Clear();
        focusItem = null;
    }

    public override void Update()
    {
        float attracToBeat = float.MinValue;
        TrackedFood trackedPrey = null;
        for (int i = food.Count - 1; i >= 0; i--)
        {
            food[i].Update();
            float itemAttrac = food[i].Attractiveness();
            food[i].itemRep.forgetCounter = 0;
            if (food[i] == focusItem)
            {
                itemAttrac *= persistanceBias;
            }
            if (food[i].itemRep.deleteMeNextFrame)
            {
                food.RemoveAt(i);
            }
            else if (itemAttrac > attracToBeat)
            {
                attracToBeat = itemAttrac;
                trackedPrey = food[i];
            }
        }
        focusItem = trackedPrey;

        if (frustrationSpeed > 0 &&
            focusItem is not null &&
            AI.pathFinder is not null &&
            AI.creature.pos.room == focusItem.itemRep.BestGuessForPosition().room &&
            !focusItem.PathFinderCanGetToPrey())
        {
            frustration = Mathf.Clamp(frustration + frustrationSpeed, 0f, 1f);
        }
        else
        {
            frustration = Mathf.Clamp(frustration - frustrationSpeed * 4f, 0f, 1f);
        }

    }

    public float DistanceEstimation(WorldCoordinate from, WorldCoordinate to, CreatureTemplate crit = null)
    {
        if (crit is not null &&
            from.room != to.room)
        {
            if (AI.creature.world.GetAbstractRoom(from).realizedRoom is not null &&
                AI.creature.world.GetAbstractRoom(from).realizedRoom.readyForAI &&
                AI.creature.world.GetAbstractRoom(from).ExitIndex(to.room) > -1)
            {
                int creatureSpecificExitIndex = AI.creature.world.GetAbstractRoom(from).CommonToCreatureSpecificNodeIndex(AI.creature.world.GetAbstractRoom(from).ExitIndex(to.room), crit);
                int exitDist = AI.creature.world.GetAbstractRoom(from).realizedRoom.aimap.ExitDistanceForCreatureAndCheckNeighbours(from.Tile, creatureSpecificExitIndex, crit);

                if (exitDist > -1 &&
                    crit.ConnectionResistance(MovementConnection.MovementType.SkyHighway).Allowed &&
                    AI.creature.world.GetAbstractRoom(from).AnySkyAccess &&
                    AI.creature.world.GetAbstractRoom(to).AnySkyAccess)
                {
                    exitDist = Math.Min(exitDist, 50);
                }
                if (exitDist > -1)
                {
                    return exitDist;
                }
            }
            return 50f;
        }
        return Vector2.Distance(IntVector2.ToVector2(from.Tile), IntVector2.ToVector2(to.Tile));
    }
}