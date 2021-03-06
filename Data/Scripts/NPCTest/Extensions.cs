using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using Draygo.API;
using System;

namespace Stollie.NPC_Test
{
    public static class Extensions
    {
        public static bool ContainsItem(this Base6Directions.Direction[] array, Base6Directions.Direction item)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == item)
                    return true;
            }

            return false;
        }

        public static bool ContainsItem(this MyDefinitionId[] array, MyDefinitionId item)
        {
            var comparer = MyDefinitionId.Comparer;
            for (int i = 0; i < array.Length; i++)
            {
                if (comparer.Equals(array[i], item))
                    return true;
            }

            return false;
        }

        public static bool ContainsItem(this MyObjectBuilderType[] array, MyObjectBuilderType item)
        {
            var comparer = MyObjectBuilderType.Comparer;
            for (int i = 0; i < array.Length; i++)
            {
                if (comparer.Equals(array[i], item))
                    return true;
            }

            return false;
        }

        public static bool IsValidPlayer(this IMyPlayer player)
        {
            return player != null && !player.IsBot && !string.IsNullOrWhiteSpace(player.DisplayName) && MyAPIGateway.Players.TryGetSteamId(player.IdentityId) != 0;
        }

        public static float GetBlockHealth(this IMySlimBlock slim)
        {
            if (slim == null || slim.MaxIntegrity == 0)
                return -1f;

            float maxIntegrity = slim.MaxIntegrity;
            float buildIntegrity = slim.BuildIntegrity;
            float currentDamage = slim.CurrentDamage;
            float health = (buildIntegrity - currentDamage) / maxIntegrity;
            return health;
        }

        public static bool IntersectsBillboard(this HudAPIv2.BillBoardHUDMessage bb, HudAPIv2.BillBoardHUDMessage other, ref double aspectRatio, bool getIntersection, out BoundingBox2D prunik, out BoundingBox2D otherBox, out BoundingBox2D bbBox)
        {
            prunik = BoundingBox2D.CreateInvalid();

            var bbCenter = bb.Origin + bb.Offset;
            var halfSize = new Vector2D(bb.Width * aspectRatio, bb.Height) * 0.5;
            var bbMin = bbCenter - halfSize;
            var bbMax = bbCenter + halfSize;

            var otherCenter = other.Origin + other.Offset;
            var halfSizeOther = new Vector2D(other.Width * aspectRatio, other.Height - 0.02) * 0.5;
            var otherMin = otherCenter - halfSizeOther;
            var otherMax = otherCenter + halfSizeOther;

            bbBox = new BoundingBox2D(bbMin, bbMax);
            otherBox = new BoundingBox2D(otherMin, otherMax);

            if (otherBox.Contains(bbBox) != ContainmentType.Intersects)
                return false;

            if (getIntersection)
                prunik = bbBox.Intersect(otherBox);

            return true;
        }

        public static bool IntersectsBillboard(this HudAPIv2.HUDMessage msg, HudAPIv2.BillBoardHUDMessage other, ref double aspectRatio, bool getIntersection, out BoundingBox2D prunik, out BoundingBox2D otherBox, out BoundingBox2D bbBox)
        {
            prunik = BoundingBox2D.CreateInvalid();

            var length = msg.GetTextLength();
            var bbCenter = msg.Origin + msg.Offset + length * 0.5;
            var halfSize = new Vector2D(length.X * aspectRatio, -length.Y) * 0.5;
            var bbMin = bbCenter - halfSize;
            var bbMax = bbCenter + halfSize;

            var otherCenter = other.Origin + other.Offset;
            var halfSizeOther = new Vector2D(other.Width * aspectRatio, other.Height - 0.02) * 0.5;
            var otherMin = otherCenter - halfSizeOther;
            var otherMax = otherCenter + halfSizeOther;

            bbBox = new BoundingBox2D(bbMin, bbMax);
            otherBox = new BoundingBox2D(otherMin, otherMax);

            if (otherBox.Contains(bbBox) != ContainmentType.Intersects)
                return false;

            if (getIntersection)
                prunik = bbBox.Intersect(otherBox);

            return true;
        }

        public static bool IsWithinBounds(this Vector2D vector, HudAPIv2.BillBoardHUDMessage bb, double aspectRatio, float sizeModifierX = 1, float sizeModifierY = 1)
        {
            var position = bb.Origin + bb.Offset;
            var halfSize = new Vector2D(bb.Width * aspectRatio, bb.Height) * new Vector2D(sizeModifierX, sizeModifierY) * 0.5;
            var min = position - halfSize;
            var max = position + halfSize;

            return vector.X > min.X && vector.Y > min.Y && vector.X < max.X && vector.Y < max.Y;
        }

        public static string ToString(this Vector2D vector, int decimals)
        {
            var x = Math.Round(vector.X, decimals);
            var y = Math.Round(vector.Y, decimals);
            return $"{{X:{y.ToString()} Y:{x.ToString()}}}";
        }

        public static string ToString(this Vector3D vector, int decimals)
        {
            var v = Vector3D.Round(vector, decimals);
            return $" X: {v.X.ToString()}\n Y: {v.Y.ToString()}\n Z: {v.Z.ToString()}";
        }

        public static float Volume(this BoundingSphere sphere)
        {
            var r = sphere.Radius;
            return 4f / 3f * MathHelper.Pi * r * r * r;
        }

        public static void ShellSort(this List<MyEntity> list, Vector3D checkPosition)
        {
            int length = list.Count;

            for (int h = length / 2; h > 0; h /= 2)
            {
                for (int i = h; i < length; i += 1)
                {
                    var tempValue = list[i];
                    double temp;
                    var pos = tempValue.PositionComp.WorldAABB.Center;
                    Vector3D.DistanceSquared(ref pos, ref checkPosition, out temp);

                    int j;
                    for (j = i; j >= h && Vector3D.DistanceSquared(list[j - h].PositionComp.WorldAABB.Center, checkPosition) > temp; j -= h)
                    {
                        list[j] = list[j - h];
                    }

                    list[j] = tempValue;
                }
            }
        }

        public static void ShellSort(this List<IMySlimBlock> list, Vector3D checkPosition, bool reverse = false)
        {
            int length = list.Count;
            var half = length / 2;

            for (int h = half; h > 0; h /= 2)
            {
                for (int i = h; i < length; i += 1)
                {
                    var tempValue = list[i];
                    double temp;
                    var pos = tempValue.CubeGrid.GridIntegerToWorld(tempValue.Position);
                    Vector3D.DistanceSquared(ref pos, ref checkPosition, out temp);

                    int j;
                    for (j = i; j >= h && Vector3D.DistanceSquared(list[j - h].CubeGrid.GridIntegerToWorld(list[j - h].Position), checkPosition) > temp; j -= h)
                    {
                        list[j] = list[j - h];
                    }

                    list[j] = tempValue;
                }
            }

            if (reverse)
            {
                for (int i = 0; i < half; i++)
                {
                    var tmp = list[i];
                    list[i] = list[length - i - 1];
                    list[length - i - 1] = tmp;
                }
            }
        }
    }
}
