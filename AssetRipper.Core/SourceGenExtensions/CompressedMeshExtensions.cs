﻿using AssetRipper.Core.Classes.Mesh;
using AssetRipper.Core.Math.Vectors;
using AssetRipper.Numerics;
using AssetRipper.SourceGenerated.Subclasses.CompressedMesh;
using System.Numerics;

namespace AssetRipper.Core.SourceGenExtensions
{
	public static class CompressedMeshExtensions
	{
		public static bool IsSet(this ICompressedMesh compressedMesh) => compressedMesh.Vertices.NumItems > 0;

		public static void DecompressCompressedMesh(this ICompressedMesh compressedMesh,
			UnityVersion version,
			out Vector3[]? vertices,
			out Vector3[]? normals,
			out Vector4[]? tangents,
			out ColorFloat[]? colors,
			out BoneWeight4[]? skin,
			out Vector2[]? uv0,
			out Vector2[]? uv1,
			out Vector2[]? uv2,
			out Vector2[]? uv3,
			out Vector2[]? uv4,
			out Vector2[]? uv5,
			out Vector2[]? uv6,
			out Vector2[]? uv7,
			out Matrix4x4[]? bindPose,
			out uint[]? processedIndexBuffer)
		{
			int vertexCount = default;
			vertices = default;
			normals = default;
			tangents = default;
			colors = default;
			skin = default;
			uv0 = default;
			uv1 = default;
			uv2 = default;
			uv3 = default;
			uv4 = default;
			uv5 = default;
			uv6 = default;
			uv7 = default;
			bindPose = default;
			processedIndexBuffer = default;

			//Vertex
			if (compressedMesh.Vertices.NumItems > 0)
			{
				vertexCount = (int)compressedMesh.Vertices.NumItems / 3;
				vertices = GetVertices(compressedMesh);
			}
			//UV
			if (compressedMesh.UV.NumItems > 0)
			{
				uint m_UVInfo = compressedMesh.UVInfo;
				if (compressedMesh.Has_UVInfo() && m_UVInfo != 0)
				{
					const int kInfoBitsPerUV = 4;
					const int kUVDimensionMask = 3;
					const int kUVChannelExists = 4;
					const int kMaxTexCoordShaderChannels = 8;

					int uvSrcOffset = 0;
					for (int uvIndex = 0; uvIndex < kMaxTexCoordShaderChannels; uvIndex++)
					{
						uint texCoordBits = m_UVInfo >> (uvIndex * kInfoBitsPerUV);
						texCoordBits &= (1u << kInfoBitsPerUV) - 1u;
						if ((texCoordBits & kUVChannelExists) != 0)
						{
							int uvDim = 1 + (int)(texCoordBits & kUVDimensionMask);
							Vector2[] m_UV = MeshHelper.FloatArrayToVector2(compressedMesh.UV.UnpackFloats(uvDim, uvDim * 4, uvSrcOffset, vertexCount));
							switch (uvIndex)
							{
								case 0:
									uv0 = m_UV;
									break;
								case 1:
									uv1 = m_UV;
									break;
								case 2:
									uv2 = m_UV;
									break;
								case 3:
									uv3 = m_UV;
									break;
								case 4:
									uv4 = m_UV;
									break;
								case 5:
									uv5 = m_UV;
									break;
								case 6:
									uv6 = m_UV;
									break;
								case 7:
									uv7 = m_UV;
									break;
								default:
									throw new IndexOutOfRangeException();
							}
							uvSrcOffset += uvDim * vertexCount;
						}
					}
				}
				else
				{
					uv0 = MeshHelper.FloatArrayToVector2(compressedMesh.UV.UnpackFloats(2, 2 * 4, 0, vertexCount));
					if (compressedMesh.UV.NumItems >= vertexCount * 4)
					{
						uv1 = MeshHelper.FloatArrayToVector2(compressedMesh.UV.UnpackFloats(2, 2 * 4, vertexCount * 2, vertexCount));
					}
				}
			}
			//BindPose
			if (compressedMesh.Has_BindPoses() && compressedMesh.BindPoses.NumItems > 0)
			{
				bindPose = GetBindPoses(compressedMesh);
			}
			//Normal
			if (compressedMesh.Normals.NumItems > 0)
			{
				normals = GetNormals(compressedMesh);
			}
			//Tangent
			if (compressedMesh.Tangents.NumItems > 0)
			{
				tangents = GetTangents(compressedMesh);
			}
			//FloatColor / Color
			if ((compressedMesh.Has_FloatColors() && compressedMesh.FloatColors.NumItems > 0)
				|| (compressedMesh.Has_Colors() && compressedMesh.Colors.NumItems > 0))
			{
				colors = GetFloatColors(compressedMesh);
			}
			//Skin
			if (compressedMesh.Weights.NumItems > 0)
			{
				skin = GetWeights(compressedMesh);
			}
			//IndexBuffer
			if (compressedMesh.Triangles.NumItems > 0)
			{
				processedIndexBuffer = GetTriangles(compressedMesh);
			}
		}

