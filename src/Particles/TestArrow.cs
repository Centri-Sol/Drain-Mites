using UnityEngine;
using RWCustom;

namespace DrainMites.Particles;

public class TestArrow : CosmeticSprite
{
    public float life;

    public float lifeTime;

    public Vector2 lastLastLastPos;

    public Vector2 lastLastPos;

    public Color color;

    public float rotation;

    public TestArrow(Vector2 pos, float rot, Vector2 vel, Color color, int lifeTime)
    {
        this.color = color;
        lastPos = pos;
        lastLastPos = pos;
        lastLastLastPos = pos;
        base.pos = pos + vel.normalized * 30f * Random.value;
        base.vel = vel;
        rotation = rot;
        pos += vel * 3f;
        this.lifeTime = lifeTime;
    }

    public override void Update(bool eu)
    {
        lastLastLastPos = lastLastPos;
        lastLastPos = lastPos;
        life += 1/lifeTime;
        if (life >= 1)
        {
            Destroy();
        }
        base.Update(eu);
    }

    public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        sLeaser.sprites = new FSprite[1];
        sLeaser.sprites[0] = new("ShortcutArrow");
        sLeaser.sprites[0].rotation = rotation;
        AddToContainer(sLeaser, rCam, null);
    }

    public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        sLeaser.sprites[0].SetPosition(pos - camPos);
        base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
    }

    public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
        sLeaser.sprites[0].color = color;
    }

    public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
    {
        base.AddToContainer(sLeaser, rCam, newContatiner);
    }
}
