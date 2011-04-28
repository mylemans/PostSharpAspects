using System;

namespace Mylemans.PostSharp.Aspects.PostCompile
{
	///<summary>
	/// Throw this in a PostCompile aspect marked method to signal a success + optionally add more descriptions on the element
	///</summary>
	public sealed class PostCompileSuccessException : Exception
	{
		/// <summary>
		/// The extra descriptions to add (on hover)
		/// </summary>
		public readonly string[] Descriptions = new string[0];

		/// <summary>
		/// Construct new PostCompileSuccessException
		/// </summary>
		public PostCompileSuccessException()
		{

		}

		/// <summary>
		/// Construct new PostCompileSuccessException
		/// </summary>
		public PostCompileSuccessException(params string[] descriptions)
		{
			Descriptions = descriptions ?? new string[0];
		}
	}
}
