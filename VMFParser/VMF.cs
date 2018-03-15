using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;

namespace VMFParser
{
	public class VMF
	{
		private StreamReader sr;
		private string filePath;

		public List<GenericVMFClass> Classes { get; private set; }


		public VMF(string _filePath, bool collapseHiddenClasses = false)
		{
			if (!File.Exists(_filePath))
			{
				Console.WriteLine("Could not find file");
				return;
			}

			sr = new StreamReader(_filePath);
			filePath = _filePath;

			ReadVMF(collapseHiddenClasses);

		}

		//Reads the vmf and returns list of raw classes
		private void ReadVMF(bool ignoreHiddenClass)
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			Classes = new List<GenericVMFClass>();

			//Read until end of file
			while (!sr.EndOfStream)
			{
				GenericVMFClass vClass = ReadClass();
				Classes.Add(vClass);
			}

			//Collapse the hidden class
			if (ignoreHiddenClass)
			{
				//Get world class from classes
				var worldClass = (from vClass in Classes
								 where vClass.ClassName == "world"
								 select vClass).First();
				
				//Get hidden class from world class
				var hiddenClasses = from hClass in worldClass.ChildClasses
					where hClass.ClassName == "hidden"
					select hClass;

				//Remove hidden classes
				int worldPos = Classes.IndexOf(worldClass);
				Classes.Remove(worldClass);

				var hiddenClassesArray = hiddenClasses as GenericVMFClass[] ?? hiddenClasses.ToArray();
				worldClass.ChildClasses.Except(hiddenClassesArray);

				//Collapse hidden classes
				foreach (var hiddenClass in hiddenClassesArray)
					hiddenClass.Collapse(ref worldClass);

				//Re add world class Back
				Classes.Insert(worldPos, worldClass);
			}
				

			stopwatch.Stop();
			Console.WriteLine($"\nCompleted in {stopwatch.Elapsed}");
		}

		GenericVMFClass ReadClass(string className = null)
		{
			string line;

			//Read ClassName
			if (string.IsNullOrEmpty(className))
				className = sr.ReadLine();

			//Skip initial bracket
			sr.ReadLine();

			List<KeyValuePair<string, object>> properties = new List<KeyValuePair<string, object>>();
			List<GenericVMFClass> childClasses = new List<GenericVMFClass>();

			//read up until final curly.
			//Read properties inside brackets
			while ((line = sr.ReadLine()?.Trim()) != "}")
			{
				var property = ReadProperty(line);

				//Handle nested classes
				if (line != null && (property == null && line.Trim() != "}" && line.Trim() != "{"))
				{
					childClasses.Add(ReadClass(line.Trim()));
				}

				if (property != null) properties.Add((KeyValuePair<string, object>) property);
			}

			return new GenericVMFClass(className, properties, childClasses);
		}

		KeyValuePair<string, object>? ReadProperty(string line)
		{
			//Get key and value surrounded by quotes
			string[] keyValue = line.Replace("\"", "").Trim().Split(new []{' '}, 2);

			//return null if there are less than 2 values
			if (keyValue.Length < 2)
				return null;

			//Return value as a plane or vec3
			if (keyValue[1].Contains("("))
			{
				string[] value = keyValue[1].Replace("(", "").Replace(")", "").Split(' ');

				//Return Vec3
				if (value.Length <= 3)
				{
					Vector3 outVec3 = new Vector3(
							ParseFloat(value[0]),
							ParseFloat(value[1]),
							ParseFloat(value[2])
						);
					return new KeyValuePair<string, object>(keyValue[0], outVec3);
				}

				//Return plane
				Plane outPlane = new Plane(
					new Vector3(ParseFloat(value[0]), ParseFloat(value[1]), ParseFloat(value[2])),
					new Vector3(ParseFloat(value[3]), ParseFloat(value[4]), ParseFloat(value[5])),
					new Vector3(ParseFloat(value[6]), ParseFloat(value[7]), ParseFloat(value[8]))
				);
				return new KeyValuePair<string, object>(keyValue[0], outPlane);
			}
			
			//Return value as a vector 3 or UV
			if (keyValue[1].Contains("["))
			{
				string[] value = keyValue[1].Replace("[", "").Replace("]", "").Split(' ');

				if (value.Length == 5)
				{
					//Return as UV
					UV outUV = new UV(
						ParseFloat(value[0]),
						ParseFloat(value[1]),
						ParseFloat(value[2]),
						ParseFloat(value[3])
						);
					return new KeyValuePair<string, object>(keyValue[0], outUV);
				}

				//Return vector2
				if (value.Length == 2)
				{
					Vector2 outVec2 = new Vector2(
							ParseFloat(value[0]),
							ParseFloat(value[1])
						);
					return new KeyValuePair<string, object>(keyValue[0], outVec2);
				}

				//Return vector3
				Vector3 outVec3 = new Vector3(
						ParseFloat(value[0]),
						ParseFloat(value[1]),
						ParseFloat(value[2])
					);
				return new KeyValuePair<string, object>(keyValue[0], outVec3);
			}

			//Return value as decimal number
			if (Regex.IsMatch(keyValue[1], @"^\d.\d.+$"))
			{
				float.TryParse(keyValue[1], out var value);
				return new KeyValuePair<string, object>(keyValue[0], value);
			}

			//Return value as decimal
			if (Regex.IsMatch(keyValue[1], @"^\d+$"))
			{
				Int32.TryParse(keyValue[1], out var value);
				return new KeyValuePair<string, object>(keyValue[0], value);
			}

			//Return value as string
			return new KeyValuePair<string, object>(keyValue[0], keyValue[1]);
		}

