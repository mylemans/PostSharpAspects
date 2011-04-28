using System;
using System.IO;
using PostSharp.Extensibility;

namespace Mylemans.PostSharp.Utilities
{
    /// <summary>
    /// Triggers an event when it is disposed. Used to detect 'post compile'. Store it in a static field!
    /// 
    /// Only create an instance of this @ compile time, and not when not disposing. 
    /// The paths are calculated the moment the object is generated, and will fail if calculated when .NET is shutting down.
    /// </summary>
    public class PostCompileTrigger
    {
        /// <summary>
        /// Attach an action on this, to detect when the instance is disposed.
        /// </summary>
        public Action<PostCompileTrigger> OnPostCompile;

        /// <summary>
        /// The full path to the project' root folder, or null when it could not be retrieved.
        /// </summary>
        public string PathProject { get; private set; }

        /// <summary>
        /// The full path to the current compiling project' obj folder, or null when it could not be retrieved
        /// </summary>
        public string PathObj { get; private set; }

        /// <summary>
        /// The full path to the current compiling project' bin (target output) folder, or null when it could not be retrieved
        /// 
        /// Needs a patched PostSharp.targets (to add OutputPath to the PostSharpProperties)
        /// </summary>
        public string PathBin { get; private set; }

        /// <summary>
        /// The assembly name of the current compiling project, or null when it could not be retrieved.
        /// 
        /// Needs a patched PostSharp.targets (to add TargetName to the PostSharpProperties)
        /// </summary>
        public string AssemblyName { get; private set; }

        /// <summary>
        /// Can optionally store a tag.
        /// </summary>
        public object Tag { get; set; }

        /// <summary>
        /// Create a new PostCompileTrigger instance, and calculate all paths
        /// </summary>
        public PostCompileTrigger()
        {
            PathProject = InnerPathProject;
            AssemblyName = InnerAssemblyName;
            PathBin = Path.Combine(PathProject, InnerPathBin);
            PathObj = InnerPathOutput;
        }

        /// <summary>
        /// Returns true if it could get all required properties
        /// </summary>
        public bool HasProperties
        {
            get
            {
                return !string.IsNullOrEmpty(PathProject)
                       && !string.IsNullOrEmpty(AssemblyName)
                       && !string.IsNullOrEmpty(PathBin)
                       && !string.IsNullOrEmpty(PathObj);
            }
        }

        /// <summary>
        /// Get the project path
        /// </summary>
        private static string InnerPathProject
        {
            get
            {
                try
                {
                    var fp = new FileInfo(PostSharpEnvironment.Current.CurrentProject.EvaluateExpression("{$MSBuildProjectFullPath}"));

                    return fp.DirectoryName;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Get the output path (obj folder)
        /// </summary>
        private static string InnerPathOutput
        {
            get
            {
                try
                {
                    var fp = new FileInfo(PostSharpEnvironment.Current.CurrentProject.EvaluateExpression("{$Output}"));

                    return fp.DirectoryName;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Get the project assembly name
        /// </summary>
        private static string InnerAssemblyName
        {
            get
            {
                try
                {
                    return PostSharpEnvironment.Current.CurrentProject.EvaluateExpression("{$TargetName}");
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Get the project bin folder
        /// </summary>
        private static string InnerPathBin
        {
            get
            {
                try
                {
                    return PostSharpEnvironment.Current.CurrentProject.EvaluateExpression("{$OutputPath}");
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// On destruct, trigger the OnPostCompile event
        /// </summary>
        ~PostCompileTrigger()
        {
            try
            {
                if (OnPostCompile != null)
                {
                    OnPostCompile(this);
                }
            }
            catch
            {

            }
        }
    }
}
