using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.Numerics;
using System.Text;

namespace VMFParser
{
    public struct Vertex
    {
        float X;
        float Y;
        float Z;
        float W;

        public Vertex(float x, float y, float z, float w = 0)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public override string ToString()
        {
            return $"({X} {Y} {Z} {W})";
        }
    }

    public struct UV
    {
        float X;
        float Y;
        float Z;
        float W;
        float Scale;

        public UV(float x, float y, float z, float scale, float w = 0)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
            Scale = scale;
        }

        public override string ToString()
        {
            return $"[{X} {Y} {Z} {W}] {Scale}";
        }
    }

    public struct Plane
    {
        public Vector3 V1;
        public Vector3 V2;
        public Vector3 V3;
        public Vector3 Normal;

        public Plane(Vector3 v1, Vector3 v2, Vector3 v3) : this()
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
            Normal = CalcNormal();
        }

        public override string ToString()
        {
            return $"({V1.X} {V1.Y} {V1.Z}) ({V2.X} {V2.Y} {V2.Z}) ({V3.X} {V3.Y} {V3.Z})";
        }

        //Calculates the normal of the plane
        private Vector3 CalcNormal()
        {
            Vector3 dir = Vector3.Cross((V2 - V1), (V3 - V2));
            return (dir / dir.Length());
        }

        //Returns distance from closest point on plane to origin
        public float Distance()
        {
            return Math.Abs(Vector3.Dot(V1, Normal));
        }
    }

}