		public static float ParseFloat(string floatString)
		{
			try
			{
				return float.Parse(floatString);
			}
			catch (Exception e)
			{
				//Return 0 on error
				Console.WriteLine("Couldn't parse value, defaulting to 0");
				return 0;
			}
		}

		public List<GenericVMFClass> FindVmfClass(string className, List<GenericVMFClass> searchClass = null)
		{
			List<GenericVMFClass> classes = new List<GenericVMFClass>();

			//Search all top level classe
			classes = (from vClass in Classes
				where vClass.ClassName == className
				select vClass).ToList();

			//Search child classes
			foreach (var vmfClass in Classes)
				classes.AddRange(vmfClass.FindClass(className));

			return classes;
		}

		//Returns a list of classes whose class or child class contain property
		public List<GenericVMFClass> FindProperty(string searchKey = "", object searchValue = null)
		{
			List<GenericVMFClass> matches = new List<GenericVMFClass>();
			foreach (var vmfClass in Classes)
			{
				if (vmfClass.HasProperty(searchKey, searchValue))
					matches.Add(vmfClass);
			}

			return matches;
		}

		public List<Solid> GetSolids()
		{
			List<Solid> solids = new List<Solid>();

			var solidClasses = FindVmfClass("solid");
							   

			solids = (from solid in solidClasses
				select new Solid(solid)).ToList();

			return solids;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder("");

			foreach (var vClass in Classes)
				sb.AppendLine(vClass.ToString());

			return sb.ToString();
		}

