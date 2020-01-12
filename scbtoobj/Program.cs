using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scbtoobj
{
    public class Vector2 : IEquatable<Vector2>
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Vector2(BinaryReader br)
        {
            X = br.ReadSingle();
            Y = br.ReadSingle();
        }

        public Vector2(Vector2 vector2)
        {
            X = vector2.X;
            Y = vector2.Y;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(X);
            bw.Write(Y);
        }

        public bool Equals(Vector2 other)
        {
            return (X == other.X) && (Y == other.Y);
        }

        public static Vector2 operator +(Vector2 x, Vector2 y)
        {
            return new Vector2(x.X + y.X, x.Y + y.Y);
        }

        public static Vector2 operator -(Vector2 x, Vector2 y)
        {
            return new Vector2(x.X - y.X, x.Y - y.Y);
        }
    }

    public class Vector3 : IEquatable<Vector3>
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public float Magnitude { get => (float)Math.Sqrt((X * X) + (Y * Y) + (Z * Z)); }

        public Vector3()
        {

        }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3(BinaryReader br)
        {
            X = br.ReadSingle();
            Y = br.ReadSingle();
            Z = br.ReadSingle();
        }

        public Vector3(Vector3 vector3)
        {
            X = vector3.X;
            Y = vector3.Y;
            Z = vector3.Z;
        }

        public Vector3(StreamReader sr)
        {
            string[] input = sr.ReadLine().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            X = float.Parse(input[0], CultureInfo.InvariantCulture.NumberFormat);
            Y = float.Parse(input[1], CultureInfo.InvariantCulture.NumberFormat);
            Z = float.Parse(input[2], CultureInfo.InvariantCulture.NumberFormat);
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(X);
            bw.Write(Y);
            bw.Write(Z);
        }

        public void Write(StreamWriter sw, string format)
        {
            sw.Write(string.Format(format, X, Y, Z));
        }

        public bool Equals(Vector3 other)
        {
            return (X == other.X) && (Y == other.Y) && (Z == other.Z);
        }
    }

    public class SCBFace
    {
        public uint[] Indices { get; private set; }
        public string Material { get; private set; }
        public Vector2[] UVs { get; private set; }

        public SCBFace(UInt32[] indices, string material, Vector2[] uvs)
        {
            Indices = indices;
            Material = material;
            UVs = uvs;
        }

        public SCBFace(BinaryReader br)
        {
            Indices = new uint[] { br.ReadUInt32(), br.ReadUInt32(), br.ReadUInt32() };
            Material = Encoding.ASCII.GetString(br.ReadBytes(64)).Replace("\0", "");
            float[] uvs = new float[] { br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle() };
            UVs = new Vector2[]
            {
                new Vector2(uvs[0], uvs[3]),
                new Vector2(uvs[1], uvs[4]),
                new Vector2(uvs[2], uvs[5])
            };
        }
    }

    public class SCBFile
    {
        [Flags]
        public enum SCBFlags : uint
        {
            VERTEX_COLORS = 1,
            TANGENTS = 2
        }

        public List<Vector3> Vertices { get; private set; } = new List<Vector3>();
        public Dictionary<string, List<SCBFace>> Materials { get; private set; } = new Dictionary<string, List<SCBFace>>();

        public SCBFile(Stream stream)
        {
            using (BinaryReader br = new BinaryReader(stream))
            {
                string magic = Encoding.ASCII.GetString(br.ReadBytes(8));
                if (magic != "r3d2Mesh")
                {
                    throw new Exception("This is not a valid SCB file");
                }

                ushort major = br.ReadUInt16();
                ushort minor = br.ReadUInt16();
                if (major != 3 && major != 2 && minor != 1)        
                {
                    throw new Exception(string.Format("The Version: {0}.{1} is not supported", major, minor));
                }

                string Name = Encoding.ASCII.GetString(br.ReadBytes(128)).Replace("\0", "");
                uint vertexCount = br.ReadUInt32();
                uint faceCount = br.ReadUInt32();
                SCBFlags flags = (SCBFlags)br.ReadUInt32();
                Vector3 Org = new Vector3(br);
                Vector3 Size = new Vector3(br);

                bool hasTangents = false;
                if (major == 3 && minor == 2)
                {
                    hasTangents = br.ReadUInt32() == 1;
                }

                for (int i = 0; i < vertexCount; i++)
                {
                    Vertices.Add(new Vector3(br));
                }

                if (major == 3 && minor == 2 && flags.HasFlag(SCBFlags.TANGENTS) && hasTangents)
                {
                    for (int i = 0; i < vertexCount; i++)
                    {
                        byte o = br.ReadByte();
                        byte k = br.ReadByte();
                        byte m = br.ReadByte();
                        byte n = br.ReadByte();
                    }
                }

                float u = br.ReadSingle();
                float j = br.ReadSingle();
                float b = br.ReadSingle();

                for (int i = 0; i < faceCount; i++)
                {
                    SCBFace face = new SCBFace(br);

                    if (!Materials.ContainsKey(face.Material))
                    {
                        Materials.Add(face.Material, new List<SCBFace>());
                    }

                    Materials[face.Material].Add(face);
                }
            }
        }
    }

    public class OBJFace
    {
        public uint[] VertexIndices { get; set; }
        public uint[] UVIndices { get; set; }

        public OBJFace(uint[] vertexIndices)
        {
            VertexIndices = vertexIndices;
        }

        public OBJFace(uint[] vertexIndices, uint[] uvIndices)
        {
            VertexIndices = vertexIndices;
            UVIndices = uvIndices;
        }

        public void Write(StreamWriter sw)
        {
            if (UVIndices != null)
            {
                sw.WriteLine(string.Format(
                    "f {0}/{1} {2}/{3} {4}/{5}",
                    VertexIndices[0] + 1,
                    UVIndices[0] + 1,
                    VertexIndices[1] + 1,
                    UVIndices[1] + 1,
                    VertexIndices[2] + 1,
                    UVIndices[2] + 1
                    ));
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string[] files = Directory.GetFiles(args[0]);
            foreach (string file in files)
            {
                try
                {
                    Stream stream = File.Open(file, FileMode.Open);
                    SCBFile scb = new SCBFile(stream);

                    List<uint> indices = new List<uint>();
                    List<Vector2> uvs = new List<Vector2>();

                    foreach (KeyValuePair<string, List<SCBFace>> material in scb.Materials)
                    {
                        foreach (SCBFace face in material.Value)
                        {
                            indices.AddRange(face.Indices);
                            uvs.AddRange(face.UVs);
                        }
                    }

                    List<OBJFace> Faces = new List<OBJFace>();
                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        uint[] faceIndices = new uint[] { indices[i], indices[i + 1], indices[i + 2] };
                        Faces.Add(new OBJFace(faceIndices, faceIndices));
                    }

                    string name = Path.GetFileNameWithoutExtension(file);
                    Stream streame = File.OpenWrite(args[1] + "/" + name + ".obj");
                    using (StreamWriter sw = new StreamWriter(streame))
                    {
                        foreach (Vector3 vertex in scb.Vertices)
                        {
                            sw.WriteLine(string.Format("v {0} {1} {2}", vertex.X, vertex.Y, vertex.Z));
                        }
                        foreach (Vector2 uv in uvs)
                        {
                            sw.WriteLine(string.Format("vt {0} {1}", uv.X, 1 - uv.Y));
                        }
                        foreach (OBJFace face in Faces)
                        {
                            face.Write(sw);
                        }
                    }
                }
                catch 
                {
                    continue;
                }
            }
        }
    }
}
