namespace DrainMites.DrainMite;

public class DrainMiteGraphics : GraphicsModule, ILookingAtCreatures
{
    public DrainMite mite => owner as DrainMite;
    public DrainMiteAI MiteAI => mite?.AI;
    public virtual BodyChunk Body => mite.Body;
    public virtual int StartOfLegSprites => 0;
    public virtual int BodySprite => (limbs?.Length * 2) ?? 0;
    public virtual int EyeSprite => BodySprite + 1;
    public virtual int TotalSprites => EyeSprite + 1;

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    private bool debug;
    private int DebugSprite;

    public Limb[,] limbs;
    public float limbLength => mite.IVars.limbLength;
    public float[,] legSpriteSizes;
    public float[,] limbLengthMultipliers;
    public float[,] limbGoalDistances;

    private bool lastLegsPosition;
    public bool legsPosition;

    public float walkCycle;

    public Color bodyColor;

    public Color eyeColor;
    public CreatureLooker creatureLooker;
    public Vector2 DefaultEyeScale;
    public float eyeSizeMult = 1f;
    public float eyeBlink = 0.1f;
    public int blinkTime;
    public float eyeZrotateFac => Mathf.Abs(Vector2.Dot(mite.bodyDir, lookAngle))/4f;

    public Vector2 lookAngle;
    public int timeSpentLooking;

    public float darkFac;

    private Tracker.CreatureRepresentation lastLookCreature;

    public DrainMiteGraphics(PhysicalObject owner) : base(owner, false)
    {
        limbs = new Limb[2, 2];
        legSpriteSizes = new float[limbs.GetLength(0), limbs.GetLength(1)];
        limbLengthMultipliers = new float[limbs.GetLength(0), limbs.GetLength(1)];
        for (int leg = 0; leg < limbs.GetLength(0); leg++)
        {
            for (int side = 0; side < limbs.GetLength(1); side++)
            {
                limbs[leg, side] = new Limb(this, Body, leg + side * 4, 1f, 0.5f, 0.98f, 15f, 0.95f);
                limbs[leg, side].mode = Limb.Mode.Dangle;
                limbs[leg, side].pushOutOfTerrain = false;
                legSpriteSizes[leg, side] = 17f;
                limbLengthMultipliers[leg, side] = 1f;
            }
        }
        limbGoalDistances = new float[limbs.GetLength(0), limbs.GetLength(1)];

        creatureLooker = new CreatureLooker(this, MiteAI.tracker, mite, 0f, 30);

    }
    public override void Reset()
    {
        base.Reset();
        for (int leg = 0; leg < limbs.GetLength(0); leg++)
        {
            for (int side = 0; side < limbs.GetLength(1); side++)
            {
                limbs[leg, side].Reset(Body.pos);
            }
        }
    }

    //--------------------------------------------------------------------------------