		public static BoneWeight4[] GetWeights(this ICompressedMesh compressedMesh)
		{
			int[] weights = compressedMesh.Weights.UnpackInts();
			int[] boneIndices = compressedMesh.BoneIndices.UnpackInts();

			BoneWeight4[] skin = new BoneWeight4[compressedMesh.Weights.NumItems];

			int bonePos = 0;
			int boneIndexPos = 0;
			int j = 0;
			int sum = 0;

			for (int i = 0; i < compressedMesh.Weights.NumItems; i++)
			{
				//read bone index and weight.
				{
					BoneWeight4 boneWeight = skin[bonePos];
					boneWeight.SetWeight(j, weights[i] / 31f);
					boneWeight.SetIndex(j, boneIndices[boneIndexPos++]);
					skin[bonePos] = boneWeight;
				}
				j++;
				sum += weights[i];

				//the weights add up to one. fill the rest for this vertex with zero, and continue with next one.
				if (sum >= 31)
				{
					for (; j < 4; j++)
					{
						BoneWeight4 boneWeight = skin[bonePos];
						boneWeight.SetWeight(j, 0);
						boneWeight.SetIndex(j, 0);
						skin[bonePos] = boneWeight;
					}
					bonePos++;
					j = 0;
					sum = 0;
				}
				//we read three weights, but they don't add up to one. calculate the fourth one, and read
				//missing bone index. continue with next vertex.
				else if (j == 3)
				{
					BoneWeight4 boneWeight = skin[bonePos];
					boneWeight.SetWeight(j, (31 - sum) / 31f);
					boneWeight.SetIndex(j, boneIndices[boneIndexPos++]);
					skin[bonePos] = boneWeight;
					bonePos++;
					j = 0;
					sum = 0;
				}
			}

			return skin;
		}

		public static Vector3[] GetNormals(this ICompressedMesh compressedMesh)
		{
			float[] normalData = compressedMesh.Normals.UnpackFloats(2, 4 * 2);
			int[] signs = compressedMesh.NormalSigns.UnpackInts();
			Vector3[] normals = new Vector3[compressedMesh.Normals.NumItems / 2];
			for (int i = 0; i < compressedMesh.Normals.NumItems / 2; ++i)
			{
				float x = normalData[(i * 2) + 0];
				float y = normalData[(i * 2) + 1];
				float zsqr = 1 - (x * x) - (y * y);
				float z;
				if (zsqr >= 0)
				{
					z = (float)System.Math.Sqrt(zsqr);
				}
				else
				{
					z = 0;
					Vector3 normal = Vector3.Normalize(new Vector3(x, y, z));
					x = normal.X;
					y = normal.Y;
					z = normal.Z;
				}
				if (signs[i] == 0)
				{
					z = -z;
				}

				normals[i] = new Vector3(x, y, z);
			}

			return normals;
		}

