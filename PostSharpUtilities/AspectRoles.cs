namespace Mylemans.PostSharp.Utilities
{
	/// <summary>
	/// Defines extra roles (in addition to the StandardRoles) that you can use
	/// </summary>
	public static class AspectRoles
	{
		/// <summary>
		/// This role tells that the aspect will provide an initial value on 'get'
		/// </summary>
		public const string ProvidesInitialValue = "ProvidesInitialValue";

		/// <summary>
		/// This role tells that the aspect will invoke the marked method on post-compile time
		/// </summary>
		public const string InvokesOnPostCompile = "InvokesOnPostCompile";
	}
}
