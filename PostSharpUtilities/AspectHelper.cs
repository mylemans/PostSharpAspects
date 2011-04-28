using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PostSharp.Aspects;
using PostSharp.Extensibility;
using PostSharp.Reflection;

namespace Mylemans.PostSharp.Utilities
{
	/// <summary>
	/// Helper class, containing goodies that can be used by an aspect
	/// </summary>
	public static class AspectHelper
	{
		/// <summary>
		/// Loops over the IEnumerable e, running fn per-item
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="e"></param>
		/// <param name="fn"></param>
		public static void Do<T>(this IEnumerable<T> e, Action<T> fn)
		{
			foreach (var ee in e)
				fn(ee);
		}

		/// <summary>
		/// Helper extension method, wrapping around string.Format
		/// </summary>
		/// <param name="unformattedString">The unformated, parameterized string</param>
		/// <param name="arguments">The arguments used to fill in the parameters</param>
		/// <returns>The formatted string</returns>
		public static string F(this string unformattedString, params object[] arguments)
		{
			return string.Format(unformattedString, arguments);
		}

		///<summary>
		/// Returns a text representation of the specified target
		///</summary>
		///<param name="target">The LocationInfo to get a text representation for</param>
		///<returns>Format: Namespace.DeclaringType::FieldPropertyName</returns>
		public static string ReflectedPath(this LocationInfo target)
		{
			return "{0}::{1}".F(target.DeclaringType.FullName, target.PropertyInfo != null ? target.PropertyInfo.Name : target.FieldInfo.Name);
		}

		///<summary>
		/// Returns a text representation of the specified target
		///</summary>
		/// <param name="target">The Methodbase to get a text representation for</param>
		/// <returns>Format: Namespace.DeclaringType::Method()</returns>
		public static string ReflectedPath(this MethodBase target)
		{
			return "{0}::{1}()".F(target.DeclaringType.FullName, target.Name);
		}

		///<summary>
		/// Returns a text representation of the specified target
		///</summary>
		/// <param name="target">The Methodbase to get a text representation for</param>
		///<param name="addParentheses">When true, it'll add () as well, otherwise it won't add it</param>
		///<returns>Format: Namespace.DeclaringType::Method()</returns>
		public static string ReflectedPath(this MethodBase target, bool addParentheses)
		{
			return "{0}::{1}{2}".F(target.DeclaringType.FullName, target.Name, addParentheses ? "()" : "");
		}

		///<summary>
		/// Returns a text representation of the specified target
		///</summary>
		/// <param name="target">The Type to get a text representation for</param>
		/// <returns>Format: Namespace.DeclaringType</returns>
		public static string ReflectedPath(this Type target)
		{
			return target.FullName;
		}

		/// <summary>
		/// Will attempt to get the project' PostSharp property, returns null if not set (or empty)
		/// </summary>
		/// <param name="propertyName">The PostSharp property to get</param>
		/// <returns>The stored value, or null if not found or empty</returns>
		public static string GetPostSharpProperty(string propertyName)
		{
			var property = PostSharpEnvironment.Current.CurrentProject.EvaluateExpression("{$" + propertyName + "}");

			return string.IsNullOrEmpty(property) ? null : property;
		}

		/// <summary>
		/// Will attempt to get the project' PostSharp property, and convert it into a boolean. 
		/// 
		/// Returns true if it set and is either 'true' (case insensitive) or '1', false otherwise, or defaultValue when the property is not set
		/// </summary>
		/// <param name="propertyName">The PostSharp property to get</param>
		/// <param name="defaultValue">When not set, this will be returned instead</param>
		/// <returns>true if it set and is either 'true' (case insensitive) or '1', false otherwise, or defaultValue when the property is not set</returns>
		public static bool GetPostSharpProperty(string propertyName, bool defaultValue)
		{
			var property = PostSharpEnvironment.Current.CurrentProject.EvaluateExpression("{$" + propertyName + "}");

			return string.IsNullOrEmpty(property) ? defaultValue : (propertyName.ToLower() == "true" || propertyName == "1");
		}