    public override void Update()
    {
        base.Update();

        creatureLooker.Update();
        if (mite.Conscious)
        {
            UpdateEye();
        }

        float bodySpeed = Body.vel.magnitude;
        if (bodySpeed > 0.2f && mite.Moving)
        {
            walkCycle += Mathf.Max(0f, (bodySpeed - 1f) / 30f);
            if (walkCycle > 1f)
            {
                walkCycle -= 1f;
            }
        }
        lastLegsPosition = legsPosition;
        legsPosition = walkCycle > 0.5f;

        UpdateLegs(bodySpeed);
        
    }
    public virtual void UpdateEye()
    {
        if (creatureLooker.lookCreature is not null)
        {
            lastLookCreature = creatureLooker.lookCreature;
            if (lastLookCreature != creatureLooker.lookCreature)
            {
                timeSpentLooking = 0;
            }
            else
            {
                timeSpentLooking++;
            }

            Vector2 lookFac = default;
            if (creatureLooker.lookCreature.VisualContact)
            {
                lookFac = Custom.DirVec(mite.Body.pos, creatureLooker.lookCreature.representedCreature.realizedCreature.DangerPos);
            }
            else if (creatureLooker.lookCreature.EstimatedChanceOfFinding > 0.2f)
            {
                lookFac = Custom.DirVec(mite.Body.pos, mite.room.MiddleOfTile(creatureLooker.lookCreature.BestGuessForPosition()));
            }
            if (lookFac != default)
            {
                lookAngle += lookFac.normalized;
                lookAngle.Normalize();
            }
        }
        else
        {
            lookAngle = Vector2.Lerp(lookAngle, default, 0.1f);
        }


        int timetoblinkThreshold = -Random.Range(2, 1800);
        if (MiteAI is not null)
        {
            if (MiteAI.behavior == DrainMiteAI.Behavior.Hunt &&
                MiteAI.CurrentPrey?.realizedObject is not null)
            {
                eyeSizeMult = Mathf.Lerp(eyeSizeMult, 0.7f, 0.2f);
                timetoblinkThreshold = (int)(timetoblinkThreshold * Mathf.Lerp(1, 1.5f, MiteAI.ObjectRelationship(MiteAI.CurrentPrey).intensity));
                if (debug)
                {
                    if (MiteAI.PreyVisual)
                    {
                        eyeColor = Color.Lerp(eyeColor, Color.red, 0.2f);
                    }
                    else
                    {
                        eyeColor = Color.Lerp(eyeColor, Color.white, 0.2f);
                    }
                }
            }
            else
            if (MiteAI.behavior == DrainMiteAI.Behavior.Flee &&
                MiteAI.CurrentThreat?.realizedObject is not null)
            {
                eyeSizeMult = Mathf.Lerp(eyeSizeMult, 1.3f, 0.2f);
                int thresholdReduction = (int)(timetoblinkThreshold * Mathf.Lerp(0, 1/3f, MiteAI.ObjectRelationship(MiteAI.CurrentThreat).intensity));
                if (thresholdReduction > 0)
                {
                    if (MiteAI.ObjectRelationship(MiteAI.CurrentPrey).type == CreatureTemplate.Relationship.Type.Afraid ||
                        MiteAI.ObjectRelationship(MiteAI.CurrentPrey).type == CreatureTemplate.Relationship.Type.StayOutOfWay)
                    {
                        thresholdReduction *= 2;
                    }
                    timetoblinkThreshold -= thresholdReduction;
                }
                if (debug)
                {
                    if (MiteAI.Panic)
                    {
                        eyeColor = Color.Lerp(eyeColor, Color.cyan, 0.2f);
                    }
                    else
                    {
                        eyeColor = Color.Lerp(eyeColor, Color.white, 0.2f);
                    }
                }
                
            }
            else
            if (debug &&
                (MiteAI.behavior == DrainMiteAI.Behavior.ReturnPrey || MiteAI.behavior == DrainMiteAI.Behavior.EscapeRain) &&
                MiteAI.DenPosition() is not null)
            {
                eyeSizeMult = Mathf.Lerp(eyeSizeMult, 1, 0.2f);
                if (debug)
                {
                    eyeColor = Color.Lerp(eyeColor, Color.green, 0.2f);
                }
            }
            else
            {
                eyeSizeMult = Mathf.Lerp(eyeSizeMult, 1, 0.2f);
                if (debug)
                {
                    eyeColor = Color.Lerp(eyeColor, Color.white, 0.2f);
                }
            }
        }

        blinkTime--;

        if (mite.inputWithDiagonals is not null && mite.jumpCooldown > 0)
        {
            blinkTime = Mathf.Max(0, mite.jumpCooldown - 5);
        }
        else if (blinkTime < timetoblinkThreshold)
        {
            blinkTime = Random.Range(4, Random.Range(4, 9));
        }

        eyeBlink = Mathf.Lerp(eyeBlink, blinkTime > 0 ? 0.1f : 1, 0.75f);

    }
    public virtual void UpdateLegs(float bodySpeed)
    {
        for (int l = 0; l < limbs.GetLength(0); l++)
        {
            for (int s = 0; s < limbs.GetLength(1); s++)
            {
                Vector2 legDir = -mite.bodyDir;
                bool otherSide = legsPosition == (l % 2 == s);
                if (mite.room.aimap.getAItile(mite.Body.pos).acc == AItile.Accessibility.Wall)
                {
                    legDir = Custom.DegToVec(Custom.VecToDeg(legDir) + (Mathf.Lerp(30f, 120f, l) + 35f * (otherSide ? -1f : 1f) * Mathf.InverseLerp(0.5f, 5f, bodySpeed)) * (-1 + 2 * s));
                }
                else
                {
                    legDir = Custom.DegToVec(Custom.VecToDeg(legDir) + (Mathf.Lerp(30f, 140f, (float)l * 1/3f) + 35f * (otherSide ? -1f : 1f) * Mathf.InverseLerp(0.5f, 5f, bodySpeed)) * (-1 + 2 * s));
                }
                float lengthOfLimb = limbLengthMultipliers[l, 0] * limbLength;
                Vector2 limbPosGoal = Body.pos + legDir * lengthOfLimb + Body.vel.normalized * lengthOfLimb * 0.5f * Mathf.InverseLerp(0.5f, 5f, bodySpeed);
                bool huntPosSet = false;

                if (mite.Conscious)
                {
                    limbs[l, s].mode = Limb.Mode.HuntAbsolutePosition;

                    if (mite.jumping)
                    {
                        huntPosSet = true;
                        limbs[l, s].absoluteHuntPos = mite.Body.pos - (mite.bodyDir * mite.Body.rad * 5f);
                        limbs[l, s].pos = limbs[l, s].absoluteHuntPos;
                    }
                    else if (!mite.CanSwim && !mite.HasFooting)
                    {
                        huntPosSet = true;
                        limbs[l, s].mode = Limb.Mode.Dangle;
                        limbs[l, s].vel += Custom.DegToVec(Random.value * 360f) * Random.value * 3f;
                    }
                }
                else
                {
                    limbs[l, s].mode = Limb.Mode.Dangle;
                    if (mite.spasmTimer > 0)
                    {
                        limbs[l, s].vel += Custom.DegToVec(Random.value * 360f) * 5f * mite.spasmStrength * Mathf.InverseLerp(0, 10, mite.spasmTimer);
                    }
                }

                if (limbs[l, s].mode == Limb.Mode.HuntAbsolutePosition)
                {
                    if (!huntPosSet)
                    {
                        if (bodySpeed < 1f)
                        {
                            if (Random.value < 0.1f && !Custom.DistLess(limbs[l, s].pos, limbPosGoal, lengthOfLimb/6f))
                            {
                                FindGrip(l, s, limbPosGoal, lengthOfLimb, bodySpeed);
                            }
                        }
                        else if (otherSide && (lastLegsPosition != legsPosition || l == limbs.GetLength(0) - 1) && !Custom.DistLess(limbs[l, s].pos, limbPosGoal, lengthOfLimb * 0.5f))
                        {
                            FindGrip(l, s, limbPosGoal, lengthOfLimb, bodySpeed);
                        }
                    }
                }
                else
                {
                    limbs[l, s].vel += Custom.DegToVec(Random.value * 360f) * mite.spasmTimer * 5f;
                    limbs[l, s].vel += legDir * 0.7f;
                    limbs[l, s].vel.y -= 0.8f;
                    limbGoalDistances[l, s] = 0f;
                }

                limbs[l, s].huntSpeed = 15f * Mathf.InverseLerp(-0.05f, 2f, bodySpeed);
                limbs[l, s].Update();
                limbs[l, s].ConnectToPoint(Body.pos, lengthOfLimb, push: false, 0f, Body.vel, 1f, 0.5f);
            }
        }
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        sLeaser.sprites = new FSprite[TotalSprites + (debug ? 1 : 0)];
        sLeaser.sprites[BodySprite] = new FSprite("Circle20");
        sLeaser.sprites[BodySprite].scale = mite.Size;
        sLeaser.sprites[BodySprite].scaleY *= 0.7f;

        DefaultEyeScale = new Vector2(mite.Size * 0.5f * 0.925f, mite.Size * 0.5f * 0.75f);
        sLeaser.sprites[EyeSprite] = new FSprite("Circle20");
        sLeaser.sprites[EyeSprite].scaleX = DefaultEyeScale.x;
        sLeaser.sprites[EyeSprite].scaleY = DefaultEyeScale.y;
        for (int l = 0; l < limbs.GetLength(0); l++)
        {
            for (int s = 0; s < limbs.GetLength(1); s++)
            {
                sLeaser.sprites[LegSprite(l, s, 0)] = new FSprite("SpiderLeg" + l + "A");
                sLeaser.sprites[LegSprite(l, s, 0)].anchorY = 1f / legSpriteSizes[l, 0];
                sLeaser.sprites[LegSprite(l, s, 0)].scaleX = (s == 0 ? 1f : -1f) * Mathf.Lerp(1, 1.8f, mite.SizeFac);
                sLeaser.sprites[LegSprite(l, s, 0)].scaleY = limbLengthMultipliers[l, 0] * limbLengthMultipliers[l, 1] * limbLength / legSpriteSizes[l, 0];
                sLeaser.sprites[LegSprite(l, s, 1)] = new FSprite("SpiderLeg" + l + "B");
                sLeaser.sprites[LegSprite(l, s, 1)].anchorY = 1f / legSpriteSizes[l, 1];
                sLeaser.sprites[LegSprite(l, s, 1)].scaleX = (s == 0 ? 1f : -1f) * Mathf.Lerp(1, 1.8f, mite.SizeFac);
            }
        }

        if (debug)
        {
            DebugSprite = sLeaser.sprites.Length - 1;
            sLeaser.sprites[DebugSprite] = new FSprite("pixel");
            sLeaser.sprites[DebugSprite].scaleX = 1.5f;
        }

        AddToContainer(sLeaser, rCam, null);
        base.InitiateSprites(sLeaser, rCam);
    }

