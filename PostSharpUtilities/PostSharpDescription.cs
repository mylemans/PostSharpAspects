using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using PostSharp.Aspects;
using PostSharp.Extensibility;
using PostSharp.Reflection;

namespace Mylemans.PostSharp.Utilities
{
    ///<summary>
    /// PostSharpDescription allows you to add descriptions for aspect-hooked methods / properties / field / types
    ///</summary>
    public static class PostSharpDescription
    {
        /// <summary>
        /// The starting symbol id that we'll use.
        /// Should be high enough to not collide with the core PostSharp symbol ids.
        /// </summary>
        private const int SYMBOL_ID = 1000000;

        /// <summary>
        /// Stores a list of actions to perform after compilation (once the _trigger triggers)
        /// </summary>
        private static readonly List<Action<XDocument, Dictionary<string, string>>> _actions = new List<Action<XDocument, Dictionary<string, string>>>();

        // ReSharper disable UnaccessedField.Local
        /// <summary>
        /// Will store a PostCompileTrigger, to trigger the description-generation when .NET is 'shutting down'
        /// </summary>
        private static readonly PostCompileTrigger _trigger;
        // ReSharper restore UnaccessedField.Local
        
        /// <summary>
        /// Create the PostCompileTrigger to trigger @ post compile
        /// </summary>
        static PostSharpDescription()
        {
            _trigger = new PostCompileTrigger { OnPostCompile = OnPostCompile };

            if (!_trigger.HasProperties)
            {
                Message.Write(SeverityType.Warning, "PostSharpDescription(1)",
                              "PostCompileTrigger was unable to extract one or more required properties. Did you update your PostSharp.targets/PostSharp.Custom.targets file? The added descriptions will not be available on-hover.");
            }
        }

        /// <summary>
        /// Add a new description entry for the specified target. The defined target must be a 'real' target
        /// of the aspect, meaning that when you target a 'method' from a TypeLevelInstance, the description will be written,
        /// but won't be shown!
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="target">The target method. Will show the added description on hover (after compilation).</param>
        public static void Add<TAspect>(string description, MethodBase target) where TAspect : IAspect
        {
            _actions.Add((xdoc, mappings) => AddDescription<TAspect>(xdoc, mappings, description, target));
        }

        /// <summary>
        /// Add a new description entry for the specified target method (getter/setter) of the specified property. 
        /// The defined target must be a 'real' target of the aspect, meaning that when you target a 'type' instead, 
        /// the description will be written, but won't be shown!
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="property">The property that the method is a getter/setter of.</param>
        /// <param name="target">The target method. Will show the added description on hover (after compilation).</param>
        public static void Add<TAspect>(string description, PropertyInfo property, MethodInfo target) where TAspect : IAspect
        {
            _actions.Add((xdoc, mappings) => AddDescription<TAspect>(xdoc, mappings, description, property, target));
        }

        /// <summary>
        /// Add a new description entry for the specified target. The defined target must be a 'real' target
        /// of the aspect, meaning that when you target a 'type' instead, the description will be written,
        /// but won't be shown!
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="target">The target type. Will show the added description on hover (after compilation).</param>
        public static void Add<TAspect>(string description, Type target) where TAspect : IAspect
        {
            _actions.Add((xdoc, mappings) => AddDescription<TAspect>(xdoc, mappings, description, target));
        }

        /// <summary>
        /// Add a new description entry for the specified target. The defined target must be a 'real' target
        /// of the aspect, meaning that when you target a 'type' instead, the description will be written,
        /// but won't be shown!
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="target">The target property. Will show the added description on hover (after compilation).</param>
        public static void Add<TAspect>(string description, PropertyInfo target) where TAspect : IAspect
        {
            _actions.Add((xdoc, mappings) => AddDescription<TAspect>(xdoc, mappings, description, target));
        }

        /// <summary>
        /// Add a new description entry for the specified target. The defined target must be a 'real' target
        /// of the aspect, meaning that when you target a 'type' instead, the description will be written,
        /// but won't be shown!
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="target">The target location (field / property). Will show the added description on hover (after compilation).</param>
        public static void Add<TAspect>(string description, LocationInfo target) where TAspect : IAspect
        {
            if (target.FieldInfo != null)
            {
                Add<TAspect>(description, target.FieldInfo);
            }
            else
            {
                Add<TAspect>(description, target.PropertyInfo);
            }
        }

