
using System.Globalization;

namespace DrainMites.DrainMite;

public class DrainMite : InsectoidCreature, Weapon.INotifyOfFlyingWeapons, IPlayerEdible
{
    public bool Edible => true;
    public int BitesLeft => bites;
    public int bites = 3;
    public int FoodPoints => room is not null && room.game.IsStorySession ? QuarterPips : QuarterPips/4;
    public int QuarterPips;
    public bool AutomaticPickUp => false;

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public float Size;
    public struct IndividualVariations
    {

        public float mass;
        public float limbLength;
        public Color color;

        public IndividualVariations(float size)
        {
            mass = size / 5f;
            limbLength = 12f * Mathf.Pow(1.3f, size);
            color = new HSLColor(
                        Custom.WrappedRandomVariation(0.06f, 0.04f, 0.5f),
                        Random.Range(0.4f, 0.6f),
                        Custom.WrappedRandomVariation(0.05f, 0.03f, 0.25f)
                        ).rgb;
        }

    }
    public IndividualVariations IVars;

    public BodyChunk Body => firstChunk;
    public DrainMiteGraphics Graphics => graphicsModule as DrainMiteGraphics;
    public DrainMiteAI AI => abstractCreature?.abstractAI?.RealAI as DrainMiteAI;
    public DrainMiteAbstractAI AbstractAI => abstractCreature?.abstractAI as DrainMiteAbstractAI;

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public virtual float SizeFac => Mathf.InverseLerp(0.6f, 1.4f, Size);

    public Vector2 lastBodyDir;
    public Vector2 bodyDir;
    public Vector2? bodyDirGoal;
    public Vector2 dragPos;
    public virtual Vector2 JumpAngle => bodyDirGoal ?? bodyDir;

    public float MovementSpeed;

    public bool jumping;
    public int jumpWindup;
    public int jumpTime;
    public int jumpCooldown;
    public bool jumpedToDodge;
    public int dodgeCooldown;
    public int jumpStruggleCounter;
    public Vector2 JumpVelocity;

    public Vector2? currentDest;

    public int walkTime;
    public virtual bool Walking => walkTime > 0;

    public MovementConnection lastFollowingConnection;
    public MovementConnection followingConnection;

    public int spasmTimer;
    public float spasmStrength;

    public Color ShortcutColor = new HSLColor(0, 0, 0.9f).rgb;

    public int SafariDropCooldown;

    public int sewageDripCooldown;
    public virtual bool CanDrip => sewageDripCooldown < 1 && AbstractAI.sewageDrench > 0;

    //----------------------------------------