		//Syntatic sugar for getting vmf classes by type
		public GenericVMFClass[] this[string searchType] => FindVmfClass(searchType).ToArray();
	}

	//public abstract class VMFClass
	//{
	//    public abstract void Convert(GenericVMFClass vmfClass);
	//}
	public class GenericVMFClass
	{
		public string ClassName { get; }

		public List<KeyValuePair<string, object>> Properties { get; }
		public List<GenericVMFClass> ChildClasses { get; }

		public GenericVMFClass(string className, List<KeyValuePair<string, object>> properties, List<GenericVMFClass> childClasses)
		{
			ClassName = className;
			Properties = properties;
			ChildClasses = childClasses;
		}

		public List<GenericVMFClass> FindClass(string searchClass, bool searchChildren = true)
		{
			List<GenericVMFClass> matches = new List<GenericVMFClass>();

			matches = (from childClass in ChildClasses
					   where childClass.ClassName == searchClass
					   select childClass).ToList();

			if (searchChildren)
				//Search through child classes
				matches.AddRange((from vmfClass in ChildClasses
							 where vmfClass.ChildClasses.Count > 0
							 let potentialMatch = vmfClass.FindClass(searchClass, searchChildren)
							 select potentialMatch).SelectMany(x => x));

			return matches;
		}

		public bool HasClass(string searchClass, bool searchChildren = true)
		{
			List<GenericVMFClass> matches = new List<GenericVMFClass>();

			matches = (from childClass in ChildClasses
				where childClass.ClassName == searchClass
				select childClass).ToList();

			if (matches.Count > 0)
				return true;

			//Search through child classes
			matches.AddRange(from vmfClass in ChildClasses 
							 where vmfClass.ChildClasses.Count > 0
							 where vmfClass.HasClass(searchClass)
							 select vmfClass);

			if (matches.Count > 0)
				return true;

			return false;
		}

		public List<KeyValuePair<string, object>> GetProperty(string searchKey)
		{
			List<KeyValuePair<string, object>> matches = (from property in Properties
														 where property.Key == searchKey
														 select property).ToList();

			return matches;
		}

		public bool HasProperty(string searchKey = "", object searchValue = null, bool firstClass = true)
		{
			List<KeyValuePair<string, object>> matches = new List<KeyValuePair<string, object>>();

			if (searchKey == "" && searchValue != null)
			{
				//Only search for values
				matches = (from property in Properties
						   where property.Value.Equals(searchValue)
						   select property).ToList();

				if (matches.Count > 0)
					return true;
			}
			else if (searchValue == null)
			{
				//Only search for keys
				matches = (from property in Properties
					where property.Key == searchKey
					select property).ToList();

				if (matches.Count > 0)
					return true;
			}
			else
			{
				//Search for key and value
				matches = (from property in Properties
						   where property.Key == searchKey
						   where property.Value.Equals(searchValue)
						   select property).ToList();

				if (matches.Count > 0)
					return true;
			}

			//Search child classes
			if (ChildClasses.Count > 0)
				foreach (var childClass in ChildClasses)
					if (childClass.HasProperty(searchKey, searchValue, false))
						return true;

			return false;
		}

		public override string ToString()
		{
			return PrintPretty();
		}

		//Print out in a tree type structure
		private string PrintPretty(string indent = "")
		{
			StringBuilder sb = new StringBuilder(indent + ClassName  + "\n");
			sb.AppendLine(indent + "{");

			string oldIndent = indent;
			indent += "   ";

			foreach (var property in Properties)
			{
				//Print out vectors with () instead of <>
				//Handle special cases
				if (property.Value is Vector3 vec3)
				{
					sb.AppendLine($"{indent}\"{property.Key}\": \"({vec3.X} {vec3.Y} {vec3.Z})\"");

				}
				else if (property.Value is Vector2 vec2) //Print out vectors with () instead of <>
				{
					sb.AppendLine($"{indent}\"{property.Key}\": \"[{vec2.X} {vec2.Y}]\"");
				}

				sb.AppendLine($"{indent}\"{property.Key}\": \"{property.Value}\"");
			}
			//Pretty Print child classes
			foreach (var childClass in ChildClasses)
			{
				sb.AppendLine(childClass.PrintPretty(indent));
			}

			sb.Append(oldIndent + "}");
			return sb.ToString();
			 
		}

		public void Collapse(ref GenericVMFClass parentClass)
		{
			//Remove from vmf's classes
			parentClass.ChildClasses.Remove(this);

			//Add child classes
			parentClass.ChildClasses.AddRange(ChildClasses);
		}

		public GenericVMFClass[] this[string searchType] => FindClass(searchType).ToArray();
	}

	public class Solid
	{
		public int ID { get; }
		public List<Side> Sides { get; }
		public EditorInfo Editor { get; }

		public Solid(List<Side> sides, int id, EditorInfo editorInfo)
		{
			Sides = Sides;
			ID = id;
			Editor = editorInfo;
		}

		public Solid(GenericVMFClass genericVmfClass)
		{
			ID = System.Convert.ToInt32(genericVmfClass.Properties[0].Value);

			Sides = (from side in genericVmfClass.ChildClasses
					 where side.ClassName == "side"
					select new Side(side)).ToList();

			Editor = (from editorClass in genericVmfClass.ChildClasses
				where editorClass.ClassName == "editor"
				select new EditorInfo(editorClass)).First();
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder("ID " + ID + "\n");
			foreach (var side in Sides)
				sb.AppendLine(side.ToString());

			return sb.ToString();
		}

		//Returns a list of solid's vertices
		public List<Vector3> GetVertices()
		{
			List<Vector3> vertices = new List<Vector3>();

			//https://math.stackexchange.com/questions/1883835/get-list-of-vertices-from-list-of-planes
			for (int i = 0; i < Sides.Count - 2; i++)
			{
				for (int j = i + 1; j < Sides.Count - 1; j++)
				{
					for (int k = j + 1; k < Sides.Count; k++)
					{
						Vector3 a = Sides[i].SidePlane.Normal;
						Vector3 b = Sides[j].SidePlane.Normal;
						Vector3 c = Sides[k].SidePlane.Normal;

						//xi(yj * zk − zj * yk)+yi(zj * xk−xj * zk)+zi(xj * yk−yj * xk)
						double temporary =
							a.X * ((b.Y * c.Z) - (b.Z * c.Y))
							+ a.Y * ((b.Z * c.X) - (b.X * c.Z))
							+ a.Z * ((b.X * c.Y) - (b.Y * c.X));

						if (Math.Abs(temporary) <= 0.000001)
							continue;

						double aDist = Sides[i].SidePlane.Distance();
						double bDist = Sides[j].SidePlane.Distance();
						double cDist = Sides[k].SidePlane.Distance();

						//(di(zjyk−yjzk)+dj(yizk−ziyk)+dk(ziyj−yizj)) / t
						double x =
							(aDist * ((b.Z * c.Y) - (b.Y * c.Z))
							+ bDist * ((a.Y * c.Z) - (a.Z * c.Y))
							+ cDist * ((a.Z * b.Y) - (a.Y * b.Z)))
							/ temporary;

						//(di(xjzk−zjxk)+dj(zixk−xizk)+dk(xizj−zixj)) / t
						double y =
							(aDist * ((b.X * c.Z) - (b.Z * c.X))
							+ bDist * ((a.Z * c.X) - (a.X * c.Z))
							+ cDist * ((a.X * b.Z) - (a.Z * b.X)))
							/ temporary;

						//(di(yjxk−xjyk)+dj(xiyk−yixk)+dk(yixj−xiyj)) / t
						double z =
							(aDist * ((b.Y * c.X) - (b.X * c.Y))
							+ bDist * ((a.X * c.Y) - (a.Y * c.X))
							+ cDist * ((a.Y * b.X) - (a.X * b.Y)))
							/ temporary;

						//Test if p*n <= d
						int m;
						for (m = 0; m < Sides.Count; m++)
						{
							Vector3 mNorm = Sides[m].SidePlane.Normal;
							if (m != i && m != j && m != k && (x * mNorm.X + y * mNorm.Y + z * mNorm.Z) > Sides[m].SidePlane.Distance() + 0.00001)
							{
								m = -1;
								break;
							}
							
						}

						if (m >= 1)
							vertices.Add(new Vector3((float) x, (float) y, (float) z));
						
					}
				}
			}

			return vertices;
		}
	}

	public class Side
	{
		public int ID;
		public Plane SidePlane;
		public string Material;
		public UV UAxis;
		public UV VAxis;
		public float Rotation;
		public float LightmapScale;
		public int SmoothingGroup;
		//TODO support for dispInfo

		public Side(GenericVMFClass genericVmfClass)
		{
			ID = Convert.ToInt32(genericVmfClass.Properties[0].Value);
			SidePlane = (Plane) genericVmfClass.Properties[1].Value;
			Material = (string) genericVmfClass.Properties[2].Value;
			UAxis = (UV) genericVmfClass.Properties[3].Value;
			VAxis = (UV) genericVmfClass.Properties[4].Value;
			Rotation = Convert.ToSingle(genericVmfClass.Properties[5].Value);
			LightmapScale = Convert.ToSingle(genericVmfClass.Properties[6].Value);
			SmoothingGroup = Convert.ToInt32(genericVmfClass.Properties[7].Value);
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();

			sb.AppendLine("side\n{");
			sb.AppendLine($"\"id\" \"{ID}\"");
			sb.AppendLine($"\"plane\" \"{SidePlane}\"");
			sb.AppendLine($"\"uaxis\" \"{UAxis}\"");
			sb.AppendLine($"\"vaxis\" \"{VAxis}\"");
			sb.AppendLine($"\"rotation\" \"{Rotation}\"");
			sb.AppendLine($"\"lightmapscale\" \"{LightmapScale}\"");
			sb.AppendLine($"\"smoothing_groups\" \"{SmoothingGroup}\"");
			sb.AppendLine("}");

			return sb.ToString();
		}
	}

	public class EditorInfo
	{
		public Vector3 Color { get; }
		public int VisGroupID { get; }
		public int GroupID { get; }
		public bool VisGroupShown { get; }
		public bool VisGroupAutoShown { get; }
		public string Comments { get; }

		public EditorInfo(Vector3 color, int visGroupId, int groupId, bool visGroupShown, bool visGroupAutoShown, string comments)
		{
			Color = color;
			VisGroupID = visGroupId;
			GroupID = groupId;
			VisGroupShown = visGroupShown;
			VisGroupAutoShown = visGroupAutoShown;
			Comments = comments;
		}

		public EditorInfo(GenericVMFClass genericVmfClass)
		{
			
		}
	}
}