        /// <summary>
        /// Add a new description entry for the specified target. The defined target must be a 'real' target
        /// of the aspect, meaning that when you target a 'type' instead, the description will be written,
        /// but won't be shown!
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="target">The target field. Will show the added description on hover (after compilation).</param>
        public static void Add<TAspect>(string description, FieldInfo target) where TAspect : IAspect
        {
            _actions.Add((xdoc, mappings) => AddDescription<TAspect>(xdoc, mappings, description, target));
        }
        
        /// <summary>
        /// The callback method, triggered once _trigger is disposed.
        /// When used @ aspect compile-time, it means that the .pssym file is generated!
        /// </summary>
        /// <param name="trigger">The trigger, not used for anything.</param>
        private static void OnPostCompile(PostCompileTrigger trigger)
        {
            // When no actions are queued, just return (no descriptions to add)
            if (_actions.Count == 0)
            {
                return;
            }

            // Calculate the path of the project' pssym file
            var pssymPath = Path.Combine(trigger.PathBin, trigger.AssemblyName + ".pssym");

            // declare the local doc variable
            XDocument document;
            try
            {
                // pssym wasn't found - just return in that case
                if (!File.Exists(pssymPath))
                {
					return;
                }

                // Attempt to load the pssym file (its XML)
                document = XDocument.Load(pssymPath);

                // Should not occur, but if we don't have a root, just return
                if (document.Root == null)
                {
                    return;
                }

                // Create a new dictionary, to store the pssym mappings / current count
                var storage = new Dictionary<string, string>();

                // Load the mappings (the core entries) from the pssym (if it fails, no new entries will be added)
                ExtractSymbols(document, storage);

                // Process all actions (each action is a description that should be added
                foreach (var a in _actions)
                {
                    try
                    {
                        a(document, storage);
                    }
                    catch
                    {
                        // Silently ignore exceptions
                    }
                }

                // Store the modified pssym
                document.Save(pssymPath);
            }
            catch
			{
				// Silently ignore exceptions
            }
        }

        /// <summary>
        /// Process the .pssym file, extract symbol mappings.
        /// 
        /// Example mapping: #30=T:Class would store #30 => T:Class
        /// </summary>
        /// <param name="document">Will extract the symbol mappings from this document</param>
        /// <param name="storage">The extracted symbols will be stored in this</param>
        private static void ExtractSymbols(XDocument document, Dictionary<string, string> storage)
        {
            if (document.Root == null) return;

            foreach (var el in document.Root.Descendants())
            {
                foreach (var a in el.Attributes())
                {
                    if (string.IsNullOrEmpty(a.Value))
                        continue;

                    var aValue = a.Value;

                    if (aValue.StartsWith("#") && a.Value.Contains("="))
                    {
                        storage[aValue.Substring(0, aValue.IndexOf('='))] = aValue.Substring(aValue.IndexOf('=') + 1);
                    }
                }
            }
        }