		public static Vector4[] GetTangents(this ICompressedMesh compressedMesh)
		{
			float[] tangentData = compressedMesh.Tangents.UnpackFloats(2, 4 * 2);
			int[] signs = compressedMesh.TangentSigns.UnpackInts();
			Vector4[] tangents = new Vector4[compressedMesh.Tangents.NumItems / 2];
			for (int i = 0; i < compressedMesh.Tangents.NumItems / 2; ++i)
			{
				float x = tangentData[(i * 2) + 0];
				float y = tangentData[(i * 2) + 1];
				float zsqr = 1 - (x * x) - (y * y);
				float z;
				if (zsqr >= 0f)
				{
					z = (float)System.Math.Sqrt(zsqr);
				}
				else
				{
					z = 0;
					Vector3 tangent = Vector3.Normalize(new Vector3(x, y, z));
					x = tangent.X;
					y = tangent.Y;
					z = tangent.Z;
				}
				if (signs[(i * 2) + 0] == 0)
				{
					z = -z;
				}

				float w = signs[(i * 2) + 1] > 0 ? 1.0f : -1.0f;
				tangents[i] = new Vector4f(x, y, z, w);
			}

			return tangents;
		}

		public static Matrix4x4[] GetBindPoses(this ICompressedMesh compressedMesh)
		{
			if (compressedMesh.Has_BindPoses())
			{
				Matrix4x4[]? bindPose = new Matrix4x4[compressedMesh.BindPoses.NumItems / 16];
				float[] m_BindPoses_Unpacked = compressedMesh.BindPoses.UnpackFloats(16, 4 * 16);
				float[] buffer = new float[16];
				for (int i = 0; i < bindPose.Length; i++)
				{
					Array.Copy(m_BindPoses_Unpacked, i * 16, buffer, 0, 16);
					bindPose[i] = ToMatrix(buffer);
				}

				return bindPose;
			}
			else
			{
				return Array.Empty<Matrix4x4>(); 
			}
		}

		public static Vector3[] GetVertices(this ICompressedMesh compressedMesh)
		{
			float[] verticesData = compressedMesh.Vertices.UnpackFloats(3, 3 * 4);
			return MeshHelper.FloatArrayToVector3(verticesData);
		}

		public static ColorFloat[] GetFloatColors(this ICompressedMesh compressedMesh)
		{
			if (compressedMesh.Has_FloatColors())
			{
				return MeshHelper.FloatArrayToColorFloat(compressedMesh.FloatColors.UnpackFloats(1, 4));
			}
			else if (compressedMesh.Has_Colors())
			{
				compressedMesh.Colors.NumItems *= 4;
				compressedMesh.Colors.BitSize /= 4;
				int[] tempColors = compressedMesh.Colors.UnpackInts();
				ColorFloat[] colors = new ColorFloat[compressedMesh.Colors.NumItems / 4];
				for (int v = 0; v < compressedMesh.Colors.NumItems / 4; v++)
				{
					colors[v] = (ColorFloat)new Color32((byte)tempColors[4 * v], (byte)tempColors[(4 * v) + 1], (byte)tempColors[(4 * v) + 2], (byte)tempColors[(4 * v) + 3]);
				}
				compressedMesh.Colors.NumItems /= 4;
				compressedMesh.Colors.BitSize *= 4;
				return colors;
			}
			else
			{
				return Array.Empty<ColorFloat>();
			}
		}

		public static uint[] GetTriangles(this ICompressedMesh compressedMesh)
		{
			return compressedMesh.Triangles.UnpackUInts();
		}

		private static Matrix4x4 ToMatrix(float[] values)
		{
			if (values == null)
			{
				throw new ArgumentNullException(nameof(values));
			}

			if (values.Length != 16)
			{
				throw new ArgumentOutOfRangeException(nameof(values), "There must be exactly sixteen input values for Matrix.");
			}

			return new()
			{
				M11 = values[0],
				M12 = values[1],
				M13 = values[2],
				M14 = values[3],

				M21 = values[4],
				M22 = values[5],
				M23 = values[6],
				M24 = values[7],

				M31 = values[8],
				M32 = values[9],
				M33 = values[10],
				M34 = values[11],

				M41 = values[12],
				M42 = values[13],
				M43 = values[14],
				M44 = values[15],
			};
		}
	}
}
