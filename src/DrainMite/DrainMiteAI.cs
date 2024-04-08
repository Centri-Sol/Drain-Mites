namespace DrainMites.DrainMite;

public class DrainMiteAI : ArtificialIntelligence, IUseARelationshipTracker, IAINoiseReaction, IUseItemTracker, ITrackItemRelationships
{
    public class Behavior : ExtEnum<Behavior>
    {
        public static readonly Behavior Idle = new Behavior("Idle", register: true);

        public static readonly Behavior Flee = new Behavior("Flee", register: true);

        public static readonly Behavior Hunt = new Behavior("Hunt", register: true);

        public static readonly Behavior EscapeRain = new Behavior("EscapeRain", register: true);

        public static readonly Behavior ReturnPrey = new Behavior("ReturnPrey", register: true);


        public Behavior(string value, bool register = false)
            : base(value, register)
        {
        }
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public DrainMite mite => creature.realizedCreature as DrainMite;
    public DrainMiteAbstractAI AbstractAI => creature.abstractAI as DrainMiteAbstractAI;

    public Behavior behavior;
    public virtual float CurrentUtility => Mathf.Clamp01(utilityComparer?.HighestUtility() ?? 0);
    public virtual AbstractPhysicalObject CurrentPrey
    {
        get
        {
            if (preyTracker?.MostAttractivePrey?.representedCreature is null || (
                    itemFoodTracker?.MostAttractiveItem?.representedItem is not null &&
                    ObjectRelationship(itemFoodTracker.MostAttractiveItem.representedItem).intensity > ObjectRelationship(preyTracker.MostAttractivePrey.representedCreature).intensity))
            {
                return itemFoodTracker?.MostAttractiveItem?.representedItem;
            }
            return preyTracker?.MostAttractivePrey?.representedCreature;
        }
    }
    public virtual AbstractPhysicalObject CurrentThreat
    {
        get
        {
            if (threatTracker?.mostThreateningCreature?.representedCreature is null || (
                    itemThreatTracker?.mostThreateningItem?.representedItem is not null &&
                    ObjectRelationship(itemThreatTracker.mostThreateningItem.representedItem).intensity > ObjectRelationship(threatTracker.mostThreateningCreature.representedCreature).intensity))
            {
                return itemThreatTracker?.mostThreateningItem?.representedItem;
            }
            return threatTracker?.mostThreateningCreature?.representedCreature;
        }
    }
    public virtual bool PreyVisual
    {
        get
        {
            PhysicalObject prey = CurrentPrey?.realizedObject;
            if (prey is not null)
            {
                if (prey is Creature && preyTracker.MostAttractivePrey.VisualContact)
                {
                    return true;
                }
                if (prey is not Creature && itemFoodTracker.MostAttractiveItem.VisualContact)
                {
                    return true;
                }
            }
            return false;
        }
    }
    public virtual bool Panic
    {
        get
        {
            PhysicalObject threat = CurrentThreat?.realizedObject;
            if (threat is not null)
            {
                bool afraid =
                    ObjectRelationship(CurrentThreat).type == CreatureTemplate.Relationship.Type.Afraid ||
                    ObjectRelationship(CurrentThreat).type == CreatureTemplate.Relationship.Type.StayOutOfWay;
                float panicRange = mite.Template.visualRadius * 1/3f * ObjectRelationship(CurrentThreat).intensity;
                if (afraid)
                {
                    panicRange += mite.Template.visualRadius * 1/3f;
                }
                Vector2 threatPos = (threat is Creature ctr) ? ctr.DangerPos : threat.firstChunk.pos;
                if (Custom.DistLess(mite.Body.pos, threatPos, panicRange))
                {
                    return true;
                }
            }
            return false;
        }
    }
    public virtual float Excitement
    {
        get
        {
            float excitement;
            if (behavior == Behavior.Flee)
            {
                excitement = creature.personality.nervous - creature.personality.bravery;
            }
            else if (behavior == Behavior.Hunt)
            {
                excitement = (creature.personality.bravery * creature.personality.aggression) - (creature.personality.nervous * 0.5f);
            }
            else
            {
                excitement = CurrentUtility;
            }

            excitement = Mathf.Max(0, excitement);
            excitement = Mathf.Pow(excitement, 1.4f);
            if (behavior == Behavior.ReturnPrey &&
                mite.grasps[0]?.grabbed is not null)
            {
                excitement = Mathf.Lerp(excitement, 1, 0.05f + ObjectRelationship(mite.grasps[0].grabbed.abstractPhysicalObject).intensity * 0.1f);
            }
            excitement = Mathf.Lerp(excitement, 1, creature.personality.energy * 0.4f);

            return Mathf.Clamp01(excitement);
        }
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public ItemFoodTracker itemFoodTracker;
    public ItemThreatTracker itemThreatTracker;
    
    public bool onValidTile;

    public int noiseRectionDelay;

    public int idleCounter;

    public int UnsafeToJump;

    public float MinJumpForce = 12f;
    public float MaxJumpForce = 18f;

    public int MaxJumpRetries => 4;

    public virtual bool HoldingFood
    {
        get
        {
            if (mite.grasps[0]?.grabbed is not null &&
                ObjectRelationship(mite.grasps[0].grabbed.abstractPhysicalObject).type == CreatureTemplate.Relationship.Type.Eats)
            {
                return true;
            }
            return false;
        }
    }

    public int SafariJumplock;
    public int SafariJumplockThreshold = 10;
    public int SafariCoyoteTime = 10;

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public DrainMiteAI(AbstractCreature absMite, World world) : base(absMite, world)
    {
        int seeAroundCorners = 5;
        int giveupTime = 1200;
        AddModule(new StandardPather(this, world, creature));

        AddModule(new Tracker(this, seeAroundCorners, 6, 800, 0.5f, 5, 5, 30));
        AddModule(new PreyTracker(this, 3, 1f, 50, 100, 0));
        AddModule(new ThreatTracker(this, 3));
        AddModule(new RelationshipTracker(this, tracker));
        preyTracker.giveUpOnUnreachablePrey = giveupTime;

        AddModule(new ItemTracker(this, seeAroundCorners, 6, 800, 50, true));
        itemFoodTracker = new ItemFoodTracker(this, 3, 1f, 50, 100, 0);
        itemThreatTracker = new ItemThreatTracker(this, 3);
        AddModule(itemFoodTracker);
        AddModule(itemThreatTracker);
        itemFoodTracker.giveUpOnUnreachablePrey = giveupTime;

        AddModule(new NoiseTracker(this, tracker));
        AddModule(new RainTracker(this));
        AddModule(new DenFinder(this, creature));
        AddModule(new StuckTracker(this, true, false));
        stuckTracker.minStuckCounter = 320;
        stuckTracker.maxStuckCounter = 960;

        AddModule(new UtilityComparer(this));
        utilityComparer.AddComparedModule(preyTracker, null, 0.7f, 1f);
        utilityComparer.AddComparedModule(itemFoodTracker, null, 0.7f, 1f);
        utilityComparer.AddComparedModule(threatTracker, null, 1f, 1.2f);
        utilityComparer.AddComparedModule(itemThreatTracker, null, 1f, 1.2f);
        utilityComparer.AddComparedModule(rainTracker, null, 1f, 1.1f);

    }

    //--------------------------------------------------------------------------------

    public override void Update()
    {
        base.Update();

        onValidTile = mite.room.aimap.TileAccessibleToCreature(mite.Body.pos, mite.Template);

        AbstractAI.carryingFood = HoldingFood;

        if (UnsafeToJump > 0)
        {
            UnsafeToJump--;
        }

        if (noiseRectionDelay > 0)
        {
            noiseRectionDelay--;
        }

        if (mite.inputWithDiagonals is not null)
        {
            behavior = Behavior.Idle;
            SafariJumpStrength();
            SafariControls();
            return;
        }

        AbstractAI.AbstractBehavior(1);

        UpdateTrackedItems();

        UpdateBehavior();

        if (!mite.Conscious)
        {
            return;
        }

        UpdateDestination();

        UpdateJumpStrength();

        if (pathFinder is not null &&
            pathFinder is StandardPather pather)
        {

            MovementConnection connection = pather.FollowPath(creature.pos, true);
            mite.lastFollowingConnection = mite.followingConnection;
            mite.followingConnection = connection;
            if (!onValidTile &&
                !mite.room.IsPositionInsideBoundries(mite.room.GetTilePosition(mite.Body.pos)) &&
                mite.room.aimap.IsConnectionAllowedForCreature(connection, mite.Template))
            {
                onValidTile = true;
            }
            Movement(connection);
            PipeTravel(connection);

        }

    }
    public virtual void UpdateTrackedItems()
    {
        for (int i = itemTracker.ItemCount - 1; i >= 0; i--)
        {
            AIModule tracker = (this as ITrackItemRelationships).ModuleToTrackItemRelationship(itemTracker.GetRep(i).representedItem);
            if (tracker is not null)
            {
                if (tracker is ItemFoodTracker f)
                {
                    f.AddFood(itemTracker.GetRep(i));
                }
                else if (tracker is ItemThreatTracker t)
                {
                    t.AddThreatItem(itemTracker.GetRep(i));
                }
                continue;
            }
        }

    }
    public virtual void UpdateBehavior()
    {
        AIModule highestUtility = utilityComparer.HighestUtilityModule();

        if (CurrentUtility < 0.05f)
        {
            behavior = Behavior.Idle;
        }
        else if (highestUtility is PreyTracker ||
                 highestUtility is ItemFoodTracker)
        {
            behavior = Behavior.Hunt;
        }
        else if (highestUtility is ThreatTracker ||
                 highestUtility is ItemThreatTracker)
        {
            behavior = Behavior.Flee;
        }
        else if (highestUtility is RainTracker)
        {
            behavior = Behavior.EscapeRain;
        }
        
        if (behavior != Behavior.Flee &&
            mite.grasps[0]?.grabbed is not null &&
            ObjectRelationship(mite.grasps[0].grabbed.abstractPhysicalObject).type == CreatureTemplate.Relationship.Type.Eats)
        {
            behavior = Behavior.ReturnPrey;
        }
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // Movement
    public virtual void UpdateDestination()
    {

        if (behavior == Behavior.Idle)
        {

            if (pathFinder.CoordinateViable(AbstractAI.MigrationDestination))
            {
                AbstractAI.SetDestination(AbstractAI.MigrationDestination);
                idleCounter = 500;
            }
            else if (idleCounter > 0)
            {
                idleCounter--;
            }
            else if (idleCounter < 1 && !mite.jumping)
            {
                AbstractAI.SetDestination(mite.room.GetWorldCoordinate(mite.Body.pos + Custom.RNV() * mite.Template.visualRadius * Random.value));
                idleCounter = Random.Range(160, 201);
            }

        }
        else if (behavior == Behavior.Hunt)
        {

            if (CurrentPrey is not null)
            {
                WorldCoordinate dest = (CurrentPrey is AbstractCreature) switch
                {
                    true => preyTracker.MostAttractivePrey.BestGuessForPosition(),
                    _ => itemFoodTracker.MostAttractiveItem.BestGuessForPosition()
                };
                AbstractAI.SetDestination(dest);
            }

        }
        else if (behavior == Behavior.Flee)
        {

            mite.currentDest = mite.Body.pos;
            WorldCoordinate dest = (CurrentThreat is AbstractCreature) switch
            {
                true => threatTracker.FleeTo(creature.pos, 3, 100, Panic || threatTracker.Utility() >= 0.9f, AbstractAI.carryingFood || threatTracker.Utility() >= 1),
                _ => itemThreatTracker.FleeTo(creature.pos, 3, 100, Panic || itemThreatTracker.Utility() >= 0.9f, AbstractAI.carryingFood || itemThreatTracker.Utility() >= 1)
            };
            AbstractAI.SetDestination(dest);

        }
        else
        if (behavior == Behavior.EscapeRain ||
            behavior == Behavior.ReturnPrey)
        {

            if (denFinder.GetDenPosition().HasValue)
            {
                AbstractAI.SetDestination(denFinder.GetDenPosition().Value);
            }

        }

    }
    public virtual void UpdateJumpStrength()
    {
        float MaxMult = 1.4f;
        if (behavior != Behavior.Idle)
        {
            if (behavior == Behavior.Flee && Panic)
            {
                MaxMult += 0.2f;
            }
            else if (behavior == Behavior.Hunt && PreyVisual)
            {
                MaxMult += 0.2f;
            }
        }

        mite.MovementSpeed = Mathf.Lerp(1, MaxMult, CurrentUtility);

        mite.MovementSpeed *= Mathf.Lerp(0.85f, 1.15f, mite.SizeFac);

        if (stuckTracker.Utility() > 0)
        {
            mite.MovementSpeed += stuckTracker.Utility() * 0.5f;
        }

        if (mite.Submersion >= 1)
        {
            mite.MovementSpeed *= 2f;
        }
    }
    public virtual void Movement(MovementConnection connection)
    {
        bool WantsToWalk =
            WouldRatherWalk(connection) &&
            mite.HasFooting &&
            mite.jumpCooldown <= 5;

        if (!mite.CanJump && !WantsToWalk)
        {
            return;
        }

        Vector2 jumpDir = (Custom.RNV() + mite.JumpAngle * 2f).normalized;
        float jumpStrength = Random.Range(MinJumpForce, MaxJumpForce);

        if (AboveDeathPit(mite.Body.pos, out bool validTerrainOnWay) && !validTerrainOnWay)
        {
            jumpDir.y = Mathf.Abs(jumpDir.y);
        }

        Vector2 destPos;
        Vector2 dirToDest;

        if (behavior == Behavior.Hunt)
        {
            PhysicalObject prey = CurrentPrey?.realizedObject;
            if (prey is not null)
            {
                if (PreyVisual)
                {
                    destPos = (prey is Creature ctr) ? ctr.DangerPos : prey.firstChunk.pos;
                    dirToDest = Custom.DirVec(mite.Body.pos, destPos);
                    mite.currentDest = destPos;
                    DestReachabilityCheck(destPos, dirToDest, ref jumpDir, connection);
                }
                else
                {
                    destPos = mite.room.MiddleOfTile(connection.DestTile);
                    dirToDest = Custom.DirVec(mite.Body.pos, destPos);
                    mite.currentDest = destPos;
                    DestReachabilityCheck(destPos, dirToDest, ref jumpDir, connection);
                }
            }
        }
        else if (behavior == Behavior.Flee && 
           CurrentThreat?.realizedObject is not null)
        {
            PhysicalObject threat = CurrentThreat.realizedObject;
            destPos = (threat is Creature ctr) ? ctr.DangerPos : threat.firstChunk.pos;
            dirToDest = Custom.DirVec(destPos, mite.Body.pos);
            dirToDest = Vector3.Slerp(mite.JumpAngle, dirToDest, Random.Range(0.25f, 0.75f));
            DestReachabilityCheck(mite.Body.pos + dirToDest * (jumpStrength + mite.Body.rad * 15f), dirToDest, ref jumpDir, connection);
            mite.currentDest = mite.Body.pos + jumpDir * (jumpStrength + mite.Body.rad * 15f);
        }
        else
        {
            if (VisualContact(pathFinder.GetDestination, 0))
            {
                destPos = mite.room.MiddleOfTile(pathFinder.GetDestination);
            }
            else
            {
                destPos = mite.room.MiddleOfTile(connection.DestTile);
            }
            dirToDest = Custom.DirVec(mite.Body.pos, destPos);
            mite.currentDest = destPos;
            DestReachabilityCheck(destPos, dirToDest, ref jumpDir, connection);
        }


        if (WantsToWalk)
        {
            jumpStrength /= 8f;
            mite.Walk(mite.room.MiddleOfTile(connection.DestTile), jumpStrength, jumpStrength);
        }
        else
        {
            int retries;
            for (retries = 1; retries < 1 + MaxJumpRetries; retries++)
            {
                OutOfBoundsCheck(ref jumpDir, jumpStrength);
                if (IsJumpSafe(jumpDir * jumpStrength * mite.MovementSpeed))
                {
                    break;
                }
                else if (retries < MaxJumpRetries)
                {
                    jumpDir = Vector3.Slerp(jumpDir, Custom.PerpendicularVector(jumpDir) * (retries % 2 == 0 ? -1 : 1), 0.2f * retries);
                }
                else
                {
                    UnsafeToJump = 40;
                }

            }
            if (retries < MaxJumpRetries)
            {
                mite.StartJump(jumpDir, jumpStrength);
            }
            else
            {
                jumpStrength /= 8f;
                mite.Walk(mite.room.MiddleOfTile(connection.DestTile), jumpStrength, jumpStrength);
            }
        }

    }
    public virtual void PipeTravel(MovementConnection connection)
    {
        if (mite.shortcutDelay > 0)
        {
            return;
        }

        bool ScavTunnel = mite.room.shortcutData(connection.StartTile).shortCutType == ShortcutData.Type.RegionTransportation;

        if (connection.type == MovementConnection.MovementType.ShortCut ||
            connection.type == MovementConnection.MovementType.NPCTransportation ||
            ScavTunnel)
        {
            mite.enteringShortCut = connection.StartTile;

            if (connection.type == MovementConnection.MovementType.NPCTransportation)
            {
                mite.NPCTransportationDestination = connection.destinationCoord;
            }
            else
            if (ScavTunnel)
            {
                mite.NPCTransportationDestination = pathFinder.BestRegionTransportationGoal();
            }

            behavior = Behavior.Idle;

        }

    }

    public virtual void SafariJumpStrength()
    {
        mite.MovementSpeed = 1.5f;
        if (mite.Submersion >= 1)
        {
            mite.MovementSpeed *= 2f;
        }
    }
    public virtual void SafariControls()
    {

        if (pathFinder is not null &&
            pathFinder is StandardPather pather)
        {

            MovementConnection connection = pather.FollowPath(creature.pos, true);
            mite.lastFollowingConnection = mite.followingConnection;
            mite.followingConnection = connection;
            if (!onValidTile &&
                !mite.room.IsPositionInsideBoundries(mite.room.GetTilePosition(mite.Body.pos)) &&
                mite.room.aimap.IsConnectionAllowedForCreature(connection, mite.Template))
            {
                onValidTile = true;
            }

        }

        if (mite.HasFooting)
        {
            SafariCoyoteTime = 10;
        }
        else if (SafariCoyoteTime > 0)
        {
            SafariCoyoteTime--;
        }

        if (mite.jumpTime < 1 &&
            mite.jumpCooldown < 1 &&
            mite.inputWithDiagonals.Value.jmp &&
            SafariJumplock < SafariJumplockThreshold)
        {
            SafariJumplock++;
        }

        if (SafariJumplock > 0)
        {
            bool release = !mite.inputWithDiagonals.Value.jmp;
            if (mite.CanJump || SafariCoyoteTime > 0)
            {
                float jumpStrength;
                if (mite.CanSwim || SafariJumplock < SafariJumplockThreshold)
                {
                    jumpStrength = MinJumpForce;
                }
                else
                {
                    jumpStrength = MaxJumpForce;
                }

                if (release)
                {
                    Vector2 jumpDir = new Vector2(mite.inputWithDiagonals.Value.x, mite.inputWithDiagonals.Value.y).normalized;
                    mite.StartJump(jumpDir, jumpStrength);
                    SafariCoyoteTime = 0;
                }
            }
            if (release)
            {
                SafariJumplock = 0;
            }
        }
        if ((mite.HasFooting || (mite.CanSwim && mite.CanJump)) &&
            mite.inputWithDiagonals.Value.AnyDirectionalInput &&
            SafariJumplock < SafariJumplockThreshold)
        {

            Vector2 aimPos = mite.Body.pos + new Vector2(mite.inputWithDiagonals.Value.x, mite.inputWithDiagonals.Value.y).normalized;
            float speed = 2f;
            if (SafariJumplock > 0)
            {
                speed /= 2f;
            }
            if (mite.CanSwim && mite.CanJump)
            {
                speed /= 8f;
            }
            float vol = Mathf.InverseLerp(SafariJumplockThreshold, 0, SafariJumplock);
            mite.Walk(aimPos, speed, vol);

        }

        if (mite.shortcutDelay < 1)
        {
            SafariPipeTravel();
        }

    }
    public virtual void SafariPipeTravel()
    {
        IntVector2 TilePos = mite.room.GetTilePosition(mite.Body.pos);

        bool CreatureTunnel =
            mite.room.shortcutData(TilePos).shortCutType == ShortcutData.Type.NPCTransportation;

        bool ScavTunnel =
            mite.room.shortcutData(TilePos).shortCutType == ShortcutData.Type.RegionTransportation;

        if (mite.room.ShorcutEntranceHoleDirection(TilePos) != default &&
            Vector2.Dot(-mite.Body.vel, mite.room.ShorcutEntranceHoleDirection(TilePos).ToVector2()) > 0.8f)
        {
            if (mite.room.GetTile(TilePos).Terrain == Room.Tile.TerrainType.ShortcutEntrance || CreatureTunnel || ScavTunnel)
            {
                mite.enteringShortCut = TilePos;

                if (ScavTunnel)
                {
                    AbstractAI.SafariModeLongTermDestination();
                    mite.NPCTransportationDestination = AbstractAI.longTermMigration;
                }
                else if (CreatureTunnel)
                {
                    bool EntranceFound = false;
                    List<IntVector2> exits = new();
                    ShortcutData[] shortcuts = mite.room.shortcuts;
                    for (int s = 0; s < shortcuts.Length; s++)
                    {
                        ShortcutData shortcutData = shortcuts[s];
                        if (shortcutData.shortCutType == ShortcutData.Type.NPCTransportation &&
                            shortcutData.StartTile != TilePos)
                        {
                            exits.Add(shortcutData.StartTile);
                        }
                        if (shortcutData.shortCutType == ShortcutData.Type.NPCTransportation &&
                            shortcutData.StartTile == TilePos)
                        {
                            EntranceFound = true;
                        }
                    }
                    if (EntranceFound && exits.Count > 0)
                    {
                        exits.Shuffle();
                        mite.NPCTransportationDestination = mite.room.GetWorldCoordinate(exits[0]);
                    }
                }
            }
        }

    }


    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // Movement checks
    public virtual void DestReachabilityCheck(Vector2 aimPos, Vector2 dirToDest, ref Vector2 currentJumpDir, MovementConnection connection)
    {
        if (PosCanBeJumpedTo(aimPos, connection))
        {
            currentJumpDir = dirToDest;
        }
        else
        {
            currentJumpDir = Vector3.Slerp(mite.JumpAngle, dirToDest, Random.value * Random.Range(0.15f, 0.3f));
        }

        JumpAngleClamp(ref currentJumpDir);

    }
    public virtual bool PosCanBeJumpedTo(Vector2 pos, MovementConnection connection)
    {
        if (mite.room is null ||
            !mite.room.VisualContact(mite.Body.pos, pos))
        {
            return false;
        }
        if (!connection.destinationCoord.TileDefined ||
            connection.destinationCoord.x >= mite.room.TileWidth ||
            connection.destinationCoord.y >= mite.room.TileHeight)
        {
            return false;
        }
        WorldCoordinate coord = mite.room.GetWorldCoordinate(pos);
        if (!coord.TileDefined ||
            coord.x >= mite.room.TileWidth ||
            coord.y >= mite.room.TileHeight)
        {
            return false;
        }
        return true;
    }
    public virtual void JumpAngleClamp(ref Vector2 jumpDir)
    {
        float angleMatch = Vector2.Dot(mite.JumpAngle, jumpDir);
        if (angleMatch < 0.25f)
        {
            Vector2 perpAngle = Custom.PerpendicularVector(mite.JumpAngle);
            if (Vector2.Dot(jumpDir, perpAngle) > Vector2.Dot(jumpDir, -perpAngle))
            {
                jumpDir = Vector3.Slerp(perpAngle, mite.JumpAngle, 0.25f);
            }
            else
            {
                jumpDir = Vector3.Slerp(-perpAngle, mite.JumpAngle, 0.25f);
            }
        }
    }

    public virtual bool WouldRatherWalk(MovementConnection connection)
    {
        if (mite.room is null ||
            mite.TightlyGrabbed)
        {
            return false;
        }

        if (mite.room.aimap.getAItile(creature.pos).narrowSpace &&
            mite.room.GetTile(creature.pos).Terrain != Room.Tile.TerrainType.Slope)
        {
            return true;
        }
        bool validTerrainOnWay;
        if (AboveDeathPit(mite.Body.pos, out validTerrainOnWay) && !validTerrainOnWay)
        {
            return true;
        }
        if (behavior == Behavior.ReturnPrey)
        {
            for (int c = 0; c < creature.Room.nodes.Length; c++)
            {
                if (Custom.ManhattanDistance(creature.pos, mite.room.LocalCoordinateOfNode(c)) < 5 && (
                        mite.room.shortcutData(mite.room.LocalCoordinateOfNode(c).Tile).shortCutType == ShortcutData.Type.DeadEnd ||
                        mite.room.shortcutData(mite.room.LocalCoordinateOfNode(c).Tile).shortCutType == ShortcutData.Type.RegionTransportation))
                {
                    return true;
                }
            }
        }
        if (mite.room.aimap.getAItile(connection.DestTile).narrowSpace &&
            mite.room.GetTile(connection.DestTile).Terrain != Room.Tile.TerrainType.Slope)
        {
            return true;
        }
        if (AboveDeathPit(mite.room.MiddleOfTile(connection.DestTile), out validTerrainOnWay) && !validTerrainOnWay)
        {
            return true;
        }
        if (connection.type == MovementConnection.MovementType.ReachUp ||
            connection.type == MovementConnection.MovementType.ReachDown ||
            connection.type == MovementConnection.MovementType.ShortCut ||
            connection.type == MovementConnection.MovementType.NPCTransportation ||
            connection.type == MovementConnection.MovementType.RegionTransportation)
        {
            return true;
        }
        if (behavior == Behavior.ReturnPrey)
        {
            for (int c = 0; c < creature.Room.nodes.Length; c++)
            {
                if (Custom.ManhattanDistance(connection.destinationCoord, mite.room.LocalCoordinateOfNode(c)) < 5 && (
                        mite.room.shortcutData(mite.room.LocalCoordinateOfNode(c).Tile).shortCutType == ShortcutData.Type.DeadEnd ||
                        mite.room.shortcutData(mite.room.LocalCoordinateOfNode(c).Tile).shortCutType == ShortcutData.Type.RegionTransportation))
                {
                    return true;
                }
            }
        }

        if (UnsafeToJump > 0)
        {
            return true;
        }

        return false;
    }
    public virtual bool AboveDeathPit(Vector2 pos, out bool validTerrainOnWay)
    {
        validTerrainOnWay = false;
        if (mite.room is null)
        {
            return false;
        }
        if (mite.LineOfSight(pos, new Vector2(mite.Body.pos.x, 0), out validTerrainOnWay))
        {
            if (mite.room.waterObject is not null &&
                mite.room.FloatWaterLevel(mite.Body.pos.x) > 0 &&
                !mite.room.waterInverted &&
                !mite.room.waterObject.WaterIsLethal)
            {
                return false;
            }
            return true;
        }
        return false;
    }
    public virtual bool IsJumpSafe(Vector2 jumpVel)
    {
        float lastCheckDist = 0;
        Vector2 lastJumpProgressPos = default;
        Vector2 jumpProgressPos = mite.Body.pos;
        for (float t = 0; t < 600; lastCheckDist += Vector2.Distance(lastJumpProgressPos, jumpProgressPos))
        {
            lastJumpProgressPos = jumpProgressPos;
            jumpVel.y -= mite.gravity;
            jumpProgressPos += jumpVel;
            if (lastCheckDist < 10)
            {
                continue;
            }
            t += lastCheckDist;
            lastCheckDist = 0f;
            if (mite.room.GetTile(jumpProgressPos).Solid)
            {
                break;
            }
            if (mite.room.waterObject is not null &&
                mite.room.waterObject.WaterIsLethal && (
                    (!mite.room.waterInverted && mite.room.FloatWaterLevel(jumpProgressPos.x) < mite.Body.pos.y) ||
                    ( mite.room.waterInverted && mite.room.FloatWaterLevel(jumpProgressPos.x) > mite.Body.pos.y)))
            {
                return false;
            }
            if (jumpProgressPos.y < 0)
            {
                return false;
            }
        }
        if (CurrentThreat is not null &&
            ObjectRelationship(CurrentThreat).type == CreatureTemplate.Relationship.Type.StayOutOfWay &&
            Custom.ManhattanDistance(mite.room.GetWorldCoordinate(lastJumpProgressPos), CurrentThreat.pos) <= 1 + (5 * ObjectRelationship(CurrentThreat).intensity))
        {
            return false;
        }
        return true;
    }
    public virtual void OutOfBoundsCheck(ref Vector2 dir, float jumpStrength)
    {
        IntVector2 StartTile = mite.room.GetTilePosition(mite.Body.pos);
        IntVector2 DestTile = mite.room.GetTilePosition(mite.Body.pos + dir * jumpStrength * mite.MovementSpeed);
        int BOUND = 0;
        if ((StartTile.x < BOUND || DestTile.x < BOUND) && dir.x < BOUND)
        {
            dir.x *= -1;
        }
        if ((StartTile.y < BOUND || DestTile.y < BOUND) && dir.y < BOUND)
        {
            dir.y *= -1;
        }
        BOUND = mite.room.TileWidth;
        if ((StartTile.x >= BOUND || DestTile.x >= BOUND) && dir.x >= BOUND)
        {
            dir.x *= -1;
        }
        BOUND = mite.room.TileHeight;
        if ((StartTile.y >= BOUND || DestTile.y >= BOUND) && dir.y >= BOUND)
        {
            dir.y *= -1;
        }
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // Creature relationships
    RelationshipTracker.TrackedCreatureState IUseARelationshipTracker.CreateTrackedCreatureState(RelationshipTracker.DynamicRelationship rel) => null;
    public override Tracker.CreatureRepresentation CreateTrackerRepresentationForCreature(AbstractCreature otherCreature)
    {
        if (otherCreature.creatureTemplate.smallCreature)
        {
            return new Tracker.SimpleCreatureRepresentation(tracker, otherCreature, 0.4f, forgetWhenNotVisible: false);
        }
        return new Tracker.ElaborateCreatureRepresentation(tracker, otherCreature, 0.6f, 3);
    }
    AIModule IUseARelationshipTracker.ModuleToTrackRelationship(CreatureTemplate.Relationship relationship)
    {
        if (relationship.type == CreatureTemplate.Relationship.Type.StayOutOfWay ||
            relationship.type == CreatureTemplate.Relationship.Type.Afraid ||
            relationship.type == CreatureTemplate.Relationship.Type.Uncomfortable)
        {
            return threatTracker;
        }
        if (relationship.type == CreatureTemplate.Relationship.Type.Eats ||
            relationship.type == CreatureTemplate.Relationship.Type.Attacks)
        {
            return preyTracker;
        }
        return null;
    }
    CreatureTemplate.Relationship IUseARelationshipTracker.UpdateDynamicRelationship(RelationshipTracker.DynamicRelationship dynanRelat)
    {
        if (ModManager.MSC &&
            dynanRelat.trackerRep.representedCreature.creatureTemplate.type == CreatureTemplate.Type.Overseer &&
            (dynanRelat.trackerRep.representedCreature.abstractAI as OverseerAbstractAI).safariOwner)
        {
            return new CreatureTemplate.Relationship
                (CreatureTemplate.Relationship.Type.Ignores, 0);
        }

        CreatureTemplate.Relationship relationship = StaticRelationship(dynanRelat.trackerRep.representedCreature);
        relationship.intensity = Mathf.Clamp01(relationship.intensity);
        return relationship;
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // Item relationships
    public virtual CreatureTemplate.Relationship ObjectRelationship(AbstractPhysicalObject absObj)
    {

        if (absObj.type == AbstractPhysicalObject.AbstractObjectType.Creature)
        {
            return DynamicRelationship(absObj as AbstractCreature);
        }

        if (absObj.type == AbstractPhysicalObject.AbstractObjectType.WaterNut)
        {
            return new CreatureTemplate.Relationship
                (CreatureTemplate.Relationship.Type.Eats, 1);
        }
        if (absObj.type == AbstractPhysicalObject.AbstractObjectType.DangleFruit ||
            absObj.type == AbstractPhysicalObject.AbstractObjectType.EggBugEgg)
        {
            return new CreatureTemplate.Relationship
                (CreatureTemplate.Relationship.Type.Eats, 0.6f);
        }
        if (absObj.type == AbstractPhysicalObject.AbstractObjectType.SlimeMold ||
            absObj.type == AbstractPhysicalObject.AbstractObjectType.Mushroom)
        {
            return new CreatureTemplate.Relationship
                (CreatureTemplate.Relationship.Type.Eats, 0.2f);
        }

        if (absObj.type == AbstractPhysicalObject.AbstractObjectType.JellyFish ||
            absObj.type == AbstractPhysicalObject.AbstractObjectType.SporePlant ||
            absObj.type == AbstractPhysicalObject.AbstractObjectType.PuffBall)
        {
            return new CreatureTemplate.Relationship
                (CreatureTemplate.Relationship.Type.StayOutOfWay, 0.1f);
        }

        if (absObj.type == AbstractPhysicalObject.AbstractObjectType.ScavengerBomb)
        {
            return new CreatureTemplate.Relationship
                (CreatureTemplate.Relationship.Type.Uncomfortable, 0.75f);
        }

        if (ModManager.MSC)
        {
            if (absObj.type == MoreSlugcatsEnums.AbstractObjectType.LillyPuck)
            {
                return new CreatureTemplate.Relationship
                    (CreatureTemplate.Relationship.Type.Eats, 0.9f);
            }
            if (absObj.type == MoreSlugcatsEnums.AbstractObjectType.GlowWeed)
            {
                return new CreatureTemplate.Relationship
                    (CreatureTemplate.Relationship.Type.Eats, 0.7f);
            }
            if (absObj.type == MoreSlugcatsEnums.AbstractObjectType.Seed)
            {
                return new CreatureTemplate.Relationship
                    (CreatureTemplate.Relationship.Type.Eats, 0.5f);
            }

            if (absObj.type == MoreSlugcatsEnums.AbstractObjectType.FireEgg)
            {
                return new CreatureTemplate.Relationship
                    (CreatureTemplate.Relationship.Type.Uncomfortable, 1);
            }
            if (absObj.type == MoreSlugcatsEnums.AbstractObjectType.SingularityBomb)
            {
                return new CreatureTemplate.Relationship
                    (CreatureTemplate.Relationship.Type.Afraid, 0.25f);
            }
        }

        return new CreatureTemplate.Relationship
                (CreatureTemplate.Relationship.Type.Ignores, 0);
    }
    bool IUseItemTracker.TrackItem(AbstractPhysicalObject obj)
    {
        if (obj is not null &&
            obj.type != AbstractPhysicalObject.AbstractObjectType.Creature &&
            ObjectRelationship(obj).type != CreatureTemplate.Relationship.Type.Ignores &&
            ObjectRelationship(obj).type != CreatureTemplate.Relationship.Type.DoesntTrack &&
            ObjectRelationship(obj).intensity > 0)
        {
            return true;
        }
        return false;
    }
    void IUseItemTracker.SeeThrownWeapon(PhysicalObject obj, Creature thrower) {}
    AIModule ITrackItemRelationships.ModuleToTrackItemRelationship(AbstractPhysicalObject obj)
    {
        if (obj is null)
        {
            return null;
        }
        if (ObjectRelationship(obj).type == CreatureTemplate.Relationship.Type.Eats ||
            ObjectRelationship(obj).type == CreatureTemplate.Relationship.Type.Attacks)
        {
            return itemFoodTracker;
        }
        if (ObjectRelationship(obj).type == CreatureTemplate.Relationship.Type.StayOutOfWay ||
            ObjectRelationship(obj).type == CreatureTemplate.Relationship.Type.Afraid ||
            ObjectRelationship(obj).type == CreatureTemplate.Relationship.Type.Uncomfortable)
        {
            return itemThreatTracker;
        }
        return null;
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // Noise-tracking
    public void ReactToNoise(NoiseTracker.TheorizedSource source, Noise.InGameNoise noise)
    {
        if (noiseRectionDelay > 0)
        {
            return;
        }

        if (mite.Graphics is not null)
        {
            if (DynamicRelationship(source.creatureRep).intensity > DynamicRelationship(mite.Graphics.creatureLooker.lookCreature).intensity)
            {
                mite.Graphics.creatureLooker.lookCreature = source.creatureRep;
            }

            if (mite.Graphics.limbs is not null)
            {
                for (int l = 0; l < mite.Graphics.limbs.GetLength(0); l++)
                {
                    for (int s = 0; s < mite.Graphics.limbs.GetLength(1); s++)
                    {
                        mite.Graphics.limbs[l, s].pos = Vector2.Lerp(mite.Graphics.limbs[l, s].pos, mite.Body.pos, Random.value);
                    }
                }
            }
        }
        noiseRectionDelay = Random.Range(40, 81);

    }

}