        /// <summary>
        /// Either extracts the symbol from the target, or fetches it from the storage
        /// </summary>
        private static string GetSymbol(string target, Dictionary<string, string> storage)
        {
            try
            {
                if (string.IsNullOrEmpty(target))
                {
                    return null;
                }

                if (!target.Contains("="))
                {
                    return storage[target];
                }

                return target.Substring(target.IndexOf('=') + 1);

            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Find the matching id in the storage
        /// </summary>
        private static string GetSymbolId(string target, Dictionary<string, string> storage)
        {
            try
            {
                return (from pair in storage
                        where pair.Value == target
                        select pair.Key).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Add a new description entry.
        /// </summary>
        private static void AddDescription<T>(XDocument document, Dictionary<string, string> storage, string description, MethodBase target)
        {
            if (document.Root == null) return;

            var aspectSymbol = "T:" + typeof(T).FullName;
			var methodSymbol = "M:" + target.AsSignature();
			var declaringTypeSymbol = "T:" + target.DeclaringType.AsSignature();

            // find the target 'Class' node
            var classNode = document.Root
                .Elements()
                .Where(el => el.Name.LocalName == "Class")
                .Where(el => el.Attribute("Class") != null)
                .Where(el => GetSymbol(el.Attribute("Class").Value, storage) == aspectSymbol)
                .FirstOrDefault();

            // No node found for the requested aspect, create a new one
            if (classNode == null)
            {
                classNode = new XElement(document.Root.Name.Namespace + "Class");

                // Add the 'Class' attribute
                classNode.Add(new XAttribute("Class", GetSymbolId(aspectSymbol, storage) ?? "#" + GenerateSymbolId(storage) + "=" + aspectSymbol));
                //classNode.Add(new XAttribute("ClassX", GenerateSymbolId(storage) + "=" + aspectSymbol));

                // Add to the root node
                document.Root.Add(classNode);
            }

            // Get the aspect symbol id (example: #1214)
            string aspectSymbolId = GetSymbolId(classNode.Attribute("Class").Value, storage) ??
                                    classNode.Attribute("Class").Value.Substring(0,
                                                                                 classNode.Attribute("Class").Value.IndexOf("="));

            // Get the method symbol id (example: #1214)
            string methodSymbolId = GetSymbolId(methodSymbol, storage);


            bool isTypeInstanceNode = true;

            // find the target 'Instance' node (first check for the declaring type)
            var instanceNode = classNode
                .Elements()
                .Where(el => el.Name.LocalName == "Instance")
                .Where(el => el.Attribute("Declaration") != null)
                .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage) == declaringTypeSymbol)
                .FirstOrDefault();

            // if not found, check if there is one for our method
            if (instanceNode == null)
            {
                instanceNode = classNode
                .Elements()
                .Where(el => el.Name.LocalName == "Instance")
                .Where(el => el.Attribute("Declaration") != null)
                .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage) == methodSymbol)
                .FirstOrDefault();

                isTypeInstanceNode = false;
            }

            // Maybe its part of a property?
            if (instanceNode == null)
            {
                instanceNode = classNode
                 .Elements()
                 .Where(el => el.Name.LocalName == "Instance")
                 .Where(el => el.Attribute("Declaration") != null)
                 .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage).StartsWith("P:"))
                 .Where(el => el.Elements().Any(e => e.Name.LocalName == "Target" && e.Attribute("Target") != null && GetSymbol(e.Attribute("Target").Value, storage) == methodSymbol))
                 .FirstOrDefault();

                if (instanceNode != null)
                {
                    isTypeInstanceNode = true;
                }
            }

            bool isPropertyMethod = false;