		/// <summary>
		/// Adds a new Error message.
		/// </summary>
		/// <param name="aspect">The aspect writing the Error message</param>
		/// <param name="code">The 'code' of this specific message</param>
		/// <param name="message">The message</param>
		/// <param name="location">Where was the aspect applied on, can be null</param>
		/// <returns>Always returns false</returns>
		public static bool RaiseError(this IAspect aspect, int code, string message, string location)
		{
			if (aspect == null || message == null)
			{
				return false;
			}

			Message.Write(SeverityType.Error, aspect.GetType().Name + "[" + code + "]",
						  location != null ? "[{0}] {1}".F(location, message) : "{0}".F(message));

			return false;
		}

		/// <summary>
		/// Adds a new Error message.
		/// </summary>
		/// <param name="aspect">The aspect writing the Error message</param>
		/// <param name="code">The 'code' of this specific message</param>
		/// <param name="message">The message</param>
		/// <param name="location">The location where the aspect applies to. Will show full signature.</param>
		/// <returns>Always returns false</returns>
		public static bool RaiseError(this IAspect aspect, int code, string message, MethodBase location)
		{
			if (aspect == null || message == null)
			{
				return false;
			}

			Message.Write(SeverityType.Error, aspect.GetType().Name + "[" + code + "]",
						  location != null ? "[{0}] {1}".F(location.AsSignature(true), message) : "{0}".F(message));

			return false;
		}

		/// <summary>
		/// Adds a new Warning message.
		/// </summary>
		/// <param name="aspect">The aspect writing the Warning message</param>
		/// <param name="code">The 'code' of this specific message</param>
		/// <param name="message">The message</param>
		/// <param name="location">Where was the aspect applied on, can be null</param>
		public static void RaiseWarning(this IAspect aspect, int code, string message, string location)
		{
			if (aspect == null || message == null)
			{
				return;
			}

			Message.Write(SeverityType.Warning, aspect.GetType().Name + "[" + code + "]",
						  location != null ? "[{0}] {1}".F(location, message) : "{0}".F(message));
		}

		/// <summary>
		/// Adds a new Warning message.
		/// </summary>
		/// <param name="aspect">The aspect writing the Warning message</param>
		/// <param name="code">The 'code' of this specific message</param>
		/// <param name="message">The message</param>
		/// <param name="location">The location where the aspect applies to. Will show full signature.</param>
		public static void RaiseWarning(this IAspect aspect, int code, string message, MethodBase location)
		{
			if (aspect == null || message == null)
			{
				return;
			}

			Message.Write(SeverityType.Warning, aspect.GetType().Name + "[" + code + "]",
						  location != null ? "[{0}] {1}".F(location.AsSignature(true), message) : "{0}".F(message));
		}
		/// <summary>
		/// Helper to check if 'declaringType' has 'genericParameterType'
		/// </summary>
		private static bool HasGeneric(Type declaringType, Type genericParameterType)
		{
			try
			{
				if (declaringType.GetGenericArguments().Length > 0)
				{
					foreach (var a in declaringType.GetGenericArguments())
					{
						if (a.Name == genericParameterType.Name)
							return true;
					}
				}
			}
			catch
			{
				// Silently ignored
			}
			return false;
		}
		/// <summary>
		/// Returns a PostSharp compatible type reference representation
		/// </summary>
		public static string AsSignature(this Type target)
		{
			return target.AsSignature(true);
		}

