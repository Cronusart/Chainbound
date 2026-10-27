using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using SignalJam.UI.MenuNavigation;

public sealed class ProfilePortraitTurntableTests
{
    [Test]
    public void Holds_VisitUpRightDownLeftThenUp_AndShareFreezeSelectsDown()
    {
        GameObject host = new GameObject("turntable-test");
        GameObject bodyObject = new GameObject("body");
        try
        {
            Image body = bodyObject.AddComponent<Image>();
            ProfilePortraitTurntable turntable = host.AddComponent<ProfilePortraitTurntable>();
            Sprite up = MakeSprite();
            Sprite right = MakeSprite();
            Sprite down = MakeSprite();
            Sprite left = MakeSprite();

            turntable.SetBody(body, null, null);
            turntable.SetSequences(new[] { up }, new[] { right }, new[] { down }, new[] { left });

            Assert.AreEqual(ProfilePortraitTurntable.Facing.Up, turntable.CurrentFacing);
            Assert.AreEqual(up, body.sprite);

            turntable.Tick(ProfilePortraitTurntable.HoldSeconds);
            Assert.AreEqual(ProfilePortraitTurntable.Facing.Right, turntable.CurrentFacing);
            Assert.AreEqual(right, body.sprite);

            turntable.Tick(ProfilePortraitTurntable.HoldSeconds);
            Assert.AreEqual(ProfilePortraitTurntable.Facing.Down, turntable.CurrentFacing);

            turntable.Tick(ProfilePortraitTurntable.HoldSeconds);
            Assert.AreEqual(ProfilePortraitTurntable.Facing.Left, turntable.CurrentFacing);

            turntable.Tick(ProfilePortraitTurntable.HoldSeconds);
            Assert.AreEqual(ProfilePortraitTurntable.Facing.Up, turntable.CurrentFacing);

            turntable.FreezeOnDown();
            Assert.IsTrue(turntable.IsFrozen);
            Assert.AreEqual(ProfilePortraitTurntable.Facing.Down, turntable.CurrentFacing);
            Assert.AreEqual(down, body.sprite);

            turntable.Tick(ProfilePortraitTurntable.HoldSeconds * 4f);
            Assert.AreEqual(ProfilePortraitTurntable.Facing.Down, turntable.CurrentFacing);
            Assert.AreEqual(down, body.sprite);
        }
        finally
        {
            Object.DestroyImmediate(bodyObject);
            Object.DestroyImmediate(host);
        }
    }

    private static Sprite MakeSprite()
    {
        Texture2D texture = new Texture2D(4, 4);
        return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 16f);
    }
}
