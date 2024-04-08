global using System;
global using System.Collections.Generic;
global using UnityEngine;
global using RWCustom;
global using MoreSlugcats;
global using Random = UnityEngine.Random;
global using Color = UnityEngine.Color;
using BepInEx;
using Fisobs.Core;
using BepInEx.Logging;
//--------------------------------------------------------------------------------

namespace DrainMites;

[BepInPlugin(MOD_ID, "Drain Mites", "1.0")]

//--------------------------------------------------------------------------------

public class Plugin : BaseUnityPlugin
{
    private const string MOD_ID = "bry.drainmites";

    public bool IsInit;

    [AllowNull] internal static ManualLogSource logger;

    public void OnEnable()
    {
        logger = Logger;
        On.RainWorld.OnModsDisabled += UnregisterEnums;
        On.RainWorld.OnModsInit += InitiateDrainMites;
        On.RainWorld.PostModsInit += ReorderUnlocks;

        Content.Register(new DrainMiteCritob());
    }
    public void OnDisable() => logger = default;

    // - - - - - - - - - - - - - - - - - - - - - -

    public void UnregisterEnums(On.RainWorld.orig_OnModsDisabled orig, RainWorld rw, ModManager.Mod[] newlyDisabledMods)
    {
        orig(rw, newlyDisabledMods);
        for (int i = 0; i < newlyDisabledMods.Length; i++)
        {
            if (newlyDisabledMods[i].id == MOD_ID)
            {
                if (MultiplayerUnlocks.CreatureUnlockList.Contains(SandboxUnlock.DrainMite))
                {
                    MultiplayerUnlocks.CreatureUnlockList.Remove(SandboxUnlock.DrainMite);
                }
                TemplateType.Unregister();
                SandboxUnlock.Unregister();
                SlugpupFoodType.Unregister();
                break;
            }
        }
    }
    public void InitiateDrainMites(On.RainWorld.orig_OnModsInit orig, RainWorld rw)
    {
        orig(rw);

        try
        {
            if (IsInit) return;
            IsInit = true;

            Futile.atlasManager.LoadAtlas("atlases/drainmite/sprites");

            On.Spear.HitSomethingWithoutStopping += HitDrainMiteWithoutStopping;
            On.Player.Grabability += HoldMitesOnehanded;
            On.SlugcatStats.NourishmentOfObjectEaten += DrainMiteSustenance;
            On.DevInterface.MapPage.CreatureVis.CritCol += PleaseStopFlickeringTheDevtoolsMapIcon;

            On.MoreSlugcats.SlugNPCAI.GetFoodType += DrainMitesForPups;

            Debug.LogWarning($"Drain Mites are on!");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            Debug.LogException(ex);
            throw;
        }
    }
    public void ReorderUnlocks(On.RainWorld.orig_PostModsInit orig, RainWorld rw)
    {
        orig(rw);
        if (MultiplayerUnlocks.CreatureUnlockList.Contains(SandboxUnlock.DrainMite))
        {
            MultiplayerUnlocks.CreatureUnlockList.Remove(SandboxUnlock.DrainMite);
            MultiplayerUnlocks.CreatureUnlockList.Insert(MultiplayerUnlocks.CreatureUnlockList.IndexOf(MultiplayerUnlocks.SandboxUnlockID.Fly), SandboxUnlock.DrainMite);
        }
    }

    // - - - - - - - - - - - - - - - - - - - - - -

    public void HitDrainMiteWithoutStopping(On.Spear.orig_HitSomethingWithoutStopping orig, Spear spr, PhysicalObject target, BodyChunk chunk, PhysicalObject.Appendage appendage)
    {
        bool TargetDead = 
           target is not null &&
           target is Creature &&
           (target as Creature).dead;

        orig(spr, target, chunk, appendage);

        if (target is not null &&
            target is DrainMite.DrainMite)
        {
            if (spr?.thrownBy is not null &&
                spr.thrownBy is Player plr &&
                spr.Spear_NeedleCanFeed() &&
                !TargetDead)
            {
                if (spr.room is not null &&
                    spr.room.game.IsStorySession)
                {
                    if (spr.room.game.GetStorySession.playerSessionRecords is not null)
                    {
                        spr.room.game.GetStorySession.playerSessionRecords[(plr.abstractCreature.state as PlayerState).playerNumber].AddEat(target);
                    }
                    plr.AddQuarterFood();
                }
                else
                {
                    plr.AddFood(1);
                }
            }
            spr.TryImpaleSmallCreature(target as Creature);
        }

    }
    public Player.ObjectGrabability HoldMitesOnehanded(On.Player.orig_Grabability orig, Player self, PhysicalObject obj)
    {
        if (obj is DrainMite.DrainMite)
        {
            return Player.ObjectGrabability.OneHand;
        }
        return orig(self, obj);
    }
    public int DrainMiteSustenance(On.SlugcatStats.orig_NourishmentOfObjectEaten orig, SlugcatStats.Name Eater, IPlayerEdible Edible)
    {
        int QuarterPips = orig(Eater, Edible);
        if (Edible is DrainMite.DrainMite && QuarterPips > 0)
        {
            QuarterPips /= 4;
        }
        return QuarterPips;
    }
    public Color PleaseStopFlickeringTheDevtoolsMapIcon(On.DevInterface.MapPage.CreatureVis.orig_CritCol orig, AbstractCreature absCtr)
    {
        if (absCtr is not null &&
            absCtr.creatureTemplate.type == TemplateType.DrainMite &&
            absCtr.InDen)
        {
            return DrainMiteCritob.FinalMapColor(absCtr);
        }
        return orig(absCtr);
    }

    public SlugNPCAI.Food DrainMitesForPups(On.MoreSlugcats.SlugNPCAI.orig_GetFoodType orig, SlugNPCAI AI, PhysicalObject food)
    {
        if (ModManager.MSC &&
            food is DrainMite.DrainMite)
        {
            return SlugNPCAI.Food.JellyFish;
        }
        return orig(AI, food);
    }

    // - - - - - - - - - - - - - - - - - - - - - -

}

//--------------------------------------------------------------------------------