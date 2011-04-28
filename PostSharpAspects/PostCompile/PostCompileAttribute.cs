using System;
using System.Reflection;
using Mylemans.PostSharp.Utilities;
using PostSharp.Aspects;
using PostSharp.Aspects.Dependencies;

namespace Mylemans.PostSharp.Aspects.PostCompile
{
	///<summary>
	/// Marked method will be invoked on compilation. 
	/// 
	/// Marked method must not accept any arguments, return nothing, and be static. Can be used to generate data files @ compile-time, log, ...
	/// 
	/// The method could throw a PostCompileSuccessException with an array of strings. 
	/// 
	/// If you use PostSharpDescription, these strings will be added to the on-hover description.
	///</summary>
	[Serializable]
	[
		AttributeUsage(AttributeTargets.Method),
		ProvideAspectRole(AspectRoles.InvokesOnPostCompile),
		AspectRoleDependency(AspectDependencyAction.Conflict, AspectRoles.InvokesOnPostCompile)
	]
	public sealed class PostCompileAttribute : MethodLevelAspect
	{
		///<summary>
		/// Validate the aspect usage
		///</summary>
		///<param name="method">The method that the aspect is applied on</param>
		///<returns>Returns true if all checks pass</returns>
		public override bool CompileTimeValidate(MethodBase method)
		{
			if (method == null)
			{
				this.RaiseError(1, "The PostCompile aspect can only be applied on methods.", method.AsSignature());

				return false;
			}

			if (!method.IsStatic)
			{
				this.RaiseError(2, "The PostCompile aspect can only be applied on static methods.", method.AsSignature());

				return false;
			}

			if (method.GetParameters().Length > 0)
			{
				this.RaiseError(3, "The PostCompile aspect can only be applied on methods without arguments.", method.AsSignature());

				return false;
			}

			if (method as MethodInfo == null)
			{
				this.RaiseError(4, "The PostCompile aspect can not be applied on constructor/deconstructors", method.AsSignature());

				return false;
			}

			if (((MethodInfo)method).ReturnType != typeof(void))
			{
				this.RaiseError(5, "The PostCompile aspect can only be applied on methods returning nothing.", method.AsSignature());

				return false;
			}

			this.Describe("On compilation, this method will be invoked", method);

			try
			{
				method.Invoke(null, null);
				this.Describe("Last post-compile run succeeded at {0}".F(DateTime.Now), method);
			}
			catch (TargetInvocationException ex) // when this exception is thrown, consider it a success, and optionally check if it has more descriptions to add
			{
				if (ex.InnerException is PostCompileSuccessException)
				{
					this.Describe("Last post-compile run succeeded at {0}".F(DateTime.Now), method);

					foreach (var d in (ex.InnerException as PostCompileSuccessException).Descriptions)
					{
						this.Describe(d, method);
					}
				}
				else
				{
					this.Describe("Last post-compile run failed at {0}".F(DateTime.Now), method);
					this.Describe(ex.ToString(), method);
				}
			}
			catch (Exception ex)
			{
				this.Describe("Last post-compile run failed at {0}".F(DateTime.Now), method);
				this.Describe(ex.ToString(), method);
			}

			return true;
		}
	}
}