            // if not found, check if there is one for our method @ a JoinPoint (also if it refers to a property)
            if (instanceNode == null)
            {
                instanceNode = classNode
                 .Elements()
                 .Where(el => el.Name.LocalName == "Instance")
                 .Where(el => el.Attribute("Declaration") != null)
                 .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage).StartsWith("P:"))
                 .Where(el => el.Elements().Any(e => e.Name.LocalName == "Target" && e.Attribute("Target") == null &&
                     e.Elements().Any(ee => ee.Attribute("Advised") != null && GetSymbol(ee.Attribute("Advised").Value, storage) == methodSymbol)
                    ))
                 .FirstOrDefault();

                if (instanceNode != null)
                {
                    isPropertyMethod = true;
                }
            }

            // no node found for the method, create a new one (for this method)
            if (instanceNode == null)
            {
                instanceNode = new XElement(document.Root.Name.Namespace + "Instance");

                // Add the 'Declaration' attribute
                instanceNode.Add(new XAttribute("Declaration", GetSymbolId(methodSymbol, storage) ?? "#" + GenerateSymbolId(storage) + "=" + methodSymbol));

                // Add the 'Token' attribute
                instanceNode.Add(new XAttribute("Token", GenerateSymbolId(storage)));

                // Add to the class node
                classNode.Add(instanceNode);
            }

            // find the 'Target' node - first try to find a specific match (one that has a 'Target' attribute)
            var targetNode = instanceNode
                .Elements()
                .Where(el => el.Name.LocalName == "Target")
                .Where(el => el.Attribute("Target") != null)
                .Where(el => GetSymbol(el.Attribute("Target").Value, storage) == methodSymbol)
                .FirstOrDefault();

            // None found, try to find a 'Target' node that does not have a target, 
            // but ONLY if the instance node is not a 'type' node
            if (targetNode == null && !isTypeInstanceNode)
            {
                targetNode = instanceNode
                   .Elements()
                   .Where(el => el.Name.LocalName == "Target")
                   .Where(el => el.Attribute("Target") == null)
                   .FirstOrDefault();
            }

            // Still none found, create the node
            if (targetNode == null)
            {
                targetNode = new XElement(document.Root.Name.Namespace + "Target");

                // if its an 'type instance node', add a Target attribute
                if (isTypeInstanceNode)
                {
                    // Add the 'Declaration' attribute
                    targetNode.Add(new XAttribute("Target",
                                                  GetSymbolId(methodSymbol, storage) ??
                                                  "#" + GenerateSymbolId(storage) + "=" + methodSymbol));
                }
                // Add to the class node
                instanceNode.Add(targetNode);
            }

            // Create the JoinPoint node
            var joinPoint = new XElement(document.Root.Name.Namespace + "JoinPoint");

            if (isPropertyMethod)
            {
                // Add the 'Advised' attribute (points to our target)
                joinPoint.Add(new XAttribute("Advised", methodSymbolId));
            }
            else
            {
                if (target.Name.StartsWith("get_") || target.Name.StartsWith("set_"))
                {
                    isPropertyMethod = true;
                }
                else
                {
                    // Add the 'Advising' attribute (points to our aspect)
                    joinPoint.Add(new XAttribute("Advising", aspectSymbolId));
                }
            }

            // Add the 'Description' attribute with the message to be shown
            joinPoint.Add(new XAttribute("Description", "#" + GenerateSymbolId(storage) + "=" + description));

            // add Semantic
            if (isPropertyMethod)
            {
                // Add the 'Advised' attribute (points to our target)
                joinPoint.Add(new XAttribute("Semantic", target.Name.StartsWith("get_") ? "Getter" : "Setter"));

                var oe = targetNode.Elements().FirstOrDefault(e => e.Attribute("Ordinal") != null);
                if (oe != null)
                {
                    joinPoint.Add(new XAttribute("Ordinal", oe.Attribute("Ordinal").Value));
                }
            }

            // Add it to the Target node
            targetNode.Add(joinPoint);
        }

        /// <summary>
        /// Add a new description entry.
        /// </summary>
        private static void AddDescription<T>(XDocument document, Dictionary<string, string> storage, string description, PropertyInfo property, MethodInfo target)
        {
            if (document.Root == null) return;

            var aspectSymbol = "T:" + typeof(T).FullName;
			var methodSymbol = "M:" + target.AsSignature();
            // var declaringTypeSymbol = "T:" + target.DeclaringType.AsSignature();
			var propSymbol = "P:" + property.DeclaringType.AsSignature() + "::" + property.Name + "()";


            // find the target 'Class' node
            var classNode = document.Root
                .Elements()
                .Where(el => el.Name.LocalName == "Class")
                .Where(el => el.Attribute("Class") != null)
                .Where(el => GetSymbol(el.Attribute("Class").Value, storage) == aspectSymbol)
                .FirstOrDefault();

            // No node found for the requested aspect, create a new one
            if (classNode == null)
            {
                classNode = new XElement(document.Root.Name.Namespace + "Class");

                // Add the 'Class' attribute
                classNode.Add(new XAttribute("Class", GetSymbolId(aspectSymbol, storage) ?? "#" + GenerateSymbolId(storage) + "=" + aspectSymbol));
                
                // Add to the root node
                document.Root.Add(classNode);
            }

            // Get the aspect symbol id (example: #1214)
// ReSharper disable PossibleNullReferenceException
            string aspectSymbolId = GetSymbolId(classNode.Attribute("Class").Value, storage) ??
                                    classNode.Attribute("Class").Value.Substring(0,
                                                                                 classNode.Attribute("Class").Value.IndexOf("="));

// ReSharper restore PossibleNullReferenceException

            // Get the method symbol id (example: #1214)
            string methodSymbolId = GetSymbolId(methodSymbol, storage);


            // bool isTypeInstanceNode;

            // find the target 'Instance' node (first check for the declaring type)
            var instanceNode = classNode
                .Elements()
                .Where(el => el.Name.LocalName == "Instance")
                .Where(el => el.Attribute("Declaration") != null)
                .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage) == propSymbol)
                .FirstOrDefault();

            bool isPropertyMethod = false;

            // ReSharper disable PossibleNullReferenceException
            // if not found, check if there is one for our method
            if (instanceNode == null)
            {
                instanceNode = classNode
                .Elements()
                .Where(el => el.Name.LocalName == "Instance")
                .Where(el => el.Attribute("Declaration") != null)
                .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage) == methodSymbol)
                .FirstOrDefault();

                //isTypeInstanceNode = false;
            }else
            {
                isPropertyMethod = true;
            }
            // ReSharper restore PossibleNullReferenceException

            // Maybe its part of a property?
            if (instanceNode == null)
            {
                instanceNode = classNode
                 .Elements()
                 .Where(el => el.Name.LocalName == "Instance")
                 .Where(el => el.Attribute("Declaration") != null)
                 .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage).StartsWith("P:"))
                 .Where(el => el.Elements().Any(e => e.Name.LocalName == "Target" && e.Attribute("Target") != null && GetSymbol(e.Attribute("Target").Value, storage) == methodSymbol))
                 .FirstOrDefault();

                if (instanceNode != null)
                {
                    //isTypeInstanceNode = true;
                }
            }


            // if not found, check if there is one for our method @ a JoinPoint (also if it refers to a property)
            if (instanceNode == null)
            {
                instanceNode = classNode
                 .Elements()
                 .Where(el => el.Name.LocalName == "Instance")
                 .Where(el => el.Attribute("Declaration") != null)
                 .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage).StartsWith("P:"))
                 .Where(el => el.Elements().Any(e => e.Name.LocalName == "Target" && e.Attribute("Target") == null &&
                     e.Elements().Any(ee => ee.Attribute("Advised") != null && GetSymbol(ee.Attribute("Advised").Value, storage) == methodSymbol)
                    ))
                 .FirstOrDefault();

                if (instanceNode != null)
                {
                    isPropertyMethod = true;
                }
            }

            // no node found for the method, create a new one (for this method)
            if (instanceNode == null)
            {
                instanceNode = new XElement(document.Root.Name.Namespace + "Instance");

                // Add the 'Declaration' attribute
                instanceNode.Add(new XAttribute("Declaration", GetSymbolId(methodSymbol, storage) ?? "#" + GenerateSymbolId(storage) + "=" + methodSymbol));

                // Add the 'Token' attribute
                instanceNode.Add(new XAttribute("Token", GenerateSymbolId(storage)));

                // Add to the class node
                classNode.Add(instanceNode);
            }

            // find the 'Target' node (the one without a 'Target' attribute)
            var targetNode = instanceNode
                .Elements()
                .Where(el => el.Name.LocalName == "Target")
                .Where(el => el.Attribute("Target") == null)
                .FirstOrDefault();

            // Still none found, create the node
            if (targetNode == null)
            {
                targetNode = new XElement(document.Root.Name.Namespace + "Target");

                // Add to the class node
                instanceNode.Add(targetNode);
            }

            // Create the JoinPoint node
            var joinPoint = new XElement(document.Root.Name.Namespace + "JoinPoint");

            if (isPropertyMethod)
            {
                // Add the 'Advised' attribute (points to our target)
                joinPoint.Add(new XAttribute("Advised", methodSymbolId));
            }
            else
            {
                // Add the 'Advising' attribute (points to our aspect)
                joinPoint.Add(new XAttribute("Advising", aspectSymbolId));
            }

            // Add the 'Description' attribute with the message to be shown
            joinPoint.Add(new XAttribute("Description", "#" + GenerateSymbolId(storage) + "=" + description));

            // add Semantic
            if (isPropertyMethod)
            {
                // Add the 'Advised' attribute (points to our target)
                joinPoint.Add(new XAttribute("Semantic", target.Name.StartsWith("get_") ? "Getter" : "Setter"));
            }

            // Add it to the Target node
            targetNode.Add(joinPoint);
        }

        /// <summary>
        /// Add a new description entry.
        /// </summary>
        private static void AddDescription<T>(XDocument document, Dictionary<string, string> storage, string description, Type target)
        {
            if (document.Root == null) return;

            var aspectSymbol = "T:" + typeof(T).FullName;
			var targetSymbol = "T:" + target.AsSignature();

            // find the target 'Class' node
            var classNode = document.Root
                .Elements()
                .Where(el => el.Name.LocalName == "Class")
                .Where(el => el.Attribute("Class") != null)
                .Where(el => GetSymbol(el.Attribute("Class").Value, storage) == aspectSymbol)
                .FirstOrDefault();

            // No node found for the requested aspect, create a new one
            if (classNode == null)
            {
                classNode = new XElement(document.Root.Name.Namespace + "Class");

                // Add the 'Class' attribute
                classNode.Add(new XAttribute("Class", GetSymbolId(aspectSymbol, storage) ?? "#" + GenerateSymbolId(storage) + "=" + aspectSymbol));
                //classNode.Add(new XAttribute("ClassX", GenerateSymbolId(storage) + "=" + aspectSymbol));

                // Add to the root node
                document.Root.Add(classNode);
            }

            // find the target 'Instance' node
            var instanceNode = classNode
                .Elements()
                .Where(el => el.Name.LocalName == "Instance")
                .Where(el => el.Attribute("Declaration") != null)
                .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage) == targetSymbol)
                .FirstOrDefault();

            // no node found for the target, create a new one
            if (instanceNode == null)
            {
                instanceNode = new XElement(document.Root.Name.Namespace + "Instance");

                // Add the 'Declaration' attribute
                instanceNode.Add(new XAttribute("Declaration", GetSymbolId(targetSymbol, storage) ?? "#" + GenerateSymbolId(storage) + "=" + targetSymbol));

                // Add the 'Token' attribute
                instanceNode.Add(new XAttribute("Token", GenerateSymbolId(storage))); // REVIEW Is the token needed?

                // Add to the class node
                classNode.Add(instanceNode);
            }

            // find the empty 'Target' node
            var targetNode = instanceNode
                    .Elements()
                    .Where(el => el.Name.LocalName == "Target")
                    .Where(el => el.Attribute("Target") == null)
                    .FirstOrDefault();

            // none found, create the node
            if (targetNode == null)
            {
                targetNode = new XElement(document.Root.Name.Namespace + "Target");

                // Add to the class node
                instanceNode.Add(targetNode);
            }

            // Create the JoinPoint node
            var joinPoint = new XElement(document.Root.Name.Namespace + "JoinPoint");

            // Add the 'Description' attribute with the message to be shown
            joinPoint.Add(new XAttribute("Description", "#" + GenerateSymbolId(storage) + "=" + description));
            //joinPoint.Add(new XAttribute("Description", "#" + description.GetHashCode() + "=" + description));

            // Add it to the Target node
            targetNode.Add(joinPoint);
        }

        /// <summary>
        /// Add a new description entry.
        /// </summary>
        private static void AddDescription<T>(XDocument document, Dictionary<string, string> storage, string description, PropertyInfo target)
        {
            if (document.Root == null) return;

            var aspectSymbol = "T:" + typeof(T).FullName;
            var targetSymbol = "P:" + target.AsSignature();

            // find the target 'Class' node
            var classNode = document.Root
                .Elements()
                .Where(el => el.Name.LocalName == "Class")
                .Where(el => el.Attribute("Class") != null)
                .Where(el => GetSymbol(el.Attribute("Class").Value, storage) == aspectSymbol)
                .FirstOrDefault();

            // No node found for the requested aspect, create a new one
            if (classNode == null)
            {
                classNode = new XElement(document.Root.Name.Namespace + "Class");

                // Add the 'Class' attribute
                classNode.Add(new XAttribute("Class", GetSymbolId(aspectSymbol, storage) ?? "#" + GenerateSymbolId(storage) + "=" + aspectSymbol));
                //classNode.Add(new XAttribute("ClassX", GenerateSymbolId(storage) + "=" + aspectSymbol));

                // Add to the root node
                document.Root.Add(classNode);
            }

            // find the target 'Instance' node
            var instanceNode = classNode
                .Elements()
                .Where(el => el.Name.LocalName == "Instance")
                .Where(el => el.Attribute("Declaration") != null)
                .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage) == targetSymbol)
                .FirstOrDefault();

            // no node found for the target, create a new one
            if (instanceNode == null)
            {
                instanceNode = new XElement(document.Root.Name.Namespace + "Instance");

                // Add the 'Declaration' attribute
                instanceNode.Add(new XAttribute("Declaration", GetSymbolId(targetSymbol, storage) ?? "#" + GenerateSymbolId(storage) + "=" + targetSymbol));

                // Add the 'Token' attribute
                instanceNode.Add(new XAttribute("Token", GenerateSymbolId(storage))); // REVIEW Is the token needed?

                // Add to the class node
                classNode.Add(instanceNode);
            }

            // find the empty 'Target' node
            var targetNode = instanceNode
                    .Elements()
                    .Where(el => el.Name.LocalName == "Target")
                    .Where(el => el.Attribute("Target") == null)
                    .FirstOrDefault();

            // none found, create the node
            if (targetNode == null)
            {
                targetNode = new XElement(document.Root.Name.Namespace + "Target");

                // Add to the class node
                instanceNode.Add(targetNode);
            }

            // Create the JoinPoint node
            var joinPoint = new XElement(document.Root.Name.Namespace + "JoinPoint");

            // Add the 'Description' attribute with the message to be shown
            joinPoint.Add(new XAttribute("Description", "#" + GenerateSymbolId(storage) + "=" + description));

            // Add it to the Target node
            targetNode.Add(joinPoint);
        }

        /// <summary>
        /// Add a new description entry.
        /// </summary>
        private static void AddDescription<T>(XDocument document, Dictionary<string, string> storage, string description, FieldInfo target)
        {
            if (document.Root == null) return;

            var aspectSymbol = "T:" + typeof(T).FullName;
            var targetSymbol = "F:" + target.AsSignature();

            // find the target 'Class' node
            var classNode = document.Root
                .Elements()
                .Where(el => el.Name.LocalName == "Class")
                .Where(el => el.Attribute("Class") != null)
                .Where(el => GetSymbol(el.Attribute("Class").Value, storage) == aspectSymbol)
                .FirstOrDefault();
               
            // No node found for the requested aspect, create a new one
            if (classNode == null)
            {
                classNode = new XElement(document.Root.Name.Namespace + "Class");

                // Add the 'Class' attribute
                classNode.Add(new XAttribute("Class", GetSymbolId(aspectSymbol, storage) ?? "#" + GenerateSymbolId(storage) + "=" + aspectSymbol));
                //classNode.Add(new XAttribute("ClassX", GenerateSymbolId(storage) + "=" + aspectSymbol));

                // Add to the root node
                document.Root.Add(classNode);
            }

            // find the target 'Instance' node
            var instanceNode = classNode
                .Elements()
                .Where(el => el.Name.LocalName == "Instance")
                .Where(el => el.Attribute("Declaration") != null)
                .Where(el => GetSymbol(el.Attribute("Declaration").Value, storage) == targetSymbol)
                .FirstOrDefault();

            // no node found for the target, create a new one
            if (instanceNode == null)
            {
                instanceNode = new XElement(document.Root.Name.Namespace + "Instance");

                // Add the 'Declaration' attribute
                instanceNode.Add(new XAttribute("Declaration", GetSymbolId(targetSymbol, storage) ?? "#" + GenerateSymbolId(storage) + "=" + targetSymbol));

                // Add the 'Token' attribute
                instanceNode.Add(new XAttribute("Token", GenerateSymbolId(storage))); // REVIEW Is the token needed?

                // Add to the class node
                classNode.Add(instanceNode);
            }

            // find the empty 'Target' node
            var targetNode = instanceNode
                    .Elements()
                    .Where(el => el.Name.LocalName == "Target")
                    .Where(el => el.Attribute("Target") == null)
                    .FirstOrDefault();

            // none found, create the node
            if (targetNode == null)
            {
                targetNode = new XElement(document.Root.Name.Namespace + "Target");

                // Add to the class node
                instanceNode.Add(targetNode);
            }

            // Create the JoinPoint node
            var joinPoint = new XElement(document.Root.Name.Namespace + "JoinPoint");

            // Add the 'Description' attribute with the message to be shown
            joinPoint.Add(new XAttribute("Description", "#" + GenerateSymbolId(storage) + "=" + description));

            // Add it to the Target node
            targetNode.Add(joinPoint);
        }

        /// <summary>
        /// Generate a new id to store @ the pssym.
        /// 
        /// Make sure that SYMBOL_ID is high enough, so it does not collide with the PostSharp generated ids.
        /// </summary>
        private static string GenerateSymbolId(Dictionary<string, string> storage)
        {
            var value = storage.ContainsKey("SYMBOL_ID") ? int.Parse(storage["SYMBOL_ID"]) : SYMBOL_ID;

            value++;

            storage["SYMBOL_ID"] = value.ToString();

            return value.ToString();
        }

        #region Extension methods for IAspect (use this. to access them)
        /// <summary>
        /// Add a new description entry for the specified target.
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="aspect">The aspect to add a description for</param>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="target">The target method. Will show the added description on hover (after compilation).</param>
        public static void Describe<TAspect>(this TAspect aspect, string description, MethodBase target) where TAspect : IAspect
        {
            _actions.Add((xdoc, mappings) => AddDescription<TAspect>(xdoc, mappings, description, target));
        }
        /// <summary>
        /// Add a new description entry for the specified target. 
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="aspect">The aspect to add a description for</param>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="target">The target type. Will show the added description on hover (after compilation).</param>
        public static void Describe<TAspect>(this TAspect aspect, string description, Type target) where TAspect : IAspect
        {
            _actions.Add((xdoc, mappings) => AddDescription<TAspect>(xdoc, mappings, description, target));
        }

        /// <summary>
        /// Add a new description entry for the specified target.
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="aspect">The aspect to add a description for</param>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="target">The target location. Will show the added description on hover (after compilation). Will use the FieldInfo if not null, otherwise uses PropertyInfo</param>
        public static void Describe<TAspect>(this TAspect aspect, string description, LocationInfo target) where TAspect : IAspect
        {
            if (target.FieldInfo != null)
            {
                Add<TAspect>(description, target.FieldInfo);
            }
            else
            {
                Add<TAspect>(description, target.PropertyInfo);
            }
        }

        /// <summary>
        /// Add a new description entry for the specified target.
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="aspect">The aspect to add a description for</param>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="target">The target property. Will show the added description on hover (after compilation).</param>
        public static void Describe<TAspect>(this TAspect aspect, string description, PropertyInfo target) where TAspect : IAspect
        {
            _actions.Add((xdoc, mappings) => AddDescription<TAspect>(xdoc, mappings, description, target));
        }

        /// <summary>
        /// Add a new description entry for the specified target method (getter/setter) of the specified property. 
        /// The defined target must be a 'real' target of the aspect, meaning that when you target a 'type' instead, 
        /// the description will be written, but won't be shown!
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="aspect">The aspect to add a description for</param>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="property">The property that the method is a getter/setter of.</param>
        /// <param name="target">The target method. Will show the added description on hover (after compilation).</param>
        public static void Describe<TAspect>(this TAspect aspect, string description, PropertyInfo property, MethodInfo target) where TAspect : IAspect
        {
            _actions.Add((xdoc, mappings) => AddDescription<TAspect>(xdoc, mappings, description, property, target));
        }

        /// <summary>
        /// Add a new description entry for the specified target.
        /// </summary>
        /// <typeparam name="TAspect">The aspect that you want to add a description entry for.</typeparam>
        /// <param name="aspect">The aspect to add a description for</param>
        /// <param name="description">The message to add. PostSharp will always add a . (dot) at the end.</param>
        /// <param name="target">The target field. Will show the added description on hover (after compilation).</param>
        public static void Describe<TAspect>(this TAspect aspect, string description, FieldInfo target) where TAspect : IAspect
        {
            _actions.Add((xdoc, mappings) => AddDescription<TAspect>(xdoc, mappings, description, target));
        }
        #endregion
    }
}