		/// <summary>
		/// Returns a PostSharp compatible type reference representation
		/// </summary>
		public static string AsSignature(this Type target, bool fullSignature)
		{
			string signature = target.FullName ?? target.Name;

			if (target.FullName == null && !target.IsGenericParameter) { signature = target.Namespace + "." + signature; }

			if (target.IsNested && fullSignature)
			{
				signature = target.DeclaringType.AsSignature(true) + "+" + target.Name;
			}

			try
			{
				if (target.GetGenericArguments().Length > 0)
				{
					// var fullsignature = target.GetGenericTypeDefinition().FullName;

					signature = signature.Substring(0, signature.IndexOf("`"));

					signature += "{";
					foreach (var gt in target.GetGenericArguments().Where(a => !fullSignature || a.DeclaringType == target))
					{
						if (target.IsNested && HasGeneric(target.DeclaringType, gt) && fullSignature)
							continue;
						if (!signature.EndsWith("{"))
						{
							signature += ",";
						}
						if (gt.IsGenericParameter)
						{
							signature += gt.Name;
						}
						else
						{
							signature += gt.FullName;
						}
					}
					signature += "}";


					if (signature.EndsWith("{}"))
					{
						signature = signature.Substring(0, signature.Length - 2);
					}
				}
			}
			catch
			{
				// Silently ignored
			}

			if (target.IsArray)
			{
				signature += "[]";
			}

			return signature;
		}

		/// <summary>
		/// Returns a PostSharp compatible type reference representation
		/// 
		/// Example: Namespace.ClassType::SomeProperty() or Namespace.ClassType::SomeField
		/// </summary>
		public static string AsSignature(this LocationInfo target)
		{
			return (target.FieldInfo != null) ? target.FieldInfo.AsSignature() : target.PropertyInfo.AsSignature();
		}

		/// <summary>
		/// Returns a PostSharp compatible property reference representation
		/// 
		/// Example: Namespace.ClassType::SomeProperty()
		/// </summary>
		public static string AsSignature(this PropertyInfo target)
		{
			return AsSignature(target.DeclaringType) + "::" + target.Name + "()";
		}

		/// <summary>
		/// Returns a PostSharp compatible field reference representation
		/// 
		/// Example: Namespace.ClassType::SomeField
		/// </summary>
		public static string AsSignature(this FieldInfo target)
		{
			return AsSignature(target.DeclaringType) + "::" + target.Name;
		}

		/// <summary>
		/// Returns a PostSharp compatible type reference representation. 
		/// Outputs the full signature, minus the return type. 
		/// 
		/// If you need the return type also @ the signature, just prefix it with the AsSignature of the return type (and a space).
		/// 
		/// <example>
		/// Usage: string reference = someMethodInfo.AsPSReference();
		/// 
		/// Example output: Namespace.DeclaringType::Method{T1, T2}(T1, T2, System.Action{T,T2}, System.Int32)
		/// </example>
		/// </summary>
		public static string AsSignature(this MethodBase target)
		{
			return AsSignature(target, false);
		}

		/// <summary>
		/// Turns .ctor into #ctor
		/// </summary>
		private static string GetSafeName(this string name)
		{
			if (name.StartsWith("."))
			{
				return "#" + name.Substring(1);
			}

			return name;
		}

		/// <summary>
		/// Returns a PostSharp compatible type reference representation. 
		/// Outputs the full signature, and optionally (if it has any) the return type.
		/// <example>
		/// Usage: string reference = someMethodInfo.AsPSReference();
		/// 
		/// Example output: Namespace.DeclaringType::Method{T1, T2}(T1, T2, System.Action{T,T2}, System.Int32)
		/// </example>
		/// </summary>
		public static string AsSignature(this MethodBase target, bool includeReturnType)
		{
			var methodSignature = AsSignature(target.DeclaringType) + "::" + target.Name.GetSafeName();

			try
			{
				if (target.GetGenericArguments().Length > 0)
				{
					methodSignature += "{";
					foreach (var gt in target.GetGenericArguments())
					{
						if (!methodSignature.EndsWith("{"))
						{
							methodSignature += ", ";
						}
						methodSignature += gt.Name;
					}
					methodSignature += "}";
				}
			}
			catch
			{
				// Silently ignored
			}

			methodSignature += "(";

			foreach (var a in target.GetParameters())
			{
				if (!methodSignature.EndsWith("("))
				{
					methodSignature += ", ";
				}

				methodSignature += AsSignature(a.ParameterType, false);
			}
			methodSignature += ")";

			if (includeReturnType && (target as MethodInfo) != null)
			{
				methodSignature = AsSignature((target as MethodInfo).ReturnType) + " " + methodSignature;
			}

			return methodSignature;
		}
	}
}
