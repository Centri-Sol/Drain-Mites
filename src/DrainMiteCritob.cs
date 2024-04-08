using Fisobs.Core;
using Fisobs.Creatures;
using Fisobs.Sandbox;
using DevInterface;


namespace DrainMites;

//----------------------------------------------------------------------------------
//----------------------------------------------------------------------------------

public class DrainMiteCritob : Critob
{

    public static HSLColor MiteIconColor = new(0, 0, 0.9f);
    public static HSLColor MiteDefaultMapColor = new(0f, 0.1f, 0.1f);
    public static HSLColor MiteFoodMapColor = new(84 / 360f, 0.7f, 0.5f);
    public static HSLColor MiteFrozenMapColor = new(200 / 360f, 1, 0.75f);

    internal DrainMiteCritob() : base(TemplateType.DrainMite)
    {
        Icon = new SimpleIcon("Kill_Drain_Mite", MiteIconColor.rgb);
        LoadedPerformanceCost = 10f;
        SandboxPerformanceCost = new(0.6f, 0.2f);
        RegisterUnlock(KillScore.Configurable(0), SandboxUnlock.DrainMite);
    }
    public override int ExpeditionScore() => 0;

    public static HSLColor BaseMapColor(DrainMite.DrainMiteAbstractAI ai, out bool pulse)
    {
        pulse = false;
        if (ai.carryingFood)
        {
            pulse = true;
            return MiteFoodMapColor;
        }
        if (ai.freeze > 0)
        {
            pulse = true;
            return MiteFrozenMapColor;
        }
        if (ai.swarm is not null &&
            ai.swarm.members.Count > 1)
        {
            if (ai.swarm.leader == ai.parent)
            {
                return ai.swarm.color;
            }
            return HSLColor.Lerp(MiteDefaultMapColor, ai.swarm.color, 0.3f + ai.parent.personality.bravery * 0.4f);
        }
        return MiteDefaultMapColor;
    }
    public static Color FinalMapColor(AbstractCreature absMite)
    {
        if (absMite.abstractAI is DrainMite.DrainMiteAbstractAI ai)
        {
            HSLColor FinalColor = BaseMapColor(ai, out bool pulse);
            if (pulse)
            {
                if (ai.colorPulse < ai.MaxPulse)
                {
                    ai.colorPulse++;
                }
                else ai.colorPulse = 0;

                if (ai.colorPulse < ai.MaxPulse / 2)
                {
                    FinalColor.lightness += ai.colorPulse / 200f;
                }
                else FinalColor.lightness += (ai.MaxPulse - ai.colorPulse) / 200f;
            }
            else if (ai.colorPulse > 0)
            {
                ai.colorPulse = 0;
            }
            return FinalColor.rgb;
        }

        return MiteDefaultMapColor.rgb;
    }
    public override Color DevtoolsMapColor(AbstractCreature absMite) => FinalMapColor(absMite);

    public override string DevtoolsMapName(AbstractCreature absMite)
    {
        if (absMite.abstractAI is DrainMite.DrainMiteAbstractAI ai)
        {
            if (ai.swarm is not null &&
                ai.swarm.leader == absMite)
            {
                return "DML";
            }
        }
        return "DM";
    }
    public override IEnumerable<RoomAttractivenessPanel.Category> DevtoolsRoomAttraction()
    {
        return new[]
        {
            RoomAttractivenessPanel.Category.Dark,
            RoomAttractivenessPanel.Category.LikesInside,
            RoomAttractivenessPanel.Category.LikesWater,
            RoomAttractivenessPanel.Category.Swimming
        };
    }
    public override IEnumerable<string> WorldFileAliases() => new[] { "DrainMite", "Drain Mite" };

