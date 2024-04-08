
namespace DrainMites;

public interface ITrackItemRelationships
{
    public AIModule ModuleToTrackItemRelationship(AbstractPhysicalObject obj);

    public CreatureTemplate.Relationship ObjectRelationship(AbstractPhysicalObject absObj);
}
