// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

namespace Foundation.Math
{
    public struct Color
    {
        public float r, g, b, a;

        public Color()
        {
            r = 0.0f;
            g = 0.0f;
            b = 0.0f;
            a = 1.0f;
        }

        public Color(float r, float g, float b, float a = 1.0f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public void Set(float r, float g, float b, float a = 1.0f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public float[] ToArray()
        {
            return new float[4] { r, g, b, a };
        }

        public static readonly Color Red = new Color(1.0f, 0.0f, 0.0f);
        public static readonly Color Yellow = new Color(1.0f, 1.0f, 0.0f);
        public static readonly Color Green = new Color(0.0f, 1.0f, 0.0f);
        public static readonly Color Blue = new Color(0.0f, 0.0f, 1.0f);
        public static readonly Color Black = new Color(0.0f, 0.0f, 0.0f);
        public static readonly Color White = new Color(1.0f, 1.0f, 1.0f);

    }
}
