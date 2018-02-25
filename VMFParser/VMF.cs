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
    class VMF
    {
        private StreamReader sr;
        private string filePath;
        //public List<GenericVMFClass> Classes { get; private set; }
        public List<GenericVMFClass> Classes;


        public VMF(string _filePath, bool ignoreHiddenClass = false)
        {
            if (!File.Exists(_filePath))
            {
                Console.WriteLine("Could not find file");
                return;
            }

            sr = new StreamReader(_filePath);
            filePath = _filePath;

            ReadVMF(ignoreHiddenClass);

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
                var hiddenClass = (from hClass in worldClass.ChildClasses
                    where hClass.ClassName == "hidden"
                    select hClass).First();
                
                //Remove hidden class and properties
                Classes.Remove(worldClass);
                int worldPos = Classes.IndexOf(worldClass);

                worldClass.ChildClasses.Remove(hiddenClass);

                //Re add world class Back
                Classes.Insert(worldPos, worldClass);
                //Classes.Add(worldClass);
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

                if (value.Length == 4)
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
                Debug.WriteLine("Couldn't parse value, defaulting to 0");
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
            {
                classes.AddRange(vmfClass.FindClass(className));
            }

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
    }

    public abstract class VMFClass
    {
        public abstract void Convert(GenericVMFClass vmfClass);
    }
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
            StringBuilder sb = new StringBuilder(indent + ClassName);
            sb.AppendLine(indent + ClassName);
            sb.AppendLine(indent + "{");

            string oldIndent = indent;
            indent += "   ";

            foreach (var property in Properties)
            {
                //Handle special cases
                if (property.Value is Vector3)
                {
                    //Print out vectors with () instead of <>
                    Vector3 vec3 = (Vector3) property.Value;

                    sb.AppendLine($"{indent}\"{property.Key}\": \"({vec3.X} {vec3.Y} {vec3.Z})\"");

                } else if (property.Value is Vector2)
                {
                    //Print out vectors with () instead of <>
                    Vector2 vec2 = (Vector2) property.Value;

                    sb.AppendLine($"{indent}\"{property.Key}\": \"[{vec2.X} {vec2.Y}]\"");
                }

                sb.AppendLine($"{indent}\"{property.Key}\": \"{property.Value}\"");
            }
            //Pretty Print child classes
            foreach (var childClass in ChildClasses)
            {
                sb.AppendLine(childClass.PrintPretty(indent));
            }

            sb.AppendLine(oldIndent + "}");
            return sb.ToString();
             
        }
    }

    public class Solid : VMFClass
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

            Editor = (from eClass in genericVmfClass.ChildClasses
                where eClass.ClassName == "editor"
                select new EditorInfo(eClass)).First();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("ID " + ID);
            foreach (var side in Sides)
                sb.AppendLine(side.ToString());

            return sb.ToString();
        }

        public override void Convert(GenericVMFClass vmfClass)
        {
            throw new NotImplementedException();
        }
    }

    public class Side : VMFClass
    {
        public int? ID;
        public Plane SidePlane;
        public string Material;

        public Side(GenericVMFClass genericVmfClass)
        {
            ID = genericVmfClass.Properties[0].Value as int?;
            SidePlane = (Plane)genericVmfClass.Properties[1].Value;
            Material = (string)genericVmfClass.Properties[2].Value;
        }

        public override string ToString()
        {
            return $"ID: {ID} \nPlane: {SidePlane} \nMaterial: {Material}";
        }

        public override void Convert(GenericVMFClass vmfClass)
        {
            throw new NotImplementedException();
        }
    }

    public class EditorInfo : VMFClass
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

        public override void Convert(GenericVMFClass vmfClass)
        {
            throw new NotImplementedException();
        }
    }
}