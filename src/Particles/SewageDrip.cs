using UnityEngine;
using RWCustom;


namespace DrainMites.Particles;

public class SewageDrip : CosmeticSprite
{
    private float lastLife;
    public float life;
    public float lifeTime;

    public Vector2 lastLastPos;
    public Vector2 lastLastLastPos;

    public Color? color;

    public float width;

    public bool mustExitTerrainOnceToBeDestroyedByTerrain;

    public SewageDrip(Vector2 pos, Vector2 vel, float dripWidth, Color? color = null)
    {
        base.pos = pos;
        lastPos = pos;
        lastLastPos = pos;
        lastLastLastPos = pos;
        base.vel = vel;
        this.color = color;
        width = dripWidth;
        lifeTime = Random.Range(80, 120);
    }

    public override void Update(bool eu)
    {
        lastLastLastPos = lastLastPos;
        lastLastPos = lastPos;
        vel.y -= 0.9f * room.gravity;
        lastLife = life;
        life += 1/lifeTime;

        if (lastLife >= 1 ||
            pos.y < room.FloatWaterLevel(pos.x) ||
            (room.GetTile(pos).Terrain == Room.Tile.TerrainType.Solid && !mustExitTerrainOnceToBeDestroyedByTerrain))
        {
            Destroy();
        }

        if (mustExitTerrainOnceToBeDestroyedByTerrain && !room.GetTile(pos).Solid)
        {
            mustExitTerrainOnceToBeDestroyedByTerrain = false;
        }

        base.Update(eu);
    }

    public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        sLeaser.sprites = new FSprite[2];
        TriangleMesh.Triangle[] tris = new TriangleMesh.Triangle[1]
        {
            new TriangleMesh.Triangle(0, 1, 2)
        };
        TriangleMesh triangleMesh = new TriangleMesh("Futile_White", tris, customColor: false);
        sLeaser.sprites[0] = triangleMesh;
        sLeaser.sprites[1] = new FSprite("Circle20");
        AddToContainer(sLeaser, rCam, null);
    }

    public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        Vector2 visPos = Vector2.Lerp(lastPos, pos, timeStacker);
        Vector2 visLastPos = Vector2.Lerp(lastLastLastPos, lastLastPos, timeStacker);
        if (lastLife < 1 && life >= 1)
        {
            visLastPos = Vector2.Lerp(visLastPos, visPos, timeStacker);
        }
        Vector2 dripDir = visPos - visLastPos;
        Vector2 perpDir = Custom.PerpendicularVector(dripDir.normalized);

        TriangleMesh TrailMesh = sLeaser.sprites[0] as TriangleMesh;
        TrailMesh.MoveVertice(0, visPos + perpDir * width - camPos);
        TrailMesh.MoveVertice(1, visPos - perpDir * width - camPos);
        TrailMesh.MoveVertice(2, visLastPos - camPos);

        sLeaser.sprites[1].x = Mathf.Lerp(lastPos.x, pos.x, timeStacker) - camPos.x;
        sLeaser.sprites[1].y = Mathf.Lerp(lastPos.y, pos.y, timeStacker) - camPos.y;
        sLeaser.sprites[1].scale = width * 0.1f;
        sLeaser.sprites[1].isVisible = true;

        base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
    }

    public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
        if (!color.HasValue)
        {
            color = Color.Lerp(palette.waterColor1, palette.waterColor2, 0.5f);
        }
        for (int s = 0; s < sLeaser.sprites.Length; s++)
        {
            sLeaser.sprites[s].color = color.Value;
        }
    }

    public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
    {
        base.AddToContainer(sLeaser, rCam, newContatiner);
    }

}