    public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
        bodyColor = mite.IVars.color;
        eyeColor = Color.Lerp(Color.white, bodyColor, 0.075f);
    }

    public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
    {
        sLeaser.RemoveAllSpritesFromContainer();
        if (newContatiner is null)
        {
            newContatiner = rCam.ReturnFContainer("Midground");
        }
        for (int i = 0; i < sLeaser.sprites.Length; i++)
        {
            newContatiner.AddChild(sLeaser.sprites[i]);
        }
    }

    public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        base.DrawSprites(sLeaser, rCam, timeStacker, camPos);

        if (mite.room is not null)
        {
            darkFac = mite.room.DarknessOfPoint(rCam, mite.Body.pos);
        }

        Vector2 bodyPos = Vector2.Lerp(Body.lastPos, Body.pos, timeStacker);
        if (mite.jumpWindup > 0)
        {
            float windupFac = Mathf.InverseLerp(0, mite.MaxJumpWindup, mite.jumpWindup);
            windupFac = Mathf.Pow(windupFac, 0.5f);
            bodyPos -= mite.JumpVelocity * windupFac;
            windupFac = Mathf.InverseLerp(mite.MaxJumpWindup/4f, mite.MaxJumpWindup, mite.jumpWindup);
            bodyPos += Custom.RNV() * 1.5f * windupFac;
        }
        sLeaser.sprites[BodySprite].SetPosition(bodyPos - camPos);
        Vector2 currBodyDir = Vector3.Slerp(mite.lastBodyDir, mite.bodyDir, timeStacker);
        Vector2 perpBodyDir = -Custom.PerpendicularVector(currBodyDir);
        sLeaser.sprites[BodySprite].rotation = Custom.AimFromOneVectorToAnother(-currBodyDir, currBodyDir);

        float eyeDisplacementFac = Mathf.Pow(Mathf.InverseLerp(0, 10, timeSpentLooking), 0.5f) * mite.Body.rad/mite.Size/2f;
        sLeaser.sprites[EyeSprite].x = sLeaser.sprites[BodySprite].x + (lookAngle.x * eyeDisplacementFac);
        sLeaser.sprites[EyeSprite].y = sLeaser.sprites[BodySprite].y + (lookAngle.y * eyeDisplacementFac);
        sLeaser.sprites[EyeSprite].rotation = sLeaser.sprites[BodySprite].rotation;
        sLeaser.sprites[EyeSprite].scaleX = DefaultEyeScale.x * eyeSizeMult * eyeBlink * (0.75f + eyeZrotateFac);
        sLeaser.sprites[EyeSprite].scaleY = DefaultEyeScale.y * eyeSizeMult * (1 - eyeZrotateFac);


        for (int l = 0; l < limbs.GetLength(0); l++)
        {
            for (int s = 0; s < limbs.GetLength(1); s++)
            {

                if (mite.bites - 1 <= l)
                {
                    sLeaser.sprites[LegSprite(l, s, 0)].isVisible = false;
                    sLeaser.sprites[LegSprite(l, s, 1)].isVisible = false;
                    continue;
                }

                Vector2 legAnchorPos = bodyPos;
                //legAnchorPos += currBodyDir * (7f - l * 0.5f - (l == 3 ? 1.5f : 0f)) * mite.size;
                legAnchorPos += perpBodyDir * (3f + l * 0.5f - (l == 3 ? 5.5f : 0f)) * mite.Size * (-1 + 2 * s);
                Vector2 limbSegmentPos = Vector2.Lerp(limbs[l, s].lastPos, limbs[l, s].pos, timeStacker);
                limbSegmentPos = Vector2.Lerp(limbSegmentPos, legAnchorPos + currBodyDir * limbLength * 0.1f, Mathf.Sin(Mathf.InverseLerp(0f, limbGoalDistances[l, s], Vector2.Distance(limbSegmentPos, limbs[l, s].absoluteHuntPos)) * Mathf.PI) * 0.4f);
                float fullLegspan = limbLengthMultipliers[l, 0] * limbLengthMultipliers[l, 1] * limbLength;
                float notSureWhatThisIs = limbLengthMultipliers[l, 0] * (1f - limbLengthMultipliers[l, 1]) * limbLength;
                float legSegmentLength = Vector2.Distance(legAnchorPos, limbSegmentPos);
                float sideFac = (l < 3) ? 1f : -1f;
                if (l == 2)
                {
                    sideFac *= 0.7f;
                }
                sideFac *= -1f + 2f * s;
                float num5 = Mathf.Acos(Mathf.Clamp((legSegmentLength * legSegmentLength + fullLegspan * fullLegspan - notSureWhatThisIs * notSureWhatThisIs) / (2f * legSegmentLength * fullLegspan), 0.2f, 0.98f)) * (180f / Mathf.PI) * sideFac;
                Vector2 legJointPos = legAnchorPos + Custom.DegToVec(Custom.AimFromOneVectorToAnother(legAnchorPos, limbSegmentPos) + num5) * fullLegspan;

                sLeaser.sprites[LegSprite(l, s, 0)].x = legAnchorPos.x - camPos.x;
                sLeaser.sprites[LegSprite(l, s, 0)].y = legAnchorPos.y - camPos.y;
                sLeaser.sprites[LegSprite(l, s, 0)].rotation = Custom.AimFromOneVectorToAnother(legAnchorPos, legJointPos);

                sLeaser.sprites[LegSprite(l, s, 1)].x = legJointPos.x - camPos.x;
                sLeaser.sprites[LegSprite(l, s, 1)].y = legJointPos.y - camPos.y;
                sLeaser.sprites[LegSprite(l, s, 1)].rotation = Custom.AimFromOneVectorToAnother(legJointPos, limbSegmentPos);
                sLeaser.sprites[LegSprite(l, s, 1)].scaleY = Vector2.Distance(legJointPos, limbSegmentPos);
                sLeaser.sprites[LegSprite(l, s, 1)].scaleY = limbLengthMultipliers[l, 0] * limbLengthMultipliers[l, 1] * limbLength / legSpriteSizes[l, 1];
            }
        }


        if (debug &&
            mite.AI is not null &&
            mite.room is not null)
        {
            sLeaser.sprites[EyeSprite].color = eyeColor;

            sLeaser.sprites[DebugSprite].isVisible = true;
            Vector2 beamDest;
            Vector2 eyePos = sLeaser.sprites[EyeSprite].GetPosition();
            if (mite.AI.behavior == DrainMiteAI.Behavior.Hunt && mite.AI.PreyVisual)
            {
                beamDest = mite.AI.CurrentPrey.realizedObject.firstChunk.pos;
            }
            else if (mite.AI.behavior == DrainMiteAI.Behavior.Flee && mite.AI.Panic)
            {
                beamDest = mite.AI.CurrentThreat.realizedObject.firstChunk.pos;
            }
            else if ((mite.AI.behavior == DrainMiteAI.Behavior.EscapeRain || mite.AI.behavior == DrainMiteAI.Behavior.ReturnPrey) && mite.AI.DenPosition().HasValue)
            {
                beamDest = mite.room.MiddleOfTile(mite.room.LocalCoordinateOfNode(mite.AI.DenPosition().Value.abstractNode));
            }
            else if (mite.currentDest.HasValue)
            {
                beamDest = mite.currentDest.Value;
            }
            else if (mite.AI.pathFinder is not null)
            {
                beamDest = mite.room.MiddleOfTile((mite.AI.pathFinder as StandardPather).FollowPath(mite.room.GetWorldCoordinate(mite.Body.pos), actuallyFollowingThisPath: false).destinationCoord);
            }
            else
            {
                sLeaser.sprites[DebugSprite].isVisible = false;
                return;
            }
            beamDest -= camPos;
            sLeaser.sprites[DebugSprite].rotation = Custom.AimFromOneVectorToAnother(eyePos, beamDest);
            sLeaser.sprites[DebugSprite].color = eyeColor;
            sLeaser.sprites[DebugSprite].scaleY = Vector2.Distance(eyePos, beamDest);
            sLeaser.sprites[DebugSprite].SetPosition(Vector2.Lerp(eyePos, beamDest, 0.5f));
        }


        for (int i = 0; i < sLeaser.sprites.Length; i++)
        {
            sLeaser.sprites[i].color = Color.Lerp(bodyColor, rCam.currentPalette.blackColor, darkFac);
        }

        sLeaser.sprites[EyeSprite].color = Color.Lerp(eyeColor, bodyColor, darkFac);

    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public virtual int LegSprite(int limb, int side, int segment)
    {
        return StartOfLegSprites + limb + (segment * limbs.GetLength(0)) + (side * limbs.Length);
    }

    public virtual void FindGrip(int l, int s, Vector2 idealPos, float rad, float moveSpeed)
    {
        if (mite.room.GetTile(idealPos).wallbehind)
        {
            limbs[l, s].absoluteHuntPos = idealPos;
        }
        else
        {
            limbs[l, s].FindGrip(mite.room, Body.pos, idealPos, rad, idealPos + mite.bodyDir * Mathf.Lerp(moveSpeed * 2f, rad / 2f, 0.5f), 2, 2, behindWalls: true);
        }
        limbGoalDistances[l, s] = Vector2.Distance(limbs[l, s].pos, limbs[l, s].absoluteHuntPos);
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    public float CreatureInterestBonus(Tracker.CreatureRepresentation crit, float score)
    {
        if (MiteAI is not null)
        {
            score *= MiteAI.DynamicRelationship(crit).intensity;
        }
        return score;
    }

    public Tracker.CreatureRepresentation ForcedLookCreature()
    {
        if (mite.AI is not null)
        {
            if (mite.AI.behavior == DrainMiteAI.Behavior.Hunt && mite.AI.preyTracker is not null)
            {
                return mite.AI.preyTracker.MostAttractivePrey;
            }
            if (mite.AI.behavior == DrainMiteAI.Behavior.Flee && mite.AI.threatTracker is not null)
            {
                return mite.AI.threatTracker.mostThreateningCreature;
            }
        }
        return null;
    }

    public void LookAtNothing()
    {
    }

}