    public override CreatureTemplate CreateTemplate()
    {
        CreatureTemplate DrainMite = new CreatureFormula(null, Type, "Drain Mite")
        {
            TileResistances = new()
            {
                Floor = new(1, PathCost.Legality.Allowed),
                Climb = new(1, PathCost.Legality.Allowed),
                Wall = new(1, PathCost.Legality.Allowed),
                Ceiling = new(1, PathCost.Legality.Allowed),
                Corridor = new(1, PathCost.Legality.Allowed),
                OffScreen = new(500, PathCost.Legality.Unwanted)
            },
            ConnectionResistances = new()
            {
                Standard = new(1, PathCost.Legality.Allowed),
                Slope = new(1, PathCost.Legality.Allowed),
                CeilingSlope = new(1, PathCost.Legality.Allowed),
                OpenDiagonal = new(1, PathCost.Legality.Allowed),
                SemiDiagonalReach = new(2, PathCost.Legality.Allowed),
                ReachOverGap = new(1, PathCost.Legality.Allowed),
                ShortCut = new(4, PathCost.Legality.Allowed),
                NPCTransportation = new(25, PathCost.Legality.Allowed),
                RegionTransportation = new(50, PathCost.Legality.Allowed),
                DropToFloor = new(25, PathCost.Legality.Allowed),
                DropToClimb = new(10, PathCost.Legality.Allowed),
                DropToWater = new(25, PathCost.Legality.Allowed),
                OffScreenMovement = new(1, PathCost.Legality.Allowed),
                BetweenRooms = new(10, PathCost.Legality.Allowed),
                OutsideRoom = new(500, PathCost.Legality.Unwanted),
                 
            },
            DamageResistances = new() { Base = 0.5f, Water = 5, Explosion = 0.2f, Blunt = 0.01f },
            StunResistances =   new() { Base = 1,    Water = 5, Explosion = 0.2f, Blunt = 0.01f },
            InstantDeathDamage = 0.5f,
            Pathing = PreBakedPathing.Ancestral(CreatureTemplate.Type.Snail),
            HasAI = true
        }.IntoTemplate();

        DrainMite.smallCreature = true;
        DrainMite.bodySize = 0.25f;
        DrainMite.shortcutSegments = 1;
        DrainMite.dangerousToPlayer = 0;
        DrainMite.communityInfluence = 0.5f;
        DrainMite.communityID = CreatureCommunities.CommunityID.None;
        DrainMite.countsAsAKill = 1;
        DrainMite.meatPoints = 4;
        DrainMite.grasps = 1;
        DrainMite.wormgrassTilesIgnored = true;

        DrainMite.visualRadius = 600f;
        DrainMite.movementBasedVision = 2/3f;
        DrainMite.throughSurfaceVision = 0.75f;
        DrainMite.waterVision = 0.75f;
        DrainMite.lungCapacity = float.PositiveInfinity;
        DrainMite.waterRelationship = CreatureTemplate.WaterRelationship.Amphibious;
        DrainMite.canSwim = true;

        DrainMite.offScreenSpeed = 1f;
        DrainMite.abstractedLaziness = 200;
        DrainMite.roamBetweenRoomsChance = -1f;
        DrainMite.roamInRoomChance = -1f;
        DrainMite.interestInOtherAncestorsCatches = 0;
        DrainMite.interestInOtherCreaturesCatches = 0;
        DrainMite.usesCreatureHoles = true;
        DrainMite.usesNPCTransportation = true;
        DrainMite.usesRegionTransportation = true;
        DrainMite.SetNodeType(AbstractRoomNode.Type.RegionTransportation, DrainMite.ConnectionResistance(MovementConnection.MovementType.RegionTransportation).Allowed);
        DrainMite.stowFoodInDen = true;
        DrainMite.hibernateOffScreen = true;

        DrainMite.BlizzardAdapted = false;
        DrainMite.BlizzardWanderer = false;

        DrainMite.jumpAction = "Jump | Hold and release to go farther";
        DrainMite.pickupAction = "Grab object | Hold while jumping to latch onto terrain";
        DrainMite.throwAction = "Drop object";

        return DrainMite;
    }
    public override void EstablishRelationships()
    {

        Relationships DrainMite = new(TemplateType.DrainMite);
        for (int i = 0; i < ExtEnum<CreatureTemplate.Type>.values.entries.Count; i++)
        {
            switch (ExtEnum<CreatureTemplate.Type>.values.entries[i])
            {
                case "NoodleEater":
                    DrainMite.Fears(new CreatureTemplate.Type(ExtEnum<CreatureTemplate.Type>.values.entries[i]), 0.5f);
                    break;
                default:
                    DrainMite.UncomfortableAround(new CreatureTemplate.Type(ExtEnum<CreatureTemplate.Type>.values.entries[i]), 0.5f);
                    break;
            }
        }

        DrainMite.Ignores(CreatureTemplate.Type.GarbageWorm);

        DrainMite.Eats(CreatureTemplate.Type.Leech, 1); // Applies to all leech types
        DrainMite.Eats(CreatureTemplate.Type.Hazer, 0.75f);
        DrainMite.Eats(CreatureTemplate.Type.Fly, 0.5f);
        DrainMite.Eats(CreatureTemplate.Type.Spider, 0.1f);
        DrainMite.Eats(CreatureTemplate.Type.VultureGrub, 0.1f);
        DrainMite.Eats(CreatureTemplate.Type.SmallNeedleWorm, 0.1f);

        DrainMite.UncomfortableAround(CreatureTemplate.Type.Vulture, 1); // Applies to all vulture types
        DrainMite.UncomfortableAround(CreatureTemplate.Type.BigNeedleWorm, 1);

        DrainMite.Fears(CreatureTemplate.Type.PoleMimic, 1);
        DrainMite.Fears(CreatureTemplate.Type.DaddyLongLegs, 1); // Applies to all longlegs types
        DrainMite.Fears(CreatureTemplate.Type.BigEel, 0.7f);
        DrainMite.Fears(CreatureTemplate.Type.CicadaA, 0.7f); // Applies to all squidcada types
        DrainMite.Fears(CreatureTemplate.Type.Centipede, 0.3f); // Applies to all centipede types
        DrainMite.Fears(CreatureTemplate.Type.Scavenger, 0.3f); // Applies to all scavenger types

        DrainMite.IntimidatedBy(CreatureTemplate.Type.Slugcat, 0.7f);
        DrainMite.IntimidatedBy(CreatureTemplate.Type.JetFish, 0.7f);
        DrainMite.IntimidatedBy(CreatureTemplate.Type.TubeWorm, 0.1f);

        DrainMite.IsInPack(TemplateType.DrainMite, 1);

        //----------------------------------------

        DrainMite.IgnoredBy(CreatureTemplate.Type.LizardTemplate);
        DrainMite.IgnoredBy(CreatureTemplate.Type.MirosBird);
        DrainMite.IgnoredBy(CreatureTemplate.Type.DaddyLongLegs); // Applies to all longlegs types

        DrainMite.EatenBy(CreatureTemplate.Type.SeaLeech, 0.5f);
        DrainMite.EatenBy(CreatureTemplate.Type.Slugcat, 0.3f);
        DrainMite.EatenBy(CreatureTemplate.Type.CicadaA, 0.3f); // Applies to all squidcada types
        DrainMite.EatenBy(CreatureTemplate.Type.JetFish, 0.3f);

        DrainMite.AttackedBy(CreatureTemplate.Type.Scavenger, 0.7f); // Applies to all scavenger types

        DrainMite.FearedBy(CreatureTemplate.Type.Fly, 0.7f);
        DrainMite.FearedBy(CreatureTemplate.Type.Leech, 0.1f); // Applies to Jungle Leeches, too, but Sea Leeches have a EatenBy relationship that overrides this

        //----------------------------------------

        if (ModManager.MSC)
        {
            MSCRelationships(DrainMite);
        }

        if (MachineConnector.IsThisModActive("theincandescent"))
        {
            HailstormRelationships(DrainMite);
        }

    }
    public void MSCRelationships(Relationships DrainMite)
    {

        DrainMite.Fears(MoreSlugcatsEnums.CreatureTemplateType.StowawayBug, 1);
        DrainMite.Fears(MoreSlugcatsEnums.CreatureTemplateType.MirosVulture, 0.5f);
        DrainMite.Fears(MoreSlugcatsEnums.CreatureTemplateType.ZoopLizard, 0.5f);

        DrainMite.IntimidatedBy(MoreSlugcatsEnums.CreatureTemplateType.SlugNPC, 0.5f);
        DrainMite.IntimidatedBy(MoreSlugcatsEnums.CreatureTemplateType.BigJelly, 0.5f);

        //----------------------------------------

        DrainMite.EatenBy(MoreSlugcatsEnums.CreatureTemplateType.ZoopLizard, 0.5f);
        DrainMite.EatenBy(MoreSlugcatsEnums.CreatureTemplateType.SlugNPC, 0.3f);

        DrainMite.IgnoredBy(MoreSlugcatsEnums.CreatureTemplateType.MirosVulture);
		
    }
    public void HailstormRelationships(Relationships DrainMite)
    {
        //DrainMite.Fears(Hailstorm.HSEnums.CreatureType.Luminescipede, 1);
        DrainMite.Fears(Hailstorm.HSEnums.CreatureType.Cyanwing, 0.7f);
        //DrainMite.Fears(Hailstorm.HSEnums.CreatureType.SnowcuttleTemplate, 0.5f);
        //DrainMite.Fears(Hailstorm.HSEnums.CreatureType.PeachSpider, 0.3f);
        //DrainMite.Fears(Hailstorm.HSEnums.CreatureType.GorditoGreenie, 0.3f);

        DrainMite.IntimidatedBy(Hailstorm.HSEnums.CreatureType.FreezerLizard, 0.3f);

        //----------------------------------------

        //DrainMite.EatenBy(Hailstorm.HSEnums.CreatureType.SnowcuttleFemale, 0.8f);
        //DrainMite.EatenBy(Hailstorm.HSEnums.CreatureType.SnowcuttleLe, 0.3f);
        //DrainMite.EatenBy(Hailstorm.HSEnums.CreatureType.Luminescipede, 0.1f);
        //DrainMite.EatenBy(Hailstorm.HSEnums.CreatureType.PeachSpider, 0.1f);

        //DrainMite.IgnoredBy(Hailstorm.HSEnums.CreatureType.SnowcuttleTemplate);
    }

    public override Creature CreateRealizedCreature(AbstractCreature absMite) => new DrainMite.DrainMite(absMite, absMite.world);
    public override ArtificialIntelligence CreateRealizedAI(AbstractCreature absMite) => new DrainMite.DrainMiteAI(absMite, absMite.world);
    public override AbstractCreatureAI CreateAbstractAI(AbstractCreature absMite) => new DrainMite.DrainMiteAbstractAI(absMite.world, absMite);
    public override CreatureState CreateState(AbstractCreature absMite) => new NoHealthState(absMite);

    #nullable enable
    public override CreatureTemplate.Type? ArenaFallback() => CreatureTemplate.Type.Spider;
    #nullable disable
}

//----------------------------------------------------------------------------------
