namespace DrainMites;


public static class TemplateType
{

    [AllowNull] public static CreatureTemplate.Type DrainMite = new("DrainMite", true);

    public static void Unregister()
    {
        if (DrainMite is not null)
        {
            DrainMite.Unregister();
            DrainMite = null;
        }
    }

}

public static class SandboxUnlock
{

    [AllowNull] public static MultiplayerUnlocks.SandboxUnlockID DrainMite = new("DrainMite", true);

    public static void Unregister()
    {
        if (DrainMite is not null)
        {
            DrainMite.Unregister();
            DrainMite = null;
        }
    }

}

public static class SlugpupFoodType
{
    [AllowNull] public static SlugNPCAI.Food DrainMite = new("DrainMite", true);

    public static void Unregister()
    {
        if (DrainMite is not null)
        {
            DrainMite.Unregister();
            DrainMite = null;
        }
    }
}