    public virtual bool TightlyGrabbed
    {
        get
        {
            if (grabbedBy.Count > 0)
            {
                for (int g = 0; g < grabbedBy.Count; g++)
                {
                    if (grabbedBy[g].pacifying ||
                        grabbedBy[g].shareability != Grasp.Shareability.NonExclusive)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
    public virtual bool Conscious => (Consious || (!dead && TightlyGrabbed)) && spasmTimer < 1;
    public virtual bool HasFooting
    {
        get
        {
            if (jumping || CanSwim)
            {
                return false;
            }
            if (AI is null || (!AI.onValidTile && !(Walking && NearSolidGround())))
            {
                return false;
            }
            return true;
        }
    }
    public virtual bool CanJump
    {
        get
        {
            if ((HasFooting || (CanSwim && (Submersion < 1 || jumpTime < 1)) || TightlyGrabbed) && jumpCooldown < 1 && (!Walking || inputWithDiagonals is not null))
            {
                return true;
            }
            return false;
        }
    }
    public virtual bool CanSwim => Submersion >= 0.5f;
    public virtual bool WillingToGrabBackground
    {
        get
        {
            if (HasFooting)
            {
                return true;
            }
            if (inputWithDiagonals is not null)
            {
                if (inputWithDiagonals.Value.pckp)
                {
                    return true;
                }
                return false;
            }
            if (jumping && Body.vel.y <= 0)
            {
                if (jumpTime > 200 || (AI is not null && AI.AboveDeathPit(Body.pos, out _) && !CanSwim))
                {
                    return true;
                }
                if (currentDest.HasValue)
                {
                    if (jumpTime >= 20 &&
                        room is not null &&
                        room.VisualContact(Body.pos, currentDest.Value) &&
                        AI is not null &&
                        AI.onValidTile)
                    {
                        return true;
                    }
                }
                
            }
            return false;
        }
    }
    public virtual bool Moving => jumping || walkTime > 0;

    public int MaxJumpWindup;
    public int MaxJumpCooldown;


    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public DrainMite(AbstractCreature absMite, World world) : base(absMite, world)
    {
        GenerateSize(absMite);

        Random.State state = Random.state;
        Random.InitState(absMite.ID.RandomSeed);
        IVars = new IndividualVariations(Size);
        Random.state = state;

        bodyChunks = new BodyChunk[1];
        bodyChunks[0] = new BodyChunk(this, 0, default, Size * 8f, IVars.mass);
        bodyChunkConnections = new BodyChunkConnection[0];

        GoThroughFloors = true;
        gravity = 1f;
        surfaceFriction = 0.3f;
        waterFriction = 1f;
        airFriction = 1f;
        bounce = 0;
        buoyancy = 1f;
        collisionLayer = 0;

        MaxJumpWindup = 16;
        MaxJumpCooldown = 100;

        bodyDir = Custom.RNV();

    }
    public override void InitiateGraphicsModule()
    {
        if (graphicsModule is null)
        {
            graphicsModule = new DrainMiteGraphics(this);
        }
    }
    public override void NewRoom(Room room)
    {
        base.NewRoom(room);
        ChangeCollisionLayer(0);
    }
    public virtual void GenerateSize(AbstractCreature absMite)
    {
        bool SizeChanged = false;
        bool FoodChanged = false;

        Size = 10f;
        if (abstractCreature.spawnData is not null &&
            abstractCreature.spawnData[0] == '{')
        {
            string[] flags = abstractCreature.spawnData.Substring(1, abstractCreature.spawnData.Length - 2).Split(new char[1] { ',' });
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i].Length <= 0 || !float.TryParse(flags[i].Split(new char[1] { ':' })[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float Multiplier))
                {
                    continue;
                }

                Multiplier = Mathf.Clamp(Multiplier, 0.5f, 1.5f);

                bool both = flags[i].Split(new char[1] { ':' })[0] == "SizeMult";

                if (both || flags[i].Split(new char[1] { ':' })[0] == "BodySize")
                {
                    SizeChanged = true;
                    Size *= Custom.WrappedRandomVariation(0.1f * Mathf.Abs(Multiplier), 0.009f + (0.001f * Mathf.Abs(Multiplier)), 0.4f);
                }
                if (both || flags[i].Split(new char[1] { ':' })[0] == "FoodPips")
                {
                    FoodChanged = true;
                    QuarterPips = (int)(Multiplier * 4f);
                }

                break;
            }
        }

        if (!SizeChanged)
        {
            Size *= Custom.WrappedRandomVariation(0.1f, 0.04f, 0.3f);
        }
        if (!FoodChanged)
        {
            QuarterPips = absMite.creatureTemplate.meatPoints;
        }
    }

    //--------------------------------------------------------------------------------

    public override void Update(bool eu)
    {
        base.Update(eu);
        if (room is null)
        {
            return;
        }
        if (room.game.devToolsActive &&
            Input.GetKey("b") &&
            room.game.cameras[0].room == room)
        {
            Body.vel += Custom.DirVec(Body.pos, (Vector2)Input.mousePosition + room.game.cameras[0].pos) * 6f;
            Stun(12);
        }
        if (lungs < 0.5f)
        {
            lungs = 1f;
        }
        if (poison > 0)
        {
            poison *= 0.99f;
        }
        if (CanSwim && AbstractAI.sewageDrench < 1)
        {
            if (room.waterObject is not null)
            {
                AbstractAI.sewageDrench += 1 / Mathf.Lerp(2400, 400, room.waterObject.viscosity);
            }
            else
            {
                AbstractAI.sewageDrench += 1 / 2400f;
            }
        }

        BodyDirUpdate();

        if (spasmTimer > 0)
        {
            spasmTimer--;
        }
        if (jumpStruggleCounter > 0 && grabbedBy.Count < 1)
        {
            jumpStruggleCounter = 0;
        }

        if (dead)
        {
            return;
        }

        if (AI is not null)
        {
            AI.Update();
        }

        MovementUpdate();

        GraspUpdate(eu);

    }
    public virtual void BodyDirUpdate()
    {
        bool controlledJump =
            AI is not null &&
            AI.SafariJumplock >= AI.SafariJumplockThreshold;

        if (!controlledJump && Conscious && Body.ContactPoint != default)
        {
            bodyDirGoal = -Body.ContactPoint.ToVector2().normalized;
        }
        if (bodyDirGoal.HasValue)
        {
            bodyDir = Vector3.Slerp(bodyDir, bodyDirGoal.Value, 0.15f);
            dragPos = Body.pos - bodyDir * Body.rad;
            if (Vector2.Dot(bodyDir, bodyDirGoal.Value) > 0.95f)
            {
                bodyDir = bodyDirGoal.Value;
                bodyDirGoal = null;
            }
        }
        else
        {
            dragPos = Body.pos + Custom.DirVec(Body.pos, dragPos) * Body.rad;
            bodyDir -= Custom.DirVec(Body.pos, dragPos);
            bodyDir += Body.vel * 0.2f;
        }
        lastBodyDir = bodyDir;

        if (!Conscious && spasmTimer > 0)
        {
            bodyDir += Custom.DegToVec(Random.value * 360f) * spasmStrength * Mathf.InverseLerp(0, 15, spasmTimer);
        }
        bodyDir.Normalize();
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public virtual void MovementUpdate()
    {
        if (CanSwim)
        {
            if (!jumping)
            {
                if (inputWithDiagonals is not null)
                {
                    Body.vel *= jumpCooldown > 0 ? 0.975f : 0.75f;
                }
                else
                {
                    Body.vel *= 0.975f;
                }
            }
        }
        else if (HasFooting)
        {
            if (Walking)
            {
                Body.vel *= 0.6f;
            }
            else
            {
                bool onThinFoothold = room.GetTile(Body.pos).AnyBeam || room.GetTile(Body.pos).Terrain == Room.Tile.TerrainType.Floor;
                if (onThinFoothold)
                {
                    Body.vel *= 0.15f;
                }
                else
                {
                    Body.vel *= 0.4f;
                }
            }
            Body.vel.y += gravity;
        }


        if (jumpCooldown > 0)
        {
            jumpCooldown--;
        }
        if (dodgeCooldown > 0)
        {
            dodgeCooldown--;
        }

        if (Walking)
        {
            walkTime--;
            Body.terrainSqueeze = Mathf.Lerp(Body.terrainSqueeze, 0.33f, 0.1f);
        }
        else
        {
            if (Body.terrainSqueeze != 1)
            {
                Body.terrainSqueeze = Mathf.Lerp(Body.terrainSqueeze, 1, 0.1f);
            }
        }

        if (jumping)
        {
            jumpTime++;
            if (CancelJump())
            {
                jumping = false;
            }
        }
        else
        if (jumpWindup > 0 &&
            CanJump &&
            AI is not null)
        {
            if (jumpWindup >= Mathf.Lerp(MaxJumpWindup, MaxJumpWindup/2f, AI.Excitement) || TightlyGrabbed)
            {
                jumpWindup = 0;
                Jump(JumpVelocity);
            }
            else
            {
                jumpWindup++;
            }
        }

        if (jumping)
        {
            bodyDir = Vector3.Slerp(bodyDir, Body.vel.normalized, 0.2f);
        }
        else if (jumpTime > 0)
        {
            JumpEnded();
        }

    }
    public virtual void TryToGrabFood()
    {

        if (grasps[0] is null &&
            AI is not null)
        {
            bool stillEmptyHanded = true;

            if (AI.tracker is not null)
            {
                for (int p = 0; p < AI.tracker.CreaturesCount; p++)
                {
                    Creature jumpTarget = AI.tracker.GetRep(p).representedCreature?.realizedCreature;
                    if (!WantToGrab(jumpTarget))
                    {
                        continue;
                    }
                    foreach (BodyChunk chunk in jumpTarget.bodyChunks)
                    {
                        if (Custom.DistLess(Body.pos, chunk.pos, (Body.rad + chunk.rad) * 2f))
                        {
                            GrabTarget(jumpTarget, chunk.index);
                            stillEmptyHanded = false;
                            break;
                        }
                    }
                }
            }
            if (stillEmptyHanded && AI.itemTracker is not null)
            {
                for (int i = 0; i < AI.itemTracker.ItemCount; i++)
                {
                    PhysicalObject jumpTarget = AI.itemTracker.GetRep(i).representedItem?.realizedObject;
                    if (!WantToGrab(jumpTarget))
                    {
                        continue;
                    }
                    foreach (BodyChunk chunk in jumpTarget.bodyChunks)
                    {
                        if (Custom.DistLess(Body.pos, chunk.pos, (Body.rad + chunk.rad) * 2f))
                        {
                            GrabTarget(jumpTarget, chunk.index);
                            stillEmptyHanded = false;
                            break;
                        }
                    }
                }
            }
        }

    }
    public virtual void GraspUpdate(bool eu)
    {

        if (SafariDropCooldown > 0)
        {
            SafariDropCooldown--;
        }

        if ((inputWithDiagonals is null && Moving) ||
            (inputWithDiagonals is not null && SafariDropCooldown < 1 && (jumping || inputWithDiagonals.Value.pckp)))
        {
            TryToGrabFood();
        }
        if (inputWithDiagonals is not null &&
            inputWithDiagonals.Value.thrw &&
            grasps[0] is not null)
        {
            ReleaseGrasp(0);
            SafariDropCooldown = 8;
        }

        if (grasps[0] is not null)
        {
            if (grasps[0].grabbed is null &&
            grasps[0].grabbed is not Creature &&
            AI.ObjectRelationship(grasps[0].grabbed.abstractPhysicalObject).type == CreatureTemplate.Relationship.Type.Ignores)
            {
                base.ReleaseGrasp(0);
                return;
            }

            if (grasps[0].grabbed is not null)
            {
                BodyChunk chunk = grasps[0].grabbedChunk;
                chunk.MoveFromOutsideMyUpdate(eu, dragPos);
                chunk.vel = Body.vel;
            }
        }

    }

    public void StartJump(Vector2 dir, float strength)
    {
        jumpWindup++;
        JumpVelocity = dir * strength;
    }
    public virtual bool CancelJump()
    {

        if (TightlyGrabbed)
        {
            return true;
        }

        if (Body.ContactPoint != default &&
            jumpTime >= 3)
        {
            return true;
        }

        if (CanSwim)
        {
            if (jumpTime > 20)
            {
                return true;
            }
        }
        else
        if (WillingToGrabBackground && (
                room.GetTile(Body.pos).AnyBeam ||
                room.GetTile(Body.pos).wallbehind ||
                room.GetTile(Body.pos).Terrain == Room.Tile.TerrainType.Floor))
        {
            return true;
        }

        return false;
    }
    public virtual void JumpEnded()
    {
        int buffer = 10;
        if (AI is not null &&
            AI.Panic &&
            !TightlyGrabbed)
        {
            buffer -= 4;
        }
        if (TightlyGrabbed)
        {
            if (inputWithDiagonals is not null)
            {
                jumpCooldown = 10;
            }
            else
            {
                jumpCooldown = Random.Range(10, 20);
            }
        }
        else
        {
            jumpCooldown = Mathf.Min(jumpTime + buffer, MaxJumpCooldown);
        }

        TerrainImpact(0, new IntVector2(0, 0), Body.vel.magnitude, Body.ContactPoint == new IntVector2(0, 0));

        jumpTime = 0;
        if (jumpedToDodge)
        {
            jumpCooldown += jumpCooldown / 2;
            jumpedToDodge = false;
        }
        if (!NearSolidGround())
        {
            bodyDirGoal = Custom.RNV();
        }
    }
    
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public virtual void Jump(Vector2 jumpVel)
    {

        jumping = true;
        Body.vel += jumpVel * MovementSpeed;
        bodyDirGoal = null;
        JumpVelocity = default;

        if (room is not null)
        {
            room.PlaySound(SoundID.Slugcat_Throw_Rock, Body, false, 0.9f, 1);
            if (AI is not null &&
                AI.Excitement > Random.value)
            {
                room.PlaySound(SoundID.Mouse_Squeak, Body, false, 0.9f, Custom.LerpMap(IVars.mass, 0.12f, 0.28f, 2.3f, 1.9f));
            }

            if (CanDrip)
            {
                int DropCount = (int)Mathf.Lerp(1, 7.25f, AbstractAI.sewageDrench) + Random.Range(-1, 2);
                for (int d = DropCount; d > 0; d--)
                {
                    Vector2 vel = (jumpVel.normalized * Random.Range(4f, 8f) + Custom.RNV() * Random.Range(2f, 4f)) * Mathf.Lerp(jumpVel.magnitude / 10f, 1, 0.5f);
                    DropADrip(vel);
                }
            }

        }


        if (grabbedBy.Count > 0)
        {
            jumpStruggleCounter++;
            for (int i = grabbedBy.Count - 1; i >= 0; i--)
            {
                if (grabbedBy[i]?.grabber is null)
                {
                    continue;
                }
                Creature grabber = grabbedBy[i]?.grabber;
                if (grabber.firstChunk is not null)
                {
                    BodyChunk chunk = grabber.firstChunk;
                    Vector2 pushForce = jumpVel * 0.75f * MovementSpeed / grabbedBy.Count;
                    float massFac = TotalMass / (TotalMass + Mathf.Pow(chunk.mass, 0.875f));
                    Body.vel += pushForce * (1f - massFac);
                    Body.pos += pushForce * (1f - massFac);
                    chunk.vel += pushForce * massFac;
                    chunk.pos += pushForce * massFac;
                }
                float mult = (inputWithDiagonals is not null) ? 0.1f : 0.05f;
                if (Random.value < Mathf.InverseLerp(grabber.TotalMass, grabber.TotalMass * 2f, jumpStruggleCounter * mult))
                {
                    grabber.ReleaseGrasp(grabbedBy[i].graspUsed);
                    continue;
                }
            }
        }

    }
    public virtual void Walk(Vector2 aimPos, float speed, float walkVolume)
    {
        walkTime = 20;

        Vector2 dir = Custom.DirVec(Body.pos, aimPos);

        Body.vel += dir * speed * MovementSpeed * Custom.LerpMap(Body.vel.magnitude, 0, MovementSpeed * 4f, 0.5f, 1);

        Vector2 tilePos = room.MiddleOfTile(abstractCreature.pos);
        Vector2 aimDir = default;
        if (CanSwim || room.aimap.getAItile(Body.pos).acc == AItile.Accessibility.Wall)
        {
            aimDir = Body.vel.normalized;
        }
        else if (room.GetTile(Body.pos).Terrain != Room.Tile.TerrainType.ShortcutEntrance)
        {
            if (IsTileSolid(0, 0, -1))
            {
                //Body.vel.y += Custom.LerpMap(Body.pos.y - tilePos.y, 25, 10, -1, 1);
                aimDir = new Vector2(0, 1);
            }
            else if (IsTileSolid(0, 0, 1))
            {
                //Body.vel.y += Custom.LerpMap(tilePos.y - Body.pos.y, 10, 25, -1, 1);
                aimDir = new Vector2(0, -1);
            }

            if (IsTileSolid(0, -1, 0))
            {
                //Body.vel.x += Custom.LerpMap(Body.pos.x - tilePos.x, 25, 10, -1, 1);
                if (aimDir == default)
                {
                    aimDir = new(1, 0);
                }
                else
                {
                    aimDir = Vector3.Slerp(aimDir, new(1, 0), 0.5f);
                }
            }
            else if (IsTileSolid(0, 1, 0))
            {
                //Body.vel.x += Custom.LerpMap(tilePos.x - Body.pos.x, 10, 25, -1, 1);
                if (aimDir == default)
                {
                    aimDir = new(-1, 0);
                }
                else
                {
                    aimDir = Vector3.Slerp(aimDir, new(-1, 0), 0.5f);
                }
            }

        }
        bodyDirGoal = aimDir;
    }

    public virtual bool NearSolidGround()
    {
        IntVector2 tilePos = room.GetTilePosition(Body.pos);
        for (int e = 0; e < Custom.eightDirections.Length; e++)
        {
            if (room.GetTile(tilePos + Custom.eightDirections[e]).Solid)
            {
                return true;
            }
        }
        return false;
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public override void Violence(BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, Appendage.Pos hitAppendage, DamageType type, float damage, float stunBonus)
    {
        if (jumping)
        {
            jumping = false;
        }
        if (!dead)
        {
            spasmTimer += 25;
            spasmStrength = Mathf.Min(1, spasmStrength + (damage / 2f));
        }

        if (CanDrip &&
            room is not null &&
            directionAndMomentum.HasValue)
        {
            int DropCount = (int)Mathf.Min(14, damage * Mathf.Lerp(1, 7.25f, AbstractAI.sewageDrench) + Random.Range(-1, 2));
            for (int d = DropCount; d > 0; d--)
            {
                Vector2 vel = (-directionAndMomentum.Value.normalized * Random.Range(4f, 8f) + Custom.RNV() * Random.Range(2f, 4f)) * Mathf.Lerp(directionAndMomentum.Value.magnitude / 10f, 1, 0.5f);
                DropADrip(vel);
            }
        }

        base.Violence(source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);

        if (stun > jumpCooldown)
        {
            jumpCooldown = stun;
        }

    }
    public override void Stun(int st)
    {
        base.LoseAllGrasps();
        base.Stun(st);
    }

    public override void Collide(PhysicalObject otherObject, int myChunk, int otherChunk)
    {
        if (WantToGrab(otherObject))
        {
            GrabTarget(otherObject, otherChunk);
        }
        base.Collide(otherObject, myChunk, otherChunk);
    }
    public override void TerrainImpact(int chunk, IntVector2 direction, float speed, bool firstContact)
    {
        if (CanDrip &&
            firstContact &&
            room is not null)
        {
            int DropCount = (int)(Mathf.Lerp(1, 7.25f, AbstractAI.sewageDrench) * (1 + speed / 20f)) + Random.Range(-1, 2);
            for (int d = DropCount; d > 0; d--)
            {
                Vector2 vel = (-Body.vel.normalized * Random.Range(4f, 8f) + Custom.RNV() * Random.Range(2f, 4f)) * Mathf.Lerp(speed / 10f, 1, 0.5f);
                DropADrip(vel);
            }
        }
        base.TerrainImpact(chunk, direction, speed, firstContact);
    }
    public virtual bool WantToGrab(PhysicalObject target)
    {
        if (grasps[0] is not null ||
            target is null ||
            target.grabbedBy.Count > 0)
        {
            return false;
        }

        for (int g = 0; g < grasps.Length; g++)
        {
            if (grasps[g]?.grabbed is not null &&
                grasps[g].grabbed == target)
            {
                return false;
            }
        }

        if (target is DrainMite)
        {
            return false;
        }

        if (AI is not null)
        {
            if (AI.ObjectRelationship(target.abstractPhysicalObject).type == CreatureTemplate.Relationship.Type.Eats ||
                AI.ObjectRelationship(target.abstractPhysicalObject).type == CreatureTemplate.Relationship.Type.PlaysWith)
            {
                return true;
            }
        }


        return false;
    }
    public virtual void GrabTarget(PhysicalObject target, int chunkGrabbed)
    {
        base.Grab(target, 0, chunkGrabbed, Grasp.Shareability.CanOnlyShareWithNonExclusive, 0.15f, true, true);
        room?.PlaySound(SoundID.Big_Spider_Grab_Creature, Body.pos, 0.9f, 2.5f - Size);
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public void FlyingWeapon(Weapon weapon)
    {
        if (Conscious &&
            HasFooting &&
            dodgeCooldown < 1 &&
            inputWithDiagonals is null &&
            !Custom.DistLess(Body.pos, weapon.thrownPos, weapon.Submersion > 0.5f ? 30 : 60))
        {
            if (AI.VisualContact(weapon.firstChunk.pos, Template.movementBasedVision) &&
                Custom.DistLess(weapon.firstChunk.pos + weapon.firstChunk.vel.normalized * 140f, Body.pos, 140f) &&
                Mathf.Abs(Custom.DistanceToLine(Body.pos, weapon.firstChunk.pos, weapon.firstChunk.pos + weapon.firstChunk.vel)) < 14f)
            {
                jumpedToDodge = true;
                dodgeCooldown += 240;
                Vector2 jumpDir = Custom.DirVec(weapon.thrownPos, weapon.thrownPos + JumpAngle * 400f);
                float jumpStrength = Random.Range(14f, 20f);
                StartJump(jumpDir, jumpStrength);

                if (weapon.thrownBy is not null &&
                    AI.tracker is not null)
                {
                    AI.tracker.SeeCreature(weapon.thrownBy.abstractCreature);
                }
            }
        }
    }

    public virtual bool LineOfSight(Vector2 pos, Vector2 goalPoint, out bool validTerrainOnWay)
    {
        validTerrainOnWay = false;
        if (room is null)
        {
            return false;
        }

        if (room.GetTile(pos).Solid || room.GetTile(goalPoint).Solid)
        {
            return false;
        }
        float dist = Vector2.Distance(pos, goalPoint);
        for (int t = 20; t < dist; t += 20)
        {
            if (room.GetTile(Vector2.Lerp(pos, goalPoint, t / dist)).Solid)
            {
                validTerrainOnWay = true;
                return false;
            }
            if (!validTerrainOnWay && t > 20 && (
                    room.GetTile(Body.pos).AnyBeam ||
                    room.GetTile(Body.pos).wallbehind ||
                    room.GetTile(Body.pos).Terrain == Room.Tile.TerrainType.Floor))
            {
                validTerrainOnWay = true;
            }
        }
        return true;
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public void BitByPlayer(Grasp grasp, bool eu)
    {
        bites--;

        if (!dead)
        {
            Die();
        }

        Body.MoveFromOutsideMyUpdate(eu, grasp.grabber.mainBodyChunk.pos);
        room?.PlaySound(bites == 0 ? SoundID.Slugcat_Final_Bite_Fly : SoundID.Slugcat_Bite_Fly, Body, false, 1.1f, 0.6f);

        if (bites < 1)
        {
            (grasp.grabber as Player).ObjectEaten(this);
			grasp.Release();
			Destroy();
        }
    }

    public void ThrowByPlayer() {}

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public override void SpitOutOfShortCut(IntVector2 pos, Room newRoom, bool spitOutAllSticks)
    {
        base.SpitOutOfShortCut(pos, newRoom, spitOutAllSticks);
        shortcutDelay = 40;
        Vector2 spitOutDir = Custom.IntVector2ToVector2(newRoom.ShorcutEntranceHoleDirection(pos));
        Body.HardSetPosition(newRoom.MiddleOfTile(pos) - spitOutDir * 5f);
        Body.terrainSqueeze = 0.1f;

        if (Consious &&
            inputWithDiagonals is null &&
            AI is not null)
        {
            float jumpVel = 10 + Mathf.Abs(6f * (Custom.WrappedRandomVariation(0.1f, 0.1f, 0.2f) - 0.1f) * 10f);
            if (AI.IsJumpSafe(spitOutDir * jumpVel * MovementSpeed))
            {
                Jump(spitOutDir * jumpVel);
            }
            else
            {
                jumpVel /= 8f;
                Walk(Body.pos + spitOutDir * 20f, jumpVel, jumpVel);
                AI.UnsafeToJump = 40;
            }
        }

        if (graphicsModule is not null)
        {
            graphicsModule.Reset();
        }
    }

    public override Color ShortCutColor()
    {
        return ShortcutColor;
    }

    public virtual void DropADrip(Vector2 vel)
    {
        Particles.SewageDrip drip = new(Body.pos, vel, SizeFac + 1f, IVars.color);
        room.AddObject(drip);
        AbstractAI.sewageDrench -= 1/500f;
        sewageDripCooldown += 3;
    }

}
