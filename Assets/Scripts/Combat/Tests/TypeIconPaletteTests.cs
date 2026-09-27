using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class TypeIconPaletteTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void GetIcon_ReturnsTypelessIconForNone()
        {
            TypeIconPaletteSO palette = CreatePalette();
            Sprite typeless = CreateSprite("typeless");
            SetPrivateField(palette, "typelessIcon", typeless);

            Assert.AreSame(typeless, palette.GetIcon(ChainlingType.None));
            Assert.AreSame(typeless, palette.TypelessIcon);

            Assert.IsTrue(palette.TryGetIcon(ChainlingType.None, out Sprite icon));
            Assert.AreSame(typeless, icon);
            Assert.AreSame(typeless, palette.BuildLookup()[ChainlingType.None]);
        }

        [Test]
        public void GetIcon_DoesNotUseTypelessIconForMissingConcreteType()
        {
            TypeIconPaletteSO palette = CreatePalette();
            Sprite typeless = CreateSprite("typeless");
            Sprite fallback = CreateSprite("fallback");
            SetPrivateField(palette, "typelessIcon", typeless);
            SetPrivateField(palette, "fallbackIcon", fallback);

            Assert.AreSame(fallback, palette.GetIcon(ChainlingType.Node));
        }

        [Test]
        public void FormatTypeName_LabelsNoneAsTypeless()
        {
            Assert.AreEqual("Typeless", MoveSelectDisplayUtility.FormatTypeName(ChainlingType.None));
        }

        private TypeIconPaletteSO CreatePalette()
        {
            TypeIconPaletteSO palette = ScriptableObject.CreateInstance<TypeIconPaletteSO>();
            _createdObjects.Add(palette);
            return palette;
        }

        private Sprite CreateSprite(string name)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.name = name + "_Texture";
            _createdObjects.Add(texture);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            _createdObjects.Add(sprite);
            return sprite;
        }

        private static void SetPrivateField<T>(TypeIconPaletteSO palette, string fieldName, T value)
        {
            FieldInfo field = typeof(TypeIconPaletteSO).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(palette, value);
        }
    }
}
